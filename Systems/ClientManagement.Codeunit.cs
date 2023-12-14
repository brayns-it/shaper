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

        [PublicAccess]
        public void Initialize()
        {
            if (!Application.IsReady())
            {
                var setup = new Setup();
                setup.Run();
                return;
            }

            if (Application.InMaintenance)
            {
                var admin = new Admin();
                admin.Run();
                return;
            }

            ClientInitializing?.Invoke(this);
        }
    }
}
