using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Indicator : Control
    {
        public string Caption { get; set; } = "";

        public static Indicator Create(AppCenter center)
        {
            Indicator? ret = center.Page!.Control<Indicator>();
            if (ret == null)
            {
                ret = new Indicator();
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
