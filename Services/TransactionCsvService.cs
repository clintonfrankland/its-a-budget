using System.Globalization;
using System.Text;
using ClintonFrankland.Models.Entities;

namespace ClintonFrankland.Services;

public sealed class TransactionCsvService
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] ExportHeaders = ["Date", "Amount", "Payee", "Category", "Cleared", "Notes"];

    public TransactionCsvDocument Read(string csv)
    {
        var rows = ReadRows(csv);
        if (rows.Count == 0)
            return new TransactionCsvDocument([], []);

        var headers = rows[0].Select((header, index) => string.IsNullOrWhiteSpace(header) ? $"Column {index + 1}" : header.Trim()).ToList();
        if (headers.Count > 0)
            headers[0] = headers[0].TrimStart('\ufeff');
        return new TransactionCsvDocument(headers, rows.Skip(1).Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value))).ToList());
    }

    public TransactionCsvMapping SuggestMapping(IReadOnlyList<string> headers) => new(
        FindHeader(headers, "date", "transaction date", "posted date"),
        FindHeader(headers, "amount", "transaction amount", "value"),
        FindHeader(headers, "payee", "description", "merchant", "name"),
        FindHeader(headers, "category", "type"));

    public IReadOnlyList<TransactionCsvPreviewRow> Preview(TransactionCsvDocument document, TransactionCsvMapping mapping, int maximumRows = 10)
    {
        var previews = new List<TransactionCsvPreviewRow>();
        foreach (var (values, index) in document.Rows.Take(maximumRows).Select((row, index) => (row, index + 2)))
        {
            var dateText = GetValue(document.Headers, values, mapping.DateColumn);
            var amountText = GetValue(document.Headers, values, mapping.AmountColumn);
            var dateValid = TryParseDate(dateText, out var date);
            var amountValid = TryParseAmount(amountText, out var amount);
            var error = !dateValid ? "Date is invalid." : !amountValid ? "Amount is invalid." : null;
            previews.Add(new TransactionCsvPreviewRow(index, dateValid ? date : null, amountValid ? amount : null,
                GetValue(document.Headers, values, mapping.PayeeColumn), GetValue(document.Headers, values, mapping.CategoryColumn), error));
        }
        return previews;
    }

    public byte[] CreateCsv(IEnumerable<Transaction> transactions)
    {
        var builder = new StringBuilder();
        AppendRow(builder, ExportHeaders);
        foreach (var transaction in transactions)
        {
            AppendRow(builder,
            [
                transaction.TransactionDate.ToString("MM/dd/yyyy", UsCulture),
                transaction.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                transaction.Payee?.PayeeName ?? string.Empty,
                transaction.Category?.CategoryName ?? string.Empty,
                transaction.Cleared ? "Yes" : "No",
                transaction.Notes ?? string.Empty
            ]);
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public string CreateFileName() => $"transactions-{DateTime.Today:yyyyMMdd}.csv";

    private static string? FindHeader(IReadOnlyList<string> headers, params string[] candidates) =>
        headers.FirstOrDefault(header => candidates.Any(candidate => string.Equals(header.Trim(), candidate, StringComparison.OrdinalIgnoreCase)));

    private static string GetValue(IReadOnlyList<string> headers, IReadOnlyList<string> values, string? column)
    {
        var columnIndex = column is null ? -1 : headers.Select((header, index) => (header, index))
            .FirstOrDefault(item => string.Equals(item.header, column, StringComparison.Ordinal)).index;
        return columnIndex < 0 ? string.Empty : values.ElementAtOrDefault(columnIndex) ?? string.Empty;
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParse(value.Trim(), UsCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
        DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Trim().Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowParentheses, UsCulture, out amount);
    }

    private static List<List<string>> ReadRows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"') { value.Append(character); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { row.Add(value.ToString()); value.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                row.Add(value.ToString()); value.Clear(); rows.Add(row); row = new List<string>();
            }
            else value.Append(character);
        }
        if (value.Length > 0 || row.Count > 0) { row.Add(value.ToString()); rows.Add(row); }
        return rows;
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string> values) =>
        builder.AppendLine(string.Join(',', values.Select(Escape)));

    private static string Escape(string value) => value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n')
        ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}

public sealed record TransactionCsvDocument(IReadOnlyList<string> Headers, IReadOnlyList<List<string>> Rows);
public sealed class TransactionCsvMapping
{
    public TransactionCsvMapping(string? dateColumn, string? amountColumn, string? payeeColumn, string? categoryColumn)
    {
        DateColumn = dateColumn;
        AmountColumn = amountColumn;
        PayeeColumn = payeeColumn;
        CategoryColumn = categoryColumn;
    }

    public string? DateColumn { get; set; }
    public string? AmountColumn { get; set; }
    public string? PayeeColumn { get; set; }
    public string? CategoryColumn { get; set; }
}
public sealed record TransactionCsvPreviewRow(int SourceRow, DateOnly? Date, decimal? Amount, string Payee, string Category, string? Error);
