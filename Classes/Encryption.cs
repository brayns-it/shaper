using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    public static partial class Functions
    {
        public static string CreateGuidString()
        {
            return Guid.NewGuid().ToString("n");
        }

        /// <summary>
        /// Returns HEX string of SHA256 hash of value
        /// </summary>
        public static string Hash(string value)
        {
            var sha = SHA256.Create();
            var shaKey = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var ret = "";
            foreach (byte b in shaKey)
                ret += b.ToString("X2");
            return ret;
        }

        public static string EncryptString(string value, string key)
        {
            var sha = SHA256.Create();
            var shaKey = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

            var aes = Aes.Create();
            aes.Key = shaKey;
            aes.IV = new byte[16];

            var aesVal = aes.EncryptEcb(Encoding.UTF8.GetBytes(value), PaddingMode.PKCS7);
            return Convert.ToBase64String(aesVal);
        }

        public static string DecryptString(string value, string key)
        {
            var sha = SHA256.Create();
            var shaKey = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

            var aes = Aes.Create();
            aes.Key = shaKey;
            aes.IV = new byte[16];

            var aesVal = aes.DecryptEcb(Convert.FromBase64String(value), PaddingMode.PKCS7);
            return Encoding.UTF8.GetString(aesVal);
        }
    }
}
