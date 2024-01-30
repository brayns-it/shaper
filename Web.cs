using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using Brayns.Shaper.Loader;
using System.Globalization;

namespace Brayns.Shaper
{
    internal class WebTask
    {
        private object lockResults = new object();
        private List<object?> Results { get; set; } = new();
        public Thread? CurrentThread { get; private set; }
        public SemaphoreSlim? Semaphore { get; private set; }
        public string? TypeName { get; set; }
        public string? ObjectId { get; set; }
        public string? MethodName { get; set; }
        public string? Route { get; set; }
        public ApiAction? ApiAction { get; set; }
        public JObject? Parameters { get; set; }
        public CultureInfo? CultureInfo { get; set; }
        public Guid SessionId { get; set; }
        public Guid? AuthenticationId { get; set; }
        public string? Address { get; set; }
        public bool IsWebClient { get; set; } = false;
        public bool IsApiRequest { get; set; } = false;

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

        public void Send(ClientMessage msg)
        {
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
                    WebTask = this
                };

                Session.Start(sa);

                object? res = null;
                Proxy proxy;
                if (IsApiRequest)
                {
                    proxy = Proxy.CreateFromRoute(Route!, ApiAction!.Value);
                    res = proxy.Invoke(Parameters);
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
            catch (Exception)
            {
                return null;
            }
        }

        internal static async Task DispatchApi(HttpContext ctx)
        {
            await Dispatch(ctx, true);
        }

        internal static async Task DispatchRpc(HttpContext ctx)
        {
            await Dispatch(ctx, false);
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

        private static async Task Dispatch(HttpContext ctx, bool apiRequest)
        {
            WebTask? task = null;

            try
            {
                task = new();
                task.Address = ctx.Connection.RemoteIpAddress!.ToString();

                if (ctx.Request.Headers.ContainsKey("Accept-Language"))
                    task.CultureInfo = TryGetCulture(ctx.Request.Headers["Accept-Language"]!);

                if (ctx.Request.Headers.ContainsKey("X-Rpc-WebClient") && (ctx.Request.Headers["X-Rpc-WebClient"] == "1"))
                    task.IsWebClient = true;

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

                JObject? body = null;
                if (ctx.Request.ContentLength > 0)
                {
                    StreamReader sr = new(ctx.Request.Body);
                    body = JObject.Parse(await sr.ReadToEndAsync());
                    sr.Close();
                }

                if (apiRequest)
                {
                    // api
                    task.Route = ctx.Request.RouteValues["path"]!.ToString();
                    task.Parameters = body;
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
                }
                else
                {
                    // rpc
                    try
                    {
                        if (body!["type"]!.ToString() == "request")
                        {
                            if (body!.ContainsKey("objectid"))
                                task.ObjectId = body!["objectid"]!.ToString();
                            else
                                task.TypeName = body!["classname"]!.ToString();
                            task.MethodName = body!["method"]!.ToString();
                            task.Parameters = (JObject)body!["arguments"]!;
                        }
                    }
                    catch
                    {
                        throw new Error(Label("Invalid HTTP RPC JSON body"));
                    }
                }

                task.Execute();
            }
            catch (Exception ex)
            {
                // pre task exception
                ctx.Response.StatusCode = ErrorToStatusCode(ex);
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(ExceptionToJson(ex));
            }

            if (task == null)
                return;

            try
            {
                bool bodyWrited = false;
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                while (true)
                {
                    foreach (var r in await task.GetResults())
                    {
                        string? resText = null;
                        bool doFlush = false;

                        switch (r)
                        {
                            case null:
                                if (task.IsWebClient && bodyWrited)
                                    await ctx.Response.WriteAsync("]");
                                return;

                            case ClientMessage msg when msg.Type == ClientMessageType.SetAuthentication:
                                if (!bodyWrited)
                                {
                                    CookieOptions opt = new();
                                    opt.Expires = msg.Expires;
                                    ctx.Response.Cookies.Append("X-Authorization", msg.Value, opt);
                                }
                                continue;

                            case ClientMessage msg when msg.Type == ClientMessageType.ClearAuthentication:
                                if (!bodyWrited)
                                    ctx.Response.Cookies.Delete("X-Authorization");
                                continue;

                            case ClientMessage msg when msg.Type == ClientMessageType.Boundary:
                                resText = _boundary;
                                doFlush = true;
                                break;

                            case ClientMessage msg when msg.Type == ClientMessageType.Message:
                                resText = msg.Value;
                                break;

                            case Exception ex:
                                if (!bodyWrited) ctx.Response.StatusCode = ErrorToStatusCode(ex);
                                resText = ExceptionToJson(ex);
                                break;

                            case string str:
                                resText = str;
                                break;

                            default:
                                throw new Error(Label("Unhandled type in web loop {0}", r.GetType()));
                        }

                        if (task.IsWebClient)
                            if (bodyWrited)
                                await ctx.Response.WriteAsync(",");
                            else
                                await ctx.Response.WriteAsync("[");

                        if (resText != null)
                        {
                            await ctx.Response.WriteAsync(resText);
                            bodyWrited = true;
                        }
                        if (doFlush) await ctx.Response.Body.FlushAsync();
                    }
                }
            }
            catch
            {
                // do nothing
            }
        }
    }
}
