using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Database
{
    public class DatabaseTypes 
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

    public abstract class Database
    {
        protected BaseTable? CompilingTable { get; set; }

        internal int DatasetSize { get; set; } = 50;
        internal DatabaseCompileMode CompileMode { get; set; } = DatabaseCompileMode.Normal;
        internal List<string> CompileResult { get; init; } = new();

        public abstract void Connect();
        public abstract void Connect(string dsn);
        public abstract void Disconnect();
        public abstract void Compile(BaseTable table);
        public abstract List<Dictionary<string, object>> Query(string sql, params object[] args);
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
        public abstract List<Dictionary<string, object>> FindFirst(BaseTable table);
        public abstract List<Dictionary<string, object>> FindLast(BaseTable table);
        public abstract List<Dictionary<string, object>> FindSet(BaseTable table, int? pageSize = null, int? offset = null);
        public abstract List<Dictionary<string, object>> NextSet(BaseTable table);
        public abstract List<Dictionary<string, object>> Get(BaseTable table, object[] pkValues);
        public abstract void LoadRow(BaseTable table, Dictionary<string, object> row);

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
