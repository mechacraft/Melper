using System.Reflection;

namespace Melper.Data;

/// <summary>
/// The roster the whole app calculates against. The shipped numbers live in the embedded
/// <c>units.json</c>; <see cref="Units"/> starts as a copy of them and can be swapped for
/// edited values at runtime (see the Data page in the web app).
/// </summary>
public static class UnitsCollection
{
    /// <summary>The roster exactly as it is checked into the repository.</summary>
    public static string DefaultJson { get; } = ReadEmbeddedJson();

    /// <summary>
    /// When the checked-in numbers were last checked against the game. Bump the
    /// <c>Date</c> line in <c>units.json</c> whenever the stats below it change.
    /// </summary>
    public static DateOnly Date { get; } = UnitsJson.DeserializeRoster(DefaultJson).Date;

    /// <summary>
    /// The roster in effect. Callers may hold on to this reference — <see cref="Replace"/>
    /// rewrites the contents in place rather than handing out a new list, so a page that
    /// captured it in a field still sees edits.
    /// </summary>
    public static readonly List<Unit> Units = Defaults();

    /// <summary>A fresh, independent copy of the shipped roster.</summary>
    public static List<Unit> Defaults() => UnitsJson.DeserializeRoster(DefaultJson).Units;

    public static void Replace(IEnumerable<Unit> units)
    {
        var replacement = units.ToList();
        Units.Clear();
        Units.AddRange(replacement);
    }

    private static string ReadEmbeddedJson()
    {
        using var stream = typeof(UnitsCollection).Assembly.GetManifestResourceStream("units.json")
                           ?? throw new InvalidOperationException("Embedded units.json is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
