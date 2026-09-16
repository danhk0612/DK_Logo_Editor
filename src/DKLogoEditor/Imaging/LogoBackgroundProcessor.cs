using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Imaging;

public sealed record ProtectedLogoLayer(BitmapSource Image, Color DetectedBackgroundColor);

public static class LogoBackgroundProcessor
{
    private const int ColorTolerance = 42;

    public static ProtectedLogoLayer ExtractProtectedLogo(BitmapSource source)
    {
        var processed = RemoveBackgroundPreserveSize(source, null, out var backgroundColor);
        var bounds = FindOpaqueBounds(processed);

        if (bounds.IsEmpty)
        {
            return new ProtectedLogoLayer(processed, backgroundColor);
        }

        var cropped = new CroppedBitmap(processed, bounds);
        cropped.Freeze();
        return new ProtectedLogoLayer(cropped, backgroundColor);
    }

    public static BitmapSource RemoveBackgroundPreserveSize(BitmapSource source, Color? preferredBackground = null)
    {
        return RemoveBackgroundPreserveSize(source, preferredBackground, out _);
    }

    private static BitmapSource RemoveBackgroundPreserveSize(BitmapSource source, Color? preferredBackground, out Color backgroundColor)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(pixels, stride, 0);

        backgroundColor = preferredBackground ?? EstimateBackgroundColor(pixels, width, height, stride);
        var mask = BuildBackgroundMask(pixels, width, height, stride, backgroundColor);

        for (var i = 0; i < mask.Length; i++)
        {
            if (mask[i])
            {
                pixels[i * 4 + 3] = 0;
            }
        }

        var result = BitmapSource.Create(
            width,
            height,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    private static Int32Rect FindOpaqueBounds(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * stride + x * 4 + 3] < 8)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? Int32Rect.Empty
            : new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Color EstimateBackgroundColor(byte[] pixels, int width, int height, int stride)
    {
        var points = new[] { (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1) };
        var red = 0;
        var green = 0;
        var blue = 0;
        var count = 0;

        foreach (var (x, y) in points)
        {
            var index = y * stride + x * 4;
            if (pixels[index + 3] < 16)
            {
                continue;
            }

            blue += pixels[index];
            green += pixels[index + 1];
            red += pixels[index + 2];
            count++;
        }

        return count == 0
            ? Colors.Transparent
            : Color.FromRgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    private static bool[] BuildBackgroundMask(byte[] pixels, int width, int height, int stride, Color backgroundColor)
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
            return alpha < 48;
        }

        if (alpha < 24)
        {
            return false;
        }

        return Math.Abs(pixels[index] - backgroundColor.B) <= ColorTolerance
               && Math.Abs(pixels[index + 1] - backgroundColor.G) <= ColorTolerance
               && Math.Abs(pixels[index + 2] - backgroundColor.R) <= ColorTolerance;
    }
}
