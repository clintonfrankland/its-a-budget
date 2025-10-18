using System;
using System.Data;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace ClintonFrankland
{
    public partial class Checkbook : Page
    {

        private ClintonFrankland _Master { get; set; }

        public Checkbook()
        {
            Load += Page_Load;
        }

        private void ShowError(Exception ex)
        {
            ErrorLabel.Text = ex.GetType().ToString() + " : " + ex.Message + "<br/>" + ex.StackTrace;
            this.ErrorLabel.Visible = true;
        }

        private void ShowErrorMessage(string message)
        {
            if (ErrorLabel.Text.Length > 0)
                ErrorLabel.Text += "<br /><br />";
            ErrorLabel.Text += message;
            ErrorLabel.Visible = true;
        }

        private void ShowAddTransaction()
        {
            try
            {
                MainMultiview.SetActiveView(EditView);
                hidTransactionId.Value = "-1";
                wdpEditDate.Text = DateTime.Today.ToString("yyyy-MM-dd");
                txtEditAmount.Text = "0.00";
                ddlTransactionType.SelectedIndex = 0;
                chkEditCleared.Checked = false;
                imgEditDelete.Visible = false;
                lnkEditDelete.Visible = false;
                hidBudgetId.Value = "-1";
                wdpEditDate.Focus();
                wddEditCategory.Text = "";
                wddEditPayee.Text = "";
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowEdit(int id)
        {
            try
            {
                var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetTransaction", new NamedValue("TransactionId", id));
                MainMultiview.SetActiveView(EditView);
                hidTransactionId.Value = id.ToString();
                wdpEditDate.Text = Convert.ToDateTime(dr["TransactionDate"]).ToString("yyyy-MM-dd");
                if (decimal.Parse(dr["Amount"].ToString()) > 0m)
                {
                    ddlTransactionType.SelectedIndex = 1;
                    txtEditAmount.Text = dr["Amount"].ToString();
                }
                else
                {
                    ddlTransactionType.SelectedIndex = 0;
                    txtEditAmount.Text = (-decimal.Parse(dr["Amount"].ToString())).ToString();
                }
                chkEditCleared.Checked = (bool)dr["Cleared"];
                imgEditDelete.Visible = true;
                lnkEditDelete.Visible = true;
                wddEditCategory.Text = dr["Category"].ToString();
                wddEditPayee.Text = dr["Payee"].ToString();
                wdpEditDate.Focus();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void MarkCleared(int id)
        {
            try
            {
                _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMyCheckboxMarkTransactionCleared", new NamedValue("TransactionId", id));
                ShowList();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void MarkUncleared(int id)
        {
            try
            {
                _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMyCheckboxMarkTransactionUncleared", new NamedValue("TransactionId", id));
                ShowList();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void AddLogEntry(string message)
        {
            try
            {
            }
            // Dim Asy As [Assembly] = [Assembly].GetCallingAssembly
            // Dim strAssemblyName As String = Asy.FullName.Split(CChar(","))(0)
            // Dim strVersion As String = Asy.FullName.Split(CChar(","))(1).Replace("Version=", "")
            // Dim strManufacturer As String = ""
            // Dim strApplication As String = strAssemblyName

            // Dim strSplit() As String = strAssemblyName.Split(CChar("."))

            // If strSplit.Length > 1 Then
            // strManufacturer = strAssemblyName.Split(CChar("."))(0)
            // strApplication = strAssemblyName.Replace(strManufacturer & ".", "")
            // End If

            // Dim objEventLog As New DotNetNuke.Services.Log.EventLog.EventLogController
            // Dim objLog As New DotNetNuke.Services.Log.EventLog.LogInfo()
            // objLog.AddProperty(strAssemblyName, message)
            // objLog.LogTypeKey = DotNetNuke.Services.Log.EventLog.EventLogController.EventLogType.HOST_ALERT.ToString()
            // objEventLog.AddLog(objLog)
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowList()
        {
            try
            {
                MainMultiview.SetActiveView(ListView);

                var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetCheckbookBalance_020000", new NamedValue("UserId", 3));
                if (dt.Rows.Count > 0)
                {
                    lblBalanceTop.Text = string.Format("{0:C}", decimal.Parse(dt.Rows[0]["Balance"].ToString()));
                    lblClearedTop.Text = string.Format("{0:C}", decimal.Parse(dt.Rows[0]["Cleared"].ToString()));
                }

                // Get the checkbook
                dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetMyCheckbook_040100", new NamedValue("UserId", 3));
                rprList.DataSource = dt;
                rprList.DataBind();
                int intBudgetDays = int.Parse(ddlBudgetDays.SelectedValue);
                if (intBudgetDays == 0)
                {
                    pnlBudgets.Visible = false;
                }
                else
                {
                    intBudgetDays += 1;
                    pnlBudgets.Visible = true;
                    dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetMyBudget_020000", new NamedValue("EndDate", DateTime.Today.AddDays(intBudgetDays)), new NamedValue("UserId", 3));
                    rprForecast.Visible = dt.Rows.Count > 0;
                    rprForecast.DataSource = dt;
                    rprForecast.DataBind();
                    this.BillsDiv.Style["display"] = "none";
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (DateTime.Parse(dr["DueDate"].ToString()) <= DateTime.Today && bool.Parse(dr["IsBill"].ToString()))
                        {
                            this.BillsDiv.Style["display"] = "block";
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }


        protected void AccountDropDown_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.ShowList();
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
                decimal decAmount = decimal.Parse(((Label)e.Item.FindControl("lnkAmount")).Text, NumberStyles.AllowCurrencySymbol | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowParentheses);
                if (decAmount > 0m)
                {
                    ((Label)e.Item.FindControl("btnBudgetName")).Font.Bold = true;
                    ((Label)e.Item.FindControl("lnkCategory")).Font.Bold = true;
                    ((Label)e.Item.FindControl("lnkDueDate")).Font.Bold = true;
                    ((Label)e.Item.FindControl("lnkAmount")).Font.Bold = true;
                    ((Label)e.Item.FindControl("lnkBalance")).Font.Bold = true;
                }
                decimal decBalance = decimal.Parse(((Label)e.Item.FindControl("lnkBalance")).Text, NumberStyles.AllowCurrencySymbol | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowParentheses);
                if (decBalance < 0m)
                {
                    ((Label)e.Item.FindControl("lnkBalance")).Font.Italic = true;
                    ((Label)e.Item.FindControl("lnkBalance")).ForeColor = System.Drawing.Color.Red;
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
                    case "markpaid":
                        {
                            ShowAddTransaction();
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            txtEditAmount.Text = ((Label)e.Item.FindControl("lnkAmount")).Text;
                            wdpEditDate.Text = DateTime.Parse(((Label)e.Item.FindControl("lnkDueDate")).Text).ToString("yyyy-MM-dd");
                            if (decimal.Parse(txtEditAmount.Text, NumberStyles.AllowParentheses | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowCurrencySymbol) < 0m)
                            {
                                ddlTransactionType.SelectedValue = 0.ToString();
                                txtEditAmount.Text = (-decimal.Parse(txtEditAmount.Text, NumberStyles.AllowParentheses | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowCurrencySymbol)).ToString();
                            }
                            else
                            {
                                ddlTransactionType.SelectedValue = 1.ToString();
                                txtEditAmount.Text = (decimal.Parse(txtEditAmount.Text, NumberStyles.AllowParentheses | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowCurrencySymbol)).ToString();
                            }
                            try
                            {
                                var payeeField = (HiddenField)e.Item.FindControl("hidPayee");
                                wddEditPayee.Text = payeeField.Value.Length > 0 ? payeeField.Value : ((Label)e.Item.FindControl("btnBudgetName")).Text;
                            }
                            catch (Exception ex)
                            {
                            }
                            try
                            {
                                wddEditCategory.Text = ((Label)e.Item.FindControl("lnkCategory")).Text;
                            }
                            catch (Exception ex)
                            {
                            }

                            break;
                        }
                    case "skip":
                        {
                            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", int.Parse(e.CommandArgument.ToString())));
                            ShowList();
                            budgetCollapse.Attributes["class"] = "collapse show";
                            break;
                        }
                    case "edit":
                        {
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            ShowEditBudget();
                            break;
                        }
                    case "editnext":
                        {
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            ShowEditNext();
                            break;
                        }
                    case "delete":
                        {
                            hidBudgetId.Value = e.CommandArgument.ToString();
                            ShowDeleteBudget();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ShowEditBudget()
        {
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            MainMultiview.SetActiveView(EditViewBudget);

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
            txtEditBudgetAmount.Text = dr["Amount"].ToString();
            txtEditBudgetPayee.Text = dr["Payee"].ToString();
            txtEditBudgetCategory.Text = dr["Category"].ToString();

        }

        protected void btnEditBudgetOk_Click(object sender, EventArgs e)
        {
            var dteEndDate = DateTime.Parse("1970-01-01");
            decimal decAmount = 0m;
            if (chkEndDate.Checked)
            {
                dteEndDate = Convert.ToDateTime(wdpEndDate.Text);
                if (dteEndDate < Convert.ToDateTime(wdpNextDueDate.Text))
                {
                    ShowErrorMessage("The end date must be after the next due date.");
                    return;
                }
            }
            try
            {
                decAmount = Convert.ToDecimal(txtEditBudgetAmount.Text);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("The amount must be a decimal.");
                return;
            }
            bool bolIsBill = chkIsBill.Checked;
            bool bolIsAuto = bolIsBill ? chkIsAuto.Checked : false;
            string strPayee = bolIsBill ? txtEditBudgetPayee.Text : "";
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfSaveBudget_030000", new NamedValue("BudgetId", Convert.ToInt32(hidBudgetId.Value)), new NamedValue("BudgetName", txtEditBudgetName.Text), new NamedValue("FrequencyId", ddlEditFrequency.SelectedValue), new NamedValue("NextDueDate", wdpNextDueDate.Text), new NamedValue("EndDate", dteEndDate), new NamedValue("Amount", decAmount), new NamedValue("BudgetTypeId", ddlBudgetType.SelectedValue), new NamedValue("Category", txtEditBudgetCategory.Text), new NamedValue("UserId", 3), new NamedValue("IsAuto", chkIsAuto.Checked), new NamedValue("IsBill", bolIsBill), new NamedValue("IsLate", chkIsLate.Checked), new NamedValue("Payee", strPayee));
            ShowList();
            budgetCollapse.Attributes["class"] = "collapse show";
        }

        private void ShowEditNext()
        {
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));

            MainMultiview.SetActiveView(EditViewNextBudget);
            txtEditNextBudgetName.Text = dr["BudgetName"].ToString();
            wdpEditNextDueDate.Text = Convert.ToDateTime(dr["NextDueDate"]).ToString("yyyy-MM-dd");
            txtEditNextAmount.Text = dr["Amount"].ToString();
            chkEditNextIsLate.Checked = (bool)dr["IsLate"];
            chkEditNextIsAuto.Checked = (bool)dr["IsAuto"];
            wddEditNextCategory.Text = dr["Category"].ToString();
            lblEditNextError.Text = "";
        }

        private void ShowDeleteBudget()
        {
            MainMultiview.SetActiveView(DeleteBudgetView);
            var dr = _Master.Sql.GetDataRow(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetBudget", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
            lblDeleteBudgetName.Text = dr["BudgetName"].ToString();
        }

        protected void btnDeleteNo_Click(object sender, EventArgs e)
        {
            MainMultiview.SetActiveView(ListView);
            budgetCollapse.Attributes["class"] = "collapse show";
        }

        protected void btnDeleteYes_Click(object sender, EventArgs e)
        {
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfDeleteBudget", new NamedValue("BudgetId", Convert.ToInt32(hidBudgetId.Value)));
            ShowList();
            budgetCollapse.Attributes["class"] = "collapse show";
        }

        protected void btnEditNextCancel_Click(object sender, EventArgs e)
        {
            ShowList();
            budgetCollapse.Attributes["class"] = "collapse show";
        }

        protected void lnkEditBudgetDelete_Click(object sender, EventArgs e)
        {
            ShowDeleteBudget();
        }

        protected void lnkEditBudgetEditNext_Click(object sender, EventArgs e)
        {
            ShowEditNext();
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
            budgetCollapse.Attributes["class"] = "collapse show";
        }

        private void DeleteTransaction()
        {
            try
            {
                _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMyCheckbookDeleteTransaction", new NamedValue("TransactionId", int.Parse(hidTransactionId.Value)));
                MainMultiview.SetActiveView(ListView);
                ShowList();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
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

        protected void lnkAddTransaction_Click(object sender, EventArgs e)
        {
            ShowAddTransaction();
        }

        protected void btnEditCancel_Click(object sender, EventArgs e)
        {
            MainMultiview.SetActiveView(ListView);
        }

        protected void btnEditOk_Click(object sender, EventArgs e)
        {
            decimal decAmount = 0m;
            try
            {
                decAmount = Convert.ToDecimal(txtEditAmount.Text);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("The amount must be a decimal.");
                return;
            }
            if (ddlTransactionType.SelectedIndex == 0)
                decAmount = -decAmount;
            _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfSaveTransaction_030000", new NamedValue("TransactionId", int.Parse(hidTransactionId.Value)), new NamedValue("TransactionDate", wdpEditDate.Text), new NamedValue("Amount", decAmount), new NamedValue("Payee", wddEditPayee.Text), new NamedValue("Category", wddEditCategory.Text), new NamedValue("AccountId", 1), new NamedValue("Cleared", chkEditCleared.Checked), new NamedValue("UserId", 3));







            if (hidBudgetId.Value != "-1")
            {
                _Master.Sql.ExecuteNonQuery(_Master.SiteInfo.DatabaseName, "dbo", "spcfMarkPaid", new NamedValue("BudgetId", int.Parse(hidBudgetId.Value)));
                hidBudgetId.Value = "-1";
            }
            ShowList();

        }

        protected void rprList_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            AddLogEntry("Command received : " + e.CommandName);
            switch (e.CommandName ?? "")
            {
                case "edit":
                    {
                        ShowEdit(int.Parse(e.CommandArgument.ToString()));
                        break;
                    }
                case "markcleared":
                    {
                        MarkCleared(int.Parse(e.CommandArgument.ToString()));
                        break;
                    }
                case "marknotcleared":
                    {
                        MarkUncleared(int.Parse(e.CommandArgument.ToString()));
                        break;
                    }
            }
        }

        protected void lnkEditDelete_Click(object sender, EventArgs e)
        {
            DeleteTransaction();
        }

        protected void imgEditDelete_Click(object sender, ImageClickEventArgs e)
        {
            DeleteTransaction();
        }

        protected void ddlBudgetDays_SelectedIndexChanged(object sender, EventArgs e)
        {
            int intBudgetDays = int.Parse(ddlBudgetDays.SelectedValue);
            if (intBudgetDays == 0)
            {
                pnlBudgets.Visible = false;
            }
            else
            {
                intBudgetDays += 1;
                pnlBudgets.Visible = true;
                var dt = _Master.Sql.GetDataTable(_Master.SiteInfo.DatabaseName, "dbo", "spcfGetMyBudget_020000", new NamedValue("EndDate", DateTime.Today.AddDays(intBudgetDays)), new NamedValue("UserId", 3));

                rprForecast.DataSource = dt;
                rprForecast.DataBind();
            }
            budgetCollapse.Attributes["class"] = "collapse show";
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

        protected void brnEditBudgetCancel_Click(object sender, EventArgs e)
        {
            MainMultiview.SetActiveView(ListView);
            budgetCollapse.Attributes["class"] = "collapse show";
        }

    }
}