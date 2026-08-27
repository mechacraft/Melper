namespace Melper.ViewModel;

public class BreakPointSideConfig
{
    public bool Attack1 { get; set; }
    public bool Attack2 { get; set; }
    public bool Hp1 { get; set; }
    public bool Hp2 { get; set; }
    public bool HasteModule { get; set; }
    public bool HeavyArmor { get; set; }
    public bool CostControl { get; set; }
    public bool Fortified { get; set; }

    /// <summary>The air specialist: it reaches the side's air units and nothing else.</summary>
    public bool AirSpec { get; set; }
    public int Lvls { get; set; } = 2;
}