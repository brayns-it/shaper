using Microsoft.AspNetCore.Http;
using Brayns.Shaper.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace Brayns.Shaper
{
    public class SessionTypes : OptList
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

    internal class ThreadData
    {
        internal Database.Database? Database { get; set; }
    }

    internal class SessionData
    {
        internal Opt<SessionTypes> Type { get; set; }
        internal CultureInfo CultureInfo { get; set; }
        internal string UserId { get; set; }
        internal Guid Id { get; set; }
        internal Guid? AuthenticationId { get; set; }
        internal string Address { get; set; }
        internal WebTask? WebTask { get; set; }
        internal Dictionary<string, object> State { get; set; }
        internal Dictionary<string, Unit> Units { get; set; }
        internal bool IsNew { get; set; }
        internal string ApplicationName { get; set; }
        internal bool IsSuperuser { get; set; }
        internal DateTime LastPoll { get; set; }
        internal bool StopRequested { get; set; }
        internal SessionData? Parent { get; set; }

        public SessionData()
        {
            CultureInfo = CultureInfo.CurrentCulture;
            UserId = "";
            Id = Guid.Empty;
            Address = "";
            Type = SessionTypes.SYSTEM;
            State = new Dictionary<string, object>();
            Units = new Dictionary<string, Unit>();
            ApplicationName = "";
            IsSuperuser = false;
            StopRequested = false;
            LastPoll = DateTime.Now;
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
        public Guid? AuthenticationId { get; set; }
        public string? Address { get; set; }
        internal WebTask? WebTask { get; set; }
        internal SessionData? Parent { get; set; } 
    }

    public delegate void SessionStartingHandler(bool sessionIsNew);

    public static class Session
    {
        public static event SessionStartingHandler? Starting;
        public static event GenericHandler? Stopping;
        public static event GenericHandler? Destroying;

        internal static object _lockSessions = new object();
        internal static Dictionary<Guid, SessionData> SessionData { get; } = new Dictionary<Guid, SessionData>();
        internal static Dictionary<int, SessionData> SessionMap { get; } = new Dictionary<int, SessionData>();
        internal static Dictionary<int, ThreadData> ThreadMap { get; } = new Dictionary<int, ThreadData>();

        public static Opt<SessionTypes> Type
        {
            get { return Instance.Type; }
            internal set { Instance.Type = value; }
        }

        public static Database.Database? Database
        {
            get { return ThreadData.Database; }
            internal set { ThreadData.Database = value; }
        }

        public static string Address
        {
            get { return Instance.Address; }
            internal set { Instance.Address = value; }
        }

        public static Guid? AuthenticationId
        {
            get { return Instance.AuthenticationId; }
            set { Instance.AuthenticationId = value; }
        }

        public static string ApplicationName
        {
            get { return Instance.ApplicationName; }
            set { Instance.ApplicationName = value; }
        }

        public static bool IsSuperuser
        {
            get { return Instance.IsSuperuser; }
            set { Instance.IsSuperuser = value; }
        }

        internal static WebTask? WebTask
        {
            get { return Instance.WebTask; }
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

        internal static DateTime LastPoll
        {
            get { return Instance.LastPoll; }
            set { Instance.LastPoll = value; }
        }

        internal static Dictionary<string, object> State
        {
            get { return Instance.State; }
        }

        internal static Dictionary<string, Unit> Units
        {
            get { return Instance.Units; }
        }

        private static ThreadData ThreadData
        {
            get
            {
                var no = Thread.CurrentThread.ManagedThreadId;
                if (!ThreadMap.ContainsKey(no))
                {
                    lock (_lockSessions)
                    {
                        ThreadMap.Add(no, new ThreadData());
                    }
                }
                return ThreadMap[no];
            }
        }

        internal static SessionData Instance
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

        public static void Stop(bool forceDestroy = false)
        {
            try
            {
                Rollback();

                Stopping?.Invoke();
                if ((Type != SessionTypes.WEBCLIENT) || forceDestroy)
                    Destroying?.Invoke();

                Commit();
            }
            catch
            {
            }

            try
            {
                Database?.Disconnect();
            }
            catch
            {
            }

            Database = null;
            Instance.StopRequested = true;

            if (Id != Guid.Empty)
            {
                if ((Type != SessionTypes.WEBCLIENT) || forceDestroy)
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
                if (ThreadMap.ContainsKey(no))
                {
                    lock (_lockSessions)
                    {
                        ThreadMap.Remove(no);
                    }
                }
            }
        }

        internal static Database.Database? DatabaseCreate()
        {
            switch (Application.Config.DatabaseType)
            {
                case DatabaseTypes.SQLSERVER:
                    return new SqlServer();
                case DatabaseTypes.SQLITE:
                    return new SQLite();
            }

            return null;
        }

        internal static void DatabaseConnect()
        {
            if (Database != null)
            {
                Database.Disconnect();
                Database = null;
            }

            Database = DatabaseCreate();
            
            if (Database != null)
                Database.Connect();
        }

        internal static List<System.Guid> CleanupClients()
        {
            lock (_lockSessions)
            {
                List<System.Guid> toDel = new();
                foreach (var sd in SessionData.Values)
                {
                    if ((DateTime.Now.Subtract(sd.LastPoll).TotalSeconds > 300) &&
                       (!SessionMap.Values.Contains(sd)) &&
                       (sd.Type == SessionTypes.WEBCLIENT))
                        toDel.Add(sd.Id);
                }
                foreach (var id in toDel)
                {
                    SessionData.Remove(id);
                }
                return toDel;                
            }
        }

        public static bool StopRequested
        {
            get
            {
                if ((Instance.Parent != null) && Instance.Parent.StopRequested)
                    return true;

                return Instance.StopRequested;
            }
        }

        public static void ThrowIfStopRequested()
        {
            if (StopRequested)
                throw new Error(Label("Session interrupted"));
        }

        public static void Start(SessionArgs? arg = null)
        {
            if (arg != null)
            {
                if ((arg.Id != null) && (arg.Id != Guid.Empty))
                {
                    lock (_lockSessions)
                    {
                        bool isNew = false;
                        if (!SessionData.ContainsKey(arg.Id.Value))
                        {
                            isNew = true;
                            var sd = new SessionData()
                            {
                                Id = arg.Id.Value
                            };
                            SessionData.Add(arg.Id.Value, sd);
                        }

                        Instance = SessionData[arg.Id.Value];
                        Instance.IsNew = isNew;
                    }
                }

                if (arg.Type != null) Type = arg.Type;
                if (arg.Address != null) Address = arg.Address;
                if (arg.CultureInfo != null) CultureInfo = arg.CultureInfo;
                if (arg.WebTask != null) Instance.WebTask = arg.WebTask;
                if (arg.AuthenticationId != null) AuthenticationId = arg.AuthenticationId;
                if (arg.Parent != null) Instance.Parent = arg.Parent;
            }

            if (Application.IsReady)
                DatabaseConnect();

            if (Application.IsLoaded)
            {
                Starting?.Invoke(Instance.IsNew);
                Commit();
            }
        }
    }
}
