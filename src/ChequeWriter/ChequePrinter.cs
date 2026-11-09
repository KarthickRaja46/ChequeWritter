using System.Drawing.Printing;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Builds a PrintDocument that prints one cheque at exact millimetre positions.</summary>
public static class ChequePrinter
{
    public static PrintDocument Create(BankLayout bank, ChequeData data, AppSettings settings, bool testPrint)
    {
        var doc = new PrintDocument { DocumentName = testPrint ? "Cheque test print" : $"Cheque {data.ChequeNo}" };
        if (!string.IsNullOrWhiteSpace(bank.PrinterName) &&
            PrinterSettings.InstalledPrinters.Cast<string>().Contains(bank.PrinterName))
        {
            doc.PrinterSettings.PrinterName = bank.PrinterName;
        }
        ApplyPaper(doc, bank);

        var text = ChequeText.Build(data, bank, settings);
        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            g.PageUnit = GraphicsUnit.Millimeter;
            // .NET draws from the printer's printable-area corner; move back to the real paper corner
            // so positions are measured from the edge of the paper. Not needed in print preview.
            if (!doc.PrintController.IsPreview)
            {
                g.TranslateTransform(-(float)(e.PageSettings.HardMarginX * 0.254), -(float)(e.PageSettings.HardMarginY * 0.254));
            }
            ChequeRenderer.Draw(g, bank, data, text, new RenderOptions
            {
                ApplyPrinterOffset = true,
                ShowChequeOutline = testPrint,
                ShowFieldBoxes = testPrint,
            });
            e.HasMorePages = false;
        };
        return doc;
    }

    /// <summary>Uses a standard paper size when the printer has one that matches, otherwise a custom size.</summary>
    static void ApplyPaper(PrintDocument doc, BankLayout bank)
    {
        var w = bank.PaperWidthMm; var h = bank.PaperHeightMm;
        var landscape = w > h;
        var shortMm = Math.Min(w, h); var longMm = Math.Max(w, h);
        var ps = doc.DefaultPageSettings;
        ps.Margins = new Margins(0, 0, 0, 0);
        ps.Landscape = landscape;

        PaperSize? match = null;
        try
        {
            match = doc.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p =>
                Math.Abs(p.Width * 0.254 - shortMm) < 1.5 && Math.Abs(p.Height * 0.254 - longMm) < 1.5);
        }
        catch (InvalidPrinterException) { /* no printer installed: preview still works */ }

        ps.PaperSize = match ?? new PaperSize("Cheque", (int)Math.Round(shortMm / 0.254), (int)Math.Round(longMm / 0.254));
    }
}
