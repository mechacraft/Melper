using Melper.Data;

namespace Melper.Core.Services;

/// <summary>
/// Translates between a unit selection and the include-only name pattern the
/// pages feed to <see cref="StringExtensions.RegMatch"/>: hand-written patterns
/// like <c>cra|fang|tara</c> stay the storage format, the picker just generates
/// and reads them back.
/// </summary>
public static class UnitFilterBuilder
{
    /// <summary>
    /// Shortest token worth emitting. Two characters would often be enough but
    /// reads as noise in the input; three still keeps the whole pattern short.
    /// </summary>
    private const int MinTokenLength = 3;

    /// <summary>
    /// Builds the shortest pattern that matches exactly <paramref name="selected"/>
    /// out of <paramref name="all"/>. Both extremes - nothing picked and
    /// everything picked - mean "do not narrow anything down", which is the empty
    /// pattern the pages treat as no filter.
    /// </summary>
    public static string Build(IEnumerable<Unit> selected, IReadOnlyCollection<Unit> all)
    {
        var allNames = all.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var picked = selected.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (picked.Count == 0 || picked.Count >= allNames.Count)
        {
            return "";
        }

        var rejected = allNames.Where(x => picked.Contains(x) == false).ToList();

        var tokens = new List<string>();
        foreach (var name in allNames.Where(picked.Contains))
        {
            // The pattern is a union, so a name an earlier token already reaches
            // needs no token of its own.
            if (tokens.Any(name.RegMatch))
            {
                continue;
            }

            tokens.Add(ShortestSafeToken(name, rejected));
        }

        return string.Join("|", tokens);
    }

    /// <summary>
    /// The units a stored pattern lets through. An empty pattern - and a
    /// half-typed one that does not compile - passes everything, matching how the
    /// pages behave.
    /// </summary>
    public static IReadOnlyCollection<Unit> SelectMatching(string? pattern, IReadOnlyCollection<Unit> all)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return all;
        }

        try
        {
            return all.Where(x => x.Name.RegMatch(pattern)).ToList();
        }
        catch (ArgumentException)
        {
            return all;
        }
    }

    /// <summary>
    /// The shortest leading slice of <paramref name="name"/> that appears in none
    /// of the <paramref name="rejected"/> names. Lower-cased: matching ignores
    /// case anyway, and it keeps the pattern looking like a hand-typed one.
    /// </summary>
    private static string ShortestSafeToken(string name, IReadOnlyCollection<string> rejected)
    {
        for (var length = Math.Min(MinTokenLength, name.Length); length <= name.Length; length++)
        {
            var token = name[..length];
            if (rejected.Any(x => x.Contains(token, StringComparison.OrdinalIgnoreCase)) == false)
            {
                return token.ToLowerInvariant();
            }
        }

        // A rejected name contains this one whole ("Fang" inside a "Fangs"), so
        // nothing short of an exact match separates them.
        return $"^{name.ToLowerInvariant()}$";
    }
}
