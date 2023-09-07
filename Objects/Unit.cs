using Brayns.Shaper.Fields;
using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public class UnitType : Option<UnitType>
    {
        public static readonly UnitType NONE = New(0);
        public static readonly UnitType TABLE = New(1, "Table");
        public static readonly UnitType CODEUNIT = New(2, "Codeunit");
    }

    public abstract class Unit
    {
        public UnitType UnitType { get; protected set; }
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
            UnitType = UnitType.NONE;
            UnitFields = new();

            foreach (PropertyInfo pi in GetType().GetProperties())
            {
                if (typeof(Fields.Field).IsAssignableFrom(pi.PropertyType))
                {
                    var f = (Fields.Field)pi.GetValue(this)!;
                    f.CodeName = pi.Name;
                    if (typeof(BaseTable).IsAssignableFrom(GetType()))
                        f.Table = (BaseTable)this;
                    UnitFields.Add(f);
                }
            }
        }
    }
}
