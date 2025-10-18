using ClintonFrankland.Properties;
using System;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace ClintonFrankland
{
    public partial class Accounts1 : Page
    {
        private ClintonFrankland _Master { get; set; }
        private decimal _decBalance = 0m;
        private decimal _decPayment = 0m;

        public Accounts1()
        {
            Load += Page_Load;
        }

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

        private void ShowError(Exception ex)
        {
            ErrorLabel.Text = ex.GetType().ToString() + " : " + ex.Message + "<br/>" + ex.StackTrace;
            this.ErrorLabel.Visible = true;
        }

        private void ShowList()
        {
            MainMultiview.SetActiveView(ListView);
            var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetAccounts", new NamedValue("userid", 3), new NamedValue("accounttype", -1));



            if (dt.Rows.Count > 0)
            {
                rprAccounts.DataSource = dt;
                rprAccounts.DataBind();
                //lblTotalPayment.Text = _decPayment.ToString("N");
                //lblTotalBalance.Text = _decBalance.ToString("N");
            }
        }

        protected void AccountDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ShowList();
        }

        protected void rprAccounts_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
        }

        protected void rprAccounts_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName == "edit")
            {
                int intAccountId = Convert.ToInt32(e.CommandArgument);
                var dr = _Master.Sql.GetDataRow(Properties.Settings.Default.DatabaseName, "dbo", "spcfGetAccount", new NamedValue("accountid", intAccountId));
                AccountInfo ai = (AccountInfo)SqlProvider.DataRowToObject(dr, typeof(AccountInfo));
                MainMultiview.SetActiveView(EditViewAccount);
                EditAccount.Account = ai;
            }
        }

        protected void EditAccount_ButtonClicked(AccountInfo account, EditAccount.eButton button)
        {
            switch (button)
            {
                case EditAccount.eButton.Cancel:
                    {
                        MainMultiview.SetActiveView(ListView);
                        break;
                    }
                case EditAccount.eButton.Delete:
                    {
                        _Master.Sql.ExecuteNonQuery(Properties.Settings.Default.DatabaseName, "dbo", "spcfDeleteAccount", new NamedValue("accountid", account.AccountId));
                        ShowList();
                        break;
                    }
                case EditAccount.eButton.Save:
                    {
                        _Master.Sql.ExecuteNonQuery(Properties.Settings.Default.DatabaseName, "dbo", "spcfSaveAccount", new NamedValue("accountid", account.AccountId), new NamedValue("userid", 3), new NamedValue("accountname", account.AccountName), new NamedValue("accountnumber", account.AccountNumber), new NamedValue("accounttypeid", account.AccountType), new NamedValue("balance", account.Balance), new NamedValue("creditlimit", account.CreditLimit), new NamedValue("availablecredit", account.AvailableCredit), new NamedValue("duedate", account.DueDate), new NamedValue("minimumpayment", account.MinimumPayment), new NamedValue("interestrate", account.InterestRate), new NamedValue("weburl", account.WebUrl));
                        ShowList();
                        break;
                    }
            }
        }

        protected void lnkAddAccount_Click(object sender, EventArgs e)
        {
            MainMultiview.SetActiveView(EditViewAccount);
            EditAccount.Account = new AccountInfo();
        }

        protected void ddlSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowList();
        }
    }
}