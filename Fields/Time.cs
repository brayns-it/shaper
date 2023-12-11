using Newtonsoft.Json.Linq;

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

        internal override string Format(object? value)
        {
            var val = (System.DateTime)value!;
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToString("T", Session.CultureInfo);
        }

        internal override object? Evaluate(string text)
        {
            throw new NotImplementedException();
        }

        internal override JValue Serialize(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("HH:mm:ss.fff"));
        }
    }
}
