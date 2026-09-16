using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public static class ImagePostProcessor
{
    private const byte AlphaContentThreshold = 16;
    private const int BackgroundTolerance = 24;
    private const int BackgroundFadeTolerance = 44;

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

        var observedBackground = transparentBackground
            ? backgroundColor
            : EstimateEdgeBackground(source, backgroundColor);

        var preparedSource = transparentBackground
            ? source
            : RemoveObservedBackground(source, observedBackground);

        var contentBounds = FindContentBounds(
            preparedSource,
            transparentBackground: true,
            backgroundColor);

        var cropBounds = contentBounds ?? new Int32Rect(0, 0, preparedSource.PixelWidth, preparedSource.PixelHeight);
        var cropped = new CroppedBitmap(preparedSource, cropBounds);
        cropped.Freeze();

        // Never crop visible content to force the requested aspect ratio.
        // Scale the complete detected content into the target canvas instead.
        var scale = Math.Min(
            outputWidth / (double)cropped.PixelWidth,
            outputHeight / (double)cropped.PixelHeight);

        var renderedWidth = Math.Max(1.0, cropped.PixelWidth * scale);
        var renderedHeight = Math.Max(1.0, cropped.PixelHeight * scale);
        var x = (outputWidth - renderedWidth) / 2.0;
        var y = (outputHeight - renderedHeight) / 2.0;

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

            drawing.DrawImage(cropped, new Rect(x, y, renderedWidth, renderedHeight));
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
        var readable = ConvertToBgra32(source);
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

        // Preserve a small safety margin around all detected artwork.
        var padX = Math.Max(4, (int)Math.Round(contentWidth * 0.045));
        var padY = Math.Max(4, (int)Math.Round(contentHeight * 0.07));

        var left = Math.Max(0, minX - padX);
        var top = Math.Max(0, minY - padY);
        var right = Math.Min(width, maxX + 1 + padX);
        var bottom = Math.Min(height, maxY + 1 + padY);

        return new Int32Rect(
            left,
            top,
            Math.Max(1, right - left),
            Math.Max(1, bottom - top));
    }

    private static BitmapSource RemoveObservedBackground(BitmapSource source, Color observedBackground)
    {
        var readable = ConvertToBgra32(source);
        var width = readable.PixelWidth;
        var height = readable.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        readable.CopyPixels(pixels, stride, 0);

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

                if (alpha == 0)
                {
                    continue;
                }

                var distance = Math.Max(
                    Math.Abs(red - observedBackground.R),
                    Math.Max(
                        Math.Abs(green - observedBackground.G),
                        Math.Abs(blue - observedBackground.B)));

                if (distance <= BackgroundTolerance)
                {
                    pixels[offset + 3] = 0;
                    continue;
                }

                if (distance < BackgroundFadeTolerance)
                {
                    var factor = (distance - BackgroundTolerance)
                                 / (double)(BackgroundFadeTolerance - BackgroundTolerance);
                    pixels[offset + 3] = (byte)Math.Clamp(
                        (int)Math.Round(alpha * factor),
                        0,
                        255);
                }
            }
        }

        var result = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    private static Color EstimateEdgeBackground(BitmapSource source, Color fallback)
    {
        var readable = ConvertToBgra32(source);
        var width = readable.PixelWidth;
        var height = readable.PixelHeight;
        if (width <= 0 || height <= 0)
        {
            return fallback;
        }

        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        readable.CopyPixels(pixels, stride, 0);

        var samplePoints = new[]
        {
            (X: 0, Y: 0),
            (X: width - 1, Y: 0),
            (X: 0, Y: height - 1),
            (X: width - 1, Y: height - 1),
            (X: width / 2, Y: 0),
            (X: width / 2, Y: height - 1),
            (X: 0, Y: height / 2),
            (X: width - 1, Y: height / 2)
        };

        var reds = new List<byte>();
        var greens = new List<byte>();
        var blues = new List<byte>();

        foreach (var point in samplePoints)
        {
            var offset = point.Y * stride + point.X * 4;
            if (pixels[offset + 3] < AlphaContentThreshold)
            {
                continue;
            }

            blues.Add(pixels[offset]);
            greens.Add(pixels[offset + 1]);
            reds.Add(pixels[offset + 2]);
        }

        if (reds.Count < 4)
        {
            return fallback;
        }

        reds.Sort();
        greens.Sort();
        blues.Sort();
        var middle = reds.Count / 2;

        return Color.FromRgb(reds[middle], greens[middle], blues[middle]);
    }

    private static BitmapSource ConvertToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static bool IsNearBackground(byte red, byte green, byte blue, Color background)
    {
        return Math.Abs(red - background.R) <= BackgroundTolerance
               && Math.Abs(green - background.G) <= BackgroundTolerance
               && Math.Abs(blue - background.B) <= BackgroundTolerance;
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
