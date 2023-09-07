namespace Brayns.Shaper.Fields
{
    public class Decimal : Field, IDecimal, INumeric
    {
        public new decimal Value
        {
            get { return (decimal)_value!; }
            set { _value = CheckValue(value); }
        }

        public new decimal XValue
        {
            get { return (decimal)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new decimal InitValue
        {
            get { return (decimal)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public bool BlankZero { get; set; }
        public int Decimals { get; set; }

        public Decimal(string name, string caption)
        {
            Type = FieldType.DECIMAL;
            Name = name;
            Caption = caption;
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0m;
            Decimals = 2;
            BlankZero = false;
            HasFormat = true;

            Create();
        }

        internal override object? CheckValue(object? value)
        {
            return (decimal)value!;
        }

        internal override string Format(object? value)
        {
            var val = (decimal)value!;
            if (BlankZero && (val == 0))
                return "";
            else
            {
                if (Decimals <= 0)
                    return val.ToString("0", Session.CultureInfo);
                else
                    return val.ToString("0." + "".PadRight(Decimals, '0'), Session.CultureInfo);
            }
        }
    }
}
