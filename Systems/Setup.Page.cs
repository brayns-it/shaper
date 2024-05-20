﻿namespace Brayns.Shaper.Systems
{
    public class Setup : Page<Setup>
    {
        private Fields.Option<Database.DatabaseTypes> DatabaseType { get; } = new(Label("Database type"));
        private Fields.Text DatabaseConnection { get; } = new(Label("Database connection"));
        private Fields.Text DatabaseLogin { get; } = new(Label("Database login"));
        private Fields.Text DatabasePassword { get; } = new(Label("Database password"));
        private Fields.Text DatabaseName { get; } = new(Label("Database name"));
        private Fields.Text DatabaseServer { get; } = new(Label("Database server"));
        private Fields.Text EnvironmentName { get; } = new(Label("Environment name"));
        private Fields.Text MaintenanceNetwork { get; } = new(Label("Maintenance network"));
        private Fields.Boolean ReversiblePasswords { get; } = new("Reversible passwords", Label("Reversible passwords"));
        private Fields.Text ReversibleKey { get; } = new("Reversible key", Label("Reversible key"), 50);

        public Setup()
        {
            UnitCaption = Label("Setup");

            var content = Controls.ContentArea.Create(this);
            {
                var general = new Controls.Group(content, Label("General"));
                {
                    var dbPar = new Controls.Field(general, EnvironmentName);
                    dbPar.Validating += DbPar_Validating;

                    new Controls.Field(general, MaintenanceNetwork);
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

                var password = new Controls.Group(content, Label("Password"));
                {
                    new Controls.Field(password, ReversibleKey) { InputType = Shaper.Controls.InputType.Password };
                    new Controls.Field(password, ReversiblePasswords);
                }
            }

            var actions = Controls.ActionArea.Create(this);
            {
                var save = new Controls.Action(actions, Label("Save"), Icon.FromName("fas fa-save"));
                save.Triggering += Save_Triggering;
            }

            Loading += Setup_Loading;
        }

        private void Setup_Loading()
        {
            DatabaseType.Value = Application.Config.DatabaseType;
            DatabaseConnection.Value = Application.Config.DatabaseConnection;
            DatabaseLogin.Value = Application.Config.DatabaseLogin;
            DatabasePassword.Value = Application.Config.DatabasePassword;
            DatabaseName.Value = Application.Config.DatabaseName;
            DatabaseServer.Value = Application.Config.DatabaseServer;
            EnvironmentName.Value = Application.Config.EnvironmentName;
            MaintenanceNetwork.Value = Application.Config.MaintenanceNetwork;
            ReversibleKey.Value = Application.Config.ReversibleKey;
            ReversiblePasswords.Value = Application.Config.ReversiblePasswords;

            if (CurrentSession.ApplicationName.Length == 0)
                CurrentSession.ApplicationName = Label("New Shaper");
        }

        private void DbPar_Validating()
        {
            switch (DatabaseType.Value)
            {
                case Database.DatabaseTypes.SQLSERVER:
                    DatabaseConnection.Value = Database.SqlServer.GetConnectionString(DatabaseServer.Value, DatabaseName.Value, EnvironmentName.Value);
                    break;
                case Database.DatabaseTypes.SQLITE:
                    DatabaseConnection.Value = Database.SQLite.GetConnectionString(DatabaseServer.Value);
                    break;
            }
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
            Application.Config.MaintenanceNetwork = MaintenanceNetwork.Value;
            Application.Config.ReversibleKey = ReversibleKey.Value;
            Application.Config.ReversiblePasswords = ReversiblePasswords.Value;

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
