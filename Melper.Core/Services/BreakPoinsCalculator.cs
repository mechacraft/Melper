using Melper.Core.Buffs;
using Melper.Core.Extensions;
using Melper.Data;

namespace Melper.Core.Services;

/// <summary>
/// The only things <see cref="BreakPointsCalculator"/> needs from a side's buff list.
/// Folding a list down to these four numbers once, instead of re-running the LINQ on
/// every cell, is what makes a full grid affordable: the buffs never change while a
/// grid is being built, but the grid holds hundreds of thousands of cells.
/// </summary>
public readonly record struct BuffAggregates(double DamageMul, int DamageIncrease, double HpMul, int HpIncrease)
{
    public static readonly BuffAggregates None = new(1d, 0, 1d, 0);

    public static BuffAggregates From(IReadOnlyList<IBuff> buffs) => new(
        buffs.DamageMul(),
        buffs.Sum(x => x.DamageIncrease),
        buffs.HpMul(),
        buffs.Sum(x => x.HpIncrease));
}

/// <summary>
/// What one side owns, in the two shapes it comes in. Every spec but the air one reaches
/// the whole side, so the two halves are usually the same numbers; the air specialist
/// reaches only the air half, and a unit reads whichever half it belongs to through
/// <see cref="For"/>. Kept as a pair rather than folded per unit because the buffs never
/// change while a grid is being built and the grid holds hundreds of thousands of cells.
/// </summary>
public readonly record struct SideBuffs(BuffAggregates Ground, BuffAggregates Air)
{
    public static readonly SideBuffs None = new(BuffAggregates.None, BuffAggregates.None);

    /// <summary>The same numbers for every unit - what a side with no air specialist comes to.</summary>
    public static SideBuffs Flat(BuffAggregates all) => new(all, all);

    public BuffAggregates For(Unit unit) => unit.IsAir ? Air : Ground;
}

public class BreakPointsCalculator
{
    public static BreakPointsResults Calculate(
        Unit main,
        Unit vs,
        IReadOnlyList<IBuff> currentMainBuffs,
        IReadOnlyList<IBuff> currentVsBuffs,
        IBuff newMainBuff)
    {
        // Union, not concat: when newMainBuff is the very same instance as one already
        // held by the side, it must not be counted twice.
        var withNew = currentMainBuffs.Union([newMainBuff]).ToList();

        return Calculate(
            main,
            vs,
            BuffAggregates.From(currentMainBuffs),
            BuffAggregates.From(currentVsBuffs),
            withNew.Sum(x => x.DamageIncrease) - currentMainBuffs.Sum(x => x.DamageIncrease),
            withNew.Sum(x => x.HpIncrease) - currentMainBuffs.Sum(x => x.HpIncrease));
    }

    /// <summary>
    /// Allocation-free path used when a whole grid is built against one fixed pair of
    /// buff sets: pass the pre-folded <see cref="BuffAggregates"/> plus what the
    /// candidate buff adds on top.
    /// </summary>
    public static BreakPointsResults Calculate(
        Unit main,
        Unit vs,
        BuffAggregates mainBuffs,
        BuffAggregates vsBuffs,
        int newBuffDamageIncrease,
        int newBuffHpIncrease)
    {
        var res = new BreakPointsResults();
        {
            var damage = main.DamageForBreakpoints * mainBuffs.DamageMul * (100 + mainBuffs.DamageIncrease) / 100d;
            var damageNew = main.DamageForBreakpoints * mainBuffs.DamageMul * (100 + mainBuffs.DamageIncrease + newBuffDamageIncrease) / 100d;
            var health = vs.Health * vsBuffs.HpMul * (100 + vsBuffs.HpIncrease) / 100d;

            res.ShotsToKill = (int)Math.Ceiling(health / damage);
            res.ShotsToKillNew = (int)Math.Ceiling(health / damageNew);
            res.ShotsToKillWinPercents = (res.ShotsToKill - res.ShotsToKillNew) / (double)res.ShotsToKill * 100;
            res.MainDamage = (int)damage;
            res.MainDamageNew = (int)damageNew;
            res.VsHealth = (int)health;
        }
        {
            var damage = vs.DamageForBreakpoints * vsBuffs.DamageMul * (100 + vsBuffs.DamageIncrease) / 100d;
            var health = main.Health * mainBuffs.HpMul * (100 + mainBuffs.HpIncrease) / 100d;
            var healthNew = main.Health * mainBuffs.HpMul * (100 + mainBuffs.HpIncrease + newBuffHpIncrease) / 100d;
            res.VsDamage = (int)damage;
            res.MainHealth = (int)health;
            res.MainHealthNew = (int)healthNew;

            res.ShotsToLive = (int)Math.Ceiling(health / damage);
            res.ShotsToLiveNew = (int)Math.Ceiling(healthNew / damage);
            res.ShotsToLiveWinPercents = (res.ShotsToLiveNew - res.ShotsToLive) / (double)res.ShotsToLive * 100;
        }

        return res;
    }
}

public record BreakPointsResults
{
    public int ShotsToKill { get; set; }
    public int ShotsToKillNew { get; set; }
    public int ShotsToLive { get; set; }
    public int ShotsToLiveNew { get; set; }
    public double ShotsToKillWinPercents { get; set; }
    public double ShotsToLiveWinPercents { get; set; }
    public int MainDamage { get; set; }
    public int MainDamageNew { get; set; }
    public int MainHealth { get; set; }
    public int MainHealthNew { get; set; }
    public int VsHealth { get; set; }
    public int VsDamage { get; set; }
}