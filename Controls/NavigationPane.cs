using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class NavigationPane : Control
    {
        public string Caption { get; set; } = "";

        public static NavigationPane Create(AppCenter center, string caption = "")
        {
            NavigationPane? ret = center.Page!.Control<NavigationPane>();
            if (ret == null)
            {
                ret = new NavigationPane();
                ret.Caption = caption;
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
