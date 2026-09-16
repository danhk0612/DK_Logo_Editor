using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public sealed class ModelCatalogService
{
    public IReadOnlyList<ModelOption> GetOptions(AppSettings settings)
    {
        var options = ModelPresets.All
            .Select(model => new ModelOption(model.DisplayName, model.ModelId, false))
            .ToList();

        foreach (var modelId in settings.CustomModels)
        {
            var normalized = modelId.Trim();
            if (normalized.Length == 0 || options.Any(option =>
                    string.Equals(option.ModelId, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new ModelOption(normalized, normalized, true));
        }

        return options;
    }
}
