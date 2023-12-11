using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class AppCenter : Control
    {
        public static AppCenter Create(BasePage page)
        {
            AppCenter? ret = page.Control<AppCenter>();
            if (ret == null)
            {
                ret = new AppCenter();
                ret.Attach(page);
            }
            return ret;
        }
    }
}
