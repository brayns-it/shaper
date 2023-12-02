namespace Brayns.Shaper.Fields
{
    public class Guid : Field
    {
        public new System.Guid Value
        {
            get { return (System.Guid)_value!; }
            set { _value = CheckValue(value); }
        }

        public new System.Guid XValue
        {
            get { return (System.Guid)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new System.Guid InitValue
        {
            get { return (System.Guid)_initValue!; }
            set { _initValue = CheckValue(value); }
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

        internal override object? CheckValue(object? value)
        {
            return (System.Guid)value!;
        }

        internal override string Format(object? value)
        {
            var val = (System.Guid)value!;
            return val.ToString();
        }

        public void Validate(System.Guid value)
        {
            Validate<System.Guid>(value);
        }

        internal override object? Evaluate(string text)
        {
            throw new NotImplementedException();
        }
    }
}
