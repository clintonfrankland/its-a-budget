using ClintonFrankland.Properties;
using System;
using System.Data;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace ClintonFrankland
{
    public partial class Budget : Page
    {

        private ClintonFrankland _Master;

        public Budget()
        {
            Load += Page_Load;
        }

        private void ShowError(Exception ex)
        {
            ErrorLabel.Text = ex.GetType().ToString() + " : " + ex.Message + "<br/>" + ex.StackTrace;
            this.ErrorLabel.Visible = true;
        }

        private void ShowList()
        {
            var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetCheckbookBalance_020000", new NamedValue("UserId", 3));
            dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetMyBudget_020000", new NamedValue("EndDate", DateTime.Today.AddMonths(6)), new NamedValue("UserId", 3));

            if (dt.Rows.Count > 0)
            {
                rprForecast.DataSource = dt;
                rprForecast.DataBind();
            }

            MainMultiview.SetActiveView(vwForecast);
        }

        private void ShowEdit()
        {
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            MainMultiview.SetActiveView(EditView);

            chkIsAuto.Checked = Convert.ToBoolean(dr["IsAuto"]);
            chkIsBill.Checked = Convert.ToBoolean(dr["IsBill"]);
            chkIsLate.Checked = Convert.ToBoolean(dr["IsLate"]);
            lblEditIsAuto.Enabled = chkIsBill.Checked;
            chkIsAuto.Enabled = chkIsBill.Checked;
            lblPayee.Enabled = chkIsBill.Checked;
            wddEditPayee.Enabled = chkIsBill.Checked;
            chkIsLate.Enabled = chkIsBill.Checked;
            lblEditIsLate.Enabled = chkIsBill.Checked;
            ddlEditFrequency.DataSource = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetFrequencies");
            ddlEditFrequency.DataBind();
            txtEditBudgetName.Text = dr["BudgetName"].ToString();
            ddlBudgetType.SelectedValue = dr["BudgetTypeId"].ToString();
            wdpNextDueDate.Text = Convert.ToDateTime(dr["NextDueDate"]).ToString("yyyy-MM-dd");
            ddlEditFrequency.SelectedValue = dr["FrequencyId"].ToString();
            wdpEndDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
            if (Convert.ToDateTime(dr["EndDate"]) == DateTime.Parse("1970-01-01"))
            {
                chkEndDate.Checked = false;
                wdpEndDate.Visible = false;
            }
            else
            {
                chkEndDate.Checked = true;
                wdpEndDate.Visible = true;
                wdpEndDate.Text = Convert.ToDateTime(dr["EndDate"]).ToString("yyyy-MM-dd");
            }
            txtEditAmount.Text = dr["Amount"].ToString();
            wddEditPayee.Text = dr["Payee"].ToString();
            wddEditCategory.Text = dr["Category"].ToString();

            lblEditError.Text = "";
        }

        private void ShowAddBudget()
        {
            MainMultiview.SetActiveView(EditView);
            ddlEditFrequency.DataSource = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetFrequencies");
            ddlEditFrequency.DataBind();
            hidBudgetId.Value = "-1";
            txtEditBudgetName.Text = "";
            ddlBudgetType.SelectedIndex = 1;
            wdpNextDueDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
            ddlEditFrequency.SelectedIndex = 0;
            wdpEndDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
            chkEndDate.Checked = false;
            wdpEndDate.Visible = false;
            txtEditAmount.Text = "0.00";
            lblEditError.Text = "";
            wddEditCategory.Text = "";
            wddEditPayee.Text = "";
            // Me.chkIsAuto.Checked = False
        }

        private void ShowDeleteBudget()
        {
            MainMultiview.SetActiveView(vwDelete);
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            lblDeleteBudgetName.Text = dr["BudgetName"].ToString();
        }

        private void ShowEditNext()
        {
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));

            MainMultiview.SetActiveView(EditViewNext);
            txtEditNextBudgetName.Text = dr["BudgetName"].ToString();
            wdpEditNextDueDate.Text = Convert.ToDateTime(dr["NextDueDate"]).ToString("yyyy-MM-dd");
            txtEditNextAmount.Text = dr["Amount"].ToString();
            chkEditNextIsLate.Checked = (bool)dr["IsLate"];
            chkEditNextIsAuto.Checked = (bool)dr["IsAuto"];
            wddEditNextCategory.Text = dr["Category"].ToString();
            lblEditNextError.Text = "";
        }

        private void MarkPaid(int budgetid)
        {
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", budgetid));
            ShowList();
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

        protected void rprForecast_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            try
            {
                if (e.CommandArgument.ToString() == "-1")
                    return;
                switch (e.CommandName ?? "")
                {
                    case "edit":
                        {
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            ShowEdit();
                            break;
                        }
                    case "markpaid":
                        {
                            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", int.Parse(e.CommandArgument.ToString())));
                            ShowList();
                            break;
                        }

                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        protected void btnEditCancel_Click(object sender, EventArgs e)
        {
            ShowList();
        }

        protected void rprForecast_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType == ListItemType.Item | e.Item.ItemType == ListItemType.AlternatingItem)
            {
                ((Image)e.Item.FindControl("imgIsAuto")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsAuto")).Value);
                ((Image)e.Item.FindControl("imgIsBill")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsBill")).Value);
                ((Image)e.Item.FindControl("imgIsLate")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsLate")).Value);
                ((Image)e.Item.FindControl("imgNotIsAuto")).Visible = !bool.Parse(((HiddenField)e.Item.FindControl("hidIsAuto")).Value);
                ((Image)e.Item.FindControl("imgNotIsBill")).Visible = !bool.Parse(((HiddenField)e.Item.FindControl("hidIsBill")).Value);
                ((Image)e.Item.FindControl("imgNotIsLate")).Visible = !bool.Parse(((HiddenField)e.Item.FindControl("hidIsLate")).Value);
                decimal decAmount = decimal.Parse(((LinkButton)e.Item.FindControl("lnkAmount")).Text, NumberStyles.AllowCurrencySymbol | NumberStyles.AllowParentheses | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint);
                if (decAmount > 0m)
                {
                    ((LinkButton)e.Item.FindControl("btnBudgetName")).Font.Bold = true;
                    ((LinkButton)e.Item.FindControl("lnkCategory")).Font.Bold = true;
                    ((LinkButton)e.Item.FindControl("lnkDueDate")).Font.Bold = true;
                    ((LinkButton)e.Item.FindControl("lnkAmount")).Font.Bold = true;
                    ((LinkButton)e.Item.FindControl("lnkBalance")).Font.Bold = true;
                }
                decimal decBalance = decimal.Parse(((LinkButton)e.Item.FindControl("lnkBalance")).Text, NumberStyles.AllowCurrencySymbol | NumberStyles.AllowParentheses | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint);
                if (decBalance < 0m)
                {
                    ((LinkButton)e.Item.FindControl("lnkBalance")).Font.Italic = true;
                    ((LinkButton)e.Item.FindControl("lnkBalance")).ForeColor = System.Drawing.Color.Red;
                }
            }
        }

        protected void chkEndDate_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEndDate.Checked)
            {
                wdpEndDate.Visible = true;
            }
            else
            {
                wdpEndDate.Visible = false;
            }
        }

        protected void btnEditOk_Click(object sender, EventArgs e)
        {
            var dteEndDate = DateTime.Parse("1970-01-01");
            decimal decAmount = 0m;
            if (chkEndDate.Checked)
            {
                dteEndDate = DateTime.Parse(wdpEndDate.Text);
                if (dteEndDate < DateTime.Parse(wdpNextDueDate.Text))
                {
                    lblEditError.Text = "The end date must be after the next due date.";
                    return;
                }
            }
            try
            {
                decAmount = Convert.ToDecimal(txtEditAmount.Text);
            }
            catch (Exception ex)
            {
                lblEditError.Text = "The amount must be a decimal.";
                return;
            }
            bool bolIsBill = chkIsBill.Checked;
            bool bolIsAuto = bolIsBill ? chkIsAuto.Checked : false;
            string strPayee = bolIsBill ? wddEditPayee.Text : "";
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfSaveBudget_030000", new NamedValue("BudgetId", Convert.ToInt32(hidBudgetId.Value)), new NamedValue("BudgetName", txtEditBudgetName.Text), new NamedValue("FrequencyId", ddlEditFrequency.SelectedValue), new NamedValue("NextDueDate", wdpNextDueDate.Text), new NamedValue("EndDate", dteEndDate), new NamedValue("Amount", decAmount), new NamedValue("BudgetTypeId", ddlBudgetType.SelectedValue), new NamedValue("Category", wddEditCategory.Text), new NamedValue("UserId", 3), new NamedValue("IsAuto", chkIsAuto.Checked), new NamedValue("IsBill", bolIsBill), new NamedValue("IsLate", chkIsLate.Checked), new NamedValue("Payee", strPayee));
            ShowList();
        }

        protected void imgAddBudget_Click(object sender, ImageClickEventArgs e)
        {
            ShowAddBudget();
        }

        protected void lnkAddBudget_Click(object sender, EventArgs e)
        {
            ShowAddBudget();
        }

        protected void lnkEditDelete_Click(object sender, EventArgs e)
        {
            ShowDeleteBudget();
        }

        protected void imgEditDelete_Click(object sender, ImageClickEventArgs e)
        {
            ShowDeleteBudget();
        }

        protected void btnDeleteNo_Click(object sender, EventArgs e)
        {
            ShowList();
        }

        protected void btnDeleteYes_Click(object sender, EventArgs e)
        {
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfDeleteBudget", new NamedValue("BudgetId", Convert.ToInt32(hidBudgetId.Value)));
            ShowList();
        }

        protected void imgEditNext_Click(object sender, ImageClickEventArgs e)
        {
            ShowEditNext();
        }

        protected void lnkEditNext_Click(object sender, EventArgs e)
        {
            ShowEditNext();
        }

        protected void btnEditNextCancel_Click(object sender, EventArgs e)
        {
            ShowList();
        }

        protected void btnEditNextOk_Click(object sender, EventArgs e)
        {
            decimal decAmount = 0m;
            try
            {
                decAmount = Convert.ToDecimal(txtEditNextAmount.Text);
            }
            catch (Exception ex)
            {
                lblEditNextError.Text = "The amount must be a decimal.";
                return;
            }
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfSaveBudget_030000", new NamedValue("BudgetId", -1), new NamedValue("BudgetName", txtEditNextBudgetName.Text), new NamedValue("FrequencyId", 0), new NamedValue("NextDueDate", wdpEditNextDueDate.Text), new NamedValue("EndDate", DateTime.Parse("1970-01-01")), new NamedValue("Amount", decAmount), new NamedValue("BudgetTypeId", dr["BudgetTypeId"]), new NamedValue("Category", wddEditCategory.Text), new NamedValue("UserId", 3), new NamedValue("IsAuto", chkEditNextIsAuto.Checked), new NamedValue("IsBill", dr["IsBill"]), new NamedValue("IsLate", chkIsLate.Checked), new NamedValue("Payee", dr["Payee"]));
            ShowList();
        }

        protected void AccountDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ShowList();
        }

        protected void imgChangeView_Click(object sender, ImageClickEventArgs e)
        {
            if (hidShowBudgetList.Value == "1")
            {
                hidShowBudgetList.Value = "0";
            }
            else
            {
                hidShowBudgetList.Value = "1";
            }
            ShowList();
        }

        protected void rprBudgetItems_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            try
            {
                switch (e.CommandName ?? "")
                {
                    case "btnBudgetName":
                        {
                            if (e.CommandArgument.ToString() == "-1")
                                return;
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            ShowEdit();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        protected void lnkChangeView_Click(object sender, EventArgs e)
        {
            if (int.Parse(hidShowBudgetList.Value) == double.Parse("1"))
            {
                hidShowBudgetList.Value = "0";
            }
            else
            {
                hidShowBudgetList.Value = "1";
            }
            ShowList();
        }

        protected void chkIsBill_CheckedChanged(object sender, EventArgs e)
        {
            lblEditIsAuto.Enabled = chkIsBill.Checked;
            chkIsAuto.Enabled = chkIsBill.Checked;
            lblPayee.Enabled = chkIsBill.Checked;
            wddEditPayee.Enabled = chkIsBill.Checked;
            lblEditIsLate.Enabled = chkIsBill.Checked;
            chkIsLate.Enabled = chkIsBill.Checked;
        }

        protected void rprBudgetItems_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType == ListItemType.Item | e.Item.ItemType == ListItemType.AlternatingItem)
            {
                ((Image)e.Item.FindControl("imgIsAuto")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsAuto")).Value);
                ((Image)e.Item.FindControl("imgIsBill")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsBill")).Value);
                ((Image)e.Item.FindControl("imgIsLate")).Visible = bool.Parse(((HiddenField)e.Item.FindControl("hidIsLate")).Value);
                try
                {

                    decimal decAmount = decimal.Parse(((Label)e.Item.FindControl("lblMonthly")).Text);
                    if (decAmount > 0m)
                    {
                        ((LinkButton)e.Item.FindControl("btnBudgetName")).Font.Bold = true;
                        ((Label)e.Item.FindControl("lblCategory")).Font.Bold = true;
                        ((Label)e.Item.FindControl("lblDueDate")).Font.Bold = true;
                        ((Label)e.Item.FindControl("lblAmount")).Font.Bold = true;
                        ((Label)e.Item.FindControl("lblFrequencyName")).Font.Bold = true;
                        ((Label)e.Item.FindControl("lblMonthly")).Font.Bold = true;
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        public string Payees()
        {
            string strReturn = "";
            var dtCategories = _Master.Sql.GetDataTable(Properties.Settings.Default.DatabaseName, "dbo", "spcfGetPayees_020000", new NamedValue("UserId", 3));
            if (dtCategories.Rows.Count > 0)
            {
                foreach (DataRow dr in dtCategories.Rows)
                {
                    if (strReturn.Length > 0)
                        strReturn += ",";
                    strReturn += "'" + dr["PayeeName"].ToString().Replace("'", @"\'").Replace("[", "").Replace("]", "") + "'";
                }
            }
            return strReturn;
        }

        public string Categories()
        {
            string strReturn = "";
            var dtCategories = _Master.Sql.GetDataTable(Properties.Settings.Default.DatabaseName, "dbo", "spcfGetCategories_020000", new NamedValue("UserId", 3));
            if (dtCategories.Rows.Count > 0)
            {
                foreach (DataRow dr in dtCategories.Rows)
                {
                    if (strReturn.Length > 0)
                        strReturn += ",";
                    strReturn += "'" + dr["CategoryName"].ToString().Replace("'", @"\'").Replace("[", "").Replace("]", "").Replace(":", @"\:") + "'";
                }
            }
            return strReturn;
        }

        public string DataValues()
        {
            string strReturn = "";
            var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfMyBudgetGetChart", new NamedValue("EndDate", DateTime.Today.AddDays(60d)), new NamedValue("UserId", 3));

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    if (strReturn.Length > 0)
                        strReturn += ",";
                    strReturn += dr["Balance"].ToString();
                }
            }
            return strReturn;
        }

        public string DataLabels()
        {
            string strReturn = "";
            var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfMyBudgetGetChart", new NamedValue("EndDate", DateTime.Today.AddDays(60d)), new NamedValue("UserId", 3));

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    if (strReturn.Length > 0)
                        strReturn += ",";
                    strReturn += "'" + ((DateTime)dr["Date"]).ToShortDateString() + "'";
                }
            }
            return strReturn;
        }

    }
}