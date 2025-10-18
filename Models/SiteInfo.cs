using System.Diagnostics;
using System.Reflection;

namespace ClintonFrankland
{
    public class SiteInfo
    {
        public int SiteId;
        public string SiteName;
        public string BaseUrl;
        public string DatabaseName;
        public string SiteSqlString;
        public string Icon;
        public string Version;
        public SiteInfo(string sitename, string baseurl, string databasename, string sitesqlstring, int siteid, string icon)
        {
            SiteName = sitename;
            BaseUrl = baseurl;
            DatabaseName = databasename;
            SiteSqlString = sitesqlstring;
            if (icon.Substring(0, 2).ToLower() == "fa")
                icon = "<i class='" + icon + "'></i>&nbsp;";
            Icon = icon;
            Version = GetVersion();
        }

        private static string GetVersion()
        {
            try
            {
                var asm = typeof(SiteInfo).Assembly;
                // Prefer file version (AssemblyFileVersion) when present
                var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                if (!string.IsNullOrEmpty(fvi.FileVersion))
                    return fvi.FileVersion;
                // Fallback to assembly version
                var ver = asm.GetName().Version;
                return ver != null ? ver.ToString() : "0.0.0.0";
            }
            catch
            {
                return "0.0.0.0";
            }
        }
    }
}