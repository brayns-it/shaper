namespace Brayns.Shaper.Objects
{
    public delegate void BatchEvent(RunningTask sender);
    public delegate void BatchError(RunningTask sender, Exception ex);

    public class RunningTask
    {
        public event BatchEvent? Starting;
        public event BatchEvent? Finishing;
        public event BatchError? Error;

        public object? Tag { get; set; }
        public Batch? Batch { get; set; }

        public string TypeName { get; set; } = "";
        public string MethodName { get; set; } = "";
        public object[] Parameters { get; set; } = new object[0];
        public ThreadStart? Method { get; set; }
        public Thread? Thread { get; private set; }

        public void Start()
        {
            Thread = new Thread(new ThreadStart(Worker));
            Thread.Start();
        }

        public void Stop()
        {
            if (Batch != null)
                Batch.StopRequest = true;
        }

        public static Thread ThreadStart(ThreadStart method)
        {
            var rt = new RunningTask();
            rt.Method = method;
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
                    Type = SessionTypes.BATCH
                });
                CurrentSession.IsSuperuser = true;

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
                    if (!typeof(Batch).IsAssignableFrom(typ))
                        throw new Error(Label("{0} is not Batch type", TypeName));

                    var mi = Loader.Proxy.MethodFromName(TypeName, MethodName);
                    if (!Loader.Proxy.HasAttribute<BatchMethod>(mi))
                        throw new Error(Label("{0} is not a Batch method", MethodName));

                    proxy = Loader.Proxy.CreateFromName(TypeName, true);

                    Batch = proxy.GetObject<Batch>();
                    Batch.Tag = Tag;
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

            CurrentSession.Stop(true);
        }
    }

    public abstract class Batch : Unit
    {
        public object? Tag { get; internal set; }
        public bool StopRequest { get; internal protected set; } = false;

        internal override void UnitInitialize()
        {
            UnitType = UnitTypes.BATCH;
        }

        protected void SafeSleep(int milliSeconds)
        {
            int seconds = milliSeconds / 1000;
            if (seconds <= 0) seconds = 1;

            for (int i = 0; (i < seconds) && (!StopRequest); i++)
                Thread.Sleep(1000);
        }
    }
}
