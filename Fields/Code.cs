namespace Brayns.Shaper.Fields
{
    public class Code : Text
    {
        public Code(string name, string caption, int length) : base(name, caption, length)
        {
            Type = FieldTypes.CODE;
        }

        public Code(string caption) : this("", caption, 0)
        {
        }

        public Code() : this("", "", 0)
        {
        }

        internal override object? CheckValue(object? value)
        {
            string val = (string)value!;
            val = val.Trim();
            val = val.ToUpper();
            return base.CheckValue(val);
        }
    }
}
