using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Boolean : BaseField
    {
        public new bool Value
        {
            get { return (bool)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new bool XValue
        {
            get { return (bool)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new bool InitValue
        {
            get { return (bool)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public Boolean(string name, string caption)
        {
            Type = FieldTypes.BOOLEAN;
            Name = name;
            Caption = caption;
            Value = false;
            XValue = false;
            InitValue = false;
            TestValue = false;
            HasFormat = true;

            Create();
        }

        public Boolean(string caption) : this("", caption)
        {
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public static bool EvaluateText(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return false;
            if (text == Label("Yes")) return true;
            if (text == "1") return true;
            if (text == Label("No")) return true;
            if (text == "0") return true;

            throw new Error(Label("{0} does not represent a valid boolean type", text));
        }

        internal override object? CheckValue(object? value)
        {
            return (bool)value!;
        }

        public static string FormatValue(bool val)
        {
            if (val)
                return Label("Yes");
            else
                return Label("No");
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        public void SetRange(bool value)
        {
            SetRange<bool>(value);
        }

        public static JValue SerializeValue(bool value)
        {
            return new JValue(value);
        }

        public override JValue Serialize()
        {
            return SerializeValue(Value);
        }

        public override void Deserialize(JValue? value)
        {
            throw new NotImplementedException();
        }
    }
}
