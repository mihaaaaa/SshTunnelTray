using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SshTunnelTray.Domain;

namespace SshTunnelTray.Tray;
public static class TrayIconProvider
{
    public static Icon Create(TunnelState state, bool hasProfile = true, bool notReady = false)
    {
        var color = !hasProfile || notReady ? "#F0B429" : state == TunnelState.Connected ? "#20A269" : "#D94A4A";
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(color));
        using var gate = new GraphicsPath(FillMode.Alternate);
        AddRoundedRectangle(gate, new RectangleF(1, 1, 30, 30), 4.5f);
        gate.StartFigure();
        gate.AddBezier(new PointF(16, 7), new PointF(11.03f, 7), new PointF(7, 11.03f), new PointF(7, 16));
        gate.AddLine(7, 26, 12.5f, 26);
        gate.AddLine(12.5f, 26, 12.5f, 16);
        gate.AddBezier(new PointF(12.5f, 16), new PointF(12.5f, 12.69f), new PointF(19.5f, 12.69f), new PointF(19.5f, 16));
        gate.AddLine(19.5f, 16, 19.5f, 26);
        gate.AddLine(19.5f, 26, 25, 26);
        gate.AddLine(25, 26, 25, 16);
        gate.AddBezier(new PointF(25, 16), new PointF(25, 11.03f), new PointF(20.97f, 7), new PointF(16, 7));
        gate.CloseFigure();
        graphics.FillPath(brush, gate);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        using var temp = new Bitmap(stream);
        var handle = temp.GetHicon();
        try { using var source = Icon.FromHandle(handle); return (Icon)source.Clone(); }
        finally { DestroyIcon(handle); }
    }

    private static void AddRoundedRectangle(GraphicsPath path, RectangleF rectangle, float radius)
    {
        var diameter = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr hIcon);
}
