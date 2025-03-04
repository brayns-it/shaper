using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public abstract class Option : BaseField
    {
        public Type? OptionType { get; internal set; }
    }

    public class Option<T> : Option where T: OptList
    {
        public new Opt<T> Value
        {
            get { return (Opt<T>)base.Value!; }
            set { base.Value = CheckValue(value); }
        }

        public new Opt<T> XValue
        {
            get { return (Opt<T>)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new Opt<T> InitValue
        {
            get { return (Opt<T>)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public Option(string name, string caption)
        {
            Type = FieldTypes.OPTION;
            Name = name;
            Caption = caption;
            Value = 0;
            XValue = 0;
            InitValue = 0;
            TestValue = 0;
            OptionType = typeof(T);
            HasFormat = true;

            Create();
        }

        public Option(string caption) : this("", caption)
        {
        }

        internal override object? CheckValue(object? value)
        {
            Opt<T> val;
            if ((value!.GetType() == typeof(int)) || (value!.GetType() == typeof(long)))
                val = Convert.ToInt32(value!);
            else
                val = (Opt<T>)value!;
            return val;
        }

        public override void Evaluate(string text)
        {
            Value = EvaluateText(text);
        }

        internal override void Evaluate(string text, out object? result)
        {
            result = EvaluateText(text);
        }

        public static Opt<T> EvaluateText(string text)
        {
            text = text.Trim();

            int i;
            if (int.TryParse(text, out i))
                return new Opt<T>(i);

            var l = (OptList)Activator.CreateInstance(typeof(T))!;
            
            foreach (int n in l.Names.Keys)
                if (l.Names[n].ToLower() == text.ToLower())
                    return new Opt<T>(n);

            foreach (int n in l.Captions.Keys)
                if (l.Captions[n].ToLower() == text.ToLower())
                    return new Opt<T>(n);
            
            throw new Error(Label("{0} does not represent a valid {1} type", text, typeof(T).Name));
        }

        public override string Format()
        {
            return FormatValue(Value);
        }

        public static string FormatValue(Opt<T> value)
        {
            return value.Caption;
        }

        public void SetRange(T value)
        {
            SetRange<T>(value);
        }

        public override JValue Serialize()
        {
            return SerializeJson(Value);
        }

        public static JValue SerializeJson(Opt<T> val)
        {
            return new JValue(val.Value);
        }

        public override void Deserialize(JValue? value)
        {
            Value = DeserializeJson(value);
        }

        public static Opt<T> DeserializeJson(JValue? value)
        {
            Opt<T> result = value!.ToObject<int>();
            return result;
        }

        public void SetFilter(params Opt<T>[] args)
        {
            string filter = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (filter.Length > 0) filter += "|";
                filter += "{" + i.ToString() + "}";
            }
            SetFilter(filter, args);
        }
    }
}
