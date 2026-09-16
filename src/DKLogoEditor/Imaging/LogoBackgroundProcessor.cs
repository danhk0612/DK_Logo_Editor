using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public sealed record ProtectedLogoLayer(BitmapSource Image, Color DetectedBackgroundColor);

public static class LogoBackgroundProcessor
{
    private const int ColorTolerance = 24;

    public static ProtectedLogoLayer ExtractProtectedLogo(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        var backgroundColor = EstimateBackgroundColor(pixels, width, height, stride);
        var backgroundMask = BuildBackgroundMask(pixels, width, height, stride, backgroundColor);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = y * stride + x * 4;
                var maskIndex = y * width + x;

                if (backgroundMask[maskIndex])
                {
                    pixels[pixelIndex + 3] = 0;
                    continue;
                }

                if (pixels[pixelIndex + 3] == 0)
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
            minX = 0;
            minY = 0;
            maxX = width - 1;
            maxY = height - 1;
        }

        var transparent = BitmapSource.Create(
            width,
            height,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        transparent.Freeze();

        var crop = new Int32Rect(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1);
        var cropped = new CroppedBitmap(transparent, crop);
        cropped.Freeze();

        return new ProtectedLogoLayer(cropped, backgroundColor);
    }

    private static Color EstimateBackgroundColor(byte[] pixels, int width, int height, int stride)
    {
        var points = new[]
        {
            (0, 0),
            (width - 1, 0),
            (0, height - 1),
            (width - 1, height - 1)
        };

        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;

        foreach (var (x, y) in points)
        {
            var index = y * stride + x * 4;
            var alpha = pixels[index + 3];
            if (alpha < 16)
            {
                continue;
            }

            blue += pixels[index];
            green += pixels[index + 1];
            red += pixels[index + 2];
            count++;
        }

        if (count == 0)
        {
            return Colors.Transparent;
        }

        return Color.FromRgb(
            (byte)(red / count),
            (byte)(green / count),
            (byte)(blue / count));
    }

    private static bool[] BuildBackgroundMask(
        byte[] pixels,
        int width,
        int height,
        int stride,
        Color backgroundColor)
    {
        var mask = new bool[width * height];
        var queue = new Queue<(int X, int Y)>();

        for (var x = 0; x < width; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, height - 1);
        }

        for (var y = 1; y < height - 1; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            TryEnqueue(x - 1, y);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y - 1);
            TryEnqueue(x, y + 1);
        }

        return mask;

        void TryEnqueue(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            var maskIndex = y * width + x;
            if (mask[maskIndex])
            {
                return;
            }

            var pixelIndex = y * stride + x * 4;
            if (!IsBackgroundPixel(pixels, pixelIndex, backgroundColor))
            {
                return;
            }

            mask[maskIndex] = true;
            queue.Enqueue((x, y));
        }
    }

    private static bool IsBackgroundPixel(byte[] pixels, int index, Color backgroundColor)
    {
        var alpha = pixels[index + 3];
        if (backgroundColor.A == 0)
        {
            return alpha < 32;
        }

        if (alpha < 32)
        {
            return false;
        }

        return Math.Abs(pixels[index] - backgroundColor.B) <= ColorTolerance
               && Math.Abs(pixels[index + 1] - backgroundColor.G) <= ColorTolerance
               && Math.Abs(pixels[index + 2] - backgroundColor.R) <= ColorTolerance;
    }
}
