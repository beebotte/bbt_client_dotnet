using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Beebotte.API.Client.Net
{
    public static class Utilities
    {
        public static string GenerateHMACHash(string stringToSign, string secureKey)
        {
            var ae = new UTF8Encoding();
            var keyByte = ae.GetBytes(secureKey);
            var data = ae.GetBytes(stringToSign);
            var hmacsha1 = new HMACSHA1(keyByte);
            var digest = hmacsha1.ComputeHash(data);
            return Convert.ToBase64String(digest);
        }
    }
}
