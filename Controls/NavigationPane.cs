using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class NavigationPane : Control
    {
        public static NavigationPane Create(AppCenter center)
        {
            NavigationPane? ret = center.Page!.Control<NavigationPane>();
            if (ret == null)
            {
                ret = new NavigationPane();
                ret.Attach(center);
            }
            return ret;
        }
    }
}
