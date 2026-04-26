using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Clicky.Windows.Models;
using Clicky.Windows.Services;
using Point = System.Windows.Point;

namespace Clicky.Windows.UI;

public sealed class OverlayWindowManager : IDisposable
{
    private readonly List<OverlayWindow> overlayWindows = [];

    public OverlayWindowManager()
    {
    }

    public bool IsVisible { get; private set; }

    public void Show()
    {
        if (IsVisible)
        {
            return;
        }

        overlayWindows.Clear();
        foreach (Screen screen in Screen.AllScreens)
        {
            var overlayWindow = new OverlayWindow(screen.Bounds);
            overlayWindows.Add(overlayWindow);
            overlayWindow.Show();
            overlayWindow.StartFollowingCursor();
            overlayWindow.SetIdle();
        }

        IsVisible = true;
    }

    public void Hide()
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.Close();
        }

        overlayWindows.Clear();
        IsVisible = false;
    }

    public async Task<IReadOnlyList<CapturedDisplayImage>> CaptureWithoutOverlayAsync(
        IScreenCaptureService screenCaptureService,
        Window? panelWindow,
        CancellationToken cancellationToken)
    {
        bool wasVisible = IsVisible;
        bool wasPanelVisible = panelWindow?.IsVisible == true;

        if (wasVisible)
        {
            Hide();
        }

        if (wasPanelVisible)
        {
            panelWindow!.Hide();
        }

        await Task.Delay(120, cancellationToken);
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages = await screenCaptureService.CaptureAllDisplaysAsync(cancellationToken);

        if (wasPanelVisible)
        {
            panelWindow!.Show();
        }

        if (wasVisible)
        {
            Show();
        }

        return capturedDisplayImages;
    }

    public void SetIdle()
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetIdle();
        }
    }

    public void SetListening()
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetListening();
        }
    }

    public void SetProcessing()
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetProcessing();
        }
    }

    public void SetResponding()
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetResponding();
        }
    }

    public void SetAudioLevel(double normalizedAudioLevel)
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetAudioLevel(normalizedAudioLevel);
        }
    }

    public void SetResponseText(string text)
    {
        foreach (OverlayWindow overlayWindow in overlayWindows)
        {
            overlayWindow.SetResponseText(text);
        }
    }

    public async Task PointAtAsync(
        VisionTurnResult visionTurnResult,
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages,
        CancellationToken cancellationToken)
    {
        if (!visionTurnResult.PointTagResult.ShouldPoint)
        {
            return;
        }

        CapturedDisplayImage? capturedDisplayImage = ResolveCapturedDisplayImage(
            visionTurnResult.PointTagResult,
            capturedDisplayImages);
        if (capturedDisplayImage is null)
        {
            return;
        }

        OverlayWindow? overlayWindow = overlayWindows.FirstOrDefault(window =>
            window.ScreenPixelBounds.Equals(capturedDisplayImage.PixelBounds));
        if (overlayWindow is null)
        {
            return;
        }

        Point targetScreenPixel = CoordinateMapper.MapScreenshotPixelToScreenPixel(
            visionTurnResult.PointTagResult,
            capturedDisplayImage);

        string label = visionTurnResult.PointTagResult.Label;
        await overlayWindow.PointAtScreenPixelAsync(targetScreenPixel, label, cancellationToken);
    }

    public void Dispose()
    {
        Hide();
    }

    private static CapturedDisplayImage? ResolveCapturedDisplayImage(
        PointTagResult pointTagResult,
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages)
    {
        if (pointTagResult.ScreenNumber is int screenNumber)
        {
            return capturedDisplayImages.FirstOrDefault(displayImage => displayImage.ScreenNumber == screenNumber);
        }

        return capturedDisplayImages.FirstOrDefault(displayImage => displayImage.IsCursorScreen)
               ?? capturedDisplayImages.FirstOrDefault();
    }
}
