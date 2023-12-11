using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Subpage : Control
    {
        public string Caption { get; set; } = "";
        public Type PageType { get; init; }

        public Subpage(ContentArea contentArea, string name, Type pageType, string caption)
        {
            Attach(contentArea);
            Caption = caption;
            Name = name;
            PageType = pageType;
        }

        public Subpage(ContentArea contentArea, Type pageType, string caption = "")
        {
            Attach(contentArea);
            Caption = caption;
            PageType = pageType;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }
    }
}
