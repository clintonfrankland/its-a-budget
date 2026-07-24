using System.Text;
using ClintonFrankland.Models.Entities;
using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public class TransactionCsvServiceTests
{
    private readonly TransactionCsvService _service = new();

    [Fact]
    public void Read_SuggestMappingAndPreview_HandlesQuotedBankValues()
    {
        var document = _service.Read("Posted Date,Description,Amount,Category\n07/01/2026,\"Store, Main\",($12.50),Food\n");
        var mapping = _service.SuggestMapping(document.Headers);
        var row = Assert.Single(_service.Preview(document, mapping));

        Assert.Equal("Posted Date", mapping.DateColumn);
        Assert.Equal("Amount", mapping.AmountColumn);
        Assert.Equal("Description", mapping.PayeeColumn);
        Assert.Equal(new DateOnly(2026, 7, 1), row.Date);
        Assert.Equal(-12.50m, row.Amount);
        Assert.Equal("Store, Main", row.Payee);
        Assert.Null(row.Error);
    }

    [Fact]
    public void Preview_ReportsInvalidRequiredValues()
    {
        var document = _service.Read("Date,Amount\nnot-a-date,wrong\n");
        var mapping = new TransactionCsvMapping("Date", "Amount", null, null);

        var row = Assert.Single(_service.Preview(document, mapping));

        Assert.Equal("Date is invalid.", row.Error);
    }

    [Fact]
    public void CreateCsv_WritesImportCompatibleEscapedTransactionRows()
    {
        var bytes = _service.CreateCsv([new Transaction
        {
            TransactionDate = new DateOnly(2026, 7, 2), Amount = -13.25m, Cleared = true,
            Payee = new Payee { PayeeName = "Store, Main" }, Category = new Category { CategoryName = "Food" }, Notes = "A \"note\""
        }]);
        var csv = Encoding.UTF8.GetString(bytes[Encoding.UTF8.GetPreamble().Length..]);

        Assert.Contains("Date,Amount,Payee,Category,Cleared,Notes", csv);
        Assert.Contains("07/02/2026,-13.25,\"Store, Main\",Food,Yes,\"A \"\"note\"\"\"", csv);
    }

    [Fact]
    public void Read_HandlesUtf8PreambleFromExport()
    {
        var document = _service.Read(Encoding.UTF8.GetString(_service.CreateCsv([])));

        Assert.Equal("Date", document.Headers[0]);
        Assert.Equal("Date", _service.SuggestMapping(document.Headers).DateColumn);
    }
}
