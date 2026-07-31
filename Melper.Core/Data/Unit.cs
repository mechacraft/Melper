using System.Text.Json.Serialization;

namespace Melper.Data;

public record Unit
{
    /// <summary>
    /// The unit id used by the game itself (MechData.id). Stable across patches,
    /// unlike the name, so it is what the extracted game data joins on.
    /// Left at 0 for ad-hoc units that do not come from the real roster.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Never serialized: the JSON roster holds base (level 1) stats, and <see cref="Damage"/>
    /// already folds the level into its getter — round-tripping it would square the multiplier.
    /// </summary>
    [JsonIgnore]
    public int Level { get; init; } = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string Name { get; init; }

    public int Cost { get; init; }

    /// <summary>
    /// What it costs to add the unit to the shop for the rest of the match
    /// (<c>CardData.unlockPrice</c>). Zero for the starters — the units that are on
    /// offer from the first round without paying anything for the privilege.
    /// </summary>
    public int UnlockCost { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
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
    [JsonIgnore]
    public int DamagePerAttack => Damage * (ProjectilesPerShotOverride ?? 1);

    /// <summary>
    /// Damage a breakpoint should be measured against. Units flagged with
    /// <see cref="CountBreakpointsForSingleProjectile"/> spread their volley over an
    /// area, so a single target eats one projectile rather than the whole salvo —
    /// counting the full volley would overstate what it takes to kill that target.
    /// </summary>
    [JsonIgnore]
    public int DamageForBreakpoints => CountBreakpointsForSingleProjectile ? Damage : DamagePerAttack;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
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
    [JsonIgnore]
    public bool CanEverAttackAir => CanAttackAir || CanAttackAirWithTech;

    public bool IsAir { get; init; }
    public bool IsGiant { get; init; }
    public bool IsTitan { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int Speed { get; set; }
    public decimal Splash { get; set; }
    public bool CalculateSalvoMode { get; init; }
}