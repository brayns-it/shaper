using Brayns.Shaper.Fields;
using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public class TableRenameEventArgs 
    {
        public object[] NewPrimaryKey { get; init; }

        public TableRenameEventArgs(object[] newPK)
        {
            NewPrimaryKey = newPK;
        }
    }

    public delegate void TableTriggerHandler<T>(T rec);
    public delegate void TableRenameHandler<T>(T rec, TableRenameEventArgs e);

    public abstract class BaseTable : Unit
    {
        internal List<ITableRelation> TableRelations { get; init; } = new();

        public FilterLevel TableFilterLevel { get; set; }
        public object TableVersion { get; internal set; } = DBNull.Value;
        public bool TableAscending { get; set; } = true;
        public bool TableLock { get; set; } = false;
        public FieldList TableSort { get; init; } = new();
        public FieldList TablePrimaryKey { get; init; } = new();
        public string TableSqlName { get; internal set; } = "";

        internal Error ErrorPrimaryKeyModify(BaseField f)
        {
            return new Classes.Error(Label("Cannot modify primary key '{0}', use rename instead"), f.Caption);
        }

        internal Error ErrorNoPrimaryKey()
        {
            return new Classes.Error(Label("Table '{0}' has no primary key"), UnitCaption);
        }

        internal Error ErrorConcurrency()
        {
            return new Classes.Error(Label("Another user has modified table '{0}' try again"), UnitCaption);
        }

        internal FieldList GetCurrentKey()
        {
            var res = new FieldList();
            foreach (var f in TableSort)
                res.Add(f);
            foreach (var f in TablePrimaryKey)
                if (!res.Contains(f))
                    res.Add(f);
            return res;
        }

        public abstract void ModifyAll(BaseField field, object? newValue, bool runTrigger = false);
    }

    public abstract class Table<T> : BaseTable 
    {
        public static event TableTriggerHandler<T>? Inserting;
        public static event TableTriggerHandler<T>? Modifying;
        public static event TableTriggerHandler<T>? Deleting;
        public static event TableRenameHandler<T>? Renaming;

        private List<Dictionary<string, object>> _dataset = new();
        private int _currentRow = -1;
        private List<FieldFilter> _lastFilters = new List<FieldFilter>();

        internal Database.Database? _database;
        internal Database.Database? TableDatabase
        {
            get
            {
                if (_database != null)
                    return _database;
                else
                    return Session.Database;
            }
            set { _database = value; }
        }

        public Table()
        {
            UnitType = UnitTypes.TABLE;
            if (typeof(T) != GetType())
                throw new Error(Label("Table type must be '{0}'"), GetType());
        }

        public Error ErrorNotFound()
        {
            string flt = string.Join(", ", _lastFilters);
            if (flt.Length == 0)
                return new Error(Error.E_RECORD_NOT_FOUND, Label("'{0}' not found"), UnitCaption);
            else
                return new Error(Error.E_RECORD_NOT_FOUND, Label("'{0}' not found: {1}"), UnitCaption, flt);
        }

        public override string UnitName
        {
            get => base.UnitName;
            set
            {
                base.UnitName = value;
                TableSqlName = Functions.ToSqlName(value);
            }
        }
        
        public bool IsEmpty()
        {
            return TableDatabase!.IsEmpty(this);
        }

        protected virtual void OnInsert()
        {
        }

        public void Insert(bool runTrigger = false)
        {
            if (runTrigger)
            {
                OnInsert();
                Inserting?.Invoke((T)Convert.ChangeType(this, typeof(T)));
            }

            TableDatabase!.Insert(this);
            AcceptChanges();
        }

        internal void AcceptChanges()
        {
            foreach (var f in UnitFields)
                f.XValue = f.Value;
        }

        public void DeleteAll(bool runTrigger = false)
        {
            if (runTrigger)
            {
                if (FindSet())
                    while (Read())
                        Delete(true);
            }
            else
            {
                TableDatabase!.DeleteAll(this);
            }
        }

        protected virtual void OnDelete()
        {
        }

        public void Delete(bool runTrigger = false)
        {
            if (runTrigger)
            {
                OnDelete();
                Deleting?.Invoke((T)Convert.ChangeType(this, typeof(T)));
            }

            TableDatabase!.Delete(this);
        }

        public override void ModifyAll(BaseField field, object? newValue, bool runTrigger = false)
        {
            if (runTrigger)
            {
                if (FindSet())
                    while (Read())
                    {
                        field.Value = newValue;
                        Modify(true);
                    }
            }
            else
            {
                field.Value = newValue;
                TableDatabase!.ModifyAll(this, field);
                AcceptChanges();
            }
        }

        protected virtual void OnModify()
        {
        }

        public void Modify(bool runTrigger = false)
        {
            foreach (BaseField f in UnitFields)
                if ((!Functions.AreEquals(f.Value, f.XValue)) && (TablePrimaryKey.Contains(f)))
                    throw ErrorPrimaryKeyModify(f);

            if (runTrigger)
            {
                OnModify();
                Modifying?.Invoke((T)Convert.ChangeType(this, typeof(T)));
            }
            
            TableDatabase!.Modify(this);
            AcceptChanges();
        }

        protected virtual void OnRename(params object[] newKey)
        {
        }

        public void Rename(params object[] newKey)
        {
            OnRename(newKey);

            var e = new TableRenameEventArgs(newKey);
            Renaming?.Invoke((T)Convert.ChangeType(this, typeof(T)), e);

            TableDatabase!.Rename(this, newKey);

            int i = 0;
            foreach (BaseField f in TablePrimaryKey)
            {
                if (f.Value != newKey[i])
                {
                    foreach (var tr in Loader.Loader.GetAllTableRelations(f))
                        tr.ModifyAll(f.Value, newKey[i]);

                    f.Value = newKey[i];
                }
                i++;
            }

            AcceptChanges();
        }

        public bool FindFirst()
        {
            _dataset = TableDatabase!.FindFirst(this);
            _currentRow = -1;
            if (_dataset!.Count > 0)
                return Read();
            else
                return false;
        }

        public bool FindLast()
        {
            _dataset = TableDatabase!.FindLast(this);
            _currentRow = -1;
            if (_dataset!.Count > 0)
                return Read();
            else
                return false;
        }

        public void Reset()
        {
            TableSort.Clear();
            foreach (BaseField f in UnitFields)
                f.Filters.Clear();
        }

        public void Reset(FilterLevel level)
        {
            foreach (BaseField f in UnitFields)
            {
                List<FieldFilter> toDel = new();
                foreach (FieldFilter ff in f.Filters)
                    if (ff.Level == level)
                        toDel.Add(ff);
                foreach (FieldFilter ff in toDel)
                    f.Filters.Remove(ff);
            }
        }

        public int Count()
        {
            return TableDatabase!.Count(this);
        }

        public bool FindSet()
        {
            _dataset = TableDatabase!.FindSet(this);
            _currentRow = -1;
            return (_dataset.Count > 0);
        }

        public bool Get(params object[] pkValues)
        {
            List<object> val = new();
            _lastFilters.Clear();

            int i = 0;
            foreach (BaseField f in TablePrimaryKey)
            {
                if (pkValues.Length > i)
                    val.Add(pkValues[i]);
                else
                    val.Add(f.InitValue!);

                _lastFilters.Add(new FieldFilter(f, val[i]));
                i++;
            }

            _dataset = TableDatabase!.Get(this, val.ToArray());
            _currentRow = -1;
            if (_dataset!.Count > 0)
                return Read();
            else
                return false;
        }

        public bool Read()
        {
            _currentRow++;
            if (_currentRow >= _dataset!.Count)
            {
                _currentRow = 0;
                _dataset = TableDatabase!.NextSet(this);
                if (_dataset.Count == 0)
                    return false;
            }

            TableDatabase!.LoadRow(this, _dataset![_currentRow]);
            AcceptChanges();
            return true;
        }

        public void AddRelation<S>(BaseField fieldFrom,
                   TableRelationFieldHandler<S>? fieldTo = null,
                   TableRelationFilterHandler<S>? filterTo = null) where S : BaseTable
        {
            var t = new TableRelation<S>(fieldFrom);
            t.FieldHandler = fieldTo;
            t.FilterHandler = filterTo;
            TableRelations.Add(t);
        }

        public void AddRelation<S, U>(BaseField fieldFrom,
                   TableRelationFieldHandler<S>? fieldTo = null,
                   TableRelationConditionHandler<U>? conditionFrom = null,
                   TableRelationFilterHandler<S>? filterTo = null) where S : BaseTable where U : BaseTable
        {
            var t = new TableRelation<S, U>(fieldFrom);
            t.FieldHandler = fieldTo;
            t.ConditionHandler = conditionFrom;
            t.FilterHandler = filterTo;
            TableRelations.Add(t);
        }

        public void Init()
        {
            TableVersion = DBNull.Value;
            foreach (BaseField f in UnitFields)
                f.Init();
        }
    }
}
