namespace Brayns.Shaper.Fields
{
    public class Timestamp : BaseField
    {
        public new ulong Value
        {
            get { return (ulong)base.Value!; }
            internal set { base.Value = CheckValue(value); }
        }

        public new ulong XValue
        {
            get { return (ulong)base.XValue!; }
            internal set { base.XValue = CheckValue(value); }
        }

        public new ulong InitValue
        {
            get { return (ulong)base.InitValue!; }
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
            TestValue = Convert.ToUInt64(0);

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
            return Convert.ToUInt64(value!);
        }

        public override string Format()
        {
            return Value.ToString();
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(ulong value)
        {
            return new JValue(value);
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }
    }
}
