namespace Brayns.Shaper.Systems
{
    public class Setup : Page<Setup>
    {
        private Fields.Option<Database.DatabaseTypes> DatabaseType { get; } = new("Database type", Label("Database type"));
        private Fields.Text DatabaseConnection { get; } = new("Database connection", Label("Database connection"), 512);
        private Fields.Text DatabaseLogin { get; } = new("Database login", Label("Database login"), 512);
        private Fields.Text DatabasePassword { get; } = new("Database password", Label("Database password"), 512);
        private Fields.Text DatabaseName { get; } = new("Database name", Label("Database name"), 512);
        private Fields.Text DatabaseServer { get; } = new("Database server", Label("Database server"), 512);
        private Fields.Text EnvironmentName { get; } = new("Environment name", Label("Environment name"), 512);

        public Setup()
        {
            UnitName = "Setup";
            UnitCaption = Label("Setup");

            var content = new Controls.ContentArea(this);
            {
                var general = new Controls.Group(content, Label("General"));
                {
                    var dbPar = new Controls.Field(general, EnvironmentName);
                    dbPar.Validating += DbPar_Validating;
                }

                var database = new Controls.Group(content, Label("Database"));
                {
                    var dbPar = new Controls.Field(database, DatabaseType);
                    dbPar.Validating += DbPar_Validating;

                    dbPar = new Controls.Field(database, DatabaseServer);
                    dbPar.Validating += DbPar_Validating;

                    dbPar = new Controls.Field(database, DatabaseName);
                    dbPar.Validating += DbPar_Validating;

                    new Controls.Field(database, DatabaseLogin);
                    new Controls.Field(database, DatabasePassword) { InputType = Shaper.Controls.InputType.Password };
                    new Controls.Field(database, DatabaseConnection);
                }
            }

            var actions = new Controls.ActionArea(this);
            {
                var save = new Controls.Action(actions, Label("Save"), "fas fa-save");
                save.Triggering += Save_Triggering;
            }
        }

        private void DbPar_Validating()
        {
            switch (DatabaseType.Value)
            {
                case Database.DatabaseTypes.SQLSERVER:
                    DatabaseConnection.Value = Database.SqlServer.GetConnectionString(DatabaseServer.Value, DatabaseName.Value, EnvironmentName.Value);
                    break;
            }
        }

        protected override void OnLoad()
        {
            DatabaseType.Value = Application.Config.DatabaseType;
            DatabaseConnection.Value = Application.Config.DatabaseConnection;
            DatabaseLogin.Value = Application.Config.DatabaseLogin;
            DatabasePassword.Value = Application.Config.DatabasePassword;
            DatabaseName.Value = Application.Config.DatabaseName;
            DatabaseServer.Value = Application.Config.DatabaseServer;
            EnvironmentName.Value = Application.Config.EnvironmentName;

            CurrentSession.ApplicationName = Label("New Shaper");
        }

        private void Save_Triggering()
        {
            Application.Config.EnvironmentName = EnvironmentName.Value;
            Application.Config.DatabaseType = DatabaseType.Value;
            Application.Config.DatabaseConnection = DatabaseConnection.Value;
            Application.Config.DatabaseLogin = DatabaseLogin.Value;
            Application.Config.DatabasePassword = DatabasePassword.Value;
            Application.Config.DatabaseName = DatabaseName.Value;
            Application.Config.DatabaseServer = DatabaseServer.Value;

            if (DatabaseType.Value != Database.DatabaseTypes.NONE)
            {
                Application.Config.Ready = true;

                try
                {
                    Application.Initialize();
                }
                catch
                {
                    Application.Config.Ready = false;
                    throw;
                }
            }

            Loader.Loader.SaveConfig();
            Client.Reload();
        }
    }
}
