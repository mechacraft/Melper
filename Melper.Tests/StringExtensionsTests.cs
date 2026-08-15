using Melper.Core;

namespace Melper.Tests;

public class StringExtensionsTests
{
    [Fact]
    public void PassesFilter_LetsEverythingThrough_WhenThereIsNoPattern()
    {
        Assert.True("Crawler".PassesFilter(""));
        Assert.True("Crawler".PassesFilter(null));
    }

    [Fact]
    public void PassesFilter_MatchesTheSameNamesAsRegMatch()
    {
        Assert.True("Crawler".PassesFilter("cra"));
        Assert.True("Crawler".PassesFilter("cra(b|w)"));
        Assert.False("Crawler".PassesFilter("fang"));
    }

    /// <summary>
    /// The filter boxes recalculate on every keystroke, so a pattern is read
    /// half-written far more often than finished.
    /// </summary>
    [Fact]
    public void PassesFilter_MatchesNothing_WhileThePatternIsStillBeingTyped()
    {
        Assert.False("Crawler".PassesFilter("cra("));
        Assert.False("Crawler".PassesFilter("cra["));
    }
}
