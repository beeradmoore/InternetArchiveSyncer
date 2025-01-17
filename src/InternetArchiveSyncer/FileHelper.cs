using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace InternetArchiveSyncer;

internal class FileHelper
{
    public string GetMD5(string filename)
    {
        using (var fileStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(fileStream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
