﻿namespace Brayns.Shaper.Fields
{
    public class Text : BaseField
    {
        public const int MAX_LENGTH = -1;

        public int Length { get; internal set; }
        public bool Binary { get; set; }
        public bool ANSI { get; set; }

        public new string Value
        {
            get { return (string)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new string XValue
        {
            get { return (string)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new string InitValue
        {
            get { return (string)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public Text(string name, string caption, int length)
        {
            Type = FieldTypes.TEXT;
            Name = name;
            Caption = caption;
            Length = length;
            Value = "";
            XValue = "";
            InitValue = "";
            TestValue = "";
            Binary = false;
            ANSI = false;

            Create();
        }

        public Text(string caption) : this("", caption, 0)
        {
        }

        public Text() : this("", "", 0)
        {
        }

        internal override object? CheckValue(object? value)
        {
            string val = (string)value!;
            if (Length > MAX_LENGTH)
                if (val.Length > Length)
                    throw new Error(Label("Value of '{0}' cannot be longer than {1}", Caption, Length));

            return val;
        }

        public static string FormatValue(string value)
        {
            return value;
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public static string EvaluateText(string text)
        {
            return text;
        }

        public void Validate(string value)
        {
            base.Validate(value);
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(string value)
        {
            return new JValue(value);
        }

        public void SetFilter(string expression, params string[] pars)
        {
            SetFilter<string>(expression, pars);
        }

        public override void Deserialize(JValue? value)
        {
            Value = DeserializeJson(value);
        }

        public static string DeserializeJson(JValue? value)
        {
            return value!.ToString();
        }
    }
}
