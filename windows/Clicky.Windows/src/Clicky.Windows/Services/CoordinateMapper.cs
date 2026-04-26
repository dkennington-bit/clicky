using System.Drawing;
using System.Windows;
using Clicky.Windows.Models;

namespace Clicky.Windows.Services;

public static class CoordinateMapper
{
    public static Point MapScreenshotPixelToOverlayPoint(
        PointTagResult pointTagResult,
        CapturedDisplayImage capturedDisplayImage,
        double overlayWidthInDeviceIndependentPixels,
        double overlayHeightInDeviceIndependentPixels)
    {
        if (!pointTagResult.ShouldPoint)
        {
            return new Point(0, 0);
        }

        double normalizedX = Clamp(pointTagResult.X / (double)Math.Max(1, capturedDisplayImage.ScreenshotWidthInPixels), 0, 1);
        double normalizedY = Clamp(pointTagResult.Y / (double)Math.Max(1, capturedDisplayImage.ScreenshotHeightInPixels), 0, 1);

        return new Point(
            normalizedX * overlayWidthInDeviceIndependentPixels,
            normalizedY * overlayHeightInDeviceIndependentPixels);
    }

    public static Point MapScreenshotPixelToScreenPixel(
        PointTagResult pointTagResult,
        CapturedDisplayImage capturedDisplayImage)
    {
        if (!pointTagResult.ShouldPoint)
        {
            return new Point(0, 0);
        }

        double normalizedX = Clamp(pointTagResult.X / (double)Math.Max(1, capturedDisplayImage.ScreenshotWidthInPixels), 0, 1);
        double normalizedY = Clamp(pointTagResult.Y / (double)Math.Max(1, capturedDisplayImage.ScreenshotHeightInPixels), 0, 1);
        Rectangle screenPixelBounds = capturedDisplayImage.PixelBounds;

        return new Point(
            screenPixelBounds.Left + normalizedX * screenPixelBounds.Width,
            screenPixelBounds.Top + normalizedY * screenPixelBounds.Height);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        if (value > maximum)
        {
            return maximum;
        }

        return value;
    }
}
