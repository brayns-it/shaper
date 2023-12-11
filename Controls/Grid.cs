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
            var jo = base.Render();
            jo["labelNodata"] = Label("No data available");
            return jo;
        }
    }
}
