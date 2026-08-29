using Melper.Data;

namespace Melper.Tests;

public class UnitsJsonTests
{
    [Fact]
    public void EmbeddedRoster_Loads()
    {
        Assert.NotEmpty(UnitsCollection.Units);
        Assert.All(UnitsCollection.Units, u => Assert.False(string.IsNullOrWhiteSpace(u.Name)));
    }

    /// <summary>
    /// The Data page prints this and starts warning once it goes stale, so a placeholder
    /// or a date the file never actually had would quietly mislead.
    /// </summary>
    [Fact]
    public void EmbeddedRoster_IsDated()
    {
        Assert.True(UnitsCollection.Date > new DateOnly(2020, 1, 1));
        Assert.True(UnitsCollection.Date <= DateOnly.FromDateTime(DateTime.Now).AddDays(1));
    }

    /// <summary>
    /// The units the roster flags out of the damage calculations, and nothing else.
    /// The flag is only reachable through the Data page's tick, so a typo in the JSON key
    /// would read back as false and quietly put their damage back into every page.
    /// </summary>
    [Fact]
    public void EmbeddedRoster_SkipsDamageForTheStatedUnits()
    {
        Assert.Equal(
            ["Abyss", "Melting Point", "Steel Ball"],
            UnitsCollection.Defaults()
                .Where(u => u.SkipDamageCalculations)
                .Select(u => u.Name)
                .Order());
    }

    [Fact]
    public void RoundTrip_PreservesEveryUnit()
    {
        var again = UnitsJson.Deserialize(UnitsJson.Serialize(UnitsCollection.Units));

        Assert.Equal(UnitsCollection.Units, again);
    }

    /// <summary>
    /// The compact writer drops fields that hold their default, which would silently
    /// break the required members on the way back in. They opt out per property, and
    /// this pins that down for the edited payloads the Data page produces.
    /// </summary>
    [Fact]
    public void RoundTrip_KeepsRequiredMembersAtTheirDefaults()
    {
        var zeroed = UnitsCollection.Defaults()[0] with { Speed = 0, ReloadTime = TimeSpan.Zero };

        var again = UnitsJson.Deserialize(UnitsJson.Serialize([zeroed]));

        Assert.Equal(zeroed, Assert.Single(again));
    }

    /// <summary>
    /// What the Data page exports has to be droppable into <c>units.json</c>, which means
    /// it has to carry the date the required <c>Date</c> member reads back from.
    /// </summary>
    [Fact]
    public void RosterRoundTrip_KeepsTheDateAndTheUnits()
    {
        var roster = new UnitsJson.Roster { Date = new DateOnly(2026, 3, 4), Units = UnitsCollection.Defaults() };

        var again = UnitsJson.DeserializeRoster(UnitsJson.SerializeRoster(roster));

        Assert.Equal(roster.Date, again.Date);
        Assert.Equal(roster.Units, again.Units);
    }

    [Fact]
    public void DeserializeAny_ReadsTheDatedShape()
    {
        var units = UnitsJson.DeserializeAny(UnitsCollection.DefaultJson);

        Assert.Equal(UnitsCollection.Defaults(), units);
    }

    [Fact]
    public void DeserializeAny_ReadsTheBareArrayATabStores()
    {
        var units = UnitsJson.DeserializeAny(UnitsJson.Serialize(UnitsCollection.Defaults()));

        Assert.Equal(UnitsCollection.Defaults(), units);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData("not json at all")]
    [InlineData("""{"Date":"2026-01-01","Units":null}""")]
    [InlineData("[null]")]
    public void DeserializeAny_RefusesWhatItCannotRead(string json) =>
        Assert.ThrowsAny<Exception>(() => UnitsJson.DeserializeAny(json));

    [Fact]
    public void Replace_IsVisibleThroughAnAlreadyCapturedReference()
    {
        var captured = UnitsCollection.Units;
        try
        {
            UnitsCollection.Replace(UnitsCollection.Defaults().Select(u => u with { Cost = 42 }));

            Assert.All(captured, u => Assert.Equal(42, u.Cost));
        }
        finally
        {
            UnitsCollection.Replace(UnitsCollection.Defaults());
        }
    }
}
