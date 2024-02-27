using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Integer : BaseField, IInteger, INumeric
    {
        public new int Value
        {
            get { return (int)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new int XValue
        {
            get { return (int)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new int InitValue
        {
            get { return (int)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
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

        public Integer() : this("")
        {
        }

        internal override object? CheckValue(object? value)
        {
            return Convert.ToInt32(value!);
        }

        public override void Evaluate(string text, out object? result)
        {
            result = Evaluate(text);
        }

        public int Evaluate(string text)
        {
            return int.Parse(text.Trim());
        }

        public override string Format(object? value)
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

        public override JValue Serialize(object? value)
        {
            return new JValue((int)value!);
        }

        public override void Deserialize(JValue? value, out object? result)
        {
            throw new NotImplementedException();
        }
    }
}
