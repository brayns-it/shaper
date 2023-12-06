using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Search : Control
    {
        public string Caption { get; set; } = "";

        public Search(AppCenter center)
        {
            SetParent(center);
            Caption = Label("Search");
        }
    }
}
