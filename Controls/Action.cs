using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public delegate void ActionTriggerHandler();

    public class Action : Control
    {
        public string Caption { get; protected set; } = "";
        public string Icon { get; protected set; } = "";
        public event ActionTriggerHandler? Triggering;

        public Action(ActionArea area, string caption = "", string icon = "")
        {
            SetParent(area);
            Caption = caption;
            Icon = icon;
        }

        public Action(Action action, string caption = "", string icon = "")
        {
            SetParent(action);
            Caption = caption;
            Icon = icon;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["icon"] = Icon;
            return jo;
        }

        internal void Trigger()
        {
            Triggering?.Invoke();
        }
    }
}
