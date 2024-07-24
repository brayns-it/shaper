using System.Text;

namespace Brayns.Shaper.Systems
{
    public class Admin : Page<Admin>
    {
        private StringBuilder _log = new();
        protected Fields.Text Log { get; init; } = new();

        public Admin()
        {
            UnitCaption = Label("Administration");

            var content = Controls.ContentArea.Create(this);
            {
                var log = new Controls.Group(content, Label("Log"));
                {
                    log.FieldPerRow = Controls.FieldPerRow.One;
                    new Controls.Field(log, Log) { InputType = Controls.InputType.Html, ReadOnly = true, ShowCaption = false };
                }
            }

            var actions = Controls.ActionArea.Create(this);
            {
                var actRestart = new Controls.Action(actions, Label("Restart"), Icon.FromName("fas fa-sync-alt"));
                actRestart.Triggering += ActRestart_Triggering;

                var actDb = new Controls.Action(actions, Label("Database"), Icon.FromName("fas fa-database"));
                {
                    var actDbCompile = new Controls.Action(actDb, Label("Compile"));
                    actDbCompile.Triggering += ActDbCompile_Triggering;

                    var actDbCompileForce = new Controls.Action(actDb, Label("Compile (force)"));
                    actDbCompileForce.Triggering += ActDbCompileForce_Triggering;

                    var actDbVerify = new Controls.Action(actDb, Label("Verify"));
                    actDbVerify.Triggering += ActDbVerify_Triggering;

                    var actDbBackup = new Controls.Action(actDb, Label("Backup"));
                    actDbBackup.Triggering += ActDbBackup_Triggering;
                }

                var actLanguage = new Controls.Action(actions, Label("Language"), Icon.FromName("fas fa-globe"));
                {
                    var actCreatePo = new Controls.Action(actLanguage, Label("Create PO templates"));
                    actCreatePo.Triggering += ActCreatePo_Triggering;
                }
            }

            Loading += Admin_Loading;
        }

        private void ActDbBackup_Triggering()
        {
            var backup = new Database.Backup();
            backup.DoBackupYN();
        }

        private void ActCreatePo_Triggering()
        {
            InitLog();

            DirectoryInfo di = new DirectoryInfo(Application.RootPath! + "code");
            if (di.Exists)
            {
                AppendLog(Label("Creating POT {0}...", di.FullName));
                Shaper.Classes.Language.CreatePoFile(di.FullName);
            }

            di = new DirectoryInfo(Application.RootPath! + "apps");
            if (di.Exists)
            {
                foreach (DirectoryInfo di2 in di.GetDirectories())
                {
                    AppendLog(Label("Creating POT {0}...", di2.FullName));
                    Shaper.Classes.Language.CreatePoFile(di2.FullName);
                }
            }

            if (Application.DebugPath != null)
            {
                di = new DirectoryInfo(Application.DebugPath);
                if (di.Exists)
                {
                    AppendLog(Label("Creating POT {0}...", di.FullName));
                    Shaper.Classes.Language.CreatePoFile(di.FullName);
                }
            }

            AppendLog(Label("POT creation done."));
        }

        private void ActRestart_Triggering()
        {
            Application.Initialize();
            Client.Reload();
        }

        private void Admin_Loading()
        {
            if (CurrentSession.ApplicationName.Length == 0)
                CurrentSession.ApplicationName = Label("Shaper");

            Log.Value = Label("No log.");
        }

        private void ActDbCompileForce_Triggering()
        {
            InitLog();
            AppendLog(Label("Compiling database (with force)..."));
            Loader.Loader.CompileTables(Database.DatabaseCompileMode.Force);
            AppendLog(Label("Compiling done."));
        }

        private void InitLog()
        {
            _log.Clear();
        }

        private void AppendLog(string text)
        {
            _log.Append(text + "<br/>");
            Log.Value = _log.ToString();
            Update();
        }

        private void ActDbVerify_Triggering()
        {
            InitLog();
            AppendLog(Label("Verifying database..."));

            Loader.Loader.CompileTables(Database.DatabaseCompileMode.CheckOnly);

            string res = "";
            foreach (var s in CurrentSession.Database!.CompileResult)
                res += "<span style='color: red'>" + s + "</span><br/>";

            if (res.Length > 0)
                AppendLog(res + Label("Verifying done with disruptions: proceed manually."));
            else
                AppendLog(Label("Verifying done, no database disruptions."));
        }

        private void ActDbCompile_Triggering()
        {
            InitLog();
            AppendLog(Label("Compiling database..."));
            Loader.Loader.CompileTables(Database.DatabaseCompileMode.Normal);
            AppendLog(Label("Compiling done."));
        }
    }
}
