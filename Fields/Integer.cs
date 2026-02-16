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

        public void SetFilter(string expression, params int[] pars)
        {
            SetFilter<int>(expression, pars);
        }

        internal override object? CheckValue(object? value)
        {
            return Convert.ToInt32(value!);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public static int EvaluateText(string text)
        {
            if (text.Trim().Length == 0)
                return 0;

            return int.Parse(text.Trim());
        }

        public static string FormatValue(int val, bool blankZero = false)
        {
            if (blankZero && (val == 0))
                return "";
            else
                return val.ToString();
        }

        public override string Format()
        {
            return FormatValue(Value, BlankZero);
        }

        public void SetRange(int value)
        {
            SetRange<int>(value);
        }

        public void SetRange(int minValue, int maxValue)
        {
            SetRange<int>(minValue, maxValue);
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(int value)
        {
            return new JValue(value);
        }

        public override void Deserialize(JValue? value)
        {
            Value = DeserializeJson(value);
        }

        public static int DeserializeJson(JValue? value)
        {
            return value!.ToObject<int>();
        }

        public int Max()
        {
            return Table!.TableDatabase!.Max<int>(Table!, this);
        }

        public int Min()
        {
            return Table!.TableDatabase!.Min<int>(Table!, this);
        }
    }
}
