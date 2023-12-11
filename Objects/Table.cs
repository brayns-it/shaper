using Brayns.Shaper.Fields;

namespace Brayns.Shaper.Objects
{
    public abstract class BaseTable : Unit
    {
        private List<Dictionary<string, object>> _dataset = new();
        private int _currentRow = -1;
        private List<FieldFilter> _lastFilters = new List<FieldFilter>();

        private Database.Database? _database;
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

        internal List<ITableRelation> TableRelations { get; init; } = new();

        public FilterLevel TableFilterLevel { get; set; }
        public object TableVersion { get; internal set; } = DBNull.Value;
        public bool TableAscending { get; set; } = true;
        public bool TableLock { get; set; } = false;
        public FieldList TableSort { get; init; } = new();
        public FieldList TablePrimaryKey { get; init; } = new();
        public string TableSqlName { get; internal set; } = "";
        public event GenericHandler? Inserting;
        public event GenericHandler? Deleting;
        public event GenericHandler? Modifying;
        public event GenericHandler? Renaming;
        
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

        internal void SetDataset(Dictionary<string, object> dataset)
        {
            TableDatabase!.LoadRow(this, dataset);
            AcceptChanges();
        }

        internal Dictionary<string, object> GetDataset()
        {
            return _dataset![_currentRow];
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

        public void ModifyAll(BaseField field, object? newValue, bool runTrigger = false)
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

        public bool FindSet()
        {
            _dataset = TableDatabase!.FindSet(this);
            _currentRow = -1;
            return (_dataset.Count > 0);
        }

        internal void AcceptChanges()
        {
            foreach (var f in UnitFields)
                f.XValue = f.Value;
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

        public void Insert(bool runTrigger = false)
        {
            if (runTrigger)
            {
                Inserting?.Invoke();
            }

            TableDatabase!.Insert(this);
            AcceptChanges();
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

        public void Delete(bool runTrigger = false)
        {
            if (runTrigger)
            {
                Deleting?.Invoke();
            }

            TableDatabase!.Delete(this);
        }

        public void Modify(bool runTrigger = false)
        {
            foreach (BaseField f in UnitFields)
                if ((!Functions.AreEquals(f.Value, f.XValue)) && (TablePrimaryKey.Contains(f)))
                    throw ErrorPrimaryKeyModify(f);

            if (runTrigger)
            {
                Modifying?.Invoke();
            }

            TableDatabase!.Modify(this);
            AcceptChanges();
        }

        public void Rename()
        {
            Renaming?.Invoke();
            TableDatabase!.Rename(this);

            foreach (BaseField f in TablePrimaryKey)
                if (!Functions.AreEquals(f.Value, f.XValue))
                    foreach (var tr in Loader.Loader.GetAllTableRelations(f))
                        tr.ModifyAll(f.XValue, f.Value);

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
            TableFilterLevel = FilterLevel.Public;
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

        public List<object> GetPrimaryKey()
        {
            List<object> result = new();
            foreach (var f in TablePrimaryKey)
                    result.Add(f.Value!);
            return result;
        }

        public void FilterByPrimaryKey(List<object> pkValues)
        {
            Reset();
            for (int i = 0; i < TablePrimaryKey.Count; i++)
                TablePrimaryKey[i].SetRange(pkValues[i]);
        }

        public bool Refresh()
        {
            return Get(GetPrimaryKey());
        }

        public bool Get(params object[] pkValues)
        {
            return Get(pkValues.ToList());
        }

        public bool Get(List<object> pkValues)
        {
            List<object> val = new();
            _lastFilters.Clear();

            int i = 0;
            foreach (BaseField f in TablePrimaryKey)
            {
                if (pkValues.Count > i)
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

    public abstract class Table<T> : BaseTable 
    {
        public Table()
        {
            UnitType = UnitTypes.TABLE;
            if (typeof(T) != GetType())
                throw new Error(Label("Table type must be '{0}'"), GetType());
        }
    }
}
