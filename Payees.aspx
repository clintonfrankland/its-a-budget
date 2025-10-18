<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Payees.aspx.cs" Inherits="ClintonFrankland.Payees" MasterPageFile="~/ClintonFrankland.Master" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:Label ID="ErrorLabel" runat="server" CssClass="NormalRed" />
    <asp:MultiView ID="MainMultiview" runat="server" ActiveViewIndex="0">
        <asp:View ID="ListView" runat="server">
            <div class="container">
                <div class="row">
                    <div class="col-3">
                        <h1>Payees</h1>
                    </div>
                    <div class="col-9 text-end">
                        <asp:LinkButton ID="AddPayeeLink" runat="server" ToolTip="Add Payee" CssClass="btn btn-primary"><i class="fas fa-plus"></i> Add a Payee</asp:LinkButton>
                        <asp:LinkButton ID="MergePayCleesLink" runat="server" ToolTip="Merge Payees" CssClass="btn btn-primary"></asp:LinkButton>
                    </div>
                </div>
            </div>
            <asp:Repeater ID="PayeeRepeater" runat="server">
                <HeaderTemplate>
                <table class="table table-striped table-sm" id="payeeGrid">
                    <thead>
                        <tr>
                            <th>Payee Name</td>
                            <th>Trans.</td>
                            <th>Budgets</td>
                            <th>Last Transaction</td>
                            <th>Transaction Total</td>
                        </tr>
                        </thead>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td><asp:CheckBox ID="SelectPayeeCheckbox" runat="server" />&nbsp;&nbsp;<asp:LinkButton ID="EditPayeeLink" runat="server" CommandName="edit" CommandArgument='<%# Eval("PayeeId")%>'><%# Eval("PayeeName")%></asp:LinkButton></td>
                        <td><%# Eval("TransactionCount")%></td>
                        <td><%# Eval("BudgetCount")%></td>
                        <td><%# Eval("LastTransaction")%></td>
                        <td><%# Eval("TransactionTotal")%></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                    </table>
                </FooterTemplate>
            </asp:Repeater>
            <script>
                new DataTable('#payeeGrid', {
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
    </asp:MultiView>
</asp:Content>