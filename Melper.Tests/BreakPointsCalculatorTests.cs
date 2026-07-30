using Melper.Core.Buffs;
using Melper.Core.Services;
using Melper.Data;
using Xunit;

namespace Melper.Tests;

public class BreakPointsCalculatorTests
{
    private sealed class TestBuff : IBuff
    {
        public int DamageIncrease { get; init; }
        public int HpIncrease { get; init; }
    }

    private static Unit MakeUnit(int damage, int health) =>
        new Unit
        {
            Name = "test",
            Speed = 10,
            Damage = damage,
            Health = health,
            CountInPack = 1,
            ReloadTime = TimeSpan.FromSeconds(5)
        };

    [Fact]
    public void Calculate_WithBaseBuffsAndNoNewBuff_KeepsValuesUnchanged()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 500);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 25, HpIncrease = 0 };

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(5, res.ShotsToKill);     // ceil(500 / 100)
        Assert.Equal(4, res.ShotsToKillNew);  // ceil(500 / 125)
        Assert.Equal(10, res.ShotsToLive);    // ceil(1000 / 100)
        Assert.Equal(10, res.ShotsToLiveNew); // HP buff in newMainBuff is 0
    }

    [Fact]
    public void Calculate_VsHpBuff_MakesVsHarderToKill_AndNewDamageBuffReducesShots()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 500);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = new IBuff[] { new TestBuff { DamageIncrease = 0, HpIncrease = 50 } }; // +50% HP => 750
        var newMainBuff = new TestBuff { DamageIncrease = 25, HpIncrease = 0 }; // damage 125

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(8, res.ShotsToKill);     // ceil(750 / 100) = 8
        Assert.Equal(6, res.ShotsToKillNew);  // ceil(750 / 125) = 6
    }

    [Fact]
    public void Calculate_VsDamageBuff_LowersShotsToLive_AndNewHpBuffRaisesItBack()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 500);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = new IBuff[] { new TestBuff { DamageIncrease = 50, HpIncrease = 0 } }; // vs damage 150
        var newMainBuff = new TestBuff { DamageIncrease = 0, HpIncrease = 20 }; // main HP 1200

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(7, res.ShotsToLive);     // ceil(1000 / 150) = 7
        Assert.Equal(8, res.ShotsToLiveNew);  // ceil(1200 / 150) = 8
    }

    [Fact]
    public void Calculate_BoundaryCeilingBehavior_IsCorrect()
    {
        // Arrange
        var main = MakeUnit(damage: 50, health: 1000);
        var vs = MakeUnit(damage: 100, health: 101);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 100, HpIncrease = 0 }; // main damage => 100

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(3, res.ShotsToKill);     // ceil(101 / 50) = 3
        Assert.Equal(2, res.ShotsToKillNew);  // ceil(101 / 100) = 2
    }

    [Fact]
    public void Calculate_StacksExistingMainBuffs_WithNewDamageBuff()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 80, health: 600);

        var currentMainBuffs = new IBuff[]
        {
            new TestBuff { DamageIncrease = 20, HpIncrease = 10 }, // damage 120, hp 1100
        };
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 30, HpIncrease = 0 }; // total damage 150, hp stays 1100

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(5, res.ShotsToKill);     // ceil(600 / 120) = 5
        Assert.Equal(4, res.ShotsToKillNew);  // ceil(600 / 150) = 4
        Assert.Equal(14, res.ShotsToLive);    // ceil(1100 / 80) = 14
        Assert.Equal(14, res.ShotsToLiveNew); // HP increase from new buff = 0
    }

    [Fact]
    public void Calculate_NewBuffAffectsBothDamageAndHp_SymmetricEffects()
    {
        // Arrange
        var main = MakeUnit(damage: 90, health: 900);
        var vs = MakeUnit(damage: 90, health: 450);

        var currentMainBuffs = new IBuff[] { new TestBuff { DamageIncrease = 10, HpIncrease = 0 } }; // damage 99
        var currentVsBuffs = new IBuff[] { new TestBuff { DamageIncrease = 0, HpIncrease = 0 } };
        var newMainBuff = new TestBuff { DamageIncrease = 10, HpIncrease = 10 }; // damage 108.9, hp 990

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        // Shots to kill vs: ceil(450 / 99) = 5, with new damage ceil(450 / 108.9..) = 5 (still 5, boundary check)
        Assert.Equal(5, res.ShotsToKill);
        Assert.Equal(5, res.ShotsToKillNew);

        // Shots to live vs damage: 90; current main hp = 900 (no HP buffs), so ceil(900/90)=10
        // New hp = 990 => ceil(990/90) = 11
        Assert.Equal(10, res.ShotsToLive);
        Assert.Equal(11, res.ShotsToLiveNew);
    }

    [Fact]
    public void Calculate_VsBothDamageAndHpBuffs_InteractsWithNewMainBuff()
    {
        // Arrange
        var main = MakeUnit(damage: 120, health: 1000);
        var vs = MakeUnit(damage: 110, health: 550);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = new IBuff[]
        {
            new TestBuff { DamageIncrease = 20, HpIncrease = 25 } // vs damage 132, vs health 687.5
        };
        var newMainBuff = new TestBuff { DamageIncrease = 15, HpIncrease = 5 }; // main damage 138, main hp 1050

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(6, res.ShotsToKill);     // ceil(687.5 / 120) = ceil(5.729..) = 6
        Assert.Equal(5, res.ShotsToKillNew);  // ceil(687.5 / 138) = ceil(4.982..) = 5

        Assert.Equal(8, res.ShotsToLive);     // ceil(1000 / 132) = ceil(7.575..) = 8
        Assert.Equal(8, res.ShotsToLiveNew);  // ceil(1050 / 132) = ceil(7.954..) = 8
    }

    [Fact]
    public void Calculate_WhenNewBuffIsDuplicateInstance_UnionPreventsDoubleCounting()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 500);

        var duplicate = new TestBuff { DamageIncrease = 20, HpIncrease = 20 };

        // current already includes the exact same instance that is passed as newMainBuff
        var currentMainBuffs = new IBuff[] { duplicate };
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = duplicate;

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        // If duplicate were counted twice, damage would be 140 and hp 1200; but with Union it should remain 120 damage and 1200 hp.
        Assert.Equal(5, res.ShotsToKill);     // ceil(500 / 120) = 5
        Assert.Equal(5, res.ShotsToKillNew);  // unchanged due to Union
        Assert.Equal(12, res.ShotsToLive);    // ceil(1200 / 100) = 12
        Assert.Equal(12, res.ShotsToLiveNew); // unchanged due to Union
    }

    [Fact]
    public void Calculate_MainWithProjectilesPerShot_CountsTheWholeVolleyPerAttack()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000) with { ProjectilesPerShotOverride = 4 };
        var vs = MakeUnit(damage: 100, health: 1000);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 25, HpIncrease = 0 };

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(400, res.MainDamage);    // 100 * 4 projectiles
        Assert.Equal(500, res.MainDamageNew); // 400 * 1.25
        Assert.Equal(3, res.ShotsToKill);     // ceil(1000 / 400)
        Assert.Equal(2, res.ShotsToKillNew);  // ceil(1000 / 500)

        // The vs unit has no override, so the defensive side is untouched.
        Assert.Equal(100, res.VsDamage);
        Assert.Equal(10, res.ShotsToLive);    // ceil(1000 / 100)
    }

    [Fact]
    public void Calculate_VsWithProjectilesPerShot_KillsMainFaster()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 1000) with { ProjectilesPerShotOverride = 4 };

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 0, HpIncrease = 50 };

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(400, res.VsDamage);      // 100 * 4 projectiles
        Assert.Equal(3, res.ShotsToLive);     // ceil(1000 / 400)
        Assert.Equal(4, res.ShotsToLiveNew);  // ceil(1500 / 400)

        // The main unit has no override, so the offensive side is untouched.
        Assert.Equal(100, res.MainDamage);
        Assert.Equal(10, res.ShotsToKill);    // ceil(1000 / 100)
    }

    [Fact]
    public void Calculate_ProjectilesPerShot_StacksWithLevel()
    {
        // Damage already folds in Level, and the volley multiplies on top of that.
        var main = MakeUnit(damage: 100, health: 1000) with { Level = 3, ProjectilesPerShotOverride = 2 };
        var vs = MakeUnit(damage: 100, health: 1200);

        var res = BreakPointsCalculator.Calculate(
            main, vs, Array.Empty<IBuff>(), Array.Empty<IBuff>(), new TestBuff());

        Assert.Equal(600, res.MainDamage); // 100 * 3 levels * 2 projectiles
        Assert.Equal(2, res.ShotsToKill);  // ceil(1200 / 600)
    }

    [Fact]
    public void Calculate_MainWithSingleProjectileBreakpoints_IgnoresTheRestOfTheVolley()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000) with
        {
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true
        };
        var vs = MakeUnit(damage: 100, health: 1000);

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 25, HpIncrease = 0 };

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(100, res.MainDamage);     // one projectile, not 4
        Assert.Equal(125, res.MainDamageNew);  // 100 * 1.25
        Assert.Equal(10, res.ShotsToKill);     // ceil(1000 / 100)
        Assert.Equal(8, res.ShotsToKillNew);   // ceil(1000 / 125)
    }

    [Fact]
    public void Calculate_VsWithSingleProjectileBreakpoints_IgnoresTheRestOfTheVolley()
    {
        // Arrange
        var main = MakeUnit(damage: 100, health: 1000);
        var vs = MakeUnit(damage: 100, health: 1000) with
        {
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true
        };

        var currentMainBuffs = Array.Empty<IBuff>();
        var currentVsBuffs = Array.Empty<IBuff>();
        var newMainBuff = new TestBuff { DamageIncrease = 0, HpIncrease = 50 };

        // Act
        var res = BreakPointsCalculator.Calculate(main, vs, currentMainBuffs, currentVsBuffs, newMainBuff);

        // Assert
        Assert.Equal(100, res.VsDamage);      // one projectile, not 4
        Assert.Equal(10, res.ShotsToLive);    // ceil(1000 / 100)
        Assert.Equal(15, res.ShotsToLiveNew); // ceil(1500 / 100)
    }

    [Fact]
    public void Calculate_SingleProjectileBreakpoints_StacksWithLevel()
    {
        // The flag drops the volley multiplier only; Level still folds into Damage.
        var main = MakeUnit(damage: 100, health: 1000) with
        {
            Level = 3,
            ProjectilesPerShotOverride = 4,
            CountBreakpointsForSingleProjectile = true
        };
        var vs = MakeUnit(damage: 100, health: 1200);

        var res = BreakPointsCalculator.Calculate(
            main, vs, Array.Empty<IBuff>(), Array.Empty<IBuff>(), new TestBuff());

        Assert.Equal(300, res.MainDamage); // 100 * 3 levels, no volley
        Assert.Equal(4, res.ShotsToKill);  // ceil(1200 / 300)
    }

    [Fact]
    public void Calculate_AggregateOverload_MatchesBuffListOverload_ForEveryRealBuffPairing()
    {
        // The grid path folds the buff lists into BuffAggregates once and then reuses
        // them for every cell. That is only safe while it stays bit-identical to the
        // per-cell list overload.
        IBuff[] candidates =
        [
            new Attack1Buff(), new Attack2Buff(), new Hp1Buff(), new Hp2Buff(),
            new SmallAmpCoreBuff(), new ImpFireControlBuff(), new HasteModuleBuff(),
            new HeavyArmorBuff(), new AmpCoreBuff(),
        ];

        IBuff[][] sides =
        [
            [],
            [new Attack1Buff()],
            [new Attack1Buff(), new Attack2Buff(), new Hp1Buff()],
            [new CostControlBuff(), new FortifiedBuff(), new Hp1Buff(), new Hp2Buff()],
        ];

        foreach (var main in sides)
        foreach (var vs in sides)
        foreach (var candidate in candidates)
        foreach (var (damage, health) in new[] { (100, 1000), (2329, 1622), (79, 263) })
        {
            var mainUnit = MakeUnit(damage, health) with { Level = 3 };
            var vsUnit = MakeUnit(health, damage) with { Level = 7 };

            var viaList = BreakPointsCalculator.Calculate(mainUnit, vsUnit, main, vs, candidate);
            var viaAggregates = BreakPointsCalculator.Calculate(
                mainUnit,
                vsUnit,
                BuffAggregates.From(main),
                BuffAggregates.From(vs),
                candidate.DamageIncrease,
                candidate.HpIncrease);

            Assert.Equal(viaList, viaAggregates);
        }
    }
}
