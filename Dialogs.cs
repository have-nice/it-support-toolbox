using System.Runtime.InteropServices;

namespace ItSupportToolbox;

internal static class Native
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static void ApplyDarkTitleBar(Form form)
    {
        if (!form.IsHandleCreated)
            return;

        int enabled = 1;
        if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
    }

    public static void ApplyDarkScrollbars(Control control)
    {
        if (!control.IsHandleCreated)
            return;

        try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
        catch { /* older Windows builds may not support it */ }
    }
}

internal sealed class ModalDialog : Form
{
    private ModalDialog(string title, string message, string confirmText, bool danger, bool showCancel)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Font = Theme.Ui(9.75f);
        Padding = new Padding(24, 20, 24, 18);

        var text = new Label
        {
            Text = message,
            Font = Theme.Ui(9.75f),
            ForeColor = Color.FromArgb(205, 212, 224),
            BackColor = Theme.Card,
            AutoSize = false,
            Dock = DockStyle.Fill
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = 44,
            BackColor = Theme.Card,
            Padding = new Padding(0, 8, 0, 0)
        };

        var confirm = new AccentButton(confirmText,
            danger ? ButtonKind.Danger : ButtonKind.Accent, 104, 36);
        confirm.DialogResult = DialogResult.OK;
        buttons.Controls.Add(confirm);

        if (showCancel)
        {
            var cancel = new AccentButton("Cancel", ButtonKind.Ghost, 90, 36) { DialogResult = DialogResult.Cancel };
            cancel.Margin = new Padding(0, 0, 6, 0);
            buttons.Controls.Add(cancel);
            CancelButton = cancel;
        }
        else
        {
            AcceptButton = confirm;
        }

        Controls.Add(text);
        Controls.Add(buttons);

        int width = 480;
        int textWidth = width - Padding.Horizontal - 2;
        int textHeight = TextRenderer.MeasureText(message, text.Font,
            new Size(textWidth, 0), TextFormatFlags.WordBreak).Height;
        ClientSize = new Size(width, Math.Clamp(textHeight + 118, 180, 420));
        text.MaximumSize = new Size(textWidth, 0);
        Padding = new Padding(24, 22, 24, 18);
        buttons.Height = 46;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.ApplyDarkTitleBar(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.ApplyDarkTitleBar(this);
    }

    public static DialogResult Info(IWin32Window owner, string title, string message, string okText = "Close")
    {
        using var dialog = new ModalDialog(title, message, okText, danger: false, showCancel: false);
        return dialog.ShowDialog(owner);
    }

    public static DialogResult Confirm(IWin32Window owner, string title, string message,
        string confirmText = "Continue", bool danger = true)
    {
        using var dialog = new ModalDialog(title, message, confirmText, danger, showCancel: true);
        return dialog.ShowDialog(owner);
    }
}
