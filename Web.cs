using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using Brayns.Shaper.Loader;
using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace Brayns.Shaper
{
    internal enum HttpContextType
    {
        Api,
        Rpc,
        RawRequest
    }

    public class RawSession
    {
        public byte[]? Request { get; set; }
        public string? RequestType { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public string RequestMethod { get; set; } = "";
        public string RequestPathWithQuery { get; set; } = "";
        public string RequestURI { get; set; } = "";

        public byte[]? Response { get; set; }
        public string ResponseType { get; set; } = "text/plain";
        public int ResponseCode { get; set; } = 200;
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    }

    internal class WebTask
    {
        private object lockResults = new object();
        private List<object?> Results { get; set; } = new();
        private Thread? CurrentThread { get; set; }
        private SemaphoreSlim? Semaphore { get; set; }

        internal string? TypeName { get; set; }
        internal string? ObjectId { get; set; }
        internal string? MethodName { get; set; }
        internal string? Route { get; set; }
        internal ApiAction? ApiAction { get; set; }
        internal JObject Parameters { get; set; } = new();

        internal RawSession RawSession { get; set; } = new();
        internal string? RouteName { get; set; }

        internal CultureInfo? CultureInfo { get; set; }
        internal Guid SessionId { get; set; }
        internal Guid? AuthenticationId { get; set; }
        internal string? Address { get; set; }

        internal bool IsWebClient { get; set; } = false;
        internal bool IsApiRequest { get; set; } = false;
        internal bool IsRawRequest { get; set; } = false;
        internal bool IsCancelation { get; set; } = false;

        internal bool Aborted { get; set; } = false;

        public WebTask()
        {
            SessionId = Guid.NewGuid();
        }

        public void Execute()
        {
            CurrentThread = new Thread(new ThreadStart(Work));
            Semaphore = new SemaphoreSlim(0);
            CurrentThread.Start();
        }

        public void Send(object msg)
        {
            if (Aborted)
                throw new Error(Label("Request aborted"));

            lock (lockResults)
            {
                Results.Add(msg);
            }
            Semaphore!.Release(1);
        }

        public async Task<object?[]> GetResults()
        {
            await Semaphore!.WaitAsync();

            lock (lockResults)
            {
                object?[] res = Results.ToArray();
                Results.Clear();
                return res;
            }
        }

        private void Work()
        {
            try
            {
                SessionArgs sa = new SessionArgs()
                {
                    AuthenticationId = AuthenticationId,
                    Id = SessionId,
                    Address = Address,
                    CultureInfo = CultureInfo,
                    Type = IsWebClient ? SessionTypes.WEBCLIENT : SessionTypes.WEB,
                    WebTask = IsCancelation ? null : this
                };

                Session.Start(sa);

                object? res = null;
                Proxy proxy;
                if (IsApiRequest)
                {
                    proxy = Proxy.CreateFromRoute(Route!, ApiAction!.Value);
                    res = proxy.Invoke(Parameters);
                }
                else if (IsRawRequest)
                {
                    proxy = Proxy.CreateFromRawRoute(RouteName!, Route!);
                    res = proxy.Invoke(RawSession);
                }
                else
                {
                    if (ObjectId != null)
                        proxy = Proxy.CreateFromId(ObjectId!);
                    else
                        proxy = Proxy.CreateFromName(TypeName!);
                    res = proxy.Invoke(MethodName!, Parameters!);
                }

                if (res != null)
                {
                    lock (lockResults)
                    {
                        JObject jo;
                        if (IsWebClient)
                        {
                            jo = new JObject();
                            jo["type"] = "response";
                            jo["value"] = JToken.FromObject(res);
                        }
                        else
                        {
                            var jv = JToken.FromObject(res);
                            if (jv.Type == JTokenType.Object)
                                jo = (JObject)jv;
                            else
                            {
                                jo = new JObject();
                                jo[proxy.ResultName] = jv;
                            }
                        }
                        Results.Add(jo.ToString(Newtonsoft.Json.Formatting.Indented));
                    }
                }

                Commit();
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;

                Results.Add(ex);
            }

            try
            {
                Session.Stop();
            }
            catch
            {
            }

            Results.Add(null);
            Semaphore!.Release(1);
        }
    }

    public class WebDispatcher
    {
        private static readonly string _boundary;

        static WebDispatcher()
        {
            // 8k buffer
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 230; i++)
                sb.Append("d4015c7b-152e-492e-8e8b-2021248db290");
            _boundary = "\"" + sb.ToString() + "\"";
        }

        private static string ExceptionToJson(Exception ex)
        {
            var res = new JObject();
            res["classname"] = ex.GetType().FullName;
            res["message"] = ex.Message;
            res["type"] = "exception";

            res["code"] = 0;
            if (typeof(Error).IsAssignableFrom(ex.GetType()))
            {
                var err = (Error)ex;
                res["code"] = err.ErrorCode;
                if (err.SourceId.Length > 0)
                    res["sourceId"] = err.SourceId;
            }

            var trace = new List<string>();

            var st = new StackTrace(ex, true);
            var frames = st.GetFrames();
            foreach (var frame in frames)
            {
                string? fn = frame.GetFileName();
                if (fn != null)
                {
                    FileInfo fi = new FileInfo(fn);
                    trace.Add("in '" + fi.Name + "' line " + frame.GetFileLineNumber() + " method '" + frame.GetMethod()!.Name + "'");
                }
            }

            res["trace"] = JArray.FromObject(trace);

            return res.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static CultureInfo? TryGetCulture(string acceptLanguage)
        {
            try
            {
                CultureInfo? res = null;
                string[] langs = acceptLanguage.Split(";");
                string[] parts = langs[0].Split(",");
                foreach (string p in parts)
                {
                    if (p.StartsWith("q=")) continue;
                    if (p == "*") continue;
                    res = CultureInfo.CreateSpecificCulture(p);
                }
                return res;
            }
            catch
            {
                return null;
            }
        }

        private static void LogException(Exception ex, HttpContext ctx, HttpContextType type, WebTask? task)
        {
            if (type == HttpContextType.Rpc) return;

            try
            {
                string message = ctx.Request.Method + " " + ctx.Request.Path + " " + ctx.Connection.RemoteIpAddress!.ToString() + " " + ctx.Connection.Id;
                Application.LogException("webdspch", message, ex);

                if ((task != null) && (ctx.Request.ContentLength > 0))
                {
                    FileStream fs = new FileStream(Application.RootPath + "var/log/request_" + DateTime.Now.ToString("yyyyMMdd") + "_" + ctx.Connection.Id + ".txt", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    if (type == HttpContextType.RawRequest)
                        fs.Write(task.RawSession.Request!, 0, task.RawSession.Request!.Length);
                    else
                    {
                        byte[] buf = Encoding.UTF8.GetBytes(task.Parameters.ToString());
                        fs.Write(buf, 0, buf.Length);
                    }
                    fs.Close();
                }
            }
            catch
            {
                // do nothing
            }
        }

        private static int ErrorToStatusCode(Exception ex)
        {
            Error? e = ex as Error;
            if (e != null)
            {
                if (e.ErrorCode == Error.E_UNAUTHORIZED) return 401;
                if (e.ErrorCode == Error.E_INVALID_ROUTE) return 404;
                if (e.ErrorCode == Error.E_SYSTEM_IN_MAINTENANCE) return 503;
            }
            return 500;
        }

        private static void ParamsFromQuery(JObject body, IQueryCollection query)
        {
            foreach (string k in query.Keys)
            {
                if (body.ContainsKey(k))
                    continue;

                string val = query[k].First()!;
                val = "\"" + val.Replace("\"", "\\\"") + "\"";
                body.Add(k, JValue.Parse(val));
            }
        }

        internal static async Task Dispatch(HttpContext ctx, HttpContextType type)
        {
            WebTask? task = null;
            bool errorReturns200 = false;

            try
            {
                task = new();
                task.Address = ctx.Connection.RemoteIpAddress!.ToString();

                if (ctx.Request.Headers.ContainsKey("Accept-Language"))
                    task.CultureInfo = TryGetCulture(ctx.Request.Headers["Accept-Language"]!);

                if (ctx.Request.Headers.ContainsKey("X-Error-Returns200") && (ctx.Request.Headers["X-Error-Returns200"] == "1"))
                    errorReturns200 = true;

                if (ctx.Request.Headers.ContainsKey("X-Rpc-SessionId") && (ctx.Request.Headers["X-Rpc-SessionId"].ToString().Length > 0))
                {
                    task.SessionId = Guid.Parse(ctx.Request.Headers["X-Rpc-SessionId"]!);
                    if (!Session.SessionData.ContainsKey(task.SessionId))
                        throw new Error(Error.E_INVALID_SESSION, Label("Invalid session"));
                }

                if (ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Headers["Authorization"].ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    task.AuthenticationId = Guid.Parse(ctx.Request.Headers["Authorization"].ToString().Substring(7));

                else if (ctx.Request.Cookies.ContainsKey("X-Authorization"))
                    task.AuthenticationId = Guid.Parse(ctx.Request.Cookies["X-Authorization"]!);

                if (ctx.Request.ContentLength > 0)
                {
                    if (type == HttpContextType.RawRequest)
                    {
                        MemoryStream ms = new();
                        await ctx.Request.Body.CopyToAsync(ms);
                        task.RawSession.Request = ms.ToArray();
                        ms.Close();
                    }
                    else
                    {
                        StreamReader sr = new(ctx.Request.Body);
                        task.Parameters = JObject.Parse(await sr.ReadToEndAsync());
                        sr.Close();
                    }
                }

                string normalizedPath = "";
                if (type != HttpContextType.Rpc)
                {
                    string[] segs = ctx.Request.RouteValues["path"]!.ToString()!.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    normalizedPath = string.Join('/', segs);
                }

                switch (type)
                {
                    case HttpContextType.Api:
                        ParamsFromQuery(task.Parameters, ctx.Request.Query);

                        task.Route = normalizedPath;
                        task.IsApiRequest = true;

                        switch (ctx.Request.Method)
                        {
                            case "POST":
                                task.ApiAction = Classes.ApiAction.Create;
                                break;
                            case "PUT":
                                task.ApiAction = Classes.ApiAction.Update;
                                break;
                            case "GET":
                                task.ApiAction = Classes.ApiAction.Read;
                                break;
                            case "DELETE":
                                task.ApiAction = Classes.ApiAction.Delete;
                                break;
                            default:
                                throw new Error(Label("Invalid API action '{0}'", ctx.Request.Method));
                        }
                        break;

                    case HttpContextType.Rpc:
                        if (ctx.Request.Headers.ContainsKey("X-Rpc-WebClient") && (ctx.Request.Headers["X-Rpc-WebClient"] == "1"))
                            task.IsWebClient = true;

                        if (ctx.Request.Headers.ContainsKey("X-Rpc-Cancelation") && (ctx.Request.Headers["X-Rpc-Cancelation"] == "1"))
                            task.IsCancelation = true;

                        try
                        {
                            if (task.Parameters["type"]!.ToString() == "request")
                            {
                                if (task.Parameters.ContainsKey("objectid"))
                                    task.ObjectId = task.Parameters["objectid"]!.ToString();
                                else
                                    task.TypeName = task.Parameters["classname"]!.ToString();
                                task.MethodName = task.Parameters["method"]!.ToString();
                                task.Parameters = (JObject)task.Parameters["arguments"]!;
                            }
                        }
                        catch
                        {
                            throw new Error(Label("Invalid HTTP RPC JSON body"));
                        }
                        break;

                    case HttpContextType.RawRequest:
                        foreach (string hk in ctx.Request.Headers.Keys)
                            task.RawSession.RequestHeaders.Add(hk, ctx.Request.Headers[hk].First()!);

                        task.IsRawRequest = true;
                        task.RawSession.RequestType = ctx.Request.ContentType;
                        task.RawSession.RequestMethod = ctx.Request.Method;
                        task.RawSession.RequestPathWithQuery = ctx.Request.Path + ((ctx.Request.QueryString.HasValue) ? ctx.Request.QueryString : "");
                        task.RawSession.RequestURI = ctx.Request.Scheme + "://" + ctx.Request.Host + ctx.Request.Path;

                        task.Route = normalizedPath;

                        RouteNameMetadata? md = ctx.GetEndpoint()!.Metadata.GetMetadata<RouteNameMetadata>();
                        if (md != null) task.RouteName = md.RouteName;

                        break;
                }

                task.Execute();
            }
            catch (Exception ex)
            {
                // pre task exception
                if (!ctx.Response.HasStarted)
                {
                    if (!errorReturns200)
                        ctx.Response.StatusCode = ErrorToStatusCode(ex);
                    ctx.Response.ContentType = "application/json";
                }
                await ctx.Response.WriteAsync(ExceptionToJson(ex));

                LogException(ex, ctx, type, task);
                return;
            }

            try
            {
                if (task == null)
                    throw new Error("No task created");

                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                }

                while (true)
                {
                    foreach (var r in await task.GetResults())
                    {
                        string? resText = null;
                        bool doFlush = false;

                        switch (r)
                        {
                            case null when task.IsRawRequest:
                                if (!ctx.Response.HasStarted)
                                {
                                    ctx.Response.StatusCode = task.RawSession.ResponseCode;
                                    ctx.Response.ContentType = task.RawSession.ResponseType;
                                    foreach (string hk in task.RawSession.ResponseHeaders.Keys)
                                        ctx.Response.Headers.Append(hk, task.RawSession.ResponseHeaders[hk]);
                                }
                                if (task.RawSession.Response != null)
                                    await ctx.Response.Body.WriteAsync(task.RawSession.Response, 0, task.RawSession.Response.Length);

                                return;

                            case null:
                                if (task.IsWebClient && ctx.Response.HasStarted)
                                    await ctx.Response.WriteAsync("]");
                                return;

                            case ClientMessageAuthentication auth:
                                if (!ctx.Response.HasStarted)
                                {
                                    if (auth.Clear)
                                        ctx.Response.Cookies.Delete("X-Authorization");
                                    else
                                    {
                                        CookieOptions opt = new();
                                        opt.Expires = auth.Expires;
                                        ctx.Response.Cookies.Append("X-Authorization", auth.Token, opt);
                                    }
                                }
                                continue;

                            case ClientMessageBoundary:
                                resText = _boundary;
                                doFlush = true;
                                break;

                            case ClientMessage msg:
                                resText = msg.Value;
                                break;

                            case Exception ex:
                                if ((!ctx.Response.HasStarted) && (!errorReturns200)) ctx.Response.StatusCode = ErrorToStatusCode(ex);
                                resText = ExceptionToJson(ex);
                                LogException(ex, ctx, type, task);
                                break;

                            case string str:
                                resText = str;
                                break;

                            default:
                                throw new Error(Label("Unhandled type in web loop {0}", r.GetType()));
                        }

                        if (task.IsWebClient)
                            if (ctx.Response.HasStarted)
                                await ctx.Response.WriteAsync(",");
                            else
                                await ctx.Response.WriteAsync("[");

                        if (resText != null)
                            await ctx.Response.WriteAsync(resText);

                        if (doFlush)
                            await ctx.Response.Body.FlushAsync();
                    }

                    if (ctx.RequestAborted.IsCancellationRequested)
                    {
                        task.Aborted = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex, ctx, type, task);
            }
        }
    }
}
