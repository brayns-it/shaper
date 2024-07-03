using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class DateTime : BaseField
    {
        public new System.DateTime Value
        {
            get { return (System.DateTime)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new System.DateTime XValue
        {
            get { return (System.DateTime)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new System.DateTime InitValue
        {
            get { return (System.DateTime)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public DateTime(string name, string caption)
        {
            Type = FieldTypes.DATETIME;
            Name = name;
            Caption = caption;
            Value = System.DateTime.MinValue;
            XValue = System.DateTime.MinValue;
            InitValue = System.DateTime.MinValue;
            TestValue = System.DateTime.MinValue;
            HasFormat = true;

            Create();
        }

        public DateTime(string caption) : this("", caption)
        {
        }

        public DateTime() : this("")
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (System.DateTime)value!;
        }

        public static string FormatValue(System.DateTime val)
        {
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToString("G", Session.CultureInfo);
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        public static System.DateTime EvaluateText(string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return System.DateTime.MinValue; 

            string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            System.DateTime date = Date.EvaluateText(parts[0]);
            System.DateTime time = System.DateTime.MinValue;
            if (parts.Length > 1)
            {
                time = Time.EvaluateText(parts[1]);
                date.AddHours(time.Hour);
                date.AddMinutes(time.Minute);
                date.AddSeconds(time.Second);
            }
            return date;
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("o"));
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
                return System.DateTime.ParseExact(value!.ToString(), "o", System.Globalization.CultureInfo.InvariantCulture);
        }

        public void SetFilter(string expression, params DateTime[] pars)
        {
            SetFilter<DateTime>(expression, pars);
        }
    }
}
