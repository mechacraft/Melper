using Melper.Core.Services;
using Melper.Data;
using Xunit;

namespace Melper.Tests;

public class UnitNameResolverTests
{
    /// <summary>
    /// The real roster: what the resolver has to cope with is exactly the names the game
    /// ships, and the awkward ones (Steel Ball's space, Wasp against War Factory) are
    /// only awkward because they sit next to each other in it. A fresh copy rather than
    /// the shared list, which anything calling <see cref="UnitsCollection.Replace"/>
    /// rewrites in place under every test holding a reference to it.
    /// </summary>
    private static readonly IReadOnlyCollection<Unit> Roster = UnitsCollection.Defaults();

    [Theory]
    [InlineData("Crawler", "Crawler")]
    [InlineData("crawler", "Crawler")]
    [InlineData("crawlers", "Crawler")]
    [InlineData("  Fang  ", "Fang")]
    [InlineData("fangs", "Fang")]
    [InlineData("steel ball", "Steel Ball")]
    [InlineData("SteelBall", "Steel Ball")]
    [InlineData("steel-ball", "Steel Ball")]
    [InlineData("void eye", "Void eye")]
    [InlineData("sabertooth", "Sabertooth")]
    [InlineData("tarantulas", "Tarantula")]
    [InlineData("arc", "Arclight")]
    [InlineData("melting", "Melting Point")]
    [InlineData("factory", "War Factory")]
    public void Resolve_FindsTheUnit(string query, string expected)
    {
        var lookup = UnitNameResolver.Resolve(query, Roster);

        Assert.Equal(expected, lookup.Match?.Name);
    }

    /// <summary>
    /// A prefix two units share is a question, not an answer. Picking the first would be
    /// silently wrong in exactly the case the caller most needs told about.
    /// </summary>
    [Fact]
    public void Resolve_WhenTwoUnitsMatch_SaysWhichOnesRatherThanPickingOne()
    {
        var lookup = UnitNameResolver.Resolve("wa", Roster);

        Assert.Null(lookup.Match);
        Assert.True(lookup.IsAmbiguous);
        Assert.Equal(["Wasp", "War Factory"], lookup.Candidates.Select(x => x.Name));
        Assert.Contains("Wasp", lookup.Problem);
    }

    [Fact]
    public void Resolve_WhenNothingMatches_QuotesTheNameBack()
    {
        var lookup = UnitNameResolver.Resolve("sabertooht", Roster);

        Assert.Null(lookup.Match);
        Assert.False(lookup.IsAmbiguous);
        Assert.Equal("\"sabertooht\" is not a unit", lookup.Problem);
    }

    /// <summary>
    /// An exact name beats a longer one containing it: Fang would otherwise be ambiguous
    /// with Fire Badger and friends the moment a substring rule ran first.
    /// </summary>
    [Fact]
    public void Resolve_PrefersTheExactNameOverALongerOneContainingIt()
    {
        Assert.Equal("Rhino", UnitNameResolver.Resolve("Rhino", Roster).Match?.Name);
        Assert.Equal("Phoenix", UnitNameResolver.Resolve("phoenix", Roster).Match?.Name);
    }

    [Fact]
    public void ResolveAll_KeepsTheOrderAskedForAndDropsRepeats()
    {
        var lookups = UnitNameResolver.ResolveAll(["Tarantula", "crawlers", "Crawler"], Roster);

        Assert.Equal(["Tarantula", "Crawler"], lookups.Select(x => x.Match?.Name));
    }

    /// <summary>
    /// Every bad name in one run: a caller that names five units should not have to fix
    /// them one run at a time.
    /// </summary>
    [Fact]
    public void ResolveAll_ReportsEveryBadNameRatherThanStoppingAtTheFirst()
    {
        var lookups = UnitNameResolver.ResolveAll(["nope", "Fang", "also nope"], Roster);

        Assert.Equal(2, lookups.Count(x => x.Match is null));
    }
}
