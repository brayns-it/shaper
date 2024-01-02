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

        [PublicAccess]
        public string Initialize()
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
            {
                if ((!Application.IsReady) || (!Application.IsLoaded))
                    throw Application.ErrorInMaintenance();
            }

            if (Application.IsReady && Application.IsLoaded)
                ClientInitializing?.Invoke(this);

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

        [PublicAccess]
        public void Destroy()
        {
            CurrentSession.Stop(true);
        }
    }
}
