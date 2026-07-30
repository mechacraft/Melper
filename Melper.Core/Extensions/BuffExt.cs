using Melper.Core.Buffs;

namespace Melper.Core.Extensions;

public static class BuffExt
{
    public static int Damage(this IReadOnlyList<IBuff> buffs, int damage)
    {
        var res = (double)damage;

        foreach (var buff in buffs.Where(x => x.DamageMul != 1d))
        {
            res *= buff.DamageMul;
        }

        return (int)res * buffs.Sum(x => x.DamageIncrease);
    }

    public static double DamageMul(this IReadOnlyList<IBuff> buffs)
    {
        var res = 1d;

        foreach (var buff in buffs.Where(x => x.DamageMul != 1d))
        {
            res *= buff.DamageMul;
        }

        return res;
    }

    public static double HpMul(this IReadOnlyList<IBuff> buffs)
    {
        var res = 1d;

        foreach (var buff in buffs.Where(x => x.HpMul != 1d))
        {
            res *= buff.HpMul;
        }

        return res;
    }
}