namespace Brayns.Shaper.Fields
{
    public class Time : DateTime
    {
        public Time(string name, string caption) : base(name, caption)
        {
            Type = FieldTypes.TIME;
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
    }
}
