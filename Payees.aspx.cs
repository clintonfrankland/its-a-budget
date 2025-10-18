using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace ClintonFrankland
{
	public partial class Payees : System.Web.UI.Page
	{
        private ClintonFrankland _Master { get; set; }
        
		protected void Page_Load(object sender, EventArgs e)
		{
            ErrorLabel.Text = "";
            ErrorLabel.Visible = false;
            _Master = (ClintonFrankland)Master;
            if (!_Master.UserInfo.IsLoggedIn)
            {
                Response.Redirect("Login.aspx?Return=" + Request.Url.AbsolutePath, false);
                Context.ApplicationInstance.CompleteRequest();
            }
            else
            {
                try
                {
                    if (!IsPostBack)
                    {
                        ShowList();
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex);
                }
            }
        }
        protected void AccountDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ShowList();
        }

        private void ShowList()
        {
            MainMultiview.SetActiveView(ListView);
            var dt = _Master.Sql.GetDataTable(
                _Master.SiteInfo.DatabaseName,
                "dbo",
                "spcfGetPayees_020000",
                new NamedValue("userid", 3));
            if (dt.Rows.Count > 0)
            {
                PayeeRepeater.DataSource = dt;
                PayeeRepeater.DataBind();
            }
        }

        private void ShowError(Exception ex)
        {
            ErrorLabel.Text = ex.GetType().ToString() + " : " + ex.Message + "<br/>" + ex.StackTrace;
            this.ErrorLabel.Visible = true;
        }
    }
}