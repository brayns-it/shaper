namespace Brayns.Shaper.Objects
{
    public delegate void BatchEvent(RunningTask sender);
    public delegate void BatchError(RunningTask sender, Exception ex);

    public class RunningTask
    {
        private SessionData? SessionInstance { get; set; }
        private SessionData? ParentInstance { get; set; }

        public event BatchEvent? Starting;
        public event BatchEvent? Finishing;
        public event BatchError? Error;

        public object? Tag { get; set; }

        public string TypeName { get; set; } = "";
        public string MethodName { get; set; } = "";
        public object[] Parameters { get; set; } = new object[0];
        public ThreadStart? Method { get; set; }
        public Thread? Thread { get; private set; }

        public bool IsAlive
        {
            get
            {
                if (Thread != null)
                    return Thread.IsAlive;
                return false;
            }
        }

        public void Start()
        {
            Thread = new Thread(new ThreadStart(Worker));
            Thread.Start();
        }

        public void Stop()
        {
            if (SessionInstance != null)
                SessionInstance.Cancel();
        }

        public static Thread ThreadStart(ThreadStart method)
        {
            var rt = new RunningTask();
            rt.Method = method;
            rt.ParentInstance = CurrentSession.Instance;
            rt.Start();
            return rt.Thread!;
        }

        private void Worker()
        {
            try
            {
                CurrentSession.Start(new SessionArgs()
                {
                    Id = Guid.NewGuid(),
                    Type = SessionTypes.BATCH,
                    Parent = ParentInstance
                });
                CurrentSession.IsSuperuser = true;

                SessionInstance = CurrentSession.Instance;

                Loader.Proxy proxy;

                if (Method != null)
                {
                    TypeName = Method.Target!.GetType().FullName!;
                    MethodName = Method.Method.Name;

                    proxy = Loader.Proxy.CreateFromObject(Method.Target);
                }
                else
                {
                    var typ = Loader.Proxy.TypeFromName(TypeName);
                    if (!Loader.Proxy.HasAttribute<Batchable>(typ))
                        throw new Error(Label("{0} is not batchable", TypeName));

                    var mi = Loader.Proxy.MethodFromName(TypeName, MethodName);
                    if (!Loader.Proxy.HasAttribute<BatchMethod>(mi))
                        throw new Error(Label("{0} is not a Batch method", MethodName));

                    proxy = Loader.Proxy.CreateFromName(TypeName, true);
                }

                Starting?.Invoke(this);
                Commit();

                proxy.Invoke(MethodName, Parameters);
                Commit();

                Finishing?.Invoke(this);
                Commit();
            }
            catch (Exception ex)
            {
                try
                {
                    Rollback();
                    Error?.Invoke(this, ex);
                    Commit();
                }
                catch
                {
                    // do nothing
                }
            }

            CurrentSession.Stop();
        }
    }
}
