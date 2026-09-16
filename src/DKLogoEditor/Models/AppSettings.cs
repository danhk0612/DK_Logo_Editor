namespace DKLogoEditor.Models;

public sealed class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string DefaultModelId { get; set; } = ModelPresets.DefaultModelId;

    public List<string> CustomModels { get; set; } = [];

    public EditorSessionSettings Editor { get; set; } = new();
}

public sealed class EditorSessionSettings
{
    public string SourceImagePath { get; set; } = string.Empty;

    public bool UseTransparentBackground { get; set; }

    public string BackgroundColorHex { get; set; } = "#FFFFFF";

    public string SubtitleText { get; set; } = string.Empty;

    public int OutputWidth { get; set; } = 200;

    public int OutputHeight { get; set; } = 60;

    public string SelectedModelId { get; set; } = ModelPresets.DefaultModelId;

    public double PreviewZoom { get; set; } = 1.0;

    public double PreviewOffsetXRatio { get; set; }

    public double PreviewOffsetYRatio { get; set; }
}
