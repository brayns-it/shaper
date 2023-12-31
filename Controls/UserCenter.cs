﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class UserCenter : Control
    {
        public string Caption { get; set; } = "";

        public static UserCenter Create(AppCenter center)
        {
            UserCenter? ret = center.Page!.Control<UserCenter>();
            if (ret == null)
            {
                ret = new UserCenter();
                ret.Attach(center);
            }
            return ret;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
