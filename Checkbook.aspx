<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Checkbook.aspx.cs" Inherits="ClintonFrankland.Checkbook" MasterPageFile="~/ClintonFrankland.Master" %>
<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="server">
    <link href="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.css" rel="stylesheet">
    <script src="https://cdn.datatables.net/v/bs5/jszip-3.10.1/dt-2.1.3/b-3.1.1/b-html5-3.1.1/sp-2.3.1/sl-2.0.4/datatables.min.js"></script>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:Label ID="ErrorLabel" runat="server" CssClass="NormalRed" />
    <asp:MultiView ID="MainMultiview" runat="server">
        <asp:View ID="ListView" runat="server">
            <div class="pirate alert alert-success">Balance : <strong><asp:Label ID="lblBalanceTop" runat="server" Text="" /></strong>&nbsp;&nbsp;Cleared : <asp:Label ID="lblClearedTop" runat="server" Text="" /></div>
            <div id="BillsDiv" runat="server" class="pirate alert alert-warning" style="display: none;">You have bills due.</div>
            <div class="collapse" id="budgetCollapse" runat="server">
                <div class="container">
                    <div class="row">
                        <div class="col">
                            <h1>Budget Items</h1>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col">
                            <asp:DropDownList ID="ddlBudgetDays" runat="server" AutoPostBack="True" CssClass="ddl" OnSelectedIndexChanged="ddlBudgetDays_SelectedIndexChanged">
                                <asp:ListItem Value ="0">none</asp:ListItem>
                                <asp:ListItem Value="1">1 day</asp:ListItem>
                                <asp:ListItem Value="3" Selected="True">3 days</asp:ListItem>
                                <asp:ListItem Value="7">7 days</asp:ListItem>
                                <asp:ListItem Value="14">14 days</asp:ListItem>
                                <asp:ListItem Value="21">21 days</asp:ListItem>
                                <asp:ListItem Value="30">30 days</asp:ListItem>
                            </asp:DropDownList>
                            <asp:Panel ID="pnlBudgets" runat="server">
                                <asp:Repeater ID="rprForecast" runat="server" OnItemCommand="rprForecast_ItemCommand" OnItemDataBound="rprForecast_ItemDataBound">
                                    <HeaderTemplate>
                                    <table class="table table-striped table-sm">
                                        <thead>
                                            <tr>
                                                <th>Due Date</th>
                                                <th>&nbsp;</th>
                                                <th>Budget Name</th>
                                                <th>Category</th>
                                                <th>Frequency</th>
                                                <th>Mark Paid</th>
                                                <th>Amount</th>
                                                <th>Balance</th>
                                                <th></th>
                                            </tr>
                                        </thead>
                                    </HeaderTemplate>
                                    <ItemTemplate>
                                        <asp:HiddenField ID='hidCategoryId' runat='server' Value='<%# Eval("CategoryId")%>' />
                                        <asp:HiddenField ID="hidIsBill" runat="server" Value='<%# Eval("IsBill") %>' />
                                        <asp:HiddenField ID="hidIsAuto" runat="server" Value='<%# Eval("IsAuto") %>' />
                                        <asp:HiddenField ID="hidIsLate" runat="server" Value='<%# Eval("IsLate") %>' />
                                        <asp:HiddenField ID="hidPayee" runat="server" Value='<%# Eval("Payee") %>' />
                                        <tr>
                                            <td><asp:Label ID='lnkDueDate' runat='server' Text='<%# Eval("DueDate")%>' /></td>
                                            <td>
                                                <asp:Image runat="server" ID="imgIsBill" AlternateText="Is Bill" ImageUrl="~/images/150-icon_bill-shock.png" Width="16px" Height="16px" />
                                                <asp:Image runat="server" ID="imgNotIsBill" AlternateText="Is Bill" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                                                <asp:Image runat="server" ID="imgIsAuto" AlternateText="Is Automatic" ImageUrl="~/images/Letter-A-violet-icon.png" Width="16px" Height="16px" />
                                                <asp:Image runat="server" ID="imgNotIsAuto" AlternateText="Is Automatic" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                                                <asp:Image runat="server" ID="imgIsLate" AlternateText="Is Late" ImageUrl="~/images/Letter-L-black-icon.png" Width="16px" Height="16px" />
                                                <asp:Image runat="server" ID="imgNotIsLate" AlternateText="Is Late" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                                            </td>
                                            <td><asp:Label ID='btnBudgetName' runat='server' Text='<%# Eval("BudgetName")%>' /></td>
                                            <td><asp:Label ID='lnkCategory' runat='server' Text='<%# Eval("Category")%>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                                            <td><asp:Label ID='lnkFrequencyName' runat='server' Text='<%# Eval("FrequencyName")%>' /></td>
                                            <td><asp:ImageButton runat="server" ID="imgEditBudgetMarkPaid" AlternateText="Mark Paid" ImageUrl="~/images/unchecked.gif" Width="16px" Height="16px" CommandName="markpaid" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                                            <td><asp:Label ID='lnkAmount' runat='server' Text='<%# Eval("Amount", "{0:c}") %>' /></td>
                                            <td><asp:Label ID='lnkBalance' runat='server' Text='<%# Eval("Balance", "{0:c}") %>' /></td>
                                            <td>
                                                <div class="dropdown">
                                                  <button class="btn btn-primary" type="button" id='dropdownMenuButton<%# Eval("BudgetId") %>' data-bs-toggle="dropdown" aria-expanded="false"><i class="fas fa-bars"></i></button>
                                                  <ul class="dropdown-menu dropdown-menu-end" aria-labelledby='dropdownMenuButton<%# Eval("BudgetId") %>'>
                                                    <li><asp:LinkButton ID="lnkSkip" runat="server" CssClass="dropdown-item" CommandName="skip" CommandArgument='<%# Eval("BudgetId") %>'><i class="fas fa-check-circle"></i> Skip</asp:LinkButton></li>
                                                    <li><asp:LinkButton ID="lnkEditBudget" runat="server" CssClass="dropdown-item" CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>'><i class="fas fa-edit"></i> Edit</asp:LinkButton></li>
                                                    <li><asp:LinkButton ID="lnkEditNextBudget" runat="server" CssClass="dropdown-item" CommandName="editnext" CommandArgument='<%# Eval("BudgetId") %>'><i class="fas fa-edit"></i> Edit Next</asp:LinkButton></li>
                                                    <li><asp:LinkButton ID="lnkDeleteBudget" runat="server" CssClass="dropdown-item" CommandName="delete" CommandArgument='<%# Eval("BudgetId") %>'><i class="fas fa-trash"></i> Delete</asp:LinkButton></li>
                                                  </ul>
                                                </div>
                                            </td>
                                        </tr>
                                    </ItemTemplate>
                                    <FooterTemplate>
                                        </table>
                                    </FooterTemplate>
                                </asp:Repeater>
                            </asp:Panel>
                        </div>
                    </div>
                </div>
            </div>
            <div class="container">
                <div class="row">
                    <div class="col-3">
                        <h1>Checkbook</h1>
                    </div>
                    <div class="col-9 text-end">
                        <asp:LinkButton ID="lnkAddTransaction" runat="server" CssClass="btn btn-primary" OnClick="lnkAddTransaction_Click"><i class="fas fa-plus"></i> Add a Transaction</asp:LinkButton>
                        <a class="btn btn-primary" data-bs-toggle="collapse" href='#<% = budgetCollapse.ClientID %>' role="button" aria-expanded="false" aria-controls='<% = budgetCollapse.ClientID %>'><i class="fas fa-list"></i> Budget Items</a>
                    </div>
                </div>
            </div>
            <div class="container">
                <asp:Repeater ID="rprList" runat="server" OnItemCommand="rprList_ItemCommand">
                    <HeaderTemplate>
                <table class="table table-striped table-sm" id="checkbookTable">
                    <thead>
                        <tr>
                            <th>Date</th>
                            <th>Payee</th>
                            <th>Category</th>
                            <th>Clr</th>
                            <th>Amount</th>
                            <th>Balance</th>
                        </tr>
                    </thead>
                    <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                        <tr>
                            <td data-sort='<%# DateTime.Parse(Eval("TransactionDate")).ToString("yyyyMMdd") %>'><%# DateTime.Parse(Eval("TransactionDate")).ToString("MM/dd/yyyy") %></td>
                            <td><asp:LinkButton ID="PayeeNameLink" runat="server" CommandName="edit" CommandArgument='<%# Eval("TransactionId")%>'><%# Eval("PayeeName")%></asp:LinkButton></td>
                            <td><%# Eval("CategoryName")%></td>
                            <td data-search='<%# Eval("ShowCleared") %>'>
                                <asp:ImageButton ID='imgNotCleared' runat='server' ImageUrl='~/images/unchecked.gif' Width='16px' Height='16px' Visible='<%# Eval("ShowNotCleared")%>' CommandName='markcleared' CommandArgument='<%# Eval("TransactionId")%>' />
                                <asp:ImageButton ID='imgCleared' runat='server' ImageUrl='~/images/checked.gif' Width='16px' Height='16px' Visible='<%# Eval("ShowCleared") %>' CommandName='marknotcleared' CommandArgument='<%# Eval("TransactionId")%>' />
                            </td>
                            <td><%# Eval("Amount", "{0:c}") %></td>
                            <td><%# Eval("Balance", "{0:c}") %></td>
                        </tr>
                    </ItemTemplate>
                    <FooterTemplate>
                    </tbody>
                </table>
                    </FooterTemplate>
                </asp:Repeater>
            </div>
            <script>
                new DataTable('#checkbookTable', {
                    layout: {
                        top1: {
                            searchPanes: {
                                initCollapsed: true,
                                orderable: false,
                                preSelect: [
                                    {
                                        rows: ['false'],
                                        column: 3
                                    }
                                ],
                            }
                        },
                        topStart: {
                            buttons: ['excel', 'print']
                        }
                    },
                    searchPanes: {
                        layout: 'columns-3'
                    },
                    order: [[0, 'desc']],
                    paging: false,
                    columnDefs: [
                        {
                            searchPanes: {
                                show: true,
                            },
                            targets: [1, 2, 3]
                        },
                        {
                            searchPanes: {
                                show: false
                            },
                            targets: [0, 4, 5]
                        },
                    ],
                });
            </script>

            <asp:HiddenField ID="hidBudgetId" runat="server" Value="-1" />
            <asp:HiddenField id="hidTransactionId" runat="server" value="-1" />
        </asp:View>
        <asp:View ID="EditView" runat="server">
                            <table width="100%" border="0" cellpadding="3" cellspacing="0">
                                <tr>
                                    <td colspan="4">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td colspan="4">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%;">Date : </td>
                                    <td style="width: 50%">
                                        <asp:TextBox ID="wdpEditDate" runat="server" TextMode="Date"></asp:TextBox>
                                    </td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%; vertical-align: middle;">Payee : </td>
                                    <td style="width: 50%">
                                            <asp:TextBox id="wddEditPayee" CssClass="ui-autocomplete-input" runat="server" autocomplete="off"></asp:TextBox>
                                    </td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%; vertical-align: middle;">Amount : </td>
                                    <td style="width: 50%"><asp:TextBox ID="txtEditAmount" runat="server" Width="80px" onfocus="this.select();" /></td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%;">Type : </td>
                                    <td style="width: 50%">
                                        <asp:DropDownList ID="ddlTransactionType" runat="server" AutoPostBack="False">
                                            <asp:ListItem Value="0">Withdraw</asp:ListItem>
                                            <asp:ListItem Value="1">Deposit</asp:ListItem>
                                        </asp:DropDownList>
                                    </td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%;">Category : </td>
                                    <td style="width: 50%">
                                            <asp:TextBox id="wddEditCategory" CssClass="ui-autocomplete" runat="server" autocomplete="off"></asp:TextBox>
                                    </td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td style="text-align: right; width: 30%;">Cleared : </td>
                                    <td style="width: 50%"><asp:CheckBox ID="chkEditCleared" runat="server" /></td>
                                    <td style="width: 10%">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td colspan="4" style="text-align: center">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="width: 10%">&nbsp;</td>
                                    <td colspan="2" style="width: 80%">
                                        <asp:ImageButton ID="imgEditDelete" runat="server" ImageUrl="/images/delete.gif" Width="16px" Height="16px" BorderStyle="None" OnClick="imgEditDelete_Click" />
                                        <asp:LinkButton ID="lnkEditDelete" runat="server" Text="Delete this Transaction" OnClick="lnkEditDelete_Click" /><br />
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
        <asp:View ID="EditViewBudget" runat="server">
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
                                <td style="width: 50%"><asp:TextBox ID="txtEditBudgetAmount" runat="server" Width="80px" /></td>
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
                                <td style="width: 50%; vertical-align: middle; height: 28px;"><asp:CheckBox ID="chkIsBill" runat="server" AutoPostBack="True" /></td>
                                <td style="width: 10%">&nbsp;</td>
                            </tr>
                            <tr>
                                <td style="width: 10%">&nbsp;</td>
                                <td style="text-align: right; width: 30%;"><asp:Label ID="lblPayee" runat="server" Enabled="false" Text="Payee : " /></td>
                                <td style="width: 50%">
                                    <asp:TextBox id="txtEditBudgetPayee" runat="server" autocomplete="off"></asp:TextBox>
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
                                    <asp:TextBox id="txtEditBudgetCategory" runat="server" autocomplete="off"></asp:TextBox>
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
                                    <asp:CheckBox ID="chkEndDate" runat="server" AutoPostBack="True" />&nbsp;&nbsp;
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
                                    <asp:LinkButton ID="lnkEditBudgetDelete" runat="server" OnClick="lnkEditBudgetDelete_Click"><i class="fas fa-trash-alt"></i> Delete this Budget</asp:LinkButton><br />
                                    <asp:LinkButton ID="lnkEditBudgetEditNext" runat="server" OnClick="lnkEditBudgetEditNext_Click"><i class="fas fa-edit"></i> Edit Next</asp:LinkButton>
                                </td>
                                <td style="width: 10%">&nbsp;</td>
                            </tr>
                            <tr>
                                <td colspan="4">&nbsp;</td>
                            </tr>
                            <tr>
                                <td colspan="4" style="text-align: center"><asp:Button id="btnEditBudgetOk" runat="server" Text="OK" OnClick="btnEditBudgetOk_Click" />&nbsp;&nbsp;<asp:Button ID="brnEditBudgetCancel" runat="server" Text="Cancel" OnClick="brnEditBudgetCancel_Click" /></td>
                            </tr>
                            </table>
        </asp:View>
        <asp:View ID="DeleteBudgetView" runat="server">
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
        <asp:View ID="EditViewNextBudget" runat="server">
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
    <script>
        $(document).ready(function () {
            // init Bloodhound
            var payee_suggestions = new Bloodhound({
                datumTokenizer: Bloodhound.tokenizers.whitespace,
                queryTokenizer: Bloodhound.tokenizers.whitespace,
                local: [<%= Me.Payees %>]
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
            $('#<%: txtEditBudgetPayee.ClientID %>').typeahead(
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
                local: [<%= Me.Categories %>]
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
            $('#<%: txtEditBudgetCategory.ClientID %>').typeahead({
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
    </script>
</asp:Content>
