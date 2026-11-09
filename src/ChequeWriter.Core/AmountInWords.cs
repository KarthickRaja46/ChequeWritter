using System.Globalization;

namespace ChequeWriter.Core;

/// <summary>English number words in the style used on UAE cheques ("Four Thousand Five Hundred Twenty Three").</summary>
public static class AmountInWords
{
    public const decimal MaxAmount = 999_999_999_999.99m;

    static readonly string[] Ones =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    };

    static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    static readonly (long Value, string Name)[] Scales =
    {
        (1_000_000_000, "Billion"), (1_000_000, "Million"), (1_000, "Thousand"),
    };

    /// <summary>Whole number in words, e.g. 4523 -> "Four Thousand Five Hundred Twenty Three".</summary>
    public static string Number(long n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n == 0) return Ones[0];
        var parts = new List<string>();
        foreach (var (value, name) in Scales)
        {
            if (n >= value)
            {
                parts.Add(UnderThousand((int)(n / value)) + " " + name);
                n %= value;
            }
        }
        if (n > 0) parts.Add(UnderThousand((int)n));
        return string.Join(" ", parts);
    }

    static string UnderThousand(int n)
    {
        var parts = new List<string>();
        if (n >= 100)
        {
            parts.Add(Ones[n / 100] + " Hundred");
            n %= 100;
        }
        if (n >= 20)
        {
            parts.Add(n % 10 == 0 ? Tens[n / 10] : Tens[n / 10] + " " + Ones[n % 10]);
        }
        else if (n > 0)
        {
            parts.Add(Ones[n]);
        }
        return string.Join(" ", parts);
    }

    /// <summary>Splits an amount into whole units and hundredths (fils), rounding to 2 decimals.</summary>
    public static (long Whole, int Fils) Split(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        if (amount > MaxAmount) throw new ArgumentOutOfRangeException(nameof(amount), "Amount is too large.");
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var whole = (long)decimal.Truncate(rounded);
        var fils = (int)((rounded - whole) * 100);
        return (whole, fils);
    }

    /// <summary>Full words for the cheque, using the settings templates (without the wrapper).</summary>
    public static string ForCheque(decimal amount, AppSettings s)
    {
        var (whole, fils) = Split(amount);
        var d = Number(whole);
        if (fils == 0) return s.WordsNoFils.Replace("{D}", d);
        return s.WordsWithFils.Replace("{D}", d).Replace("{F}", Number(fils));
    }

    /// <summary>Amount in figures, e.g. 4523.2 -> "4,523.20".</summary>
    public static string Figures(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", CultureInfo.InvariantCulture);
}
