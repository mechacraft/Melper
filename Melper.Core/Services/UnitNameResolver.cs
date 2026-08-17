using System.Text;
using Melper.Data;

namespace Melper.Core.Services;

/// <summary>What one spoken name turned out to mean.</summary>
/// <param name="Query">The name exactly as it was asked for, so an error can quote it back.</param>
/// <param name="Match">The single unit it means, or null when nothing or too much matched.</param>
/// <param name="Candidates">
/// What it could have meant. Empty when the name reaches nothing at all; two or more when
/// it is ambiguous. Never one - a lone candidate is a <see cref="Match"/>.
/// </param>
public sealed record UnitNameLookup(string Query, Unit? Match, IReadOnlyList<Unit> Candidates)
{
    public bool IsAmbiguous => Match is null && Candidates.Count > 1;

    /// <summary>Why the name did not resolve, in a form fit to print. Null when it did.</summary>
    public string? Problem => Match is not null
        ? null
        : IsAmbiguous
            ? $"\"{Query}\" is ambiguous - {string.Join(", ", Candidates.Select(x => x.Name))}"
            : $"\"{Query}\" is not a unit";
}

/// <summary>
/// Turns unit names as a person says them into units off the roster. Unlike
/// <see cref="UnitFilterBuilder"/>, which builds one pattern out of a selection already
/// made in a picker, this takes names typed or dictated one at a time and insists on a
/// single unit for each: a pattern that reaches nothing quietly passes the whole roster,
/// which is exactly the wrong answer to give a caller that named five units.
/// </summary>
public static class UnitNameResolver
{
    /// <summary>
    /// The unit <paramref name="query"/> names. Matching ignores case, spacing and
    /// punctuation ("steel ball", "SteelBall" and "Steel-Ball" are one name), tries an
    /// exact name first, then a prefix, then anything containing it, and finally the
    /// same three again with an English plural taken off. Each step is only accepted
    /// when it lands on exactly one unit, so "wa" comes back ambiguous - Wasp and War
    /// Factory - rather than silently picking whichever the roster lists first.
    /// </summary>
    public static UnitNameLookup Resolve(string query, IReadOnlyCollection<Unit> roster)
    {
        var wanted = Normalize(query);

        if (wanted.Length == 0)
        {
            return new UnitNameLookup(query, null, []);
        }

        // Longest first so the singular is only tried once the name as given has failed
        // outright: "Fangs" is a plural of Fang, but a roster that later gains a unit
        // actually called Fangs should still match it as itself.
        foreach (var form in Forms(wanted))
        {
            foreach (var rule in Rules)
            {
                var hits = roster.Where(x => rule(Normalize(x.Name), form)).ToList();

                if (hits.Count == 1)
                {
                    return new UnitNameLookup(query, hits[0], hits);
                }

                if (hits.Count > 1)
                {
                    return new UnitNameLookup(query, null, hits);
                }
            }
        }

        return new UnitNameLookup(query, null, []);
    }

    /// <summary>
    /// Every name in <paramref name="queries"/>, in the order asked for and with
    /// duplicates dropped. Nothing is thrown: the lookups come back whole so a caller
    /// can report all the bad names at once rather than one per run.
    /// </summary>
    public static IReadOnlyList<UnitNameLookup> ResolveAll(
        IEnumerable<string> queries, IReadOnlyCollection<Unit> roster)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<UnitNameLookup>();

        foreach (var query in queries)
        {
            var lookup = Resolve(query, roster);

            // Two spellings of one unit are one unit; two bad names are two complaints.
            if (lookup.Match is not null && seen.Add(lookup.Match.Name) == false)
            {
                continue;
            }

            found.Add(lookup);
        }

        return found;
    }

    /// <summary>
    /// Tried in this order, and the first that lands on exactly one unit wins. Exact
    /// before prefix before substring: "rhino" must not be beaten by a longer name that
    /// happens to contain it.
    /// </summary>
    private static readonly Func<string, string, bool>[] Rules =
    [
        (name, wanted) => name == wanted,
        (name, wanted) => name.StartsWith(wanted, StringComparison.Ordinal),
        (name, wanted) => name.Contains(wanted, StringComparison.Ordinal)
    ];

    /// <summary>
    /// The name as given, then without an English plural. "es" comes off before "s" so
    /// that a hypothetical "foxes" reaches "fox"; a two-letter stem is not a name worth
    /// searching on, so short words keep their s.
    /// </summary>
    private static IEnumerable<string> Forms(string wanted)
    {
        yield return wanted;

        if (wanted.EndsWith("es", StringComparison.Ordinal) && wanted.Length > 4)
        {
            yield return wanted[..^2];
        }

        if (wanted.EndsWith('s') && wanted.Length > 3)
        {
            yield return wanted[..^1];
        }
    }

    /// <summary>
    /// Letters and digits only, lower-cased. Spacing and punctuation carry no meaning in
    /// a unit name, and dictated names arrive with either or neither.
    /// </summary>
    private static string Normalize(string value)
    {
        var text = new StringBuilder(value.Length);

        foreach (var symbol in value)
        {
            if (char.IsLetterOrDigit(symbol))
            {
                text.Append(char.ToLowerInvariant(symbol));
            }
        }

        return text.ToString();
    }
}
