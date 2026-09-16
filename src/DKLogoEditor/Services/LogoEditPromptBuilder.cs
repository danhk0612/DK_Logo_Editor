using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public static class LogoEditPromptBuilder
{
    public static string Build(LogoSubtitleEditRequest request)
    {
        var backgroundInstruction = request.TransparentBackground
            ? "The editable background should be transparent."
            : $"The editable background should use the requested color {request.BackgroundColorHex ?? "#FFFFFF"}.";

        var subtitleInstruction = string.IsNullOrWhiteSpace(request.Subtitle)
            ? "Do not add any subtitle text."
            : $"Add the subtitle text exactly as written: \"{request.Subtitle}\". Place it naturally relative to the existing logo with appropriate spacing, alignment, scale, and visual balance.";

        return $"""
You are preparing an editable logo composition for a final {request.OutputWidth} x {request.OutputHeight} output.

STRICT PRESERVATION RULE:
- Do not redraw, recolor, restyle, reshape, retouch, simplify, sharpen, blur, or otherwise modify any non-background part of the original logo.
- The original logo artwork may only be moved and proportionally scaled to make room for the subtitle.
- Keep the original logo aspect ratio.
- Do not cover or overlap the original logo artwork with new content.

EDITABLE AREAS:
- Background only.
- Newly added subtitle area only.

{backgroundInstruction}
{subtitleInstruction}

The returned image is an intermediate design reference. Preserve the original logo artwork exactly in appearance while arranging the composition naturally for the requested output aspect ratio.
""";
    }
}
