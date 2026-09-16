namespace DKLogoEditor.Models;

public sealed class AppSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string DefaultModelId { get; set; } = ModelPresets.DefaultModelId;

    public List<string> CustomModels { get; set; } = [];
}
