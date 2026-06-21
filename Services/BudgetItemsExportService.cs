using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ClintonFrankland.Models;

namespace ClintonFrankland.Services;

public class BudgetItemsExportService
{
    private static readonly CultureInfo ExportCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] Headers =
    [
        "Budget Name",
        "Type",
        "Category",
        "Next Due Date",
        "End Date",
        "Frequency",
        "Amount",
        "Monthly Amount",
        "Bill",
        "Auto-Pay",
        "Late",
        "Payee"
    ];

    public string CreateFileName(string extension) =>
        $"budget-items-{DateTime.Today:yyyyMMdd}.{extension.TrimStart('.')}";

    public byte[] CreateCsv(IEnumerable<BudgetItemViewModel> items)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, Headers);

        foreach (var item in items)
            AppendCsvRow(builder, GetTextValues(item));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public byte[] CreateExcel(IEnumerable<BudgetItemViewModel> items)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Budget Items");

        for (var column = 0; column < Headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = Headers[column];

        var row = 2;
        foreach (var item in items)
        {
            worksheet.Cell(row, 1).Value = item.BudgetName;
            worksheet.Cell(row, 2).Value = item.Type;
            worksheet.Cell(row, 3).Value = item.Category;
            worksheet.Cell(row, 4).Value = item.DueDate;
            worksheet.Cell(row, 4).Style.DateFormat.Format = "mm/dd/yyyy";
            worksheet.Cell(row, 5).Value = item.EndDateName;
            worksheet.Cell(row, 6).Value = item.FrequencyName;
            worksheet.Cell(row, 7).Value = item.Amount;
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 8).Value = item.Monthly;
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "$#,##0.00";
            worksheet.Cell(row, 9).Value = FormatBoolean(item.IsBill);
            worksheet.Cell(row, 10).Value = FormatBoolean(item.IsAuto);
            worksheet.Cell(row, 11).Value = FormatBoolean(item.IsLate);
            worksheet.Cell(row, 12).Value = item.Payee;
            row++;
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[] GetTextValues(BudgetItemViewModel item) =>
    [
        item.BudgetName,
        item.Type,
        item.Category,
        item.DueDate.ToString("MM/dd/yyyy", ExportCulture),
        item.EndDateName,
        item.FrequencyName,
        item.Amount.ToString("C", ExportCulture),
        item.Monthly.ToString("C", ExportCulture),
        FormatBoolean(item.IsBill),
        FormatBoolean(item.IsAuto),
        FormatBoolean(item.IsLate),
        item.Payee
    ];

    private static string FormatBoolean(bool value) => value ? "Yes" : "No";

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string> values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsvValue)));
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
