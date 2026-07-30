using System.Diagnostics.CodeAnalysis;
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

    [SetsRequiredMembers]
    public UnitViewModel(Unit unit)
    {
        Name = unit.Name;
        Cost = unit.Cost;
        CountInPack = unit.CountInPack;
        Damage = unit.Damage;
        ProjectilesPerShotOverride = unit.ProjectilesPerShotOverride;
        ReloadTime = unit.ReloadTime;
        Health = unit.Health;
        Range = unit.Range;
        CanAttackAir = unit.CanAttackAir;
        IsAir = unit.IsAir;
        IsGiant = unit.IsGiant;
        IsTitan = unit.IsTitan;
        Speed = unit.Speed;
        Splash = unit.Splash;

        TotalHealth = (int)GetTotalHealth(unit);
        DpsPerUnit = GetDpsPerUnit(unit);
        TotalDps = GetTotalDps(unit);
        DpsPerCost = GetDpsRel1KPer100Cost(unit);
        HealthPerCost = GetHealthRel1KPer100Cost(unit);
    }

    private static double GetDpsPerUnit(Unit unit)
    {
        return unit.Damage * (unit.ProjectilesPerShotOverride ?? 1) / unit.ReloadTime.TotalSeconds;
    }

    private static double GetTotalDps(Unit unit)
    {
        return GetDpsPerUnit(unit) * unit.CountInPack;
    }

    private static double GetDpsRel1KPer100Cost(Unit unit)
    {
        return GetTotalDps(unit) / 1000d / unit.Cost * 100d;
    }

    private static double GetTotalHealth(Unit unit)
    {
        return (double)unit.Health * unit.CountInPack;
    }

    private static double GetHealthRel1KPer100Cost(Unit unit)
    {
        return unit.Cost > 0 ? (GetTotalHealth(unit) / 1000d) / unit.Cost * 100d : 0;
    }
}