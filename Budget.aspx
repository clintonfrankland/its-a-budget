<%@ Page Language="C#" AutoEventWireup="false" CodeBehind="Budget.aspx.cs" Inherits="ClintonFrankland.Budget" MasterPageFile="~/ClintonFrankland.Master" %>
<asp:Content ID="headContent" ContentPlaceHolderID="head" runat="server">
    <style>
        .chart-container {
            width: 100%;
            max-width: 1232px;
            height: 330px;
            overflow-x: auto;
            padding: 10px 0;
            scrollbar-width: thin;
        }

        .chart-container::-webkit-scrollbar {
            height: 8px;
        }

        .chart-container::-webkit-scrollbar-thumb {
            background-color: #aaa;
            border-radius: 10px;
        }
    </style>
</asp:Content>
<asp:Content ID="BodyContent" ContentPlaceHolderID="BodyContent" runat="server">
    <asp:Label ID="ErrorLabel" runat="server" CssClass="NormalRed" />
    <asp:MultiView ID="MainMultiview" runat="server" ActiveViewIndex="0">
        <asp:View ID="vwForecast" runat="server">
            <div class="container">
                <div class="row">
                    <div class="col-3">
                        <h1>Budget Forcast</h1>
                    </div>
                    <div class="col-9 text-end">
                        <asp:LinkButton ID="AddBudgetLink" runat="server" ToolTip="Add Budget" OnClick="lnkAddBudget_Click" CssClass="btn btn-primary"><i class="fas fa-plus"></i> Add a Budget</asp:LinkButton>
                    </div>
                </div>
            </div>
            <div class="container mt-4">
                <div class="chart-container" style="position: relative; height: 300px; width: 100%; overflow-x: auto;">
                    <canvas id="myChart" style="min-width: 600px;" role="img" aria-label="Budget Forecast Line Chart"></canvas>
                </div>
            </div>
            <asp:Repeater ID="rprForecast" runat="server" OnItemCommand="rprForecast_ItemCommand" OnItemDataBound="rprForecast_ItemDataBound">
                <HeaderTemplate>
                <table class="table table-striped table-sm" id="budgetForcastTable">
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
                        </tr>
                    </thead>
                </HeaderTemplate>
                <ItemTemplate>
                    <asp:HiddenField ID="hidIsBill" runat="server" Value='<%# Eval("IsBill") %>' />
                    <asp:HiddenField ID="hidIsAuto" runat="server" Value='<%# Eval("IsAuto") %>' />
                    <asp:HiddenField ID="hidIsLate" runat="server" Value='<%# Eval("IsLate") %>' />
                    <tr">
                        <td data-order="<%# DateTime.Parse(Eval("DueDate").ToString()).ToString("yyyyMMddhhmmss") %>" ><asp:LinkButton ID='lnkDueDate' runat='server' Text='<%# Eval("DueDate") %>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td>
                            <asp:Image runat="server" ID="imgIsBill" AlternateText="Is Bill" ImageUrl="~/images/150-icon_bill-shock.png" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgNotIsBill" AlternateText="Is Bill" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgIsAuto" AlternateText="Is Automatic" ImageUrl="~/images/Letter-A-violet-icon.png" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgNotIsAuto" AlternateText="Is Automatic" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgIsLate" AlternateText="Is Late" ImageUrl="~/images/Letter-L-black-icon.png" Width="16px" Height="16px" />
                            <asp:Image runat="server" ID="imgNotIsLate" AlternateText="Is Late" ImageUrl="~/images/spacer.gif" Width="16px" Height="16px" />
                        </td>
                        <td><asp:LinkButton ID='btnBudgetName' runat='server' Text='<%# Eval("BudgetName")%>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td><asp:LinkButton ID='lnkCategory' runat='server' Text='<%# Eval("Category")%>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td><asp:LinkButton ID='lnkFrequencyName' runat='server' Text='<%# Eval("FrequencyName")%>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td><asp:ImageButton runat="server" ID="imgEditBudgetMarkPaid" AlternateText="Mark Paid" ImageUrl="~/images/unchecked.gif" Width="16px" Height="16px" CommandName="markpaid" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td><asp:LinkButton ID='lnkAmount' runat='server' Text='<%# Eval("Amount", "{0:c}") %>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                        <td><asp:LinkButton ID='lnkBalance' runat='server' Text='<%# Eval("Balance", "{0:c}") %>' CommandName="edit" CommandArgument='<%# Eval("BudgetId") %>' /></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                    </table>
                </FooterTemplate>
            </asp:Repeater>
            <script>
                new DataTable('#budgetForcastTable', {
                    responsive: true,
                    layout: {
                        top1: {
                            searchPanes: {
                                initCollapsed: true,
                                orderable: false,
                            }
                        },
                        topStart: {
                            buttons: ['copy', 'csv', 'excel']
                        }
                    },
                    searchPanes: {
                        layout: 'columns-3'
                    },
                    order: [[0, 'asc']],
                    paging: false,
                    columnDefs: [
                        {
                            searchPanes: {
                                show: true,
                            },
                            targets: [2, 3, 4]
                        },
                        {
                            searchPanes: {
                                show: false
                            },
                            targets: [0, 1, 5, 6, 7]
                        },
                        { targets: [2, 6, 7], responsivePriority: 1 }, // Always visible columns
                        { targets: [0], responsivePriority: 2 }, // Hidden on small screens
                        { targets: [1, 3, 4, 5], responsivePriority: 3 } // Hidden on small screens
                    ],
                });
            </script>
            <script>
                document.addEventListener('DOMContentLoaded', function () {
                    const ctx = document.getElementById('myChart').getContext('2d');

                    // Data values for the chart
                    const dataValues = [<%= this.DataValues() %>];

                    // Labels for the chart
                    const dataLabels = [<%= this.DataLabels() %>];

                    // Set point colors: Blue for >= 0, Orange for < 0
                    const pointColors = dataValues.map(value => value < 0 ? '#FF8C00' : '#36A2EB');

                    const myChart = new Chart(ctx, {
                        type: 'line',
                        data: {
                            labels: dataLabels,
                            datasets: [{
                                label: 'Budget Forecast',
                                data: dataValues,
                                fill: false,
                                borderColor: '#36A2EB',
                                tension: 0.4,
                                pointRadius: 5,
                                pointHoverRadius: 7,
                                pointBackgroundColor: pointColors
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false, // Prevent aspect ratio enforcement
                            plugins: {
                                title: {
                                    display: true,
                                    text: 'Budget Forecast'
                                },
                                tooltip: {
                                    backgroundColor: '#333',  // Keep tooltip background dark
                                    titleColor: '#ffffff',
                                    bodyColor: '#ffffff',
                                    bodyFont: {
                                        weight: 'bold'
                                    },
                                    callbacks: {
                                        label: function (tooltipItem) {
                                            let value = tooltipItem.raw;
                                            let color = value < 0 ? '#FF8C00' : '#36A2EB';
                                            return `● $${Math.abs(value).toFixed(2)}`;
                                        },
                                        labelColor: function (tooltipItem) {
                                            let value = tooltipItem.raw;
                                            let color = value < 0 ? '#FF8C00' : '#36A2EB';
                                            return {
                                                borderColor: color,
                                                backgroundColor: color
                                            };
                                        }
                                    }
                                },
                                legend: {
                                    display: false
                                }
                            },
                            interaction: {
                                mode: 'nearest',
                                intersect: true
                            },
                            scales: {
                                x: {
                                    title: {
                                        display: true,
                                        text: 'Time'
                                    }
                                },
                                y: {
                                    title: {
                                        display: true,
                                        text: 'Amount ($)'
                                    }
                                }
                            }
                        }
                    });
                    ctx.canvas.width = 1232;
                    ctx.canvas.height = 308;                });
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
    </script>
</asp:Content>