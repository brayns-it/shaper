using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class BigInteger : BaseField, IInteger, INumeric
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
            Type = FieldTypes.BIGINTEGER;
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

        public BigInteger(string caption) : this("", caption)
        {
        }

        public long Evaluate(string text)
        {
            if (text.Length == 0) return 0;
            return long.Parse(text);
        }

        public override void Evaluate(string text, out object? result)
        {
            result = Evaluate(text);
        }

        internal override object? CheckValue(object? value)
        {
            return Convert.ToInt64(value!);
        }

        public override string Format(object? value)
        {
            var val = (long)value!;
            if (BlankZero && (val == 0))
                return "";
            else
                return val.ToString();
        }

        public override JValue Serialize(object? value)
        {
            return new JValue((long)value!);
        }

        public override void Deserialize(JValue? value, out object? result)
        {
            throw new NotImplementedException();
        }
    }
}
