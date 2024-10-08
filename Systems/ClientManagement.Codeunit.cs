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
        public string ClientPathName { get; private set; } = "";
        public string ClientSearch { get; private set; } = "";

        public static event GenericHandler<ClientManagement>? ClientInitializing;
        public static event GenericHandler<ClientManagement>? ClientPolling;
        public static event GenericHandler<ClientManagement>? RunningLogin;
        public static event GenericHandler<ClientManagement>? RunningStart;

        [PublicAccess]
        public string Start(string pathname, string search)
        {
            ClientPathName = pathname;
            ClientSearch = search;

            if (Application.IsReady && Application.IsLoaded)
            {
                ClientInitializing?.Invoke(this);

                Type? pageType = null;
                ClientAccess? accAttr = null;
                if (!pathname.EndsWith("/")) pathname += "/";
                foreach (var acc in Application.ClientAccesses.Keys)
                {
                    string p2 = acc.Path;
                    if (!p2.EndsWith("/")) p2 += "/";
                    if (p2.Equals(pathname, StringComparison.OrdinalIgnoreCase))
                    {
                        accAttr = acc;
                        pageType = Application.ClientAccesses[acc];
                        break;
                    }
                }

                if (CurrentSession.UserId.Length == 0)
                {
                    if ((accAttr != null) && accAttr.IsLogin)
                    {
                        var bp = Activator.CreateInstance(pageType!) as BasePage;
                        bp!.Run();
                    }
                    else
                        RunningLogin?.Invoke(this);
                }
                else
                {
                    if ((accAttr != null) && (!accAttr.IsLogin))
                    {
                        var proxy = Loader.Proxy.CreateFromName(pageType!.FullName!);
                        var p = proxy.GetObject<BasePage>();
                        if (!p.Standalone)
                            RunningStart?.Invoke(this);
                        p.Run();
                    }
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
    }
}
