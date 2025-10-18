using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;

namespace ClintonFrankland
{
    public partial class ClintonFrankland : MasterPage
    {
        public SqlProvider Sql { get; set; }
        public UserInfo UserInfo { get; set; }
        public PageInfo PageInfo { get; set; } = new PageInfo();
        public SiteInfo SiteInfo { get; set; }
        public string FormClass { get; set; } = "";

        public ClintonFrankland()
        {
            Init += ClintonFrankland_Init;
            Load += ClintonFrankland_Load;
            Unload += ClintonFrankland_Unload;
        }

        private void ClintonFrankland_Init(object sender, EventArgs e)
        {
            if (Application["SiteInfo"] == null)
            {
                SiteInfo = new SiteInfo("Clinton Frankland, RN", "https://clintonfrankland.com/", Properties.Settings.Default.DatabaseName, Properties.Settings.Default.SiteSqlString, 0, "fa fa-address-card");
                Application["SiteInfo"] = SiteInfo;
            }
            else
            {
                SiteInfo = (SiteInfo)Application["SiteInfo"];
            }
            UserInfo = Session["UserInfo"] != null ? (UserInfo)Session["UserInfo"] : new UserInfo(SiteInfo.SiteId);
            PageInfo.PageName = Request.Url.Segments.Last();

            Sql = new SqlProvider(Properties.Settings.Default.SiteSqlString);
            this.MenuPanel.Visible = UserInfo.IsLoggedIn;
            if (!UserInfo.IsLoggedIn & PageInfo.PageName.ToLower() != "login.aspx" & PageInfo.PageName.ToLower() != "default.aspx")
            {
                bool bolFound = false;
                foreach (string strPage in "Checkbook|Budget|Accounts".Split('|'))
                {
                    if ((strPage.ToLower() + ".aspx" ?? "") == (PageInfo.PageName.ToLower() ?? ""))
                        bolFound = true;
                }
                if (!bolFound)
                {
                    Response.Redirect("Login.aspx?Return=" + Request.Url.AbsolutePath, false);
                    return;
                }
            }
        }

        private void AddPageCss()
        {
            string strPage = PageInfo.PageName.Contains(".") ? PageInfo.PageName.Substring(0, PageInfo.PageName.IndexOf(".")) : PageInfo.PageName;
            if (FormClass.Length > 0)
                frmMain.Attributes.Add("class", FormClass);
            if (System.IO.File.Exists(Server.MapPath("~/Content/" + strPage + ".css")))
            {
                var link = new HtmlLink();
                link.Href = "/Content/" + strPage + ".css";
                link.Attributes.Add("rel", "stylesheet");
                link.Attributes.Add("type", "text/css");
                Page.Header.Controls.Add(link);
            }
        }

        private void UpdateLoginUserLink()
        {
            if (UserInfo.IsLoggedIn)
            {
                lnkLogin.Text = "<i class='fa fa-sign-in'></i>&nbsp;&nbsp;" + UserInfo.DisplayName;
            }
            else
            {
                lnkLogin.Text = "<i class='fa fa-sign-in'></i>&nbsp;&nbsp;Login";
            }
        }

        private void ClintonFrankland_Unload(object sender, EventArgs e)
        {
            try
            {
                Sql.Dispose();
            }
            catch (Exception ex)
            {
            }
        }

        public void lnkLogin_Click(object sender, EventArgs e)
        {
            if (UserInfo.IsLoggedIn)
            {
                UserInfo = new UserInfo(SiteInfo.SiteId);
                Session["UserInfo"] = UserInfo;
                var ck = new HttpCookie("UserName");
                ck.Value = "";
                ck.Expires = DateTime.Now.AddDays(2d);
                Response.Cookies.Add(ck);
            }
            Response.Redirect("Login.aspx?Return=" + Request.Url.AbsolutePath, false);
        }

        private void ClintonFrankland_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                AddPageCss();
                UpdateLoginUserLink();
            }

        }
    }
}