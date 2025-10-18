using System;
using System.Web.UI;


namespace ClintonFrankland
{
    public partial class EditAccount : UserControl
    {
        private AccountInfo _account = new AccountInfo();

        public enum eButton
        {
            Save,
            Cancel,
            Delete
        }

        public event ButtonClickedEventHandler ButtonClicked;

        public delegate void ButtonClickedEventHandler(AccountInfo account, eButton button);

        public EditAccount()
        {
            Load += Page_Load;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
                RefreshView();
        }

        public void RefreshView()
        {
            hidAccountId.Value = _account.AccountId.ToString();
            txtAccountName.Text = _account.AccountName;
            txtAccountNumber.Text = _account.AccountNumber;
            ddlAccountType.SelectedValue = Convert.ToInt32((int)_account.AccountType).ToString();
            txtBalance.Text = _account.Balance.ToString();
            txtCreditLimit.Text = _account.CreditLimit.ToString();
            txtAvailableCredit.Text = _account.AvailableCredit.ToString();
            txtDueDate.Text = _account.DueDate.ToString();
            txtMinimumPayment.Text = _account.MinimumPayment.ToString();
            txtInterestRate.Text = _account.InterestRate.ToString();
            txtWebUrl.Text = _account.WebUrl;
        }

        protected void lnkEditAccountCancel_Click(object sender, EventArgs e)
        {
            _account.AccountId = Convert.ToInt32(hidAccountId.Value);
            ButtonClicked?.Invoke(_account, eButton.Cancel);
        }

        protected void btnEditAccountSave_Click(object sender, EventArgs e)
        {
            _account.AccountId = Convert.ToInt32(hidAccountId.Value);
            _account.AccountName = txtAccountName.Text;
            _account.AccountNumber = txtAccountNumber.Text;
            _account.AccountType = (AccountInfo.eAccountType)Convert.ToInt32(ddlAccountType.SelectedValue);
            _account.Balance = Convert.ToDecimal(txtBalance.Text);
            _account.CreditLimit = Convert.ToDecimal(txtCreditLimit.Text);
            _account.AvailableCredit = Convert.ToDecimal(txtAvailableCredit.Text);
            _account.DueDate = Convert.ToInt32(txtDueDate.Text);
            _account.MinimumPayment = Convert.ToDecimal(txtMinimumPayment.Text);
            _account.InterestRate = Convert.ToDecimal(txtInterestRate.Text);
            _account.WebUrl = txtWebUrl.Text;
            ButtonClicked?.Invoke(_account, eButton.Save);
        }

        protected void lnkEditAccountDelete_Click(object sender, EventArgs e)
        {
            _account.AccountId = Convert.ToInt32(hidAccountId.Value);
            ButtonClicked?.Invoke(_account, eButton.Delete);
        }

        public AccountInfo Account
        {
            get
            {
                return _account;
            }
            set
            {
                _account = value;
                RefreshView();
            }
        }

    }
}