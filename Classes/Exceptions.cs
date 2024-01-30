using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Brayns.Shaper.Classes
{
    public class Error : Exception
    {
        public const int E_SYSTEM_IN_MAINTENANCE = 1;
        public const int E_RECORD_NOT_FOUND = 2;
        public const int E_UNAUTHORIZED = 3;
        public const int E_INVALID_ROUTE = 4;
        public const int E_SYSTEM_NOT_READY = 5;
        public const int E_INVALID_SESSION = 6;

        public int ErrorCode { get; protected set; } = 0;
        public string SourceId { get; protected set; } = "";

        public Error() : base("")
        {
        }

        public Error(string text) : base(text)
        {
        }

        public Error(int code, string text) : base(text)
        {
            ErrorCode = code;
        }

        public Error(int code, string sourceId, string text) : base(text)
        {
            ErrorCode = code;
            SourceId = sourceId;
        }

        public static Error FromException(int defaultCode, string defaultSourceId, Exception ex)
        {
            if (typeof(Error).IsAssignableFrom(ex.GetType()))
            {
                var err = (Error)ex;
                if (err.ErrorCode == 0) err.ErrorCode = defaultCode;
                if (err.SourceId.Length == 0) err.SourceId = defaultSourceId;
                return err;
            }
            else
            {
                return new Error(defaultCode, defaultSourceId, ex.Message);
            }
        }
    }
}
