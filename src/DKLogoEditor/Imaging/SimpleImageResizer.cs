using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public static class SimpleImageResizer
{
    public static BitmapSource FitToCanvas(
        BitmapSource source,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor)
    {
        if (outputWidth <= 0 || outputHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputWidth));
        }

        var scale = Math.Min(
            outputWidth / (double)source.PixelWidth,
            outputHeight / (double)source.PixelHeight);

        var drawWidth = Math.Max(1, source.PixelWidth * scale);
        var drawHeight = Math.Max(1, source.PixelHeight * scale);
        var x = (outputWidth - drawWidth) / 2.0;
        var y = (outputHeight - drawHeight) / 2.0;

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var drawing = visual.RenderOpen())
        {
            if (!transparentBackground)
            {
                drawing.DrawRectangle(
                    new SolidColorBrush(backgroundColor),
                    null,
                    new Rect(0, 0, outputWidth, outputHeight));
            }

            drawing.DrawImage(source, new Rect(x, y, drawWidth, drawHeight));
        }

        var result = new RenderTargetBitmap(
            outputWidth,
            outputHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }
}
