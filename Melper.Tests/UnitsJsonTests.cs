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
        Assert.True(UnitsCollection.AsOf > new DateOnly(2020, 1, 1));
        Assert.True(UnitsCollection.AsOf <= DateOnly.FromDateTime(DateTime.Now).AddDays(1));
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
