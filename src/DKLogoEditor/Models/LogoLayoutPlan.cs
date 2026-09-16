namespace DKLogoEditor.Models;

public sealed record LogoLayoutPlan(
    double LogoX,
    double LogoY,
    double LogoWidth,
    double LogoHeight,
    double SubtitleX,
    double SubtitleY,
    double SubtitleWidth,
    double SubtitleHeight,
    string SubtitleAlignment)
{
    public static LogoLayoutPlan Fallback(int outputWidth, int outputHeight, int subtitleLength)
    {
        var wide = outputWidth >= outputHeight * 3.2 && subtitleLength <= 10;
        var compact = outputWidth <= 320 || outputHeight <= 100;

        if (wide && compact)
        {
            // Tight horizontal lockup for very small wide logo outputs.
            return new LogoLayoutPlan(
                0.04, 0.12, 0.60, 0.76,
                0.67, 0.30, 0.28, 0.40,
                "left");
        }

        return wide
            ? new LogoLayoutPlan(
                0.04, 0.12, 0.58, 0.76,
                0.65, 0.25, 0.30, 0.50,
                "left")
            : new LogoLayoutPlan(
                0.06, 0.06, 0.88, 0.64,
                0.08, 0.74, 0.54, 0.18,
                "left");
    }
}
