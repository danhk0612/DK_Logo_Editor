using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DKLogoEditor.Imaging;
using DKLogoEditor.Models;
using DKLogoEditor.Services;
using DKLogoEditor.Storage;
using Microsoft.Win32;

namespace DKLogoEditor.UI;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly ModelCatalogService _modelCatalogService = new();
    private readonly OpenRouterImageClient _openRouterImageClient = new();
    private AppSettings _settings;
    private BitmapSource? _sourceBitmap;
    private BitmapSource? _resultBitmap;
    private bool _isInitializing = true;
    private bool _isSynchronizingViewport;
    private bool _isSynchronizingBackgroundColor;
    private bool _isGenerating;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsStore.Load();
        RefreshModelOptions();
        RestoreEditorState();

        ColorBackgroundRadioButton.Checked += BackgroundModeRadioButton_Checked;
        TransparentBackgroundRadioButton.Checked += BackgroundModeRadioButton_Checked;
        BackgroundColorTextBox.TextChanged += BackgroundColorTextBox_TextChanged;
        SubtitleTextBox.TextChanged += EditorValueChanged;
        OutputWidthTextBox.TextChanged += EditorValueChanged;
        OutputHeightTextBox.TextChanged += EditorValueChanged;
        ModelComboBox.SelectionChanged += EditorValueChanged;

        _isInitializing = false;
        SynchronizeBackgroundColorFromText();
        UpdateBackgroundColorUi();
        UpdateGenerateButtonState();
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
            UpdateGenerateButtonState();
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

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating || _sourceBitmap is null)
        {
            return;
        }

        if (!TryGetOutputSize(out var outputWidth, out var outputHeight))
        {
            MessageBox.Show(this, "출력 크기를 올바른 양의 정수로 입력해 주세요.", "출력 크기", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            MessageBox.Show(this, "AI 편집을 사용하려면 설정에서 OpenRouter API Key를 입력해 주세요.", "OpenRouter API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var transparentBackground = TransparentBackgroundRadioButton.IsChecked == true;
        var backgroundColor = GetBackgroundColorOrDefault();
        var subtitle = SubtitleTextBox.Text.Trim();
        var selectedModelId = (ModelComboBox.SelectedItem as ModelOption)?.ModelId
                              ?? _settings.DefaultModelId;
        var aspectRatio = ImagePostProcessor.SelectClosestAspectRatio(outputWidth, outputHeight);

        _isGenerating = true;
        GenerateButton.IsEnabled = false;
        GenerateButton.Content = "AI 편집 중...";
        SaveEditorState();

        try
        {
            var request = new NaturalLogoEditRequest(
                BitmapSourceCodec.EncodePng(_sourceBitmap),
                "image/png",
                selectedModelId,
                subtitle,
                outputWidth,
                outputHeight,
                transparentBackground,
                transparentBackground ? null : FormatColor(backgroundColor),
                aspectRatio,
                "2K");

            var aiResult = await _openRouterImageClient.NaturalEditAsync(_settings.ApiKey, request);
            var aiBitmap = BitmapSourceCodec.Decode(aiResult.ImageBytes);
            var finalResult = ImagePostProcessor.FitToOutput(
                aiBitmap,
                outputWidth,
                outputHeight,
                transparentBackground,
                backgroundColor);

            var outputPath = SetResultAndAutoSave(finalResult);
            SaveAsButton.ToolTip = $"자동 저장됨: {outputPath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "생성 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isGenerating = false;
            GenerateButton.Content = "생성 / 편집";
            UpdateGenerateButtonState();
        }
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

    private void BackgroundColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!BackgroundColorPanel.IsEnabled)
        {
            return;
        }

        if (TryParseHexColor(BackgroundColorTextBox.Text, out var color))
        {
            BackgroundColorPicker.SetColor(color);
        }

        BackgroundColorPickerPopup.IsOpen = true;
    }

    private void BackgroundColorPicker_SelectedColorChanged(object? sender, ColorPickerColorChangedEventArgs e)
    {
        if (_isSynchronizingBackgroundColor)
        {
            return;
        }

        _isSynchronizingBackgroundColor = true;
        BackgroundColorTextBox.Text = FormatColor(e.Color);
        BackgroundColorSwatch.Background = new SolidColorBrush(e.Color);
        _settings.Editor.BackgroundColorHex = FormatColor(e.Color);
        _isSynchronizingBackgroundColor = false;
    }

    private void BackgroundColorTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isSynchronizingBackgroundColor)
        {
            return;
        }

        SynchronizeBackgroundColorFromText();
    }

    private void SynchronizeBackgroundColorFromText()
    {
        if (!TryParseHexColor(BackgroundColorTextBox.Text, out var color))
        {
            return;
        }

        _isSynchronizingBackgroundColor = true;
        BackgroundColorPicker.SetColor(color);
        BackgroundColorSwatch.Background = new SolidColorBrush(color);
        _settings.Editor.BackgroundColorHex = FormatColor(color);
        _isSynchronizingBackgroundColor = false;
    }

    private void BackgroundModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        UpdateBackgroundColorUi();
        if (!_isInitializing)
        {
            _settings.Editor.UseTransparentBackground = TransparentBackgroundRadioButton.IsChecked == true;
        }
    }

    private void UpdateBackgroundColorUi()
    {
        var enabled = TransparentBackgroundRadioButton.IsChecked != true;
        BackgroundColorPanel.IsEnabled = enabled;
        if (!enabled)
        {
            BackgroundColorPickerPopup.IsOpen = false;
        }
    }

    private void EditorValueChanged(object sender, EventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        UpdateGenerateButtonState();
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
        OutputWidthTextBox.Text = editor.OutputWidth.ToString(CultureInfo.InvariantCulture);
        OutputHeightTextBox.Text = editor.OutputHeight.ToString(CultureInfo.InvariantCulture);

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
        _resultBitmap = null;
        SourcePreview.SetImage(_sourceBitmap, "원본 로고 미리보기");
        ResultPreview.SetImage(null, "결과 미리보기");
        SaveAsButton.IsEnabled = false;
        SaveAsButton.ToolTip = null;
        UpdateGenerateButtonState();
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

        if (TryParseHexColor(BackgroundColorTextBox.Text, out var backgroundColor))
        {
            editor.BackgroundColorHex = FormatColor(backgroundColor);
        }

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

    private void UpdateGenerateButtonState()
    {
        GenerateButton.IsEnabled = !_isGenerating && _sourceBitmap is not null;
    }

    private bool TryGetOutputSize(out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!int.TryParse(OutputWidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWidth)
            || !int.TryParse(OutputHeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeight)
            || parsedWidth <= 0
            || parsedHeight <= 0)
        {
            return false;
        }

        width = parsedWidth;
        height = parsedHeight;
        return true;
    }

    private Color GetBackgroundColorOrDefault()
    {
        return TryParseHexColor(BackgroundColorTextBox.Text, out var color)
            ? color
            : Colors.White;
    }

    private static bool TryParseHexColor(string? text, out Color color)
    {
        color = Colors.White;
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        if (!byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            || !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            || !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        color = Color.FromRgb(red, green, blue);
        return true;
    }

    private static string FormatColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
