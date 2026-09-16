using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public static class ImagePostProcessor
{
    private const byte AlphaContentThreshold = 16;
    private const int BackgroundTolerance = 18;

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
        var contentBounds = FindContentBounds(source, transparentBackground, backgroundColor);
        var cropBounds = contentBounds.HasValue
            ? BuildContentAwareCrop(source.PixelWidth, source.PixelHeight, contentBounds.Value, targetRatio)
            : BuildCenteredAspectCrop(source.PixelWidth, source.PixelHeight, targetRatio);

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

    private static Int32Rect? FindContentBounds(
        BitmapSource source,
        bool transparentBackground,
        Color backgroundColor)
    {
        BitmapSource readable = source;
        if (source.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            readable = converted;
        }

        var width = readable.PixelWidth;
        var height = readable.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        readable.CopyPixels(pixels, stride, 0);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var offset = row + (x * 4);
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var alpha = pixels[offset + 3];

                var isContent = transparentBackground
                    ? alpha >= AlphaContentThreshold
                    : alpha >= AlphaContentThreshold && !IsNearBackground(red, green, blue, backgroundColor);

                if (!isContent)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        var contentWidth = maxX - minX + 1;
        var contentHeight = maxY - minY + 1;

        // If almost the whole generated canvas differs from the requested background,
        // background detection is unreliable. Fall back to a simple centered crop.
        if (contentWidth >= width * 0.96 && contentHeight >= height * 0.96)
        {
            return null;
        }

        var padX = Math.Max(4, (int)Math.Round(contentWidth * 0.06));
        var padY = Math.Max(4, (int)Math.Round(contentHeight * 0.08));

        var left = Math.Max(0, minX - padX);
        var top = Math.Max(0, minY - padY);
        var right = Math.Min(width, maxX + 1 + padX);
        var bottom = Math.Min(height, maxY + 1 + padY);

        return new Int32Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static bool IsNearBackground(byte red, byte green, byte blue, Color background)
    {
        return Math.Abs(red - background.R) <= BackgroundTolerance
               && Math.Abs(green - background.G) <= BackgroundTolerance
               && Math.Abs(blue - background.B) <= BackgroundTolerance;
    }

    private static Int32Rect BuildContentAwareCrop(
        int sourceWidth,
        int sourceHeight,
        Int32Rect contentBounds,
        double targetRatio)
    {
        var desiredWidth = (double)contentBounds.Width;
        var desiredHeight = contentBounds.Height;

        if (desiredWidth / desiredHeight > targetRatio)
        {
            desiredHeight = desiredWidth / targetRatio;
        }
        else
        {
            desiredWidth = desiredHeight * targetRatio;
        }

        // Keep a little breathing room beyond the detected content so small outputs
        // do not look mechanically cropped.
        desiredWidth *= 1.04;
        desiredHeight *= 1.04;

        if (desiredWidth > sourceWidth || desiredHeight > sourceHeight)
        {
            var fit = Math.Min(sourceWidth / desiredWidth, sourceHeight / desiredHeight);
            desiredWidth *= fit;
            desiredHeight *= fit;
        }

        var cropWidth = Math.Max(1, Math.Min(sourceWidth, (int)Math.Round(desiredWidth)));
        var cropHeight = Math.Max(1, Math.Min(sourceHeight, (int)Math.Round(desiredHeight)));

        // Correct rounding so the crop follows the requested ratio as closely as possible.
        var roundedRatio = cropWidth / (double)cropHeight;
        if (roundedRatio > targetRatio)
        {
            cropWidth = Math.Max(1, Math.Min(sourceWidth, (int)Math.Round(cropHeight * targetRatio)));
        }
        else
        {
            cropHeight = Math.Max(1, Math.Min(sourceHeight, (int)Math.Round(cropWidth / targetRatio)));
        }

        var centerX = contentBounds.X + contentBounds.Width / 2.0;
        var centerY = contentBounds.Y + contentBounds.Height / 2.0;

        var x = (int)Math.Round(centerX - cropWidth / 2.0);
        var y = (int)Math.Round(centerY - cropHeight / 2.0);
        x = Math.Clamp(x, 0, Math.Max(0, sourceWidth - cropWidth));
        y = Math.Clamp(y, 0, Math.Max(0, sourceHeight - cropHeight));

        // Shift, rather than shrink, when needed to keep all detected content inside.
        if (x > contentBounds.X)
        {
            x = Math.Max(0, contentBounds.X);
        }
        if (x + cropWidth < contentBounds.X + contentBounds.Width)
        {
            x = Math.Min(sourceWidth - cropWidth, contentBounds.X + contentBounds.Width - cropWidth);
        }
        if (y > contentBounds.Y)
        {
            y = Math.Max(0, contentBounds.Y);
        }
        if (y + cropHeight < contentBounds.Y + contentBounds.Height)
        {
            y = Math.Min(sourceHeight - cropHeight, contentBounds.Y + contentBounds.Height - cropHeight);
        }

        return new Int32Rect(
            Math.Clamp(x, 0, Math.Max(0, sourceWidth - cropWidth)),
            Math.Clamp(y, 0, Math.Max(0, sourceHeight - cropHeight)),
            cropWidth,
            cropHeight);
    }

    private static Int32Rect BuildCenteredAspectCrop(int sourceWidth, int sourceHeight, double targetRatio)
    {
        var sourceRatio = sourceWidth / (double)sourceHeight;

        if (sourceRatio > targetRatio)
        {
            var cropWidth = Math.Max(1, (int)Math.Round(sourceHeight * targetRatio));
            var cropX = Math.Max(0, (sourceWidth - cropWidth) / 2);
            return new Int32Rect(cropX, 0, Math.Min(cropWidth, sourceWidth - cropX), sourceHeight);
        }

        var cropHeight = Math.Max(1, (int)Math.Round(sourceWidth / targetRatio));
        var cropY = Math.Max(0, (sourceHeight - cropHeight) / 2);
        return new Int32Rect(0, cropY, sourceWidth, Math.Min(cropHeight, sourceHeight - cropY));
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
