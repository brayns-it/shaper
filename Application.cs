using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Brayns.Shaper.Classes;
using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Brayns.Shaper
{
    public static class Application
    {
        private static readonly object _lockLog = new();

        internal static Dictionary<ApiAction, Dictionary<string, MethodInfo>> Routes { get; } = new();
        internal static Dictionary<Guid, AppModule> Apps { get; } = new();
        public static string? RootPath { get; internal set; }
        public static bool InMaintenance { get; internal set; } = true;
        public static Config Config { get; internal set; } = new Config();
        internal static SystemModule? SystemModule { get; set; }
        internal static ILogger? Logger { get; set; }

        private static void InitializeFromWebRoot(string rootPath)
        {
            if (!Directory.Exists(rootPath))
                throw new Error(Label("Root path '{0}' does not exists"), rootPath);

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

            di = new DirectoryInfo(RootPath + "apps");
            if (!di.Exists)
                di.Create();

            Loader.Loader.LoadConfig();
            Loader.Loader.LoadAppsFromRoot();
            Initialize();
        }

        public static void InitializeFromDomain()
        {
            Loader.Loader.LoadAppsFromDomain();
            Initialize();
        }

        private static void Initialize()
        {
            Loader.Loader.LoadTranslations();

            Session.Start();

            Loader.Loader.SyncSchema(false);
            Loader.Loader.CollectTableRelations();
            Loader.Loader.CollectApiEndpoints();
            Loader.Loader.InstallApps();

            Session.Stop();
        }

        public static void StartWebApplication(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
                Logger = app.Logger;

            try
            {
                InitializeFromWebRoot(builder.Environment.ContentRootPath);
                WebDispatcher.Initialize();

                InMaintenance = false;
            }
            catch (Exception ex)
            {
                LogException("initroot", ex);
            }

            // rest entry point
            app.MapMethods("/api/{**path}", new string[] { "GET", "POST", "PUT", "DELETE" }, WebDispatcher.DispatchApi);

            // client entry point
            app.MapPost("/rpc", WebDispatcher.DispatchRpc);
            
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.Run();
        }

        public static void LogException(string context, Exception ex)
        {
            Log(context, "E", ex.Message + " " + ex.StackTrace);
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
