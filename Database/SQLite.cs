using Microsoft.AspNetCore.Http.HttpResults;
using Brayns.Shaper.Fields;
using Brayns.Shaper.Objects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
using System.Data.Common;
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
        static SQLite()
        {
            SQLitePCL.Batteries.Init();
        }

        protected override List<string> GetTables()
        {
            var res = new List<string>();
            foreach (var row in Query("SELECT[name] FROM sqlite_schema WHERE[type] = 'table'"))
                res.Add(row.Value<string>("name"));
            return res;
        }

        protected override void CompileTable(BaseTable table)
        {
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

        protected override string GetParameterName(int number)
        {
            return "$p" + number.ToString();
        }

        protected override string QuoteIdentifier(string name)
        {
            return "[" + name + "]";
        }

        public static string CreateConnectionString()
        {
            return "Data Source=" + Application.RootPath + "var/database.db";
        }

        internal override string GetConnectionString()
        {
            return Application.Config.DatabaseConnection;
        }

        internal override DbConnection GetConnection(string dsn)
        {
            var conn = new SqliteConnection(dsn);
            conn.Open();
            return conn;
        }

        internal void MemoryConnect()
        {
            Connection = new SqliteConnection("Data Source=:memory:");
            Connection.Open();
        }

        protected override DbCommand CreateCommand(DbConnection connection, string sql, params object[] args)
        {
            var cmd = connection!.CreateCommand();
            cmd.CommandText = sql;
            
            // sqlite currently not support transactions to avoid database lock

            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.Add(new SqliteParameter("$p" + i.ToString(), args[i]));

            return cmd;
        }

        protected override object? FromSqlValue(BaseField f, object value)
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

        protected override object ToSqlValue(BaseField f, object? value)
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

        protected override void OnInsertGetLastIdentity(BaseTable table, BaseField identity)
        {
            identity.Value = Query("SELECT last_insert_rowid() AS [id]")[0]["id"];
        }

        protected override string GetOffset(int offset, int first)
        {
            return " LIMIT " + first.ToString() + " OFFSET " + offset.ToString();
        }
    }
}
