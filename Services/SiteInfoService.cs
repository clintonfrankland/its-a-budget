using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public class SiteInfoService
{
    private readonly IConfiguration _configuration;
    private SiteInfo? _siteInfo;

    public SiteInfoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SiteInfo SiteInfo
    {
        get
        {
            if (_siteInfo == null)
            {
                _siteInfo = new SiteInfo(
                    _configuration["AppSettings:SiteName"] ?? "It's a Budget",
                    _configuration["AppSettings:BaseUrl"] ?? "/",
                    _configuration["AppSettings:DatabaseName"] ?? "",
                    0,
                    _configuration["AppSettings:SiteIcon"] ?? "fa fa-address-card"
                );
            }
            return _siteInfo;
        }
    }

    public string DatabaseName => _configuration["AppSettings:DatabaseName"] ?? "";

    public int DefaultUserId => _configuration.GetValue<int>("AppSettings:DefaultUserId", 3);
}
