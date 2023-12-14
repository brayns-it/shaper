using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class FieldTypes 
    {
        public const int NONE = 0;

        [Label("Code")]
        public const int CODE = 1;

        [Label("Integer")]
        public const int INTEGER = 2;

        [Label("Option")]
        public const int OPTION = 3;

        [Label("Text")]
        public const int TEXT = 4;

        [Label("Big Integer")]
        public const int BIGINTEGER = 5;

        [Label("Decimal")]
        public const int DECIMAL = 6;

        [Label("Date")]
        public const int DATE = 7;

        [Label("DateTime")]
        public const int DATETIME = 8;

        [Label("Boolean")]
        public const int BOOLEAN = 9;

        [Label("Time")]
        public const int TIME = 10;

        [Label("Blob")]
        public const int BLOB = 11;

        [Label("Guid")]
        public const int GUID = 12;
    }

    public class FieldList : List<Fields.BaseField>
    {
        public void Add(params BaseField[] args)
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
        public BaseField Field { get; init; }
        public FilterLevel Level { get; set; }
        public FilterType Type { get; set; }
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public object? Value { get; set; }
        public List<object> Values { get; init; } = new();
        public string? Expression { get; set; }

        public FieldFilter(BaseField field)
        {
            Field = field;
        }

        public FieldFilter(BaseField field, object? equalTo)
        {
            Field = field;
            Type = FilterType.Equal;
            Value = equalTo;
        }

        public FieldFilter Clone(BaseField newField)
        {
            FieldFilter ff = new(newField);
            ff.Level = Level;
            ff.Type = Type;
            ff.MinValue = MinValue;
            ff.MaxValue = MaxValue;
            ff.Value = Value;
            ff.Values.AddRange(Values);
            ff.Expression = Expression;
            return ff;
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

        public string Tokenize(List<object> allValues)
        {
            string expr = Expression!;

            // resolve between
            Regex re = new Regex("([^.]+)(\\.\\.)([^.]+)");
            expr = re.Replace(expr, m => m.Groups[0].Value + " " + m.Groups[1].Value + " " + m.Groups[2].Value);

            // resolve values
            allValues.AddRange(Values);

            re = new Regex("[^|^&^(^)^\\s^>^<^=]+");
            expr = re.Replace(expr, m =>
            {
                if (m.Value.StartsWith("{") && m.Value.EndsWith("}"))
                    return m.Value;
                else
                {
                    int n = allValues.Count;
                    allValues.Add(m.Value);
                    return "{" + n.ToString() + "}";
                }
            });

            // assert operators for between
            re = new Regex("({\\d})\\s*\\.\\.\\s*({\\d})");
            expr = re.Replace(expr, m => ">= " + m.Groups[1] + " AND <= " + m.Groups[2]);

            // assert operators
            expr = " " + expr + " ";
            re = new Regex("([^>^<^=])({\\d})");
            expr = re.Replace(expr, m => m.Groups[1] + "=" + m.Groups[2]);

            // resolve logical
            expr = expr.Replace("&", " AND ");
            expr = expr.Replace("|", " OR ");

            return expr;
        }
    }

    public abstract class BaseField
    {
        public Opt<FieldTypes> Type { get; init; }
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

        public BaseField()
        {
            Type = FieldTypes.NONE;
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
        internal abstract object? Evaluate(string text);
        internal abstract JValue Serialize(object? value);

        public void Init()
        {
            Value = InitValue;
            XValue = InitValue;
        }

        internal void Validate(object? value)
        {
            Value = value;
            Validating?.Invoke();
        }

        public void SetFilter<T>(string expression, params T[] pars)
        {
            SetRange();

            var ff = new FieldFilter(this);
            ff.Type = FilterType.Expression;
            ff.Level = Table!.TableFilterLevel;
            ff.Expression = expression;
            foreach (T v in pars)
                ff.Values.Add(v!);
            Filters.Add(ff);
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

        public bool IsEmpty()
        {
            return Functions.AreEquals(Value, TestValue);
        }

        public void Test()
        {
            if (Functions.AreEquals(Value, TestValue))
                throw new Error(Label("Field {0} is empty"), Caption);
        }

        public void Test<T>(T valueToTest)
        {
            if (!Functions.AreEquals(Value, valueToTest))
                throw new Error(Label("Field {0} must be equal to {1}"), Caption, valueToTest!);
        }

        public object? GetFilterValue()
        {
            foreach (FieldFilter ff in Filters)
            {
                if (ff.Type == FilterType.Equal)
                    return ff.Value;
            }
            return null;
        }
    }
}
