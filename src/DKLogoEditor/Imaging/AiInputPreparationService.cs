using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public static class AiInputPreparationService
{
    private const int MinimumLongEdge = 2048;
    private const int MaximumLongEdge = 4096;

    public static BitmapSource UpscaleForAi(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sourceLongEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (sourceLongEdge <= 0)
        {
            throw new ArgumentException("Source image has invalid dimensions.", nameof(source));
        }

        var targetLongEdge = Math.Clamp(Math.Max(sourceLongEdge, MinimumLongEdge), MinimumLongEdge, MaximumLongEdge);
        var scale = targetLongEdge / (double)sourceLongEdge;

        if (scale <= 1.0001)
        {
            return source;
        }

        var width = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
        var height = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(source, new Rect(0, 0, width, height));
        }

        var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
