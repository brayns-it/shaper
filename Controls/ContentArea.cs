using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class ContentArea : Control
    {
        public ContentArea(BasePage page)
        {
            SetParent(page);
        }
    }
}
