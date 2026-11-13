using System.Drawing.Drawing2D;

namespace ItSupportToolbox;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(15, 17, 22);
    public static readonly Color Sidebar = Color.FromArgb(10, 12, 16);
    public static readonly Color Card = Color.FromArgb(23, 26, 33);
    public static readonly Color CardBorder = Color.FromArgb(41, 46, 58);
    public static readonly Color Input = Color.FromArgb(13, 15, 20);
    public static readonly Color Text = Color.FromArgb(232, 236, 244);
    public static readonly Color Muted = Color.FromArgb(148, 156, 171);
    public static readonly Color Faint = Color.FromArgb(96, 104, 120);
    public static readonly Color Accent = Color.FromArgb(76, 141, 255);
    public static readonly Color AccentHover = Color.FromArgb(99, 160, 255);
    public static readonly Color AccentDown = Color.FromArgb(58, 118, 224);
    public static readonly Color Green = Color.FromArgb(52, 199, 123);
    public static readonly Color Amber = Color.FromArgb(245, 180, 80);
    public static readonly Color Red = Color.FromArgb(240, 92, 92);

    public static Font Ui(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style, GraphicsUnit.Point);

    public static Font Mono(float size = 9.5f) =>
        new("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);

    public static Font Icon(float size = 12f) =>
        new("Segoe MDL2 Assets", size, FontStyle.Regular, GraphicsUnit.Point);

    public static GraphicsPath Round(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
