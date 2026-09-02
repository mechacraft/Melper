using System.Diagnostics.CodeAnalysis;
using Melper.Core.Services;
using Melper.Data;

namespace Melper.ViewModel;

public class UnitViewModel
{
    public required string Name { get; set; }
    public int Cost { get; set; }
    public required int CountInPack { get; set; }
    public required double Damage { get; set; }
    public int? ProjectilesPerShotOverride { get; set; }
    public required TimeSpan ReloadTime { get; set; }
    public required long Health { get; set; }
    public int Range { get; set; }
    public bool CanAttackAir { get; set; }
    public bool IsAir { get; set; }
    public bool IsGiant { get; set; }
    public bool IsTitan { get; set; }
    public required int Speed { get; set; }
    public decimal Splash { get; set; }

    public int TotalHealth { get; set; }
    public double DpsPerUnit { get; }
    public double TotalDps { get; }
    public double DpsPerCost { get; }
    public double HealthPerCost { get; }

    /// <param name="buffs">
    /// What the side owns, folded down the same way the Breakpoints page folds it, so the
    /// two pages state one unit's damage and health identically. Nothing is owned by
    /// default, which is the roster's own numbers.
    /// </param>
    [SetsRequiredMembers]
    public UnitViewModel(Unit unit, BuffAggregates? buffs = null)
    {
        var owned = buffs ?? BuffAggregates.None;

        Name = unit.Name;
        Cost = unit.Cost;
        CountInPack = unit.CountInPack;
        Damage = Buffed(unit.Damage, owned.DamageMul, owned.DamageIncrease);
        ProjectilesPerShotOverride = unit.ProjectilesPerShotOverride;
        ReloadTime = unit.ReloadTime;
        Health = (long)Buffed(unit.Health, owned.HpMul, owned.HpIncrease);
        Range = unit.Range;
        CanAttackAir = unit.CanAttackAir;
        IsAir = unit.IsAir;
        IsGiant = unit.IsGiant;
        IsTitan = unit.IsTitan;
        Speed = unit.Speed;
        Splash = unit.Splash;

        // Everything below is derived from the buffed damage and health above, so a
        // technology moves the efficiency columns the same way it moves the raw ones.
        TotalHealth = (int)GetTotalHealth();
        DpsPerUnit = GetDpsPerUnit();
        TotalDps = GetTotalDps();
        DpsPerCost = GetDpsPer100Cost();
        HealthPerCost = GetHealthPer100Cost();
    }

    /// <summary>
    /// One stat under what the side owns, in the shape <see cref="BreakPointsCalculator"/>
    /// uses: the multipliers multiply and the percentages add.
    /// </summary>
    private static double Buffed(double stat, double mul, int increase) =>
        stat * mul * (100 + increase) / 100d;

    private double GetDpsPerUnit()
    {
        // The Data page can put a zero in either divisor, and an Infinity would poison
        // every derived column and sort the row straight to the top.
        return ReloadTime.TotalSeconds > 0
            ? Damage * (ProjectilesPerShotOverride ?? 1) / ReloadTime.TotalSeconds
            : 0;
    }

    private double GetTotalDps()
    {
        return GetDpsPerUnit() * CountInPack;
    }

    /// <summary>How much DPS the pack buys for 100 supplies.</summary>
    private double GetDpsPer100Cost()
    {
        return Cost > 0 ? GetTotalDps() / Cost * 100d : 0;
    }

    private double GetTotalHealth()
    {
        return (double)Health * CountInPack;
    }

    /// <summary>How much health the pack buys for 100 supplies.</summary>
    private double GetHealthPer100Cost()
    {
        return Cost > 0 ? GetTotalHealth() / Cost * 100d : 0;
    }
}