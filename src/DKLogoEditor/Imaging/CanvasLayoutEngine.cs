using System.Windows;

namespace DKLogoEditor.Imaging;

public sealed record LogoCanvasLayout(Int32Rect LogoBounds, Int32Rect SubtitleBounds);

public static class CanvasLayoutEngine
{
    public static LogoCanvasLayout Calculate(
        int canvasWidth,
        int canvasHeight,
        int logoWidth,
        int logoHeight,
        bool reserveSubtitle)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || logoWidth <= 0 || logoHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvasWidth));
        }

        var padding = Math.Max(2, (int)Math.Round(Math.Min(canvasWidth, canvasHeight) * 0.05));
        var subtitleHeight = reserveSubtitle
            ? Math.Max(10, (int)Math.Round(canvasHeight * 0.28))
            : 0;

        var availableWidth = Math.Max(1, canvasWidth - padding * 2);
        var availableHeight = Math.Max(1, canvasHeight - padding * 2 - subtitleHeight);
        var scale = Math.Min(
            availableWidth / (double)logoWidth,
            availableHeight / (double)logoHeight);

        var renderedWidth = Math.Max(1, (int)Math.Round(logoWidth * scale));
        var renderedHeight = Math.Max(1, (int)Math.Round(logoHeight * scale));
        var logoX = (canvasWidth - renderedWidth) / 2;
        var logoY = padding + (availableHeight - renderedHeight) / 2;

        var subtitleY = canvasHeight - padding - subtitleHeight;
        var subtitleBounds = reserveSubtitle
            ? new Int32Rect(
                padding,
                subtitleY,
                availableWidth,
                subtitleHeight)
            : new Int32Rect(0, 0, 0, 0);

        return new LogoCanvasLayout(
            new Int32Rect(logoX, logoY, renderedWidth, renderedHeight),
            subtitleBounds);
    }
}
