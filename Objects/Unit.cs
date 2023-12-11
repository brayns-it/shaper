using Brayns.Shaper.Fields;
using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public delegate void GenericHandler();
    public delegate void GenericHandler<T>(T sender) where T : Unit;

    public class UnitTypes
    {
        public const int NONE = 0;

        [Label("Table")]
        public const int TABLE = 1;

        [Label("Codeunit")]
        public const int CODEUNIT = 2;

        [Label("Page")]
        public const int PAGE = 4;
    }

    public abstract class Unit
    {
        public System.Guid UnitID { get; protected set; }
        public Opt<UnitTypes> UnitType { get; protected set; }
        public FieldList UnitFields { get; init; }
        public string UnitCaption { get; protected set; } = "";

        private string _unitName = "";
        public virtual string UnitName
        {
            get { return _unitName; }
            set
            {
                _unitName = value;
                UnitCaption = _unitName;
            }
        }

        public Unit()
        {
            UnitType = UnitTypes.NONE;
            UnitFields = new();
            UnitID = System.Guid.NewGuid();

            foreach (PropertyInfo pi in GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (typeof(Fields.BaseField).IsAssignableFrom(pi.PropertyType))
                {
                    var f = (Fields.BaseField)pi.GetValue(this)!;
                    f.CodeName = pi.Name;
                    if (typeof(BaseTable).IsAssignableFrom(GetType()))
                        f.Table = (BaseTable)this;
                    if (typeof(BasePage).IsAssignableFrom(GetType()) && (f.Type == FieldTypes.TEXT))
                        ((Fields.Text)f).Length = Fields.Text.MAX_LENGTH;
                    UnitFields.Add(f);
                }
            }
        }

        protected void Extend()
        {
            foreach (MethodInfo mi in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (mi.GetCustomAttributes(typeof(Classes.ExtendedAttribute), true).Length > 0)
                    mi.Invoke(this, null);
            }
        }

        internal void SessionRegister()
        {
            CurrentSession.Units[UnitID.ToString()] = this;
        }

        internal void SessionUnregister()
        {
            if (CurrentSession.Units.ContainsKey(UnitID.ToString()))
                CurrentSession.Units.Remove(UnitID.ToString());
        }
    }
}
