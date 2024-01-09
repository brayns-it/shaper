using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class Opt
    {
        public int Value { get; set; } = 0;
        public Type? Type { get; protected set; }
        public Dictionary<int, string> Names { get; private set; } = new();

        private bool _translated = false;
        private Dictionary<int, string> _captions = new();
        public Dictionary<int, string> Captions 
        { 
            get
            {
                if (!_translated)
                {
                    _translated = true;
                    foreach (int i in _captions.Keys)
                        _captions[i] = Classes.Language.TranslateText(_captions[i], Type!);
                }
                return _captions;
            }
        } 

        public string Caption
        {
            get
            {
                if (Captions.ContainsKey(Value))
                    return Captions[Value];
                else
                    return "";
            }
        }

        protected void GetNames()
        {
            Names.Clear();
            _captions.Clear();

            if (Type != null)
                foreach (FieldInfo f in Type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    if (f.FieldType == typeof(int) && f.IsLiteral)
                    {
                        Names.Add((int)f.GetValue(null)!, f.Name);

                        Label? l = f.GetCustomAttribute<Label>();
                        if (l != null)
                            _captions.Add((int)f.GetValue(null)!, l.Label);
                        else
                            _captions.Add((int)f.GetValue(null)!, "");
                    }
        }

        public override string ToString()
        {
            if (Names.ContainsKey(Value))
                return Names[Value];
            else
                return Value.ToString();
        }
    }

    public class Opt<T> : Opt
    {
        public Opt(int value)
        {
            Type = typeof(T);
            Value = value;
            GetNames();
        }
        
        public static implicit operator int(Opt<T> opt)
        {
            return opt.Value;
        }

        public static implicit operator Opt<T>(int val)
        {
            return new(val);
        }

        public static bool operator ==(Opt<T>? a, Opt<T>? b)
        {
            if ((a is null) && (b is null)) return true;
            if ((a is null) || (b is null)) return false;
            return (a!.Value == b!.Value);
        }

        public static bool operator !=(Opt<T>? a, Opt<T>? b)
        {
            if ((a is null) && (b is null)) return false;
            if ((a is null) || (b is null)) return true;
            return (a!.Value != b!.Value);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(Opt<T>))
            {
                if (obj.GetType() == typeof(int))
                    return Value == (int)obj;
                else
                    return false;
            }
            return Value == ((Opt<T>)(obj)).Value;
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
