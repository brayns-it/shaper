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
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["icon"] = (Icon != null) ? Icon.ToString() : "";
            jo["shortcut"] = Shortcut;
            return jo;
        }

        internal void Trigger()
        {
            if (Run != null)
            {
                var p = (BasePage)Activator.CreateInstance(Run)!;
                p.Run();
            }
            else
                Triggering?.Invoke();
        }
    }
}
