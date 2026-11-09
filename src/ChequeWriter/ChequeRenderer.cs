using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ChequeWriter.Core;

namespace ChequeWriter;

public class RenderOptions
{
    /// <summary>Draw the cheque border (screen preview and plain-paper test prints).</summary>
    public bool ShowChequeOutline { get; init; }
    /// <summary>Draw a thin box around every field (layout editor and test prints).</summary>
    public bool ShowFieldBoxes { get; init; }
    /// <summary>Shift everything by the bank's printer offset (true when printing).</summary>
    public bool ApplyPrinterOffset { get; init; }
    public Image? Background { get; init; }
    public object? Selected { get; init; }
}

/// <summary>
/// Draws a cheque onto any Graphics whose world unit is 1 mm
/// (printer: PageUnit = Millimeter; screen: PageUnit = Pixel + ScaleTransform).
/// </summary>
public static class ChequeRenderer
{
    // Small inner gap like most cheque programs use, so text does not touch the field edge.
    const float GapX = 0.5f, GapY = 0.25f;

    static readonly Bitmap MeasureBitmap = new(1, 1);

    public static Font MakeFont(BankLayout bank, FieldLayout f) =>
        new(bank.FontName, (float)(f.FontSize * 25.4 / 72.0), f.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.World);

    /// <summary>Printed width of a string in mm, independent of the screen or printer.</summary>
    public static double MeasureMm(string text, Font font)
    {
        lock (MeasureBitmap)
        {
            using var g = Graphics.FromImage(MeasureBitmap);
            g.PageUnit = GraphicsUnit.Millimeter;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            return g.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;
        }
    }

    /// <summary>Splits the amount in words over the two word lines of this bank.</summary>
    public static WrapResult WrapWords(BankLayout bank, string words)
    {
        var f1 = bank.Field(FieldKind.AmountWordsLine1);
        var f2 = bank.Field(FieldKind.AmountWordsLine2);
        if (f1 == null) return new WrapResult(words, "", false);
        using var font = MakeFont(bank, f1);
        var w1 = f1.Width - GapX;
        var w2 = f2 == null || !f2.Visible ? 0 : f2.Width - GapX;
        return TextWrap.TwoLines(words, w1, w2, s => MeasureMm(s, font));
    }

    public static WrapResult Draw(Graphics g, BankLayout bank, ChequeData data, ChequeText text, RenderOptions o)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        var state = g.Save();
        if (o.ApplyPrinterOffset) g.TranslateTransform((float)bank.OffsetXmm, (float)bank.OffsetYmm);

        var chequeRect = new RectangleF(0, 0, (float)bank.ChequeWidthMm, (float)bank.ChequeHeightMm);
        if (o.Background != null) g.DrawImage(o.Background, chequeRect);
        if (o.ShowChequeOutline)
        {
            using var pen = new Pen(Color.Gray, 0.3f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, chequeRect.X, chequeRect.Y, chequeRect.Width, chequeRect.Height);
        }

        var wrap = WrapWords(bank, text.Words);

        foreach (var f in bank.Fields)
        {
            var s = f.Kind switch
            {
                FieldKind.Date => text.Date,
                FieldKind.Payee => text.Payee,
                FieldKind.AmountWordsLine1 => wrap.Line1,
                FieldKind.AmountWordsLine2 => wrap.Line2,
                FieldKind.AmountFigures => text.Figures,
                FieldKind.BearerStrike => data.StrikeBearer ? f.Text : "",
                FieldKind.AcPayeeText => data.AcPayeeOnly ? f.Text : "",
                _ => f.Text,
            };
            if (o.ShowFieldBoxes || ReferenceEquals(o.Selected, f)) DrawFieldBox(g, f, ReferenceEquals(o.Selected, f));
            if (!f.Visible || string.IsNullOrEmpty(s)) continue;
            using var font = MakeFont(bank, f);
            if (f.Kind == FieldKind.Date && f.CharPitch > 0) DrawSpaced(g, f, font, s);
            else DrawText(g, f, font, s);
        }

        foreach (var l in bank.Lines)
        {
            var selected = ReferenceEquals(o.Selected, l);
            if (!selected && (!l.Visible || (l.OnlyWithAcPayee && !data.AcPayeeOnly))) continue;
            using var pen = new Pen(selected ? Color.DodgerBlue : Color.Black, (float)Math.Max(0.1, l.Thickness));
            g.DrawLine(pen, (float)l.X1, (float)l.Y1, (float)l.X2, (float)l.Y2);
        }

        g.Restore(state);
        return wrap;
    }

    static void DrawFieldBox(Graphics g, FieldLayout f, bool selected)
    {
        using var pen = new Pen(selected ? Color.DodgerBlue : Color.FromArgb(150, Color.OrangeRed), selected ? 0.4f : 0.15f);
        g.DrawRectangle(pen, (float)f.X, (float)f.Y, (float)f.Width, (float)f.Height);
    }

    static void DrawText(Graphics g, FieldLayout f, Font font, string s)
    {
        var size = g.MeasureString(s, font, PointF.Empty, StringFormat.GenericTypographic);
        if (Math.Abs(f.Rotation) > 0.01)
        {
            // Rotate around the centre of the field box.
            var st = g.Save();
            g.TranslateTransform((float)(f.X + f.Width / 2), (float)(f.Y + f.Height / 2));
            g.RotateTransform((float)-f.Rotation);
            g.DrawString(s, font, Brushes.Black, -size.Width / 2, -size.Height / 2, StringFormat.GenericTypographic);
            g.Restore(st);
            return;
        }
        var x = f.Align switch
        {
            TextAlign.Center => (float)(f.X + (f.Width - size.Width) / 2),
            TextAlign.Right => (float)(f.X + f.Width - GapX - size.Width),
            _ => (float)f.X + GapX,
        };
        var y = f.CenterVertically ? (float)(f.Y + (f.Height - size.Height) / 2) : (float)f.Y + GapY;
        g.DrawString(s, font, Brushes.Black, x, y, StringFormat.GenericTypographic);
    }

    /// <summary>Date on cheques with one box per character: each character is centred in its own cell.</summary>
    static void DrawSpaced(Graphics g, FieldLayout f, Font font, string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i].ToString();
            var size = g.MeasureString(ch, font, PointF.Empty, StringFormat.GenericTypographic);
            var cx = f.X + (i + 0.5) * f.CharPitch;
            var y = f.CenterVertically ? f.Y + (f.Height - size.Height) / 2 : f.Y + GapY;
            g.DrawString(ch, font, Brushes.Black, (float)(cx - size.Width / 2), (float)y, StringFormat.GenericTypographic);
        }
    }

    /// <summary>Area of a field in mm, used for mouse hit-testing in the layout editor.</summary>
    public static RectangleF Bounds(FieldLayout f) => new((float)f.X, (float)f.Y, (float)f.Width, (float)f.Height);
}
