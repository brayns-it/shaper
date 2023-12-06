using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class UserCenter : Control
    {
        public string Caption { get; set; } = "";

        public UserCenter(AppCenter center)
        {
            SetParent(center);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
