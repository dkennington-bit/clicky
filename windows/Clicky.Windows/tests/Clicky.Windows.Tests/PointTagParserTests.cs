using Clicky.Windows.AI;
using Xunit;

namespace Clicky.Windows.Tests;

public sealed class PointTagParserTests
{
    [Fact]
    public void ParseReturnsNoneForExplicitNoneTag()
    {
        var pointTagParser = new PointTagParser();

        var pointTagResult = pointTagParser.Parse("nothing to point at here. [POINT:none]");

        Assert.False(pointTagResult.ShouldPoint);
    }

    [Fact]
    public void ParseReturnsCoordinatesForCurrentScreenTag()
    {
        var pointTagParser = new PointTagParser();

        var pointTagResult = pointTagParser.Parse("click the save button. [POINT:120,48:save button]");

        Assert.True(pointTagResult.ShouldPoint);
        Assert.Equal(120, pointTagResult.X);
        Assert.Equal(48, pointTagResult.Y);
        Assert.Equal("save button", pointTagResult.Label);
        Assert.Null(pointTagResult.ScreenNumber);
    }

    [Fact]
    public void ParseReturnsCoordinatesForExplicitScreenTag()
    {
        var pointTagParser = new PointTagParser();

        var pointTagResult = pointTagParser.Parse("look over there. [POINT:900,420:settings:screen2]");

        Assert.True(pointTagResult.ShouldPoint);
        Assert.Equal(900, pointTagResult.X);
        Assert.Equal(420, pointTagResult.Y);
        Assert.Equal("settings", pointTagResult.Label);
        Assert.Equal(2, pointTagResult.ScreenNumber);
    }

    [Fact]
    public void RemovePointTagStripsPointMetadata()
    {
        var pointTagParser = new PointTagParser();

        string spokenText = pointTagParser.RemovePointTag("open the command palette. [POINT:42,77:palette]");

        Assert.Equal("open the command palette.", spokenText);
    }
}
