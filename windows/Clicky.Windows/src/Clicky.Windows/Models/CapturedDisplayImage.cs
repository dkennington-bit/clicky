using System.Drawing;

namespace Clicky.Windows.Models;

public sealed record CapturedDisplayImage(
    byte[] ImageBytes,
    string Label,
    int ScreenNumber,
    bool IsCursorScreen,
    Rectangle PixelBounds,
    int ScreenshotWidthInPixels,
    int ScreenshotHeightInPixels);
