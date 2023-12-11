using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Integer : BaseField, IInteger, INumeric
    {
        public new int Value
        {
            get { return (int)_value!; }
            set { _value = CheckValue(value); }
        }

        public new int XValue
        {
            get { return (int)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new int InitValue
        {
            get { return (int)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public bool AutoIncrement { get; set; }
        public bool BlankZero { get; set; }

        public Integer(string name, string caption)
        {
            Type = FieldTypes.INTEGER;
            Name = name;
            Caption = caption;
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0;
            AutoIncrement = false;
            BlankZero = false;
            HasFormat = true;

            Create();
        }

        public Integer(string caption) : this("", caption)
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (int)value!;
        }

        internal override object? Evaluate(string text)
        {
            return int.Parse(text);
        }

        internal override string Format(object? value)
        {
            var val = (int)value!;
            if (BlankZero && (val == 0))
                return "";
            else
                return val.ToString();
        }

        public void SetRange(int value)
        {
            SetRange<int>(value);
        }

        public void SetRange(int minValue, int maxValue)
        {
            SetRange<int>(minValue, maxValue);
        }

        internal override JValue Serialize(object? value)
        {
            var val = (System.Guid)value!;
            return new JValue(val.ToString());
        }
    }
}
