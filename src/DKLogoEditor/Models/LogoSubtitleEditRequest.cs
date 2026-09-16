namespace DKLogoEditor.Models;

public sealed record LogoSubtitleEditRequest(
    byte[] SourceImageBytes,
    string SourceMediaType,
    string ModelId,
    string Subtitle,
    int OutputWidth,
    int OutputHeight,
    bool TransparentBackground,
    string? BackgroundColorHex);

public sealed record OpenRouterImageResult(
    byte[] ImageBytes,
    string MediaType,
    string ModelId);
