using ClintonFrankland.Properties;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.UI;


namespace ClintonFrankland
{

    public partial class Login : Page
    {

        private ClintonFrankland _master;
        private string _strReturn;

        public Login()
        {
            _master = (ClintonFrankland)Master;
            Load += Page_Load;
            Init += Login_Init;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            divWarning.Visible = false;
            if (!(Request["Return"] == null) && !string.IsNullOrEmpty(Request["Return"]))
            {
                _strReturn = Request["Return"];
                hidReturn.Value = _strReturn;
            }
            else if (!string.IsNullOrEmpty(hidReturn.Value))
            {
                _strReturn = hidReturn.Value;
            }
            else
            {
                _strReturn = _master.SiteInfo.BaseUrl;
                hidReturn.Value = _strReturn;
            }
            if (!(Request.Cookies["UserName"] == null))
            {
                var ck = HttpContext.Current.Request.Cookies["UserName"];
                if (ck.Value == "cfrankland")
                {
                    _master.UserInfo.DisplayName = "Clinton Frankland";
                    _master.UserInfo.UserName = "cfrankland";
                    _master.UserInfo.IsLoggedIn = true;
                }
            }
            if (_master.UserInfo.IsLoggedIn)
            {
                Session["UserInfo"] = _master.UserInfo;
                var ck = new HttpCookie("UserName");
                ck.Value = _master.UserInfo.UserName;
                ck.Expires = DateTime.Now.AddDays(30d);
                Response.Cookies.Add(ck);
                Response.Redirect(_strReturn, false);
            }
        }

        private string RandomString(int size, bool lowerCase)
        {
            var builder = new StringBuilder();
            var random = new Random();
            char ch;
            int i;
            var loopTo = size - 1;
            for (i = 0; i <= loopTo; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(26d * random.NextDouble() + 65d));
                builder.Append(ch);
            }
            if (lowerCase)
            {
                return builder.ToString().ToLower();
            }
            return builder.ToString();
        }

        private void btnSignin_Click(object sender, EventArgs e)
        {
            if (inputUsername.Text.ToLower().TrimEnd() == Properties.Settings.Default.LoginUser & inputPassword.Text.TrimEnd() == Properties.Settings.Default.LoginPassword)
            {
                _master.UserInfo.DisplayName = "Clinton Frankland";
                _master.UserInfo.UserName = "cfrankland";
                _master.UserInfo.IsLoggedIn = true;
            }
            if (_master.UserInfo.IsLoggedIn)
            {
                Session["UserInfo"] = _master.UserInfo;
                var ck = new HttpCookie("UserName");
                ck.Value = _master.UserInfo.UserName;
                ck.Expires = DateTime.Now.AddDays(30d);
                Response.Cookies.Add(ck);
                Response.Redirect(_strReturn, false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        private void Login_Init(object sender, EventArgs e)
        {
            _master = (ClintonFrankland)Master;
            _master.FormClass = "form-signin text-center";
        }

        public string EncryptPassword(string clearText)
        {
            string EncryptionKey = Settings.Default.EncryptionKey;
            var clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (var encryptor = Aes.Create())
            {
                var pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6E, 0x20, 0x4D, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                        clearText = Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            return clearText;
        }

        public string DecryptPassword(string cipherText)
        {
            string EncryptionKey = Settings.Default.EncryptionKey;
            var cipherBytes = Convert.FromBase64String(cipherText);
            using (var encryptor = Aes.Create())
            {
                var pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6E, 0x20, 0x4D, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

        public string EncryptPasswordBase64(string text)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(plainTextBytes);
        }

        public string DecryptPasswordBase64(string base64EncodedData)
        {

            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

    }
}