using System.Text;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>List of every printed cheque, with search, totals, void and CSV export.</summary>
public class RegisterForm : Form
{
    readonly DataStore _store;
    readonly MainForm _main;
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoGenerateColumns = false,
        RowHeadersVisible = false, BackgroundColor = SystemColors.Window,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };
    readonly TextBox _search = new() { Width = 220, PlaceholderText = "Search payee or cheque no." };
    readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, ShowCheckBox = true, Checked = false };
    readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, ShowCheckBox = true, Checked = false };
    readonly CheckBox _showVoid = new() { Text = "Show void", AutoSize = true, Checked = true, Margin = new Padding(8, 6, 3, 3) };
    readonly Label _total = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

    List<ChequeRecord> _rows = new();
    Font? _voidFont;

    public RegisterForm(DataStore store, MainForm main)
    {
        _store = store; _main = main;
        Text = "Cheque register";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(1100, 640);
        StartPosition = FormStartPosition.CenterParent;

        AddCol("No.", nameof(ChequeRecord.ChequeNo), 70);
        AddCol("Cheque date", nameof(ChequeRecord.ChequeDate), 80, "dd-MM-yyyy");
        AddCol("Payee", nameof(ChequeRecord.Payee), 220);
        AddCol("Amount", nameof(ChequeRecord.Amount), 90, "#,##0.00", right: true);
        AddCol("Amount in words", nameof(ChequeRecord.AmountInWords), 300);
        AddCol("Status", nameof(ChequeRecord.Status), 60);
        AddCol("Printed", nameof(ChequeRecord.PrintedAt), 110, "dd-MM-yyyy HH:mm");
        AddCol("Notes", nameof(ChequeRecord.Notes), 120);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _rows.Count && _rows[e.RowIndex].Status == ChequeStatus.Void)
            {
                e.CellStyle!.ForeColor = Color.Gray;
                e.CellStyle.Font = _voidFont ??= new Font(Font, FontStyle.Strikeout);
            }
        };
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Reprint(); };

        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 4) };
        Label L(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(8, 7, 3, 3) };
        filters.Controls.AddRange(new Control[] { _search, L("From"), _from, L("To"), _to, _showVoid, _total });

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        Button B(string t, EventHandler h) { var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 3, 8, 3) }; b.Click += h; return b; }
        bar.Controls.Add(B("Load into form to reprint", (_, _) => Reprint()));
        bar.Controls.Add(B("Mark void / undo void", (_, _) => ToggleVoid()));
        bar.Controls.Add(B("Edit note...", (_, _) => EditNote()));
        bar.Controls.Add(B("Delete", (_, _) => DeleteRow()));
        bar.Controls.Add(B("Export to Excel (CSV)...", (_, _) => Export()));
        var close = B("Close", (_, _) => Close());
        bar.Controls.Add(close);
        CancelButton = close;

        Controls.Add(_grid);
        Controls.Add(filters);
        Controls.Add(bar);

        _search.TextChanged += (_, _) => Reload();
        _from.ValueChanged += (_, _) => Reload();
        _to.ValueChanged += (_, _) => Reload();
        _showVoid.CheckedChanged += (_, _) => Reload();
        Reload();
    }

    void AddCol(string header, string prop, int weight, string? format = null, bool right = false)
    {
        var c = new DataGridViewTextBoxColumn { HeaderText = header, DataPropertyName = prop, FillWeight = weight };
        if (format != null) c.DefaultCellStyle.Format = format;
        if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.Add(c);
    }

    void Reload()
    {
        IEnumerable<ChequeRecord> q = _store.Data.Cheques;
        var s = _search.Text.Trim();
        if (s.Length > 0)
            q = q.Where(r => r.Payee.Contains(s, StringComparison.OrdinalIgnoreCase) || r.ChequeNo.Contains(s, StringComparison.OrdinalIgnoreCase));
        if (_from.Checked) q = q.Where(r => r.ChequeDate.Date >= _from.Value.Date);
        if (_to.Checked) q = q.Where(r => r.ChequeDate.Date <= _to.Value.Date);
        if (!_showVoid.Checked) q = q.Where(r => r.Status != ChequeStatus.Void);
        _rows = q.OrderByDescending(r => r.PrintedAt).ToList();
        _grid.DataSource = null;
        _grid.DataSource = _rows;
        var total = _rows.Where(r => r.Status == ChequeStatus.Printed).Sum(r => r.Amount);
        _total.Text = $"{_rows.Count} cheques  |  Total (not void): AED {AmountInWords.Figures(total)}";
    }

    ChequeRecord? Current => _grid.CurrentRow?.Index is int i && i >= 0 && i < _rows.Count ? _rows[i] : null;

    void Reprint()
    {
        if (Current is not { } r) return;
        _main.LoadFromRecord(r);
        Close();
    }

    void ToggleVoid()
    {
        if (Current is not { } r) return;
        r.Status = r.Status == ChequeStatus.Void ? ChequeStatus.Printed : ChequeStatus.Void;
        Persist();
    }

    void EditNote()
    {
        if (Current is not { } r) return;
        var note = Prompt.Show(this, "Note", $"Note for cheque {r.ChequeNo}:", r.Notes);
        if (note == null) return;
        r.Notes = note;
        Persist();
    }

    void DeleteRow()
    {
        if (Current is not { } r) return;
        if (MessageBox.Show(this, $"Delete cheque {r.ChequeNo} ({r.Payee}, {AmountInWords.Figures(r.Amount)}) from the register?\nTip: 'Mark void' keeps a record instead.",
                "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _store.Data.Cheques.Remove(r);
        Persist();
    }

    void Export()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV for Excel (*.csv)|*.csv", FileName = $"Cheque-register-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // UTF-8 with BOM so Excel shows Arabic and special characters correctly
            File.WriteAllText(dlg.FileName, CsvExport.Register(_rows), new UTF8Encoding(true));
            MessageBox.Show(this, $"Exported {_rows.Count} cheques.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void Persist()
    {
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        Reload();
    }
}
