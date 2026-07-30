namespace Melper.Data;

public record Unit
{
    /// <summary>
    /// The unit id used by the game itself (MechData.id). Stable across patches,
    /// unlike the name, so it is what the extracted game data joins on.
    /// Left at 0 for ad-hoc units that do not come from the real roster.
    /// </summary>
    public int Id { get; init; }

    public int Level { get; init; } = 1;
    public required string Name { get; init; }
    public int Cost { get; init; }
    public required int CountInPack { get; init; }

    private readonly int _damage;

    public int Damage
    {
        get => _damage * Level;
        init => _damage = value;
    }

    public int? HitsPerAttackOverride { get; set; }
    public int? ProjectilesPerShotOverride { get; init; }
    public bool CountBreakpointsForSingleProjectile { get; init; }

    /// <summary>
    /// Damage a single unit lands in one attack. Multi-projectile units (Stormcaller,
    /// Wraith, Overlord and friends) fire their whole volley per reload, so
    /// <see cref="Damage"/> alone understates an attack by the projectile count.
    /// </summary>
    public int DamagePerAttack => Damage * (ProjectilesPerShotOverride ?? 1);

    /// <summary>
    /// Damage a breakpoint should be measured against. Units flagged with
    /// <see cref="CountBreakpointsForSingleProjectile"/> spread their volley over an
    /// area, so a single target eats one projectile rather than the whole salvo —
    /// counting the full volley would overstate what it takes to kill that target.
    /// </summary>
    public int DamageForBreakpoints => CountBreakpointsForSingleProjectile ? Damage : DamagePerAttack;

    public required TimeSpan ReloadTime { get; init; }

    private readonly int _health;

    public int Health
    {
        get => _health * Level;
        init => _health = value;
    }

    public int Range { get; set; }
    /// <summary>
    /// Whether the unit can hit air targets with no upgrades at all.
    /// </summary>
    public bool CanAttackAir { get; init; }

    /// <summary>
    /// Whether a purchasable technology grants anti-air (防空弹药 and friends).
    /// Six units rely on this rather than on innate anti-air: Fortress, Arclight,
    /// Sandworm, Tarantula, Void eye and Mountain.
    /// </summary>
    public bool CanAttackAirWithTech { get; init; }

    /// <summary>
    /// True when the unit can hit air at all, innately or once upgraded.
    /// </summary>
    public bool CanEverAttackAir => CanAttackAir || CanAttackAirWithTech;

    public bool IsAir { get; init; }
    public bool IsGiant { get; init; }
    public bool IsTitan { get; init; }
    public required int Speed { get; set; }
    public decimal Splash { get; set; }
    public bool CalculateSalvoMode { get; init; }
}