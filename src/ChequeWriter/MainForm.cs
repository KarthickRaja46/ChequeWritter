using System.Diagnostics;
using System.Drawing.Printing;
using System.Globalization;
using ChequeWriter.Core;

namespace ChequeWriter;

public class MainForm : Form
{
    readonly DataStore _store;
    BankLayout Bank => _store.Data.LastBank!;

    readonly TextBox _chequeNo = new() { Width = 140 };
    readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 140 };
    readonly ComboBox _payee = new() { Width = 420, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    readonly TextBox _amount = new() { Width = 160, TextAlign = HorizontalAlignment.Right };
    readonly Label _words = new() { AutoSize = false, Height = 40, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(40, 40, 40) };
    readonly CheckBox _acPayee = new() { Text = "A/C Payee Only (crossing)", AutoSize = true, Checked = true };
    readonly CheckBox _bearer = new() { Text = "Cross out \"or Bearer\"", AutoSize = true, Checked = true };
    readonly ChequePreview _preview = new() { Dock = DockStyle.Fill };
    readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public MainForm(DataStore store)
    {
        _store = store;
        Text = "Cheque Writer";
        AppIcon.Apply(this);
        Font = new Font("Segoe UI", 10f);
        MinimumSize = new Size(900, 640);
        Size = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildLayout();
        BuildMenu();
        LoadBank();

        _date.ValueChanged += (_, _) => UpdatePreview();
        _payee.TextChanged += (_, _) => UpdatePreview();
        _amount.TextChanged += (_, _) => UpdatePreview();
        _acPayee.CheckedChanged += (_, _) => UpdatePreview();
        _bearer.CheckedChanged += (_, _) => UpdatePreview();
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.P) { e.SuppressKeyPress = true; PrintCheque(); } };
    }

    // ---------------- UI ----------------

    void BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("&Backup data...", null, (_, _) => Backup());
        file.DropDownItems.Add("&Restore data from backup...", null, (_, _) => Restore());
        file.DropDownItems.Add("Open &data folder", null, (_, _) => OpenDataFolder());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());

        var tools = new ToolStripMenuItem("&Tools");
        tools.DropDownItems.Add("Cheque &register", null, (_, _) => ShowRegister());
        tools.DropDownItems.Add("&Payees", null, (_, _) => ShowPayees());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("&Layout editor (field positions)", null, (_, _) => ShowLayoutEditor());
        tools.DropDownItems.Add("&Settings (wording && formats)", null, (_, _) => ShowSettings());

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("&How to align with your printer", null, (_, _) => ShowAlignHelp());
        help.DropDownItems.Add("&About", null, (_, _) => MessageBox.Show(this,
            $"Cheque Writer\nVersion {Application.ProductVersion}\n\nData file:\n{_store.FilePath}",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menu.Items.AddRange(new ToolStripItem[] { file, tools, help });
        MainMenuStrip = menu;
        Controls.Add(menu);
        menu.SendToBack();   // docked first, so it sits at the very top
    }

    void BuildLayout()
    {
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(12, 10, 12, 4),
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Label L(string t) => new() { Text = t, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 3) };

        form.Controls.Add(L("Cheque No."), 0, 0); form.Controls.Add(_chequeNo, 1, 0);
        form.Controls.Add(L("Date"), 2, 0); form.Controls.Add(_date, 3, 0);
        form.Controls.Add(L("Pay to"), 0, 1); form.Controls.Add(_payee, 1, 1); form.SetColumnSpan(_payee, 3);
        form.Controls.Add(L("Amount (AED)"), 0, 2); form.Controls.Add(_amount, 1, 2);
        var checks = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        checks.Controls.Add(_acPayee); checks.Controls.Add(_bearer);
        form.Controls.Add(checks, 2, 2); form.SetColumnSpan(checks, 2);
        form.Controls.Add(L("In words"), 0, 3); form.Controls.Add(_words, 1, 3); form.SetColumnSpan(_words, 3);

        _payee.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _amount.Leave += (_, _) => { if (TryAmount(out var a)) _amount.Text = AmountInWords.Figures(a); };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 2, 12, 8) };
        Button B(string t, EventHandler h, bool primary = false)
        {
            var b = new Button { Text = t, AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 0, 8, 0) };
            if (primary) { b.BackColor = Color.FromArgb(0, 102, 204); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
            b.Click += h;
            return b;
        }
        var print = B("Print cheque  (Ctrl+P)", (_, _) => PrintCheque(), true);
        buttons.Controls.Add(print);
        buttons.Controls.Add(B("Print preview", (_, _) => Preview()));
        buttons.Controls.Add(B("Test print on plain paper", (_, _) => TestPrint()));
        buttons.Controls.Add(B("Clear", (_, _) => ClearEntry()));
        AcceptButton = null;

        var previewBox = new GroupBox { Text = "Preview", Dock = DockStyle.Fill, Padding = new Padding(8) };
        previewBox.Controls.Add(_preview);

        var status = new StatusStrip();
        status.Items.Add(_status);

        Controls.Add(previewBox);
        Controls.Add(buttons);
        Controls.Add(form);
        Controls.Add(status);
        previewBox.BringToFront();
    }

    void LoadBank()
    {
        Text = $"Cheque Writer - {Bank.Name}";
        _chequeNo.Text = Bank.NextChequeNo;
        RefreshPayeeList();
        UpdatePreview();
    }

    void RefreshPayeeList()
    {
        var current = _payee.Text;
        _payee.BeginUpdate();
        _payee.Items.Clear();
        foreach (var p in _store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            _payee.Items.Add(p.Name);
        _payee.EndUpdate();
        _payee.Text = current;
    }

    // ---------------- data from the form ----------------

    bool TryAmount(out decimal amount)
    {
        var t = _amount.Text.Trim().Replace("AED", "", StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    ChequeData CurrentData()
    {
        TryAmount(out var amount);
        return new ChequeData
        {
            ChequeNo = _chequeNo.Text.Trim(),
            Date = _date.Value.Date,
            Payee = _payee.Text,
            Amount = Math.Max(0, amount),
            AcPayeeOnly = _acPayee.Checked,
            StrikeBearer = _bearer.Checked,
        };
    }

    void UpdatePreview()
    {
        var data = CurrentData();
        _errors.SetError(_amount, "");
        if (_amount.Text.Trim().Length > 0 && !TryAmount(out _))
            _errors.SetError(_amount, "Type a number like 1250.50");

        string words;
        try { words = ChequeText.Build(data, Bank, _store.Data.Settings).Words; }
        catch (ArgumentOutOfRangeException ex) { words = ex.Message; }
        _words.Text = data.Amount > 0 ? words : "";

        _preview.SetContent(Bank, data, _store.Data.Settings);
        var wrap = ChequeRenderer.WrapWords(Bank, words);
        _status.ForeColor = wrap.Overflow ? Color.Firebrick : SystemColors.ControlText;
        _status.Text = wrap.Overflow
            ? "Amount in words is too long for the two lines on the cheque. Make the font smaller in the layout editor."
            : $"{_store.Data.Cheques.Count} cheques in register  |  Bank: {Bank.Name}";
    }

    /// <summary>Checks the entry before a real print. Returns false (and tells the user) when something is wrong.</summary>
    bool ValidateCheque(ChequeData data)
    {
        string? problem = null;
        if (data.Payee.Trim().Length == 0) problem = "Please enter who the cheque is paid to.";
        else if (!TryAmount(out var a) || a <= 0) problem = "Please enter an amount greater than zero.";
        else if (decimal.Round(a, 2) != a) problem = "Amount can have at most 2 decimals (fils).";
        else if (a > AmountInWords.MaxAmount) problem = "Amount is too large.";
        else if (ChequeRenderer.WrapWords(Bank, ChequeText.Build(data, Bank, _store.Data.Settings).Words).Overflow)
            problem = "Amount in words does not fit on the cheque lines.";
        if (problem != null)
        {
            MessageBox.Show(this, problem, "Check the cheque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (data.ChequeNo.Length == 0 &&
            MessageBox.Show(this, "No cheque number entered. Print anyway?", "Cheque number",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;

        var dup = _store.Data.Cheques.FirstOrDefault(c => c.Status == ChequeStatus.Printed && data.ChequeNo.Length > 0 &&
                                                         c.BankId == Bank.Id && c.ChequeNo == data.ChequeNo);
        if (dup != null && MessageBox.Show(this,
                $"Cheque No. {dup.ChequeNo} was already printed on {dup.PrintedAt:dd-MM-yyyy} for {dup.Payee} ({AmountInWords.Figures(dup.Amount)}).\n\nPrint again with the same number?",
                "Duplicate cheque number", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
        return true;
    }

    // ---------------- actions ----------------

    void PrintCheque()
    {
        var data = CurrentData();
        if (!ValidateCheque(data)) return;
        var text = ChequeText.Build(data, Bank, _store.Data.Settings);

        if (_store.Data.Settings.ConfirmBeforePrint && MessageBox.Show(this,
                $"Cheque No.: {data.ChequeNo}\nDate: {text.Date}\nPay to: {data.Payee.Trim()}\nAmount: AED {AmountInWords.Figures(data.Amount)}\n{text.Words}\n\nPut the cheque in the printer and click OK.",
                "Print cheque", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

        using var doc = ChequePrinter.Create(Bank, data, _store.Data.Settings, testPrint: false);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false, AllowSelection = false };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Bank.PrinterName = doc.PrinterSettings.PrinterName;

        try { doc.Print(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _store.Data.Cheques.Add(new ChequeRecord
        {
            ChequeNo = data.ChequeNo, BankId = Bank.Id, BankName = Bank.Name, ChequeDate = data.Date,
            Payee = data.Payee.Trim(), Amount = data.Amount, AmountInWords = text.Words, AcPayeeOnly = data.AcPayeeOnly,
        });
        _store.RememberPayee(data.Payee);
        if (data.ChequeNo.Length > 0) Bank.NextChequeNo = DataStore.NextNumber(data.ChequeNo);
        Save();

        _chequeNo.Text = Bank.NextChequeNo;
        _payee.Text = ""; _amount.Text = "";
        RefreshPayeeList();
        UpdatePreview();
        _payee.Focus();
    }

    void Preview()
    {
        using var doc = ChequePrinter.Create(Bank, CurrentData(), _store.Data.Settings, testPrint: false);
        using var dlg = new PrintPreviewDialog { Document = doc, Width = 1100, Height = 800, UseAntiAlias = true, Icon = AppIcon.Get() };
        dlg.ShowDialog(this);
    }

    void TestPrint()
    {
        var data = CurrentData();
        if (data.Payee.Trim().Length == 0) data.Payee = "Sample Payee Trading LLC";
        if (data.Amount <= 0) data.Amount = 1354.50m;
        using var doc = ChequePrinter.Create(Bank, data, _store.Data.Settings, testPrint: true);
        using var dlg = new PrintDialog { Document = doc, UseEXDialog = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { doc.Print(); }
        catch (Exception ex) { MessageBox.Show(this, "Printing failed:\n" + ex.Message, "Print", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ClearEntry()
    {
        _payee.Text = ""; _amount.Text = ""; _date.Value = DateTime.Today;
        _acPayee.Checked = true; _bearer.Checked = true;
        _chequeNo.Text = Bank.NextChequeNo;
        _payee.Focus();
    }

    /// <summary>Called from the register: fills the form with an earlier cheque so it can be printed again.</summary>
    public void LoadFromRecord(ChequeRecord r)
    {
        _chequeNo.Text = r.ChequeNo; _date.Value = r.ChequeDate; _payee.Text = r.Payee;
        _amount.Text = AmountInWords.Figures(r.Amount); _acPayee.Checked = r.AcPayeeOnly;
        UpdatePreview();
    }

    void ShowRegister()
    {
        using var f = new RegisterForm(_store, this);
        f.ShowDialog(this);
        UpdatePreview();
    }

    void ShowPayees()
    {
        using var f = new PayeesForm(_store);
        f.ShowDialog(this);
        RefreshPayeeList();
    }

    void ShowLayoutEditor()
    {
        using var f = new LayoutEditorForm(_store, Bank, CurrentData());
        if (f.ShowDialog(this) == DialogResult.OK) Save();
        LoadBank();
    }

    void ShowSettings()
    {
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            System.Text.Json.JsonSerializer.Serialize(_store.Data.Settings))!;
        using var f = new PropertyDialog("Settings - wording and formats", copy);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        copy.LastBankId = _store.Data.Settings.LastBankId;
        _store.Data.Settings = copy;
        Save();
        UpdatePreview();
    }

    void ShowAlignHelp() => MessageBox.Show(this,
        "1. Click 'Test print on plain paper'. It prints the cheque outline and all fields.\n" +
        "2. Hold the printout over a real cheque against a window.\n" +
        "3. If everything is shifted the same way, open Tools > Layout editor > Cheque settings and change\n" +
        "   Offset X (right +, left -) and Offset Y (down +, up -) in mm.\n" +
        "4. If only one field is wrong, drag that field in the layout editor (or use the arrow keys).\n" +
        "5. Feed the cheque the same way every time (against the paper guide).",
        "Aligning with your printer", MessageBoxButtons.OK, MessageBoxIcon.Information);

    void Save()
    {
        try { _store.Save(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save data:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void Backup()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "Cheque Writer backup (*.json)|*.json",
            FileName = $"ChequeWriter-backup-{DateTime.Now:yyyyMMdd-HHmm}.json",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _store.BackupTo(dlg.FileName);
            MessageBox.Show(this, "Backup saved.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Backup failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void Restore()
    {
        using var dlg = new OpenFileDialog { Filter = "Cheque Writer backup (*.json)|*.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, "Replace ALL current data (register, payees, layout) with this backup?\nThe current data is kept as a .bak file.",
                "Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            _store.RestoreFrom(dlg.FileName);
            LoadBank();
            MessageBox.Show(this, "Data restored.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Restore failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_store.FilePath)!;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }
}
