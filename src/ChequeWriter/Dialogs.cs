namespace ChequeWriter;

/// <summary>The app icon (embedded app.ico), shared by every window.</summary>
public static class AppIcon
{
    static Icon? _icon;

    public static Icon Get()
    {
        if (_icon != null) return _icon;
        using var s = typeof(AppIcon).Assembly.GetManifestResourceStream("ChequeWriter.app.ico");
        _icon = s != null ? new Icon(s) : SystemIcons.Application;
        return _icon;
    }

    /// <summary>Gives every window the app icon as soon as it is created.</summary>
    public static void Apply(Form f) => f.Icon = Get();
}

/// <summary>A simple OK/Cancel window around a PropertyGrid.</summary>
public class PropertyDialog : Form
{
    public PropertyDialog(string title, object target)
    {
        Text = title;
        Font = new Font("Segoe UI", 9.5f);
        Size = new Size(620, 520);
        StartPosition = FormStartPosition.CenterParent;
        AppIcon.Apply(this);
        var grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = target, ToolbarVisible = false };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        bar.Controls.Add(cancel); bar.Controls.Add(ok);
        AcceptButton = ok; CancelButton = cancel;
        Controls.Add(grid);
        Controls.Add(bar);
    }
}

/// <summary>One-line text input box.</summary>
public static class Prompt
{
    public static string? Show(IWin32Window owner, string title, string label, string value)
    {
        using var f = new Form
        {
            Text = title, Font = new Font("Segoe UI", 9.5f), FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 110), ShowIcon = false,
        };
        var l = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };
        var t = new TextBox { Left = 12, Top = 36, Width = 396, Text = value };
        var ok = new Button { Text = "OK", Left = 252, Top = 72, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 333, Top = 72, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, t, ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        return f.ShowDialog(owner) == DialogResult.OK ? t.Text : null;
    }
}
