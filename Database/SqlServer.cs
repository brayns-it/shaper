using Microsoft.AspNetCore.Http.HttpResults;
using Brayns.Shaper.Fields;
using Brayns.Shaper.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
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
        private string DatabaseCollation { get; set; } = "";

        internal override void DatabaseInit()
        {
            DatabaseCollation = Query("SELECT collation_name FROM sys.databases WHERE name = DB_NAME()")[0].Value<string>("collation_name");
        }

        protected override List<string> GetTables()
        {
            var res = new List<string>();
            foreach (var row in Query("SELECT o.name FROM sysobjects o WHERE o.xtype = @p0", "U"))
                res.Add(row.Value<string>("name"));
            return res;
        }

        protected override void CompileTable(BaseTable table)
        {
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
                string collate = (f.Binary) ? " COLLATE Latin1_General_BIN " : " ";

                if (!f.ANSI) res += "n";

                if (f.Length == Fields.Text.MAX_LENGTH)
                    res += "varchar(max)" + collate + "NOT NULL";
                else
                    res += "varchar(" + f.Length.ToString() + ")" + collate + "NOT NULL";
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
                    c.name, c.is_nullable, c.collation_name 
                    FROM sys.objects o, sys.columns c, sys.types t
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

                            if (((string)row["typename"]! == "nvarchar") || ((string)row["typename"]! == "varchar"))
                                if (Convert.ToInt32(row["max_length"]) == -1)
                                    curDef += "(max)";
                                else
                                    curDef += "(" + (Convert.ToInt32(row["max_length"]) / 2).ToString() + ")";

                            else if ((string)row["typename"]! == "decimal")
                                curDef += "(" + Convert.ToInt32(row["precision"]).ToString() + "," + Convert.ToInt32(row["scale"]).ToString() + ")";

                            var collate = row.Value<string>("collation_name");
                            if ((collate.Length > 0) && (collate != DatabaseCollation))
                                curDef += " COLLATE " + collate;

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

                    bool hasDefault = true;
                    if (field.Type == FieldTypes.BLOB)
                        hasDefault = false;
                    if ((field.Type == FieldTypes.INTEGER) || (field.Type == FieldTypes.BIGINTEGER))
                        if (((Fields.IInteger)field).AutoIncrement)
                            hasDefault = false;

                    if (hasDefault)
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

                    if (hasDefault)
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

        public static string CreateConnectionString(string server, string database, string envName)
        {
            return "Data Source=" + server + ";Initial Catalog=" + database +
                ";Application Name=" + envName +
                ";Trust Server Certificate=true";
        }

        internal override string GetConnectionString()
        {
            string dsn = Application.Config.DatabaseConnection;
            if (!dsn.EndsWith("; ")) dsn += ";";

            if (Application.Config.DatabaseLogin.Length > 0)
            {
                dsn += "User ID=" + Application.Config.DatabaseLogin + ";";
                if (Application.Config.DatabasePassword.Length > 0)
                    dsn += "Password=" + Application.Config.DatabasePassword + ";";
            }
            else
            {
                dsn += "Integrated Security=true;";
            }

            dsn += "MultipleActiveResultSets=True;";

            return dsn;
        }

        protected override DbConnection GetConnection()
        {
            var conn = new SqlConnection(Dsn);
            conn.Open();
            return conn;
        }

        protected override DbCommand CreateCommand(string sql, params object[] args)
        {
            var cmd = Connection!.CreateCommand();
            if (Transaction == null) Transaction = Connection!.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            cmd.Transaction = Transaction;
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.Add(new SqlParameter("@p" + i.ToString(), args[i]));

            return cmd;
        }

        protected override object? FromSqlValue(BaseField f, object value)
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

        protected override object ToSqlValue(BaseField f, object? value)
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

        protected override string QuoteIdentifier(string name)
        {
            return "[" + name + "]";
        }

        protected override string GetParameterName(int number)
        {
            return "@p" + number.ToString();
        }

        protected override void OnFindSetAfterSelect(BaseTable table, ref string sql)
        {
            if (table.TableLock)
                sql += " WITH (UPDLOCK)";
        }

        protected override void OnInsertBeforeIdentityInsert(BaseTable table)
        {
            Execute("SET IDENTITY_INSERT [" + table.TableSqlName + "] ON");
        }

        protected override void OnInsertAfterIdentityInsert(BaseTable table)
        {
            Execute("SET IDENTITY_INSERT [" + table.TableSqlName + "] OFF");
        }

        protected override void OnInsertGetLastIdentity(BaseTable table, BaseField identity)
        {
            identity.Value = Query("SELECT @@IDENTITY AS [id]")[0]["id"];
        }

        protected override void SetVersionAfterExecute(BaseTable table)
        {
            table.TableVersion.Value = Query("SELECT CAST(@@DBTS AS bigint) [dbts]")[0].Value<long>("dbts");
        }

        protected override string GetTop(int limitRows)
        {
            return "TOP " + limitRows.ToString();
        }

        protected override string GetLimit(int limitRows)
        {
            return "";

            // FUTURE depends on SQL version
            // return "OFFSET 0 ROWS FETCH FIRST " + limitRows.ToString() + " ROWS ONLY";
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

    }
}
