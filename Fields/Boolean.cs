namespace Brayns.Shaper.Fields
{
    public class Boolean : Field
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
            Type = FieldType.BOOLEAN;
            Name = name;
            Caption = caption;
            Value = false;
            XValue = false;
            InitValue = false;
            TestValue = false;
            HasFormat = true;

            Create();
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
    }
}
