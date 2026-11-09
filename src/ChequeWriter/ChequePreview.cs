using System.Drawing.Drawing2D;
using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// Shows the cheque at screen scale. With <see cref="Editable"/> on, fields can be clicked,
/// dragged with the mouse and nudged with the arrow keys (0.1 mm, or 1 mm with Shift).
/// </summary>
public class ChequePreview : Control
{
    const float MarginMm = 6;

    BankLayout? _bank;
    ChequeData _data = new();
    AppSettings _settings = new();
    object? _selected;
    Image? _background;
    string _backgroundPath = "";

    PointF _dragStartMm;
    bool _dragging;
    (double X, double Y, double X2, double Y2) _dragOrigin;

    public bool Editable { get; set; }
    public bool ShowFieldBoxes { get; set; }

    public event EventHandler? SelectionChanged;
    public event EventHandler? LayoutChanged;

    public ChequePreview()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(235, 238, 242);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public WrapResult? LastWrap { get; private set; }

    public void SetContent(BankLayout bank, ChequeData data, AppSettings settings)
    {
        _bank = bank; _data = data; _settings = settings;
        LoadBackground();
        Invalidate();
    }

    public object? Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    void LoadBackground()
    {
        var path = _bank?.BackgroundImagePath ?? "";
        if (path == _backgroundPath) return;
        _background?.Dispose(); _background = null; _backgroundPath = path;
        if (path.Length > 0 && File.Exists(path))
        {
            try
            {
                using var fs = File.OpenRead(path);
                _background = Image.FromStream(fs);
            }
            catch { _background = null; }
        }
    }

    public void RefreshBackground() { _backgroundPath = "\0"; LoadBackground(); Invalidate(); }

    float Scale_ => _bank == null ? 1 :
        Math.Max(0.1f, Math.Min((Width - 10) / (float)(_bank.ChequeWidthMm + 2 * MarginMm),
                                (Height - 10) / (float)(_bank.ChequeHeightMm + 2 * MarginMm)));

    PointF Origin => _bank == null ? PointF.Empty : new PointF(
        (Width - (float)_bank.ChequeWidthMm * Scale_) / 2,
        (Height - (float)_bank.ChequeHeightMm * Scale_) / 2);

    PointF ToMm(Point p) => new((p.X - Origin.X) / Scale_, (p.Y - Origin.Y) / Scale_);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_bank == null) return;
        var g = e.Graphics;
        g.PageUnit = GraphicsUnit.Pixel;
        var o = Origin; var s = Scale_;

        // paper-white cheque area with a soft shadow
        var paper = new RectangleF(o.X, o.Y, (float)_bank.ChequeWidthMm * s, (float)_bank.ChequeHeightMm * s);
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            g.FillRectangle(shadow, paper.X + 3, paper.Y + 3, paper.Width, paper.Height);
        g.FillRectangle(Brushes.White, paper);

        g.TranslateTransform(o.X, o.Y);
        g.ScaleTransform(s, s);
        var text = SafeText();
        LastWrap = ChequeRenderer.Draw(g, _bank, _data, text, new RenderOptions
        {
            ShowChequeOutline = true,
            ShowFieldBoxes = ShowFieldBoxes,
            Background = _background,
            Selected = Editable ? _selected : null,
        });
        g.ResetTransform();

        if (Focused && Editable)
            ControlPaint.DrawFocusRectangle(g, new Rectangle(1, 1, Width - 3, Height - 3));
    }

    ChequeText SafeText()
    {
        try { return ChequeText.Build(_data, _bank!, _settings); }
        catch (ArgumentOutOfRangeException) { return new ChequeText { Payee = _data.Payee, Words = "(amount too large)" }; }
    }

    // ---------- editing ----------

    object? HitTest(PointF mm)
    {
        if (_bank == null) return null;
        // lines first (thin, so give them a 1.5 mm tolerance)
        foreach (var l in _bank.Lines)
            if (DistanceToSegment(mm, l) < 1.5) return l;
        // smallest field box containing the point wins (fields can overlap)
        return _bank.Fields
            .Where(f => ChequeRenderer.Bounds(f).Contains(mm))
            .OrderBy(f => f.Width * f.Height)
            .FirstOrDefault();
    }

    static double DistanceToSegment(PointF p, LineLayout l)
    {
        double dx = l.X2 - l.X1, dy = l.Y2 - l.Y1;
        var len2 = dx * dx + dy * dy;
        var t = len2 == 0 ? 0 : Math.Clamp(((p.X - l.X1) * dx + (p.Y - l.Y1) * dy) / len2, 0, 1);
        double cx = l.X1 + t * dx - p.X, cy = l.Y1 + t * dy - p.Y;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Editable || e.Button != MouseButtons.Left) return;
        Focus();
        var mm = ToMm(e.Location);
        var hit = HitTest(mm);
        if (!ReferenceEquals(hit, _selected))
        {
            _selected = hit;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        if (hit != null)
        {
            _dragging = true;
            _dragStartMm = mm;
            _dragOrigin = hit switch
            {
                FieldLayout f => (f.X, f.Y, 0, 0),
                LineLayout l => (l.X1, l.Y1, l.X2, l.Y2),
                _ => default,
            };
            Capture = true;
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!Editable) return;
        var mm = ToMm(e.Location);
        if (!_dragging)
        {
            Cursor = HitTest(mm) != null ? Cursors.SizeAll : Cursors.Default;
            return;
        }
        // snap to 0.1 mm
        var dx = Math.Round(mm.X - _dragStartMm.X, 1);
        var dy = Math.Round(mm.Y - _dragStartMm.Y, 1);
        MoveSelected(_dragOrigin, dx, dy);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging) { _dragging = false; Capture = false; LayoutChanged?.Invoke(this, EventArgs.Empty); }
    }

    void MoveSelected((double X, double Y, double X2, double Y2) from, double dx, double dy)
    {
        switch (_selected)
        {
            case FieldLayout f:
                f.X = Math.Round(from.X + dx, 2); f.Y = Math.Round(from.Y + dy, 2); break;
            case LineLayout l:
                l.X1 = Math.Round(from.X + dx, 2); l.Y1 = Math.Round(from.Y + dy, 2);
                l.X2 = Math.Round(from.X2 + dx, 2); l.Y2 = Math.Round(from.Y2 + dy, 2); break;
            default: return;
        }
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Editable || _selected == null) return;
        var step = e.Shift ? 1.0 : 0.1;
        (double dx, double dy) = e.KeyCode switch
        {
            Keys.Left => (-step, 0.0), Keys.Right => (step, 0.0),
            Keys.Up => (0.0, -step), Keys.Down => (0.0, step),
            _ => (0.0, 0.0),
        };
        if (dx == 0 && dy == 0) return;
        var from = _selected switch
        {
            FieldLayout f => (f.X, f.Y, 0.0, 0.0),
            LineLayout l => (l.X1, l.Y1, l.X2, l.Y2),
            _ => default,
        };
        MoveSelected(from, dx, dy);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _background?.Dispose();
        base.Dispose(disposing);
    }
}
