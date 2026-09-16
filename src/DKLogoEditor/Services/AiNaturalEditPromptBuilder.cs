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
            ? "Do not add any new text or graphics. Keep the original logo prominent and avoid excessive empty margins."
            : BuildSubtitleInstruction(request);

        var layoutGuidance = request.LayoutPlan is null
            ? "Choose the layout yourself after comparing multiple plausible arrangements."
            : BuildLayoutGuidance(request.LayoutPlan);

        return $"""
Edit the supplied ORIGINAL logo image into a polished final logo composition.

FINAL OUTPUT INTENT
- Target display size: {request.OutputWidth} x {request.OutputHeight}.
- Target aspect ratio: {request.OutputWidth}:{request.OutputHeight}.
- Use the available canvas efficiently. Do not leave the logo unnecessarily small inside a large empty field.
- Keep safe margins, but prefer a compact professional lockup whose visible content occupies most of the useful canvas.
- Nothing may be cropped after downscaling.

ORIGINAL LOGO PRESERVATION — HIGHEST PRIORITY
- Preserve the original logo artwork as faithfully as possible.
- Preserve the exact wording, symbol identity, geometry, proportions, letter shapes, stroke weight, spacing, and color relationships of the existing logo.
- Do not redesign, reinterpret, replace, stylize, simplify, sharpen into a different shape, blur, distort, recolor, or invent any part of the original logo.
- Treat the supplied logo as protected brand artwork. Only its overall proportional scale and position may change when needed for composition.
- Do not crop any part of the original logo.

BACKGROUND
- {backgroundInstruction}

SUPPLEMENTARY NAME
{subtitleInstruction}

AI LAYOUT GUIDANCE
{layoutGuidance}

QUALITY
- Produce a clean, high-resolution, professional corporate logo result.
- Keep all edges crisp and all text readable.
- Do not add decorative effects, mockups, lighting, textures, shadows, frames, extra icons, or unrelated graphics.
- Return only the finished logo composition.
""";
    }

    private static string BuildSubtitleInstruction(NaturalLogoEditRequest request)
    {
        var ratio = request.OutputWidth / (double)request.OutputHeight;
        var wideInstruction = ratio >= 2.5
            ? "Because the target canvas is wide, actively consider a compact horizontal lockup such as placing the supplementary name beside the logo or aligning it to the logo structure. Use centered-below only if it is genuinely the strongest composition."
            : "Actively compare side, below-left, below-right, and centered-below arrangements before choosing.";

        return $"""
Add the supplementary name exactly as written: "{request.Subtitle}".
Decide the most natural professional logo lockup automatically.
Do NOT default to placing the supplementary name centered below the logo.
{wideInstruction}
Choose the placement and alignment from the geometry of this specific logo and the target aspect ratio.
If necessary, proportionally reduce or reposition the original logo to create enough space, but keep it as large as practical and never crop it.
The supplementary name must remain secondary to the main logo, with clean spacing and a visually balanced relationship to the original artwork.
Render the supplementary text sharply and legibly, especially for Korean or other non-Latin characters.
Do not place the supplementary text on a box, strip, plate, banner, ribbon, badge, panel, or separate background shape.
Avoid excessive unused margins around the combined logo and supplementary name.
""";
    }

    private static string BuildLayoutGuidance(LogoLayoutPlan plan)
    {
        static string P(double value) => $"{Math.Clamp(value, 0.0, 1.0) * 100:0.#}%";

        return $"""
A separate AI layout-planning pass already analyzed the source logo. Use its recommendation as strong composition guidance while still preserving the original artwork:
- Main logo approximate region: x {P(plan.LogoX)}, y {P(plan.LogoY)}, width {P(plan.LogoWidth)}, height {P(plan.LogoHeight)}.
- Supplementary name approximate region: x {P(plan.SubtitleX)}, y {P(plan.SubtitleY)}, width {P(plan.SubtitleWidth)}, height {P(plan.SubtitleHeight)}.
- Supplementary name alignment: {plan.SubtitleAlignment}.
These are layout regions, not instructions to redraw the logo. Keep the original logo intact and use only proportional scale/position changes.
Do not force centered-below placement if the planned regions indicate another relationship.
""";
    }
}
