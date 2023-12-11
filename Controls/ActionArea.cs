using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class ActionArea : Control
    {
        public static ActionArea Create(BasePage page)
        {
            ActionArea? ret = page.Control<ActionArea>();
            if (ret == null)
            {
                ret = new ActionArea();
                ret.Attach(page);
            }
            return ret;
        }
    }
}
