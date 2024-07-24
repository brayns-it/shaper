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
            return SerializeJson(Value);
        }

        public override void Deserialize(JValue? value)
        {
            Value = DeserializeJson(value);
        }

        public new static JValue SerializeJson(System.DateTime value)
        {
            if (value == System.DateTime.MinValue)
                return new JValue("");
            else
                return new JValue(value.ToString("HH:mm:ss.fff"));
        }

        public new static System.DateTime DeserializeJson(JValue? value)
        {
            string val = value!.ToString();
            if (val.Length == 0)
                return System.DateTime.MinValue;
            else
            {
                string fmt = "HH:mm:ss";
                if (val.Length > 9)
                    fmt += "." + "".PadRight(val.Length - 9, 'f');
                return System.DateTime.ParseExact(value!.ToString(), fmt, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
