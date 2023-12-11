﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Html : Control
    {
        public string Content { get; set; }

        public Html(ContentArea area, string name = "")
        {
            Attach(area);
            Content = "";
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
