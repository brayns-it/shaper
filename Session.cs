using Microsoft.AspNetCore.Http;
using Brayns.Shaper.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper
{
    public class SessionTypes
    {
        [Label("Console")]
        public const int CONSOLE = 0;

        [Label("Web")]
        public const int WEB = 1;

        [Label("System")]
        public const int SYSTEM = 2;

        [Label("Batch")]
        public const int BATCH = 3;

        [Label("Web Client")]
        public const int WEBCLIENT = 4;
    }

    internal class SessionData
    {
        internal Opt<SessionTypes> Type { get; set; }
        internal Database.Database? Database { get; set; }
        internal CultureInfo CultureInfo { get; set; }
        internal string UserId { get; set; }
        internal Guid Id { get; set; }
        internal string Address { get; set; }
        internal WebTask? WebTask { get; set; }
        internal Dictionary<string, object> Values { get; set; }

        public SessionData()
        {
            CultureInfo = CultureInfo.CurrentCulture;
            UserId = "";
            Id = Guid.Empty;
            Address = "";
            Type = SessionTypes.SYSTEM;
            Values = new Dictionary<string, object>();
        }

        public override string ToString()
        {
            return Id.ToString();
        }
    }

    public class SessionArgs
    {
        public Opt<SessionTypes>? Type { get; set; }
        public CultureInfo? CultureInfo { get; set; }
        public Guid? Id { get; set; }
        public string? Address { get; set; }
        internal WebTask? WebTask { get; set; }
    }

    public static class Session
    {
        internal static object _lockSessions = new object();
        internal static Dictionary<Guid, SessionData> SessionData { get; } = new Dictionary<Guid, SessionData>();
        internal static Dictionary<int, SessionData> SessionMap { get; } = new Dictionary<int, SessionData>();

        public static Opt<SessionTypes> Type
        {
            get { return Instance.Type; }
            internal set { Instance.Type = value; }
        }

        public static Database.Database? Database
        {
            get { return Instance.Database; }
            internal set { Instance.Database = value; }
        }

        public static string Address
        {
            get { return Instance.Address; }
            internal set { Instance.Address = value; }
        }

        public static Guid Id
        {
            get { return Instance.Id; }
            internal set { Instance.Id = value; }
        }

        public static CultureInfo CultureInfo
        {
            get { return Instance.CultureInfo; }
            internal set { Instance.CultureInfo = value; }
        }

        public static string UserId
        {
            get { return Instance.UserId; }
            set { Instance.UserId = value; }
        }

        public static string Server
        {
            get
            {
                return System.Net.Dns.GetHostName();
            }
        }

        public static int ProcessID
        {
            get
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
        }

        public static int ThreadID
        {
            get
            {
                return Thread.CurrentThread.ManagedThreadId;
            }
        }

        public static Dictionary<string, object> Values
        {
            get { return Instance.Values; }
        }

        private static SessionData Instance
        {
            get
            {
                var no = Thread.CurrentThread.ManagedThreadId;
                if (!SessionMap.ContainsKey(no))
                {
                    lock (_lockSessions)
                    {
                        SessionMap.Add(no, new SessionData());
                    }
                }
                return SessionMap[no];
            }
            set
            {
                var no = Thread.CurrentThread.ManagedThreadId;
                lock (_lockSessions)
                {
                    SessionMap[no] = value;
                }
            }
        }

        public static void SendToClient(object o, bool optional = false)
        {
            if ((Instance.WebTask == null) || (Instance.Type != SessionTypes.WEBCLIENT))
            {
                if (optional)
                    return;
                else
                    throw new Error("Current session does not support client messaging");
            }

            Instance.WebTask.Send(o);
        }

        public static void Stop(bool destroy = false, bool ignoreErrors = false)
        {
            if ((Type != SessionTypes.SYSTEM) && (Database != null))
            {
                try
                {
                    Rollback();

                    Application.SystemModule?.SessionStop();
                    if (destroy)
                        Application.SystemModule?.SessionDestroy();

                    Commit();
                }
                catch (Exception)
                {
                    if (!ignoreErrors)
                        throw;
                }
            }

            try
            {
                Database?.Disconnect();
            }
            catch (Exception)
            {
                if (!ignoreErrors)
                    throw;
            }

            Database = null;

            if (Id != Guid.Empty)
            {
                if (destroy)
                {
                    if (SessionData.ContainsKey(Id))
                    {
                        lock (_lockSessions)
                        {
                            SessionData.Remove(Id);
                        }
                    }
                }

                var no = Thread.CurrentThread.ManagedThreadId;
                if (SessionMap.ContainsKey(no))
                {
                    lock (_lockSessions)
                    {
                        SessionMap.Remove(no);
                    }
                }
            }
        }

        internal static bool DatabaseConnect()
        {
            switch (Application.Config.DatabaseType)
            {
                case DatabaseTypes.SQLSERVER:
                    Database = new SqlServer();
                    break;
            }

            if (Database != null)
            {
                Database.Connect();
                return true;
            }

            return false;
        }

        public static void Start(SessionArgs? arg = null)
        {
            if (arg != null)
            {
                if ((arg.Id != null) && (arg.Id != Guid.Empty))
                {
                    lock (_lockSessions)
                    {
                        if (!SessionData.ContainsKey(arg.Id.Value))
                        {
                            var sd = new SessionData()
                            {
                                Id = arg.Id.Value
                            };
                            SessionData.Add(arg.Id.Value, sd);
                        }

                        Instance = SessionData[arg.Id.Value];
                    }
                }

                if (arg.Type != null) Type = arg.Type;
                if (arg.Address != null) Address = arg.Address;
                if (arg.CultureInfo != null) CultureInfo = arg.CultureInfo;
                if (arg.WebTask != null) Instance.WebTask = arg.WebTask;
            }

            if (Application.IsReady())
                if (DatabaseConnect())
                {
                    if (Type != SessionTypes.SYSTEM)
                    {
                        Application.SystemModule?.SessionStart();
                        Commit();
                    }
                }
        }
    }


}
