using System.Drawing;

namespace Clicky.Windows.Models;

public sealed record ClickyDisplayBounds(
    int ScreenNumber,
    bool IsCursorScreen,
    Rectangle PixelBounds,
    double DpiScaleX,
    double DpiScaleY);
