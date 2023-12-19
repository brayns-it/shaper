using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Brayns.Shaper.Fields
{
    public class Date : DateTime
    {
        public Date(string name, string caption) : base(name, caption)
        {
            Type = FieldTypes.DATE;
        }

        public Date(string caption) : this("", caption)
        {
        }

        internal override string Format(object? value)
        {
            var val = (System.DateTime)value!;
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToLocalTime().ToString("d", Session.CultureInfo);
        }

        internal override object? Evaluate(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return System.DateTime.MinValue;

            var m2 = Regex.Match(text, "^(\\d{2})(\\d{2})(\\d{2,4})$");
            if (m2.Success)
                return System.DateTime.Parse(m2.Groups[1] + "/" + m2.Groups[2] + "/" + m2.Groups[3]);

            var m3 = Regex.Match(text, "^(\\d{2})(\\d{2})$");
            if (m3.Success)
                return System.DateTime.Parse(m3.Groups[1] + "/" + m3.Groups[2] + "/" + System.DateTime.Now.Year.ToString());

            return System.DateTime.Parse(text, Session.CultureInfo);
        }

        internal override JValue Serialize(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("yyyy-MM-dd"));
        }
    }
}
