using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class Option
    {
        public int Value { get; internal set; } = 0;
        public string Caption { get; internal set; } = "";
        public Type? Type { get; internal set; }

        public static T GetDefault<T>()
        {
            Option<T>? def = null;

            foreach (FieldInfo f in typeof(T).GetFields())
            {
                if (f.FieldType == typeof(T))
                {
                    var opt = (Option<T>)f.GetValue(null)!;
                    if ((def == null) || (def.Value > opt.Value))
                        def = opt;
                }
            }

            if (def == null)
                throw new Error(Label("Option '{0}' has no members"), typeof(T));

            return (T)Convert.ChangeType(def, typeof(T));
        }

        public static object GetByValue(Type t, int value)
        {
            foreach (FieldInfo f in t.GetFields())
            {
                if (f.FieldType == t)
                {
                    var opt = (Option)f.GetValue(null)!;
                    if (opt.Value == value)
                        return Convert.ChangeType(opt, t);
                }
            }

            throw new Error(Label("Option '{0}' has no value {1}"), t, value);
        }

        public static T GetByValue<T>(int value)
        {
            return (T)GetByValue(typeof(T), value);
        }

        public override string ToString()
        {
            return Caption;
        }
    }

    public abstract class Option<T> : Option
    {
        public static T New(int value, string caption="")
        {
            var o = Activator.CreateInstance(typeof(T))!;
            var opt = (Option<T>)o;
            opt.Value = value;
            opt.Caption = caption;
            opt.Type = typeof(T);
            return (T)o;
        }

        public Option()
        {
            if (typeof(T) != GetType())
                throw new Error(Label("Option type must be '{0}'"), GetType());
        }
    }
}
