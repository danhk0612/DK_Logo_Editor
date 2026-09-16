using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public static class LogoEditPromptBuilder
{
    public static string Build(LogoSubtitleEditRequest request)
    {
        var p = request.LayoutPlan
                ?? LogoLayoutPlan.Fallback(request.OutputWidth, request.OutputHeight, request.Subtitle.Length);
        var backgroundInstruction = request.TransparentBackground
            ? "Keep the background transparent. Do not create a white or colored panel behind the subtitle."
            : $"The canvas background is {request.BackgroundColorHex ?? "#FFFFFF"}. Do not create a separate panel, banner, strip, box, or plate behind the subtitle.";

        return $"""
Add ONLY the supplementary name exactly as written: "{request.Subtitle}".
The supplied image is an intermediate {request.OutputWidth} x {request.OutputHeight} composition whose original logo is already positioned correctly.

STRICT RULES:
- Never redraw, edit, recolor, restyle, sharpen, blur, distort, replace, or cover any part of the existing logo.
- Add only the new subtitle text.
- Do not create a box, panel, banner, rectangle, strip, badge, or decorative background behind the subtitle.
- Render clean, crisp professional lettering suitable for a corporate logo lockup.
- Match the visual weight and character of the existing logo without imitating or changing its actual artwork.
- Subtitle alignment: {p.SubtitleAlignment}.
- Place all newly generated subtitle pixels only inside this normalized canvas region: x={p.SubtitleX:F3}, y={p.SubtitleY:F3}, width={p.SubtitleWidth:F3}, height={p.SubtitleHeight:F3}.
- Keep clear space between the existing logo and the subtitle.

{backgroundInstruction}

Return the complete image, but the only visible difference from the supplied image must be the added subtitle text inside the specified region.
""";
    }
}
