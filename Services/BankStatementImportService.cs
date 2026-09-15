using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ClintonFrankland.Services;

/// <summary>Reads a deliberately limited, review-first subset of bank statement formats.</summary>
public sealed partial class BankStatementImportService(TransactionCsvService transactionCsvService)
{
    public const int MaximumFileBytes = 5 * 1024 * 1024;
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly HashSet<string> LegacyScalarTags = new(StringComparer.Ordinal)
    {
        "CODE", "SEVERITY", "MESSAGE", "DTSERVER", "LANGUAGE", "DTPROFUP", "DTACCTUP", "FIID", "ORG", "FID",
        "INTU.BID", "INTU.USERID", "TRNUID", "CLTCOOKIE", "CURDEF", "BANKID", "BRANCHID", "ACCTID", "ACCTTYPE",
        "ACCTKEY", "DTSTART", "DTEND", "TRNTYPE", "DTPOSTED", "DTUSER", "DTAVAIL", "TRNAMT", "FITID", "NAME",
        "MEMO", "CHECKNUM", "REFNUM", "SIC", "PAYEEID", "SRVRTID", "BALAMT", "DTASOF", "MKTGINFO",
        "CORRECTFITID", "CORRECTACTION", "CURRENCY", "ORIGCURRENCY"
    };

    public BankStatementDocument Read(string fileName, string contents)
    {
        if (contents is null || contents.Length > MaximumFileBytes || Encoding.UTF8.GetByteCount(contents) > MaximumFileBytes)
            throw new InvalidOperationException("Choose a statement no larger than 5 MiB.");
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => ReadCsv(contents),
            ".ofx" or ".qfx" => ReadOfx(contents),
            ".qif" => ReadQif(contents),
            _ => throw new InvalidOperationException("Choose a CSV, OFX, QFX, or QIF statement.")
        };
    }

    private BankStatementDocument ReadCsv(string contents)
    {
        var document = transactionCsvService.Read(contents);
        var mapping = transactionCsvService.SuggestMapping(document.Headers);
        var rows = transactionCsvService.Preview(document, mapping, int.MaxValue)
            .Select(row => new BankStatementRow(row.SourceRow, row.Date, row.Amount, row.Payee, row.Category, false, null, row.Error)).ToList();
        return new BankStatementDocument("CSV", rows, document);
    }

    private static BankStatementDocument ReadOfx(string contents)
    {
        var document = ReadOfxXml(contents);
        var root = document.Root;
        if (root is null || root.Name != "OFX" || root.Descendants().Any(element => element.Name.Namespace != XNamespace.None))
            throw new InvalidOperationException("Expected an OFX bank or credit-card statement without XML namespaces.");
        var statements = root.Descendants().Where(element => element.Name.LocalName is "STMTRS" or "CCSTMTRS").ToList();
        if (statements.Count != 1 || root.Descendants().Any(element => element.Name.LocalName is "INVSTMTRS" or "STMTTRNP" or "INVBANKTRAN"))
            throw new InvalidOperationException("Import exactly one bank or credit-card statement; investment and pending transactions are unsupported.");
        var statement = statements[0];
        if (!string.Equals(RequiredValue(statement, "CURDEF"), "USD", StringComparison.Ordinal))
            throw new InvalidOperationException("Only USD statements are supported.");
        var accounts = root.Descendants().Where(element => element.Name.LocalName is "BANKACCTFROM" or "CCACCTFROM").ToList();
        var expectedAccountName = statement.Name == "STMTRS" ? "BANKACCTFROM" : "CCACCTFROM";
        if (accounts.Count != 1 || accounts[0].Parent != statement || accounts[0].Name != expectedAccountName)
            throw new InvalidOperationException("The statement must identify exactly one source account.");
        var sourceAccount = RequiredValue(accounts[0], "ACCTID");
        var lists = statement.Elements("BANKTRANLIST").ToList();
        if (lists.Count != 1)
            throw new InvalidOperationException("The statement must contain exactly one transaction list.");
        var transactions = lists[0].Elements("STMTTRN").ToList();
        if (transactions.Count == 0 || root.Descendants("STMTTRN").Count() != transactions.Count)
            throw new InvalidOperationException("The statement has no transactions or has transactions outside its account list.");
        if (lists[0].Elements().Any(element => element.Name.LocalName is not ("DTSTART" or "DTEND" or "STMTTRN")))
            throw new InvalidOperationException("The statement transaction list contains unsupported records.");
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<BankStatementRow>();
        foreach (var transaction in transactions)
        {
            if (transaction.Descendants().Any(element => element.Name.LocalName is "CORRECTFITID" or "CORRECTACTION" or "CURRENCY" or "ORIGCURRENCY" or "BANKACCTTO" or "CCACCTTO"))
                throw new InvalidOperationException("Correction, foreign-currency, and linked-account transfer records are unsupported.");
            var identifier = RequiredValue(transaction, "FITID");
            if (!identifiers.Add(identifier))
                throw new InvalidOperationException("The statement contains a duplicate FITID; no transactions were imported.");
            var dateText = RequiredValue(transaction, "DTPOSTED");
            var amountText = RequiredValue(transaction, "TRNAMT");
            var dateValid = TryParseOfxDate(dateText, out var date);
            var amountValid = TryParseAmount(amountText, out var amount);
            var payee = OptionalValue(transaction, "NAME");
            if (string.IsNullOrWhiteSpace(payee))
                payee = OptionalValue(transaction, "MEMO");
            rows.Add(new BankStatementRow(rows.Count + 1, dateValid ? date : null, amountValid ? amount : null,
                payee, string.Empty, true, identifier, !dateValid ? "Date is invalid." : !amountValid ? "Amount is invalid." : null));
        }
        return new BankStatementDocument("OFX/QFX", rows, SourceAccount: sourceAccount);
    }

    private static XDocument ReadOfxXml(string contents)
    {
        if (contents.Contains("<!", StringComparison.Ordinal))
            throw new InvalidOperationException("OFX declarations, DTDs, comments, and external entities are unsupported.");
        var text = contents.TrimStart('\ufeff', ' ', '\r', '\n', '\t');
        if (text.StartsWith("OFXHEADER:", StringComparison.Ordinal))
        {
            var start = text.IndexOf("<OFX>", StringComparison.Ordinal);
            if (start < 0 || !text[..start].Split('\n').Any(line => line.Trim() == "OFXHEADER:100"))
                throw new InvalidOperationException("Expected a legacy OFXHEADER:100 statement.");
            text = NormalizeLegacyOfx(text[start..]);
        }
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumFileBytes,
                IgnoreWhitespace = true
            });
            var document = XDocument.Load(reader);
            if (document.Descendants().Any(element => element.HasElements && element.Nodes().OfType<XText>().Any(value => !string.IsNullOrWhiteSpace(value.Value))))
                throw new InvalidOperationException("Malformed OFX aggregate text.");
            return document;
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException("The OFX/QFX document is malformed or uses unsupported structure.", exception);
        }
    }

    // OFX 1.x omits scalar closing tags, but aggregate boundaries must remain explicit.
    private static string NormalizeLegacyOfx(string contents)
    {
        return LegacyScalarRegex().Replace(contents, match =>
        {
            var tag = match.Groups["tag"].Value;
            if (!LegacyScalarTags.Contains(tag))
                return match.Value;
            var next = match.Index + match.Length;
            if (contents.AsSpan(next).StartsWith($"</{tag}>", StringComparison.Ordinal))
                return match.Value;
            return match.Value + $"</{tag}>";
        });
    }

    private static string RequiredValue(XElement parent, string name)
    {
        var value = OptionalValue(parent, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"The OFX statement is missing required {name}.");
        return value;
    }

    private static string OptionalValue(XElement parent, string name)
    {
        var elements = parent.Elements(name).ToList();
        if (elements.Count > 1 || elements.Any(element => element.HasElements))
            throw new InvalidOperationException($"The OFX statement has duplicate or malformed {name} fields.");
        return elements.SingleOrDefault()?.Value.Trim() ?? string.Empty;
    }

    private static BankStatementDocument ReadQif(string contents)
    {
        var rows = new List<BankStatementRow>();
        var fields = new Dictionary<char, string>();
        var hasHeader = false;
        var sourceRow = 0;
        foreach (var rawLine in contents.TrimStart('\ufeff').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            sourceRow++;
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
                continue;
            if (!hasHeader)
            {
                if (line is not ("!Type:Bank" or "!Type:CCard"))
                    throw new InvalidOperationException("QIF must begin with !Type:Bank or !Type:CCard; other sections are unsupported.");
                hasHeader = true;
                continue;
            }
            if (line.StartsWith('!'))
                throw new InvalidOperationException("QIF account definitions and multiple sections are unsupported; export one account only.");
            if (line == "^")
            {
                if (fields.Count == 0)
                    throw new InvalidOperationException("QIF contains an empty transaction record.");
                rows.Add(ReadQifRow(fields, sourceRow));
                fields.Clear();
                continue;
            }
            if (line[0] is 'S' or 'E' or '$')
                throw new InvalidOperationException("QIF split transactions are unsupported.");
            if (line[0] is not ('D' or 'T' or 'P' or 'L' or 'C' or 'N' or 'M'))
                throw new InvalidOperationException($"QIF field '{line[0]}' is unsupported.");
            if (!fields.TryAdd(line[0], line[1..].Trim()))
                throw new InvalidOperationException($"QIF contains duplicate '{line[0]}' fields.");
        }
        if (fields.Count != 0)
            throw new InvalidOperationException("Every QIF transaction must end with ^; the final record is incomplete.");
        if (rows.Count == 0)
            throw new InvalidOperationException("The QIF file contains no bank transactions.");
        return new BankStatementDocument("QIF", rows);
    }

    private static BankStatementRow ReadQifRow(IReadOnlyDictionary<char, string> fields, int sourceRow)
    {
        if (!fields.TryGetValue('D', out var dateText) || !fields.TryGetValue('T', out var amountText))
            throw new InvalidOperationException("Every QIF transaction requires Date (D) and Amount (T).");
        var category = fields.GetValueOrDefault('L') ?? string.Empty;
        if (category.Contains('[') || category.Contains(']') || category.Contains('/'))
            throw new InvalidOperationException("QIF account transfers and category classes are unsupported.");
        var cleared = fields.GetValueOrDefault('C') ?? string.Empty;
        if (cleared is not ("" or "*" or "X" or "x"))
            throw new InvalidOperationException("QIF contains an unsupported cleared-state marker.");
        var dateValid = TryParseQifDate(dateText, out var date);
        var amountValid = TryParseAmount(amountText, out var amount);
        return new BankStatementRow(sourceRow, dateValid ? date : null, amountValid ? amount : null,
            fields.GetValueOrDefault('P') ?? fields.GetValueOrDefault('M') ?? string.Empty, category,
            cleared is "*" or "X" or "x", fields.GetValueOrDefault('N'),
            !dateValid ? "Date is invalid." : !amountValid ? "Amount is invalid." : null);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        amount = default;
        if (!AmountRegex().IsMatch(value) || !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out amount) || !CurrencyPolicy.FitsSqlDecimal(amount, CurrencyPolicy.TransactionPrecision))
            return false;
        amount = CurrencyPolicy.RoundSignedSqlAmount(amount, CurrencyPolicy.TransactionPrecision);
        return true;
    }

    private static bool TryParseOfxDate(string value, out DateOnly date)
    {
        date = default;
        return OfxDateRegex().IsMatch(value) && DateOnly.TryParseExact(value[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date != DateOnly.MinValue;
    }

    private static bool TryParseQifDate(string value, out DateOnly date)
    {
        date = default;
        var match = QifDateRegex().Match(value);
        if (!match.Success)
            return false;
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        if (match.Groups["separator"].Value == "'")
            year += 2000;
        return DateOnly.TryParseExact($"{match.Groups["month"].Value}/{match.Groups["day"].Value}/{year:D4}",
            "M/d/yyyy", UsCulture, DateTimeStyles.None, out date) && date != DateOnly.MinValue;
    }

    [GeneratedRegex(@"<(?<tag>[A-Z][A-Z0-9.]*)>(?<value>[^<]*)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex LegacyScalarRegex();
    [GeneratedRegex(@"^[+-]?(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]+)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AmountRegex();
    [GeneratedRegex(@"^[0-9]{8}(?:(?:[01][0-9]|2[0-3])[0-5][0-9][0-5][0-9](?:\.[0-9]{1,3})?(?:\[[+-]?[0-9]{1,2}(?:\.[0-9]+)?(?::[A-Za-z]+)?\])?)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OfxDateRegex();
    [GeneratedRegex(@"^(?<month>[0-9]{1,2})/(?<day>[0-9]{1,2})(?:(?<separator>/)(?<year>[0-9]{4})|(?<separator>')\s*(?<year>[0-9]{1,2}))$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex QifDateRegex();
}

public sealed record BankStatementDocument(string Format, IReadOnlyList<BankStatementRow> Rows, TransactionCsvDocument? CsvDocument = null, string? SourceAccount = null);
public sealed record BankStatementRow(int SourceRow, DateOnly? Date, decimal? Amount, string Payee, string Category, bool Cleared, string? ExternalId, string? Error);
