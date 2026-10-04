using System.Drawing.Drawing2D;
using PortProxyGUI.Data;

namespace PortProxyGUI.UI;

internal static class StatusIcons
{
    internal static Bitmap Create(RuleStatus status, int size)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 20f, size / 20f);
        using var stroke = new Pen(Color.FromArgb(40, 40, 40), 1);
        using var symbol = new Pen(Color.White, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var fill = new SolidBrush(status switch
        {
            RuleStatus.Active => Color.FromArgb(20, 145, 70),
            RuleStatus.Inactive => Color.FromArgb(115, 120, 130),
            _ => Color.FromArgb(255, 196, 40)
        });
        if (status == RuleStatus.Warning)
        {
            PointF[] triangle = [new(10, 2), new(19, 17), new(1, 17)];
            graphics.FillPolygon(fill, triangle);
            graphics.DrawPolygon(stroke, triangle);
            using var mark = new Pen(Color.FromArgb(35, 35, 35), 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(mark, 10, 7, 10, 11);
            graphics.DrawLine(mark, 10, 14, 10, 14.2f);
        }
        else
        {
            graphics.FillEllipse(fill, 2, 2, 16, 16);
            graphics.DrawEllipse(stroke, 2, 2, 16, 16);
            if (status == RuleStatus.Active) graphics.DrawLines(symbol, [new(6, 10), new(9, 13), new(14, 7)]);
            else graphics.DrawLine(symbol, 6, 10, 14, 10);
        }
        return bitmap;
    }

    internal static void Populate(ImageList images, int dpi)
    {
        images.Images.Clear();
        images.ColorDepth = ColorDepth.Depth32Bit;
        var size = Math.Max(16, (int)Math.Round(20 * dpi / 96d));
        images.ImageSize = new Size(size, size);
        // Without a native handle, ImageList retains the original bitmap for lazy
        // creation. Create the handle first so Add copies pixels before disposal.
        _ = images.Handle;
        foreach (var status in Enum.GetValues<RuleStatus>())
        {
            using var bitmap = Create(status, size);
            images.Images.Add(status.ToString(), bitmap);
        }
    }
}
