using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DKLogoEditor.Imaging;
using DKLogoEditor.Models;
using DKLogoEditor.Services;

namespace DKLogoEditor.UI;

public partial class MainWindow
{
    private readonly OpenRouterCandidateClient _candidateClient = new();
    private readonly List<GeneratedLogoCandidate> _generatedCandidates = new();
    private int _selectedCandidateIndex = -1;

    private void ImageSelectButtonCandidates_Click(object sender, RoutedEventArgs e)
    {
        var previousPath = _settings.Editor.SourceImagePath;
        ImageSelectButton_Click(sender, e);

        if (!string.Equals(previousPath, _settings.Editor.SourceImagePath, StringComparison.OrdinalIgnoreCase))
        {
            ClearCandidateSession();
        }
    }

    private async void GenerateCandidatesButton_Click(object sender, RoutedEventArgs e)
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
            MessageBox.Show(this, "AI 후보 생성을 사용하려면 설정에서 OpenRouter API Key를 입력해 주세요.", "OpenRouter API Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var transparentBackground = TransparentBackgroundRadioButton.IsChecked == true;
        var backgroundColor = GetBackgroundColorOrDefault();
        var subtitle = SubtitleTextBox.Text.Trim();
        var selectedModelId = (ModelComboBox.SelectedItem as ModelOption)?.ModelId
                              ?? _settings.DefaultModelId;

        _isGenerating = true;
        GenerateButton.IsEnabled = false;
        GenerateButton.Content = "후보 생성 중...";
        SaveEditorState();
        ClearCandidateSession(keepResultPreview: false);

        try
        {
            GenerationStatusTextBlock.Text = "원본 업스케일 중...";
            await Dispatcher.Yield(DispatcherPriority.Render);

            var preparedInput = AiInputPreparationService.UpscaleForAi(_sourceBitmap);
            var preparedBytes = BitmapSourceCodec.EncodePng(preparedInput);

            // OutputWidth/OutputHeight remain in the request for final local resize and
            // prompt context compatibility only. Candidate image generation itself does
            // not send fixed resolution/aspect-ratio parameters to OpenRouter.
            var request = new NaturalLogoEditRequest(
                preparedBytes,
                "image/png",
                selectedModelId,
                subtitle,
                outputWidth,
                outputHeight,
                transparentBackground,
                transparentBackground ? null : FormatColor(backgroundColor),
                string.Empty,
                string.Empty,
                null);

            var totalCost = 0.0;
            var hasCost = false;

            for (var candidateNumber = 1; candidateNumber <= 3; candidateNumber++)
            {
                GenerationStatusTextBlock.Text =
                    $"AI 후보 {candidateNumber}/3 생성 중...\n모델: {selectedModelId}";
                await Dispatcher.Yield(DispatcherPriority.Render);

                var result = await _candidateClient.GenerateAsync(
                    _settings.ApiKey,
                    request,
                    candidateNumber);

                var rawPath = GetCandidateRawPath(_settings.Editor.SourceImagePath, candidateNumber);
                await File.WriteAllBytesAsync(rawPath, result.ImageBytes);

                var candidate = new GeneratedLogoCandidate(
                    candidateNumber,
                    result.ImageBytes,
                    result.MediaType,
                    result.ModelId,
                    result.CostUsd,
                    rawPath);

                _generatedCandidates.Add(candidate);
                SetCandidateThumbnail(candidateNumber - 1, BitmapSourceCodec.Decode(result.ImageBytes));

                if (result.CostUsd.HasValue)
                {
                    hasCost = true;
                    totalCost += result.CostUsd.Value;
                }
            }

            EnableCandidateButtons(true);
            SelectCandidate(0, saveAutomatically: false);

            var costText = hasCost ? $" / 총 비용 ${totalCost:0.######}" : string.Empty;
            GenerationStatusTextBlock.Text =
                $"후보 3개 생성 완료{costText}\n모델: {selectedModelId}\n원하는 후보를 클릭하면 {outputWidth}×{outputHeight}로 로컬 리사이징 후 자동 저장됩니다.";
        }
        catch (Exception ex)
        {
            GenerationStatusTextBlock.Text = $"후보 생성 실패: {ex.Message}";
            MessageBox.Show(this, ex.Message, "후보 생성 실패", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isGenerating = false;
            GenerateButton.Content = "후보 3개 다시 생성";
            UpdateGenerateButtonState();
        }
    }

    private void CandidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not string tag
            || !int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return;
        }

        SelectCandidate(index, saveAutomatically: true);
    }

    private void SelectCandidate(int index, bool saveAutomatically)
    {
        if (index < 0 || index >= _generatedCandidates.Count)
        {
            return;
        }

        if (!TryGetOutputSize(out var outputWidth, out var outputHeight))
        {
            return;
        }

        var candidate = _generatedCandidates[index];
        var rawBitmap = BitmapSourceCodec.Decode(candidate.ImageBytes);
        var finalBitmap = SimpleImageResizer.FitToCanvas(
            rawBitmap,
            outputWidth,
            outputHeight,
            TransparentBackgroundRadioButton.IsChecked == true,
            GetBackgroundColorOrDefault());

        _selectedCandidateIndex = index;
        _resultBitmap = finalBitmap;
        ResultPreview.SetImage(finalBitmap, $"후보 {index + 1} 선택 결과");
        SaveAsButton.IsEnabled = true;
        UpdateCandidateSelectionVisual();

        if (saveAutomatically)
        {
            var outputPath = SetResultAndAutoSave(finalBitmap);
            GenerationStatusTextBlock.Text =
                $"후보 {index + 1} 선택됨\n최종 저장: {Path.GetFileName(outputPath)}\nraw: {Path.GetFileName(candidate.RawFilePath)}";
        }
    }

    private void SetCandidateThumbnail(int index, BitmapSource bitmap)
    {
        switch (index)
        {
            case 0:
                Candidate1Image.Source = bitmap;
                Candidate1Button.IsEnabled = true;
                break;
            case 1:
                Candidate2Image.Source = bitmap;
                Candidate2Button.IsEnabled = true;
                break;
            case 2:
                Candidate3Image.Source = bitmap;
                Candidate3Button.IsEnabled = true;
                break;
        }
    }

    private void EnableCandidateButtons(bool enabled)
    {
        Candidate1Button.IsEnabled = enabled && _generatedCandidates.Count > 0;
        Candidate2Button.IsEnabled = enabled && _generatedCandidates.Count > 1;
        Candidate3Button.IsEnabled = enabled && _generatedCandidates.Count > 2;
    }

    private void UpdateCandidateSelectionVisual()
    {
        var buttons = new[] { Candidate1Button, Candidate2Button, Candidate3Button };
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].BorderThickness = i == _selectedCandidateIndex
                ? new Thickness(3)
                : new Thickness(1);
            buttons[i].BorderBrush = i == _selectedCandidateIndex
                ? new SolidColorBrush(Color.FromRgb(45, 108, 223))
                : SystemColors.ControlDarkBrush;
        }
    }

    private void ClearCandidateSession(bool keepResultPreview = false)
    {
        _generatedCandidates.Clear();
        _selectedCandidateIndex = -1;

        Candidate1Image.Source = null;
        Candidate2Image.Source = null;
        Candidate3Image.Source = null;
        Candidate1Button.IsEnabled = false;
        Candidate2Button.IsEnabled = false;
        Candidate3Button.IsEnabled = false;
        UpdateCandidateSelectionVisual();

        if (!keepResultPreview)
        {
            _resultBitmap = null;
            ResultPreview.SetImage(null, "선택 결과 미리보기");
            SaveAsButton.IsEnabled = false;
            SaveAsButton.ToolTip = null;
        }
    }

    private static string GetCandidateRawPath(string sourcePath, int candidateNumber)
    {
        var directory = string.IsNullOrWhiteSpace(sourcePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
        var baseName = string.IsNullOrWhiteSpace(sourcePath)
            ? "logo"
            : Path.GetFileNameWithoutExtension(sourcePath);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"{baseName}_candidate{candidateNumber}_raw_{stamp}.png");

        if (!File.Exists(path))
        {
            return path;
        }

        for (var suffix = 2; ; suffix++)
        {
            path = Path.Combine(directory, $"{baseName}_candidate{candidateNumber}_raw_{stamp}_{suffix}.png");
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }
}
