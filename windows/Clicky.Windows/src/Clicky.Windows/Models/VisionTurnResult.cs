namespace Clicky.Windows.Models;

public sealed record VisionTurnResult(
    string FullResponseText,
    string SpokenResponseText,
    PointTagResult PointTagResult);
