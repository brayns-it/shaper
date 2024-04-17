using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Systems
{
    [Published]
    public class ClientManagement : Codeunit
    {
        public static event GenericHandler<ClientManagement>? ClientInitializing;
        public static event GenericHandler<ClientManagement>? ClientPolling;
        public static event GenericHandler<ClientManagement>? RunningLogin;
        public static event GenericHandler<ClientManagement>? RunningStart;

        [PublicAccess]
        public string Start(string page)
        {
            if (Application.IsReady && Application.IsLoaded)
            {
                ClientInitializing?.Invoke(this);

                if (page.Length > 0)
                {
                    StartPage(page);
                }
                else
                {
                    if (CurrentSession.UserId.Length == 0)
                        RunningLogin?.Invoke(this);
                    else
                        RunningStart?.Invoke(this);

                }
            }
            else
            {
                if (Application.IsFromMaintenanceNetwork())
                {
                    if (!Application.IsReady)
                    {
                        var setup = new Setup();
                        setup.Run();
                    }
                    else if (!Application.IsLoaded)
                    {
                        var admin = new Admin();
                        admin.Run();
                    }
                }
                else
                    throw Application.ErrorInMaintenance();
            }

            return Session.Id.ToString();
        }

        [PublicAccess]
        public void Poll()
        {
            CurrentSession.LastPoll = DateTime.Now;

            ClientPolling?.Invoke(this);

            foreach (Unit u in Session.Units.Values)
                u.TriggerPoll();
        }

        private void StartPage(string requestedPage)
        {
            Loader.Proxy.AssertType<BasePage>(requestedPage);

            if ((!Loader.Proxy.HasAttribute<PublicAccess>(requestedPage)) && (CurrentSession.UserId.Length == 0))
            {
                RunningLogin?.Invoke(this);
            }
            else
            {
                var proxy = Loader.Proxy.CreateFromName(requestedPage);
                var p = proxy.GetObject<BasePage>();
                if (!p.Standalone)
                    RunningStart?.Invoke(this);
                p.Run();
            }
        }
    }
}
