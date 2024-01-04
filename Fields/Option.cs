using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public abstract class Option : BaseField
    {
        public Type? OptionType { get; internal set; }
    }

    public class Option<T> : Option
    {
        public new Opt<T> Value
        {
            get { return (Opt<T>)_value!; }
            set { _value = CheckValue(value); }
        }

        public new Opt<T> XValue
        {
            get { return (Opt<T>)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new Opt<T> InitValue
        {
            get { return (Opt<T>)_initValue!; }
            set { _initValue = CheckValue(value); }
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
            if (value!.GetType() == typeof(int))
                val = (int)value!;
            else
                val = (Opt<T>)value!;
            return val;
        }

        internal override object? DoEvaluate(string text)
        {
            text = text.Trim();

            int i;
            if (int.TryParse(text, out i))
                return new Opt<T>(i);
            
            foreach (int n in Value.Names.Keys)
                if (Value.Names[n].ToLower() == text.ToLower())
                    return new Opt<T>(n);

            foreach (int n in Value.Captions.Keys)
                if (Value.Captions[n].ToLower() == text.ToLower())
                    return new Opt<T>(n);

            throw new Error(Label("{0} does not represent a valid {1} type", text, Value.Type!.Name));
        }

        internal override string Format(object? value)
        {
            return ((Opt<T>)value!).Caption;
        }

        public void SetRange(T value)
        {
            SetRange<T>(value);
        }

        internal override JValue Serialize(object? value)
        {
            var val = (Opt<T>)value!;
            return new JValue(val.Value);
        }
    }
}
