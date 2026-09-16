using System.Windows;
using DKLogoEditor.Models;
using DKLogoEditor.Services;
using DKLogoEditor.Storage;

namespace DKLogoEditor.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ModelCatalogService _modelCatalogService;
    private readonly List<string> _customModels;

    public SettingsWindow(
        AppSettings settings,
        SettingsStore settingsStore,
        ModelCatalogService modelCatalogService)
    {
        InitializeComponent();

        _settings = settings;
        _settingsStore = settingsStore;
        _modelCatalogService = modelCatalogService;
        _customModels = [.. settings.CustomModels];

        ApiKeyBox.Password = settings.ApiKey;
        RefreshCustomModels();
        RefreshDefaultModels(settings.DefaultModelId);
    }

    private void AddCustomModelButton_Click(object sender, RoutedEventArgs e)
    {
        var modelId = CustomModelTextBox.Text.Trim();
        if (modelId.Length == 0)
        {
            return;
        }

        var isPreset = ModelPresets.All.Any(model =>
            string.Equals(model.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
        var isCustom = _customModels.Any(model =>
            string.Equals(model, modelId, StringComparison.OrdinalIgnoreCase));

        if (isPreset || isCustom)
        {
            return;
        }

        var selectedModelId = (DefaultModelComboBox.SelectedItem as ModelOption)?.ModelId;
        _customModels.Add(modelId);
        CustomModelTextBox.Clear();
        RefreshCustomModels();
        RefreshDefaultModels(selectedModelId);
    }

    private void DeleteCustomModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (CustomModelsListBox.SelectedItem is not string selected)
        {
            return;
        }

        var selectedModelId = (DefaultModelComboBox.SelectedItem as ModelOption)?.ModelId;
        _customModels.Remove(selected);
        RefreshCustomModels();
        RefreshDefaultModels(selectedModelId);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ApiKey = ApiKeyBox.Password.Trim();
        _settings.CustomModels = [.. _customModels];
        _settings.DefaultModelId = (DefaultModelComboBox.SelectedItem as ModelOption)?.ModelId
                                   ?? ModelPresets.DefaultModelId;

        _settingsStore.Save(_settings);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RefreshCustomModels()
    {
        CustomModelsListBox.ItemsSource = null;
        CustomModelsListBox.ItemsSource = _customModels;
    }

    private void RefreshDefaultModels(string? preferredModelId)
    {
        var temporarySettings = new AppSettings
        {
            CustomModels = [.. _customModels]
        };

        var options = _modelCatalogService.GetOptions(temporarySettings);
        DefaultModelComboBox.ItemsSource = options;
        DefaultModelComboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.ModelId, preferredModelId, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }
}
