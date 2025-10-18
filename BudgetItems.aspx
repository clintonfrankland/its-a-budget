<%@ Page Language="C#" AutoEventWireup="false" CodeBehind="BudgetItems.aspx.cs" Inherits="ClintonFrankland.BudgetItems" MasterPageFile="~/ClintonFrankland.Master" %>
<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="server">
    <link href="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.css" rel="stylesheet">
    <script src="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.js"></script>
</asp:Content>
<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:Label ID="ErrorLabel" runat="server" CssClass="NormalRed" />
    <asp:MultiView ID="MainMultiview" runat="server" ActiveViewIndex="0">
        <asp:View ID="vwBudgetItems" runat="server">
            <div class="container">
                <div class="row">
                    <div class="col-3">
                        <h1>Budget Items</h1>
                    </div>
                    <div class="col-9 text-end">
                        <asp:LinkButton ID="AddBudgetLink" runat="server" ToolTip="Add Budget" OnClick="lnkAddBudget_Click" CssClass="btn btn-primary"><i class="fas fa-plus"></i> Add a Budget</asp:LinkButton>
                    </div>
                </div>
            </div>
            <asp:Repeater ID="rprBudgetItems" runat="server" OnItemCommand="rprBudgetItems_ItemCommand" OnItemDataBound="rprBudgetItems_ItemDataBound">
                <HeaderTemplate>
                <table class="table table-striped table-sm" id="budgetItemsTable">
                    <thead>
                        <tr>
                            <th>&nbsp;</td>
                            <th>Budget Name</td>
                            <th>Category</td>
                            <th>Due Date</td>
                            <th>End Date</td>
                            <th>Frequency</td>
                            <th>Amount</td>
                            <th>Monthly</td>
                        </tr>
                        </thead>
                </HeaderTemplate>
                <ItemTemplate>
                    <asp:HiddenField ID="hidIsBill" runat="server" Value='<%# Eval("IsBill") %>' />
                    <asp:HiddenField ID="hidIsAuto" runat="server" Value='<%# Eval("IsAuto") %>' />
                    <asp:HiddenField ID="hidIsLate" runat="server" Value='<%# Eval("IsLate") %>' />
                    <tr>
                        <td>
                            <asp:Image runat="server" ID="imgIsBill" AlternateText="Is Bill" ImageUrl="~/images/150-icon_bill-shock.png" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgIsAuto" AlternateText="Is Automatic" ImageUrl="~/images/Letter-A-violet-icon.png" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgIsLate" AlternateText="Is Late" ImageUrl="~/images/Letter-L-black-icon.png" Width="16px" Height="16px" />
                        </td>
                        <td data-sort='<%# Eval("BudgetNameSort") %>'>
                            <asp:LinkButton ID='btnBudgetName' runat='server' Text='<%# Eval("BudgetName")%>' CommandName="btnBudgetName" CommandArgument='<%# Eval("BudgetId") %>' />
                            &nbsp;&nbsp;<asp:ImageButton runat="server" ID="ImageButton1" AlternateText="Mark Paid" Visible="false" ImageUrl="~/images/checked.gif" Width="16px" Height="16px" CommandName="imgEditBudgetMarkPaid" CommandArgument='<%# Eval("BudgetId") %>' />
                            <asp:ImageButton runat="server" ID="imgEditBudgetCancel" AlternateText="Cancel" Visible="false" ImageUrl="~/images/cancel.gif" Width="16px" Height="16px" CommandName="imgEditBudgetCancel" CommandArgument='<%# Eval("BudgetId") %>' />
                        </td>
                        <td><asp:Label ID='lblCategory' runat='server' Text='<%# Eval("Category")%>' /><asp:TextBox ID="txtDescription" runat="server" Text="" Visible="false" /></td>
                        <td data-order='<%# Eval("DueDateSort") %>'><asp:Label ID='lblDueDate' runat='server' Text='<%# Eval("DueDate")%>' /></td>
                        <td><asp:Label ID='lblEndDate' runat='server' Text='<%# Eval("EndDateName")%>' /></td>
                        <td><asp:Label ID='lblFrequencyName' runat='server' Text='<%# Eval("FrequencyName")%>' /></td>
                        <td><asp:Label ID='lblAmount' runat='server' Text='<%# Eval("Amount", "{0:c}")%>' /></td>
                        <td><asp:Label ID='lblMonthly' runat='server' Text='<%# Eval("Monthly")%>' /></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                    </table>
                </FooterTemplate>
            </asp:Repeater>
            <script>
                new DataTable('#budgetItemsTable', {
                    layout: {
                        top1: {
                            searchPanes: {
                                initCollapsed: true,
                                orderable: false,
                            }
                        },
                        topStart: {
                            buttons: ['copy', 'csv', 'excel', 'pdf', 'print']
                        }
                    },
                    searchPanes: {
                        layout: 'columns-3'
                    },
                    order: [[1, 'asc']],
                    paging: false,
                    columnDefs: [
                        {
                            searchPanes: {
                                show: true,
                            },
                            targets: [2, 5]
                        },
                        {
                            searchPanes: {
                                show: false
                            },
                            targets: [0, 1, 3, 4, 6, 7]
                        },
                    ],
                });
            </script>
        </asp:View>
        <asp:View ID="EditView" runat="server">
            <asp:Label ID="lblEditError" runat="server" CssClass="NormalRed" />
            <table width="100%" border="0" cellpadding="3" cellspacing="0">
                <tr>
                    <td colspan="4">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: middle;">Amount : </td>
                    <td style="width: 50%"><asp:TextBox ID="txtEditAmount" runat="server" Width="80px" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: middle;">Budget Name : </td>
                    <td style="width: 50%"><asp:TextBox ID="txtEditBudgetName" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: bottom; height: 28px;"><asp:Label ID="lblEditIsAuto" runat="server" Text="Automatic : " /></td>
                    <td style="width: 50%; vertical-align: middle;"><asp:CheckBox ID="chkIsAuto" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: bottom; height: 28px;"><asp:Label ID="lblEditIsLate" runat="server" Text="Late : " /></td>
                    <td style="width: 50%; vertical-align: middle;"><asp:CheckBox ID="chkIsLate" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: bottom;">Bill/Paycheck :&nbsp;</td>
                    <td style="width: 50%; vertical-align: middle; height: 28px;"><asp:CheckBox ID="chkIsBill" runat="server" AutoPostBack="True" OnCheckedChanged="chkIsBill_CheckedChanged" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;"><asp:Label ID="lblPayee" runat="server" Enabled="false" Text="Payee : " /></td>
                    <td style="width: 50%">
                        <asp:TextBox id="wddEditPayee" runat="server" autocomplete="off"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Type : </td>
                    <td style="width: 50%">
                        <asp:DropDownList ID="ddlBudgetType" runat="server" AutoPostBack="False">
                            <asp:ListItem Value="0">Income</asp:ListItem>
                            <asp:ListItem Value="1">Expense</asp:ListItem>
                        </asp:DropDownList>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Category : </td>
                    <td style="width: 50%">
                        <asp:TextBox id="wddEditCategory" runat="server" autocomplete="off"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Frequency : </td>
                    <td style="width: 50%"><asp:DropDownList ID="ddlEditFrequency" runat="server" 
                            DataTextField="FrequencyName" DataValueField="FrequencyId" 
                            AutoPostBack="False"></asp:DropDownList></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Next Due Date : </td>
                    <td style="width: 50%">
                        <asp:TextBox ID="wdpNextDueDate" runat="server" TextMode="Date"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">End Date :&nbsp;</td>
                    <td style="width: 50%; vertical-align: middle; height: 28px;">                            
                        <asp:CheckBox ID="chkEndDate" runat="server" AutoPostBack="True" OnCheckedChanged="chkEndDate_CheckedChanged" />&nbsp;&nbsp;
                        <asp:TextBox ID="wdpEndDate" runat="server" TextMode="Date"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4" style="text-align: center">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td colspan="2" style="width: 80%">
                        <asp:ImageButton ID="imgEditDelete" runat="server" ImageUrl="/images/delete.gif" Width="16px" Height="16px" BorderStyle="None" OnClick="imgEditDelete_Click" />
                        <asp:LinkButton ID="lnkEditDelete" runat="server" Text="Delete this Budget" OnClick="lnkEditDelete_Click" /><br />
                        <asp:ImageButton ID="imgEditNext" runat="server" ImageUrl="/images/edit.gif" Width="16px" Height="16px" BorderStyle="None" OnClick="imgEditNext_Click" />
                        <asp:LinkButton ID="lnkEditNext" runat="server" Text="Edit Next" OnClick="lnkEditNext_Click" />
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4" style="text-align: center"><asp:Button id="btnEditOk" runat="server" Text="OK" OnClick="btnEditOk_Click" />&nbsp;&nbsp;<asp:Button ID="btnEditCancel" runat="server" Text="Cancel" OnClick="btnEditCancel_Click" /></td>
                </tr>
                </table>
            </asp:View>
        <asp:View ID="vwDelete" runat="server">
            <table width="100%" border="0" cellpadding="3" cellspacing="0">
                <tr>
                    <td colspan="3">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="3">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="width: 80%; vertical-align: middle;">
                        Are you sure you wish to delete the budget "<asp:Label ID="lblDeleteBudgetName" runat="server" Text="" />"?
                        </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="3" style="text-align: center">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="3" style="text-align: center"><asp:Button id="btnDeleteYes" runat="server" Text="Yes" OnClick="btnDeleteYes_Click" />&nbsp;&nbsp;<asp:Button ID="btnDeleteNo" runat="server" Text="No" OnClick="btnDeleteNo_Click" /></td>
                </tr>
                </table>
        </asp:View>
        <asp:View ID="EditViewNext" runat="server">
            <asp:Label ID="lblEditNextError" runat="server" CssClass="NormalRed" />
            <table width="100%" border="0" cellpadding="3" cellspacing="0">
                <tr>
                    <td colspan="4">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: middle;">Amount : </td>
                    <td style="width: 50%"><asp:TextBox ID="txtEditNextAmount" runat="server" Width="80px" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: middle;">Budget Name : </td>
                    <td style="width: 50%"><asp:TextBox ID="txtEditNextBudgetName" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Category : </td>
                    <td style="width: 50%">
                        <asp:TextBox id="wddEditNextCategory" runat="server" autocomplete="off"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%;">Due Date : </td>
                    <td style="width: 50%">
                        <asp:TextBox ID="wdpEditNextDueDate" runat="server" TextMode="Date"></asp:TextBox>
                    </td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: bottom; height: 28px;"><asp:Label ID="lblEditNextIsLate" runat="server" Text="Late : " /></td>
                    <td style="width: 50%; vertical-align: middle;"><asp:CheckBox ID="chkEditNextIsLate" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td style="width: 10%">&nbsp;</td>
                    <td style="text-align: right; width: 30%; vertical-align: bottom; height: 28px;"><asp:Label ID="lblEditNextIsAuto" runat="server" Text="Automatic : " /></td>
                    <td style="width: 50%; vertical-align: middle;"><asp:CheckBox ID="chkEditNextIsAuto" runat="server" /></td>
                    <td style="width: 10%">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4" style="text-align: center">&nbsp;</td>
                </tr>
                <tr>
                    <td colspan="4" style="text-align: center"><asp:Button id="btnEditNextOk" runat="server" Text="OK" OnClick="btnEditNextOk_Click" />
                        &nbsp;&nbsp;<asp:Button ID="btnEditNextCancel" runat="server" Text="Cancel" OnClick="btnEditNextCancel_Click" /></td>
                </tr>
                </table>
        </asp:View>
    </asp:MultiView>
    <asp:HiddenField id="hidBudgetId" runat="server" value="-1" />
    <asp:HiddenField id="hidShowBudgetList" runat="server" value="0" />
    <script>
        $(document).ready(function () {
            // init Bloodhound
            var payee_suggestions = new Bloodhound({
                datumTokenizer: Bloodhound.tokenizers.whitespace,
                queryTokenizer: Bloodhound.tokenizers.whitespace,
                local: [<%= this.Payees() %>]
            });

            // init Typeahead
            $('#<%: wddEditPayee.ClientID %>').typeahead(
                {
                    hint: true,
                    highlight: true,
                    minLength: 1
                },
                {
                    name: 'payee',
                    source: payee_suggestions   // suggestion engine is passed as the source
                }
            );

            // init Bloodhound
            var category_suggestions = new Bloodhound({
                datumTokenizer: Bloodhound.tokenizers.whitespace,
                queryTokenizer: Bloodhound.tokenizers.whitespace,
                local: [<%= this.Categories() %>]
            });

            // init Typeahead
            $('#<%: wddEditCategory.ClientID %>').typeahead({
                hint: true,
                highlight: true,
                minLength: 1
            },
                {
                    name: 'category',
                    source: category_suggestions   // suggestion engine is passed as the source
                });
            $('#<%: wddEditNextCategory.ClientID %>').typeahead({
                hint: true,
                highlight: true,
                minLength: 1
            },
                {
                    name: 'category',
                    source: category_suggestions   // suggestion engine is passed as the source
                });
        });
        var ctx = document.getElementById('myChart').getContext('2d');
        var myChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [<%= this.DataLabels() %>],
                datasets: [{
                    fill: false,
                    backgroundColor: 'rgb(54, 162, 235)',
                    borderColor: 'rgb(54, 162, 235)',
                    data: [<%= this.DataValues() %>]
                }]
            },
            options: {
                responsive: true,
                title: {
                    display: true,
                    text: 'Budget Forcast'
                },
                tooltips: {
                    mode: 'index',
                    intersect: false
                },
                hover: {
                    mode: 'nearest',
                    intersect: true
                },
                legend: {
                    display: false
                }
            }
        });

    </script>
</asp:Content>