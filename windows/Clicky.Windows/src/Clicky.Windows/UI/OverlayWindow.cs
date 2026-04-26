using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Clicky.Windows.Infrastructure;
using DrawingRectangle = System.Drawing.Rectangle;
using Point = System.Windows.Point;
using ShapeRectangle = System.Windows.Shapes.Rectangle;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfOrientation = System.Windows.Controls.Orientation;
using WinForms = System.Windows.Forms;

namespace Clicky.Windows.UI;

public sealed class OverlayWindow : Window
{
    private readonly DrawingRectangle screenPixelBounds;
    private readonly Canvas rootCanvas = new();
    private readonly Polygon cursorTriangle = new();
    private readonly Border responseBubble = new();
    private readonly TextBlock responseTextBlock = new();
    private readonly StackPanel waveformStackPanel = new();
    private readonly Ellipse spinnerEllipse = new();
    private readonly DispatcherTimer cursorFollowTimer = new();
    private readonly DispatcherTimer spinnerTimer = new();

    private Point currentCursorPoint = new(80, 80);
    private double spinnerRotationDegrees;
    private bool isPointing;

    public OverlayWindow(DrawingRectangle screenPixelBounds)
    {
        this.screenPixelBounds = screenPixelBounds;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = WpfBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Left = screenPixelBounds.Left;
        Top = screenPixelBounds.Top;
        Width = screenPixelBounds.Width;
        Height = screenPixelBounds.Height;
        Content = rootCanvas;

        BuildCursorTriangle();
        BuildResponseBubble();
        BuildWaveform();
        BuildSpinner();

        SourceInitialized += (_, _) => NativeWindowUtilities.MakeWindowClickThroughAndTopmost(this, screenPixelBounds);

        cursorFollowTimer.Interval = TimeSpan.FromMilliseconds(16);
        cursorFollowTimer.Tick += (_, _) => FollowRealCursor();

        spinnerTimer.Interval = TimeSpan.FromMilliseconds(33);
        spinnerTimer.Tick += (_, _) =>
        {
            spinnerRotationDegrees = (spinnerRotationDegrees + 12) % 360;
            spinnerEllipse.RenderTransform = new RotateTransform(spinnerRotationDegrees, 9, 9);
        };
    }

    public DrawingRectangle ScreenPixelBounds => screenPixelBounds;

    public void StartFollowingCursor()
    {
        cursorFollowTimer.Start();
    }

    public void StopFollowingCursor()
    {
        cursorFollowTimer.Stop();
    }

    public void SetIdle()
    {
        spinnerTimer.Stop();
        cursorTriangle.Visibility = Visibility.Visible;
        waveformStackPanel.Visibility = Visibility.Collapsed;
        spinnerEllipse.Visibility = Visibility.Collapsed;
    }

    public void SetListening()
    {
        spinnerTimer.Stop();
        cursorTriangle.Visibility = Visibility.Collapsed;
        waveformStackPanel.Visibility = Visibility.Visible;
        spinnerEllipse.Visibility = Visibility.Collapsed;
    }

    public void SetProcessing()
    {
        cursorTriangle.Visibility = Visibility.Collapsed;
        waveformStackPanel.Visibility = Visibility.Collapsed;
        spinnerEllipse.Visibility = Visibility.Visible;
        spinnerTimer.Start();
    }

    public void SetResponding()
    {
        spinnerTimer.Stop();
        cursorTriangle.Visibility = Visibility.Visible;
        waveformStackPanel.Visibility = Visibility.Collapsed;
        spinnerEllipse.Visibility = Visibility.Collapsed;
    }

    public void SetAudioLevel(double normalizedAudioLevel)
    {
        int barIndex = 0;
        foreach (ShapeRectangle waveformBar in waveformStackPanel.Children.OfType<ShapeRectangle>())
        {
            double profile = barIndex switch
            {
                0 => 0.45,
                1 => 0.75,
                2 => 1.0,
                3 => 0.75,
                _ => 0.45
            };
            waveformBar.Height = 8 + normalizedAudioLevel * profile * 24;
            barIndex++;
        }
    }

    public void SetResponseText(string text)
    {
        responseTextBlock.Text = text;
        responseBubble.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        PositionOverlayElements(currentCursorPoint);
    }

    public async Task PointAtScreenPixelAsync(Point targetScreenPixel, string label, CancellationToken cancellationToken)
    {
        isPointing = true;
        StopFollowingCursor();
        SetResponding();

        Point targetLocalPoint = ConvertScreenPixelToLocalPoint(targetScreenPixel);
        Point startPoint = currentCursorPoint;
        Point controlPoint = new(
            (startPoint.X + targetLocalPoint.X) / 2,
            Math.Min(startPoint.Y, targetLocalPoint.Y) - 90);

        const int frameCount = 36;
        for (int currentFrame = 1; currentFrame <= frameCount; currentFrame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double progress = currentFrame / (double)frameCount;
            Point nextPoint = QuadraticBezier(startPoint, controlPoint, targetLocalPoint, EaseOutCubic(progress));
            currentCursorPoint = ClampToWindow(nextPoint);
            PositionOverlayElements(currentCursorPoint);
            await Task.Delay(16, cancellationToken);
        }

        SetResponseText(string.IsNullOrWhiteSpace(label) ? "right here" : label);
        await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);
        SetResponseText(string.Empty);

        isPointing = false;
        StartFollowingCursor();
    }

    private void FollowRealCursor()
    {
        if (isPointing)
        {
            return;
        }

        System.Drawing.Point cursorPosition = WinForms.Cursor.Position;
        if (!screenPixelBounds.Contains(cursorPosition))
        {
            rootCanvas.Visibility = Visibility.Hidden;
            return;
        }

        rootCanvas.Visibility = Visibility.Visible;
        currentCursorPoint = ConvertScreenPixelToLocalPoint(new Point(cursorPosition.X, cursorPosition.Y));
        currentCursorPoint = new Point(currentCursorPoint.X + 34, currentCursorPoint.Y + 24);
        PositionOverlayElements(ClampToWindow(currentCursorPoint));
    }

    private void PositionOverlayElements(Point cursorPoint)
    {
        Canvas.SetLeft(cursorTriangle, cursorPoint.X);
        Canvas.SetTop(cursorTriangle, cursorPoint.Y);

        Canvas.SetLeft(waveformStackPanel, cursorPoint.X + 2);
        Canvas.SetTop(waveformStackPanel, cursorPoint.Y + 2);

        Canvas.SetLeft(spinnerEllipse, cursorPoint.X + 4);
        Canvas.SetTop(spinnerEllipse, cursorPoint.Y + 4);

        Canvas.SetLeft(responseBubble, Math.Min(ActualWidth - responseBubble.ActualWidth - 20, cursorPoint.X + 28));
        Canvas.SetTop(responseBubble, Math.Min(ActualHeight - responseBubble.ActualHeight - 20, cursorPoint.Y + 24));
    }

    private Point ConvertScreenPixelToLocalPoint(Point screenPixelPoint)
    {
        double width = Math.Max(1, ActualWidth > 0 ? ActualWidth : Width);
        double height = Math.Max(1, ActualHeight > 0 ? ActualHeight : Height);
        double x = (screenPixelPoint.X - screenPixelBounds.Left) * width / Math.Max(1, screenPixelBounds.Width);
        double y = (screenPixelPoint.Y - screenPixelBounds.Top) * height / Math.Max(1, screenPixelBounds.Height);
        return new Point(x, y);
    }

    private Point ClampToWindow(Point point)
    {
        double width = Math.Max(1, ActualWidth > 0 ? ActualWidth : Width);
        double height = Math.Max(1, ActualHeight > 0 ? ActualHeight : Height);
        return new Point(
            Math.Clamp(point.X, 18, width - 18),
            Math.Clamp(point.Y, 18, height - 18));
    }

    private static Point QuadraticBezier(Point startPoint, Point controlPoint, Point endPoint, double progress)
    {
        double oneMinusProgress = 1 - progress;
        double x = oneMinusProgress * oneMinusProgress * startPoint.X
                   + 2 * oneMinusProgress * progress * controlPoint.X
                   + progress * progress * endPoint.X;
        double y = oneMinusProgress * oneMinusProgress * startPoint.Y
                   + 2 * oneMinusProgress * progress * controlPoint.Y
                   + progress * progress * endPoint.Y;
        return new Point(x, y);
    }

    private static double EaseOutCubic(double progress)
    {
        return 1 - Math.Pow(1 - progress, 3);
    }

    private void BuildCursorTriangle()
    {
        cursorTriangle.Points = new PointCollection
        {
            new(0, 0),
            new(30, 42),
            new(6, 34)
        };
        cursorTriangle.Fill = new SolidColorBrush(WpfColor.FromRgb(48, 144, 255));
        cursorTriangle.Stroke = WpfBrushes.White;
        cursorTriangle.StrokeThickness = 1.6;
        cursorTriangle.StrokeLineJoin = PenLineJoin.Round;
        cursorTriangle.Stretch = Stretch.None;
        cursorTriangle.RenderTransform = new RotateTransform(-35, 12, 20);
        cursorTriangle.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = WpfColor.FromRgb(48, 144, 255),
            BlurRadius = 14,
            Opacity = 0.65
        };
        rootCanvas.Children.Add(cursorTriangle);
    }

    private void BuildResponseBubble()
    {
        responseTextBlock.Foreground = WpfBrushes.White;
        responseTextBlock.FontSize = 13;
        responseTextBlock.TextWrapping = TextWrapping.Wrap;
        responseTextBlock.MaxWidth = 320;
        responseTextBlock.LineHeight = 18;

        responseBubble.Child = responseTextBlock;
        responseBubble.Padding = new Thickness(12, 9, 12, 10);
        responseBubble.CornerRadius = new CornerRadius(8);
        responseBubble.Background = new SolidColorBrush(WpfColor.FromArgb(238, 17, 24, 39));
        responseBubble.BorderBrush = new SolidColorBrush(WpfColor.FromArgb(90, 148, 163, 184));
        responseBubble.BorderThickness = new Thickness(1);
        responseBubble.Visibility = Visibility.Collapsed;
        responseBubble.MaxWidth = 360;
        responseBubble.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 20,
            Opacity = 0.35,
            ShadowDepth = 8
        };

        rootCanvas.Children.Add(responseBubble);
    }

    private void BuildWaveform()
    {
        waveformStackPanel.Orientation = WpfOrientation.Horizontal;
        waveformStackPanel.VerticalAlignment = VerticalAlignment.Center;
        waveformStackPanel.Visibility = Visibility.Collapsed;

        for (int barIndex = 0; barIndex < 5; barIndex++)
        {
            waveformStackPanel.Children.Add(new ShapeRectangle
            {
                Width = 3,
                Height = 12,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = new SolidColorBrush(WpfColor.FromRgb(48, 144, 255)),
                Margin = new Thickness(2, 0, 2, 0)
            });
        }

        rootCanvas.Children.Add(waveformStackPanel);
    }

    private void BuildSpinner()
    {
        spinnerEllipse.Width = 18;
        spinnerEllipse.Height = 18;
        spinnerEllipse.StrokeThickness = 3;
        spinnerEllipse.Stroke = new SolidColorBrush(WpfColor.FromRgb(48, 144, 255));
        spinnerEllipse.Visibility = Visibility.Collapsed;
        rootCanvas.Children.Add(spinnerEllipse);
    }
}
