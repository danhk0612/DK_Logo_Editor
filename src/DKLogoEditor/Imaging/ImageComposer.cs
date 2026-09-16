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
            if (!transparentBackground)
            {
                drawing.DrawRectangle(
                    new SolidColorBrush(backgroundColor),
                    null,
                    new Rect(0, 0, outputWidth, outputHeight));
            }

            drawing.DrawImage(
                protectedLogo,
                new Rect(
                    layout.LogoBounds.X,
                    layout.LogoBounds.Y,
                    layout.LogoBounds.Width,
                    layout.LogoBounds.Height));
        }

        var bitmap = new RenderTargetBitmap(
            outputWidth,
            outputHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
