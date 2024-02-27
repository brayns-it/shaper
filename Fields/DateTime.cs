using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class DateTime : BaseField
    {
        public new System.DateTime Value
        {
            get { return (System.DateTime)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new System.DateTime XValue
        {
            get { return (System.DateTime)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new System.DateTime InitValue
        {
            get { return (System.DateTime)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
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

        public DateTime() : this("")
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (System.DateTime)value!;
        }

        public override string Format(object? value)
        {
            var val = (System.DateTime)value!;
            if (val == System.DateTime.MinValue)
                return "";
            else
                return val.ToString("G", Session.CultureInfo);
        }

        public override void Evaluate(string text, out object? result)
        {
            throw new NotImplementedException();
        }

        public override JValue Serialize(object? value)
        {
            var val = (System.DateTime)value!;
            return new JValue(val.ToString("o"));
        }

        public override void Deserialize(JValue? value, out object? result)
        {
            throw new NotImplementedException();
        }

        public void SetFilter(string expression, params DateTime[] pars)
        {
            SetFilter<DateTime>(expression, pars);
        }
    }
}
