using Melper.Data;

namespace Melper.Services;

public class BattleCalculator
{
    public static BattleCalcResults Calculate(Unit main, Unit vs, BattleCalcMode mode)
    {
        // Anti-air bought as a technology still counts: without it the six units
        // that rely on 防空弹药 would read as unable to engage air at all.
        if (vs.IsAir && !main.CanEverAttackAir)
        {
            return new BattleCalcResults
            {
                Fails = true
            };
        }

        decimal secondsToDestroy = 0;
        var totalShots = 0;
        var overkill = 0;

        var reloadSeconds = (decimal)main.ReloadTime.TotalSeconds;
        var projectilesPerShot = main.ProjectilesPerShotOverride ?? 1;
        var shotsPerSalvo = main.CountInPack * projectilesPerShot;

        if (mode == BattleCalcMode.Salvo)
        {
            for (int j = 0; j < vs.CountInPack; j++)
            {
                var curUnitHealth = vs.Health;

                while (true)
                {
                    totalShots += shotsPerSalvo;

                    curUnitHealth -= shotsPerSalvo * main.Damage;

                    if (curUnitHealth <= 0)
                    {
                        overkill += Math.Abs(curUnitHealth);
                        break;
                    }

                    secondsToDestroy += reloadSeconds;
                }

                // reload if it's not last unit
                if (j != vs.CountInPack - 1)
                {
                    secondsToDestroy += reloadSeconds;
                }
            }
        }

        if (mode == BattleCalcMode.Ideal)
        {
            var vsUnitLeft = vs.CountInPack;
            var vsUnitHealthLeft = vs.Health;

            while (true)
            {
                for (int i = 0; i < main.CountInPack; i++)
                {
                    vsUnitHealthLeft -= main.Damage * projectilesPerShot;
                    totalShots += projectilesPerShot;

                    if (vsUnitHealthLeft <= 0)
                    {
                        overkill += Math.Abs(vsUnitHealthLeft);
                        vsUnitLeft--;
                        vsUnitHealthLeft = vs.Health;
                        if (vsUnitLeft == 0)
                        {
                            break;
                        }
                    }
                }

                if (vsUnitLeft == 0)
                {
                    break;
                }

                secondsToDestroy += reloadSeconds;
            }
        }

        return new BattleCalcResults
        {
            Fails = false,
            TimeToDestroy = secondsToDestroy,
            TimeToDestroyWithReload = secondsToDestroy + reloadSeconds,
            TotalShots = totalShots,
            Overkill = overkill,
            OverkillPerUnit = overkill / vs.CountInPack,
            TotalSalvos = (double)totalShots / shotsPerSalvo,
            OverkillPercentage = overkill / ((double)totalShots * main.Damage) * 100d
        };
    }
}

public enum BattleCalcMode
{
    Ideal = 0, // units to not overkill themselves
    Salvo = 1, // all units shot a salvo
}

public class BattleCalcResults
{
    public bool Fails { get; set; }
    public decimal TimeToDestroy { get; set; }
    public decimal TimeToDestroyWithReload { get; set; }
    public double TotalSalvos { get; set; }
    public int TotalShots { get; set; }
    public double OverkillPercentage { get; set; }
    public int Overkill { get; set; }
    public int OverkillPerUnit { get; set; }
}