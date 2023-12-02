using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class Series
    {
        public int Value { get; set; } = 0;
        public Type? Type { get; protected set; }
        public Dictionary<int, string> Names { get; private set; } = new();
        public Dictionary<int, string> Captions { get; private set; } = new();

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
            Captions.Clear();

            if (Type != null)
                foreach (FieldInfo f in Type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    if (f.FieldType == typeof(int) && f.IsLiteral)
                    {
                        Names.Add((int)f.GetValue(null)!, f.Name);

                        Label? l = f.GetCustomAttribute<Label>();
                        if (l != null)
                            Captions.Add((int)f.GetValue(null)!, l.Label);
                        else
                            Captions.Add((int)f.GetValue(null)!, "");
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

    public class Series<T> : Series
    {
        public Series(int value)
        {
            Type = typeof(T);
            Value = value;
            GetNames();
        }
        
        public static implicit operator int(Series<T> opt)
        {
            return opt.Value;
        }

        public static implicit operator Series<T>(int val)
        {
            return new(val);
        }

        public static bool operator ==(Series<T>? a, Series<T>? b)
        {
            if ((a is null) && (b is null)) return true;
            if ((a is null) || (b is null)) return false;
            return (a!.Value == b!.Value);
        }

        public static bool operator !=(Series<T>? a, Series<T>? b)
        {
            if ((a is null) && (b is null)) return false;
            if ((a is null) || (b is null)) return true;
            return (a!.Value != b!.Value);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(Series<T>)) return false;
            return (Value == ((Series<T>)(obj)).Value);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
