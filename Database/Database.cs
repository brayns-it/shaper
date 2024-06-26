﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Database
{
    public class DatabaseTypes : OptList
    {
        public const int NONE = 0;

        [Label("SQL Server")]
        public const int SQLSERVER = 1;

        [Label("SQLite")]
        public const int SQLITE = 2;
    }

    public enum DatabaseCompileMode
    {
        Normal,
        Force,
        CheckOnly
    }

    public class DbRow : Dictionary<string, object>
    {
        public T Value<T>(string columnName)
        {
            object o = this[columnName];
            if (o == DBNull.Value) return default(T)!;
            return (T)Convert.ChangeType(o, typeof(T));
        }
    }

    public class DbTable : List<DbRow>
    {

    }

    public abstract class Database
    {
        protected BaseTable? CompilingTable { get; set; }

        internal int DatasetSize { get; set; } = 50;
        internal DatabaseCompileMode CompileMode { get; set; } = DatabaseCompileMode.Normal;
        internal List<string> CompileResult { get; init; } = new();
        internal List<string> CompiledTables { get; init; } = new();

        public abstract void Connect();
        public abstract void Connect(string dsn);
        public abstract void Disconnect();
        public abstract void Compile(BaseTable table);
        public abstract void DatabaseCheck();
        public abstract DbTable Query(string sql, params object[] args);
        public abstract int Execute(string sql, params object[] args);
        public abstract void Commit();
        public abstract void Rollback();
        public abstract bool IsEmpty(BaseTable table);
        public abstract void Insert(BaseTable table);
        public abstract void Modify(BaseTable table);
        public abstract void ModifyAll(BaseTable table, Fields.BaseField field);
        public abstract void Delete(BaseTable table);
        public abstract void DeleteAll(BaseTable table);
        public abstract void Rename(BaseTable table);
        public abstract int Count(BaseTable table);
        public abstract int GetConnectionId();
        public abstract DbTable FindFirst(BaseTable table);
        public abstract DbTable FindLast(BaseTable table);
        public abstract DbTable FindSet(BaseTable table, int? pageSize = null, int? offset = null);
        public abstract DbTable NextSet(BaseTable table);
        public abstract DbTable Get(BaseTable table, object[] pkValues);
        public abstract void LoadRow(BaseTable table, Dictionary<string, object> row);
        public abstract object ExecuteReader(string sql, params object[] args);
        public abstract DbRow? ReadRow(object reader);

        protected int CompileExec(string sql, bool disruptive, params object[] args)
        {
            switch(CompileMode)
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
            }

            return 0;
        }
    }
}
