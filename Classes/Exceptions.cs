using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

namespace Brayns.Shaper.Classes
{
    public class FormattedException
    {
        public Exception Exception { get; init; }
        public string Message { get; init; }
        public List<string> Trace { get; } = new();
        public Type Type { get; init; }
        public int ErrorCode { get; init; }
        public string SourceId { get; init; }

        public FormattedException(Exception ex)
        {
            while (true)
            {
                var st = new StackTrace(ex, true);
                var frames = st.GetFrames();
                foreach (var frame in frames)
                {
                    string? fn = frame.GetFileName();
                    if (fn != null)
                    {
                        FileInfo fi = new FileInfo(fn);
                        Trace.Add("in '" + fi.Name + "' line " + frame.GetFileLineNumber() + " method '" + frame.GetMethod()!.Name + "'");
                    }
                }

                if (ex.InnerException != null)
                    ex = ex.InnerException;
                else
                    break;
            }

            Exception = ex;
            Type = ex.GetType();
            Message = ex.Message;

            ErrorCode = 0;
            SourceId = "";

            if (typeof(Error).IsAssignableFrom(ex.GetType()))
            {
                ErrorCode = ((Error)ex).ErrorCode;
                SourceId = ((Error)ex).SourceId;
            }
        }
    }

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
