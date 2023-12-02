namespace Brayns.Shaper.Fields
{
    public class Code : Text
    {
        public Code(string name, string caption, int length) : base(name, caption, length)
        {
            Type = FieldTypes.CODE;
        }

        internal override object? CheckValue(object? value)
        {
            string val = (string)value!;
            val = val.Trim();
            val = val.ToUpper();
            return base.CheckValue(val);
        }

        internal override object? Evaluate(string text)
        {
            return text;
        }
    }
}
