using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public static class LogoEditPromptBuilder
{
    public static string Build(LogoSubtitleEditRequest request)
    {
        var backgroundInstruction = request.TransparentBackground
            ? "Preserve the transparent background. Do not introduce an opaque background."
            : $"Preserve the existing flat background color {request.BackgroundColorHex ?? "#FFFFFF"} exactly.";

        var subtitleInstruction = string.IsNullOrWhiteSpace(request.Subtitle)
            ? "Do not add any subtitle text."
            : $"Add the subtitle text exactly as written: \"{request.Subtitle}\". Draw it naturally inside the empty lower subtitle area only, with appropriate font style, spacing, alignment, and visual balance relative to the protected logo.";

        return $"""
The input image is already prepared as the final {request.OutputWidth} x {request.OutputHeight} logo composition canvas.
The original logo artwork is already positioned and scaled correctly. Treat it as a protected, immutable layer.

STRICT PRESERVATION RULE:
- Do not move, redraw, recolor, restyle, reshape, retouch, simplify, sharpen, blur, or otherwise modify the existing logo artwork.
- Do not add content over the logo artwork.
- Do not alter pixels outside the reserved lower subtitle area except where strictly necessary to preserve transparency.
- Do not recreate or duplicate the logo.

EDITABLE AREA:
- Only the empty lower subtitle area is editable.
- The subtitle must remain entirely inside that area.

{backgroundInstruction}
{subtitleInstruction}

Return a clean logo image with the same overall composition and aspect ratio. The existing logo must remain visually unchanged.
""";
    }
}
