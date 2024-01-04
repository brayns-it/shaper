using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class DetailArea : Control
    {
        public static DetailArea Create(BasePage page)
        {
            DetailArea? ret = page.Control<DetailArea>();
            if (ret == null)
            {
                ret = new DetailArea();
                ret.Attach(page);
            }
            return ret;
        }
    }
}
