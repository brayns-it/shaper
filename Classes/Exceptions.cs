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

        public int ErrorCode { get; private set; } = 0;

        public Error(string text) : base(text)
        {
        }

        public Error(string text, params object[] args) : base(String.Format(text, args))
        {
        }

        public Error(int code, string text) : base(text)
        {
            ErrorCode = code;
        }

        public Error(int code, string text, params object[] args) : base(String.Format(text, args))
        {
            ErrorCode = code;
        }
    }
}
