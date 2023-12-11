using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class ActionGroup : Control
    {
        public string Caption { get; set; } = "";

        public ActionGroup(NavigationPane pane, string name, string caption)
        {
            Attach(pane);
            Caption = caption;
            Name = name;
        }

        public ActionGroup(NavigationPane pane, string caption)
        {
            Attach(pane);
            Caption = caption;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
