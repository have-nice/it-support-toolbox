using System.Drawing.Drawing2D;

namespace ItSupportToolbox;

internal enum ButtonKind
{
    Accent,
    Ghost,
    Danger
}

internal sealed class Card : Panel
{
    private readonly Label _title;
    private readonly Label? _subtitle;
    private readonly int _radius = 14;
    private int _lastWidth = -1;
    private bool _adjusting;
    private bool _adjustQueued;

    public FlowLayoutPanel Body { get; }

    public Card(string title, string? subtitle = null)
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        Padding = new Padding(20, 18, 20, 18);
        Margin = new Padding(0, 0, 0, 14);

        _title = new Label
        {
            Text = title,
            Font = Theme.Ui(11.5f, FontStyle.Bold),
            ForeColor = Theme.Text,
            BackColor = Theme.Card,
            AutoSize = false,
            UseMnemonic = false
        };
        Controls.Add(_title);

        if (subtitle is not null)
        {
            _subtitle = new Label
            {
                Text = subtitle,
                Font = Theme.Ui(9f),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                UseMnemonic = false
            };
            Controls.Add(_subtitle);
        }

        Body = new FlowLayoutPanel
        {
            AutoSize = false,
            WrapContents = true,
            BackColor = Theme.Card,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        Body.ControlAdded += (_, e) =>
        {
            if (e.Control is not null)
                e.Control.SizeChanged += (_, _) => QueueAdjust();
            QueueAdjust();
        };
        Body.ControlRemoved += (_, _) => QueueAdjust();
        Body.Layout += (_, _) => QueueAdjust();
        Controls.Add(Body);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), _radius));
            old?.Dispose();
        }

        if (_adjusting || Width <= 0 || Width == _lastWidth)
            return;

        _lastWidth = Width;
        AdjustLayout();
    }

    private void QueueAdjust()
    {
        if (_adjustQueued || _adjusting || IsDisposed)
            return;

        _adjustQueued = true;

        if (!IsHandleCreated)
        {
            _adjustQueued = false;
            return;
        }

        BeginInvoke(new Action(() =>
        {
            _adjustQueued = false;
            if (!IsDisposed)
                AdjustLayout();
        }));
    }

    private void AdjustLayout()
    {
        if (_adjusting || Width <= 0)
            return;

        _adjusting = true;
        try
        {
            int inner = Math.Max(80, Width - Padding.Horizontal);
            int x = Padding.Left;
            int y = Padding.Top;

            int titleHeight = Measure(_title.Text, _title.Font, inner);
            _title.SetBounds(x, y, inner, titleHeight);
            y += titleHeight;

            if (_subtitle is not null)
            {
                int subtitleHeight = Measure(_subtitle.Text, _subtitle.Font, inner);
                _subtitle.SetBounds(x, y + 5, inner, subtitleHeight);
                y += 5 + subtitleHeight;
            }

            int bodyTop = y + 14;
            Body.SetBounds(x, bodyTop, inner, 0);
            Body.Height = MeasureBody(inner);

            int desiredHeight = bodyTop + Body.Height + Padding.Bottom;
            if (Height != desiredHeight)
                Height = desiredHeight;
        }
        finally
        {
            _adjusting = false;
        }

        Body.Invalidate(true);
        Invalidate(true);
    }

    private static int Measure(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(width, 0), TextFormatFlags.WordBreak).Height;

    private int MeasureBody(int width)
    {
        int rowWidth = 0;
        int rowHeight = 0;
        int total = 0;

        foreach (Control control in Body.Controls)
        {
            if (!control.Visible)
                continue;

            Padding margin = control.Margin;
            int controlWidth = control.Width + margin.Horizontal;
            int controlHeight = control.Height + margin.Vertical;

            if (rowWidth > 0 && rowWidth + controlWidth > width)
            {
                total += rowHeight;
                rowWidth = 0;
                rowHeight = 0;
            }

            rowWidth += controlWidth;
            rowHeight = Math.Max(rowHeight, controlHeight);
        }

        if (rowWidth > 0)
            total += rowHeight;

        return total;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), _radius);
        using var pen = new Pen(Theme.CardBorder, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class AccentButton : Button
{
    private readonly ButtonKind _kind;
    private bool _hover;
    private bool _down;

    public AccentButton(string text, ButtonKind kind = ButtonKind.Ghost, int minWidth = 0, int height = 36, float fontSize = 9.5f)
    {
        _kind = kind;
        Text = text;
        Font = Theme.Ui(fontSize, FontStyle.Bold);
        Height = height;
        Width = Math.Max(minWidth, TextRenderer.MeasureText(text, Font).Width + (height <= 30 ? 26 : 34));
        Margin = new Padding(0, 0, 10, 8);
        UseMnemonic = false;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), 9));
            old?.Dispose();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var host = new SolidBrush(Parent?.BackColor ?? Theme.Card))
            e.Graphics.FillRectangle(host, ClientRectangle);

        Color fill, border, fore;
        switch (_kind)
        {
            case ButtonKind.Accent:
                fill = _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent;
                border = fill;
                fore = Color.White;
                break;
            case ButtonKind.Danger:
                fill = _down ? Color.FromArgb(66, 240, 92, 92)
                             : _hover ? Color.FromArgb(52, 240, 92, 92)
                                       : Color.FromArgb(38, 240, 92, 92);
                border = Color.FromArgb(_hover ? 130 : 80, 240, 92, 92);
                fore = Theme.Red;
                break;
            default:
                fill = _down ? Color.FromArgb(40, 45, 57)
                             : _hover ? Color.FromArgb(34, 39, 49)
                                       : Color.FromArgb(28, 32, 41);
                border = Theme.CardBorder;
                fore = Theme.Text;
                break;
        }

        using var path = Theme.Round(rect, 9);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        if (_kind != ButtonKind.Accent)
        {
            using var pen = new Pen(border, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, rect, fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (!Enabled)
        {
            using var overlay = new SolidBrush(Color.FromArgb(150, Theme.Card));
            e.Graphics.FillPath(overlay, path);
        }
    }
}

internal sealed class NavButton : Button
{
    private readonly string _glyph;
    private bool _active;
    private bool _hover;

    public NavButton(string glyph, string text)
    {
        _glyph = glyph;
        Text = text;
        Height = 44;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 4);
        UseMnemonic = false;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TabStop = false;
        Font = Theme.Ui(9.75f, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), 10));
            old?.Dispose();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill = _active ? Color.FromArgb(28, 38, 56)
                    : _hover ? Color.FromArgb(20, 24, 32)
                    : Theme.Sidebar;
        using (var path = Theme.Round(rect, 10))
        using (var brush = new SolidBrush(fill))
            e.Graphics.FillPath(brush, path);

        if (_active)
        {
            using var bar = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(bar, 0, (Height - 20) / 2, 3, 20);
        }

        TextRenderer.DrawText(e.Graphics, _glyph, Theme.Icon(12f),
            new Rectangle(16, 0, 22, Height),
            _active ? Theme.Accent : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(46, 0, Width - 56, Height),
            _active ? Theme.Text : (_hover ? Theme.Text : Color.FromArgb(188, 196, 210)),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class RoundedInput : Panel
{
    private bool _focused;

    public TextBox Box { get; }

    public RoundedInput(int width = 220, string text = "")
    {
        DoubleBuffered = true;
        BackColor = Theme.Card;
        Margin = new Padding(0, 0, 10, 8);

        Box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = Theme.Ui(9.75f),
            BackColor = Theme.Input,
            ForeColor = Theme.Text,
            Text = text
        };
        Box.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        Box.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(Box);

        Width = width;
        Height = 36;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Box is null)
            return;
        int h = Box.PreferredHeight;
        Box.SetBounds(12, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - 24), h);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), 9));
            old?.Dispose();
        }
        PerformLayout();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(rect, 9);
        using var fill = new SolidBrush(Theme.Input);
        e.Graphics.FillPath(fill, path);
        using var pen = new Pen(_focused ? Theme.Accent : Theme.CardBorder, 1f);
        e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }
}

internal sealed class Pill : Panel
{
    private readonly Color _accent;

    public Pill(string text, Color accent)
    {
        _accent = accent;
        Text = text;
        Height = 26;
        Font = Theme.Ui(7.75f, FontStyle.Bold);
        Width = TextRenderer.MeasureText(text, Font).Width + 34;
        Margin = new Padding(0, 0, 10, 0);
        BackColor = Theme.Bg;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), Height / 2));
            old?.Dispose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(rect, Height / 2))
        using (var brush = new SolidBrush(Color.FromArgb(28, _accent)))
            e.Graphics.FillPath(brush, path);
        using (var pen = new Pen(Color.FromArgb(90, _accent), 1f))
        using (var path = Theme.Round(rect, Height / 2))
            e.Graphics.DrawPath(pen, path);

        using var dot = new SolidBrush(_accent);
        e.Graphics.FillEllipse(dot, 10, (Height - 6) / 2, 6, 6);

        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(22, 0, Width - 26, Height), _accent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}

internal sealed class LogoMark : Control
{
    public LogoMark()
    {
        Size = new Size(44, 44);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(rect, 12);
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(96, 158, 255), Color.FromArgb(43, 84, 201), 90f);
        g.FillPath(brush, path);

        using var white = new SolidBrush(Color.FromArgb(238, 255, 255, 255));
        using var accent = new SolidBrush(Color.FromArgb(255, 48, 92, 210));
        using var body = Theme.Round(new Rectangle(10, 20, 24, 15), 3);
        g.FillPath(white, body);

        using var handlePen = new Pen(Color.FromArgb(238, 255, 255, 255), 2.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawArc(handlePen, new Rectangle(13, 13, 18, 10), 180, 180);

        using var seamPen = new Pen(Color.FromArgb(255, 48, 92, 210), 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(seamPen, 13f, 26f, 31f, 26f);

        using var latch = Theme.Round(new Rectangle(19, 24, 6, 5), 2);
        g.FillPath(accent, latch);
    }
}
