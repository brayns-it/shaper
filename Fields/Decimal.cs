using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Decimal : BaseField, IDecimal, INumeric
    {
        public new decimal Value
        {
            get { return (decimal)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new decimal XValue
        {
            get { return (decimal)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new decimal InitValue
        {
            get { return (decimal)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public bool BlankZero { get; set; }
        public int Decimals { get; set; }

        public Decimal(string name, string caption)
        {
            Type = FieldTypes.DECIMAL;
            Name = name;
            Caption = caption;
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0m;
            Decimals = 2;
            BlankZero = false;
            HasFormat = true;

            Create();
        }

        public Decimal(string caption) : this("", caption)
        {
        }

        internal override object? CheckValue(object? value)
        {
            return (decimal)value!;
        }

        public override string Format(object? value)
        {
            var val = (decimal)value!;
            if (BlankZero && (val == 0))
                return "";
            else
            {
                if (Decimals <= 0)
                    return val.ToString("#,##0", Session.CultureInfo);
                else
                    return val.ToString("#,##0." + "".PadRight(Decimals, '0'), Session.CultureInfo);
            }
        }

        public override JValue Serialize(object? value)
        {
            var val = (decimal)value!;
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberGroupSeparator = "";
            nfi.NumberDecimalSeparator = ".";
            return new JValue(val.ToString("G29", nfi));
        }

        public override void Deserialize(JValue? value, out object? result)
        {
            result = Deserialize(value);
        }

        public decimal Deserialize(JValue? value)
        {
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberGroupSeparator = "";
            nfi.NumberDecimalSeparator = ".";
            return decimal.Parse(value!.ToString(), nfi);
        }

        public override void Evaluate(string text, out object? result)
        {
            result = Evaluate(text);
        }

        public static decimal Evaluate(string text)
        {
            text = text.Replace(",", ".");

            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberGroupSeparator = "";
            nfi.NumberDecimalSeparator = ".";
            return decimal.Parse(text, nfi);
        }
    }
}
