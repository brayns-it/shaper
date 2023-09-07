using Brayns.Shaper.Objects;

namespace Brayns.Shaper.Fields
{
    public class FieldType : Brayns.Shaper.Objects.Option<FieldType>
    {
        public static readonly FieldType NONE = New(0);
        public static readonly FieldType CODE = New(1, "Code");
        public static readonly FieldType INTEGER = New(2, "Integer");
        public static readonly FieldType OPTION = New(3, "Option");
        public static readonly FieldType TEXT = New(4, "Text");
        public static readonly FieldType BIGINTEGER = New(5, "Big Integer");
        public static readonly FieldType DECIMAL = New(6, "Decimal");
        public static readonly FieldType DATE = New(7, "Date");
        public static readonly FieldType DATETIME = New(8, "DateTime");
        public static readonly FieldType BOOLEAN = New(9, "Boolean");
        public static readonly FieldType TIME = New(10, "Time");
        public static readonly FieldType BLOB = New(11, "Blob");
        public static readonly FieldType GUID = New(12, "Guid");
    }

    public class FieldList : List<Fields.Field>
    {
        public void Add(params Field[] args)
        {
            AddRange(args);
        }
    }

    public delegate void ValidatingHandler();

    public enum FilterLevel
    {
        Public = 1,
        Private = 2,
        Relations = 4,
        DropDown = 8,
        Quick = 16,
        Or = 32,
        Custom1 = 1024,
        Custom2 = 2048,
        Custom3 = 4096
    }

    public enum FilterType
    {
        Equal = 1,
        Range = 2,
        Expression = 4
    }

    public class FieldFilter
    {
        public Field Field { get; init; }
        public FilterLevel Level { get; set; }
        public FilterType Type { get; set; }
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public object? Value { get; set; }
        public List<object> Values { get; init; } = new();
        public string? Expression { get; set; }

        public FieldFilter(Field field)
        {
            Field = field;
        }

        public FieldFilter(Field field, object? equalTo)
        {
            Field = field;
            Type = FilterType.Equal;
            Value = equalTo;
        }

        public bool TestFilter()
        {
            if (Type == FilterType.Equal)
            {
                return (Field.Value == Value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override string ToString()
        {
            if (Type == FilterType.Equal)
            {
                return "'" + Field.Caption + "' = '" + Value!.ToString() + "'";
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    public abstract class Field
    {
        public FieldType Type { get; init; }
        public string Name { get; init; }
        public string Caption { get; init; }
        public string CodeName { get; internal set; }
        public string SqlName { get; private set; }
        public bool HasFormat { get; init; }
        public event ValidatingHandler? Validating;
        public List<FieldFilter> Filters { get; init; }
        public BaseTable? Table { get; internal set; }

        protected object? _value;
        public object? Value
        { 
            get { return _value; }
            set { _value = CheckValue(value); }
        }

        protected object? _xValue;
        public object? XValue
        {
            get { return _xValue; }
            set { _xValue = CheckValue(value); }
        }

        protected object? _initValue;
        public object? InitValue
        {
            get { return _initValue; }
            set { _initValue = CheckValue(value); }
        }

        protected object? TestValue { get; set; }

        public Field()
        {
            Type = FieldType.NONE;
            Name = "";
            Caption = "";
            CodeName = "";
            SqlName = "";
            HasFormat = false;
            Filters = new List<FieldFilter>();
        }

        protected void Create()
        {
            SqlName = Functions.ToSqlName(Name);
        }

        internal abstract object? CheckValue(object? value);
        internal abstract string Format(object? value);

        public void Init()
        {
            Value = InitValue;
            XValue = InitValue;
        }

        public void Validate<T>(T value)
        {
            Value = value;
            Validating?.Invoke();
        }

        public void SetRange<T>(T minValue, T maxValue)
        {
            SetRange();

            var ff = new FieldFilter(this);
            ff.Type = FilterType.Range;
            ff.Level = Table!.TableFilterLevel;
            ff.MinValue = minValue;
            ff.MaxValue = maxValue;
            Filters.Add(ff);
        }

        public void SetRange<T>(T value)
        {
            SetRange();

            var ff = new FieldFilter(this);
            ff.Type = FilterType.Equal;
            ff.Level = Table!.TableFilterLevel;
            ff.Value = value;
            Filters.Add(ff);
        }

        public void SetRange()
        {
            if (Table == null)
                throw new Error(Label("Field '{0}' does not belongs to table"), Caption);

            List<FieldFilter> toDelete = new List<FieldFilter>();
            foreach (FieldFilter ff in Filters)
            {
                if (ff.Level == Table.TableFilterLevel)
                    toDelete.Add(ff);
            }
            foreach (FieldFilter ff in toDelete)
                Filters.Remove(ff);
        }
    }
}
