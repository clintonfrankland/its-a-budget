<%@ Control Language="vb" AutoEventWireup="false" CodeBehind="EditAccount.ascx.cs" Inherits="ClintonFrankland.EditAccount" %>
<h1>Edit Account</h1>
<div class="container">
    <asp:HiddenField ID="hidAccountId" runat="server" Value="-1" />
    <div class="mb-3">
        <label for='<% = txtAccountName.ClientID %>' class="form-label">Account Name</label>
        <asp:TextBox ID="txtAccountName" runat="server" CssClass="form-control" MaxLength="255" Required="true"></asp:TextBox>
    </div>
    <div class="row mb-3">
        <div class="col-md-6">
            <label for='<% = txtAccountNumber.ClientID %>' class="form-label">Account Number</label>
            <asp:TextBox ID="txtAccountNumber" runat="server" CssClass="form-control" MaxLength="16" Required="true"></asp:TextBox>
        </div>
        <div class="col-md-6">
            <label for='<% = ddlAccountType.ClientID %>' class="form-label">Account Type</label>
            <asp:DropDownList ID="ddlAccountType" runat="server" CssClass="form-select">
                <asp:ListItem Value="1" Text="Checking" Selected="True"></asp:ListItem>
                <asp:ListItem Value="2" Text="Credit Card"></asp:ListItem>
                <asp:ListItem Value="3" Text="Loan"></asp:ListItem>
                <asp:ListItem Value="4" Text="Taxes"></asp:ListItem>
                <asp:ListItem Value="5" Text="Phone"></asp:ListItem>
            </asp:DropDownList>
        </div>
    </div>
    <div class="row mb-3">
        <div class="col-md-4">
            <label for='<% = txtBalance.ClientID %>' class="form-label">Balance</label>
            <asp:TextBox ID="txtBalance" runat="server" TextMode="Number" min="0" step=".01" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
        <div class="col-md-4">
            <label for='<% = txtBalance.ClientID %>' class="form-label">Credit Limit</label>
            <asp:TextBox ID="txtCreditLimit" runat="server" TextMode="Number" min="0" step=".01" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
        <div class="col-md-4">
            <label for='<% = txtBalance.ClientID %>' class="form-label">Available Credit</label>
            <asp:TextBox ID="txtAvailableCredit" runat="server" TextMode="Number" min="0" step=".01" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
    </div>
    <div class="row mb-3">
        <div class="col-md-4">
            <label for='<% = txtDueDate.ClientID %>' class="form-label">Due Date</label>
            <asp:TextBox ID="txtDueDate" runat="server" TextMode="Number" min="1" max="31" step="1" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
        <div class="col-md-4">
            <label for='<% = txtMinimumPayment.ClientID %>' class="form-label">Minimum Payment</label>
            <asp:TextBox ID="txtMinimumPayment" runat="server" TextMode="Number" min="0" step=".01" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
        <div class="col-md-4">
            <label for='<% = txtInterestRate.ClientID %>' class="form-label">Interest Rate</label>
            <asp:TextBox ID="txtInterestRate" runat="server" TextMode="Number" min="0" step=".01" CssClass="form-control" Required="true"></asp:TextBox>
        </div>
    </div>
    <div class="mb-3">
        <label for='<% = txtWebUrl.ClientID %>' class="form-label">Web URL</label>
        <asp:TextBox ID="txtWebUrl" runat="server" CssClass="form-control" MaxLength="1024"></asp:TextBox>
    </div>
    <div class="row mb-3">
        <div class="col-12 text-end">
            <asp:LinkButton ID="lnkEditAccountCancel" runat="server" CssClass="btn btn-primary" OnClick="lnkEditAccountCancel_Click"><i class="fas fa-ban"></i> Cancel</asp:LinkButton>
            &nbsp;&nbsp;<asp:LinkButton ID="lnkEditAccountDelete" runat="server" CssClass="btn btn-primary" OnClick="lnkEditAccountDelete_Click"><i class="fas fa-trash"></i> Delete</asp:LinkButton>
            &nbsp;&nbsp;<asp:Button ID="btnEditAccountSave" runat="server" CssClass="btn btn-primary" Text="Save" OnClick="btnEditAccountSave_Click" />
        </div>
    </div>
</div>
