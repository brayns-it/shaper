﻿using Microsoft.AspNetCore.Http.HttpResults;
using Brayns.Shaper.Fields;
using Brayns.Shaper.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Database
{
    public class SQLite : Database
    {
        string _dsn = "";
        SqliteConnection? _connection;
        SqliteTransaction? _transaction;
        Dictionary<SqliteDataReader, SqliteConnection> _readerConns = new();

        static SQLite()
        {
            SQLitePCL.Batteries.Init();
        }
        
        public override void DatabaseCheck()
        {
            // TODO
        }

        public override void Compile(BaseTable table)
        {
            if (table.TablePrimaryKey.Count == 0)
                throw table.ErrorNoPrimaryKey();

            CompilingTable = table;

            ProcessTable();
            ProcessPrimaryKey();
        }

        private List<string> GetPrimaryKey(BaseTable table)
        {
            var res = Query(@"SELECT sql FROM sqlite_schema WHERE ([type] = 'index') AND ([name] = $p0) AND ([tbl_name] = $p1)",
                table.TableSqlName + "$PK", table.TableSqlName);

            var pk = new List<string>();

            if (res.Count > 0)
            {
                string part = Regex.Match(res[0]["sql"].ToString()!, "\\((.*)\\)").Groups[1].Value;
                foreach (Match m in Regex.Matches(part, "(\\[(.*?)\\])"))
                    pk.Add(m.Groups[2].Value);
            }

            return pk;
        }

        private string GetFieldType(BaseField field)
        {
            string res = "";

            if (field.Type == FieldTypes.TIMESTAMP)
            {
                res += "int NOT NULL";
            }
            else if ((field.Type == FieldTypes.CODE) || (field.Type == FieldTypes.TEXT))
            {
                var f = (Fields.Text)field;
                if (f.Length == Fields.Text.MAX_LENGTH)
                    res += "text COLLATE NOCASE NOT NULL";
                else
                    res += "nvarchar(" + f.Length.ToString() + ") COLLATE NOCASE NOT NULL";
            }
            else if (field.Type == FieldTypes.INTEGER)
            {
                var f = (Fields.Integer)field;

                if (f.AutoIncrement)
                    res += "INTEGER PRIMARY KEY AUTOINCREMENT";
                else
                    res += "int NOT NULL";
            }
            else if (field.Type == FieldTypes.BIGINTEGER)
            {
                var f = (Fields.BigInteger)field;
                if (f.AutoIncrement)
                    res += "INTEGER PRIMARY KEY AUTOINCREMENT";
                else
                    res += "bigint NOT NULL";
            }
            else if (field.Type == FieldTypes.DECIMAL)
            {
                res += "decimal(38,20) NOT NULL";
            }
            else if ((field.Type == FieldTypes.DATE) || (field.Type == FieldTypes.TIME) || (field.Type == FieldTypes.DATETIME))
            {
                res += "datetime NOT NULL";
            }
            else if (field.Type == FieldTypes.OPTION)
            {
                res += "int NOT NULL";
            }
            else if (field.Type == FieldTypes.BOOLEAN)
            {
                res += "tinyint NOT NULL";
            }
            else if (field.Type == FieldTypes.GUID)
            {
                res += "nvarchar(100) COLLATE NOCASE NOT NULL";
            }
            else if (field.Type == FieldTypes.BLOB)
            {
                res += "blob NULL";
            }
            else
            {
                throw new Error(Label("Unknown field type '{0}'", field.Type));
            }

            return res;
        }

        public override int Execute(string sql, params object[] args)
        {
            var cmd = CreateCommand(sql, args);
            return cmd.ExecuteNonQuery();
        }

        public override int GetConnectionId()
        {
            return Thread.CurrentThread.ManagedThreadId;
        }

        private void ProcessPrimaryKey()
        {
            var curPk = String.Join(", ", GetPrimaryKey(CompilingTable!));
            var newPk = String.Join(", ", CompilingTable!.TablePrimaryKey.Select(f => f.SqlName));

            if (curPk == newPk)
                return;

            if (curPk.Length > 0)
                DropPrimaryKey();

            if (newPk.Length > 0)
            {
                var sql = "CREATE UNIQUE INDEX [" + CompilingTable!.TableSqlName + "$PK] " +
                    "ON [" + CompilingTable!.TableSqlName + "] (" +
                    ListFields(CompilingTable!.TablePrimaryKey) +
                    ")";

                CompileExec(sql, false);
            }
        }

        private string ListFields(List<BaseField> fields, string prefix = "")
        {
            var res = "";
            bool comma = false;
            foreach (BaseField f in fields)
            {
                if (comma) res += ", ";
                comma = true;

                res += prefix + "[" + f.SqlName + "]";
            }
            return res;
        }

        private void DropPrimaryKey()
        {
            var res = Query(@"SELECT sql FROM sqlite_schema WHERE ([type] = 'index') AND ([name] = $p0) AND ([tbl_name] = $p1)",
                CompilingTable!.TableSqlName + "$PK", CompilingTable!.TableSqlName);

            if (res.Count == 0)
                return;

            var sql = "DROP INDEX [" + CompilingTable!.TableSqlName + "$PK]";

            CompileExec(sql, false);
        }

        private bool AlterTable(string def)
        {
            int n1 = def.IndexOf("(");
            int n2 = def.LastIndexOf(")");
            def = def.Substring(n1 + 1, n2 - n1 - 1);

            string[] fieldDefs = def.Split(',');

            var toDelete = new List<string>();
            var toAdd = new List<Fields.BaseField>();
            var curPk = GetPrimaryKey(CompilingTable!);

            foreach (var field in CompilingTable!.UnitFields)
            {
                bool ok = false;
                var newDef = GetFieldType(field);

                foreach (var row in fieldDefs)
                {
                    if (row.Trim().StartsWith("[" + field.SqlName + "]"))
                    {
                        n1 = row.IndexOf("]");
                        var curDef = row.Substring(n1 + 1).Trim();

                        n1 = curDef.IndexOf("DEFAULT");
                        if (n1 > -1)
                            curDef = curDef.Substring(0, n1).Trim();

                        if (!newDef.Equals(curDef, StringComparison.OrdinalIgnoreCase))
                        {
                            if (newDef.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) || curDef.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                            {
                                // cannot add primary key to tables
                                return false;
                            }

                            toDelete.Add(field.SqlName);
                            toAdd.Add(field);
                        }

                        ok = true;
                        break;
                    }
                }

                if (!ok)
                {
                    if (newDef.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                    {
                        // cannot add primary key to tables
                        return false;
                    }

                    toAdd.Add(field);
                }
            }

            foreach (var row in fieldDefs)
            {
                bool ok = false;

                foreach (var field in CompilingTable!.UnitFields)
                {
                    if (row.Trim().StartsWith("[" + field.SqlName + "]"))
                    {
                        ok = true;
                        break;
                    }
                }

                if (!ok)
                {
                    n1 = row.IndexOf("[");
                    n2 = row.LastIndexOf("]");
                    toDelete.Add(row.Substring(n1 + 1, n2 - n1 - 1));
                }
            }

            foreach (string fn in toDelete)
            {
                if (curPk.Contains(fn))
                    DropPrimaryKey();

                var sql = "ALTER TABLE [" + CompilingTable.TableSqlName + "] " +
                    "DROP COLUMN [" + fn + "]";

                CompileExec(sql, true);
            }

            foreach (BaseField field in toAdd)
            {
                var sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] ADD " +
                    "[" + field.SqlName + "] " + GetFieldType(field);

                if ((field.Type != FieldTypes.BLOB))
                {
                    sql += " DEFAULT ";

                    if ((field.Type == FieldTypes.CODE) || (field.Type == FieldTypes.TEXT))
                        sql += "''";
                    else if ((field.Type == FieldTypes.INTEGER) || (field.Type == FieldTypes.BIGINTEGER) || (field.Type == FieldTypes.TIMESTAMP))
                        sql += "0";
                    else if ((field.Type == FieldTypes.DECIMAL) || (field.Type == FieldTypes.BOOLEAN))
                        sql += "0";
                    else if (field.Type == FieldTypes.OPTION)
                        sql += "0";
                    else if (field.Type == FieldTypes.GUID)
                        sql += "'00000000-0000-0000-0000-000000000000'";
                    else if ((field.Type == FieldTypes.DATE) || (field.Type == FieldTypes.DATETIME))
                        sql += "'1753-01-01'";
                    else if (field.Type == FieldTypes.TIME)
                        sql += "'1753-01-01'";
                }

                CompileExec(sql, false);
            }

            return true;
        }

        private void ProcessTable()
        {
            DbTable tab = Query("SELECT * FROM sqlite_schema WHERE ([type] = 'table') AND ([name] = $p0) LIMIT 1", CompilingTable!.TableSqlName);
            bool createTable = true;

            if (tab.Count > 0)
            {
                string def = tab[0].Value<string>("sql");
                createTable = !AlterTable(def);

                if (createTable)
                {
                    var sql = "DROP TABLE [" + CompilingTable.TableSqlName + "]";
                    CompileExec(sql, true);
                }
            }

            if (createTable)
            {
                var sql = "CREATE TABLE [" + CompilingTable!.TableSqlName + "] (";

                bool comma = false;
                foreach (BaseField field in CompilingTable!.UnitFields)
                {
                    if (comma) sql += ", ";
                    comma = true;
                    sql += "[" + field.SqlName + "] " + GetFieldType(field);
                }
                sql += ")";

                CompileExec(sql, false);
            }
        }

        public override void Commit()
        {
            if (_transaction != null)
            {
                _transaction!.Commit();
                _transaction = null;
            }
        }

        public override void Rollback()
        {
            if (_transaction != null)
            {
                _transaction!.Rollback();
                _transaction = null;
            }
        }

        public static string GetConnectionString(string fileName)
        {
            return "Data Source=" + Application.RootPath + "var/" + fileName;
        }

        public override void Connect()
        {
            Connect(Application.Config.DatabaseConnection);
        }

        public void Connect(bool temporary)
        {
            if (temporary)
            {
                _connection = new("Data Source=:memory:");
                _connection.Open();
                _transaction = null;
            }
            else
                Connect();
        }

        public override void Connect(string dsn)
        {
            _dsn = dsn;
            _connection = new(dsn);
            _connection.Open();
            _transaction = null;
        }

        private SqliteCommand CreateCommand(string sql, params object[] args)
        {
            var cmd = _connection!.CreateCommand();
            if (_transaction == null) _transaction = _connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            cmd.Transaction = _transaction;
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.AddWithValue("$p" + i.ToString(), args[i]);

            return cmd;
        }

        public override object ExecuteReader(string sql, params object[] args)
        {
            var conn = new SqliteConnection(_dsn);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i.ToString(), args[i]);

            var rdr = cmd.ExecuteReader();
            _readerConns.Add(rdr, conn);
            return rdr;
        }

        public override DbRow? ReadRow(object reader)
        {
            var rdr = (SqliteDataReader)reader;
            if (rdr.Read())
            {
                var row = new DbRow();
                for (var i = 0; i < rdr.FieldCount; i++)
                    row[rdr.GetName(i)] = rdr[i];
                return row;
            }
            else
            {
                rdr.Close();

                if (_readerConns.ContainsKey(rdr))
                {
                    _readerConns[rdr].Close();
                    _readerConns.Remove(rdr);
                }

                return null;
            }
        }

        public override DbTable Query(string sql, params object[] args)
        {
            var res = new DbTable();
            DbRow? row;

            var cmd = CreateCommand(sql, args);
            var rdr = cmd.ExecuteReader();
            while ((row = ReadRow(rdr)) != null)
                res.Add(row);

            return res;
        }

        public override void Disconnect()
        {
            _transaction?.Rollback();
            _transaction = null;

            _connection?.Close();
            _connection = null;

            foreach (var conn in _readerConns.Values)
                conn.Close();

            _readerConns.Clear();
        }

        private object? FromSqlValue(BaseField f, object value)
        {
            if (f.Type == FieldTypes.TIMESTAMP)
                return Convert.ToInt64(value!);

            if ((f.Type == FieldTypes.CODE) || (f.Type == FieldTypes.TEXT))
                return Convert.ToString(value);

            if (f.Type == FieldTypes.INTEGER)
                return Convert.ToInt32(value);

            if (f.Type == FieldTypes.BIGINTEGER)
                return Convert.ToInt64(value);

            if (f.Type == FieldTypes.DECIMAL)
                return Convert.ToDecimal(value);

            if (f.Type == FieldTypes.GUID)
                return System.Guid.Parse(Convert.ToString(value)!);

            if (f.Type == FieldTypes.OPTION)
                return Convert.ToInt32(value);

            if (f.Type == FieldTypes.BOOLEAN)
                return Convert.ToInt32(value) == 1;

            if (f.Type == FieldTypes.DATETIME)
            {
                var dt = System.DateTime.Parse(value.ToString()!);
                if (dt == new System.DateTime(1753, 1, 1))
                    return System.DateTime.MinValue;
                else
                    return dt.ToLocalTime();
            }

            if (f.Type == FieldTypes.DATE)
            {
                var dt = System.DateTime.Parse(value.ToString()!);
                if (dt == new System.DateTime(1753, 1, 1))
                    return System.DateTime.MinValue;
                else
                    return new System.DateTime(dt.Year, dt.Month, dt.Day);
            }

            if (f.Type == FieldTypes.TIME)
            {
                var dt = System.DateTime.Parse(value.ToString()!);
                if (dt == new System.DateTime(1753, 1, 1))
                    return System.DateTime.MinValue;
                else
                    return new System.DateTime(1754, 1, 1, dt.Hour, dt.Minute, dt.Second);
            }

            if (f.Type == FieldTypes.BLOB)
                return (value == DBNull.Value) ? null : value;

            throw new Error(Label("Unknown field type '{0}'", f.Type));
        }

        private object ToSqlValue(BaseField f, object? value)
        {
            if (f.Type == FieldTypes.TIMESTAMP)
                return Convert.ToInt64(value!);

            if ((f.Type == FieldTypes.CODE) || (f.Type == FieldTypes.TEXT))
                return value!;

            if ((f.Type == FieldTypes.INTEGER) || (f.Type == FieldTypes.BIGINTEGER))
                return value!;

            if (f.Type == FieldTypes.DECIMAL)
                return value!;

            if (f.Type == FieldTypes.GUID)
                return value!;

            if (f.Type == FieldTypes.OPTION)
                if (value!.GetType() == typeof(int))
                    return value!;
                else
                    return ((Opt)value!).Value;

            if (f.Type == FieldTypes.BOOLEAN)
                return ((bool)value!) ? 1 : 0;

            if (f.Type == FieldTypes.DATETIME)
            {
                var dt = (System.DateTime)value!;
                if (dt == System.DateTime.MinValue)
                    return new System.DateTime(1753, 1, 1);
                else
                    return dt.ToUniversalTime();
            }

            if (f.Type == FieldTypes.DATE)
            {
                var dt = (System.DateTime)value!;
                if (dt == System.DateTime.MinValue)
                    return new System.DateTime(1753, 1, 1);
                else
                    return new System.DateTime(dt.Year, dt.Month, dt.Day);
            }

            if (f.Type == FieldTypes.TIME)
            {
                var dt = (System.DateTime)value!;
                if (dt == System.DateTime.MinValue)
                    return new System.DateTime(1753, 1, 1);
                else
                    return new System.DateTime(1754, 1, 1, dt.Hour, dt.Minute, dt.Second);
            }

            if (f.Type == FieldTypes.BLOB)
                return value ?? DBNull.Value;

            throw new Error(Label("Unknown field type '{0}'", f.Type));
        }

        private string GetWherePrimaryKey(BaseTable table, List<object> pars, bool withTimestamp = true, bool useXvalues = false)
        {
            var sql = "";

            bool comma = false;
            foreach (BaseField f in table.TablePrimaryKey)
            {
                if (comma) sql += " AND ";
                comma = true;

                sql += "([" + f.SqlName + "] = $p" + pars.Count + ")";

                if (useXvalues)
                    pars.Add(ToSqlValue(f, f.XValue));
                else
                    pars.Add(ToSqlValue(f, f.Value));
            }

            if (withTimestamp)
            {
                sql += " AND ([timestamp] <= $p" + pars.Count + ")";
                pars.Add(ToSqlValue(table.TableVersion, table.TableVersion.Value));
            }

            return sql;
        }

        private string FilterToSql(FieldFilter ff, List<object> pars)
        {
            string sql = "";
            if (ff.Type == FilterType.Equal)
            {
                sql += "[" + ff.Field.SqlName + "] = $p" + pars.Count;
                pars.Add(ToSqlValue(ff.Field, ff.Value));
            }
            else if (ff.Type == FilterType.Range)
            {
                sql += "[" + ff.Field.SqlName + "] BETWEEN $p" + pars.Count + " AND $p" + (pars.Count + 1);
                pars.Add(ToSqlValue(ff.Field, ff.MinValue));
                pars.Add(ToSqlValue(ff.Field, ff.MaxValue));
            }
            else if (ff.Type == FilterType.Expression)
            {
                List<object> vals = new List<object>();
                string expr = ff.Tokenize(vals);

                Regex re = new Regex("({f})(.*?){(\\d)}");
                expr = re.Replace(expr, m =>
                {
                    int n = pars.Count;
                    int d = int.Parse(m.Groups[3].Value);
                    pars.Add(ToSqlValue(ff.Field, vals[d]));
                    return "[" + ff.Field.SqlName + "]" + m.Groups[2].Value + "$p" + n;
                });

                sql += expr;
            }
            return sql;
        }

        private List<string> GetWhere(BaseTable table, List<object> pars)
        {
            var where = new List<string>();
            var filters = new Dictionary<FilterLevel, List<FieldFilter>>();

            foreach (BaseField f in table.UnitFields)
            {
                foreach (FieldFilter ff in f.Filters)
                {
                    if (!filters.ContainsKey(ff.Level))
                        filters[ff.Level] = new List<FieldFilter>();
                    filters[ff.Level].Add(ff);
                }
            }

            foreach (FilterLevel l in filters.Keys)
            {
                var lWhere = new List<string>();
                foreach (FieldFilter ff in filters[l])
                    lWhere.Add(FilterToSql(ff, pars));

                var mode = (l == FilterLevel.Or) ? " OR " : " AND ";
                where.Add("(" + String.Join(mode, lWhere) + ")");
            }

            return where;
        }

        public override bool IsEmpty(BaseTable table)
        {
            List<object> pars = new();
            var sql = "SELECT NULL [ne] FROM [" + table.TableSqlName + "]";

            var where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            sql += " LIMIT 1";

            if (Query(sql, pars.ToArray()).Count == 0)
                return true;
            else
                return false;
        }

        public override void Insert(BaseTable table)
        {
            List<object> pars = new();
            List<string> places = new();

            BaseField? identity = null;
            bool identityInsert = false;
            List<BaseField> fields = new();

            table.TableVersion.Value = 1;

            foreach (BaseField field in table.UnitFields)
            {
                if ((field.Type == FieldTypes.INTEGER) || (field.Type == FieldTypes.BIGINTEGER))
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
                places.Add("$p" + pars.Count);
                pars.Add(ToSqlValue(field, field.Value));
            }

            var sql = "INSERT INTO [" + table.TableSqlName + "] (" +
                ListFields(fields) +
                ") VALUES (" +
                String.Join(", ", places) +
                ")";

            Execute(sql, pars.ToArray());

            if ((identity != null) && (!identityInsert))
                identity.Value = Query("SELECT last_insert_rowid() AS [id]")[0]["id"];
        }

        private DbTable FindSet(BaseTable table, int? pageSize, int? offset, bool nextSet, bool? ascending, object[]? pkValues)
        {
            List<object> pars = new();
            ascending = ascending ?? table.TableAscending;

            var sql = "SELECT " + ListFields(table.UnitFields) + " FROM [" + table.TableSqlName + "]";

            var where = new List<string>();
            if (pkValues != null)
            {
                int i = 0;
                foreach (BaseField f in table.TablePrimaryKey)
                {
                    where.Add("([" + f.SqlName + "] = $p" + pars.Count + ")");
                    pars.Add(ToSqlValue(f, pkValues[i]));
                    i++;
                }
            }
            else
            {
                if (nextSet)
                {
                    List<BaseField> ck = table.GetCurrentSort();
                    int k = ck.Count;
                    int l = k;
                    var wn = new List<string>();

                    for (int i = 0; i < k; i++)
                    {
                        var ws = new List<string>();

                        for (int j = 0; j < l; j++)
                        {
                            BaseField f = ck[j];
                            string op = "=";
                            if (j == (l - 1))
                                op = (ascending ?? false) ? ">" : "<";

                            ws.Add("([" + f.SqlName + "] " + op + " $p" + pars.Count + ")");
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
                foreach (BaseField f in table.GetCurrentSort())
                {
                    if (comma) sql += ", ";
                    comma = true;

                    sql += "[" + f.SqlName + "]";
                    if (!(ascending ?? false))
                        sql += " DESC";
                }

                offset = offset ?? 0;
                pageSize = pageSize ?? DatasetSize;

                sql += " LIMIT " + pageSize.ToString() + " OFFSET " + offset.ToString();
            }

            return Query(sql, pars.ToArray());
        }

        public override int Count(BaseTable table)
        {
            List<object> pars = new();
            var sql = "SELECT COUNT(*) [c] FROM [" + table.TableSqlName + "]";

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            var res = Query(sql, pars.ToArray());
            return Convert.ToInt32(res[0]["c"]!);
        }

        public override DbTable FindFirst(BaseTable table)
        {
            return FindSet(table, 1, 0, false, null, null);
        }

        public override DbTable FindLast(BaseTable table)
        {
            return FindSet(table, 1, 0, false, !(table.TableAscending ^ false), null);
        }

        public override DbTable FindSet(BaseTable table, int? pageSize = null, int? offset = null)
        {
            return FindSet(table, pageSize, offset, false, null, null);
        }

        public override DbTable NextSet(BaseTable table)
        {
            return FindSet(table, null, null, true, null, null);
        }

        public override DbTable Get(BaseTable table, object[] pkValues)
        {
            return FindSet(table, null, null, false, null, pkValues);
        }

        public override void LoadRow(BaseTable table, Dictionary<string, object> row)
        {
            foreach (BaseField f in table.UnitFields)
                f.Value = FromSqlValue(f, row[f.SqlName]);
        }

        public override void Delete(BaseTable table)
        {
            List<object> pars = new();
            var sql = "DELETE FROM [" + table.TableSqlName + "] WHERE " +
                GetWherePrimaryKey(table, pars);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();
        }

        public override void DeleteAll(BaseTable table)
        {
            List<object> pars = new();
            var sql = "DELETE FROM [" + table.TableSqlName + "]";

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            Execute(sql, pars.ToArray());
        }

        public override void ModifyAll(BaseTable table, Fields.BaseField field)
        {
            List<object> pars = new();

            var sql = "UPDATE [" + table.TableSqlName + "] SET " +
                "[" + field.SqlName + "] = $p0, [timestamp] = [timestamp] + 1";
            pars.Add(ToSqlValue(field, field.Value));

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            Execute(sql, pars.ToArray());
            table.TableVersion.Value++;
        }

        public override void Rename(BaseTable table)
        {
            List<object> pars = new();

            List<BaseField> fields = new();
            foreach (BaseField f in table.TablePrimaryKey)
            {
                if (Functions.AreEquals(f.Value, f.XValue))
                    continue;
                fields.Add(f);
            }
            if (fields.Count == 0)
                return;

            var sql = "UPDATE [" + table.TableSqlName + "] SET ";

            bool comma = false;
            foreach (BaseField f in fields)
            {
                if (comma) sql += ", ";
                comma = true;

                sql += "[" + f.SqlName + "] = $p" + pars.Count;
                pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars, true, true);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            //SetVersion(table);
        }

        public override void Modify(BaseTable table)
        {
            List<object> pars = new();

            List<BaseField> fields = new();
            foreach (BaseField f in table.UnitFields)
            {
                if (Functions.AreEquals(f.Value, f.XValue))
                    continue;
                fields.Add(f);
            }
            if (fields.Count == 0)
                return;

            fields.Add(table.TableVersion);

            var sql = "UPDATE [" + table.TableSqlName + "] SET ";
            bool comma = false;
            foreach (BaseField f in fields)
            {
                if (comma) sql += ", ";
                comma = true;
                sql += "[" + f.SqlName + "] = $p" + pars.Count;

                if (f == table.TableVersion)
                    pars.Add(ToSqlValue(f, table.TableVersion.Value + 1));
                else
                    pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            table.TableVersion.Value++;
        }
    }
}
