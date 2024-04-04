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

        public Date() : this("", "")
        {
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        public new static string FormatValue(System.DateTime val)
        {
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToLocalTime().ToString("d", Session.CultureInfo);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        public new static System.DateTime EvaluateText(string text)
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

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(System.DateTime val)
        {
            return new JValue(val.ToString("yyyy-MM-dd"));
        }

        public override void Deserialize(JValue? value)
        {
            Value = DeserializeJson(value);
        }

        public static System.DateTime DeserializeJson(JValue? value)
        {
            string val = value!.ToString();
            if (val.Length == 0)
                return System.DateTime.MinValue;
            else
                return System.DateTime.ParseExact(value!.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
