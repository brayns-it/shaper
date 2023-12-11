using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class Search : Control
    {
        public string Caption { get; set; } = "";

        public static Search Create(AppCenter center)
        {
            Search? ret = center.Page!.Control<Search>();
            if (ret == null)
            {
                ret = new Search();
                ret.Attach(center);
            }
            return ret;
        }
    }
}
