using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Indicator : Control
    {
        public string Caption { get; set; } = "";

        public Indicator(AppCenter center)
        {
            SetParent(center);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
