using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public delegate void ActionTriggerHandler();

    public class Action : Control
    {
        public string Caption { get; set; } = "";
        public string Icon { get; set; } = "";
        public event ActionTriggerHandler? Triggering;
        public string Shortcut { get; set; } = "";

        public Action(ActionArea area, string caption = "", string icon = "")
        {
            SetParent(area);
            Caption = caption;
            Icon = icon;
        }

        public Action(Group group, string caption = "")
        {
            SetParent(group);
            Caption = caption;
        }

        public Action(Action action, string caption = "", string icon = "")
        {
            SetParent(action);
            Caption = caption;
            Icon = icon;
        }

        public Action(UserCenter center, string caption = "", string icon = "")
        {
            SetParent(center);
            Caption = caption;
            Icon = icon;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["icon"] = Icon;
            jo["shortcut"] = Shortcut;
            return jo;
        }

        internal void Trigger()
        {
            Triggering?.Invoke();
        }
    }
}
