using Melper.Core.Buffs;
using Melper.Data;

namespace Melper.Core.Services;

public enum UpgradeKind
{
    /// <summary>Moves how many attacks the main unit needs to kill the opponent.</summary>
    Attack,

    /// <summary>Moves how many of the opponent's attacks the main unit survives.</summary>
    Hp
}

/// <summary>
/// One buy the main side could make, as a single percentage step: whatever
/// <paramref name="Increase"/> the caller says that one step is worth.
/// </summary>
public sealed record UpgradeCandidate(string Name, UpgradeKind Kind, int Increase);

/// <summary>
/// How much of a pairing's move is worth reporting at all. The order is the order of
/// interest: a caller showing only what matters takes everything down to
/// <see cref="Noticeable"/>.
/// </summary>
public enum SuggestionTier
{
    /// <summary>
    /// The move is the technology's percentage and nothing else - the count shifted,
    /// no threshold was crossed. 556 attacks into 496 off a 12% buy is this, and a long
    /// tail of them is what buries the pairings that did cross something.
    /// </summary>
    Marginal,

    /// <summary>Something was crossed: the move is worth more than the stats alone paid for.</summary>
    Noticeable,

    /// <summary>The pairing is decided rather than shortened - the count halves or better.</summary>
    Decisive
}

/// <summary>A breakpoint one <see cref="UpgradeCandidate"/> crosses against one opponent.</summary>
/// <param name="Flat">
/// What the buy multiplies the stat by from where the side stands now. Not the technology's
/// own percentage: increases add up before they divide, so Attack2 bought on top of an owned
/// Attack1 is 136/112 - a 21% step, not the 24% written on it.
/// </param>
public sealed record BreakpointSuggestion(
    Unit Main, Unit Vs, UpgradeCandidate Upgrade, int Before, int After, double Flat)
{
    /// <summary>
    /// How much better the pairing gets, as a multiplier. Attack counts fall and hp counts
    /// rise, so each is divided the way round that leaves a number above 1 - otherwise the
    /// two kinds would not be comparable and hp advice would always outrank attack advice
    /// (one attack of movement is 3->2 = -33% but 2->3 = +50%).
    /// </summary>
    public double Ratio => Upgrade.Kind == UpgradeKind.Attack
        ? Before / (double)After
        : After / (double)Before;

    /// <summary>
    /// What ranks one breakpoint above another: the <see cref="Ratio"/> measured against the
    /// <see cref="Flat"/> gain that bought it. A pairing that takes 265 attacks and then 385
    /// has gained exactly the 45% the technology states - the count moved, but nothing was
    /// crossed. Going 3->2 on a 12% attack technology turns 12% into 50%, and that surplus
    /// is the whole point of a breakpoint, so it is what the list is ordered by.
    /// </summary>
    public double Score => Ratio / Flat;

    /// <summary>
    /// The low end of the pair. Used to break ties: at the same score, 2->1 decides a
    /// fight where 8->4 only shortens one.
    /// </summary>
    public int SmallEnd => Upgrade.Kind == UpgradeKind.Attack ? After : Before;

    /// <summary>
    /// Where a pairing stops being a shortening and starts being a decision - 3 attacks
    /// into 2, or 2 into 3 survived.
    /// </summary>
    public const double DecisiveRatio = 1.5;

    /// <summary>
    /// How much of the move has to be more than the technology's percentage already paid
    /// for before the pairing is worth reading at all.
    /// </summary>
    public const double NoticeableSurplus = 1.02;

    /// <summary>
    /// How far the pairing moves, said in the three steps every surface reports in: the
    /// web page colours its chips by this, and the console tool drops
    /// <see cref="SuggestionTier.Marginal"/> from what it reads out.
    /// </summary>
    public SuggestionTier Tier => this switch
    {
        _ when Ratio >= DecisiveRatio => SuggestionTier.Decisive,
        _ when Score >= NoticeableSurplus => SuggestionTier.Noticeable,
        _ => SuggestionTier.Marginal
    };
}

/// <summary>
/// Picks out the pairings where an attack or hp technology actually moves the attack
/// count, and ranks them by how much it moves.
/// </summary>
public static class BreakpointAdvisor
{
    /// <summary>
    /// The next attack and hp technology the main side could buy. A tier-2 technology
    /// cannot be taken without its tier-1 prerequisite, so it is only offered once that
    /// prerequisite is owned: advising on the pair as one purchase names a buy the side
    /// cannot make, and the tier-1 half of it is already on the list on its own.
    /// </summary>
    public static IReadOnlyList<UpgradeCandidate> AttackAndHpCandidates(
        bool attack1, bool attack2, bool hp1, bool hp2)
    {
        var candidates = new List<UpgradeCandidate>();

        if (attack1 == false)
        {
            candidates.Add(new UpgradeCandidate("Attack1", UpgradeKind.Attack, new Attack1Buff().DamageIncrease));
        }
        else if (attack2 == false)
        {
            candidates.Add(new UpgradeCandidate("Attack2", UpgradeKind.Attack, new Attack2Buff().DamageIncrease));
        }

        if (hp1 == false)
        {
            candidates.Add(new UpgradeCandidate("Hp1", UpgradeKind.Hp, new Hp1Buff().HpIncrease));
        }
        else if (hp2 == false)
        {
            candidates.Add(new UpgradeCandidate("Hp2", UpgradeKind.Hp, new Hp2Buff().HpIncrease));
        }

        return candidates;
    }

    /// <summary>
    /// What one side's technologies come to, from the ladder level of each track plus the
    /// two specs. Only the things the advice is about: the rest of the game's technologies
    /// would move the numbers with nothing said about them anywhere the advice is read.
    /// A spec is not something left to buy, so it never becomes a candidate - it only moves
    /// the stats the candidates are measured against, and Fortified's hp makes the next hp
    /// technology worth proportionally less, so it moves the order of the list as well as
    /// the counts. Cost control moves the counts alone: it is a multiplier, and a multiplier
    /// sits outside the increase the scoring divides by, so it cancels out of what a buy
    /// is worth.
    /// </summary>
    public static BuffAggregates Aggregates(int attackLevel, int hpLevel, bool costControl, bool fortified)
    {
        var buffs = new List<IBuff>();

        if (attackLevel >= 1) buffs.Add(new Attack1Buff());
        if (attackLevel >= 2) buffs.Add(new Attack2Buff());
        if (hpLevel >= 1) buffs.Add(new Hp1Buff());
        if (hpLevel >= 2) buffs.Add(new Hp2Buff());
        if (costControl) buffs.Add(new CostControlBuff());
        if (fortified) buffs.Add(new FortifiedBuff());

        return BuffAggregates.From(buffs);
    }

    /// <summary>
    /// Every pairing where one of <paramref name="candidates"/> crosses a breakpoint,
    /// best first. Pairings the technology cannot help - the opponent is out of reach,
    /// or the count does not move - are left out entirely rather than listed as zeroes.
    /// </summary>
    public static List<BreakpointSuggestion> Suggest(
        IEnumerable<Unit> mains,
        IReadOnlyCollection<Unit> opponents,
        BuffAggregates mainBuffs,
        BuffAggregates vsBuffs,
        IReadOnlyList<UpgradeCandidate> candidates)
    {
        var found = new List<BreakpointSuggestion>();

        foreach (var main in mains)
        {
            foreach (var vs in opponents)
            {
                // The two kinds have their own reach test, and they are not the same one:
                // a ground unit cannot shoot a Wasp, but the Wasp still shoots it, so its
                // hp advice stands even though its attack advice is meaningless. The same
                // asymmetry holds for a unit flagged out of the damage calculations: the
                // side whose damage the kind is worked out from is the side it gates, so
                // such a unit still gets hp advice about surviving what shoots at it, and
                // still shows up as an opponent worth shortening the kill on.
                var attackApplies = main.SkipDamageCalculations == false
                                    && main.DamageForBreakpoints > 0
                                    && vs.Health > 0
                                    && (main.CanAttackAir || vs.IsAir == false);

                var hpApplies = vs.SkipDamageCalculations == false
                                && vs.DamageForBreakpoints > 0
                                && main.Health > 0
                                && (main.IsAir == false || vs.CanEverAttackAir);

                // The best count each kind has reached for this pairing, 0 until one is kept.
                var bestKill = 0;
                var bestLive = 0;

                foreach (var candidate in candidates)
                {
                    var isAttack = candidate.Kind == UpgradeKind.Attack;
                    if (isAttack ? attackApplies == false : hpApplies == false)
                    {
                        continue;
                    }

                    var calc = BreakPointsCalculator.Calculate(
                        main, vs, mainBuffs, vsBuffs,
                        isAttack ? candidate.Increase : 0,
                        isAttack ? 0 : candidate.Increase);

                    var before = isAttack ? calc.ShotsToKill : calc.ShotsToLive;
                    var after = isAttack ? calc.ShotsToKillNew : calc.ShotsToLiveNew;

                    // Cheaper tiers are offered first, so a dearer one is only worth naming
                    // when it moves the count past what is already on the list for this pair.
                    var toBeat = isAttack
                        ? (bestKill == 0 ? before : bestKill)
                        : (bestLive == 0 ? before : bestLive);

                    if (isAttack ? after >= toBeat : after <= toBeat)
                    {
                        continue;
                    }

                    if (isAttack)
                    {
                        bestKill = after;
                    }
                    else
                    {
                        bestLive = after;
                    }

                    var owned = isAttack ? mainBuffs.DamageIncrease : mainBuffs.HpIncrease;
                    var flat = (100d + owned + candidate.Increase) / (100d + owned);

                    found.Add(new BreakpointSuggestion(main, vs, candidate, before, after, flat));
                }
            }
        }

        return found
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.SmallEnd)
            .ThenBy(x => x.Upgrade.Increase)
            .ThenBy(x => x.Main.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Vs.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
