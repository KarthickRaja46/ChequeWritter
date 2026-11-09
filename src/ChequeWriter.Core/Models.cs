using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ChequeWriter.Core;

/// <summary>What a field prints. The renderer decides the text from this.</summary>
public enum FieldKind
{
    Date,
    Payee,
    AmountWordsLine1,
    AmountWordsLine2,
    AmountFigures,
    AcPayeeText,
    BearerStrike,
    FixedText,
}

public enum TextAlign { Left, Center, Right }

/// <summary>A text field on the cheque. All positions are millimetres from the top-left of the printed page.</summary>
public class FieldLayout
{
    [Browsable(false)] public FieldKind Kind { get; set; }

    [Category("General"), DisplayName("Name"), ReadOnly(true)]
    public string Label { get; set; } = "";

    [Category("General"), Description("Print this field.")]
    public bool Visible { get; set; } = true;

    [Category("Position (mm)"), Description("Distance from the left edge of the page, in mm.")]
    public double X { get; set; }

    [Category("Position (mm)"), Description("Distance from the top edge of the page, in mm.")]
    public double Y { get; set; }

    [Category("Position (mm)")] public double Width { get; set; } = 40;
    [Category("Position (mm)")] public double Height { get; set; } = 5;

    [Category("Text"), DisplayName("Font size (pt)")]
    public double FontSize { get; set; } = 11.25;

    [Category("Text")] public bool Bold { get; set; }
    [Category("Text")] public TextAlign Align { get; set; } = TextAlign.Left;

    [Category("Text"), DisplayName("Vertical centre"), Description("Centre the text vertically inside the field height.")]
    public bool CenterVertically { get; set; }

    [Category("Text"), DisplayName("Rotation (deg)"), Description("Counter-clockwise rotation, e.g. 35 for the A/C Payee crossing.")]
    public double Rotation { get; set; }

    [Category("Date"), DisplayName("Character pitch (mm)"),
     Description("Date only: distance between character centres, for cheques with separate date boxes. 0 = normal text.")]
    public double CharPitch { get; set; }

    [Category("Text"), DisplayName("Fixed text"), Description("Text for 'Fixed text' fields and the bearer strike / A/C payee text.")]
    public string Text { get; set; } = "";

    public FieldLayout Clone() => (FieldLayout)MemberwiseClone();
    public override string ToString() => Label;
}

/// <summary>A straight line, used for the two A/C Payee crossing lines.</summary>
public class LineLayout
{
    [Category("General"), ReadOnly(true)] public string Label { get; set; } = "";
    [Category("General"), Description("Print only when 'A/C Payee Only' is ticked.")]
    public bool OnlyWithAcPayee { get; set; } = true;
    [Category("General")] public bool Visible { get; set; } = true;
    [Category("Line (mm)")] public double X1 { get; set; }
    [Category("Line (mm)")] public double Y1 { get; set; }
    [Category("Line (mm)")] public double X2 { get; set; }
    [Category("Line (mm)")] public double Y2 { get; set; }
    [Category("Line (mm)"), DisplayName("Thickness (mm)")] public double Thickness { get; set; } = 0.26;

    public LineLayout Clone() => (LineLayout)MemberwiseClone();
    public override string ToString() => Label;
}

/// <summary>One bank's cheque design plus how it is fed into the printer.</summary>
public class BankLayout
{
    [Browsable(false)] public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Category("Bank"), DisplayName("Bank name")] public string Name { get; set; } = "New Bank";

    [Category("Page"), DisplayName("Paper width (mm)"), Description("Size of the page sent to the printer. A4 landscape = 297 x 210.")]
    public double PaperWidthMm { get; set; } = 297;

    [Category("Page"), DisplayName("Paper height (mm)")]
    public double PaperHeightMm { get; set; } = 210;

    [Category("Page"), DisplayName("Cheque width (mm)"), Description("Used for the preview outline and test prints.")]
    public double ChequeWidthMm { get; set; } = 204;

    [Category("Page"), DisplayName("Cheque height (mm)")]
    public double ChequeHeightMm { get; set; } = 94;

    [Category("Printer adjustment"), DisplayName("Offset X (mm)"), Description("Moves everything right (+) or left (-). Use this to match your printer.")]
    public double OffsetXmm { get; set; }

    [Category("Printer adjustment"), DisplayName("Offset Y (mm)"), Description("Moves everything down (+) or up (-).")]
    public double OffsetYmm { get; set; }

    [Category("Printer adjustment"), DisplayName("Printer"), Description("Printer to use for this bank. Empty = Windows default printer.")]
    public string PrinterName { get; set; } = "";

    [Category("Formats"), DisplayName("Date format"), Description(".NET date format, e.g. dd-MM-yyyy or ddMMyyyy.")]
    public string DateFormat { get; set; } = "dd-MM-yyyy";

    [Category("Formats"), DisplayName("Font")] public string FontName { get; set; } = "Arial";

    [Category("Cheque numbers"), DisplayName("Next cheque no."), Description("Filled in automatically and increased after each print.")]
    public string NextChequeNo { get; set; } = "";

    [Category("Background"), DisplayName("Background image"), Description("Optional scan/photo of a blank cheque, shown only on screen to help place fields.")]
    public string BackgroundImagePath { get; set; } = "";

    [Browsable(false)] public List<FieldLayout> Fields { get; set; } = new();
    [Browsable(false)] public List<LineLayout> Lines { get; set; } = new();

    public FieldLayout? Field(FieldKind kind) => Fields.FirstOrDefault(f => f.Kind == kind);

    public BankLayout Clone()
    {
        var c = (BankLayout)MemberwiseClone();
        c.Id = Guid.NewGuid().ToString("N");
        c.Fields = Fields.Select(f => f.Clone()).ToList();
        c.Lines = Lines.Select(l => l.Clone()).ToList();
        return c;
    }

    public override string ToString() => Name;
}

/// <summary>What the user typed for one cheque.</summary>
public class ChequeData
{
    public DateTime Date { get; set; } = DateTime.Today;
    public string Payee { get; set; } = "";
    public decimal Amount { get; set; }
    public bool AcPayeeOnly { get; set; } = true;
    public bool StrikeBearer { get; set; } = true;
    public string ChequeNo { get; set; } = "";
}

public enum ChequeStatus { Printed, Void }

/// <summary>One line in the cheque register.</summary>
public class ChequeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ChequeNo { get; set; } = "";
    public string BankId { get; set; } = "";
    public string BankName { get; set; } = "";
    public DateTime ChequeDate { get; set; }
    public string Payee { get; set; } = "";
    public decimal Amount { get; set; }
    public string AmountInWords { get; set; } = "";
    public bool AcPayeeOnly { get; set; }
    public ChequeStatus Status { get; set; } = ChequeStatus.Printed;
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
}

public class Payee
{
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public override string ToString() => Name;
}

/// <summary>How amounts and names are written. Shared by all banks.</summary>
public class AppSettings
{
    [Category("Amount in words"), DisplayName("Words (with fils)"),
     Description("{D} = whole amount in words, {F} = fils in words.")]
    public string WordsWithFils { get; set; } = "{D} And Fils {F}";

    [Category("Amount in words"), DisplayName("Words (no fils)"), Description("Used when there are no fils. {D} = whole amount in words.")]
    public string WordsNoFils { get; set; } = "{D}";

    [Category("Amount in words"), DisplayName("Words wrapper"), Description("{W} = the words above.")]
    public string WordsWrapper { get; set; } = "**{W} Only**";

    [Category("Amount in figures"), DisplayName("Figures wrapper"), Description("{A} = amount like 4,523.20")]
    public string FiguresWrapper { get; set; } = "**{A}**";

    [Category("Payee"), DisplayName("Payee wrapper"), Description("{P} = payee name")]
    public string PayeeWrapper { get; set; } = "**{P}**";

    [Category("Payee"), DisplayName("Payee in CAPITALS")]
    public bool PayeeUpperCase { get; set; } = true;

    [Category("Program"), DisplayName("Ask before printing")]
    public bool ConfirmBeforePrint { get; set; } = true;

    [Browsable(false)] public string LastBankId { get; set; } = "";
}

public class AppData
{
    public int Version { get; set; } = 1;
    public AppSettings Settings { get; set; } = new();
    public List<BankLayout> Banks { get; set; } = new();
    public List<Payee> Payees { get; set; } = new();
    public List<ChequeRecord> Cheques { get; set; } = new();

    [JsonIgnore] public BankLayout? LastBank => Banks.FirstOrDefault(b => b.Id == Settings.LastBankId) ?? Banks.FirstOrDefault();
}
