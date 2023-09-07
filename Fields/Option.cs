namespace Brayns.Shaper.Fields
{
    public abstract class Option : Field
    {
        public Type? OptionType { get; internal set; }
    }

    public class Option<T> : Option where T : Brayns.Shaper.Objects.Option<T>
    {
        public new T Value
        {
            get { return (T)_value!; }
            set { _value = CheckValue(value); }
        }

        public new T XValue
        {
            get { return (T)_xValue!; }
            set { _xValue = CheckValue(value); }
        }

        public new T InitValue
        {
            get { return (T)_initValue!; }
            set { _initValue = CheckValue(value); }
        }

        public Option(string name, string caption)
        {
            Type = FieldType.OPTION;
            Name = name;
            Caption = caption;
            Value = Brayns.Shaper.Objects.Option.GetDefault<T>();
            XValue = Brayns.Shaper.Objects.Option.GetDefault<T>();
            InitValue = Brayns.Shaper.Objects.Option.GetDefault<T>();
            TestValue = Brayns.Shaper.Objects.Option.GetDefault<T>();
            OptionType = typeof(T);

            Create();
        }

        internal override object? CheckValue(object? value)
        {
            return (T)value!;
        }

        internal override string Format(object? value)
        {
            return ((T)value!).Caption;
        }

        public void SetRange(T value)
        {
            SetRange<T>(value);
        }
    }
}
