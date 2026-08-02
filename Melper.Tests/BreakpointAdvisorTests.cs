using Melper.Core.Services;
using Melper.Data;
using Xunit;

namespace Melper.Tests;

public class BreakpointAdvisorTests
{
    private static Unit MakeUnit(string name, int damage, int health) =>
        new()
        {
            Name = name,
            Speed = 10,
            Damage = damage,
            Health = health,
            CountInPack = 1,
            ReloadTime = TimeSpan.FromSeconds(5)
        };

    private static List<BreakpointSuggestion> Suggest(
        IReadOnlyCollection<Unit> mains,
        IReadOnlyCollection<Unit> opponents,
        IReadOnlyList<UpgradeCandidate> candidates) =>
        BreakpointAdvisor.Suggest(mains, opponents, BuffAggregates.None, BuffAggregates.None, candidates);

    private static readonly UpgradeCandidate Attack = new("Attack1", UpgradeKind.Attack, 12);
    private static readonly UpgradeCandidate Hp = new("Hp1", UpgradeKind.Hp, 15);

    [Fact]
    public void Candidates_WhenNothingOwned_FoldTheTier1PrerequisiteIntoTheTier2Step()
    {
        var candidates = BreakpointAdvisor.AttackAndHpCandidates(
            attack1: false, attack2: false, hp1: false, hp2: false);

        Assert.Equal(
            [("Attack1", 12), ("Attack1+2", 36), ("Hp1", 15), ("Hp1+2", 45)],
            candidates.Select(x => (x.Name, x.Increase)));
    }

    [Fact]
    public void Candidates_WhenTier1Owned_OfferTheTier2StepOnItsOwn()
    {
        var candidates = BreakpointAdvisor.AttackAndHpCandidates(
            attack1: true, attack2: false, hp1: true, hp2: false);

        Assert.Equal(
            [("Attack2", 24), ("Hp2", 30)],
            candidates.Select(x => (x.Name, x.Increase)));
    }

    [Fact]
    public void Candidates_WhenEverythingOwned_AreEmpty()
    {
        Assert.Empty(BreakpointAdvisor.AttackAndHpCandidates(true, true, true, true));
    }

    [Fact]
    public void Suggest_ReportsTheBreakpointAndLeavesTheRestOut()
    {
        // 3 attacks to kill at 100 damage, 2 once +12% lands: ceil(280/112) = 3... but
        // 250 health is the pairing that actually moves - ceil(250/100) = 3, ceil(250/112) = 3.
        var main = MakeUnit("main", damage: 100, health: 1000);
        var moves = MakeUnit("moves", damage: 10, health: 220); // ceil(220/100)=3 -> ceil(220/112)=2
        var stays = MakeUnit("stays", damage: 10, health: 300); // ceil(300/100)=3 -> ceil(300/112)=3

        var found = Suggest([main], [moves, stays], [Attack]);

        var only = Assert.Single(found);
        Assert.Equal("moves", only.Vs.Name);
        Assert.Equal(3, only.Before);
        Assert.Equal(2, only.After);
        Assert.Equal(1.5, only.Ratio, 3);
    }

    [Fact]
    public void Suggest_HpCandidate_CountsTheAttacksTheMainUnitSurvives()
    {
        var main = MakeUnit("main", damage: 100, health: 200);
        var vs = MakeUnit("vs", damage: 100, health: 1_000_000); // out of reach for attack advice

        var found = Suggest([main], [vs], [Hp]);

        var only = Assert.Single(found);
        Assert.Equal(UpgradeKind.Hp, only.Upgrade.Kind);
        Assert.Equal(2, only.Before);  // ceil(200/100)
        Assert.Equal(3, only.After);   // ceil(230/100)
        Assert.Equal(1.5, only.Ratio, 3);
    }

    [Fact]
    public void Suggest_RanksAGenuineCrossingAboveOneThatOnlyBanksTheFlatGain()
    {
        // 265 -> 385 survived on a +45% technology is exactly the 45% it states: the count
        // moved, but no threshold was crossed. 5 -> 6 on a +15% one turns 15% into 20%.
        var main = MakeUnit("main", damage: 1, health: 265);
        var flat = MakeUnit("flat", damage: 1, health: 1);
        var crossing = MakeUnit("crossing", damage: 53, health: 1);

        var found = Suggest(
            [main],
            [flat, crossing],
            [new UpgradeCandidate("Hp1", UpgradeKind.Hp, 15), new UpgradeCandidate("Hp1+2", UpgradeKind.Hp, 45)]);

        var small = found.Single(x => x.Vs.Name == "crossing" && x.Upgrade.Name == "Hp1");
        var large = found.Single(x => x.Vs.Name == "flat" && x.Upgrade.Name == "Hp1+2");

        Assert.Equal((5, 6), (small.Before, small.After));
        Assert.Equal((265, 385), (large.Before, large.After));

        // The plain multiplier would have had it the other way round.
        Assert.True(large.Ratio > small.Ratio);
        Assert.True(found.IndexOf(small) < found.IndexOf(large));
    }

    [Fact]
    public void Suggest_RanksTheCheaperTechnologyFirst_WhenBothReachTheSameBreakpoint()
    {
        var main = MakeUnit("main", damage: 100, health: 1000);
        var cheaply = MakeUnit("cheaply", damage: 0, health: 220); // 3 -> 2 on +12% already
        var dearly = MakeUnit("dearly", damage: 0, health: 250);   // 3 -> 2 only once +36% lands

        var candidates = BreakpointAdvisor.AttackAndHpCandidates(false, false, hp1: true, hp2: true);
        var found = Suggest([main], [cheaply, dearly], candidates);

        // The same 3 -> 2 either way, so what separates them is what it cost to get there.
        Assert.All(found, x => Assert.Equal((3, 2), (x.Before, x.After)));
        Assert.Equal(
            [("cheaply", "Attack1"), ("dearly", "Attack1+2")],
            found.Select(x => (x.Vs.Name, x.Upgrade.Name)));
    }

    [Fact]
    public void Suggest_MeasuresTheBuyAgainstWhatTheSideAlreadyOwns()
    {
        // Increases add up before they divide, so Attack2 on top of an owned Attack1 is
        // 136/112 - a 21% step, not the 24% written on it.
        var main = MakeUnit("main", damage: 100, health: 1000);
        var vs = MakeUnit("vs", damage: 0, health: 250);

        var owned = new BuffAggregates(1d, 12, 1d, 0);
        var found = BreakpointAdvisor.Suggest(
            [main], [vs], owned, BuffAggregates.None,
            [new UpgradeCandidate("Attack2", UpgradeKind.Attack, 24)]);

        var only = Assert.Single(found);
        Assert.Equal(3, only.Before);          // ceil(250 / 112)
        Assert.Equal(2, only.After);           // ceil(250 / 136)
        Assert.Equal(136 / 112d, only.Flat, 6);
        Assert.Equal(1.5 / (136 / 112d), only.Score, 6);
    }

    [Fact]
    public void Suggest_RanksByScore_SoBothKindsAreComparableAndDecisiveMovesComeFirst()
    {
        // One attack of movement is worth the same whichever kind moved it: 2->1 attacks to
        // kill and 2->3 attacks survived are both a doubling and a half respectively, and
        // both belong above a 4->3 that only shaves a quarter off.
        var main = MakeUnit("main", damage: 100, health: 200);
        var oneShot = MakeUnit("oneShot", damage: 0, health: 110);   // 2 -> 1 attacks to kill
        var quarter = MakeUnit("quarter", damage: 0, health: 330);   // 4 -> 3 attacks to kill
        var hitter = MakeUnit("hitter", damage: 100, health: 1) with { IsAir = true }; // 2 -> 3 survived

        var found = Suggest([main], [oneShot, quarter, hitter], [Attack, Hp]);

        Assert.Equal(
            [("oneShot", "Attack1", 2.0), ("hitter", "Hp1", 1.5), ("quarter", "Attack1", 4 / 3d)],
            found.Select(x => (x.Vs.Name, x.Upgrade.Name, x.Ratio)));
    }

    [Fact]
    public void Suggest_TiesBreakTowardsTheSmallerAttackCount()
    {
        var main = MakeUnit("main", damage: 100, health: 1000);
        var near = MakeUnit("near", damage: 10, health: 110);   // 2 -> 1
        var far = MakeUnit("far", damage: 10, health: 390);     // 4 -> 2

        var found = Suggest([main], [near, far], [new UpgradeCandidate("Attack1+2", UpgradeKind.Attack, 100)]);

        Assert.Equal(["near", "far"], found.Select(x => x.Vs.Name));
        Assert.All(found, x => Assert.Equal(2.0, x.Ratio, 3));
    }

    [Fact]
    public void Suggest_DropsADearerTierThatLandsOnTheSameBreakpoint()
    {
        var main = MakeUnit("main", damage: 100, health: 1000);
        var vs = MakeUnit("vs", damage: 10, health: 220); // 3 attacks; +12% and +36% both reach 2

        var candidates = BreakpointAdvisor.AttackAndHpCandidates(false, false, hp1: true, hp2: true);
        var found = Suggest([main], [vs], candidates);

        var only = Assert.Single(found);
        Assert.Equal("Attack1", only.Upgrade.Name);
    }

    [Fact]
    public void Suggest_KeepsADearerTierThatMovesTheCountFurther()
    {
        var main = MakeUnit("main", damage: 100, health: 1000);
        var vs = MakeUnit("vs", damage: 10, health: 135); // 2 attacks; +12% keeps 2, +36% reaches 1

        var candidates = BreakpointAdvisor.AttackAndHpCandidates(false, false, hp1: true, hp2: true);
        var found = Suggest([main], [vs], candidates);

        var only = Assert.Single(found);
        Assert.Equal("Attack1+2", only.Upgrade.Name);
        Assert.Equal(2, only.Before); // measured from what the side owns now, not from Attack1
        Assert.Equal(1, only.After);
    }

    [Fact]
    public void Suggest_SkipsAttackAdviceAgainstAirTheMainUnitCannotReach()
    {
        var ground = MakeUnit("ground", damage: 100, health: 1000);
        var air = MakeUnit("air", damage: 10, health: 220) with { IsAir = true };

        Assert.Empty(Suggest([ground], [air], [Attack]));
    }

    [Fact]
    public void Suggest_StillGivesHpAdviceAgainstAirTheMainUnitCannotReach()
    {
        // The Wasp shoots the Sledgehammer even though the Sledgehammer cannot shoot back,
        // so how many of its attacks are survived is exactly the question worth asking.
        var ground = MakeUnit("ground", damage: 100, health: 200);
        var air = MakeUnit("air", damage: 100, health: 220) with { IsAir = true };

        var only = Assert.Single(Suggest([ground], [air], [Attack, Hp]));
        Assert.Equal(UpgradeKind.Hp, only.Upgrade.Kind);
    }

    [Fact]
    public void Suggest_SkipsHpAdviceWhenTheOpponentCannotReachAnAirMainUnit()
    {
        var air = MakeUnit("air", damage: 100, health: 200) with { IsAir = true };
        var ground = MakeUnit("ground", damage: 100, health: 220);

        var only = Assert.Single(Suggest([air], [ground], [Attack, Hp]));
        Assert.Equal(UpgradeKind.Attack, only.Upgrade.Kind);
    }

    [Fact]
    public void Suggest_GivesHpAdviceWhenTheOpponentOnlyGetsAntiAirFromATechnology()
    {
        var air = MakeUnit("air", damage: 1, health: 200) with { IsAir = true };
        var ground = MakeUnit("ground", damage: 100, health: 220) with { CanAttackAirWithTech = true };

        Assert.Contains(Suggest([air], [ground], [Hp]), x => x.Upgrade.Kind == UpgradeKind.Hp);
    }

    [Fact]
    public void Suggest_IgnoresUnitsWithNoDamage_RatherThanRankingThemFirst()
    {
        // Ceiling of a division by zero casts to int.MinValue, which would sort straight
        // to the top of the list and stay there.
        var support = MakeUnit("support", damage: 0, health: 200);
        var vs = MakeUnit("vs", damage: 0, health: 220);

        Assert.Empty(Suggest([support], [vs], [Attack, Hp]));
    }

    [Fact]
    public void Suggest_OverTheRealRoster_FindsTheAdviceForAFreshMatch()
    {
        var roster = UnitsCollection.Defaults();
        var candidates = BreakpointAdvisor.AttackAndHpCandidates(false, false, false, false);

        var found = Suggest(roster, roster, candidates);

        Assert.NotEmpty(found);

        // Ranked, and nothing that fails to move a count is listed.
        Assert.Equal(found.OrderByDescending(x => x.Score).Select(x => x.Score), found.Select(x => x.Score));
        Assert.All(found, x => Assert.True(x.Ratio > 1));

        // Marksman is the example the page was asked for: it shoots air, so it should have
        // advice against the flyers as well as against the ground roster.
        var marksman = found.Where(x => x.Main.Name == "Marksman").ToList();
        Assert.NotEmpty(marksman);
        Assert.Contains(marksman, x => x.Vs.IsAir);
    }
}
