using Newtonsoft.Json.Linq;

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
            throw new NotImplementedException();
        }

        internal override JValue Serialize(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("yyyy-MM-dd"));
        }
    }
}
