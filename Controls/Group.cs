using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public enum LabelStyle
    {
        Horizontal,
        Vertical,
        Placeholder
    }

    public enum FieldPerRow
    {
        One,
        Two
    }

    public class Group : Control
    {
        public string Caption { get; set; } = "";
        public LabelStyle LabelStyle { get; set; } = LabelStyle.Horizontal;
        public FieldPerRow FieldPerRow { get; set; } = FieldPerRow.Two;
        public bool Collapsible { get; set; } = true;
        public bool Primary { get; set; } = false;

        public Group(ContentArea contentArea, string name, string caption)
        {
            Attach(contentArea);
            Caption = caption;
            Name = name;
        }

        public Group(ContentArea contentArea, string caption = "")
        {
            Attach(contentArea);
            Caption = caption;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["labelStyle"] = LabelStyle.ToString();
            jo["fieldPerRow"] = FieldPerRow.ToString();
            jo["collapsible"] = Collapsible;
            jo["primary"] = Primary;
            return jo;
        }
    }
}
