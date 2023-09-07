using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    public static partial class Functions
    {
        public static string ToSqlName(string name)
        {
            var sqlname = "";
            foreach (char c in name)
            {
                if ((c >= 'A') && (c <= 'Z'))
                    sqlname += c;
                else if ((c >= 'a') && (c <= 'z'))
                    sqlname += c;
                else if ((c >= '0') && (c <= '9'))
                    sqlname += c;
                else if (new[] { '(', ')', '-', ' ' }.Contains(c)) 
                    sqlname += c;
                else
                    sqlname += '_';
            }
            return sqlname;
        }
    }
}
