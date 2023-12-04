using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public enum LabelOrientation
    {
        Horizontal,
        Vertical
    }

    public enum FieldPerRow
    {
        One,
        Two
    }

    public class Group : Control
    {
        public string Caption { get; set; } = "";
        public LabelOrientation LabelOrientation { get; set; } = LabelOrientation.Horizontal;
        public FieldPerRow FieldPerRow { get; set; } = FieldPerRow.Two;
        public bool Collapsible { get; set; } = true;

        public Group(ContentArea contentArea, string caption = "")
        {
            SetParent(contentArea);
            Caption = caption;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["labelOrientation"] = LabelOrientation.ToString();
            jo["fieldPerRow"] = FieldPerRow.ToString();
            jo["collapsible"] = Collapsible;
            return jo;
        }
    }
}
