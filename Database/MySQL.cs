using Microsoft.AspNetCore.Http.HttpResults;
using Brayns.Shaper.Fields;
using Brayns.Shaper.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using System.Data.Common;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Database
{
    public class MySQL : Database
    {
        private string DatabaseCollation { get; set; } = "";

        internal override void DatabaseInit()
        {
            DatabaseCollation = Query("SELECT DEFAULT_COLLATION_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE " +
                "SCHEMA_NAME = @p0", Connection!.Database)[0].Value<string>("DEFAULT_COLLATION_NAME");
        }

        protected override List<string> GetTables()
        {
            var res = new List<string>();
            foreach (var row in Query("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @p0", Connection!.Database))
                res.Add(row.Value<string>("TABLE_NAME"));
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
            var res = Query(@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = @p0 AND TABLE_NAME = @p1 AND INDEX_NAME = @p2
                ORDER BY SEQ_IN_INDEX",
                Connection!.Database, table.TableSqlName, key);

            var idx = new List<string>();
            foreach (var row in res)
                idx.Add((string)row["COLUMN_NAME"]!);

            return idx;
        }

        private List<string> GetPrimaryKey(BaseTable table)
        {
            var res = Query(@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                WHERE TABLE_SCHEMA = @p0 AND TABLE_NAME = @p1 AND CONSTRAINT_NAME = 'PRIMARY'
                ORDER BY ORDINAL_POSITION",
                Connection!.Database, table.TableSqlName);

            var pk = new List<string>();
            foreach (var row in res)
                pk.Add((string)row["COLUMN_NAME"]!);

            return pk;
        }

        private string GetFieldType(BaseField field)
        {
            string res = "";

            if ((field.Type == FieldTypes.CODE) || (field.Type == FieldTypes.TEXT))
            {
                var f = (Fields.Text)field;
                string collate = (f.Binary) ? " COLLATE utf8mb4_bin " : " ";

                if (f.Length == Fields.Text.MAX_LENGTH)
                    res += "text" + collate + "NOT NULL";
                else
                {
                    if (f.Table!.TablePrimaryKey.Contains(f) && (f.Length > 1024))
                        f.Length = 1024;

                    res += "varchar(" + f.Length.ToString() + ")" + collate + "NOT NULL";
                }
            }
            else if (field.Type == FieldTypes.INTEGER)
            {
                var f = (Fields.Integer)field;
                res += "int";
                if (f.AutoIncrement) res += " AUTO_INCREMENT PRIMARY KEY";
                res += " NOT NULL";
            }
            else if (field.Type == FieldTypes.BIGINTEGER)
            {
                var f = (Fields.BigInteger)field;
                res += "bigint";
                if (f.AutoIncrement) res += " AUTO_INCREMENT PRIMARY KEY";
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
                res += "varchar(40) NOT NULL";
            }
            else if (field.Type == FieldTypes.BLOB)
            {
                res += "blob NULL";
            }
            else if (field.Type == FieldTypes.TIMESTAMP)
            {
                res += "bigint NOT NULL";
            }
            else
            {
                throw new Error(Label("Unknown field type '{0}'", field.Type));
            }

            return res;
        }

        public override int GetConnectionId()
        {
            return ((MySqlConnection)Connection!).ServerThread;
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
                    var sql = "CREATE INDEX `" + k + "` ON " +
                        "`" + CompilingTable!.TableSqlName + "` (" +
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
                var sql = "ALTER TABLE `" + CompilingTable!.TableSqlName + "` " +
                    "ADD PRIMARY KEY (" +
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
                var sql = "DROP INDEX `" + row["name"] + "` ON `" + CompilingTable!.TableSqlName + "`";
                CompileExec(sql, false);
            }
        }

        private void DropIndex(string key)
        {
            var res = Query(@"SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE 
                TABLE_SCHEMA = @p0 AND TABLE_NAME = @p1 AND INDEX_NAME = @p2",
                Connection!.Database, CompilingTable!.TableSqlName, key);

            if (res.Count == 0)
                return;

            var sql = "DROP INDEX `" + res[0]["INDEX_NAME"] + "` ON `" + CompilingTable!.TableSqlName + "`";
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
            if (Query("SELECT NULL FROM INFORMATION_SCHEMA.TABLES WHERE (TABLE_SCHEMA = @p0) AND (TABLE_NAME = @p1)",
                Connection!.Database, CompilingTable!.TableSqlName).Count > 0)
            {
                var res = Query(@"SELECT EXTRA, CHARACTER_MAXIMUM_LENGTH, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE, 
                    COLUMN_NAME, IS_NULLABLE, COLLATION_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE (TABLE_SCHEMA = @p0) AND (TABLE_NAME = @p1)",
                    Connection!.Database, CompilingTable!.TableSqlName);

                var toDelete = new List<string>();
                var toAdd = new List<Fields.BaseField>();
                var toChange = new List<Fields.BaseField>();
                var curPk = GetPrimaryKey(CompilingTable!);

                foreach (var field in CompilingTable!.UnitFields)
                {
                    bool ok = false;

                    foreach (var row in res)
                    {
                        if ((string)row["COLUMN_NAME"]! == field.SqlName)
                        {
                            var newDef = GetFieldType(field);

                            var curDef = (string)row["DATA_TYPE"]!;

                            if ((string)row["DATA_TYPE"]! == "varchar")
                                curDef += "(" + row["CHARACTER_MAXIMUM_LENGTH"].ToString() + ")";

                            else if ((string)row["DATA_TYPE"]! == "decimal")
                                curDef += "(" + Convert.ToInt32(row["NUMERIC_PRECISION"]).ToString() + "," + Convert.ToInt32(row["NUMERIC_SCALE"]).ToString() + ")";

                            var collate = row.Value<string>("COLLATION_NAME");
                            if ((collate.Length > 0) && (collate != DatabaseCollation))
                                curDef += " COLLATE " + collate;

                            if (row["EXTRA"].ToString()!.Contains("AUTO_INCREMENT", StringComparison.OrdinalIgnoreCase))
                                curDef += " AUTO_INCREMENT PRIMARY KEY";

                            if (row["IS_NULLABLE"].ToString()!.Equals("NO", StringComparison.OrdinalIgnoreCase))
                                curDef += " NOT NULL";
                            else
                                curDef += " NULL";

                            if (!newDef.Equals(curDef, StringComparison.OrdinalIgnoreCase))
                            {
                                if ((field.Type == FieldTypes.TEXT) || (field.Type == FieldTypes.CODE))
                                {
                                    var f = (Fields.Text)field;
                                    if ((f.Length == Fields.Text.MAX_LENGTH) || (f.Length > (Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]))))
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
                        if ((string)row["COLUMN_NAME"]! == field.SqlName)
                        {
                            ok = true;
                            break;
                        }
                    }

                    if (!ok)
                        toDelete.Add((string)row["COLUMN_NAME"]!);
                }

                foreach (string fn in toDelete)
                {
                    if (curPk.Contains(fn))
                        DropPrimaryKey();

                    DropIndexByColumn(fn);

                    var sql = "ALTER TABLE `" + CompilingTable.TableSqlName + "` " +
                        "DROP COLUMN `" + fn + "`";

                    CompileExec(sql, true);
                }

                foreach (BaseField field in toAdd)
                {
                    var sql = "ALTER TABLE `" + CompilingTable!.TableSqlName + "` ADD " +
                        "`" + field.SqlName + "` " + GetFieldType(field);

                    bool hasDefault = true;
                    if (field.Type == FieldTypes.BLOB)
                        hasDefault = false;
                    if ((field.Type == FieldTypes.INTEGER) || (field.Type == FieldTypes.BIGINTEGER))
                        if (((Fields.IInteger)field).AutoIncrement)
                            hasDefault = false;

                    if (hasDefault)
                    {
                        sql += " DEFAULT ";

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
                        sql = "ALTER TABLE `" + CompilingTable!.TableSqlName + "` " +
                            "ALTER `" + field.SqlName + "` DROP DEFAULT";

                        CompileExec(sql, false);
                    }
                }

                foreach (BaseField field in toChange)
                {
                    if (curPk.Contains(field.SqlName))
                        DropPrimaryKey();

                    var sql = "ALTER TABLE `" + CompilingTable!.TableSqlName + "` " +
                        "CHANGE COLUMN `" + field.SqlName + "` `" + field.SqlName + "`" +
                        GetFieldType(field);

                    CompileExec(sql, false);
                }
            }
            else
            {
                var sql = "CREATE TABLE `" + CompilingTable!.TableSqlName + "` (";

                bool comma = false;
                foreach (BaseField field in CompilingTable!.UnitFields)
                {
                    if (comma) sql += ", ";
                    comma = true;
                    sql += "`" + field.SqlName + "` " + GetFieldType(field);
                }
                sql += ")";

                CompileExec(sql, false);
            }
        }

        public static string CreateConnectionString(string server, string database)
        {
            return "Server=" + server + ";Database=" + database;
        }

        internal override string GetConnectionString()
        {
            string dsn = Application.Config.DatabaseConnection;
            if (!dsn.EndsWith("; ")) dsn += ";";

            dsn += "UID=" + Application.Config.DatabaseLogin + ";";
            if (Application.Config.DatabasePassword.Length > 0)
                dsn += "PWD=" + Application.Config.DatabasePassword + ";";

            return dsn;
        }

        protected override DbConnection GetConnection()
        {
            var conn = new MySqlConnection(Dsn);
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
                cmd.Parameters.Add(new MySqlParameter("@p" + i.ToString(), args[i]));

            return cmd;
        }

        protected override object? FromSqlValue(BaseField f, object value)
        {
            if (f.Type == FieldTypes.TIMESTAMP)
                return value;

            if ((f.Type == FieldTypes.CODE) || (f.Type == FieldTypes.TEXT))
                return value;

            if ((f.Type == FieldTypes.INTEGER) || (f.Type == FieldTypes.BIGINTEGER))
                return value;

            if (f.Type == FieldTypes.DECIMAL)
                return value;

            if (f.Type == FieldTypes.GUID)
                return System.Guid.Parse(value.ToString()!);

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
                return value!;

            if ((f.Type == FieldTypes.CODE) || (f.Type == FieldTypes.TEXT))
                return value!;

            if ((f.Type == FieldTypes.INTEGER) || (f.Type == FieldTypes.BIGINTEGER))
                return value!;

            if (f.Type == FieldTypes.DECIMAL)
                return value!;

            if (f.Type == FieldTypes.GUID)
                return value!.ToString()!;

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
            return "`" + name + "`";
        }

        protected override string GetParameterName(int number)
        {
            return "@p" + number.ToString();
        }

        protected override void OnFindSetAfterSelect(BaseTable table, ref string sql)
        {
            if (table.TableLock || table._lockOnce)
            {
                table._lockOnce = false;
                sql += " FOR UPDATE";
            }
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
            identity.Value = Query("SELECT LAST_INSERT_ID() AS `id`")[0]["id"];
        }

        protected override string GetTop(int limitRows)
        {
            return "";
        }

        protected override string GetLimit(int limitRows)
        {
            return "LIMIT " + limitRows.ToString();

            // FUTURE depends on SQL version
            // return "OFFSET 0 ROWS FETCH FIRST " + limitRows.ToString() + " ROWS ONLY";
        }

        protected override bool OnInsertTimestamp(BaseTable table, Timestamp timestamp)
        {
            timestamp.Value = 1;
            return true;
        }

        protected override bool OnModifyTimestamp(BaseTable table, Timestamp timestamp)
        {
            timestamp.Value += 1;
            return true;
        }
    }
}
