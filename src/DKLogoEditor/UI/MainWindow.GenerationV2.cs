using System.Windows;
using System.Windows.Media.Imaging;
using DKLogoEditor.Imaging;
using DKLogoEditor.Models;
using DKLogoEditor.Services;

namespace DKLogoEditor.UI;

public partial class MainWindow
{
    private readonly OpenRouterLayoutPlanner _layoutPlanner = new();

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        GenerateButton.Click -= GenerateButton_Click;
        GenerateButton.Click -= GenerateButtonV2_Click;
        GenerateButton.Click += GenerateButtonV2_Click;
    }

    private async void GenerateButtonV2_Click(object sender, RoutedEventArgs e)
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

        var transparentBackground = TransparentBackgroundRadioButton.IsChecked == true;
        var backgroundColor = GetBackgroundColorOrDefault();
        var subtitle = SubtitleTextBox.Text.Trim();
        var selectedModelId = (ModelComboBox.SelectedItem as ModelOption)?.ModelId
                              ?? _settings.DefaultModelId;

        if (!string.IsNullOrWhiteSpace(subtitle) && string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            MessageBox.Show(this, "부기명 생성과 자동 레이아웃 판단을 위해 설정에서 OpenRouter API Key를 입력해 주세요.", "OpenRouter API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isGenerating = true;
        GenerateButton.IsEnabled = false;
        GenerateButton.Content = "생성 중...";
        SaveEditorState();

        try
        {
            var protectedLayer = LogoBackgroundProcessor.ExtractProtectedLogo(_sourceBitmap);
            BitmapSource finalResult;

            if (string.IsNullOrWhiteSpace(subtitle))
            {
                // No subtitle means no AI call at all. Only the protected original logo is composited locally.
                finalResult = ImageComposer.ComposeProtectedLogo(
                    protectedLayer.Image,
                    outputWidth,
                    outputHeight,
                    transparentBackground,
                    backgroundColor,
                    reserveSubtitle: false);
            }
            else
            {
                var originalBytes = BitmapSourceCodec.EncodePng(_sourceBitmap);
                var plan = await _layoutPlanner.PlanAsync(
                    _settings.ApiKey,
                    originalBytes,
                    "image/png",
                    subtitle,
                    outputWidth,
                    outputHeight);

                var workingScale = ImageComposer.CalculateWorkingScale(outputWidth, outputHeight);
                var workingWidth = checked(outputWidth * workingScale);
                var workingHeight = checked(outputHeight * workingScale);

                var preparedInput = ImageComposer.ComposePreparedLayout(
                    protectedLayer.Image,
                    workingWidth,
                    workingHeight,
                    transparentBackground,
                    backgroundColor,
                    plan);

                var request = new LogoSubtitleEditRequest(
                    BitmapSourceCodec.EncodePng(preparedInput),
                    "image/png",
                    selectedModelId,
                    subtitle,
                    workingWidth,
                    workingHeight,
                    transparentBackground,
                    transparentBackground ? null : FormatColor(backgroundColor),
                    plan);

                var aiResult = await _openRouterImageClient.EditAsync(_settings.ApiKey, request);
                var aiReference = BitmapSourceCodec.Decode(aiResult.ImageBytes);

                finalResult = ImageComposer.ComposeWithAiSubtitle(
                    protectedLayer.Image,
                    aiReference,
                    outputWidth,
                    outputHeight,
                    transparentBackground,
                    backgroundColor,
                    plan);
            }

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
}
