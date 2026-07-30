namespace Melper.Core.Buffs;

public interface IBuff
{
    int DamageIncrease => 0;
    int HpIncrease => 0;
    double DamageMul => 1;
    double HpMul => 1;
    string Name => GetType().Name.Replace("Buff", "");
}
