using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawing = visual.RenderOpen())
        {
            DrawBackground(drawing, outputWidth, outputHeight, transparentBackground, backgroundColor);
            DrawProtectedLogo(drawing, protectedLogo, layout.LogoBounds);
        }

        return Render(visual, outputWidth, outputHeight);
    }

    public static BitmapSource ComposeWithAiSubtitle(
        BitmapSource protectedLogo,
        BitmapSource aiReference,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor)
    {
        var layout = CanvasLayoutEngine.Calculate(
            outputWidth,
            outputHeight,
            protectedLogo.PixelWidth,
            protectedLogo.PixelHeight,
            reserveSubtitle: true);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawing = visual.RenderOpen())
        {
            DrawBackground(drawing, outputWidth, outputHeight, transparentBackground, backgroundColor);

            if (layout.SubtitleBounds.Width > 0 && layout.SubtitleBounds.Height > 0)
            {
                var subtitleRect = ToRect(layout.SubtitleBounds);
                drawing.PushClip(new RectangleGeometry(subtitleRect));
                drawing.DrawImage(aiReference, new Rect(0, 0, outputWidth, outputHeight));
                drawing.Pop();
            }

            DrawProtectedLogo(drawing, protectedLogo, layout.LogoBounds);
        }

        return Render(visual, outputWidth, outputHeight);
    }

    private static void DrawBackground(
        DrawingContext drawing,
        int outputWidth,
        int outputHeight,
        bool transparentBackground,
        Color backgroundColor)
    {
        if (transparentBackground)
        {
            return;
        }

        drawing.DrawRectangle(
            new SolidColorBrush(backgroundColor),
            null,
            new Rect(0, 0, outputWidth, outputHeight));
    }

    private static void DrawProtectedLogo(DrawingContext drawing, BitmapSource protectedLogo, Int32Rect bounds)
    {
        drawing.DrawImage(protectedLogo, ToRect(bounds));
    }

    private static Rect ToRect(Int32Rect bounds)
    {
        return new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static BitmapSource Render(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
