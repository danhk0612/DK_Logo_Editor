using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public static class AiNaturalEditPromptBuilder
{
    public static string Build(NaturalLogoEditRequest request)
    {
        var backgroundInstruction = request.TransparentBackground
            ? "Use a fully transparent background. Keep only the logo artwork and any requested supplementary text visible. Do not create white, gray, or colored panels behind any element."
            : $"Use one uniform solid background color exactly {request.BackgroundColorHex ?? "#FFFFFF"}. The entire canvas background must be this color with no gradients, patches, strips, shadows, or alternate background areas.";

        var subtitleInstruction = string.IsNullOrWhiteSpace(request.Subtitle)
            ? "Do not add any new text or graphics."
            : $"""
Add the supplementary name exactly as written: "{request.Subtitle}".
Decide the most natural professional logo lockup automatically. You may place the supplementary name below, beside, or in another visually balanced supporting position. Do not assume it belongs below the logo.
If necessary, proportionally reduce or reposition the original logo to create enough space, but keep it as large as practical and never crop it.
The supplementary name must remain secondary to the main logo, with clean spacing and a visually balanced relationship to the original artwork.
Render the supplementary text sharply and legibly, especially for Korean or other non-Latin characters.
Do not place the supplementary text on a box, strip, plate, banner, ribbon, badge, panel, or separate background shape.
""";

        return $"""
Edit the supplied ORIGINAL logo image into a polished final logo composition.

FINAL OUTPUT INTENT
- Target display size: {request.OutputWidth} x {request.OutputHeight}.
- Target aspect ratio: {request.OutputWidth}:{request.OutputHeight}.
- Compose everything so it remains comfortably inside that target frame when the generated image is proportionally fitted down to the final size.
- Keep generous safe margins so no logo or text is cut off after downscaling.

ORIGINAL LOGO PRESERVATION — HIGHEST PRIORITY
- Preserve the original logo artwork as faithfully as possible.
- Preserve the exact wording, symbol identity, geometry, proportions, letter shapes, stroke weight, spacing, and color relationships of the existing logo.
- Do not redesign, reinterpret, replace, stylize, simplify, sharpen into a different shape, blur, distort, recolor, or invent any part of the original logo.
- Treat the supplied logo as protected brand artwork. Only its overall scale and position may change when needed for composition.
- Do not crop any part of the original logo.

BACKGROUND
- {backgroundInstruction}

SUPPLEMENTARY NAME
{subtitleInstruction}

QUALITY
- Produce a clean, high-resolution, professional corporate logo result.
- Keep all edges crisp and all text readable.
- Do not add decorative effects, mockups, lighting, textures, shadows, frames, extra icons, or unrelated graphics.
- Return only the finished logo composition.
""";
    }
}
