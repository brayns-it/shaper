using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using Brayns.Shaper.Loader;
using System.Globalization;
using Microsoft.AspNetCore.Routing;
using System.Net.WebSockets;

namespace Brayns.Shaper
{
    internal class TaskResult
    {
        internal bool Empty { get; set; } = false;
        internal bool Finished { get; set; } = false;
        internal object? Result { get; set; } = null;
        internal bool IsRequest { get; set; } = false;
        internal bool IsCancelation { get; set; } = false;
    }

    public class ApiResult
    {
        public string ContentType { get; set; } = "application/json";
        public byte[] Content { get; set; } = new byte[0];
        public int StatusCode { get; set; } = 200;
    }

    public class RawSession
    {
        public Dictionary<string, string> RouteParts { get; internal set; } = new();

        public byte[]? Request { get; set; }
        public string? RequestType { get; set; }
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public Dictionary<string, string> RequestQuery { get; set; } = new();
        public RequestMethod RequestMethod { get; set; }
        public string RequestPath { get; set; } = "";
        public string RequestPathWithQuery { get; set; } = "";
        public string RequestURI { get; set; } = "";
        public string RequestBaseURI { get; set; } = "";

        public byte[]? Response { get; set; }
        public string ResponseType { get; set; } = "text/plain";
        public int ResponseCode { get; set; } = 200;
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    }

    internal class WebTask
    {
        private object lockResults = new object();
        private List<TaskResult> Results { get; set; } = new();
        private Thread? CurrentThread { get; set; }
        private SemaphoreSlim? Semaphore { get; set; }

        internal string? TypeName { get; set; }
        internal string? ObjectId { get; set; }
        internal string? MethodName { get; set; }
        internal string Route { get; set; } = "";
        internal RequestMethod HttpMethod { get; set; }
        internal JObject Parameters { get; set; } = new();

        internal RawSession RawSession { get; set; } = new();
        internal string RouteName { get; set; } = "";

        internal CultureInfo? CultureInfo { get; set; }
        internal Guid SessionId { get; set; }
        internal string? AuthenticationId { get; set; }
        internal string? Address { get; set; }

        internal WebSocket? WebSocket { get; set; }
        internal bool IsWebClient { get; set; } = false;
        internal bool IsApiRequest { get; set; } = false;
        internal bool IsRawRequest { get; set; } = false;

        internal WebTask()
        {
            SessionId = Guid.NewGuid();
        }

        internal void Execute()
        {
            CurrentThread = new Thread(new ThreadStart(Work));
            Semaphore = new SemaphoreSlim(0);
            CurrentThread.Start();
        }

        internal void Send(ClientMessage msg)
        {
            lock (lockResults)
            {
                Results.Add(new TaskResult() { Result = msg });
            }
            Semaphore!.Release(1);
        }

        internal async Task<TaskResult> GetNextResult()
        {
            TaskResult res;

            lock (lockResults)
            {
                if (Results.Count > 0)
                {
                    res = Results[0];
                    Results.RemoveAt(0);
                    return res;
                }
            }

            await Semaphore!.WaitAsync();

            lock (lockResults)
            {
                if (Results.Count > 0)
                {
                    res = Results[0];
                    Results.RemoveAt(0);
                    return res;
                }
                else
                {
                    return new TaskResult() { Empty = true };
                }
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
                    proxy = Proxy.CreateFromRoute(Route!, HttpMethod);
                    res = proxy.Invoke(Parameters);
                }
                else if (IsRawRequest)
                {
                    proxy = Proxy.CreateFromRawRoute(RouteName!, Route!, RawSession);
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

                lock (lockResults)
                {
                    Results.Add(new TaskResult() { Result = res });
                }

                Commit();
            }
            catch (Exception ex)
            {
                Results.Add(new TaskResult() { Result = ex });
            }

            try
            {
                Session.Stop();
            }
            catch
            {
            }

            Results.Add(new TaskResult() { Finished = true });
            Semaphore!.Release(1);
        }
    }

    public class WebDispatcher
    {
        private static string ObjectToJson(object? o, bool addResponseType = false, string? requestId = null)
        {
            JObject jo;
            if (o == null)
            {
                jo = new();
                jo["value"] = null;
            }
            else
            {
                var jt = JToken.FromObject(o);
                if (jt.Type == JTokenType.Object)
                    jo = (JObject)jt;
                else
                {
                    jo = new();
                    jo["value"] = jt;
                }
            }
            if (addResponseType)
            {
                jo["type"] = "response";
                if (requestId != null)
                    jo["requestid"] = requestId!;
            }

            return jo.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static string ExceptionToJson(Exception ex, string? requestId = null)
        {
            var fe = new Classes.FormattedException(ex);

            var res = new JObject();
            res["classname"] = fe.Type.FullName;
            res["message"] = fe.Message;
            res["type"] = "exception";
            if (requestId != null)
                res["requestid"] = requestId!;

            res["code"] = fe.ErrorCode;
            res["sourceId"] = fe.SourceId;
            res["trace"] = JArray.FromObject(fe.Trace);

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

        private static void LogException(Exception ex, HttpContext ctx, WebTask? task = null)
        {
            try
            {
                string message = ctx.Request.Method + " " + ctx.Request.Path + " " + ctx.Connection.RemoteIpAddress!.ToString() + " " + ctx.Connection.Id;
                Application.LogException("webdspch", message, ex);

                if ((task != null) && (ctx.Request.ContentLength > 0))
                {
                    FileStream fs = new FileStream(Application.RootPath + "var/log/request_" + DateTime.Now.ToString("yyyyMMdd") + "_" + ctx.Connection.Id + ".txt", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    if (task.RawSession.Request != null)
                        fs.Write(task.RawSession.Request, 0, task.RawSession.Request!.Length);
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

        private static async Task<WebTask> ParseRequest(HttpContext ctx, bool isRawRequest)
        {
            WebTask task = new();
            task.Address = ctx.Connection.RemoteIpAddress!.ToString();

            if (ctx.Request.Headers.ContainsKey("Accept-Language"))
                task.CultureInfo = TryGetCulture(ctx.Request.Headers["Accept-Language"]!);

            if (ctx.Request.RouteValues.ContainsKey("path") && (ctx.Request.RouteValues["path"] != null))
            {
                string[] segs = ctx.Request.RouteValues["path"]!.ToString()!.Split('/', StringSplitOptions.RemoveEmptyEntries);
                task.Route = string.Join('/', segs);
            }

            switch (ctx.Request.Method)
            {
                case "POST":
                    task.HttpMethod = RequestMethod.Post;
                    break;
                case "PUT":
                    task.HttpMethod = RequestMethod.Put;
                    break;
                case "HEAD":
                case "GET":
                    task.HttpMethod = RequestMethod.Get;
                    break;
                case "DELETE":
                    task.HttpMethod = RequestMethod.Delete;
                    break;
            }

            if (isRawRequest)
            {
                MemoryStream ms = new();
                await ctx.Request.Body.CopyToAsync(ms);
                task.RawSession.Request = ms.ToArray();
                ms.Close();

                foreach (string hk in ctx.Request.Headers.Keys)
                    task.RawSession.RequestHeaders.Add(hk, ctx.Request.Headers[hk].First()!);

                task.RawSession.RequestType = ctx.Request.ContentType;
                task.RawSession.RequestPath = ctx.Request.Path;
                task.RawSession.RequestPathWithQuery = ctx.Request.Path + ((ctx.Request.QueryString.HasValue) ? ctx.Request.QueryString : "");
                task.RawSession.RequestURI = ctx.Request.Scheme + "://" + ctx.Request.Host + ctx.Request.Path;
                task.RawSession.RequestMethod = task.HttpMethod;

                task.RawSession.RequestBaseURI = task.RawSession.RequestURI.Trim();
                if (task.RawSession.RequestBaseURI.EndsWith("/")) task.RawSession.RequestBaseURI = task.RawSession.RequestBaseURI.Substring(0, task.RawSession.RequestBaseURI.Length - 1);
                int n = task.RawSession.RequestBaseURI.LastIndexOf("/");
                if (n > -1) task.RawSession.RequestBaseURI = task.RawSession.RequestBaseURI.Substring(0, n);
                if (!task.RawSession.RequestBaseURI.EndsWith("/")) task.RawSession.RequestBaseURI += "/";

                foreach (string k in ctx.Request.Query.Keys)
                    if (!task.RawSession.RequestQuery.ContainsKey(k))
                        task.RawSession.RequestQuery.Add(k, ctx.Request.Query[k].First()!);

                RouteNameMetadata? md = ctx.GetEndpoint()!.Metadata.GetMetadata<RouteNameMetadata>();
                if ((md != null) && (md.RouteName != null)) task.RouteName = md.RouteName;
            }
            else
            {
                if (ctx.Request.Headers.ContainsKey("Authorization") &&
                    ctx.Request.Headers["Authorization"].ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    task.AuthenticationId = ctx.Request.Headers["Authorization"].ToString().Substring(7);

                else if (ctx.Request.Cookies.ContainsKey("X-Authorization"))
                    task.AuthenticationId = ctx.Request.Cookies["X-Authorization"]!;

                if ((ctx.Request.ContentLength > 0) && ctx.Request.HasJsonContentType())
                {
                    StreamReader sr = new(ctx.Request.Body);
                    task.Parameters = JObject.Parse(await sr.ReadToEndAsync());
                    sr.Close();
                }

                foreach (string k in ctx.Request.Query.Keys)
                {
                    if (task.Parameters.ContainsKey(k))
                        continue;

                    string val = ctx.Request.Query[k].First()!;
                    val = "\"" + val.Replace("\"", "\\\"") + "\"";
                    task.Parameters.Add(k, JValue.Parse(val));
                }
            }

            return task;
        }

        private static async Task WaitRawForEnd(WebTask task)
        {
            while (true)
            {
                var r = await task.GetNextResult();
                if (r.Finished) return;
                if (r.Empty) continue;

                switch (r.Result)
                {
                    case null:  // return void allowed          
                        return;

                    case Exception ex:
                        throw ex;

                    default:
                        throw new Error(Label("Unhandled type in raw web loop {0}", r.Result.GetType()));
                }
            }
        }

        internal static async Task DispatchRaw(HttpContext ctx)
        {
            WebTask? task = null;

            try
            {
                task = await ParseRequest(ctx, true);
                task.IsRawRequest = true;
                task.Execute();

                await WaitRawForEnd(task);

                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = task.RawSession.ResponseCode;
                    ctx.Response.ContentType = task.RawSession.ResponseType;
                    if (task.RawSession.Response != null)
                        ctx.Response.ContentLength = task.RawSession.Response.Length;
                    foreach (string hk in task.RawSession.ResponseHeaders.Keys)
                        ctx.Response.Headers.Append(hk, task.RawSession.ResponseHeaders[hk]);
                }

                if (ctx.Request.Method != "HEAD")
                    if (task.RawSession.Response != null)
                        await ctx.Response.Body.WriteAsync(task.RawSession.Response, 0, task.RawSession.Response.Length);
            }
            catch (Exception ex)
            {
                if (!ctx.Response.HasStarted)
                    ctx.Response.StatusCode = 500;

                LogException(ex, ctx, task);
            }
        }

        private static async Task WriteApiDirectResult(HttpContext ctx, ApiResult result)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.ContentType = result.ContentType;
                ctx.Response.StatusCode = result.StatusCode;
                if ((result.Content != null) && (result.Content.Length > 0))
                    ctx.Response.ContentLength = result.Content.Length;
            }

            if (ctx.Request.Method != "HEAD")
                if ((result.Content != null) && (result.Content.Length > 0))
                    await ctx.Response.Body.WriteAsync(result.Content, 0, result.Content.Length);
        }

        private static async Task WriteApiResponse(HttpContext ctx, object? obj)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
            }

            await ctx.Response.WriteAsync(ObjectToJson(obj));
        }

        private static async Task WriteApiException(HttpContext ctx, Exception ex, bool errorReturns200)
        {
            try
            {
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.StatusCode = errorReturns200 ? 200 : ErrorToStatusCode(ex);
                }

                await ctx.Response.WriteAsync(ExceptionToJson(ex));
            }
            catch
            {
            }
        }

        internal static async Task DispatchApi(HttpContext ctx)
        {
            WebTask? task = null;
            bool errorReturns200 = false;

            try
            {
                if (ctx.Request.Headers.ContainsKey("X-Error-Returns200") && (ctx.Request.Headers["X-Error-Returns200"] == "1"))
                    errorReturns200 = true;

                task = await ParseRequest(ctx, false);
                task.IsApiRequest = true;
                task.Execute();
            }
            catch (Exception ex)
            {
                await WriteApiException(ctx, ex, errorReturns200);
                LogException(ex, ctx, task);
                return;
            }

            try
            {
                while (true)
                {
                    var r = await task.GetNextResult();
                    if (r.Finished) return;
                    if (r.Empty) continue;

                    switch (r.Result)
                    {
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

                        case Exception ex:
                            throw ex;

                        case ApiResult ar:
                            await WriteApiDirectResult(ctx, ar);
                            break;

                        default:
                            await WriteApiResponse(ctx, r.Result);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await WriteApiException(ctx, ex, errorReturns200);
                LogException(ex, ctx, task);
            }
        }

        private static async Task WriteRpcException(WebSocket ws, Exception ex, string? requestId)
        {
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(ExceptionToJson(ex, requestId))), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
            }
        }

        private static async Task WriteRpcMessage(WebSocket ws, ClientMessage msg)
        {
            JObject jo;

            if (msg.GetType() == typeof(ClientDirectMessage))
            {
                var dm = (ClientDirectMessage)msg;
                if (dm.Value != null)
                    jo = JObject.FromObject(dm.Value);
                else
                    jo = new();
                jo["type"] = "send";
            }
            else
            {
                jo = JObject.FromObject(msg);
                jo["type"] = msg.GetType().Name!.ToLower();
            }

            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jo.ToString(Newtonsoft.Json.Formatting.Indented))), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task WriteRpcResponse(WebSocket ws, object? obj, string? requestId)
        {
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(ObjectToJson(obj, true, requestId))), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<TaskResult> ReceiveMessage(WebSocket ws)
        {
            TaskResult result = new();
            result.IsRequest = true;
            
            var buf = new byte[1024];
            var ms = new MemoryStream();
            while ((!ws.CloseStatus.HasValue) && (!Application.Shutdown))
            {
                var rcv = await ws.ReceiveAsync(new ArraySegment<byte>(buf), Application.ShutdownCancellation);
                if (rcv.CloseStatus.HasValue) break;
                if (Application.Shutdown) break;

                ms.Write(buf, 0, rcv.Count);
                if (rcv.EndOfMessage)
                {
                    var jo = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));
                    result.Result = jo;

                    if (jo.ContainsKey("type") && (jo["type"]!.ToString() == "cancel"))
                        result.IsCancelation = true;

                    return result;
                }
            }

            result.Finished = true;
            return result;
        }

        internal static async Task DispatchRpc(HttpContext ctx)
        {
            var sessionId = Guid.NewGuid();

            try
            {
                if (ctx.WebSockets.IsWebSocketRequest)
                {
                    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                    WebTask? task = null;
                    string? requestId = null;
                    List<TaskResult> requests = new();
                    Task<TaskResult>? reqTask = null;

                    while ((!ws.CloseStatus.HasValue) && (!Application.Shutdown))
                    {
                        TaskResult req;

                        if (requests.Count > 0)
                        {
                            req = requests[0];
                            requests.RemoveAt(0);
                        }
                        else
                        {
                            if (reqTask == null) reqTask = ReceiveMessage(ws);
                            req = await reqTask;
                            reqTask = null;
                        }

                        if (req.Finished) break;
                        if (req.IsCancelation) continue;

                        try
                        {
                            task = await ParseRequest(ctx, false);
                            task.IsWebClient = true;
                            task.SessionId = sessionId;

                            var jReq = (JObject)req.Result!;

                            if (jReq.ContainsKey("objectid"))
                                task.ObjectId = jReq["objectid"]!.ToString();
                            else
                                task.TypeName = jReq["classname"]!.ToString();
                            task.MethodName = jReq["method"]!.ToString();
                            task.Parameters = (JObject)jReq["arguments"]!;

                            requestId = null;
                            if (jReq.ContainsKey("requestid"))
                                requestId = jReq["requestid"]!.ToString();

                            task.Execute();

                            Task<TaskResult>? resTask = null;

                            while (!Application.Shutdown)
                            {
                                if (resTask == null) resTask = task.GetNextResult();
                                if (reqTask == null) reqTask = ReceiveMessage(ws);

                                Task<TaskResult> first = await Task.WhenAny(resTask, reqTask);

                                if (first == resTask)
                                {
                                    var res = await first;
                                    resTask = null;

                                    if (res.Empty) continue;
                                    if (res.Finished) break;

                                    switch (res.Result)
                                    {
                                        case Exception ex:
                                            throw ex;

                                        case ClientMessage msg:
                                            await WriteRpcMessage(ws, msg);
                                            break;

                                        default:
                                            await WriteRpcResponse(ws, res.Result, requestId);
                                            break;
                                    }
                                }

                                if (first == reqTask)
                                {
                                    req = await first;
                                    reqTask = null;

                                    if (req.IsCancelation)
                                        Session.Cancel(sessionId);
                                    else
                                        requests.Add(req);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            await WriteRpcException(ws, ex, requestId);
                            LogException(ex, ctx, task);
                        }
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
                }
                else
                {
                    ctx.Response.StatusCode = 426;
                }
            }
            catch (Exception ex)
            {
                LogException(ex, ctx);
            }

            lock (Session._lockSessions)
            {
                Session.Finished.Add(sessionId);
            }
        }
    }
}
