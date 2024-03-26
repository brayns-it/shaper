using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class BigInteger : BaseField, IInteger, INumeric
    {
        public new long Value
        {
            get { return (long)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new long XValue
        {
            get { return (long)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new long InitValue
        {
            get { return (long)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
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

        public static long EvaluateText(string text)
        {
            if (text.Length == 0) return 0;
            return long.Parse(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override object? CheckValue(object? value)
        {
            return Convert.ToInt64(value!);
        }

        public override string Format()
        {
            return FormatValue(Value, BlankZero);
        }

        public static string FormatValue(long val, bool blankZero = false)
        {
            if (blankZero && (val == 0))
                return "";
            else
                return val.ToString();
        }

        public override JValue Serialize()
        {
            return SerializeValue(Value);
        }

        public static JValue SerializeValue(long value)
        {
            return new JValue(value);
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }
    }
}
