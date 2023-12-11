using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class ContentArea : Control
    {
        public static ContentArea Create(BasePage page)
        {
            ContentArea? ret = page.Control<ContentArea>();
            if (ret == null)
            {
                ret = new ContentArea();
                ret.Attach(page);
            }
            return ret;
        }
    }
}
