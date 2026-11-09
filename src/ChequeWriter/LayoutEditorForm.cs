using System.Text.Json;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Adjust where each part of the RAK cheque prints. Works on a copy; nothing changes until Save.
/// </summary>
public class LayoutEditorForm : Form
{
    readonly DataStore _store;
    readonly BankLayout _original;
    readonly BankLayout _work;
    readonly ChequeData _sample;

    readonly ChequePreview _canvas = new() { Dock = DockStyle.Fill, Editable = true, ShowFieldBoxes = true };
    readonly ListBox _items = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill, ToolbarVisible = false, HelpVisible = true };
    readonly Label _hint = new()
    {
        Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray,
        Text = "  Drag a field with the mouse, or click it and use the arrow keys (0.1 mm; Shift = 1 mm).",
    };

    const string ChequeSettingsItem = "[ Cheque & printer settings ]";

    public LayoutEditorForm(DataStore store, BankLayout bank, ChequeData sample)
    {
        _store = store;
        _original = bank;
        _work = Copy(bank);
        _sample = new ChequeData
        {
            Date = sample.Date,
            Payee = string.IsNullOrWhiteSpace(sample.Payee) ? "Sample Payee Trading LLC" : sample.Payee,
            Amount = sample.Amount > 0 ? sample.Amount : 4523.20m,
            AcPayeeOnly = true, StrikeBearer = true,
        };

        Text = $"Layout editor - {bank.Name}";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(1200, 720);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterParent;

        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 220 };
        right.Panel1.Controls.Add(_items);
        right.Panel2.Controls.Add(_grid);

        var main = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        main.Panel1.Controls.Add(_canvas);
        main.Panel1.Controls.Add(_hint);
        main.Panel2.Controls.Add(right);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        Button B(string t, EventHandler h) { var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 3, 8, 3) }; b.Click += h; return b; }
        var save = B("Save", (_, _) => SaveAndClose());
        var cancel = B("Cancel", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        bar.Controls.Add(cancel);
        bar.Controls.Add(save);
        bar.Controls.Add(B("Reset to original RAK layout", (_, _) => ResetDefaults()));
        bar.Controls.Add(B("Test print on plain paper", (_, _) => TestPrint()));
        bar.Controls.Add(B("Clear background", (_, _) => { _work.BackgroundImagePath = ""; _canvas.RefreshBackground(); }));
        bar.Controls.Add(B("Background image (cheque scan)...", (_, _) => PickBackground()));
        AcceptButton = save; CancelButton = cancel;

        Controls.Add(main);
        Controls.Add(bar);
        Load += (_, _) =>
        {
            main.SplitterDistance = Math.Max(300, ClientSize.Width - 360);
            right.SplitterDistance = Math.Min(240, right.Height / 3);   // short list on top, big settings grid below
        };

        FillItems();
        _items.SelectedIndexChanged += (_, _) => SelectFromList();
        _canvas.SelectionChanged += (_, _) => SelectFromCanvas();
        _canvas.LayoutChanged += (_, _) => _grid.Refresh();
        _grid.PropertyValueChanged += (_, _) => { _canvas.RefreshBackground(); _canvas.Invalidate(); };
        _canvas.SetContent(_work, _sample, store.Data.Settings);
        _items.SelectedIndex = 0;
    }

    static BankLayout Copy(BankLayout b) =>
        JsonSerializer.Deserialize<BankLayout>(JsonSerializer.Serialize(b, DataStore.JsonOptions), DataStore.JsonOptions)!;

    void FillItems()
    {
        _items.Items.Clear();
        _items.Items.Add(ChequeSettingsItem);
        foreach (var f in _work.Fields) _items.Items.Add(f);
        foreach (var l in _work.Lines) _items.Items.Add(l);
    }

    void SelectFromList()
    {
        var item = _items.SelectedItem;
        if (item is string) { _grid.SelectedObject = _work; _canvas.Selected = null; }
        else { _grid.SelectedObject = item; _canvas.Selected = item; }
    }

    void SelectFromCanvas()
    {
        var sel = _canvas.Selected;
        _items.SelectedItem = sel ?? (object)ChequeSettingsItem;
    }

    void PickBackground()
    {
        using var dlg = new OpenFileDialog { Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _work.BackgroundImagePath = dlg.FileName;
        _canvas.RefreshBackground();
        MessageBox.Show(this,
            "The image is stretched to the cheque size (Cheque width x height in Cheque settings).\n" +
            "Crop the scan exactly to the cheque edges for the best match. It is only shown on screen, never printed.",
            "Background image", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void TestPrint()
    {
        using var doc = ChequePrinter.Create(_work, _sample, _store.Data.Settings, testPrint: true);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ResetDefaults()
    {
        if (MessageBox.Show(this, "Put every field back to the original RAK Business positions?\n(Your printer offsets and background are kept.)",
                "Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var d = DefaultLayouts.RakBusiness();
        _work.Fields = d.Fields; _work.Lines = d.Lines;
        FillItems();
        _canvas.SetContent(_work, _sample, _store.Data.Settings);
        _items.SelectedIndex = 0;
    }

    void SaveAndClose()
    {
        // copy the edited values back into the live layout object
        _original.Name = _work.Name;
        _original.PaperWidthMm = _work.PaperWidthMm; _original.PaperHeightMm = _work.PaperHeightMm;
        _original.ChequeWidthMm = _work.ChequeWidthMm; _original.ChequeHeightMm = _work.ChequeHeightMm;
        _original.OffsetXmm = _work.OffsetXmm; _original.OffsetYmm = _work.OffsetYmm;
        _original.PrinterName = _work.PrinterName; _original.DateFormat = _work.DateFormat;
        _original.FontName = _work.FontName; _original.NextChequeNo = _work.NextChequeNo;
        _original.BackgroundImagePath = _work.BackgroundImagePath;
        _original.Fields = _work.Fields; _original.Lines = _work.Lines;
        DialogResult = DialogResult.OK;
        Close();
    }
}
