using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DKLogoEditor.Models;

namespace DKLogoEditor.Imaging;

public static class ImageComposer
{
    public static BitmapSource ComposeProtectedLogo(
        BitmapSource protectedLogo,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor,
        bool reserveSubtitle)
    {
        var layout = CanvasLayoutEngine.Calculate(
            outputWidth,
            outputHeight,
            protectedLogo.PixelWidth,
            protectedLogo.PixelHeight,
            reserveSubtitle);

        return ComposeBase(protectedLogo, outputWidth, outputHeight, transparentBackground, backgroundColor, layout);
    }

    public static BitmapSource ComposePreparedLayout(
        BitmapSource protectedLogo,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor,
        LogoLayoutPlan plan)
    {
        var layout = CanvasLayoutEngine.Calculate(
            outputWidth,
            outputHeight,
            protectedLogo.PixelWidth,
            protectedLogo.PixelHeight,
            plan);

        return ComposeBase(protectedLogo, outputWidth, outputHeight, transparentBackground, backgroundColor, layout);
    }

    public static BitmapSource OverlayProtectedLogo(
        BitmapSource aiResult,
        BitmapSource protectedLogo,
        LogoLayoutPlan plan)
    {
        var layout = CanvasLayoutEngine.Calculate(
            aiResult.PixelWidth,
            aiResult.PixelHeight,
            protectedLogo.PixelWidth,
            protectedLogo.PixelHeight,
            plan);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(aiResult, new Rect(0, 0, aiResult.PixelWidth, aiResult.PixelHeight));
            DrawProtectedLogo(drawing, protectedLogo, layout.LogoBounds);
        }

        return Render(visual, aiResult.PixelWidth, aiResult.PixelHeight);
    }

    public static BitmapSource ComposeWithAiSubtitle(
        BitmapSource protectedLogo,
        BitmapSource aiReference,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor)
    {
        return ComposeWithAiSubtitle(
            protectedLogo,
            aiReference,
            outputWidth,
            outputHeight,
            transparentBackground,
            backgroundColor,
            LogoLayoutPlan.Fallback(outputWidth, outputHeight, 1));
    }

    public static BitmapSource ComposeWithAiSubtitle(
        BitmapSource protectedLogo,
        BitmapSource aiReference,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor,
        LogoLayoutPlan plan)
    {
        var layout = CanvasLayoutEngine.Calculate(
            outputWidth,
            outputHeight,
            protectedLogo.PixelWidth,
            protectedLogo.PixelHeight,
            plan);

        var aiSubtitleBounds = CanvasLayoutEngine.GetNormalizedSubtitleBounds(aiReference.PixelWidth, aiReference.PixelHeight, plan);
        var aiSubtitleCrop = new CroppedBitmap(aiReference, aiSubtitleBounds);
        aiSubtitleCrop.Freeze();

        var subtitleLayer = LogoBackgroundProcessor.RemoveBackgroundPreserveSize(
            aiSubtitleCrop,
            transparentBackground ? null : backgroundColor);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawing = visual.RenderOpen())
        {
            DrawBackground(drawing, outputWidth, outputHeight, transparentBackground, backgroundColor);
            drawing.DrawImage(subtitleLayer, ToRect(layout.SubtitleBounds));
            DrawProtectedLogo(drawing, protectedLogo, layout.LogoBounds);
        }

        return Render(visual, outputWidth, outputHeight);
    }

    public static int CalculateWorkingScale(int outputWidth, int outputHeight)
    {
        var largest = Math.Max(outputWidth, outputHeight);
        if (largest <= 0)
        {
            return 1;
        }

        return Math.Clamp(1200 / largest, 1, 4);
    }

    private static BitmapSource ComposeBase(
        BitmapSource protectedLogo,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor,
        LogoCanvasLayout layout)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawing = visual.RenderOpen())
        {
            DrawBackground(drawing, outputWidth, outputHeight, transparentBackground, backgroundColor);
            DrawProtectedLogo(drawing, protectedLogo, layout.LogoBounds);
        }

        return Render(visual, outputWidth, outputHeight);
    }

    private static void DrawBackground(DrawingContext drawing, int width, int height, bool transparent, Color color)
    {
        if (!transparent)
        {
            drawing.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, width, height));
        }
    }

    private static void DrawProtectedLogo(DrawingContext drawing, BitmapSource protectedLogo, Int32Rect bounds)
    {
        drawing.DrawImage(protectedLogo, ToRect(bounds));
    }

    private static Rect ToRect(Int32Rect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static BitmapSource Render(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
