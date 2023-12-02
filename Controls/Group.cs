using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Group : Control
    {
        public string Caption { get; protected set; } = "";

        public Group(ContentArea contentArea, string caption = "")
        {
            SetParent(contentArea);
            Caption = caption;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
