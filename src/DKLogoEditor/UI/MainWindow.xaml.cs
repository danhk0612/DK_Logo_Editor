using System.Windows;
using DKLogoEditor.Models;
using DKLogoEditor.Services;
using DKLogoEditor.Storage;

namespace DKLogoEditor.UI;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ModelCatalogService _modelCatalogService = new();
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        RefreshModelOptions();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _settingsStore, _modelCatalogService)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _settings = _settingsStore.Load();
            RefreshModelOptions();
        }
    }

    private void RefreshModelOptions()
    {
        var options = _modelCatalogService.GetOptions(_settings);
        ModelComboBox.ItemsSource = options;
        ModelComboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.ModelId, _settings.DefaultModelId, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }
}
