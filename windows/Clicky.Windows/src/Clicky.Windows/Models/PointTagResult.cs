namespace Clicky.Windows.Models;

public sealed record PointTagResult(
    bool ShouldPoint,
    int X,
    int Y,
    string Label,
    int? ScreenNumber)
{
    public static PointTagResult None { get; } = new(false, 0, 0, string.Empty, null);
}
