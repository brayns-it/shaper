using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Guid : BaseField
    {
        public new System.Guid Value
        {
            get { return (System.Guid)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new System.Guid XValue
        {
            get { return (System.Guid)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new System.Guid InitValue
        {
            get { return (System.Guid)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public Guid(string name, string caption)
        {
            Type = FieldTypes.GUID;
            Name = name;
            Caption = caption;
            Value = System.Guid.Empty;
            XValue = System.Guid.Empty;
            InitValue = System.Guid.Empty;
            TestValue = System.Guid.Empty;

            Create();
        }

        public Guid(string caption) : this("", caption)
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (System.Guid)value!;
        }

        public static string FormatValue(System.Guid val)
        {
            return val.ToString();
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        public void Validate(System.Guid value)
        {
            base.Validate(value);
        }

        public override void Evaluate(string text)
        {
            throw new NotImplementedException();
        }

        internal override void Evaluate(string text, out object? result)
        {
            throw new NotImplementedException();
        }

        public override JValue Serialize()
        {
            return SerializeValue(Value);
        }

        public static JValue SerializeValue(System.Guid val)
        {
            return new JValue(val.ToString());
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }

        public void SetRange(Guid value)
        {
            SetRange<Guid>(value);
        }
    }
}
