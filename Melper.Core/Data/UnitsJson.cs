using System.Text.Json;
using System.Text.Json.Serialization;

namespace Melper.Data;

/// <summary>
/// The single JSON contract for the roster — used both for the file checked into the
/// repository and for the copy a browser tab keeps in sessionStorage, so a payload
/// written by the editor page always reads back the same way the shipped file does.
/// </summary>
public static class UnitsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Keeps the file about as terse as the C# initializer it replaced: only the
        // fields a unit actually sets show up. Required members opt out of this per
        // property, otherwise a zeroed one would be omitted and fail to read back.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        // units.json is meant to be edited by hand after a patch, so a stray "damage"
        // for "Damage" should read rather than silently come back as zero.
        PropertyNameCaseInsensitive = true,
        Converters = { new TimeSpanSecondsConverter() }
    };

    /// <summary>
    /// The shape of the checked-in <c>units.json</c>. The date rides along with the numbers
    /// on purpose: whoever edits the stats after a patch cannot miss it sitting on line two.
    /// A tab's own edits are stored as the bare array instead — they are not a new reading
    /// of the game, so they must not restamp the roster.
    /// </summary>
    public sealed record Roster
    {
        /// <summary>When the numbers below were last checked against the game.</summary>
        public required DateOnly AsOf { get; init; }

        public required List<Unit> Units { get; init; }
    }

    public static Roster DeserializeRoster(string json) =>
        JsonSerializer.Deserialize<Roster>(json, Options)
        ?? throw new JsonException("Roster JSON deserialized to null.");

    public static string SerializeRoster(Roster roster) => JsonSerializer.Serialize(roster, Options);

    /// <summary>
    /// Reads whichever of the two shapes it is handed: the bare array a tab keeps in
    /// sessionStorage, or the dated object the repository file and the Data page's export
    /// use. An imported <c>AsOf</c> is read and then dropped — <see cref="UnitsCollection.AsOf"/>
    /// is fixed to the shipped file for the life of the process, and a pasted roster is
    /// someone's edits rather than a fresh reading of the game, so it must not claim a date.
    /// </summary>
    public static List<Unit> DeserializeAny(string json)
    {
        var text = json.AsSpan().TrimStart();
        if (text.IsEmpty)
        {
            throw new JsonException("The JSON is empty.");
        }

        var units = text[0] == '[' ? Deserialize(json) : DeserializeRoster(json).Units;

        // A required member only has to be present, not non-null, so "Units": null and a
        // null entry in the array both read as valid and would then blow up on first use.
        if (units is null || units.Any(u => u is null))
        {
            throw new JsonException("The roster has nulls where units should be.");
        }

        return units;
    }

    public static string Serialize(IEnumerable<Unit> units) =>
        JsonSerializer.Serialize(units.ToList(), Options);

    public static List<Unit> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<Unit>>(json, Options)
        ?? throw new JsonException("Units JSON deserialized to null.");

    /// <summary>Reload times read as plain seconds (0.6) rather than as "00:00:00.6000000".</summary>
    private sealed class TimeSpanSecondsConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            TimeSpan.FromSeconds(reader.GetDouble());

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Math.Round(value.TotalSeconds, 4));
    }
}
