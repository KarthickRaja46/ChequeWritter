using System.Globalization;
using System.Text;

namespace ChequeWriter.Core;

public static class CsvExport
{
    public static string Register(IEnumerable<ChequeRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Cheque No,Bank,Cheque Date,Payee,Amount,Amount in Words,A/C Payee,Status,Printed At,Notes");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Q(r.ChequeNo), Q(r.BankName), Q(r.ChequeDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)), Q(r.Payee),
                r.Amount.ToString("0.00", CultureInfo.InvariantCulture), Q(r.AmountInWords), r.AcPayeeOnly ? "Yes" : "No",
                r.Status.ToString(), Q(r.PrintedAt.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)), Q(r.Notes)));
        }
        return sb.ToString();
    }

    /// <summary>Quotes a CSV value and neutralises spreadsheet formulas (=, +, -, @ at the start).</summary>
    static string Q(string? v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
