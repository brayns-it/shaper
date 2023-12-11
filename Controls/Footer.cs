using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Footer : Control
    {
        private string _caption = "";
        public string Caption
        {
            get { return _caption; }
            set
            {
                _caption = value;
                _caption = _caption.Replace("%Y", DateTime.Now.Year.ToString());
            }
        }

        public static Footer Create(BasePage page)
        {
            Footer? ret = page.Control<Footer>();
            if (ret == null)
            {
                ret = new Footer();
                ret.Attach(page);
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
