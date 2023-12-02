using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    public class Config
    {
        public bool Ready { get; set; }
        public int? DatabaseType { get; set; }
        public string? DatabaseConnection { get; set; }
        public string? DatabaseLogin { get; set; }
        public string? DatabasePassword { get; set; }

        public bool EncryptPlainPasswords()
        {
            bool ret = false;
            if ((DatabasePassword != null) && DatabasePassword.StartsWith("plain:"))
            {
                DatabasePassword = Functions.EncryptString(DatabasePassword.Substring(6), "DatabasePassword");
                ret = true;
            }
            return ret;
        }
    }
}
