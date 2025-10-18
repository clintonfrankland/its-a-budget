

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
        public SiteInfo(string sitename, string baseurl, string databasename, string sitesqlstring, int siteid, string icon)
        {
            SiteName = sitename;
            BaseUrl = baseurl;
            DatabaseName = databasename;
            SiteSqlString = sitesqlstring;
            if (icon.Substring(0, 2).ToLower() == "fa")
                icon = "<i class='" + icon + "'></i>&nbsp;";
            Icon = icon;
        }
    }
}