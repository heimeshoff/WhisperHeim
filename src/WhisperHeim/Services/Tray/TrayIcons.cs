using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhisperHeim.Services.Tray;

/// <summary>
/// Shared factory for the two-tone microphone icon used both by the system
/// tray (small variant, drawn with a tray-friendly padding) and the main
/// window/taskbar icon (large variant, fills the full 32x32 canvas).
/// </summary>
internal static class TrayIcons
{
    /// <summary>
    /// Creates the two-tone microphone logo for the window/taskbar icon.
    /// Blue capsule head + orange stand, no background box.
    /// </summary>
    public static ImageSource CreateTwoToneLogoIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // Mic paths actual bounds: x=[6,18] y=[2,22.5] → width=12, height=20.5
            // Scale to fill the full 32x32 icon (fit height, center horizontally)
            const double pathW = 12.0;
            const double pathH = 20.5;
            const double pathX = 6.0;
            const double pathY = 2.0;
            double scale = size / pathH;
            double offsetX = (size - pathW * scale) / 2 - pathX * scale;
            double offsetY = -pathY * scale;

            var blueBrush = new SolidColorBrush(Color.FromRgb(0x25, 0xab, 0xfe));
            var orangeBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x8b, 0x00));

            // Microphone head (capsule) - Blue
            var headGeometry = Geometry.Parse("M12,2 C9.79,2 8,3.79 8,6 L8,12 C8,14.21 9.79,16 12,16 C14.21,16 16,14.21 16,12 L16,6 C16,3.79 14.21,2 12,2 Z");
            // Microphone stand (arc + stem + base) - Orange
            var standGeometry = Geometry.Parse("M6,11 L6,12 C6,15.31 8.69,18 12,18 C15.31,18 18,15.31 18,12 L18,11 L16.5,11 L16.5,12 C16.5,14.49 14.49,16.5 12,16.5 C9.51,16.5 7.5,14.49 7.5,12 L7.5,11 Z M11.25,18.5 L11.25,21 L8.5,21 L8.5,22.5 L15.5,22.5 L15.5,21 L12.75,21 L12.75,18.5 Z");

            ctx.PushTransform(new TranslateTransform(offsetX, offsetY));
            ctx.PushTransform(new ScaleTransform(scale, scale));
            ctx.DrawGeometry(blueBrush, null, headGeometry);
            ctx.DrawGeometry(orangeBrush, null, standGeometry);
            ctx.Pop();
            ctx.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
