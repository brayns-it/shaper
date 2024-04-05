using Microsoft.AspNetCore.Http.HttpResults;
using Brayns.Shaper.Fields;
using Brayns.Shaper.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Database
{
    public class SqlServer : Database
    {
        string _dsn = "";
        SqlConnection? _connection;
        SqlTransaction? _transaction;
        Dictionary<SqlDataReader, SqlConnection> _readerConns = new();

        public override void Compile(BaseTable table)
        {
            if (table.TablePrimaryKey.Count == 0)
                throw table.ErrorNoPrimaryKey();

            CompilingTable = table;

            ProcessTable();
            ProcessPrimaryKey();
            ProcessIndexes();
        }

        private List<string> GetIndex(BaseTable table, string key)
        {
            var res = Query(@"SELECT c.name FROM sys.objects o, sys.indexes i, sys.index_columns x, sys.columns c 
                WHERE (o.name = @p0) AND (o.type = @p1) AND (o.object_id = i.object_id) AND (i.name = @p2) AND
                (x.object_id = o.object_id) AND (x.index_id = i.index_id) AND (c.object_id = o.object_id) AND
                (c.column_id = x.column_id) ORDER BY x.key_ordinal",
                table.TableSqlName, "U", key);

            var idx = new List<string>();
            foreach (var row in res)
                idx.Add((string)row["name"]!);

            return idx;
        }

        private List<string> GetPrimaryKey(BaseTable table)
        {
            var res = Query(@"SELECT c.name FROM sys.objects o, sys.indexes i, sys.index_columns x, sys.columns c 
                WHERE (o.name = @p0) AND (o.type = @p1) AND (o.object_id = i.object_id) AND (i.is_primary_key = 1) AND
                (x.object_id = o.object_id) AND (x.index_id = i.index_id) AND (c.object_id = o.object_id) AND
                (c.column_id = x.column_id) ORDER BY x.key_ordinal",
                table.TableSqlName, "U");

            var pk = new List<string>();
            foreach (var row in res)
                pk.Add((string)row["name"]!);

            return pk;
        }

        private string GetFieldType(BaseField field)
        {
            string res = "";

            if ((field.Type == FieldTypes.CODE) || (field.Type == FieldTypes.TEXT))
            {
                var f = (Fields.Text)field;
                if (f.Length == Fields.Text.MAX_LENGTH)
                    res += "nvarchar(max) NOT NULL";
                else
                    res += "nvarchar(" + f.Length.ToString() + ") NOT NULL";
            }
            else if (field.Type == FieldTypes.INTEGER)
            {
                var f = (Fields.Integer)field;
                res += "int";
                if (f.AutoIncrement) res += " IDENTITY(1,1)";
                res += " NOT NULL";
            }
            else if (field.Type == FieldTypes.BIGINTEGER)
            {
                var f = (Fields.BigInteger)field;
                res += "bigint";
                if (f.AutoIncrement) res += " IDENTITY(1,1)";
                res += " NOT NULL";
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
                res += "uniqueidentifier NOT NULL";
            }
            else if (field.Type == FieldTypes.BLOB)
            {
                res += "varbinary(max) NULL";
            }
            else if (field.Type == FieldTypes.TIMESTAMP)
            {
                res += "timestamp NOT NULL";
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
            return Convert.ToInt32(Query("SELECT @@SPID [spid]")[0]["spid"]!);
        }

        private void ProcessIndexes()
        {
            foreach (string k in CompilingTable!.TableIndexes.Keys)
            {
                var curIdx = String.Join(", ", GetIndex(CompilingTable!, k));
                var newIdx = String.Join(", ", CompilingTable!.TableIndexes[k].Select(f => f.SqlName));

                if (curIdx == newIdx)
                    continue;

                if (curIdx.Length > 0)
                    DropIndex(k);

                if (newIdx.Length > 0)
                {
                    var sql = "CREATE INDEX [" + k + "] ON " +
                        "[" + CompilingTable!.TableSqlName + "] (" +
                        ListFields(CompilingTable!.TableIndexes[k]) +
                        ")";

                    CompileExec(sql, false);
                }
            }
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
                var sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] " +
                    "ADD CONSTRAINT [" + CompilingTable!.TableSqlName + "$PK] " +
                    "PRIMARY KEY (" +
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

        private void DropIndexByColumn(string colName)
        {
            var res = Query(@"SELECT i.name FROM sys.objects o, sys.indexes i, sys.index_columns x, sys.columns c 
                WHERE (o.name = @p0) AND (o.type = @p1) AND (o.object_id = i.object_id) AND (i.is_primary_key = 0) AND
                (c.name = @p2) AND
                (x.object_id = o.object_id) AND (x.index_id = i.index_id) AND (c.object_id = o.object_id) AND
                (c.column_id = x.column_id) ORDER BY x.key_ordinal",
                CompilingTable!.TableSqlName, "U", colName);

            foreach (var row in res)
            {
                var sql = "DROP INDEX [" + row["name"] + "] ON [" + CompilingTable!.TableSqlName + "]";
                CompileExec(sql, false);
            }
        }

        private void DropIndex(string key)
        {
            var res = Query(@"SELECT i.[name] FROM sys.objects o, sys.indexes i
                WHERE (o.name = @p0) AND (o.type = @p1) AND (o.object_id = i.object_id) AND
                (i.name = @p2)",
                CompilingTable!.TableSqlName, "U", key);

            if (res.Count == 0)
                return;

            var sql = "DROP INDEX [" + res[0]["name"] + "] ON [" + CompilingTable!.TableSqlName + "]";
            CompileExec(sql, false);
        }

        private void DropPrimaryKey()
        {
            var res = Query(@"SELECT i.[name] FROM sys.objects o, sys.indexes i
                WHERE (o.name = @p0) AND (o.type = @p1) AND (o.object_id = i.object_id) AND
                (i.is_primary_key = 1)",
                CompilingTable!.TableSqlName, "U");

            if (res.Count == 0)
                return;

            var sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] " +
                "DROP CONSTRAINT [" + res[0]["name"] + "]";

            CompileExec(sql, false);
        }

        private void ProcessTable()
        {
            if (Query("SELECT TOP 1 NULL FROM sysobjects WHERE (xtype = @p0) AND (name = @p1)",
                "U", CompilingTable!.TableSqlName).Count > 0)
            {
                var res = Query(@"SELECT c.is_identity, c.max_length, t.name AS typename, c.precision, c.scale, 
                    c.name, c.is_nullable FROM sys.objects o, sys.columns c, sys.types t 
                    WHERE (o.name = @p0) AND (o.type = @p1) AND (c.object_id = o.object_id) AND 
                    (c.system_type_id = t.system_type_id) AND (c.user_type_id = t.user_type_id)",
                    CompilingTable!.TableSqlName,
                    "U");

                var toDelete = new List<string>();
                var toAdd = new List<Fields.BaseField>();
                var toChange = new List<Fields.BaseField>();
                var curPk = GetPrimaryKey(CompilingTable!);

                foreach (var field in CompilingTable!.UnitFields)
                {
                    bool ok = false;

                    foreach (var row in res)
                    {
                        if ((string)row["name"]! == field.SqlName)
                        {
                            var newDef = GetFieldType(field);

                            var curDef = (string)row["typename"]!;

                            if ((string)row["typename"]! == "nvarchar")
                                if (Convert.ToInt32(row["max_length"]) == -1)
                                    curDef += "(max)";
                                else
                                    curDef += "(" + (Convert.ToInt32(row["max_length"]) / 2).ToString() + ")";

                            else if ((string)row["typename"]! == "decimal")
                                curDef += "(" + Convert.ToInt32(row["precision"]).ToString() + "," + Convert.ToInt32(row["scale"]).ToString() + ")";

                            if (Convert.ToInt32(row["is_identity"]) == 1)
                                curDef += " IDENTITY(1,1)";

                            if (Convert.ToInt32(row["is_nullable"]) == 0)
                                curDef += " NOT NULL";
                            else
                                curDef += " NULL";

                            if (!newDef.Equals(curDef, StringComparison.OrdinalIgnoreCase))
                            {
                                if ((field.Type == FieldTypes.TEXT) || (field.Type == FieldTypes.CODE))
                                {
                                    var f = (Fields.Text)field;
                                    if ((f.Length == Fields.Text.MAX_LENGTH) || (f.Length > (Convert.ToInt32(row["max_length"]) / 2)))
                                        toChange.Add(field);
                                    else
                                    {
                                        toDelete.Add(field.SqlName);
                                        toAdd.Add(field);
                                    }
                                }
                                else
                                {
                                    toDelete.Add(field.SqlName);
                                    toAdd.Add(field);
                                }
                            }

                            ok = true;
                            break;
                        }
                    }

                    if (!ok)
                        toAdd.Add(field);
                }

                foreach (var row in res)
                {
                    bool ok = false;

                    foreach (var field in CompilingTable!.UnitFields)
                    {
                        if ((string)row["name"]! == field.SqlName)
                        {
                            ok = true;
                            break;
                        }
                    }

                    if (!ok)
                        toDelete.Add((string)row["name"]!);
                }

                if (toDelete.Count > 0)
                {

                    foreach (string fn in toDelete)
                    {
                        if (curPk.Contains(fn))
                            DropPrimaryKey();

                        DropIndexByColumn(fn);

                        var sql = "ALTER TABLE [" + CompilingTable.TableSqlName + "] " +
                            "DROP COLUMN [" + fn + "]";

                        CompileExec(sql, true);
                    }
                }

                foreach (BaseField field in toAdd)
                {
                    var sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] ADD " +
                        "[" + field.SqlName + "] " + GetFieldType(field);

                    if (field.Type != FieldTypes.BLOB)
                    {
                        sql += " CONSTRAINT [" + field.SqlName + "$DEF] DEFAULT ";

                        if ((field.Type == FieldTypes.CODE) || (field.Type == FieldTypes.TEXT))
                            sql += "''";
                        else if ((field.Type == FieldTypes.INTEGER) || (field.Type == FieldTypes.BIGINTEGER))
                            sql += "0";
                        else if ((field.Type == FieldTypes.DECIMAL) || (field.Type == FieldTypes.BOOLEAN))
                            sql += "0";
                        else if (field.Type == FieldTypes.OPTION)
                            sql += "0";
                        else if (field.Type == FieldTypes.GUID)
                            sql += "'00000000-0000-0000-0000-000000000000'";
                        else if ((field.Type == FieldTypes.DATE) || (field.Type == FieldTypes.DATETIME))
                            sql += "'17530101'";
                        else if (field.Type == FieldTypes.TIME)
                            sql += "'17530101'";
                    }

                    CompileExec(sql, false);

                    if (field.Type != FieldTypes.BLOB)
                    {
                        sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] " +
                            "DROP CONSTRAINT [" + field.SqlName + "$DEF]";

                        CompileExec(sql, false);
                    }
                }

                foreach (BaseField field in toChange)
                {
                    if (curPk.Contains(field.SqlName))
                        DropPrimaryKey();

                    var sql = "ALTER TABLE [" + CompilingTable!.TableSqlName + "] " +
                        "ALTER COLUMN [" + field.SqlName + "] " +
                        GetFieldType(field);

                    CompileExec(sql, false);
                }
            }
            else
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
            _transaction!.Commit();
            _transaction = _connection!.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        }

        public override void Rollback()
        {
            _transaction!.Rollback();
            _transaction = _connection!.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        }

        public static string GetConnectionString(string server, string database, string envName)
        {
            return "Data Source=" + server + ";Initial Catalog=" + database +
                ";Application Name=" + envName +
                ";Trust Server Certificate=true";
        }

        public override void Connect()
        {
            string dsn = Application.Config.DatabaseConnection;
            if (!dsn.EndsWith("; ")) dsn += ";";

            if (Application.Config.DatabaseLogin.Length > 0)
            {
                dsn += "User ID=" + Application.Config.DatabaseLogin + ";";
                if (Application.Config.DatabasePassword.Length > 0)
                    dsn += "Password=" + Application.Config.DatabasePassword;
            }
            else
            {
                dsn += "Integrated Security=true;";
            }
            Connect(dsn);
        }

        public override void Connect(string dsn)
        {
            _dsn = dsn;
            _connection = new(dsn);
            _connection.Open();
            _transaction = _connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        }

        private SqlCommand CreateCommand(string sql, params object[] args)
        {
            var cmd = _connection!.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i.ToString(), args[i]);

            return cmd;
        }

        public override object ExecuteReader(string sql, params object[] args)
        {
            var conn = new SqlConnection(_dsn);
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
            var rdr = (SqlDataReader)reader;
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

        public override List<Dictionary<string, object>> Query(string sql, params object[] args)
        {
            var res = new List<Dictionary<string, object>>();
            Dictionary<string, object>? row;

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
                return TimestampToLong((byte[])value!);

            if ((f.Type == FieldTypes.CODE) || (f.Type == FieldTypes.TEXT))
                return value;

            if ((f.Type == FieldTypes.INTEGER) || (f.Type == FieldTypes.BIGINTEGER))
                return value;

            if (f.Type == FieldTypes.DECIMAL)
                return value;

            if (f.Type == FieldTypes.GUID)
                return value;

            if (f.Type == FieldTypes.OPTION)
                return value;

            if (f.Type == FieldTypes.BOOLEAN)
                return Convert.ToInt32(value) == 1;

            if (f.Type == FieldTypes.DATETIME)
            {
                var dt = (System.DateTime)value;
                if (dt == new System.DateTime(1753, 1, 1))
                    return System.DateTime.MinValue;
                else
                    return dt.ToLocalTime();
            }

            if (f.Type == FieldTypes.DATE)
            {
                var dt = (System.DateTime)value;
                if (dt == new System.DateTime(1753, 1, 1))
                    return System.DateTime.MinValue;
                else
                    return new System.DateTime(dt.Year, dt.Month, dt.Day);
            }

            if (f.Type == FieldTypes.TIME)
            {
                var dt = (System.DateTime)value;
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
                return LongToTimestamp((long)value!);

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

                sql += "([" + f.SqlName + "] = @p" + pars.Count + ")";

                if (useXvalues)
                    pars.Add(ToSqlValue(f, f.XValue));
                else
                    pars.Add(ToSqlValue(f, f.Value));
            }

            if (withTimestamp)
            {
                sql += " AND ([timestamp] <= @p" + pars.Count + ")";
                pars.Add(ToSqlValue(table.TableVersion, table.TableVersion.Value));
            }

            return sql;
        }

        private string FilterToSql(FieldFilter ff, List<object> pars)
        {
            string sql = "";
            if (ff.Type == FilterType.Equal)
            {
                sql += "[" + ff.Field.SqlName + "] = @p" + pars.Count;
                pars.Add(ToSqlValue(ff.Field, ff.Value));
            }
            else if (ff.Type == FilterType.Range)
            {
                sql += "[" + ff.Field.SqlName + "] BETWEEN @p" + pars.Count + " AND @p" + (pars.Count + 1);
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
                    return "[" + ff.Field.SqlName + "]" + m.Groups[2].Value + "@p" + n;
                });

                sql += expr;
            }
            if (sql.Length > 0)
                sql = "(" + sql + ")";
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
            var sql = "SELECT TOP 1 NULL [ne] FROM [" + table.TableSqlName + "]";

            var where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

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

            foreach (BaseField field in table.UnitFields)
            {
                if (field.Type == FieldTypes.TIMESTAMP)
                    continue;

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
                places.Add("@p" + pars.Count);
                pars.Add(ToSqlValue(field, field.Value));
            }

            var sql = "INSERT INTO [" + table.TableSqlName + "] (" +
                ListFields(fields) +
                ") VALUES (" +
                String.Join(", ", places) +
                ")";

            if (identityInsert)
                Execute("SET IDENTITY_INSERT [" + table.TableSqlName + "] ON");

            Execute(sql, pars.ToArray());
            SetVersion(table);

            if (identityInsert)
                Execute("SET IDENTITY_INSERT [" + table.TableSqlName + "] OFF");

            if (identity != null)
                identity.Value = Query("SELECT @@IDENTITY AS [id]")[0]["id"];

        }

        private void SetVersion(BaseTable table)
        {
            table.TableVersion.Value = (long)Query("SELECT CAST(@@DBTS AS bigint) [dbts]")[0]["dbts"]!;
        }

        private List<Dictionary<string, object>> FindSet(BaseTable table, int? pageSize, int? offset, bool nextSet, bool? ascending, object[]? pkValues)
        {
            List<object> pars = new();
            ascending = ascending ?? table.TableAscending;

            var sql = "SELECT " + ListFields(table.UnitFields) + " FROM [" + table.TableSqlName + "]";
            if (table.TableLock)
                sql += " WITH (UPDLOCK)";

            var where = new List<string>();
            if (pkValues != null)
            {
                int i = 0;
                foreach (BaseField f in table.TablePrimaryKey)
                {
                    where.Add("([" + f.SqlName + "] = @p" + pars.Count + ")");
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

                            ws.Add("([" + f.SqlName + "] " + op + " @p" + pars.Count + ")");
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

                sql += " OFFSET " + offset.ToString() + " ROWS FETCH FIRST " + pageSize.ToString() + " ROWS ONLY";
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
            return (int)res[0]["c"]!;
        }

        public override List<Dictionary<string, object>> FindFirst(BaseTable table)
        {
            return FindSet(table, 1, 0, false, null, null);
        }

        public override List<Dictionary<string, object>> FindLast(BaseTable table)
        {
            return FindSet(table, 1, 0, false, !(table.TableAscending ^ false), null);
        }

        public override List<Dictionary<string, object>> FindSet(BaseTable table, int? pageSize = null, int? offset = null)
        {
            return FindSet(table, pageSize, offset, false, null, null);
        }

        public override List<Dictionary<string, object>> NextSet(BaseTable table)
        {
            return FindSet(table, null, null, true, null, null);
        }

        public override List<Dictionary<string, object>> Get(BaseTable table, object[] pkValues)
        {
            return FindSet(table, null, null, false, null, pkValues);
        }

        public override void LoadRow(BaseTable table, Dictionary<string, object> row)
        {
            foreach (BaseField f in table.UnitFields)
                f.Value = FromSqlValue(f, row[f.SqlName]);
        }

        private byte[] LongToTimestamp(long val)
        {
            byte[] res = new byte[8];
            res[7] = Convert.ToByte(val & 0xFF);
            res[6] = Convert.ToByte((val >> 8) & 0xFF);
            res[5] = Convert.ToByte((val >> 16) & 0xFF);
            res[4] = Convert.ToByte((val >> 24) & 0xFF);
            res[3] = Convert.ToByte((val >> 32) & 0xFF);
            res[2] = Convert.ToByte((val >> 40) & 0xFF);
            res[1] = Convert.ToByte((val >> 48) & 0xFF);
            res[0] = Convert.ToByte((val >> 56) & 0xFF);
            return res;
        }

        private long TimestampToLong(byte[] buf)
        {
            long res = buf[7] +
                       (buf[6] << 8) +
                       (buf[5] << 16) +
                       (buf[4] << 24) +
                       (buf[3] << 32) +
                       (buf[2] << 40) +
                       (buf[1] << 48) +
                       (buf[0] << 56);

            return res;
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
                "[" + field.SqlName + "] = @p0";
            pars.Add(ToSqlValue(field, field.Value));

            List<string> where = GetWhere(table, pars);
            if (where.Count > 0)
                sql += " WHERE " + String.Join(" AND ", where);

            Execute(sql, pars.ToArray());
            SetVersion(table);
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

                sql += "[" + f.SqlName + "] = @p" + pars.Count;
                pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars, true, true);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            SetVersion(table);
        }

        public override void Modify(BaseTable table)
        {
            List<object> pars = new();

            List<BaseField> fields = new();
            foreach (BaseField f in table.UnitFields)
            {
                if (f.Type == FieldTypes.TIMESTAMP)
                    continue;
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

                sql += "[" + f.SqlName + "] = @p" + pars.Count;
                pars.Add(ToSqlValue(f, f.Value));
            }

            sql += " WHERE ";
            sql += GetWherePrimaryKey(table, pars);

            int a = Execute(sql, pars.ToArray());
            if (a != 1)
                throw table.ErrorConcurrency();

            SetVersion(table);
        }
    }
}
