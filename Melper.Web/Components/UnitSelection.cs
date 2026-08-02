using Melper.Core.Services;
using Melper.Data;

namespace Melper.Web.Components;

/// <summary>
/// What <see cref="UnitPicker"/> hands a page when the selection changes: the two
/// include-only name patterns, in the same storage format the plain text inputs on the
/// other pages use.
/// </summary>
public sealed record UnitSelection(string MainPattern, string VsPattern)
{
    /// <summary>The localStorage key holding the pattern for the unit being calculated.</summary>
    public const string MainKey = "mainNameFilter";

    /// <summary>The localStorage key holding the pattern for the unit it is measured against.</summary>
    public const string VsKey = "vsNameFilter";

    public static readonly UnitSelection Everything = new("", "");

    public IReadOnlyCollection<Unit> Mains(IReadOnlyCollection<Unit> all) =>
        UnitFilterBuilder.SelectMatching(MainPattern, all);

    public IReadOnlyCollection<Unit> Opponents(IReadOnlyCollection<Unit> all) =>
        UnitFilterBuilder.SelectMatching(VsPattern, all);
}
