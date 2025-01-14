using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public delegate void ActionTriggerHandler();

    public class Action : Control
    {
        public string Caption { get; set; } = "";
        public Icon? Icon { get; set; }
        public event ActionTriggerHandler? Triggering;
        public string Shortcut { get; set; } = "";
        public Type? PermissionBy { get; set; } = null;
        public bool IsCancelation { get; set; } = false;
        public bool Disabled { get; set; } = false;

        private Type? _run;
        public Type? Run
        {
            get { return _run; }
            set
            {
                if (value != null)
                    if (!typeof(BasePage).IsAssignableFrom(value))
                        throw new Error(Label("Run type must be derived from BasePage"));
                _run = value;
            }
        }

        public bool RunAsPrincipal { get; set; } = false;

        public Action(ActionArea area, string name, string caption, Icon? icon = null)
        {
            Init(area, name, caption);
            Icon = icon;
        }

        public Action(ActionArea area, string caption, Icon? icon = null)
        {
            Init(area, "", caption);
            Icon = icon;
        }

        public Action(Action action, string name, string caption, Icon? icon = null)
        {
            Init(action, name, caption);
            Icon = icon;
        }

        public Action(Action action, string caption, Icon? icon = null)
        {
            Init(action, "", caption);
            Icon = icon;
        }

        public Action(ActionGroup actionGroup, string name, string caption, Icon? icon = null)
        {
            Init(actionGroup, name, caption);
            Icon = icon;
        }

        public Action(ActionGroup actionGroup, string caption, Icon? icon = null)
        {
            Init(actionGroup, "", caption);
            Icon = icon;
        }

        public Action(UserCenter center, string name, string caption, Icon? icon = null)
        {
            Init(center, name, caption);
            Icon = icon;
        }

        public Action(UserCenter center, string caption, Icon? icon = null)
        {
            Init(center, "", caption);
            Icon = icon;
        }

        public Action(Notifications notif, string name, string caption, Icon? icon = null)
        {
            Init(notif, name, caption);
            Icon = icon;
        }

        public Action(Notifications notif, string caption, Icon? icon = null)
        {
            Init(notif, "", caption);
            Icon = icon;
        }

        public Action(Group group, string name, string caption)
        {
            Init(group, name, caption);
        }

        public Action(Group group, string caption)
        {
            Init(group, "", caption);
        }

        private void Init(Control parent, string name, string caption)
        {
            Attach(parent);
            Caption = caption;
            Name = name;

            if (HasParentOfType(typeof(NavigationPane)))
                RunAsPrincipal = true;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["icon"] = (Icon != null) ? Icon.ToString() : "";
            jo["shortcut"] = Shortcut;
            jo["isCancelation"] = IsCancelation;
            jo["disabled"] = Disabled;
            return jo;
        }

        internal void Trigger()
        {
            if (Run != null)
            {
                if (RunAsPrincipal)
                    foreach (var u in CurrentSession.Units.Values)
                        if (u != Page)
                            if (typeof(BasePage).IsAssignableFrom(u.GetType()))
                            {
                                var bp = ((BasePage)u);
                                if (bp.Parent == null)
                                    bp.Close();
                            }

                var p = (BasePage)Activator.CreateInstance(Run)!;
                p.Run();
            }
            else
                Triggering?.Invoke();
        }
    }
}
