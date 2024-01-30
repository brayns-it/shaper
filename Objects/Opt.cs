using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class OptList
    {
        public Dictionary<int, string>? _names;
        public Dictionary<int, string> Names
        {
            get
            {
                if (_names == null) GetNames();
                return _names!;
            }
        }

        public Dictionary<int, string>? _captions;
        public Dictionary<int, string> Captions
        {
            get
            {
                if (_names == null) GetNames();
                return _captions!;
            }
        }

        private void GetNames()
        {
            var t = GetType();
            _names = new();
            _captions = new();

            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                if (f.FieldType == typeof(int) && f.IsLiteral)
                {
                    int value = (int)f.GetValue(null)!;
                    _names.Add(value, f.Name);

                    Label? l = f.GetCustomAttribute<Label>();
                    string label = (l != null) ? l.Label : "";
                    _captions.Add(value, Classes.Language.TranslateText(label, t));
                }
        }
    }

    public abstract class Opt 
    {
        public int Value { get; set; } = 0;
        public Type? Type { get; protected set; }
        protected OptList? Instance { get; set; }
        
        public Dictionary<int, string> Names
        {
            get { return Instance!.Names; }
        }

        public Dictionary<int, string> Captions
        {
            get { return Instance!.Captions; }
        }

        public string Caption
        {
            get
            {
                if (Instance!.Captions.ContainsKey(Value))
                    return Instance!.Captions[Value];
                else
                    return "";
            }
        }

        public string Name
        {
            get
            {
                if (Instance!.Names.ContainsKey(Value))
                    return Instance!.Names[Value];
                else
                    return "";
            }
        }


        public override string ToString()
        {
            if (Instance!.Names.ContainsKey(Value))
                return Instance!.Names[Value];
            else
                return Value.ToString();
        }
    }

    public class Opt<T> : Opt where T : OptList
    {
        public Opt()
        {
            Type = typeof(T);
            Instance = Activator.CreateInstance<T>()!;
        }

        public Opt(int value)
        {
            Type = typeof(T);
            Value = value;
            Instance = Activator.CreateInstance<T>()!;
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
