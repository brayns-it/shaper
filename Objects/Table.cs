﻿using System.Text;
using Brayns.Shaper.Fields;

namespace Brayns.Shaper.Objects
{
    public abstract class BaseTable : Unit
    {
        private DbTable _dataset = new();
        private int _currentRow = -1;
        private List<FieldFilter> _lastFilters = new List<FieldFilter>();
        private bool _selection = false;
        private bool _pagination = false;
        internal bool _tableIsTemporary = false;
        internal bool _tableIsVirtual = false;
        internal bool _lockOnce = false;

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
        }

        internal List<ITableRelation> TableRelations { get; init; } = new();

        public bool TableIsTemporary
        {
            get { return _tableIsTemporary; }
        }

        public Fields.Timestamp TableVersion { get; } = new();
        public FilterLevel TableFilterLevel { get; set; }
        public bool TableAscending { get; set; } = true;
        public bool TableLock { get; set; } = false;
        public FieldList TableLookup { get; init; } = new();
        public FieldList TableSort { get; init; } = new();
        public FieldList TablePrimaryKey { get; init; } = new();
        public IndexList TableIndexes { get; init; } = new();
        public string TableSqlName { get; internal set; } = "";
        public event GenericHandler? Inserting;
        public event GenericHandler? Deleting;
        public event GenericHandler? Modifying;
        public event GenericHandler? Renaming;

        ~BaseTable()
        {
            try
            {
                if (_tableIsTemporary && (!_tableIsVirtual))
                    ((Database.SQLite)_database!).DropTemporaryTable(this);
            }
            catch
            {
                // already disposed
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();
            foreach (Fields.BaseField f in UnitFields)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(f.Name + ": " + ((f.Value != null) ? f.Value.ToString() : "NULL"));
            }
            return sb.ToString();
        }

        internal Error ErrorPrimaryKeyModify(BaseField f)
        {
            return new Classes.Error(Label("Cannot modify primary key '{0}', use rename instead", f.Caption));
        }

        internal Error ErrorNoPrimaryKey()
        {
            return new Classes.Error(Label("Table '{0}' has no primary key", UnitCaption));
        }

        internal Error ErrorConcurrency()
        {
            return new Classes.Error(Label("Another user has modified table '{0}' try again", UnitCaption));
        }

        internal FieldList GetCurrentSort()
        {
            var res = new FieldList();
            foreach (var f in TableSort)
                res.Add(f);
            foreach (var f in TablePrimaryKey)
                if (!res.Contains(f))
                    res.Add(f);
            return res;
        }

        internal void SetDataset(DbRow dataset)
        {
            TableDatabase!.LoadRow(this, dataset);
            AcceptChanges();
        }

        public DbRow GetDataset()
        {
            return _dataset![_currentRow];
        }

        internal Database.Database GetMemoryDatabase(BaseTable? sharedTable = null, string? tableSqlName = null)
        {
            Database.SQLite db;
            if (!Session.State.ContainsKey("MemoryDatabase"))
            {
                db = new();
                db.MemoryConnect();
                Session.State["MemoryDatabase"] = db;
            }
            else
                db = (Database.SQLite)Session.State["MemoryDatabase"];

            this.TableSqlName = tableSqlName ?? System.Guid.NewGuid().ToString("n");
            db.Compile(this);
            return db;
        }

        internal void Connect(Database.Database db)
        {
            _database = db;
        }

        public void SetTemporary(BaseTable? sharedTable = null)
        {
            if (sharedTable != null)
            {
                if (!sharedTable._tableIsTemporary)
                    throw new Error(Label("Table {0} must be temporary", sharedTable.UnitCaption));

                _database = sharedTable._database;
            }
            else
            {
                if (!_tableIsTemporary)
                    _database = GetMemoryDatabase();
            }

            _tableIsTemporary = true;
        }

        public bool Read()
        {
            _currentRow++;
            if (_currentRow >= _dataset!.Count)
            {
                _currentRow = 0;

                if (_selection || _pagination)
                    return false;

                _dataset = TableDatabase!.NextSet(this);
                if (_dataset.Count == 0)
                    return false;
            }

            TableDatabase!.LoadRow(this, _dataset![_currentRow]);
            AcceptChanges();
            return true;
        }

        internal void SetTextFilter(FieldList fields, string text)
        {
            Reset(FilterLevel.Or);

            var xLevel = TableFilterLevel;
            TableFilterLevel = FilterLevel.Or;

            if (text.Trim().Length > 0)
                foreach (var f in fields)
                {
                    if (!UnitFields.Contains(f))
                        continue;

                    if ((f.Type == Fields.FieldTypes.CODE) || (f.Type == Fields.FieldTypes.TEXT))
                        f.SetFilter("*" + text + "*");
                    else
                        f.SetFilter(text);

                    int n = f.Filters.Count - 1;
                    try
                    {
                        List<object> vals = new();
                        f.Filters[n].Tokenize(vals);
                    }
                    catch
                    {
                        f.Filters.RemoveAt(n);
                    }
                }

            TableFilterLevel = xLevel;
        }

        internal void ModifyAll(BaseField field, object? newValue, bool runTrigger = false)
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

        public void SetSelection(DbTable dataset)
        {
            _selection = true;
            _dataset = dataset;
        }

        internal bool FindSet(int limitRows, bool nextSet, bool reverseSort)
        {
            _dataset = TableDatabase!.FindSet(this, limitRows, nextSet, reverseSort, null);
            _pagination = true;
            _currentRow = -1;
            return (_dataset.Count > 0);
        }

        public bool FindSet()
        {
            if (!_selection)
                _dataset = TableDatabase!.FindSet(this);

            _pagination = false;
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
                return new Error(Error.E_RECORD_NOT_FOUND, Label("'{0}' not found", UnitCaption));
            else
                return new Error(Error.E_RECORD_NOT_FOUND, Label("'{0}' not found: {1}", UnitCaption, flt));
        }

        private string _tableName = "";
        public string TableName
        {
            get => _tableName;
            set
            {
                _tableName = value;
                TableSqlName = Functions.ToSqlName(value);
            }
        }

        public bool IsEmpty()
        {
            if (_selection)
                return (_dataset.Count == 0);
            else
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

        public bool FindFirst(bool lockOnce = false)
        {
            _lockOnce = lockOnce;
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

        public void Reset(bool allLevels = true)
        {
            _selection = false;

            if (allLevels)
            {
                TableFilterLevel = FilterLevel.Public;
                TableSort.Clear();
                foreach (BaseField f in UnitFields)
                    f.Filters.Clear();
            }
            else
            {
                Reset(TableFilterLevel);
            }
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
            if (_selection)
                return _dataset.Count();
            else
                return TableDatabase!.Count(this);
        }

        public List<object> PrimaryKeyValues()
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

        public bool Reload(bool lockOnce = false)
        {
            _lockOnce = lockOnce;
            return Get(PrimaryKeyValues());
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

            fieldFrom.TableRelations.Add(t);
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

            fieldFrom.TableRelations.Add(t);
        }

        public void Init()
        {
            Init(false);
        }

        public void Init(bool keepPrimaryKey)
        {
            foreach (BaseField f in UnitFields)
            {
                if (keepPrimaryKey && TablePrimaryKey.Contains(f)) continue;
                if (keepPrimaryKey && (f == TableVersion)) continue;

                if (keepPrimaryKey)
                    f.Value = f.InitValue;
                else
                    f.Init();
            }
        }

        public BaseField? FieldByName(string name)
        {
            foreach (var f in UnitFields)
                if (f.Name == name)
                    return f;
            return null;
        }

        public T? FieldByName<T>(string name) where T : BaseField
        {
            foreach (var f in UnitFields)
                if (f.Name == name)
                    return (T)f;
            return null;
        }

        public void CopyValues<T>(T fromTable) where T : BaseTable
        {
            Init();
            foreach (var f in UnitFields)
            {
                var f2 = fromTable.FieldByName(f.Name);
                if (f2 == null) continue;
                if (f2.Type != f.Type) continue;
                f.Value = f2.Value;
            }
        }

        public void CopyFilters<T>(T fromTable) where T : BaseTable
        {
            Reset();
            foreach (var f in UnitFields)
            {
                var fromF = fromTable.FieldByName(f.Name)!;
                foreach (var ff in fromF.Filters)
                    f.Filters.Add(ff.Clone(f));
            }
        }

        public JObject Serialize()
        {
            JObject result = new();
            foreach (BaseField f in UnitFields)
                result[Functions.NameForProperty(f.Name)] = f.Serialize();
            return result;
        }
    }

    public abstract class Table<T> : BaseTable
    {
        internal override void UnitInitialize()
        {
            base.UnitInitialize();

            UnitType = UnitTypes.TABLE;
            if (typeof(T) != GetType())
                throw new Error(Label("Table type must be '{0}'", GetType()));
        }

        internal override void UnitAfterInitialize()
        {
            base.UnitAfterInitialize();

            var atts = GetType().GetCustomAttributes(typeof(VirtualTable), true);
            if (atts.Length > 0)
            {
                VirtualTable vt = (VirtualTable)atts[0];
                if (vt.DataPerSession)
                {
                    _database = GetMemoryDatabase(tableSqlName: GetType().FullName!);
                    _tableIsTemporary = true;
                    _tableIsVirtual = true;
                }
                else
                {
                    SetTemporary();
                }
            }
        }
    }
}
