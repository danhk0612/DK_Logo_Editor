using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public static class ImagePostProcessor
{
    public static BitmapSource FitToOutput(
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

        var targetRatio = outputWidth / (double)outputHeight;
        var sourceRatio = source.PixelWidth / (double)source.PixelHeight;

        Int32Rect cropBounds;
        if (sourceRatio > targetRatio)
        {
            var cropWidth = Math.Max(1, (int)Math.Round(source.PixelHeight * targetRatio));
            var cropX = Math.Max(0, (source.PixelWidth - cropWidth) / 2);
            cropBounds = new Int32Rect(cropX, 0, Math.Min(cropWidth, source.PixelWidth - cropX), source.PixelHeight);
        }
        else
        {
            var cropHeight = Math.Max(1, (int)Math.Round(source.PixelWidth / targetRatio));
            var cropY = Math.Max(0, (source.PixelHeight - cropHeight) / 2);
            cropBounds = new Int32Rect(0, cropY, source.PixelWidth, Math.Min(cropHeight, source.PixelHeight - cropY));
        }

        var cropped = new CroppedBitmap(source, cropBounds);
        cropped.Freeze();

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

            drawing.DrawImage(cropped, new Rect(0, 0, outputWidth, outputHeight));
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

    public static string SelectClosestAspectRatio(int width, int height)
    {
        var target = width / (double)height;
        var supported = new[]
        {
            (Name: "1:8", Value: 1.0 / 8.0),
            (Name: "1:4", Value: 1.0 / 4.0),
            (Name: "9:16", Value: 9.0 / 16.0),
            (Name: "2:3", Value: 2.0 / 3.0),
            (Name: "3:4", Value: 3.0 / 4.0),
            (Name: "4:5", Value: 4.0 / 5.0),
            (Name: "1:1", Value: 1.0),
            (Name: "5:4", Value: 5.0 / 4.0),
            (Name: "4:3", Value: 4.0 / 3.0),
            (Name: "3:2", Value: 3.0 / 2.0),
            (Name: "16:9", Value: 16.0 / 9.0),
            (Name: "21:9", Value: 21.0 / 9.0),
            (Name: "4:1", Value: 4.0),
            (Name: "8:1", Value: 8.0)
        };

        return supported
            .OrderBy(item => Math.Abs(Math.Log(target / item.Value)))
            .First()
            .Name;
    }
}
