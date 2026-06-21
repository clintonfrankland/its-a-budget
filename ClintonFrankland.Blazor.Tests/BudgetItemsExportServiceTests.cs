using System.Text;
using ClosedXML.Excel;
using ClintonFrankland.Models;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public class BudgetItemsExportServiceTests
{
    private readonly BudgetItemsExportService _service = new();

    [Fact]
    public void CreateCsv_WritesHeaderAndEscapedRows()
    {
        var csv = DecodeCsv(_service.CreateCsv([CreateBudgetItem()]));

        Assert.Contains("Budget Name,Type,Category,Next Due Date,End Date,Frequency,Amount,Monthly Amount,Bill,Auto-Pay,Late,Payee", csv);
        Assert.Contains("\"Rent, primary\",Expense,Housing,07/01/2026,,Monthly,\"$1,250.50\",\"-$1,250.50\",Yes,Yes,No,\"Landlord \"\"Main\"\"\"", csv);
    }

    [Fact]
    public void CreateCsv_WithNoRows_WritesOnlyHeaders()
    {
        var csv = DecodeCsv(_service.CreateCsv([]));
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.Equal("Budget Name,Type,Category,Next Due Date,End Date,Frequency,Amount,Monthly Amount,Bill,Auto-Pay,Late,Payee", lines[0]);
    }

    [Fact]
    public void CreateExcel_WritesReadableWorksheet()
    {
        using var stream = new MemoryStream(_service.CreateExcel([CreateBudgetItem()]));
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Budget Items");

        Assert.Equal("Budget Name", worksheet.Cell(1, 1).GetString());
        Assert.Equal("Rent, primary", worksheet.Cell(2, 1).GetString());
        Assert.Equal("Expense", worksheet.Cell(2, 2).GetString());
        Assert.Equal("Housing", worksheet.Cell(2, 3).GetString());
        Assert.Equal(new DateTime(2026, 7, 1), worksheet.Cell(2, 4).GetDateTime());
        Assert.Equal(1250.50m, worksheet.Cell(2, 7).GetValue<decimal>());
        Assert.Equal(-1250.50m, worksheet.Cell(2, 8).GetValue<decimal>());
        Assert.Equal("Yes", worksheet.Cell(2, 9).GetString());
        Assert.Equal("Landlord \"Main\"", worksheet.Cell(2, 12).GetString());
    }

    [Fact]
    public void CreateExcel_WithNoRows_WritesHeadersOnly()
    {
        using var stream = new MemoryStream(_service.CreateExcel([]));
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Budget Items");

        Assert.Equal("Budget Name", worksheet.Cell(1, 1).GetString());
        Assert.True(worksheet.Row(2).IsEmpty());
    }

    private static string DecodeCsv(byte[] bytes)
    {
        var preambleLength = Encoding.UTF8.GetPreamble().Length;
        return Encoding.UTF8.GetString(bytes[preambleLength..]);
    }

    private static BudgetItemViewModel CreateBudgetItem() => new()
    {
        BudgetId = 10,
        BudgetName = "Rent, primary",
        Type = "Expense",
        Category = "Housing",
        DueDate = new DateTime(2026, 7, 1),
        EndDateName = string.Empty,
        FrequencyName = "Monthly",
        Amount = 1250.50m,
        Monthly = -1250.50m,
        IsBill = true,
        IsAuto = true,
        IsLate = false,
        Payee = "Landlord \"Main\""
    };
}
