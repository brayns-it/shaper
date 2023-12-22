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
        public void Initialize()
        {
            if (Application.IsFromMaintenanceNetwork())
            {
                if (!Application.IsReady)
                {
                    var setup = new Setup();
                    setup.Run();
                    return;
                }

                if (!Application.IsLoaded)
                {
                    var admin = new Admin();
                    admin.Run();
                    return;
                }
            }
            else
            {
                if ((!Application.IsReady) || (!Application.IsLoaded))
                    throw Application.ErrorInMaintenance();
            }

            ClientInitializing?.Invoke(this);
        }

        [PublicAccess]
        public void Poll()
        {
            ClientPolling?.Invoke(this);

            foreach (Unit u in Session.Units.Values)
                u.TriggerPoll();
        }

        [PublicAccess]
        public void Destroy()
        {
            CurrentSession.Stop(true, false);
        }
    }
}
