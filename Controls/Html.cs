using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Html : Control
    {
        public string Content { get; set; } = "";

        public Html(ContentArea contentArea, string name = "")
        {
            Attach(contentArea);
            Name = name;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["content"] = Content;
            return jo;
        }
    }
}
