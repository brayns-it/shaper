namespace Brayns.Shaper.Objects
{
    public class Query : Unit
    {
        System.Data.Common.DbDataReader? _reader = null;

        public DbRow Row { get; private set; } = new();
        public string QueryText { get; set; } = "";
        public List<object> QueryParameters { get; init; } = new();
        
        internal Database.Database? QueryDatabase
        {
            get
            {
                return Session.Database;
            }
        }

        internal override void UnitInitialize()
        {
            UnitType = UnitTypes.QUERY;
        }

        public bool Read()
        {
            if (_reader == null)
                _reader = QueryDatabase!.ExecuteReader(QueryText, QueryParameters.ToArray());

            var row = QueryDatabase!.ReadRow(_reader);
            if (row != null)
            {
                Row = row;
                return true;
            }

            return false;
        }

        public T Value<T>(string columnName)
        {
            return Row.Value<T>(columnName);
        }
    }
}
