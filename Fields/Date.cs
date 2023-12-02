namespace Brayns.Shaper.Fields
{
    public class Date : DateTime
    {
        public Date(string name, string caption) : base(name, caption)
        {
            Type = FieldTypes.DATE;
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
    }
}
