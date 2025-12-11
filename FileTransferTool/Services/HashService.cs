using System.Security.Cryptography;
using System.Text;

namespace FileTransferTool.Services
{
    class HashService : IHashService
    {
        public string ComputeMd5(byte[] data)
        {
            using MD5 md5 = MD5.Create();
            return ConvertToHex(md5.ComputeHash(data));
        }

        public string ComputeSha256(string path)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return ConvertToHex(sha256.ComputeHash(stream));
        }

        public string ConvertToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
