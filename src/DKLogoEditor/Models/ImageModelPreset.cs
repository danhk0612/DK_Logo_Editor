namespace DKLogoEditor.Models;

public sealed record ImageModelPreset(string DisplayName, string ModelId);

public sealed record ModelOption(string DisplayName, string ModelId, bool IsCustom);

public static class ModelPresets
{
    public const string DefaultModelId = "google/gemini-3.1-flash-image";

    public static IReadOnlyList<ImageModelPreset> All { get; } =
    [
        new("Nano Banana 2", "google/gemini-3.1-flash-image"),
        new("Nano Banana 2 Lite", "google/gemini-3.1-flash-lite-image"),
        new("Nano Banana Pro", "google/gemini-3-pro-image"),
        new("GPT Image 2", "openai/gpt-image-2"),
        new("Recraft V4.1", "recraft/recraft-v4.1")
    ];
}
