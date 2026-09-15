using ClintonFrankland.Services;

namespace ClintonFrankland.Blazor.Tests;

public class BankStatementImportServiceTests
{
    private readonly BankStatementImportService _service = new(new TransactionCsvService());
    private const string TransactionXml = "<STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20260722120000[-5:EST]</DTPOSTED><TRNAMT>-42.19</TRNAMT><FITID>abc-1</FITID><NAME>Market &amp; Cafe</NAME></STMTTRN>";

    private static string Statement(string transactions = TransactionXml, bool creditCard = false, string currency = "USD")
    {
        var statementTag = creditCard ? "CCSTMTRS" : "STMTRS";
        var accountTag = creditCard ? "CCACCTFROM" : "BANKACCTFROM";
        return $"<OFX><{statementTag}><CURDEF>{currency}</CURDEF><{accountTag}><ACCTID>ending-1234</ACCTID></{accountTag}><BANKTRANLIST><DTSTART>20260701</DTSTART><DTEND>20260731</DTEND>{transactions}</BANKTRANLIST></{statementTag}></OFX>";
    }

    [Theory]
    [InlineData("checking.ofx", false)]
    [InlineData("card.QFX", true)]
    public void ReadOfx_ImportsXmlPostedTransactionWithAccountAndDecodedPayee(string fileName, bool creditCard)
    {
        var document = _service.Read(fileName, "<?xml version=\"1.0\"?>" + Statement(creditCard: creditCard));
        var row = Assert.Single(document.Rows);
        Assert.Equal("OFX/QFX", document.Format);
        Assert.Equal("ending-1234", document.SourceAccount);
        Assert.Equal(new DateOnly(2026, 7, 22), row.Date);
        Assert.Equal(-42.19m, row.Amount);
        Assert.Equal("Market & Cafe", row.Payee);
        Assert.Equal("abc-1", row.ExternalId);
        Assert.True(row.Cleared);
        Assert.Null(row.Error);
    }

    [Fact]
    public void ReadOfx_ImportsLegacySgmlWithOnlyScalarEndTagsOmitted()
    {
        const string contents = "OFXHEADER:100\r\nDATA:OFXSGML\r\nVERSION:102\r\n\r\n<OFX><BANKMSGSRSV1><STMTTRNRS><TRNUID>0\n<STATUS><CODE>0\n<SEVERITY>INFO\n</STATUS><STMTRS><CURDEF>USD\n<BANKACCTFROM><BANKID>123\n<ACCTID>checking\n<ACCTTYPE>CHECKING\n</BANKACCTFROM><BANKTRANLIST><DTSTART>20260701\n<DTEND>20260731\n<STMTTRN><TRNTYPE>DEBIT\n<DTPOSTED>20260722\n<TRNAMT>-42.19\n<FITID>legacy-1\n<MEMO>Local store\n</STMTTRN></BANKTRANLIST><LEDGERBAL><BALAMT>12.00\n<DTASOF>20260731\n</LEDGERBAL></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
        var document = _service.Read("checking.ofx", contents);
        Assert.Equal("checking", document.SourceAccount);
        var row = Assert.Single(document.Rows);
        Assert.Equal(-42.19m, row.Amount);
        Assert.Equal("Local store", row.Payee);
        Assert.Null(row.Error);
    }

    [Fact]
    public void ReadOfx_PreservesPositiveAmountAndEveryRow()
    {
        var second = TransactionXml.Replace("abc-1", "abc-2").Replace("-42.19", "+1500.25");
        var rows = _service.Read("checking.ofx", Statement(TransactionXml + second)).Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal(1500.25m, rows[1].Amount);
        Assert.Equal(2, rows[1].SourceRow);
    }

    [Theory]
    [InlineData("<DTPOSTED>20260722120000[-5:EST]</DTPOSTED>", "<DTPOSTED>20260230</DTPOSTED>", "Date")]
    [InlineData("<DTPOSTED>20260722120000[-5:EST]</DTPOSTED>", "<DTPOSTED>20260722garbage</DTPOSTED>", "Date")]
    [InlineData("<DTPOSTED>20260722120000[-5:EST]</DTPOSTED>", "<DTPOSTED>20260722259999</DTPOSTED>", "Date")]
    [InlineData("<TRNAMT>-42.19</TRNAMT>", "<TRNAMT>not-money</TRNAMT>", "Amount")]
    [InlineData("<TRNAMT>-42.19</TRNAMT>", "<TRNAMT>1,2.00</TRNAMT>", "Amount")]
    public void ReadOfx_InvalidValueRemainsAnErrorRow(string original, string replacement, string error)
    {
        var rows = _service.Read("checking.ofx", Statement(TransactionXml.Replace(original, replacement))).Rows;
        Assert.Contains(error, Assert.Single(rows).Error);
    }

    [Theory]
    [InlineData("<FITID>abc-1</FITID>", "")]
    [InlineData("<FITID>abc-1</FITID>", "<FITID> </FITID>")]
    [InlineData("<TRNAMT>-42.19</TRNAMT>", "")]
    [InlineData("<DTPOSTED>20260722120000[-5:EST]</DTPOSTED>", "")]
    [InlineData("<FITID>abc-1</FITID>", "<FITID>abc-1</FITID><FITID>abc-2</FITID>")]
    [InlineData("<TRNAMT>-42.19</TRNAMT>", "<TRNAMT>-42.19</TRNAMT><TRNAMT>100</TRNAMT>")]
    public void ReadOfx_RejectsMissingOrDuplicateRequiredFields(string original, string replacement) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement(TransactionXml.Replace(original, replacement))));

    [Theory]
    [InlineData("<CORRECTFITID>old</CORRECTFITID>")]
    [InlineData("<CORRECTACTION>DELETE</CORRECTACTION>")]
    [InlineData("<ORIGCURRENCY><CURSYM>EUR</CURSYM></ORIGCURRENCY>")]
    [InlineData("<CURRENCY><CURSYM>EUR</CURSYM></CURRENCY>")]
    [InlineData("<BANKACCTTO><ACCTID>other</ACCTID></BANKACCTTO>")]
    public void ReadOfx_RejectsUnsupportedTransactionSemantics(string extra) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement(TransactionXml.Replace("</STMTTRN>", extra + "</STMTTRN>"))));

    [Fact]
    public void ReadOfx_RejectsDuplicateIdentifiers() =>
        Assert.Contains("duplicate FITID", Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement(TransactionXml + TransactionXml))).Message);

    [Fact]
    public void ReadOfx_RejectsMultipleStatementsEvenForSameAccount()
    {
        var statement = Statement().Replace("<OFX>", "").Replace("</OFX>", "");
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", "<OFX>" + statement + statement + "</OFX>"));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("")]
    public void ReadOfx_RejectsNonUsdOrMissingCurrency(string currency) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement(currency: currency)));

    [Theory]
    [InlineData("<ACCTID>ending-1234</ACCTID>", "")]
    [InlineData("</BANKACCTFROM>", "</BANKACCTFROM><BANKACCTFROM><ACCTID>other</ACCTID></BANKACCTFROM>")]
    [InlineData("</OFX>", "<INVSTMTRS /></OFX>")]
    [InlineData("</BANKTRANLIST>", "<STMTTRNP /></BANKTRANLIST>")]
    [InlineData("</OFX>", "<STMTTRN /></OFX>")]
    [InlineData("</STMTTRN>", "")]
    [InlineData("</BANKTRANLIST>", "")]
    [InlineData("<OFX>", "<OFX xmlns=\"urn:unsupported\">")]
    public void ReadOfx_RejectsMalformedOrUnsupportedStructure(string original, string replacement) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement().Replace(original, replacement)));

    [Fact]
    public void ReadOfx_RejectsDtdAndExternalEntityBeforeParsing() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", "<!DOCTYPE OFX [<!ENTITY remote SYSTEM 'file:///should-not-be-read'>]>" + Statement().Replace("Market &amp; Cafe", "&remote;")));

    [Fact]
    public void ReadOfx_RejectsEmptyTransactionList() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.ofx", Statement("")));

    [Theory]
    [InlineData("Bank", "07/23/2026", 2026)]
    [InlineData("CCard", "7/23'26", 2026)]
    [InlineData("Bank", "7/23/1999", 1999)]
    [InlineData("Bank", "7/23' 6", 2006)]
    [InlineData("Bank", "7/23'00", 2000)]
    public void ReadQif_ImportsSupportedAccountDateAndClearedState(string type, string date, int year)
    {
        var document = _service.Read("checking.qif", $"!Type:{type}\r\nD{date}\r\nT-1,012.50\r\nPBook Shop\r\nLShopping\r\nC*\r\nNref-2\r\n^\r\n");
        var row = Assert.Single(document.Rows);
        Assert.Equal("QIF", document.Format);
        Assert.Null(document.SourceAccount);
        Assert.Equal(new DateOnly(year, 7, 23), row.Date);
        Assert.Equal(-1012.50m, row.Amount);
        Assert.Equal("Book Shop", row.Payee);
        Assert.Equal("Shopping", row.Category);
        Assert.Equal("ref-2", row.ExternalId);
        Assert.True(row.Cleared);
        Assert.Null(row.Error);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("C\n", false)]
    [InlineData("Cx\n", true)]
    [InlineData("CX\n", true)]
    public void ReadQif_RespectsClearedMarkersAndMemoPayeeFallback(string marker, bool cleared)
    {
        var row = Assert.Single(_service.Read("checking.qif", $"!Type:Bank\nD7/23/2026\nT12.5\nMFallback\n{marker}^\n").Rows);
        Assert.Equal(cleared, row.Cleared);
        Assert.Equal("Fallback", row.Payee);
        Assert.Equal(12.5m, row.Amount);
    }

    [Theory]
    [InlineData("!Account\nNChecking\n^\n")]
    [InlineData("!Type:Invst\nD7/23/2026\nT12\n^\n")]
    [InlineData("!Type:Cat\nNShopping\n^\n")]
    [InlineData("D7/23/2026\nT12\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\n^\n!Type:CCard\nD7/24/2026\nT10\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nT13\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nSFood\n$12\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nL[Savings]\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nLFood/Work\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nC?\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\nT12\nAAddress\n^\n")]
    [InlineData("!Type:Bank\nD7/23/2026\n^\n")]
    [InlineData("!Type:Bank\n^\n")]
    [InlineData("!Type:Bank\n")]
    public void ReadQif_RejectsUnsupportedIncompleteOrAmbiguousRecords(string contents) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("checking.qif", contents));

    [Theory]
    [InlineData("7/23/26")]
    [InlineData("7/23/99")]
    [InlineData("23/7/2026")]
    [InlineData("7/23'2026")]
    public void ReadQif_RejectsAmbiguousOrUnsupportedDateRepresentations(string date)
    {
        var row = Assert.Single(_service.Read("checking.qif", $"!Type:Bank\nD{date}\nT12\n^\n").Rows);
        Assert.Null(row.Date);
        Assert.Equal("Date is invalid.", row.Error);
    }

    [Fact]
    public void ReadQif_DoesNotSilentlyDropInvalidLaterRows()
    {
        var rows = _service.Read("checking.qif", "!Type:Bank\nD7/23/2026\nT12\n^\nD2/30/2026\nTbad\n^\n").Rows;
        Assert.Equal(2, rows.Count);
        Assert.Null(rows[0].Error);
        Assert.Null(rows[1].Date);
        Assert.Null(rows[1].Amount);
        Assert.NotNull(rows[1].Error);
    }

    [Fact]
    public void ReadCsv_PreservesManualMappingAndUnclearedBehavior()
    {
        var document = _service.Read("checking.csv", "When,Value,Merchant\n07/23/2026,-12.50,Book Shop\n");
        Assert.Equal("CSV", document.Format);
        Assert.NotNull(document.CsvDocument);
        Assert.Equal("When", document.CsvDocument.Headers[0]);
        Assert.Single(document.Rows);
        Assert.False(document.Rows[0].Cleared);
        var preview = new TransactionCsvService().Preview(document.CsvDocument, new TransactionCsvMapping("When", "Value", "Merchant", null));
        Assert.Equal(-12.5m, Assert.Single(preview).Amount);
        Assert.Null(preview[0].Error);
    }

    [Theory]
    [InlineData("statement.txt", "data")]
    [InlineData("statement.ofx", "not an OFX statement")]
    public void Read_RejectsUnsupportedFiles(string name, string contents) =>
        Assert.Throws<InvalidOperationException>(() => _service.Read(name, contents));

    [Theory]
    [InlineData("10000000.00")]
    [InlineData("9999999.995")]
    [InlineData("79228162514264337593543950335")]
    [InlineData("-10000000.00")]
    public void Read_ReportsOutOfRangeAmountsBeforePersistence(string amount)
    {
        var ofx = Assert.Single(_service.Read("statement.ofx", Statement(TransactionXml.Replace("-42.19", amount))).Rows);
        var qif = Assert.Single(_service.Read("statement.qif", $"!Type:Bank\nD7/23/2026\nT{amount}\n^\n").Rows);
        Assert.Equal("Amount is invalid.", ofx.Error);
        Assert.Equal("Amount is invalid.", qif.Error);
        Assert.Null(ofx.Amount);
        Assert.Null(qif.Amount);
    }

    [Theory]
    [InlineData("-1.005", "-1.01")]
    [InlineData("1.005", "1.01")]
    [InlineData("9999999.994", "9999999.99")]
    public void Read_AppliesSameRoundingAsPersistence(string amount, string expected)
    {
        var row = Assert.Single(_service.Read("statement.ofx", Statement(TransactionXml.Replace("-42.19", amount))).Rows);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), row.Amount);
        Assert.Null(row.Error);
    }

    [Fact]
    public void ReadOfx_LegacyNormalizationDoesNotRepairMissingAggregateBoundaries()
    {
        var malformed = "OFXHEADER:100\n\n" + Statement().Replace("</STMTTRN>", "");
        Assert.Throws<InvalidOperationException>(() => _service.Read("statement.ofx", malformed));
    }

    [Fact]
    public void ReadOfx_XmlModeDoesNotRepairMissingScalarBoundaries() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("statement.ofx", Statement().Replace("</FITID>", "")));

    [Fact]
    public void ReadOfx_RejectsInvalidEntityWithoutFetchingAnything() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("statement.ofx", Statement().Replace("Market &amp; Cafe", "&external;")));

    [Fact]
    public void Read_EnforcesUtf8ByteLimitNotOnlyCharacterCount() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("statement.csv", new string('é', BankStatementImportService.MaximumFileBytes / 2 + 1)));

    [Fact]
    public void Read_RejectsOversizeBeforeParsing() =>
        Assert.Throws<InvalidOperationException>(() => _service.Read("statement.ofx", new string(' ', BankStatementImportService.MaximumFileBytes + 1)));
}
