using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Brayns.Shaper.Database
{
    public class DatabaseTypes : OptList
    {
        public const int NONE = 0;

        [Label("SQL Server")]
        public const int SQLSERVER = 1;

        [Label("SQLite")]
        public const int SQLITE = 2;

        [Label("MySQL")]
        public const int MYSQL = 3;
    }

    public enum DatabaseCompileMode
    {
        Normal,
        Force,
        CheckOnly,
        Debug
    }

    public class DbRow : Dictionary<string, object>
    {
        public T Value<T>(string columnName)
        {
            object o = this[columnName];
            if (o == DBNull.Value)
            {
                if (typeof(T) == typeof(string)) o = "";
                else return default(T)!;
            }
            return (T)Convert.ChangeType(o, typeof(T));
        }
    }

    public class DbTable : List<DbRow>
    {
    }

    public abstract class Database
    {
        protected string Dsn { get; set; } = "";
        protected DbConnection? Connection { get; set; }
        protected DbTransaction? Transaction { get; set; }
        protected BaseTable? CompilingTable { get; set; }
        protected abstract object? FromSqlValue(Fields.BaseField f, object value);
        protected abstract object ToSqlValue(Fields.BaseField f, object? value);
        protected abstract List<string> GetTables();
        protected abstract void CompileTable(BaseTable table);
        protected abstract string QuoteIdentifier(string name);
        protected abstract string GetParameterName(int number);
        protected abstract DbCommand CreateCommand(string sql, params object[] args);
        protected abstract string GetLimit(int limitRows);
        protected abstract string GetTop(int limitRows);
        protected abstract DbConnection GetConnection();

        internal int DatasetSize { get; set; } = 50;
        internal DatabaseCompileMode CompileMode { get; set; } = DatabaseCompileMode.Normal;
        internal List<string> CompileResult { get; init; } = new();
        internal List<string> CompiledTables { get; init; } = new();

        internal abstract string GetConnectionString();

        public abstract int GetConnectionId();

        internal virtual void DatabaseInit()
        {
        }

        protected virtual void SetVersionAfterExecute(BaseTable table)
        {
        }

        internal virtual void DatabaseCheck()
        {
            foreach (var tab in GetTables())
                if (!CompiledTables.Contains(tab))
                    Application.Log("database", "I", Label("Foreign table '{0}' found", tab));
        }

        internal void Compile(BaseTable table)
        {
            if (table.TablePrimaryKey.Count == 0)
                throw table.ErrorNoPrimaryKey();

            CompilingTable = table;

            if (!CompiledTables.Contains(table.TableSqlName))
                CompiledTables.Add(table.TableSqlName);

            CompileTable(table);
        }

        protected int CompileExec(string sql, bool disruptive, params object[] args)
        {
            switch (CompileMode)
            {
                case DatabaseCompileMode.Force:
                    return Execute(sql, args);

                case DatabaseCompileMode.Normal:
                    if (disruptive)
                        throw new Error(Label("Prevent disruptive SQL {0}", sql));
                    else
                        return Execute(sql, args);

                case DatabaseCompileMode.CheckOnly:
                    if (disruptive)
                        CompileResult.Add(sql);
                    break;

                case DatabaseCompileMode.Debug:
                    Application.Log("dbcomp", "I", sql);
                    break;
            }

            return 0;
        }

        private void LogStatement(DateTime start, string sql, params object[] args)
        {
            StringBuilder log = new();
            log.Append(Convert.ToInt32(DateTime.Now.Subtract(start).TotalMilliseconds).ToString() + " ");

            sql = sql.Replace("\n", " ");
            sql = sql.Replace("\r", " ");
            sql = sql.Replace("\t", " ");

            log.Append(sql);

            int n = 0;
            foreach (var o in args)
            {
                log.Append(" p" + n.ToString() + ": ");
                if (o == null)
                    log.Append("NULL");
                else
                    log.Append(o.ToString());
                n++;
            }

            Application.Log("database", "sqlquery", "D", log.ToString());
        }

        internal int Execute(string sql, params object[] args)
        {
            var cmd = CreateCommand(sql, args);

            if (CurrentSession.DatabaseDebug)
            {
                DateTime dt = DateTime.Now;
                var result = cmd.ExecuteNonQuery();
                LogStatement(dt, sql, args);
                return result;
            }
            else
                return cmd.ExecuteNonQuery();
        }

        internal void Commit()
        {
            if (Transaction != null)
            {
                Transaction!.Commit();
                Transaction = null;
            }
        }

        internal void Rollback()
        {
            try
            {
                if (Transaction != null)
                {
                    Transaction!.Rollback();
                    Transaction = null;
                }
            }
            catch
            {
                // killed
                Disconnect();
                Connect(Dsn);
            }
        }

        public void LoadRow(BaseTable table, DbRow row)
        {
            foreach (Fields.BaseField f in table.UnitFields)
                f.Value = FromSqlValue(f, row[f.SqlName]);
        }

        internal DbRow? ReadRow(DbDataReader reader)
        {
            if (reader.Read())
            {
                var row = new DbRow();
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader[i];
                return row;
            }
            else
            {
                reader.Close();
                return null;
            }
        }

        internal DbTable Query(string sql, params object[] args)
        {
            var res = new DbTable();
            DbRow? row;

            var rdr = ExecuteReader(sql, args);
            while ((row = ReadRow(rdr)) != null)
                res.Add(row);

            return res;
        }

        internal void Connect(string dsn)
        {
            if (Connection != null)
                return;

            Dsn = dsn;
            Connection = GetConnection();
        }

        internal DbDataReader ExecuteReader(string sql, params object[] args)
        {
            var cmd = CreateCommand(sql, args);

            if (CurrentSession.DatabaseDebug)
            {
                DateTime dt = DateTime.Now;
                var result = cmd.ExecuteReader();
                LogStatement(dt, sql, args);
                return result;
            }
            else
                return cmd.ExecuteReader();
        }

        public void Disconnect()
        {
            try
            {
                if (Transaction != null)
                {
                    Transaction.Rollback();
                    Transaction = null;
                }
            }
            catch
            {
                // killed
                Transaction = null;
            }

            try
            {
                if (Connection != null)
                {
                    Connection.Close();
                    Connection = null;
                }
            }
            catch
            {
                // killed
                Connection = null;
            }
        }

        internal DbTable FindFirst(BaseTable table)
        {
            return FindSet(table, 1, false, false, null);
        }

        internal DbTable FindLast(BaseTable table)
        {
            return FindSet(table, 1, false, true, null);
        }

        internal DbTable NextSet(BaseTable table)
        {
            return FindSet(table, null, true, false, null);
        }

        internal DbTable Get(BaseTable table, object[] pkValues)
        {
            return FindSet(table, null, false, false, pkValues);
        }

        protected List<string> GetWhere(BaseTable table, List<object> pars)
        {
            var where = new List<string>();
            var filters = new Dictionary<FilterLevel, List<Fields.FieldFilter>>();

            foreach (Fields.BaseField f in table.UnitFields)
            {
                foreach (Fields.FieldFilter ff in f.Filters)
                {
                    if (!filters.ContainsKey(ff.Level))
                        filters[ff.Level] = new List<Fields.FieldFilter>();
                    filters[ff.Level].Add(ff);
                }
            }

            foreach (FilterLevel l in filters.Keys)
            {
                var lWhere = new List<string>();
                foreach (Fields.FieldFilter ff in filters[l])
                {
                    var sqlFilter = FilterToSql(ff, pars);
                    if (sqlFilter.Length > 0)
                        lWhere.Add(sqlFilter);
                }

                var mode = (l == FilterLevel.Or) ? " OR " : " AND ";
                where.Add("(" + String.Join(mode, lWhere) + ")");
            }

            return where;
        }

        protected string FilterToSql(Fields.FieldFilter ff, List<object> pars)
        {
            string sql = "";
            if (ff.Type == Fields.FilterType.Equal)
            {
                sql += QuoteIdentifier(ff.Field.SqlName) + " = " + GetParameterName(pars.Count);
                pars.Add(ToSqlValue(ff.Field, ff.Value));
            }
            else if (ff.Type == Fields.FilterType.Range)
            {
                sql += QuoteIdentifier(ff.Field.SqlName) + " BETWEEN " + GetParameterName(pars.Count) + " AND " + GetParameterName(pars.Count + 1);
                pars.Add(ToSqlValue(ff.Field, ff.MinValue));
                pars.Add(ToSqlValue(ff.Field, ff.MaxValue));
            }
            else if (ff.Type == Fields.FilterType.Expression)
            {
                List<object> vals = new List<object>();
                string expr = ff.Tokenize(vals);

                Regex re = new Regex("({f})(.*?){(\\d)}");
                expr = re.Replace(expr, m =>
                {
                    int n = pars.Count;
                    int d = int.Parse(m.Groups[3].Value);
                    pars.Add(ToSqlValue(ff.Field, vals[d]));
                    return QuoteIdentifier(ff.Field.SqlName) + m.Groups[2].Value + GetParameterName(n);
                });

                sql += expr;
            }
            if (sql.Length > 0)
                sql = "(" + sql + ")";
            return sql;
        }

        protected string GetWherePrimaryKey(BaseTable table, List<object> pars, bool withTimestamp = true, bool useXvalues = false)
        {
            var sql = "";

            bool comma = false;
            foreach (Fields.BaseField f in table.TablePrimaryKey)
            {
                if (comma) sql += " AND ";
                comma = true;

                sql += "(" + QuoteIdentifier(f.SqlName) + " = " + GetParameterName(pars.Count) + ")";

                if (useXvalues)
                    pars.Add(ToSqlValue(f, f.XValue));
                else
                    pars.Add(ToSqlValue(f, f.Value));
            }

            if (withTimestamp)
            {
                sql += " AND (" + QuoteIdentifier(table.TableVersion.SqlName) + " <= " + GetParameterName(pars.Count) + ")";
                pars.Add(ToSqlValue(table.TableVersion, table.TableVersion.XValue));
            }

            return sql;
        }

        internal T Min<T>(BaseTable table, Fields.BaseField field)
        {
            List<object> pars = new();
            var sql = "SELECT MIN(" + QuoteIdentifier(field.SqlName) + ") " + QuoteIdentifier("min") + " FROM " + QuoteIdentifier(table.TableSqlName);

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            var res = Query(sql, pars.ToArray());
            return res[0].Value<T>("min");
        }

        internal T Max<T>(BaseTable table, Fields.BaseField field)
        {
            List<object> pars = new();
            var sql = "SELECT MAX(" + QuoteIdentifier(field.SqlName) + ") " + QuoteIdentifier("max") + " FROM " + QuoteIdentifier(table.TableSqlName);

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            var res = Query(sql, pars.ToArray());
            return res[0].Value<T>("max");
        }

        internal T Sum<T>(BaseTable table, Fields.BaseField field)
        {
            List<object> pars = new();
            var sql = "SELECT SUM(" + QuoteIdentifier(field.SqlName) + ") " + QuoteIdentifier("sum") + " FROM " + QuoteIdentifier(table.TableSqlName);

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            var res = Query(sql, pars.ToArray());
            return res[0].Value<T>("sum");
        }

        internal int Count(BaseTable table)
        {
            List<object> pars = new();
            var sql = "SELECT COUNT(*) " + QuoteIdentifier("count") + " FROM " + QuoteIdentifier(table.TableSqlName);

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            var res = Query(sql, pars.ToArray());
            return res[0].Value<int>("count");
        }

        internal void Delete(BaseTable table)
        {
            List<object> pars = new();
            var sql = "DELETE FROM " + QuoteIdentifier(table.TableSqlName) + " WHERE " +
                GetWherePrimaryKey(table, pars);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();
        }

        internal void DeleteAll(BaseTable table)
        {
            List<object> pars = new();
            var sql = "DELETE FROM " + QuoteIdentifier(table.TableSqlName);

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            Execute(sql, pars.ToArray());
        }

        public void ModifyAll(BaseTable table, Fields.BaseField field)
        {
            List<object> pars = new();

            var sql = "UPDATE " + QuoteIdentifier(table.TableSqlName) + " SET " + QuoteIdentifier(field.SqlName) + " = " + GetParameterName(pars.Count);
            pars.Add(ToSqlValue(field, field.Value));

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            Execute(sql, pars.ToArray());
            SetVersionAfterExecute(table);
        }

        internal void Rename(BaseTable table)
        {
            List<object> pars = new();

            List<Fields.BaseField> fields = new();
            foreach (Fields.BaseField f in table.TablePrimaryKey)
            {
                if (Functions.AreEquals(f.Value, f.XValue))
                    continue;
                fields.Add(f);
            }
            if (fields.Count == 0)
                return;

            var sql = "UPDATE " + QuoteIdentifier(table.TableSqlName) + " SET ";

            bool comma = false;
            foreach (Fields.BaseField f in fields)
            {
                if (comma) sql += ", ";
                comma = true;

                sql += QuoteIdentifier(f.SqlName) + " = " + GetParameterName(pars.Count);
                pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars, true, true);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            SetVersionAfterExecute(table);
        }

        protected virtual bool OnModifyTimestamp(BaseTable table, Fields.Timestamp timestamp)
        {
            return false;
        }

        internal void Modify(BaseTable table)
        {
            List<object> pars = new();

            Fields.Timestamp? timestamp = null;
            List<Fields.BaseField> fields = new();
            foreach (Fields.BaseField f in table.UnitFields)
            {
                if (f.Type == Fields.FieldTypes.TIMESTAMP)
                {
                    timestamp = (Fields.Timestamp)f;
                    continue;
                }
                if (Functions.AreEquals(f.Value, f.XValue))
                    continue;
                fields.Add(f);
            }
            if (fields.Count == 0)
                return;

            if (timestamp != null)
                if (OnModifyTimestamp(table, timestamp))
                    fields.Add(timestamp);

            var sql = "UPDATE " + QuoteIdentifier(table.TableSqlName) + " SET ";

            bool comma = false;
            foreach (Fields.BaseField f in fields)
            {
                if (comma) sql += ", ";
                comma = true;

                sql += QuoteIdentifier(f.SqlName) + " = " + GetParameterName(pars.Count);
                pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            SetVersionAfterExecute(table);
        }

        internal bool IsEmpty(BaseTable table)
        {
            List<object> pars = new();
            var sql = "SELECT " + GetTop(1) + " NULL " + QuoteIdentifier("ne") + " FROM " + QuoteIdentifier(table.TableSqlName);

            var where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            sql += " ORDER BY ";

            bool comma = false;
            foreach (Fields.BaseField f in table.TablePrimaryKey)
            {
                if (comma) sql += ", ";
                comma = true;
                sql += QuoteIdentifier(f.SqlName);
            }

            sql += " " + GetLimit(1);

            if (Query(sql, pars.ToArray()).Count == 0)
                return true;
            else
                return false;
        }

        internal string ListFields(List<Fields.BaseField> fields, string prefix = "")
        {
            var res = "";
            bool comma = false;
            foreach (Fields.BaseField f in fields)
            {
                if (comma) res += ", ";
                comma = true;

                res += prefix + QuoteIdentifier(f.SqlName);
            }
            return res;
        }

        protected virtual void OnFindSetAfterFrom(BaseTable table, ref string sql)
        {
        }

        protected virtual void OnFindSetAfterSelect(BaseTable table, ref string sql)
        {
        }

        internal DbTable FindSet(BaseTable table)
        {
            return FindSet(table, null, false, false, null);
        }

        internal DbTable FindSet(BaseTable table, int? limitRows, bool nextSet, bool reverseSort, object[]? pkValues)
        {
            List<object> pars = new();
            bool ascending = table.TableAscending;
            if (reverseSort) ascending = !ascending;

            var sql = "SELECT ";
            if (pkValues == null)
                sql += GetTop(limitRows ?? DatasetSize);
            sql += " " + ListFields(table.UnitFields) + " FROM " + QuoteIdentifier(table.TableSqlName);
            OnFindSetAfterFrom(table, ref sql);

            var where = new List<string>();
            if (pkValues != null)
            {
                int i = 0;
                foreach (Fields.BaseField f in table.TablePrimaryKey)
                {
                    where.Add("(" + QuoteIdentifier(f.SqlName) + " = " + GetParameterName(pars.Count) + ")");
                    pars.Add(ToSqlValue(f, pkValues[i]));
                    i++;
                }
            }
            else
            {
                if (nextSet)
                {
                    List<Fields.BaseField> ck = table.GetCurrentSort();
                    int k = ck.Count;
                    int l = k;
                    var wn = new List<string>();

                    for (int i = 0; i < k; i++)
                    {
                        var ws = new List<string>();

                        for (int j = 0; j < l; j++)
                        {
                            Fields.BaseField f = ck[j];
                            string op = "=";
                            if (j == (l - 1))
                                op = ascending ? ">" : "<";

                            ws.Add("(" + QuoteIdentifier(f.SqlName) + " " + op + " " + GetParameterName(pars.Count) + ")");
                            pars.Add(ToSqlValue(f, f.Value));
                        }

                        wn.Add("(" + String.Join(" AND ", ws) + ")");
                        l--;
                    }

                    where.Add("(" + String.Join(" OR ", wn) + ")");
                }

                where.AddRange(GetWhere(table, pars));
            }

            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            if (pkValues == null)
            {
                sql += " ORDER BY ";

                bool comma = false;
                foreach (Fields.BaseField f in table.GetCurrentSort())
                {
                    if (comma) sql += ", ";
                    comma = true;

                    sql += QuoteIdentifier(f.SqlName);
                    if (!ascending)
                        sql += " DESC";
                }

                sql += " " + GetLimit(limitRows ?? DatasetSize);
            }

            OnFindSetAfterSelect(table, ref sql);

            var result = Query(sql, pars.ToArray());
            if (reverseSort) result.Reverse();
            return result;
        }

        protected virtual bool OnInsertTimestamp(BaseTable table, Fields.Timestamp timestamp)
        {
            return false;
        }

        protected virtual void OnInsertBeforeIdentityInsert(BaseTable table)
        {
        }

        protected virtual void OnInsertAfterIdentityInsert(BaseTable table)
        {
        }

        protected virtual void OnInsertGetLastIdentity(BaseTable table, Fields.BaseField identity)
        {
        }

        internal void Insert(BaseTable table)
        {
            List<object> pars = new();
            List<string> places = new();

            Fields.BaseField? identity = null;
            bool identityInsert = false;
            List<Fields.BaseField> fields = new();

            foreach (Fields.BaseField field in table.UnitFields)
            {
                if (field.Type == Fields.FieldTypes.TIMESTAMP)
                    if (!OnInsertTimestamp(table, (Fields.Timestamp)field))
                        continue;

                if ((field.Type == Fields.FieldTypes.INTEGER) || (field.Type == Fields.FieldTypes.BIGINTEGER))
                {
                    var f = (Fields.IInteger)field;
                    if (f.AutoIncrement)
                    {
                        identity = field;
                        if (Convert.ToInt64(field.Value!) == 0)
                            continue;
                        else
                            identityInsert = true;
                    }
                }

                fields.Add(field);
                places.Add(GetParameterName(pars.Count));
                pars.Add(ToSqlValue(field, field.Value));
            }

            var sql = "INSERT INTO " + QuoteIdentifier(table.TableSqlName) + " (" +
                ListFields(fields) +
                ") VALUES (" +
                String.Join(", ", places) +
                ")";

            if (identityInsert)
                OnInsertBeforeIdentityInsert(table);

            Execute(sql, pars.ToArray());
            SetVersionAfterExecute(table);

            if (identityInsert)
                OnInsertAfterIdentityInsert(table);

            if ((identity != null) && (!identityInsert))
                OnInsertGetLastIdentity(table, identity);
        }
    }
}
