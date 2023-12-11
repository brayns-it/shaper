using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Boolean : BaseField
    {
        public new bool Value
        {
            get { return (bool)_value!; }
            set { _value = CheckValue(value); }
        }

        public new bool XValue
        {
            get { return (bool)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new bool InitValue
        {
            get { return (bool)_initValue!; }
            set { _initValue = CheckValue(value); }
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

        internal override object? Evaluate(string text)
        {
            text = text.Trim();
            if (text.Length == 0) return false;
            if (text == Label("Yes")) return true;
            if (text == "1") return true;
            if (text == Label("No")) return true;
            if (text == "0") return true;

            throw new Error(Label("{0} does not represent a valid boolean type"), text);
        }

        internal override object? CheckValue(object? value)
        {
            return (bool)value!;
        }

        internal override string Format(object? value)
        {
            var val = (bool)value!;
            if (val)
                return Label("Yes");
            else
                return Label("No");
        }

        public void SetRange(bool value)
        {
            SetRange<bool>(value);
        }

        internal override JValue Serialize(object? value)
        {
            return new JValue((bool)value!);
        }
    }
}
