<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Accounts.aspx.cs" Inherits="ClintonFrankland.Accounts1" MasterPageFile="~/ClintonFrankland.Master" %>
<%@ Register Src="~/EditAccount.ascx" TagPrefix="uc1" TagName="EditAccount" %>
<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="server">
    <link href="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.css" rel="stylesheet">
    <script src="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.js"></script>
</asp:Content>
<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:Label ID="ErrorLabel" runat="server" CssClass="NormalRed" />
    <asp:MultiView ID="MainMultiview" runat="server" ActiveViewIndex="0">
        <asp:View ID="ListView" runat="server">
            <div class="container">
                <div class="row">
                    <div class="col-3">
                        <h1>Accounts</h1>
                    </div>
                    <div class="col-9 text-end">
                        <asp:LinkButton ID="lnkAddAccount" runat="server" CssClass="btn btn-primary" OnClick="lnkAddAccount_Click"><i class="fas fa-plus"></i> Add an Account</asp:LinkButton>
                    </div>
                </div>
            </div>
            <asp:Repeater ID="rprAccounts" runat="server" OnItemCommand="rprAccounts_ItemCommand" OnItemDataBound="rprAccounts_ItemDataBound">
                <HeaderTemplate>
                <table class="table table-striped table-sm" id="accountsTable">
                    <thead>
                        <tr>
                            <th>Account Name</th>
                            <th>Account Type</th>
                            <th>Last Upd</th>
                            <th>Acct#</th>
                            <th>Interest</th>
                            <th>Payment</th>
                            <th>Balance</th>
                            <th>Ratio</th>
                        </tr>
                        </thead>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td><asp:LinkButton ID="EditAccountLink" runat="server" CommandName="edit" CommandArgument='<%# Eval("AccountId")%>'><%# Eval("AccountName")%></asp:LinkButton></td>
                        <td><%# Eval("AccountType")%></td>
                        <td><%# Eval("LastUpdated")%></td>
                        <td><%# Eval("AccountNumber")%></td>
                        <td><%# Eval("InterestRate")%></td>
                        <td><%# Eval("MinimumPayment", "{0:c}") %></td>
                        <td><%# Eval("Balance", "{0:c}") %></td>
                        <td><%# Eval("Ratio", "{0:#.##}") %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                    </table>
                </FooterTemplate>
            </asp:Repeater>
            <script>
                new DataTable('#accountsTable', {
                    layout: {
                        topStart: {
                            buttons: ['copy', 'csv', 'excel', 'pdf', 'print']
                        }
                    },
                    order: [[0, 'asc']],
                    paging: false,
                });
            </script>
        </asp:View>
        <asp:View ID="EditViewAccount" runat="server">
            <uc1:EditAccount runat="server" ID="EditAccount" OnButtonClicked="EditAccount_ButtonClicked" />
        </asp:View>
    </asp:MultiView>
</asp:Content>