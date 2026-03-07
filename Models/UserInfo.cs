namespace ClintonFrankland.Models;

public class UserInfo
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsLoggedIn { get; set; } = false;
    public int SiteId { get; set; }

    public UserInfo(int siteId = 0)
    {
        SiteId = siteId;
    }
}
