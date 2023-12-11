using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class DateTime : BaseField
    {
        public new System.DateTime Value
        {
            get { return (System.DateTime)_value!; }
            set { _value = CheckValue(value); }
        }

        public new System.DateTime XValue
        {
            get { return (System.DateTime)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new System.DateTime InitValue
        {
            get { return (System.DateTime)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public DateTime(string name, string caption)
        {
            Type = FieldTypes.DATETIME;
            Name = name;
            Caption = caption;
            Value = System.DateTime.MinValue;
            XValue = System.DateTime.MinValue;
            InitValue = System.DateTime.MinValue;
            TestValue = System.DateTime.MinValue;
            HasFormat = true;

            Create();
        }

        public DateTime(string caption) : this("", caption)
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (System.DateTime)value!;
        }

        internal override string Format(object? value)
        {
            var val = (System.DateTime)value!;
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToString("G", Session.CultureInfo);
        }

        internal override object? Evaluate(string text)
        {
            throw new NotImplementedException();
        }

        internal override JValue Serialize(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("o"));
        }

        public void SetFilter(string expression, params DateTime[] pars)
        {
            SetFilter<DateTime>(expression, pars);
        }
    }
}
