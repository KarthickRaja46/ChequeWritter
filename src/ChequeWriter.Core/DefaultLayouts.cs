namespace ChequeWriter.Core;

/// <summary>Built-in bank layouts. Positions are millimetres from the top-left of the page.</summary>
public static class DefaultLayouts
{
    public const string RakBusinessId = "rak-business";

    /// <summary>
    /// RAK Bank (Business) cheque, A4 landscape with the cheque in the top-left corner.
    /// Measured and confirmed by a test print on a real RAK Business cheque (Nov 2026).
    /// </summary>
    public static BankLayout RakBusiness() => new()
    {
        Id = RakBusinessId,
        Name = "RAK Bank (Business)",
        PaperWidthMm = 297,
        PaperHeightMm = 210,
        ChequeWidthMm = 204,
        ChequeHeightMm = 94,
        DateFormat = "dd-MM-yyyy",
        Fields =
        {
            new() { Kind = FieldKind.Date, Label = "Date", X = 156.23, Y = 18.83, Width = 32, Height = 6.0,
                    FontSize = 9.75, CenterVertically = true, CharPitch = 3.1575 },
            new() { Kind = FieldKind.Payee, Label = "Payee", X = 37.22, Y = 36.14, Width = 130.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountWordsLine1, Label = "Amount in words - line 1", X = 16.50, Y = 45.31, Width = 118.12, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountWordsLine2, Label = "Amount in words - line 2", X = 7.06, Y = 55.00, Width = 134.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.AmountFigures, Label = "Amount in figures", X = 157.67, Y = 45.70, Width = 40.0, Height = 5.0, FontSize = 11.25 },
            new() { Kind = FieldKind.BearerStrike, Label = "Strike 'or Bearer'", X = 5.45, Y = 38.35, Width = 17.0, Height = 4.0, FontSize = 9, Text = "XXXXXXX" },
            new() { Kind = FieldKind.AcPayeeText, Label = "A/C Payee text", X = 3.70, Y = 1.30, Width = 34.0, Height = 26.0, FontSize = 8.25,
                    Rotation = 35, Align = TextAlign.Center, CenterVertically = true, Text = "A/C PAYEE ONLY" },
        },
        Lines =
        {
            new() { Label = "Crossing line 1", X1 = -0.50, Y1 = 18.90, X2 = 27.50, Y2 = -1.10 },
            new() { Label = "Crossing line 2", X1 = -0.30, Y1 = 26.10, X2 = 38.70, Y2 = -1.90 },
        },
    };

    public static AppData NewAppData()
    {
        var data = new AppData();
        data.Banks.Add(RakBusiness());
        data.Settings.LastBankId = RakBusinessId;
        return data;
    }
}
