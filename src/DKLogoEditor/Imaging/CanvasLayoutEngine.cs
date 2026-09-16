using System.Windows;
using DKLogoEditor.Models;

namespace DKLogoEditor.Imaging;

public sealed record LogoCanvasLayout(Int32Rect LogoBounds, Int32Rect SubtitleBounds, string SubtitleAlignment = "left");

public static class CanvasLayoutEngine
{
    public static LogoCanvasLayout Calculate(
        int canvasWidth,
        int canvasHeight,
        int logoWidth,
        int logoHeight,
        bool reserveSubtitle)
    {
        if (!reserveSubtitle)
        {
            var padding = Math.Max(2, (int)Math.Round(Math.Min(canvasWidth, canvasHeight) * 0.05));
            var bounds = FitInto(
                new Int32Rect(padding, padding, Math.Max(1, canvasWidth - padding * 2), Math.Max(1, canvasHeight - padding * 2)),
                logoWidth,
                logoHeight);
            return new LogoCanvasLayout(bounds, new Int32Rect(0, 0, 0, 0));
        }

        return Calculate(
            canvasWidth,
            canvasHeight,
            logoWidth,
            logoHeight,
            LogoLayoutPlan.Fallback(canvasWidth, canvasHeight, 1));
    }

    public static LogoCanvasLayout Calculate(
        int canvasWidth,
        int canvasHeight,
        int logoWidth,
        int logoHeight,
        LogoLayoutPlan plan)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || logoWidth <= 0 || logoHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(canvasWidth));
        }

        var logoArea = ToSafeRect(canvasWidth, canvasHeight, plan.LogoX, plan.LogoY, plan.LogoWidth, plan.LogoHeight);
        var subtitleArea = ToSafeRect(canvasWidth, canvasHeight, plan.SubtitleX, plan.SubtitleY, plan.SubtitleWidth, plan.SubtitleHeight);
        var logoBounds = FitInto(logoArea, logoWidth, logoHeight);

        if (IntersectsWithMargin(logoBounds, subtitleArea, Math.Max(1, Math.Min(canvasWidth, canvasHeight) / 50)))
        {
            var fallback = LogoLayoutPlan.Fallback(canvasWidth, canvasHeight, 1);
            logoArea = ToSafeRect(canvasWidth, canvasHeight, fallback.LogoX, fallback.LogoY, fallback.LogoWidth, fallback.LogoHeight);
            subtitleArea = ToSafeRect(canvasWidth, canvasHeight, fallback.SubtitleX, fallback.SubtitleY, fallback.SubtitleWidth, fallback.SubtitleHeight);
            logoBounds = FitInto(logoArea, logoWidth, logoHeight);
        }

        return new LogoCanvasLayout(logoBounds, subtitleArea, NormalizeAlignment(plan.SubtitleAlignment));
    }

    public static Int32Rect GetNormalizedSubtitleBounds(int width, int height, LogoLayoutPlan plan)
    {
        return ToSafeRect(width, height, plan.SubtitleX, plan.SubtitleY, plan.SubtitleWidth, plan.SubtitleHeight);
    }

    private static Int32Rect ToSafeRect(int width, int height, double x, double y, double w, double h)
    {
        x = Math.Clamp(x, 0.0, 0.95);
        y = Math.Clamp(y, 0.0, 0.95);
        w = Math.Clamp(w, 0.05, 1.0 - x);
        h = Math.Clamp(h, 0.05, 1.0 - y);

        var px = Math.Clamp((int)Math.Round(x * width), 0, Math.Max(0, width - 1));
        var py = Math.Clamp((int)Math.Round(y * height), 0, Math.Max(0, height - 1));
        var pw = Math.Clamp((int)Math.Round(w * width), 1, width - px);
        var ph = Math.Clamp((int)Math.Round(h * height), 1, height - py);
        return new Int32Rect(px, py, pw, ph);
    }

    private static Int32Rect FitInto(Int32Rect area, int sourceWidth, int sourceHeight)
    {
        var scale = Math.Min(area.Width / (double)sourceWidth, area.Height / (double)sourceHeight);
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new Int32Rect(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height);
    }

    private static bool IntersectsWithMargin(Int32Rect first, Int32Rect second, int margin)
    {
        var expanded = new Int32Rect(
            Math.Max(0, first.X - margin),
            Math.Max(0, first.Y - margin),
            first.Width + margin * 2,
            first.Height + margin * 2);
        return expanded.IntersectsWith(second);
    }

    private static string NormalizeAlignment(string? alignment)
    {
        return alignment?.Trim().ToLowerInvariant() switch
        {
            "center" => "center",
            "right" => "right",
            _ => "left"
        };
    }
}
