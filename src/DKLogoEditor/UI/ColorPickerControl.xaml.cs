using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DKLogoEditor.UI;

public sealed class ColorPickerColorChangedEventArgs(Color color) : EventArgs
{
    public Color Color { get; } = color;
}

public partial class ColorPickerControl : UserControl
{
    private double _hue;
    private double _saturation;
    private double _value = 1.0;
    private bool _isSurfaceDragging;
    private bool _isHueDragging;

    public event EventHandler<ColorPickerColorChangedEventArgs>? SelectedColorChanged;

    public Color SelectedColor { get; private set; } = Colors.White;

    public ColorPickerControl()
    {
        InitializeComponent();
    }

    public void SetColor(Color color)
    {
        SelectedColor = color;
        RgbToHsv(color, out _hue, out _saturation, out _value);
        UpdateVisuals();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateVisuals();
    }

    private void ColorSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSurfaceDragging = true;
        ColorSurface.CaptureMouse();
        UpdateFromSurface(e.GetPosition(ColorSurface));
    }

    private void ColorSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSurfaceDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateFromSurface(e.GetPosition(ColorSurface));
        }
    }

    private void ColorSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSurfaceDragging)
        {
            return;
        }

        UpdateFromSurface(e.GetPosition(ColorSurface));
        _isSurfaceDragging = false;
        ColorSurface.ReleaseMouseCapture();
    }

    private void HueBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isHueDragging = true;
        HueBar.CaptureMouse();
        UpdateFromHueBar(e.GetPosition(HueBar));
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isHueDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateFromHueBar(e.GetPosition(HueBar));
        }
    }

    private void HueBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isHueDragging)
        {
            return;
        }

        UpdateFromHueBar(e.GetPosition(HueBar));
        _isHueDragging = false;
        HueBar.ReleaseMouseCapture();
    }

    private void UpdateFromSurface(Point point)
    {
        var width = Math.Max(1.0, ColorSurface.ActualWidth);
        var height = Math.Max(1.0, ColorSurface.ActualHeight);
        _saturation = Math.Clamp(point.X / width, 0.0, 1.0);
        _value = 1.0 - Math.Clamp(point.Y / height, 0.0, 1.0);
        CommitUserColor();
    }

    private void UpdateFromHueBar(Point point)
    {
        var width = Math.Max(1.0, HueBar.ActualWidth);
        _hue = Math.Clamp(point.X / width, 0.0, 1.0) * 360.0;
        CommitUserColor();
    }

    private void CommitUserColor()
    {
        SelectedColor = HsvToColor(_hue, _saturation, _value);
        UpdateVisuals();
        SelectedColorChanged?.Invoke(this, new ColorPickerColorChangedEventArgs(SelectedColor));
    }

    private void UpdateVisuals()
    {
        if (!IsLoaded)
        {
            return;
        }

        HueBase.Background = new SolidColorBrush(HsvToColor(_hue, 1.0, 1.0));

        var surfaceWidth = Math.Max(1.0, ColorSurface.ActualWidth);
        var surfaceHeight = Math.Max(1.0, ColorSurface.ActualHeight);
        Canvas.SetLeft(SurfaceMarker, Math.Clamp(_saturation * surfaceWidth - SurfaceMarker.Width / 2, -SurfaceMarker.Width / 2, surfaceWidth - SurfaceMarker.Width / 2));
        Canvas.SetTop(SurfaceMarker, Math.Clamp((1.0 - _value) * surfaceHeight - SurfaceMarker.Height / 2, -SurfaceMarker.Height / 2, surfaceHeight - SurfaceMarker.Height / 2));

        var hueWidth = Math.Max(1.0, HueBar.ActualWidth);
        Canvas.SetLeft(HueMarker, Math.Clamp((_hue / 360.0) * hueWidth - HueMarker.Width / 2, -HueMarker.Width / 2, hueWidth - HueMarker.Width / 2));
        Canvas.SetTop(HueMarker, -1);
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        hue = ((hue % 360.0) + 360.0) % 360.0;
        saturation = Math.Clamp(saturation, 0.0, 1.0);
        value = Math.Clamp(value, 0.0, 1.0);

        var chroma = value * saturation;
        var x = chroma * (1.0 - Math.Abs((hue / 60.0) % 2.0 - 1.0));
        var m = value - chroma;

        double r1;
        double g1;
        double b1;

        if (hue < 60)
        {
            (r1, g1, b1) = (chroma, x, 0);
        }
        else if (hue < 120)
        {
            (r1, g1, b1) = (x, chroma, 0);
        }
        else if (hue < 180)
        {
            (r1, g1, b1) = (0, chroma, x);
        }
        else if (hue < 240)
        {
            (r1, g1, b1) = (0, x, chroma);
        }
        else if (hue < 300)
        {
            (r1, g1, b1) = (x, 0, chroma);
        }
        else
        {
            (r1, g1, b1) = (chroma, 0, x);
        }

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255.0),
            (byte)Math.Round((g1 + m) * 255.0),
            (byte)Math.Round((b1 + m) * 255.0));
    }

    private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta < 0.000001)
        {
            hue = 0;
        }
        else if (Math.Abs(max - r) < 0.000001)
        {
            hue = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (Math.Abs(max - g) < 0.000001)
        {
            hue = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            hue = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (hue < 0)
        {
            hue += 360.0;
        }

        saturation = max <= 0.000001 ? 0 : delta / max;
        value = max;
    }
}
