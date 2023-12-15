using Brayns.Shaper.Fields;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Objects
{
    public delegate BaseField TableRelationFieldHandler<T>(T table) where T : BaseTable;
    public delegate void TableRelationConditionHandler<U>(U table) where U : BaseTable;
    public delegate void TableRelationFilterHandler<T>(T table) where T : BaseTable;

    internal interface ITableRelation
    {
        public string FieldFrom { get; }
        public Type? TableFrom { get; }
        public (Type, string) GetFieldForCollect();
        public void ModifyAll(object? oldValue, object? newValue);
    }

    internal class TableRelation<T> : ITableRelation where T : BaseTable
    {
        public Type TableTo { get; private set; }
        public Type? TableFrom { get; private set; }
        public string FieldFrom { get; init; }
        public BaseField FieldFromInstance { get; init; }

        public TableRelationFilterHandler<T>? FilterHandler { get; set; }
        public TableRelationFieldHandler<T>? FieldHandler { get; set; }

        public (Type, string) GetFieldForCollect()
        {
            var t = (T)Activator.CreateInstance(TableTo)!;
            BaseField f = t.TablePrimaryKey[0];

            if (FieldHandler != null)
            {
                f = FieldHandler.Invoke(t);
                if (!t.TablePrimaryKey.Contains(f))
                    throw new Error(Label("Cannot relate to field '{0}' because it's not primary key member", f.Caption));
            }

            if (f.Type != FieldFromInstance.Type)
                throw new Error(Label("Field '{0}' must be of the same type of '{1}'", f.Caption, FieldFromInstance.Caption));

            var ft1 = f as Fields.Text;
            var ft2 = FieldFromInstance as Fields.Text;
            if ((ft1 != null) && (ft2 != null) && (ft1.Length != ft2.Length))
                throw new Error(Label("Field '{0}' must be of the same size of '{1}'", f.Caption, FieldFromInstance.Caption));

            return (typeof(T), f.SqlName.ToLower());
        }

        public TableRelation(BaseField fieldFrom)
        {
            FieldFromInstance = fieldFrom;
            FieldFrom = fieldFrom.SqlName.ToLower();
            TableTo = typeof(T);

            if (fieldFrom.Table != null)
                TableFrom = fieldFrom.Table.GetType();
        }

        protected void ModifyAll(BaseTable t, object? oldValue, object? newValue)
        {
            BaseField? field = null;
            foreach (var f in t.UnitFields)
            {
                if (f.SqlName.ToLower() == FieldFrom)
                {
                    field = f;
                    break;
                }
            }

            if (field != null)
            {
                t.TableFilterLevel = FilterLevel.Private;
                field.SetRange(oldValue);
                t.ModifyAll(field, newValue, false);
            }
        }

        public void ModifyAll(object? oldValue, object? newValue)
        {
            if (TableFrom == null)
                return;

            var t = (BaseTable)Activator.CreateInstance(TableFrom)!;
            OnApplyCondition(t);
            ModifyAll(t, oldValue, newValue);
        }

        protected virtual void OnApplyCondition(BaseTable t)
        {
        }
    }

    internal class TableRelation<T, U> : TableRelation<T> where T : BaseTable where U : BaseTable
    {
        public TableRelationConditionHandler<U>? ConditionHandler { get; set; }

        public TableRelation(BaseField fieldFrom) : base(fieldFrom)
        {
            if (TableFrom != null)
                if (TableFrom != typeof(U))
                    throw new Error(Label("Field '{0}' does not belong to '{1}'", fieldFrom.Caption, typeof(U)));
        }

        protected override void OnApplyCondition(BaseTable t)
        {
            if (ConditionHandler != null)
                ConditionHandler.Invoke((U)t);
        }
    }
}
