namespace DKLogoEditor.Models;

public sealed record NaturalLogoEditRequest(
    byte[] SourceImageBytes,
    string SourceMediaType,
    string ModelId,
    string Subtitle,
    int OutputWidth,
    int OutputHeight,
    bool TransparentBackground,
    string? BackgroundColorHex,
    string AspectRatio,
    string Resolution = "2K",
    LogoLayoutPlan? LayoutPlan = null);
