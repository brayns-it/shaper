using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Objects
{
    public class PageTypes
    {
        public const int NORMAL = 0;
    }

    public abstract class Page : Unit
    {
        public List<Controls.Control> Controls { get; private set; } = new();
        internal Dictionary<string, Controls.Control> AllControls { get; set; } = new();
        public Series<PageTypes> PageType { get; protected set; }

        public Page()
        {
            UnitType = UnitTypes.PAGE;
            PageType = PageTypes.NORMAL;
        }

        internal JObject Render()
        {
            var result = new JObject();
            result["id"] = UnitID.ToString();
            result["name"] = UnitName;
            result["unitType"] = GetType().ToString();
            result["pageType"] = PageType.ToString();
            result["caption"] = UnitCaption;
            result["action"] = "page";
            result["display"] = "content";

            var controls = new JArray();
            foreach (var c in Controls)
                controls.Add(c.Render());
            result["controls"] = controls;

            return result;
        }

        public void Run()
        {
            SessionRegister();
            CurrentSession.SendToClient(Render());
        }

        [PublicAccess]
        internal void ControlInvoke(string controlid, string method, JObject? args = null)
        {
            var ctl = AllControls[controlid];
            var prx = Loader.Proxy.CreateFromObject(ctl);
            prx.SkipMethodSecurity = true;
            prx.Invoke(method, args);
        }
    }
}
