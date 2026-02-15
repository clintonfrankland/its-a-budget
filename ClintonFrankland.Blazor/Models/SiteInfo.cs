using System.Diagnostics;

namespace ClintonFrankland.Models;

public class SiteInfo
{
    public int SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    public SiteInfo() { }

    public SiteInfo(string siteName, string baseUrl, string databaseName, int siteId, string icon)
    {
        SiteName = siteName;
        BaseUrl = baseUrl;
        DatabaseName = databaseName;
        SiteId = siteId;

        if (icon.Length >= 2 && icon.Substring(0, 2).ToLower() == "fa")
            Icon = $"<i class='{icon}'></i>&nbsp;";
        else
            Icon = icon;

        Version = GetVersion();
    }

    private static string GetVersion()
    {
        try
        {
            var asm = typeof(SiteInfo).Assembly;
            var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            if (!string.IsNullOrEmpty(fvi.FileVersion))
                return fvi.FileVersion;
            var ver = asm.GetName().Version;
            return ver?.ToString() ?? "0.0.0.0";
        }
        catch
        {
            return "0.0.0.0";
        }
    }
}
