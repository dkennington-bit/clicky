using System.Drawing;
using Clicky.Windows.Models;
using Clicky.Windows.Services;
using Xunit;

namespace Clicky.Windows.Tests;

public sealed class CoordinateMapperTests
{
    [Fact]
    public void MapScreenshotPixelToOverlayPointMapsSingleDisplayCenter()
    {
        var capturedDisplayImage = CreateCapturedDisplayImage(
            pixelBounds: new Rectangle(0, 0, 1920, 1080),
            screenshotWidthInPixels: 1920,
            screenshotHeightInPixels: 1080);
        var pointTagResult = new PointTagResult(true, 960, 540, "center", null);

        System.Windows.Point overlayPoint = CoordinateMapper.MapScreenshotPixelToOverlayPoint(
            pointTagResult,
            capturedDisplayImage,
            overlayWidthInDeviceIndependentPixels: 1920,
            overlayHeightInDeviceIndependentPixels: 1080);

        Assert.Equal(960, overlayPoint.X, precision: 3);
        Assert.Equal(540, overlayPoint.Y, precision: 3);
    }

    [Fact]
    public void MapScreenshotPixelToScreenPixelMapsSecondaryDisplayOffset()
    {
        var capturedDisplayImage = CreateCapturedDisplayImage(
            pixelBounds: new Rectangle(1920, 0, 2560, 1440),
            screenshotWidthInPixels: 1280,
            screenshotHeightInPixels: 720);
        var pointTagResult = new PointTagResult(true, 640, 360, "middle", 2);

        System.Windows.Point screenPoint = CoordinateMapper.MapScreenshotPixelToScreenPixel(
            pointTagResult,
            capturedDisplayImage);

        Assert.Equal(3200, screenPoint.X, precision: 3);
        Assert.Equal(720, screenPoint.Y, precision: 3);
    }

    [Fact]
    public void MapScreenshotPixelToOverlayPointScalesForDpiSizedOverlay()
    {
        var capturedDisplayImage = CreateCapturedDisplayImage(
            pixelBounds: new Rectangle(0, 0, 3840, 2160),
            screenshotWidthInPixels: 1920,
            screenshotHeightInPixels: 1080);
        var pointTagResult = new PointTagResult(true, 960, 540, "center", null);

        System.Windows.Point overlayPoint = CoordinateMapper.MapScreenshotPixelToOverlayPoint(
            pointTagResult,
            capturedDisplayImage,
            overlayWidthInDeviceIndependentPixels: 2560,
            overlayHeightInDeviceIndependentPixels: 1440);

        Assert.Equal(1280, overlayPoint.X, precision: 3);
        Assert.Equal(720, overlayPoint.Y, precision: 3);
    }

    private static CapturedDisplayImage CreateCapturedDisplayImage(
        Rectangle pixelBounds,
        int screenshotWidthInPixels,
        int screenshotHeightInPixels)
    {
        return new CapturedDisplayImage(
            ImageBytes: [1, 2, 3],
            Label: "test screen",
            ScreenNumber: 1,
            IsCursorScreen: true,
            PixelBounds: pixelBounds,
            ScreenshotWidthInPixels: screenshotWidthInPixels,
            ScreenshotHeightInPixels: screenshotHeightInPixels);
    }
}
