﻿using Microsoft.AspNetCore.Http;
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
        private List<string> Results { get; set; } = new();
        public Thread? CurrentThread { get; private set; }
        public SemaphoreSlim? Semaphore { get; private set; }
        public string? TypeName { get; set; }
        public string? MethodName { get; set; }
        public string? Route { get; set; }
        public ApiAction? ApiAction { get; set; }
        public JObject? Parameters { get; set; }
        public CultureInfo? CultureInfo { get; set; }
        public Guid SessionId { get; set; }
        public string? Address { get; set; }
        public bool SessionOwner { get; set; } = true;
        public bool IsWebClient { get; set; } = false;
        public bool Finished { get; private set; } = false;
        public Exception? Exception { get; private set; }
        public bool IsApiRequest { get; set; } = false;

        public void Execute()
        {
            CurrentThread = new Thread(new ThreadStart(Work));
            Semaphore = new SemaphoreSlim(0);
            CurrentThread.Start();
        }

        public void Send(object o)
        {
            lock (lockResults)
            {
                Results.Add(JToken.FromObject(o).ToString(Newtonsoft.Json.Formatting.Indented));
                Results.Add(WebDispatcher.Boundary);
            }
            Semaphore!.Release(1);
        }

        public async Task<string[]> GetResults()
        {
            await Semaphore!.WaitAsync();

            lock (lockResults)
            {
                string[] res = Results.ToArray();
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
                    Id = SessionId,
                    Address = Address,
                    CultureInfo = CultureInfo,
                    Type = IsWebClient ? SessionType.WEBCLIENT : SessionType.WEB,
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
                if (ex.InnerException != null)
                    ex = ex.InnerException;

                Results.Add(WebDispatcher.ExceptionToJson(ex));
                Exception = ex;
            }

            try
            {
                if (Session.UserId.Length > 0)
                    SessionOwner = false;

                Session.Stop(SessionOwner, true);
            }
            catch (Exception)
            {
                // do nothing
            }

            Finished = true;
            Semaphore!.Release(1);
        }
    }

    public class WebDispatcher
    {
        internal static string Boundary { get; private set; } = "";

        internal static string ExceptionToJson(Exception ex)
        {
            var res = new JObject();
            res["classname"] = ex.GetType().FullName;
            res["message"] = ex.Message;
            res["type"] = "exception";
            
            res["code"] = 0;
            if (typeof(Error).IsAssignableFrom(ex.GetType()))
                res["code"] = ((Error)ex).ErrorCode;

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
                else
                {
                    trace.Add("method '" + frame.GetMethod()!.Name + "'");
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

        internal static void Initialize()
        {
            // 8k buffer
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 230; i++)
                sb.Append("d4015c7b-152e-492e-8e8b-2021248db290");
            Boundary = "\"" + sb.ToString() + "\"";
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
                if (Application.InMaintenance)
                    throw new Error(Error.E_SYSTEM_IN_MAINTENANCE, Label("Application is in maintenance, try again later"));

                task = new();
                task.Address = ctx.Connection.RemoteIpAddress!.ToString();

                if (ctx.Request.Headers.ContainsKey("Accept-Language"))
                    task.CultureInfo = TryGetCulture(ctx.Request.Headers["Accept-Language"]!);

                if (ctx.Request.Headers.ContainsKey("X-Rpc-WebClient") && (ctx.Request.Headers["X-Rpc-WebClient"] == "1"))
                    task.IsWebClient = true;
                else
                    task.IsWebClient = false;

                if (ctx.Request.Headers.ContainsKey("X-Session-Id"))
                {
                    task.SessionId = Guid.Parse(ctx.Request.Headers["X-Session-Id"]!);
                    task.SessionOwner = false;
                }
                else if (ctx.Request.Headers.ContainsKey("Authorization") && 
                    ctx.Request.Headers["Authorization"].ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    task.SessionId = Guid.Parse(ctx.Request.Headers["Authorization"].ToString().Substring(7));
                    task.SessionOwner = false;
                }
                else
                {
                    task.SessionId = Guid.NewGuid();
                    task.SessionOwner = true;
                }

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
                            throw new Error(Label("Invalid API action '{0}'"), ctx.Request.Method);
                    }
                }
                else
                {
                    // rpc
                    try
                    {
                        if (body!["type"]!.ToString() == "request")
                        {
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
                bool isArray = false;
                bool comma = false;
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                while (!task.Finished)
                {
                    string[] res = await task.GetResults();

                    if ((res.Length > 1) && (!isArray))
                    {
                        await ctx.Response.WriteAsync("[");
                        isArray = true;
                    }

                    foreach (var r in res)
                    {
                        if ((task.Exception != null) && (!isArray) && (!comma))
                            ctx.Response.StatusCode = ErrorToStatusCode(task.Exception);

                        if (comma) await ctx.Response.WriteAsync(",");
                        await ctx.Response.WriteAsync(r);
                        comma = true;
                    }
                }

                if (isArray)
                    await ctx.Response.WriteAsync("]");
            }
            catch
            {
                // do nothing
            }
        }
    }
}
