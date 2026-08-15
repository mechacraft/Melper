using System.Text.RegularExpressions;

namespace Melper.Core;

public static class StringExtensions
{
    public static bool RegMatch(this string input, string pattern)
    {
        return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Whether a name passes a filter box that is recalculated on every keystroke.
    /// An empty pattern filters nothing. A half-written one - "cra(" on the way to
    /// "cra(b|w)" - is not an error but it matches nothing yet: letting it through
    /// instead would flash the full roster back into the table mid-word.
    /// </summary>
    public static bool PassesFilter(this string input, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        try
        {
            return input.RegMatch(pattern);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}