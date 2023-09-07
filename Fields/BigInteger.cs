namespace Brayns.Shaper.Fields
{
    public class BigInteger : Field, IInteger, INumeric
    {
        public new long Value
        {
            get { return (long)_value!; }
            set { _value = CheckValue(value); }
        }

        public new long XValue
        {
            get { return (long)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new long InitValue
        {
            get { return (long)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public bool AutoIncrement { get; set; }
        public bool BlankZero { get; set; }

        public BigInteger(string name, string caption)
        {
            Type = FieldType.BIGINTEGER;
            Name = name;
            Caption = caption;
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0L;
            AutoIncrement = false;
            BlankZero = false;
            HasFormat = true;

            Create();
        }

        internal override object? CheckValue(object? value)
        {
            return (long)value!;
        }

        internal override string Format(object? value)
        {
            var val = (long)value!;
            if (BlankZero && (val == 0))
                return "";
            else
                return val.ToString();
        }
    }
}
