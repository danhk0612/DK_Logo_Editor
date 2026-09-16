using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DKLogoEditor.Models;
using DKLogoEditor.Services;
using DKLogoEditor.Storage;
using Microsoft.Win32;

namespace DKLogoEditor.UI;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ModelCatalogService _modelCatalogService = new();
    private AppSettings _settings;
    private BitmapSource? _sourceBitmap;
    private BitmapSource? _resultBitmap;
    private bool _isInitializing = true;
    private bool _isSynchronizingViewport;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        RefreshModelOptions();
        RestoreEditorState();
        _isInitializing = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveEditorState();
        base.OnClosing(e);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveEditorState();

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

    private void ImageSelectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.webp|모든 파일|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LoadSourceImage(dialog.FileName);
        _settings.Editor.SourceImagePath = dialog.FileName;
        SaveEditorState();
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_resultBitmap is null)
        {
            return;
        }

        var sourcePath = _settings.Editor.SourceImagePath;
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 이미지|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(sourcePath)
                ? "logo_edited.png"
                : $"{Path.GetFileNameWithoutExtension(sourcePath)}_edited.png",
            InitialDirectory = string.IsNullOrWhiteSpace(sourcePath)
                ? null
                : Path.GetDirectoryName(sourcePath)
        };

        if (dialog.ShowDialog(this) == true)
        {
            ResultFileService.SavePng(_resultBitmap, dialog.FileName);
        }
    }

    private void SourcePreview_ViewportChanged(object? sender, PreviewViewportChangedEventArgs e)
    {
        SynchronizeViewport(ResultPreview, e.State);
    }

    private void ResultPreview_ViewportChanged(object? sender, PreviewViewportChangedEventArgs e)
    {
        SynchronizeViewport(SourcePreview, e.State);
    }

    private void SynchronizeViewport(ZoomPanImageViewer target, PreviewViewportState state)
    {
        if (_isSynchronizingViewport)
        {
            return;
        }

        _isSynchronizingViewport = true;
        target.SetViewport(state);
        _isSynchronizingViewport = false;

        if (!_isInitializing)
        {
            _settings.Editor.PreviewZoom = state.Zoom;
            _settings.Editor.PreviewOffsetXRatio = state.OffsetXRatio;
            _settings.Editor.PreviewOffsetYRatio = state.OffsetYRatio;
        }
    }

    private void RefreshModelOptions()
    {
        var options = _modelCatalogService.GetOptions(_settings);
        var preferredModelId = _settings.Editor.SelectedModelId;

        ModelComboBox.ItemsSource = options;
        ModelComboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.ModelId, preferredModelId, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault(option =>
                string.Equals(option.ModelId, _settings.DefaultModelId, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
    }

    private void RestoreEditorState()
    {
        var editor = _settings.Editor;

        ColorBackgroundRadioButton.IsChecked = !editor.UseTransparentBackground;
        TransparentBackgroundRadioButton.IsChecked = editor.UseTransparentBackground;
        BackgroundColorTextBox.Text = string.IsNullOrWhiteSpace(editor.BackgroundColorHex)
            ? "#FFFFFF"
            : editor.BackgroundColorHex;
        SubtitleTextBox.Text = editor.SubtitleText;
        OutputWidthTextBox.Text = editor.OutputWidth.ToString();
        OutputHeightTextBox.Text = editor.OutputHeight.ToString();

        var viewport = new PreviewViewportState(
            editor.PreviewZoom <= 0 ? 1.0 : editor.PreviewZoom,
            editor.PreviewOffsetXRatio,
            editor.PreviewOffsetYRatio);
        SourcePreview.SetViewport(viewport);
        ResultPreview.SetViewport(viewport);
        SourcePreview.SetImage(null, "원본 로고 미리보기");
        ResultPreview.SetImage(null, "결과 미리보기");

        if (!string.IsNullOrWhiteSpace(editor.SourceImagePath) && File.Exists(editor.SourceImagePath))
        {
            LoadSourceImage(editor.SourceImagePath);
        }
    }

    private void LoadSourceImage(string path)
    {
        _sourceBitmap = LoadBitmap(path);
        SourcePreview.SetImage(_sourceBitmap, "원본 로고 미리보기");
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void SaveEditorState()
    {
        if (_isInitializing)
        {
            return;
        }

        var editor = _settings.Editor;
        editor.UseTransparentBackground = TransparentBackgroundRadioButton.IsChecked == true;
        editor.BackgroundColorHex = BackgroundColorTextBox.Text.Trim();
        editor.SubtitleText = SubtitleTextBox.Text;

        if (int.TryParse(OutputWidthTextBox.Text, out var width) && width > 0)
        {
            editor.OutputWidth = width;
        }

        if (int.TryParse(OutputHeightTextBox.Text, out var height) && height > 0)
        {
            editor.OutputHeight = height;
        }

        if (ModelComboBox.SelectedItem is ModelOption selectedModel)
        {
            editor.SelectedModelId = selectedModel.ModelId;
        }

        var viewport = SourcePreview.CurrentViewport;
        editor.PreviewZoom = viewport.Zoom;
        editor.PreviewOffsetXRatio = viewport.OffsetXRatio;
        editor.PreviewOffsetYRatio = viewport.OffsetYRatio;

        _settingsStore.Save(_settings);
    }

    private string SetResultAndAutoSave(BitmapSource result)
    {
        _resultBitmap = result;
        ResultPreview.SetImage(result, "결과 미리보기");
        SaveAsButton.IsEnabled = true;

        var outputPath = ResultFileService.GetAutomaticOutputPath(_settings.Editor.SourceImagePath);
        ResultFileService.SavePng(result, outputPath);
        return outputPath;
    }
}
