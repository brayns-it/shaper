﻿using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Brayns.Shaper.Classes;
using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;

namespace Brayns.Shaper
{
    public delegate void SessionCleaningHandler(List<Guid> sessionsIds);

    public static class Application
    {
        private static readonly object _lockLog = new();
        private static readonly object _lockValues = new();
        private static bool _quitMonitor = false;

        internal static Thread? MonitorThread { get; set; }
        internal static Dictionary<string, object> Values = new();
        internal static Dictionary<ApiAction, Dictionary<string, MethodInfo>> Routes { get; } = new();
        internal static Dictionary<Guid, AppModule> Apps { get; } = new();
        internal static Config Config { get; set; } = new Config();
        internal static ILogger? Logger { get; set; }
        internal static bool IsLoaded { get; private set; } = false;
        internal static bool IsReady { get { return Config.Ready; } }
        public static string? RootPath { get; internal set; }
        public static event GenericHandler? Initializing;
        public static event GenericHandler? Monitoring;
        public static event SessionCleaningHandler? SessionCleaning;

        private static string? _debugPath;
        public static string? DebugPath
        {
            get { return _debugPath; }
            set
            {
                _debugPath = value;
                if (_debugPath != null)
                {
                    _debugPath = _debugPath.Replace("\\", "/");
                    if (!_debugPath.EndsWith("/"))
                        _debugPath += "/";
                }
            }
        }

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

        private static void InitializeFromWebRoot(string rootPath)
        {
            if (!Directory.Exists(rootPath))
                throw new Error(Label("Root path '{0}' does not exists", rootPath));

            RootPath = rootPath;
            RootPath = RootPath.Replace("\\", "/");
            if (!RootPath.EndsWith("/"))
                RootPath += "/";

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
            DateTime lastCleanup = DateTime.Now;
            bool sessionStarted = false;
            bool restart = false;

            while (!_quitMonitor)
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
                        if (DateTime.Now.Subtract(lastCleanup).TotalSeconds > 60)
                        {
                            var ids = Session.CleanupClients();
                            SessionCleaning?.Invoke(ids);
                            lastCleanup = DateTime.Now;
                        }

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

                Loader.Loader.CompileTables(Database.DatabaseCompileMode.Normal);
                Loader.Loader.CollectTableRelations();
                Loader.Loader.CollectApiEndpoints();
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

            app.Lifetime.ApplicationStopped.Register(() =>
            {
                _quitMonitor = true;
            });
        }

        public static void MapShaperApi(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
                Logger = app.Logger;

            // rest entry point
            app.MapMethods("/api/{**path}", new string[] { "GET", "POST", "PUT", "DELETE" }, WebDispatcher.DispatchApi);

            // client entry point
            app.MapPost("/rpc", WebDispatcher.DispatchRpc);
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
            string trace = "";

            var st = new StackTrace(ex, true);
            var frames = st.GetFrames();
            foreach (var frame in frames)
            {
                string? fn = frame.GetFileName();
                if (fn != null)
                {
                    FileInfo fi = new FileInfo(fn);
                    trace += (" in '" + fi.Name + "' line " + frame.GetFileLineNumber() + " method '" + frame.GetMethod()!.Name + "'");
                }
            }

            Log(context, "E", ex.Message + trace);
        }

        /// <summary>
        /// Log a message
        /// </summary>
        /// <param name="context">Message context (8 chars)</param>
        /// <param name="severity">E error W warning I information D debug</param>
        public static void Log(string context, string severity, string message)
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
                    var fs = new FileStream(RootPath + "var/log/application_" + DateTime.Now.ToString("yyyyMMdd") + ".log", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
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

            if (Logger != null)
            {
                if (severity == "E")
                    Logger.LogError(message);
                else if (severity == "D")
                    Logger.LogDebug(message);
                else if (severity == "I")
                    Logger.LogInformation(message);
                else if (severity == "W")
                    Logger.LogWarning(message);
            }
        }
    }
}
