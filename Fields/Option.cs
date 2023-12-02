namespace Brayns.Shaper.Fields
{
    public abstract class Option : Field
    {
        public Type? OptionType { get; internal set; }
    }

    public class Option<T> : Option
    {
        public new Series<T> Value
        {
            get { return (Series<T>)_value!; }
            set { _value = CheckValue(value); }
        }

        public new Series<T> XValue
        {
            get { return (Series<T>)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new Series<T> InitValue
        {
            get { return (Series<T>)_initValue!; }
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

            Create();
        }

        internal override object? CheckValue(object? value)
        {
            return (Series<T>)value!;
        }

        internal override object? Evaluate(string text)
        {
            text = text.Trim();

            int i;
            if (int.TryParse(text, out i))
                return new Series<T>(i);
            
            foreach (int n in Value.Names.Keys)
                if (Value.Names[n].ToLower() == text.ToLower())
                    return new Series<T>(n);

            foreach (int n in Value.Captions.Keys)
                if (Value.Captions[n].ToLower() == text.ToLower())
                    return new Series<T>(n);

            throw new Error(Label("{0} does not represent a valid {1} type"), text, Value.Type!.Name);
        }

        internal override string Format(object? value)
        {
            return ((Series<T>)value!).Caption;
        }

        public void SetRange(T value)
        {
            SetRange<T>(value);
        }
    }
}
