namespace Melper.Core.Buffs;

/// <summary>
/// The air specialist. Unlike the other specs it reaches only half the roster: an air
/// unit gets both halves of it, a ground unit gets nothing, so a side running it is two
/// sets of aggregates rather than one - see <see cref="Melper.Core.Services.SideBuffs"/>.
/// </summary>
public class AirSpecBuff : IBuff
{
    public int DamageIncrease => 13;
    public int HpIncrease => 13;
}
