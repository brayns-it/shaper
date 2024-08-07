﻿using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class FieldTypes : OptList
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

        [Label("Timestamp")]
        public const int TIMESTAMP = 13;
    }

    public class IndexList : Dictionary<string, FieldList>
    {
        public void Add(string key, params BaseField[] args)
        {
            this[key] = new();
            this[key].Add(args);
        }
    }

    public class FieldList : List<Fields.BaseField>
    {
        public void Add(params BaseField[] args)
        {
            AddRange(args);
        }

        public void Set(params BaseField[] args)
        {
            Clear();
            AddRange(args);
        }

        public BaseField? ByCodeName(string codeName)
        {
            foreach (var f in this)
                if (f.CodeName.Equals(codeName))
                    return f;
            return null;
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

            var reSep = new Regex("[^|()&]+");
            var reBet = new Regex("(.*)\\.\\.(.*)");
            var reOpe = new Regex("^[<>=]+");
            var reVal = new Regex("^([<>=]+)(.*)");
            var reTag = new Regex("^{\\d}$");

            // transform ".." in between
            expr = reSep.Replace(expr, m => reBet.Replace(m.Value, m2 => "(>=" + m2.Groups[1] + "&<=" + m2.Groups[2] + ")"));

            // assert starting operators >= > = < <= <> if not is equal
            expr = reSep.Replace(expr, m =>
            {
                var part = m.Value.Trim();
                if (!reOpe.IsMatch(part)) part = "=" + part;
                return part;
            });

            // resolve values
            allValues.AddRange(Values);

            expr = reSep.Replace(expr, m =>
            {
                return reVal.Replace(m.Value, m2 =>
                {
                    var ope = m2.Groups[1].ToString();
                    var val = m2.Groups[2].ToString().Trim();

                    if (!reTag.IsMatch(val))
                    {
                        int n = allValues.Count;

                        // resolve LIKE
                        if (((Field.Type == FieldTypes.CODE) || (Field.Type == FieldTypes.TEXT)) && (val.Contains("*")) && (ope == "="))
                        {
                            ope = "LIKE";
                            val = val.Replace("*", "%");
                        }

                        // resolve empty
                        if (((Field.Type == FieldTypes.CODE) || (Field.Type == FieldTypes.TEXT)) && (val == "''") && (ope == "="))
                            val = "";

                        object? result;
                        Field.Evaluate(val, out result);
                        allValues.Add(result!);
                        val = "{" + n.ToString() + "}";
                    }

                    return "{f} " + ope + " " + val;
                });
            });

            // resolve logical
            expr = expr.Replace("&", " AND ");
            expr = expr.Replace("|", " OR ");

            return expr;
        }
    }

    public abstract class BaseField
    {
        internal bool ValuePerSession { get; set; }
        internal Unit? Parent { get; set; }
        internal TableRelationList TableRelations { get; init; } = new();

        public Opt<FieldTypes> Type { get; init; }
        public string Name { get; init; }
        public string Caption { get; init; }
        public string CodeName { get; internal set; }
        public string SqlName { get; set; }
        public bool HasFormat { get; init; }
        public event ValidatingHandler? Validating;
        public List<FieldFilter> Filters { get; init; }
        public BaseTable? Table { get; internal set; }
        public bool ValidateRelation { get; set; } = true;

        private object? _value;
        public object? Value
        {
            get
            {
                if (ValuePerSession)
                {
                    string k = "FieldValue:" + Parent!.GetType().FullName + "_" + CodeName;
                    if (Session.State.ContainsKey(k))
                        _value = Session.State[k];
                    else
                        _value = InitValue;
                }

                return _value;
            }
            set
            {
                _value = CheckValue(value);

                if (ValuePerSession)
                {
                    string k = "FieldValue:" + Parent!.GetType().FullName + "_" + CodeName;
                    if (_value != null)
                        Session.State[k] = _value;
                    else if (Session.State.ContainsKey(k))
                        Session.State.Remove(k);
                }
            }
        }

        private object? _xValue;
        public object? XValue
        {
            get { return _xValue; }
            set { _xValue = CheckValue(value); }
        }

        private object? _initValue;
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
            ValuePerSession = false;
        }

        protected void Create()
        {
            SqlName = Functions.ToSqlName(Name);
        }

        internal abstract object? CheckValue(object? value);

        public abstract string Format();
        public abstract void Evaluate(string text);
        internal abstract void Evaluate(string text, out object? result);
        public abstract JValue Serialize();
        public abstract void Deserialize(JValue? value);

        public void Init()
        {
            Value = InitValue;
            XValue = InitValue;
        }

        internal void Validate(object? value)
        {
            Value = value;
            Validating?.Invoke();

            if (ValidateRelation && (!IsEmpty()))
                TableRelations.Get()?.ThrowIfNotValid();
        }

        internal void SetFilter(string expression)
        {
            SetRange();

            var ff = new FieldFilter(this);
            ff.Type = FilterType.Expression;
            ff.Level = Table!.TableFilterLevel;
            ff.Expression = expression;
            Filters.Add(ff);
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
                throw new Error(Label("Field '{0}' does not belongs to table", Caption));

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
                throw new Error(Label("Field {0} is empty", Caption));
        }

        public void Test<T>(T valueToTest)
        {
            if (!Functions.AreEquals(Value, valueToTest))
                throw new Error(Label("Field {0} must be equal to {1}", Caption, valueToTest!));
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

        public void ModifyAll(object? newValue, bool runTrigger = false)
        {
            Table!.ModifyAll(this, newValue, runTrigger);
        }
    }
}
