using Melper.Core.Services;
using Melper.Data;

namespace Melper.Tests;

public class UnitFilterBuilderTests
{
    private static readonly List<Unit> All = UnitsCollection.Units;

    /// <summary>
    /// The property the whole picker rests on: the generated pattern must let
    /// through exactly the picked units and nothing else.
    /// </summary>
    private static void AssertRoundTrip(IReadOnlyCollection<Unit> selected)
    {
        var pattern = UnitFilterBuilder.Build(selected, All);
        var matched = UnitFilterBuilder.SelectMatching(pattern, All).Select(x => x.Name).Order().ToList();
        var expected = selected.Select(x => x.Name).Order().ToList();

        Assert.Equal(expected, matched);
    }

    [Fact]
    public void RoundTrips_EverySingleUnit()
    {
        foreach (var unit in All)
        {
            AssertRoundTrip([unit]);
        }
    }

    [Fact]
    public void RoundTrips_EveryCostBracket()
    {
        foreach (var cost in All.Select(x => x.Cost).Distinct())
        {
            AssertRoundTrip(All.Where(x => x.Cost == cost).ToList());
        }

        AssertRoundTrip(All.Where(x => x.Cost is >= 400 and <= 500).ToList());
    }

    [Fact]
    public void RoundTrips_RandomSubsets()
    {
        var random = new Random(20260725);

        for (var i = 0; i < 500; i++)
        {
            var selected = All.Where(_ => random.Next(2) == 0).ToList();
            if (selected.Count == 0 || selected.Count == All.Count)
            {
                continue; // Those two are the "no pattern" cases, covered below.
            }

            AssertRoundTrip(selected);
        }
    }

    [Fact]
    public void AllAndNothingSelected_BothMeanNoPattern()
    {
        Assert.Equal("", UnitFilterBuilder.Build(All, All));
        Assert.Equal("", UnitFilterBuilder.Build([], All));
    }

    [Fact]
    public void Tokens_AreShortNamePrefixes()
    {
        var selected = All.Where(x => x.Cost == 200).ToList();
        var tokens = UnitFilterBuilder.Build(selected, All).Split('|');

        Assert.All(tokens, token =>
        {
            // Three characters unless a rejected name gets in the way, as
            // "Typhoon" does for "Phoenix".
            Assert.InRange(token.Length, 3, 4);
            Assert.Equal(token.ToLowerInvariant(), token);
            Assert.Contains(selected, unit => unit.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void EmptyAndBrokenPatterns_PassEveryUnit()
    {
        Assert.Equal(All.Count, UnitFilterBuilder.SelectMatching("", All).Count);
        Assert.Equal(All.Count, UnitFilterBuilder.SelectMatching(null, All).Count);
        Assert.Equal(All.Count, UnitFilterBuilder.SelectMatching("cra|fan|(", All).Count);
    }

    [Fact]
    public void HandWrittenPattern_SelectsTheSameUnitsItRegenerates()
    {
        var selected = UnitFilterBuilder.SelectMatching("cra|fang|tara|sle|must|mark|steel|saber|hound|storm|arc|vort|badg", All);

        Assert.Equal(
            ["Crawler", "Fang", "Hound", "Arclight", "Marksman", "Mustang", "Sledgehammer", "Stormcaller", "Steel Ball", "Tarantula", "Sabertooth", "Fire Badger", "Vortex"],
            selected.Select(x => x.Name));

        // Regenerating shortens the tokens without changing what passes.
        Assert.Equal("cra|fan|hou|arc|mar|mus|sle|sto|ste|tar|sab|fir|vor", UnitFilterBuilder.Build(selected, All));
        AssertRoundTrip(selected);
    }
}
