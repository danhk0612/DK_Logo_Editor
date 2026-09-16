using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public static class AiCandidatePromptBuilder
{
    public static string Build(NaturalLogoEditRequest request, int variantIndex)
    {
        var backgroundInstruction = request.TransparentBackground
            ? "Use a fully transparent background. Do not place the logo or supplementary text on a white, gray, or colored panel."
            : $"Use one uniform solid background color exactly {request.BackgroundColorHex ?? "#FFFFFF"} across the entire canvas. Do not use gradients, panels, strips, patches, textures, or alternate background colors.";

        var subtitleInstruction = string.IsNullOrWhiteSpace(request.Subtitle)
            ? "Do not add any supplementary text, labels, or extra graphics."
            : $"""
Add the supplementary name exactly as written: "{request.Subtitle}".
You decide automatically where it belongs, how large it should be, and how the original logo should be proportionally scaled or repositioned to create the most natural final lockup.
The supplementary name is secondary information. Keep it clearly smaller and visually subordinate to the original logo.
Do not assume it belongs centered below the logo. It may sit beside, below-left, below-right, or in another compact supporting position if that is more natural.
Keep it close enough to the main logo that the composition reads as one logo lockup, not two separate objects.
Render Korean and other non-Latin lettering sharply and exactly. Do not paraphrase, misspell, duplicate, or invent characters.
""";

        var variation = variantIndex switch
        {
            1 => "Favor a conservative, compact corporate lockup with balanced whitespace and strong readability.",
            2 => "Explore a compact horizontal relationship when appropriate, while keeping the supplementary name close to the original logo and clearly secondary.",
            3 => "Explore a different but still restrained professional arrangement. It may use a supporting below or offset alignment if that better fits the original logo, but keep the composition compact.",
            _ => "Choose the most natural compact professional composition."
        };

        return $"""
Create a polished final logo composition from the supplied ORIGINAL logo image.

TARGET
- Intended final display size: {request.OutputWidth} x {request.OutputHeight}.
- The generated image will later be proportionally fitted into that final size without cropping.
- Compose the important content compactly enough that it stays readable after significant downscaling.
- Keep all important content safely away from the outer edges.

ORIGINAL LOGO
- Preserve the original logo's identity and appearance as closely as possible.
- Preserve its wording, symbol, geometry, proportions, letter shapes, stroke relationships, spacing, and color relationships.
- Do not redesign the logo or replace it with a different interpretation.
- You may proportionally scale and reposition the original logo when needed for the overall composition.
- Do not crop the original logo.

BACKGROUND
- {backgroundInstruction}

SUPPLEMENTARY NAME
{subtitleInstruction}

VARIATION DIRECTION
- {variation}

COMPOSITION RULES
- Use the canvas efficiently without excessive empty space.
- Do not scatter the original logo and supplementary name far apart.
- Do not add boxes, ribbons, banners, plates, badges, frames, shadows, lighting effects, mockups, decorative icons, or unrelated graphics.
- Do not place any element so close to an edge that it risks being cut off.
- Prefer clean corporate logo design over poster-like or illustrative composition.

QUALITY
- Produce a clean high-resolution result suitable for later downscaling.
- Keep edges crisp and all text readable.
- Return only the finished logo composition.
""";
    }
}
