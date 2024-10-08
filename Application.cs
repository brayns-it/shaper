using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Brayns.Shaper.Classes;
using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;
using System.Xml;

namespace Brayns.Shaper
{
    public static class Application
    {
        private static readonly object _lockLog = new();
        private static readonly object _lockValues = new();

        internal static List<Type> RequiredTables { get; } = new();
        internal static Thread? MonitorThread { get; set; }
        internal static Dictionary<string, object> Values = new();
        internal static Dictionary<ApiMethod, MethodInfo> Routes { get; } = new();
        internal static Dictionary<RawMethodAttribute, MethodInfo> RawRoutes { get; } = new();
        internal static Dictionary<Guid, AppModule> Apps { get; } = new();
        internal static Config Config { get; set; } = new Config();
        internal static bool IsLoaded { get; private set; } = false;
        internal static bool IsReady { get { return Config.Ready; } }
        internal static CancellationTokenSource ShutdownSource { get; } = new CancellationTokenSource();

        public static string? RootPath { get; internal set; }
        public static event GenericHandler? Initializing;
        public static event GenericHandler? Monitoring;

        public static bool Shutdown { get; internal set; } = false;
        public static CancellationToken ShutdownCancellation
        {
            get { return ShutdownSource.Token; }
        }

        public static List<string> SourcesPath { get; } = new();
        public static Dictionary<ClientAccess, Type> ClientAccesses { get; } = new();

        internal static bool IsFromMaintenanceNetwork()
        {
            if (Config.MaintenanceNetwork.Length == 0) return true;
            if (Session.Address.Length == 0) return false;

            foreach (var s in Config.MaintenanceNetwork.Split(','))
                if (Session.Address.StartsWith(s))
                    return true;

            return false;
        }

        public static string GetEnvironmentName()
        {
            return Config.EnvironmentName;
        }

        private static void SetDebugPath()
        {
#if DEBUG
            SourcesPath.Clear();

            try
            {
                if (Directory.Exists(RootPath! + "code"))
                    SourcesPath.Add(RootPath! + "code");

                var di = new DirectoryInfo(RootPath!);
                foreach (var fi in di.GetFiles("*.csproj"))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(fi.FullName);

                    var nodes = doc.SelectNodes("Project/ItemGroup/ProjectReference");
                    if (nodes != null)
                        foreach (XmlNode nod in nodes)
                        {
                            if ((nod.Attributes == null) || (nod.Attributes["Include"] == null)) continue;
                            var sfi = new FileInfo(RootPath! + nod.Attributes["Include"]!.InnerText);
                            SourcesPath.Add(sfi.DirectoryName!);
                        }

                    nodes = doc.SelectNodes("Project/Import");
                    if (nodes != null)
                        foreach (XmlNode nod in nodes)
                        {
                            if ((nod.Attributes == null) || (nod.Attributes["Project"] == null)) continue;
                            var sfi = new FileInfo(RootPath! + nod.Attributes["Project"]!.InnerText);
                            SourcesPath.Add(sfi.DirectoryName!);
                        }
                }
            }
            catch
            {
            }
#endif
        }

        private static void InitializeFromWebRoot(string rootPath)
        {
            if (!Directory.Exists(rootPath))
                throw new Error(Label("Root path '{0}' does not exists", rootPath));

            RootPath = rootPath;
            RootPath = RootPath.Replace("\\", "/");
            if (!RootPath.EndsWith("/"))
                RootPath += "/";

            SetDebugPath();

            var di = new DirectoryInfo(RootPath + "var");
            if (!di.Exists)
                di.Create();

            di = new DirectoryInfo(RootPath + "var/log");
            if (!di.Exists)
                di.Create();

            di = new DirectoryInfo(RootPath + "var/temp");
            if (!di.Exists)
                di.Create();

            Loader.Loader.LoadConfig();
            Loader.Loader.LoadAppsFromDomain();

            try
            {
                Initialize();
            }
            finally
            {
                Session.Stop();
            }
        }

        private static void MonitorWork()
        {
            bool sessionStarted = false;
            bool restart = false;

            while (!Shutdown)
            {
                if (IsLoaded && IsReady && (!sessionStarted))
                    try
                    {
                        Session.Start(new SessionArgs()
                        {
                            Id = Guid.NewGuid(),
                            Type = SessionTypes.SYSTEM
                        });
                        Session.IsSuperuser = true;
                        sessionStarted = true;
                    }
                    catch (Exception ex)
                    {
                        LogException("monitorw", ex);
                    }

                if ((((!IsLoaded) || (!IsReady)) && sessionStarted) || restart)
                    try
                    {
                        restart = false;
                        sessionStarted = false;
                        Session.Stop();
                    }
                    catch (Exception ex)
                    {
                        LogException("monitorw", ex);
                    }

                try
                {
                    if (sessionStarted)
                    {
                        // session cleanup
                        Session.CleanupFinished();

                        // monitoring
                        Monitoring?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    restart = true;
                    LogException("monitorw", ex);
                }

                if (restart)
                    Thread.Sleep(5000);
                else
                    Thread.Sleep(1000);
            }

            if (sessionStarted)
                Session.Stop();
        }

        internal static void Initialize()
        {
            IsLoaded = false;

            if (Config.Ready)
            {
                Session.DatabaseConnect();

                Loader.Loader.InitializeApps();
                Loader.Loader.CompileTables(Database.DatabaseCompileMode.Normal);
                Loader.Loader.CollectTableRelations();
                Loader.Loader.CollectEndpoints();
                Loader.Loader.InstallApps();

                Initializing?.Invoke();
                Commit();
            }

            IsLoaded = true;
        }

        internal static Exception ErrorInMaintenance()
        {
            return new Error(Error.E_SYSTEM_IN_MAINTENANCE, Label("Application is in maintenance, try again later"));
        }

        public static void RequireTable<T>()
        {
            if (!RequiredTables.Contains(typeof(T)))
                RequiredTables.Add(typeof(T));
        }

        public static void InitializeShaper()
        {
            Loader.Loader.LoadAppsFromDomain();

            try
            {
                Initialize();
            }
            finally
            {
                Session.Stop();
            }
        }

        public static void InitializeShaper(this WebApplicationBuilder builder)
        {
            try
            {
                InitializeFromWebRoot(builder.Environment.ContentRootPath);
            }
            catch (Exception ex)
            {
                LogException("initroot", ex);
            }
        }

        public static void UseShaperMonitor(this WebApplication app)
        {
            MonitorThread = new Thread(new ThreadStart(MonitorWork));
            MonitorThread.Start();

            app.Lifetime.ApplicationStopping.Register(() =>
            {
                Shutdown = true;
                ShutdownSource.Cancel();
            });
        }

        public static void MapShaperRawRequest(this WebApplication app, string name, string prefix)
        {
            if ((prefix.Length > 1) && prefix.StartsWith("/")) prefix = prefix.Substring(1);
            if ((prefix.Length > 1) && prefix.EndsWith("/")) prefix = prefix.Substring(0, prefix.Length - 1);
            if (prefix == "/") prefix = "";

            app.MapMethods("/" + prefix + "/{**path}", new string[] { "HEAD", "GET", "POST", "PUT", "DELETE" },
                async ctx => await WebDispatcher.DispatchRaw(ctx)).WithName(name);
        }

        public static void MapShaperApi(this WebApplication app)
        {
            // rest entry point
            app.MapMethods("/api/{**path}", new string[] { "HEAD", "GET", "POST", "PUT", "DELETE" }, async ctx => await WebDispatcher.DispatchApi(ctx));
            app.MapMethods("/rpc/{**path}", new string[] { "HEAD", "GET", "POST", "PUT", "DELETE" }, async ctx => await WebDispatcher.DispatchApi(ctx));

            // client entry point
            app.MapGet("/rpc", async ctx => await WebDispatcher.DispatchRpc(ctx));
        }

        internal static void SetValue(string key, object value)
        {
            lock (_lockValues)
            {
                Values[key] = value;
            }
        }

        internal static void DelValue(string key)
        {
            lock (_lockValues)
            {
                if (Values.ContainsKey(key))
                    Values.Remove(key);
            }
        }

        public static void LogException(string context, Exception ex)
        {
            LogException(context, "", ex);
        }

        public static void LogException(string context, string message, Exception ex)
        {
            var fe = new Classes.FormattedException(ex);

            if (message.Length > 0) message += " ";
            message += fe.Message;
            foreach (var t in fe.Trace)
                message += " " + t;

            Log(context, "E", message);
        }

        public static void Log(string context, string severity, string message)
        {
            Log("application", context, severity, message);
        }

        /// <summary>
        /// Log a message
        /// </summary>
        /// <param name="logname">Name of logfile</param>
        /// <param name="context">Message context (8 chars)</param>
        /// <param name="severity">E error W warning I information D debug</param>
        public static void Log(string logname, string context, string severity, string message)
        {
            if (context.Length > 8)
                context = context.Substring(0, 8);
            else if (context.Length < 8)
                context = context.PadRight(8, '-');

            message = message.Replace("\r", "");
            message = message.Replace("\n", " ");
            message = message.Replace("\t", " ");

            severity = severity.Substring(0, 1);
            if (severity.Length == 0)
                severity = "E";

            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            line += " " + severity;
            line += " " + context;

            if (Session.Type == null)
                line += " -";
            else
                line += " " + Session.Type.Value;

            line += " " + System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
            line += ' ' + Thread.CurrentThread.ManagedThreadId.ToString();

            if (Session.UserId.Length > 0)
                line += " " + Session.UserId;
            else
                line += " -";

            line += ' ' + message;

            lock (_lockLog)
            {
                try
                {
                    var fs = new FileStream(RootPath + "var/log/" + logname + "_" + DateTime.Now.ToString("yyyyMMdd") + ".log", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                    fs.Position = fs.Length;
                    var sw = new StreamWriter(fs);
                    sw.WriteLine(line);
                    sw.Close();
                    fs.Close();
                }
                catch (Exception)
                {
                    // do nothing
                }
            }
        }
    }
}
