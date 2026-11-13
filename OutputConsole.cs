using System.Drawing.Drawing2D;

namespace ItSupportToolbox;

internal sealed class OutputConsole : Panel
{
    private readonly RichTextBox _box;
    private readonly Font _mono = Theme.Mono(9.25f);
    private readonly Font _meta = Theme.Mono(8.75f);
    private readonly Font _title = Theme.Ui(9.5f, FontStyle.Bold);
    private readonly Font _badge = Theme.Ui(8.25f, FontStyle.Bold);

    public event Action? SaveRequested;
    public event Action? ClearRequested;

    public string AllText => _box.Text;

    public OutputConsole()
    {
        DoubleBuffered = true;
        BackColor = Theme.Sidebar;
        Padding = new Padding(1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Sidebar,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var toolbar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar };
        var caption = new Label
        {
            Text = "OUTPUT",
            Font = Theme.Ui(8f, FontStyle.Bold),
            ForeColor = Theme.Faint,
            AutoSize = true,
            Location = new Point(14, 14),
            BackColor = Theme.Sidebar
        };
        toolbar.Controls.Add(caption);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Theme.Sidebar,
            Padding = new Padding(0, 7, 8, 0)
        };

        var clear = new AccentButton("Clear", ButtonKind.Ghost, 0, 26, 8.5f) { Margin = new Padding(0, 0, 6, 0) };
        clear.Click += (_, _) => ClearRequested?.Invoke();
        var save = new AccentButton("Save", ButtonKind.Ghost, 0, 26, 8.5f) { Margin = new Padding(0, 0, 6, 0) };
        save.Click += (_, _) => SaveRequested?.Invoke();
        var copy = new AccentButton("Copy", ButtonKind.Ghost, 0, 26, 8.5f) { Margin = new Padding(0, 0, 6, 0) };
        copy.Click += (_, _) => CopyAll();
        actions.Controls.Add(clear);
        actions.Controls.Add(save);
        actions.Controls.Add(copy);
        toolbar.Controls.Add(actions);

        layout.Controls.Add(toolbar, 0, 0);

        _box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(10, 12, 16),
            ForeColor = Color.FromArgb(199, 207, 221),
            Font = _mono,
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            HideSelection = false,
            TabStop = false
        };
        _box.Margin = Padding.Empty;
        _box.HandleCreated += (_, _) => Native.ApplyDarkScrollbars(_box);
        layout.Controls.Add(_box, 0, 1);
        Controls.Add(layout);

        AppendLine("IT Support Toolbox ready.", Theme.Muted, _mono);
        AppendLine("Choose an action above. Results are captured here and included in the exported report.",
            Theme.Faint, _mono);
        AppendLine(string.Empty, Theme.Faint, _mono);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0 && Height > 0)
        {
            Region? old = Region;
            Region = new Region(Theme.Round(new Rectangle(0, 0, Width, Height), 12));
            old?.Dispose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), 12);
        using var pen = new Pen(Theme.CardBorder, 1f);
        e.Graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }

    public void Append(ToolResult result)
    {
        Color statusColor = result.Success switch
        {
            true => Theme.Green,
            false => Theme.Red,
            _ => Theme.Accent
        };

        AppendColored($"{result.Time:HH:mm:ss}  ", Theme.Faint, _meta);
        AppendColored($"[{result.StatusText}]  ", statusColor, _badge);
        AppendColored(result.Title + Environment.NewLine, Theme.Text, _title);

        if (!string.IsNullOrWhiteSpace(result.Output))
            AppendColored(result.Output.TrimEnd() + Environment.NewLine, Color.FromArgb(199, 207, 221), _mono);

        AppendColored(new string('-', 72) + Environment.NewLine + Environment.NewLine,
            Color.FromArgb(42, 47, 58), _mono);
    }

    public void AppendNote(string title, string message, bool ok)
    {
        AppendColored($"{DateTime.Now:HH:mm:ss}  ", Theme.Faint, _meta);
        AppendColored(ok ? "[OK]  " : "[INFO]  ", ok ? Theme.Green : Theme.Accent, _badge);
        AppendColored(title + Environment.NewLine, Theme.Text, _title);
        if (!string.IsNullOrWhiteSpace(message))
            AppendColored(message + Environment.NewLine, Theme.Muted, _mono);
        AppendColored(new string('-', 72) + Environment.NewLine + Environment.NewLine,
            Color.FromArgb(42, 47, 58), _mono);
    }

    private void AppendLine(string text, Color color, Font font) => AppendColored(text + Environment.NewLine, color, font);

    private void AppendColored(string text, Color color, Font font)
    {
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionColor = color;
        _box.SelectionFont = font;
        _box.AppendText(text);
        _box.SelectionStart = _box.TextLength;
        _box.SelectionLength = 0;
        _box.ScrollToCaret();
    }

    public void CopyAll()
    {
        if (string.IsNullOrEmpty(_box.Text))
            return;
        try { Clipboard.SetText(_box.Text); }
        catch { /* clipboard busy */ }
    }

    public void ClearOutput()
    {
        _box.Clear();
        AppendLine("Output cleared.", Theme.Faint, _mono);
        AppendLine(string.Empty, Theme.Faint, _mono);
    }
}
