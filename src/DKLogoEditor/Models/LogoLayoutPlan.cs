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

        return wide
            ? new LogoLayoutPlan(
                0.04, 0.12, 0.52, 0.76,
                0.59, 0.18, 0.37, 0.64,
                "left")
            : new LogoLayoutPlan(
                0.06, 0.05, 0.88, 0.66,
                0.08, 0.73, 0.84, 0.22,
                "left");
    }
}
