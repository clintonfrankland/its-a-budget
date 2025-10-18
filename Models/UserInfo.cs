using System;
using System.Security.Cryptography;
using System.Text;


namespace ClintonFrankland
{

    public class UserInfo
    {
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public bool IsLoggedIn { get; set; }
        public UserInfo(int siteid)
        {
            UserName = "";
            DisplayName = "";
            IsLoggedIn = false;
        }

        private string GenerateSalt()
        {
            var rng = new RNGCryptoServiceProvider();
            var buff = new byte[33];
            rng.GetBytes(buff);
            return Convert.ToBase64String(buff);
        }

        private string HashedPassword(string password, string salt)
        {
            var bytes = Encoding.Unicode.GetBytes(password);
            var src = Encoding.Unicode.GetBytes(salt);
            var dst = new byte[src.Length + bytes.Length + 1];
            Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            Buffer.BlockCopy(bytes, 0, dst, src.Length, bytes.Length);
            var algorithm = HashAlgorithm.Create("SHA1");
            var inarray = algorithm.ComputeHash(dst);
            return Convert.ToBase64String(inarray);
        }

    }
}