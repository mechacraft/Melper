using Melper.Data;
using Melper.Services;

namespace Melper.Tests;

public class UnitTest1
{
    [Fact]
    public void BattleCalculator_1()
    {
        var craw = UnitsCollection.Units.First(x => x.Name.Contains("craw", StringComparison.OrdinalIgnoreCase));
        var arc = UnitsCollection.Units.First(x => x.Name.Contains("arcl", StringComparison.OrdinalIgnoreCase));
        var res = BattleCalculator.Calculate(craw, arc, BattleCalcMode.Salvo);
        var reloadTimeTotalSeconds = (decimal)craw.ReloadTime.TotalSeconds;

        Assert.Equal(3, res.TotalSalvos);
        Assert.Equal(reloadTimeTotalSeconds * 2, res.TimeToDestroy);
        Assert.Equal(reloadTimeTotalSeconds * 3, res.TimeToDestroyWithReload);
    }

    [Fact]
    public void BattleCalculator_2()
    {
        var phoe = UnitsCollection.Units.First(x => x.Name.Contains("phoenix", StringComparison.OrdinalIgnoreCase));
        var mark = UnitsCollection.Units.First(x => x.Name.Contains("marks", StringComparison.OrdinalIgnoreCase));
        var res = BattleCalculator.Calculate(phoe, mark, BattleCalcMode.Ideal);
        var reloadTimeTotalSeconds = (decimal)phoe.ReloadTime.TotalSeconds;

        Assert.Equal(.5, res.TotalSalvos);
        Assert.Equal(0, res.TimeToDestroy);
        Assert.Equal(reloadTimeTotalSeconds, res.TimeToDestroyWithReload);
    }

    [Fact]
    public void BattleCalculator_3()
    {
        var pha = UnitsCollection.Units.First(x => x.Name.Contains("phantom", StringComparison.OrdinalIgnoreCase));
        var mark = UnitsCollection.Units.First(x => x.Name.Contains("raide", StringComparison.OrdinalIgnoreCase));
        var res = BattleCalculator.Calculate(pha, mark, BattleCalcMode.Salvo);
        var reloadTimeTotalSeconds = (decimal)pha.ReloadTime.TotalSeconds;

        Assert.Equal(3, res.TotalSalvos);
        Assert.Equal(reloadTimeTotalSeconds * 2, res.TimeToDestroy);
        Assert.Equal(reloadTimeTotalSeconds * 3, res.TimeToDestroyWithReload);
    }

    [Fact]
    public void BattleCalculator_4()
    {
        var pha = UnitsCollection.Units.First(x => x.Name.Contains("phantom", StringComparison.OrdinalIgnoreCase));
        var sle = UnitsCollection.Units.First(x => x.Name.Contains("sledge", StringComparison.OrdinalIgnoreCase));
        var res = BattleCalculator.Calculate(pha, sle, BattleCalcMode.Salvo);
        var reloadTimeTotalSeconds = (decimal)pha.ReloadTime.TotalSeconds;

        Assert.Equal(5, res.TotalSalvos);
        Assert.Equal(reloadTimeTotalSeconds * 4, res.TimeToDestroy);
        Assert.Equal(reloadTimeTotalSeconds * 5, res.TimeToDestroyWithReload);
    }
}