namespace Brayns.Shaper.Fields
{
    public class Blob : Field
    {
        public new byte[]? Value
        {
            get { return (byte[]?)_value; }
            set { _value = CheckValue(value); }
        }

        public new byte[]? XValue
        {
            get { return (byte[]?)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new byte[]? InitValue
        {
            get { return (byte[]?)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public Blob(string name, string caption)
        {
            Type = FieldTypes.BLOB;
            Name = name;
            Caption = caption;
            Value = null;
            XValue = null;
            InitValue = null;
            TestValue = null;

            Create();
        }

        internal override object? Evaluate(string text)
        {
            throw new NotImplementedException();
        }

        internal override object? CheckValue(object? value)
        {
            return (byte[]?)value;
        }

        internal override string Format(object? value)
        {
            if ((value == null) || (((byte[])value).Length == 0))
                return "";
            else
                return "*";
        }
    }
}
