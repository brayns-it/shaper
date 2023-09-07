﻿using Microsoft.AspNetCore.Http;
using Brayns.Shaper.Database;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper
{
    public class SessionType : Option<SessionType>
    {
        public static readonly SessionType CONSOLE = New(0, "Console");
        public static readonly SessionType WEB = New(1, "Web");
        public static readonly SessionType SYSTEM = New(2, "System");
        public static readonly SessionType BATCH = New(3, "Batch");
        public static readonly SessionType WEBCLIENT = New(4, "Web Client");
    }

    internal class SessionData
    {
        internal SessionType Type { get; set; }
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
            Type = SessionType.SYSTEM;
            Values = new Dictionary<string, object>();
        }

        public override string ToString()
        {
            return Id.ToString();
        }
    }

    public class SessionArgs
    {
        public SessionType? Type { get; set; }
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

        public static SessionType Type
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
            if ((Instance.WebTask == null) || (Instance.Type != SessionType.WEBCLIENT))
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
            if (Id != Guid.Empty)
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

        public static void Start(SessionArgs? arg = null)
        {
            if ((arg != null) && (arg.Id != null) && (arg.Id != Guid.Empty))
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

            if (arg?.Type != null) Type = arg.Type;
            if (arg?.Address != null) Address = arg.Address;
            if (arg?.CultureInfo != null) CultureInfo = arg.CultureInfo;
            if (arg?.WebTask != null) Instance.WebTask = arg.WebTask;

            switch (Application.Config.DatabaseType)
            {
                case Brayns.Shaper.Database.DatabaseType.SqlServer:
                    Database = new SqlServer();
                    break;
            }

            Database?.Connect();

            if (Id != Guid.Empty)
            {
                Application.SystemModule?.SessionStart();
                Commit();
            }
        }
    }


}
