using System.Text.RegularExpressions;

namespace Melper.Core;

public static class StringExtensions
{
    public static bool RegMatch(this string input, string pattern)
    {
        return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
    }
}