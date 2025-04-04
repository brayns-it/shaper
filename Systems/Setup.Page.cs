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
        private Fields.Text EncryptionKey { get; } = new(Label("Encryption key"));

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
                    new Controls.Field(general, EncryptionKey) { InputType = Controls.InputType.Password };
                }

                var database = new Controls.Group(content, "database", Label("Database"));
                {
                    var dbPar = new Controls.Field(database, DatabaseType);
                    dbPar.Validating += DbPar_Validating;

                    dbPar = new Controls.Field(database, "db-server", DatabaseServer);
                    dbPar.Validating += DbPar_Validating;

                    dbPar = new Controls.Field(database, "db-name", DatabaseName);
                    dbPar.Validating += DbPar_Validating;

                    new Controls.Field(database, "db-login", DatabaseLogin);
                    new Controls.Field(database, "db-password", DatabasePassword) { InputType = Shaper.Controls.InputType.Password };
                    new Controls.Field(database, DatabaseConnection);
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
            EncryptionKey.Value = Application.Config.EncryptionKey;

            if (CurrentSession.ApplicationName.Length == 0)
                CurrentSession.ApplicationName = Label("New Shaper");

            ShowFields();
        }

        private void ShowFields()
        {
            var v = (DatabaseType.Value == Database.DatabaseTypes.SQLSERVER) ||
                    (DatabaseType.Value == Database.DatabaseTypes.MYSQL);
            Control("db-server")!.Visible = v;
            Control("db-name")!.Visible = v;
            Control("db-login")!.Visible = v;
            Control("db-password")!.Visible = v;
        }

        private void DbPar_Validating()
        {
            switch (DatabaseType.Value)
            {
                case Database.DatabaseTypes.SQLSERVER:
                    DatabaseConnection.Value = Database.SqlServer.CreateConnectionString(DatabaseServer.Value, DatabaseName.Value, EnvironmentName.Value);
                    break;
                case Database.DatabaseTypes.SQLITE:
                    DatabaseConnection.Value = Database.SQLite.CreateConnectionString();
                    break;
                case Database.DatabaseTypes.MYSQL:
                    DatabaseConnection.Value = Database.MySQL.CreateConnectionString(DatabaseServer.Value, DatabaseName.Value);
                    break;
            }

            ShowFields();
            Control("database")!.Redraw();
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
            Application.Config.EncryptionKey = EncryptionKey.Value;

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
