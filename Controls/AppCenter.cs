using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class AppCenter : Control
    {
        public AppCenter(BasePage page)
        {
            SetParent(page);
        }
    }
}
