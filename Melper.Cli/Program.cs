using System.Text.Json;
using System.Text.Json.Serialization;
using Melper.Cli;
using Melper.Core.Services;
using Melper.Data;

// The advise command is the only one there is, so naming it is optional: an agent that
// types "melper advise ..." and one that types "melper --units ..." both work.
var arguments = args.Length > 0 && args[0].Equals("advise", StringComparison.OrdinalIgnoreCase)
    ? args[1..]
    : args;

var options = AdviseOptions.Parse(arguments);

if (options.Help || arguments.Length == 0)
{
    Console.WriteLine(Usage);
    return 0;
}

if (options.Problems.Count > 0)
{
    return Fail(options.Problems);
}

var roster = UnitsCollection.Units;

// Both sides are resolved before either is complained about, so a line with a typo in
// each half is one run rather than two.
var mine = UnitNameResolver.ResolveAll(options.Mine, roster);
var vs = UnitNameResolver.ResolveAll(options.Vs, roster);

var problems = mine.Concat(vs).Select(x => x.Problem).OfType<string>().Distinct().ToList();
if (problems.Count > 0)
{
    return Fail(problems);
}

// No names for a side is the whole roster, which is what the web picker's empty pattern
// means too - not an empty board.
var mineUnits = Units(mine, roster);
var vsUnits = Units(vs, roster);

var candidates = BreakpointAdvisor.AttackAndHpCandidates(
    options.Attack >= 1, options.Attack >= 2, options.Hp >= 1, options.Hp >= 2);

var suggestions = BreakpointAdvisor.Suggest(
    mineUnits,
    vsUnits,
    BreakpointAdvisor.Aggregates(
        options.Attack, options.Hp,
        options.CostControl == SpecOwner.Mine, options.Fortified == SpecOwner.Mine),
    BreakpointAdvisor.Aggregates(
        options.VsAttack, options.VsHp,
        options.CostControl == SpecOwner.Vs, options.Fortified == SpecOwner.Vs),
    candidates);

// The tail that only banks the technology's own percentage is dropped rather than ranked
// last: it is long enough to bury everything above it when read out loud.
var worth = options.All
    ? suggestions
    : suggestions.Where(x => x.Tier >= SuggestionTier.Noticeable).ToList();

var shown = options.All ? worth : worth.Take(options.Top).ToList();

Console.WriteLine(options.Json ? Json() : Text());
return 0;

string Text()
{
    var lines = new List<string>
    {
        $"Roster of {UnitsCollection.Date:yyyy-MM-dd}, {roster.Count} units, level 1 stats.",
        $"Mine ({Owned(options.Attack, options.Hp, SpecOwner.Mine)}): {Named(mineUnits, mine)}",
        $"Vs   ({Owned(options.VsAttack, options.VsHp, SpecOwner.Vs)}): {Named(vsUnits, vs)}"
    };

    if (candidates.Count == 0)
    {
        lines.Add("");
        lines.Add("Every attack and hp technology is already owned - nothing left to advise on.");
        return string.Join(Environment.NewLine, lines);
    }

    lines.Add("On offer: " + string.Join(", ",
        candidates.Select(x => $"{x.Name} +{x.Increase}% {(x.Kind == UpgradeKind.Attack ? "damage" : "hp")}")));
    lines.Add("");

    if (shown.Count == 0)
    {
        lines.Add(suggestions.Count == 0
            ? "No breakpoints at all: every pairing needs the same number of attacks either way."
            : $"No breakpoint worth naming - all {suggestions.Count} moves only bank the "
              + "percentage the technology states. Pass --all to see them anyway.");

        return string.Join(Environment.NewLine, lines);
    }

    lines.Add(shown.Count < worth.Count
        ? $"Top {shown.Count} of {worth.Count} breakpoints, best first:"
        : $"{shown.Count} breakpoint{(shown.Count == 1 ? "" : "s")}, best first:");

    // Widths taken from what is actually being printed: a run where every buy is Attack1
    // should not be padded out to the width of a name it never mentions.
    var upgrade = shown.Max(x => x.Upgrade.Name.Length);
    var tier = shown.Max(x => x.Tier.ToString().Length);
    var sentence = shown.Max(x => Sentence(x).Length);

    for (var i = 0; i < shown.Count; i++)
    {
        var item = shown[i];

        lines.Add(
            $"{i + 1,3}. {item.Tier.ToString().ToLowerInvariant().PadRight(tier)}"
            + $"  {item.Upgrade.Name.PadRight(upgrade)}"
            + $"  {Sentence(item).PadRight(sentence)}"
            + $"  (x{item.Ratio:0.##} from a x{item.Flat:0.##} buy)");
    }

    return string.Join(Environment.NewLine, lines);
}

// One pairing said the way it would be read out. Which side of the pairing is doing what
// depends on the kind: an attack buy shortens the kill my unit is making, an hp buy
// lengthens what it lives through.
static string Sentence(BreakpointSuggestion item) => item.Upgrade.Kind == UpgradeKind.Attack
    ? $"{item.Main.Name} kills {item.Vs.Name} in {Attacks(item.After)} instead of {item.Before}"
    : $"{item.Main.Name} survives {Attacks(item.After)} from {item.Vs.Name} instead of {item.Before}";

static string Attacks(int count) => $"{count} attack{(count == 1 ? "" : "s")}";

string Json() => JsonSerializer.Serialize(
    new
    {
        Roster = new { Date = UnitsCollection.Date.ToString("yyyy-MM-dd"), Units = roster.Count },
        Mine = new
        {
            Units = mineUnits.Select(x => x.Name),
            Attack = options.Attack,
            Hp = options.Hp,
            Fortified = options.Fortified == SpecOwner.Mine,
            CostControl = options.CostControl == SpecOwner.Mine
        },
        Vs = new
        {
            Units = vsUnits.Select(x => x.Name),
            Attack = options.VsAttack,
            Hp = options.VsHp,
            Fortified = options.Fortified == SpecOwner.Vs,
            CostControl = options.CostControl == SpecOwner.Vs
        },
        Candidates = candidates.Select(x => new { x.Name, Kind = x.Kind.ToString(), x.Increase }),
        Total = suggestions.Count,
        Worth = worth.Count,
        Breakpoints = shown.Select((x, i) => new
        {
            Rank = i + 1,
            Upgrade = x.Upgrade.Name,
            Kind = x.Upgrade.Kind.ToString(),
            Main = x.Main.Name,
            Vs = x.Vs.Name,
            x.Before,
            x.After,
            Ratio = Math.Round(x.Ratio, 3),
            Buy = Math.Round(x.Flat, 3),
            Score = Math.Round(x.Score, 3),
            Tier = x.Tier.ToString(),
            Says = Sentence(x)
        })
    },
    new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    });

// The units behind a side's lookups, or the whole roster when the side was not named -
// every lookup here has already been checked, so the matches are all present.
static IReadOnlyCollection<Unit> Units(IReadOnlyList<UnitNameLookup> lookups, IReadOnlyCollection<Unit> all) =>
    lookups.Count == 0 ? all : lookups.Select(x => x.Match!).ToList();

// Says what a side has, since the numbers below mean nothing without it.
string Owned(int attack, int hp, SpecOwner side)
{
    var has = new List<string>();

    if (attack > 0) has.Add($"attack lvl {attack}");
    if (hp > 0) has.Add($"hp lvl {hp}");
    if (options.Fortified == side) has.Add("fortified");
    if (options.CostControl == side) has.Add("cost control");

    return has.Count == 0 ? "no upgrades" : string.Join(", ", has);
}

// What a side came to. A side nobody named says so rather than reciting 32 names; a side
// that was named is read back in full however long it is, since that echo is the whole
// point of the header - a wrong guess at whose units are whose should be one line to
// correct, and a count would hide exactly that.
static string Named(IReadOnlyCollection<Unit> units, IReadOnlyList<UnitNameLookup> asked) =>
    asked.Count == 0
        ? $"whole roster, {units.Count} units"
        : string.Join(", ", units.Select(x => x.Name));

static int Fail(IEnumerable<string> problems)
{
    foreach (var problem in problems)
    {
        Console.Error.WriteLine($"error: {problem}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage);
    return 1;
}

internal partial class Program
{
    /// <summary>Printed on --help, on a bad command line, and when nothing at all was asked for.</summary>
    private const string Usage = """
        melper advise - which attack or hp technology crosses a breakpoint, best first.

        Usage:
          melper advise --units "Crawler, Fang, Sabertooth, Arclight, Tarantula"
          melper advise --mine "Crawler, Fang" --vs "Arclight" --attack 1

        Units (names are matched loosely: case, spacing and an English plural are ignored):
          -u, --units <names>   units on both sides of the board, comma separated
          -m, --mine <names>    only the side whose upgrades are being advised on
          -v, --vs <names>      only the side it is measured against
                                a side with no names given is the whole roster

        What each side already owns:
          --attack <0|1|2>      my attack technology ladder            (default 0)
          --hp <0|1|2>          my hp technology ladder                (default 0)
          --vs-attack <0|1|2>   the opponent's, same ladder            (default 0)
          --vs-hp <0|1|2>
          --fortified <mine|vs|none>      who is running the spec      (default none)
          --cost-control <mine|vs|none>   one spec to a side, and one side to a spec

        Output:
          -n, --top <count>     how many lines to print                (default 15)
              --all             every pairing, uncapped, including the ones that only
                                bank the percentage the technology states
              --json            the same list as JSON

        Only the next buy on each ladder is advised on: a tier-2 technology cannot be
        taken without its tier-1 prerequisite. Stats are level 1.
        """;
}
