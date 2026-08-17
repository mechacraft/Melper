namespace Melper.Cli;

/// <summary>Which half of the board a spec belongs to.</summary>
public enum SpecOwner
{
    None,
    Mine,
    Vs
}

/// <summary>
/// Everything the advise command was asked for, already checked. Built by
/// <see cref="Parse"/>, which never throws: a bad command line comes back as a
/// <see cref="Problems"/> list so the caller can print all of it at once.
/// </summary>
public sealed class AdviseOptions
{
    /// <summary>
    /// Units on my side of the board - the ones whose upgrades are being advised on.
    /// Empty means the whole roster, the same as leaving the web picker untouched.
    /// </summary>
    public List<string> Mine { get; } = [];

    /// <summary>Units to measure against. Empty means the whole roster.</summary>
    public List<string> Vs { get; } = [];

    /// <summary>Attack and hp ladders my side already owns, 0 to 2.</summary>
    public int Attack { get; private set; }

    public int Hp { get; private set; }

    /// <summary>The same for the opponent: what it already has, not what it might buy.</summary>
    public int VsAttack { get; private set; }

    public int VsHp { get; private set; }

    public SpecOwner Fortified { get; private set; }

    public SpecOwner CostControl { get; private set; }

    /// <summary>
    /// How many lines to read out. Fifteen is about as much as is worth hearing in one
    /// go; <see cref="All"/> lifts the cap.
    /// </summary>
    public int Top { get; private set; } = 15;

    /// <summary>
    /// Everything, uncapped and including the pairings that only bank the percentage the
    /// technology states. Off by default: that tail is long, and it is what buries the
    /// pairings that crossed something.
    /// </summary>
    public bool All { get; private set; }

    public bool Json { get; private set; }

    public bool Help { get; private set; }

    public List<string> Problems { get; } = [];

    public static AdviseOptions Parse(IReadOnlyList<string> args)
    {
        var options = new AdviseOptions();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            // The one flag that has to work even when the rest of the line is nonsense.
            if (arg is "--help" or "-h" or "-?" or "/?" or "help")
            {
                options.Help = true;
                continue;
            }

            switch (arg)
            {
                case "--all":
                    options.All = true;
                    continue;
                case "--json":
                    options.Json = true;
                    continue;
            }

            // Everything below takes a value, either "--flag value" or "--flag=value".
            var split = arg.IndexOf('=');
            var name = split < 0 ? arg : arg[..split];
            string? value = split < 0 ? null : arg[(split + 1)..];

            if (value is null)
            {
                if (i + 1 < args.Count && args[i + 1].StartsWith("--", StringComparison.Ordinal) == false)
                {
                    value = args[++i];
                }
                else
                {
                    options.Problems.Add($"{name} needs a value");
                    continue;
                }
            }

            switch (name)
            {
                // Units named without a side are on both: what is on the board is usually
                // the question, and both halves of a pairing come out of that same board.
                case "--units" or "-u":
                    options.Mine.AddRange(Names(value));
                    options.Vs.AddRange(Names(value));
                    break;
                case "--mine" or "-m":
                    options.Mine.AddRange(Names(value));
                    break;
                case "--vs" or "-v":
                    options.Vs.AddRange(Names(value));
                    break;

                case "--attack":
                    options.Attack = options.Level(name, value);
                    break;
                case "--hp":
                    options.Hp = options.Level(name, value);
                    break;
                case "--vs-attack":
                    options.VsAttack = options.Level(name, value);
                    break;
                case "--vs-hp":
                    options.VsHp = options.Level(name, value);
                    break;

                case "--fortified":
                    options.Fortified = options.Owner(name, value);
                    break;
                case "--cost-control":
                    options.CostControl = options.Owner(name, value);
                    break;

                case "--top" or "-n":
                    if (int.TryParse(value, out var top) && top > 0)
                    {
                        options.Top = top;
                    }
                    else
                    {
                        options.Problems.Add($"{name} takes a positive number, not \"{value}\"");
                    }

                    break;

                default:
                    options.Problems.Add($"unknown option \"{arg}\"");
                    break;
            }
        }

        // One spec to a side and one side to a spec, the rule the web page's controls have
        // built into their shape. Here it has to be checked, since both flags are free.
        if (options.Fortified != SpecOwner.None && options.Fortified == options.CostControl)
        {
            options.Problems.Add("--fortified and --cost-control cannot both be on the same side");
        }

        return options;
    }

    /// <summary>
    /// Names off one flag. Comma-separated, since unit names have spaces in them - and
    /// blanks dropped, so a trailing comma is not a name that resolves to nothing.
    /// </summary>
    private static IEnumerable<string> Names(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private int Level(string name, string value)
    {
        if (int.TryParse(value, out var level) && level is >= 0 and <= 2)
        {
            return level;
        }

        Problems.Add($"{name} takes 0, 1 or 2, not \"{value}\"");
        return 0;
    }

    private SpecOwner Owner(string name, string value) => value.ToLowerInvariant() switch
    {
        "mine" or "me" or "main" => SpecOwner.Mine,
        "vs" or "enemy" or "them" => SpecOwner.Vs,
        "none" or "no" => SpecOwner.None,
        _ => Reject(name, value)
    };

    private SpecOwner Reject(string name, string value)
    {
        Problems.Add($"{name} takes mine, vs or none, not \"{value}\"");
        return SpecOwner.None;
    }
}
