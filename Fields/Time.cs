using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Brayns.Shaper.Fields
{
    public class Time : DateTime
    {
        public Time(string name, string caption) : base(name, caption)
        {
            Type = FieldTypes.TIME;
        }

        public Time(string caption) : this("", caption)
        {
        }

        public Time() : this("", "")
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
                return val.ToString("T", Session.CultureInfo);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public new static System.DateTime EvaluateText(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return System.DateTime.MinValue;

            var m2 = Regex.Match(text, "^(\\d{2})(\\d{2})(\\d{2})$");
            if (m2.Success)
                return System.DateTime.Parse(m2.Groups[1] + ":" + m2.Groups[2] + ":" + m2.Groups[3]);

            var m3 = Regex.Match(text, "^(\\d{2})(\\d{2})$");
            if (m3.Success)
                return System.DateTime.Parse(m3.Groups[1] + ":" + m3.Groups[2]);

            return System.DateTime.Parse(text, Session.CultureInfo);
        }

        public override JValue Serialize()
        {
            return SerializeValue(Value);
        }

        public static JValue SerializeValue(System.DateTime val)
        {
            return new JValue(val.ToString("HH:mm:ss.fff"));
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }
    }
}
