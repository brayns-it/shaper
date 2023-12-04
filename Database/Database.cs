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
    }

    public abstract class Database
    {
        internal int DatasetSize { get; set; } = 50;
        public abstract void Connect();
        public abstract void Disconnect();
        public abstract void Compile(BaseTable table, bool onlyCheck);
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
        public abstract void Rename(BaseTable table, object[] newPkValues);
        public abstract int Count(BaseTable table);
        public abstract int GetConnectionId();
        public abstract List<Dictionary<string, object>> FindFirst(BaseTable table);
        public abstract List<Dictionary<string, object>> FindLast(BaseTable table);
        public abstract List<Dictionary<string, object>> FindSet(BaseTable table, int? size = null, int? offset = null);
        public abstract List<Dictionary<string, object>> NextSet(BaseTable table);
        public abstract List<Dictionary<string, object>> Get(BaseTable table, object[] pkValues);
        public abstract void LoadRow(BaseTable table, Dictionary<string, object> row);
    }
}
