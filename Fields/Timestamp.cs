using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Timestamp : BaseField
    {
        public new long Value
        {
            get { return (long)base.Value!; }
            internal set { base.Value = CheckValue(value); }
        }

        public new long XValue
        {
            get { return (long)base.XValue!; }
            internal set { base.XValue = CheckValue(value); }
        }

        public new long InitValue
        {
            get { return (long)base.InitValue!; }
            internal set { base.InitValue = CheckValue(value); }
        }

        public Timestamp()
        {
            Type = FieldTypes.TIMESTAMP;
            Name = "timestamp";
            Caption = "timestamp";
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0L;

            Create();
        }

        internal override void Evaluate(string text, out object? result)
        {
            throw new NotImplementedException();
        }

        public override void Evaluate(string text)
        {
            throw new NotImplementedException();
        }

        internal override object? CheckValue(object? value)
        {
            return Convert.ToInt64(value!);
        }

        public override string Format()
        {
            return Value.ToString();
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(long value)
        {
            return new JValue(value);
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }
    }
}
