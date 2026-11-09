using System.Globalization;

namespace ChequeWriter.Core;

/// <summary>The finished text for each part of a cheque.</summary>
public class ChequeText
{
    public string Date { get; init; } = "";
    public string Payee { get; init; } = "";
    public string Figures { get; init; } = "";
    public string Words { get; init; } = "";

    public static ChequeText Build(ChequeData data, BankLayout bank, AppSettings s)
    {
        var payee = (data.Payee ?? "").Trim();
        if (s.PayeeUpperCase) payee = payee.ToUpperInvariant();
        var words = AmountInWords.ForCheque(data.Amount, s);
        return new ChequeText
        {
            Date = data.Date.ToString(bank.DateFormat, CultureInfo.InvariantCulture),
            Payee = payee.Length == 0 ? "" : s.PayeeWrapper.Replace("{P}", payee),
            Figures = s.FiguresWrapper.Replace("{A}", AmountInWords.Figures(data.Amount)),
            Words = s.WordsWrapper.Replace("{W}", words),
        };
    }
}

public record WrapResult(string Line1, string Line2, bool Overflow);

public static class TextWrap
{
    /// <summary>
    /// Fills line 1 word by word up to <paramref name="width1"/>, puts the rest on line 2.
    /// <paramref name="measure"/> returns the printed width of a string in the same units as the widths.
    /// Overflow is true when the remaining text does not fit on line 2 either.
    /// </summary>
    public static WrapResult TwoLines(string text, double width1, double width2, Func<string, double> measure)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return new WrapResult("", "", false);

        var line1 = words[0];
        var i = 1;
        for (; i < words.Length; i++)
        {
            var candidate = line1 + " " + words[i];
            if (measure(candidate) > width1) break;
            line1 = candidate;
        }
        var line2 = string.Join(" ", words.Skip(i));
        var overflow = measure(line1) > width1 || (line2.Length > 0 && measure(line2) > width2);
        return new WrapResult(line1, line2, overflow);
    }
}
