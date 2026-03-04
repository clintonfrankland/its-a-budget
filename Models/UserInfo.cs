namespace ClintonFrankland.Models;

public class UserInfo
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; } = false;
    public int SiteId { get; set; }

    public UserInfo(int siteId = 0)
    {
        SiteId = siteId;
    }
}
