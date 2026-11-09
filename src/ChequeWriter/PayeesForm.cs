using System.ComponentModel;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>Saved payee names used for auto-complete. Edit directly in the grid.</summary>
public class PayeesForm : Form
{
    readonly DataStore _store;
    readonly BindingList<Payee> _list;
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };

    public PayeesForm(DataStore store)
    {
        _store = store;
        Text = "Payees";
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(640, 520);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);

        _list = new BindingList<Payee>(store.Data.Payees.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Payee { Name = p.Name, Notes = p.Notes }).ToList()) { AllowNew = true, AllowRemove = true };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payee name (as printed on the cheque)", DataPropertyName = nameof(Payee.Name), FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", DataPropertyName = nameof(Payee.Notes), FillWeight = 35 });
        _grid.DataSource = _list;

        var info = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Text = "  Type in the last row to add a payee. Select a row and press Delete to remove it." };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => SaveAndClose();
        bar.Controls.Add(cancel); bar.Controls.Add(save);
        CancelButton = cancel;

        Controls.Add(_grid);
        Controls.Add(info);
        Controls.Add(bar);
    }

    void SaveAndClose()
    {
        _grid.EndEdit();
        var cleaned = _list.Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new Payee { Name = p.Name.Trim(), Notes = (p.Notes ?? "").Trim() })
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToList();
        _store.Data.Payees = cleaned;
        try { _store.Save(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save:\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        DialogResult = DialogResult.OK;
        Close();
    }
}
