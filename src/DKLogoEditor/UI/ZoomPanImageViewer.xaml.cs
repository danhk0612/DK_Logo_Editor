using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DKLogoEditor.UI;

public partial class ZoomPanImageViewer : UserControl
{
    private const double MinimumZoom = 0.25;
    private const double MaximumZoom = 16.0;

    private bool _isDragging;
    private bool _suppressViewportChanged;
    private Point _lastMousePosition;
    private double _zoom = 1.0;
    private double _offsetXRatio;
    private double _offsetYRatio;

    public ZoomPanImageViewer()
    {
        InitializeComponent();
    }

    public event EventHandler<PreviewViewportChangedEventArgs>? ViewportChanged;

    public PreviewViewportState CurrentViewport =>
        new(_zoom, _offsetXRatio, _offsetYRatio);

    public void SetImage(ImageSource? source, string placeholder)
    {
        PreviewImage.Source = source;
        PlaceholderTextBlock.Text = placeholder;
        PlaceholderTextBlock.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetViewport(PreviewViewportState state)
    {
        _suppressViewportChanged = true;
        _zoom = Math.Clamp(state.Zoom, MinimumZoom, MaximumZoom);
        _offsetXRatio = state.OffsetXRatio;
        _offsetYRatio = state.OffsetYRatio;
        ApplyTransform();
        _suppressViewportChanged = false;
    }

    private void ViewportBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (PreviewImage.Source is null)
        {
            return;
        }

        var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        _zoom = Math.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        ApplyTransform();
        RaiseViewportChanged();
        e.Handled = true;
    }

    private void ViewportBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (PreviewImage.Source is null)
        {
            return;
        }

        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        ViewportBorder.Cursor = Cursors.SizeAll;
        ViewportBorder.CaptureMouse();
        e.Handled = true;
    }

    private void ViewportBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || PreviewImage.Source is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - _lastMousePosition;
        _lastMousePosition = current;

        var width = Math.Max(ActualWidth, 1.0);
        var height = Math.Max(ActualHeight, 1.0);
        _offsetXRatio += delta.X / width;
        _offsetYRatio += delta.Y / height;

        ApplyTransform();
        RaiseViewportChanged();
    }

    private void ViewportBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
    }

    private void ViewportBorder_LostMouseCapture(object sender, MouseEventArgs e)
    {
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        ViewportBorder.Cursor = Cursors.Arrow;
        ViewportBorder.ReleaseMouseCapture();
    }

    private void ApplyTransform()
    {
        ImageScale.ScaleX = _zoom;
        ImageScale.ScaleY = _zoom;
        ImageTranslate.X = _offsetXRatio * ActualWidth;
        ImageTranslate.Y = _offsetYRatio * ActualHeight;
    }

    private void RaiseViewportChanged()
    {
        if (_suppressViewportChanged)
        {
            return;
        }

        ViewportChanged?.Invoke(
            this,
            new PreviewViewportChangedEventArgs(CurrentViewport));
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyTransform();
    }
}

public readonly record struct PreviewViewportState(
    double Zoom,
    double OffsetXRatio,
    double OffsetYRatio);

public sealed class PreviewViewportChangedEventArgs(PreviewViewportState state) : EventArgs
{
    public PreviewViewportState State { get; } = state;
}
