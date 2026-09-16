using System.IO;
using System.Windows.Media.Imaging;

namespace DKLogoEditor.Storage;

public static class ResultFileService
{
    public static string GetAutomaticOutputPath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("원본 이미지 경로가 없습니다.");
        }

        var directory = Path.GetDirectoryName(sourcePath)
                        ?? throw new InvalidOperationException("원본 이미지 폴더를 확인할 수 없습니다.");
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var preferred = Path.Combine(directory, $"{baseName}_edited.png");

        if (!File.Exists(preferred))
        {
            return preferred;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var candidate = Path.Combine(directory, $"{baseName}_edited_{timestamp}.png");
        var suffix = 2;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}_edited_{timestamp}_{suffix}.png");
            suffix++;
        }

        return candidate;
    }

    public static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
