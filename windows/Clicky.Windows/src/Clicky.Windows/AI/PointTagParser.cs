using System.Text.RegularExpressions;
using Clicky.Windows.Models;
using Clicky.Windows.Services;

namespace Clicky.Windows.AI;

public sealed partial class PointTagParser : IPointTagParser
{
    public PointTagResult Parse(string responseText)
    {
        Match noneMatch = NonePointTagRegex().Match(responseText);
        if (noneMatch.Success)
        {
            return PointTagResult.None;
        }

        Match pointMatch = CoordinatePointTagRegex().Match(responseText);
        if (!pointMatch.Success)
        {
            return PointTagResult.None;
        }

        string label = pointMatch.Groups["label"].Value.Trim();
        int? screenNumber = null;
        if (pointMatch.Groups["screen"].Success &&
            int.TryParse(pointMatch.Groups["screen"].Value, out int parsedScreenNumber))
        {
            screenNumber = parsedScreenNumber;
        }

        return new PointTagResult(
            ShouldPoint: true,
            X: int.Parse(pointMatch.Groups["x"].Value),
            Y: int.Parse(pointMatch.Groups["y"].Value),
            Label: label,
            ScreenNumber: screenNumber);
    }

    public string RemovePointTag(string responseText)
    {
        string withoutCoordinateTag = CoordinatePointTagRegex().Replace(responseText, string.Empty);
        return NonePointTagRegex().Replace(withoutCoordinateTag, string.Empty).Trim();
    }

    [GeneratedRegex(@"\[POINT:none\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonePointTagRegex();

    [GeneratedRegex(@"\[POINT:(?<x>\d+),(?<y>\d+):(?<label>[^\]:]+)(?::screen(?<screen>\d+))?\]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CoordinatePointTagRegex();
}
