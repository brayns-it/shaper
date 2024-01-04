using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Grid : Control
    {
        public Grid(ContentArea area, string name = "")
        {
            Attach(area);
            Name = name;

            if (Page != null)
                Page.MultipleRows = true;
        }

        internal override JObject Render()
        {
            if (Page!.Card != null)
            {
                Field? first = null;
                Field? ctlOpen = null;
                foreach (var c in Items.OfType<Controls.Field>())
                {
                    if (first == null)
                        first = c;

                    if (c.OpenRecord)
                        ctlOpen = c;
                }
                if (ctlOpen == null) ctlOpen = first;
                if (ctlOpen != null)
                    ctlOpen.Triggering += () => Page!.OpenRecord();
            }

            var jo = base.Render();
            jo["labelNodata"] = Label("No data available");
            return jo;
        }
    }
}
