namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Reads the group values a row of upstream's <c>test_various</c> table asks for.
/// </summary>
/// <remarks>
/// Upstream's loop calls <c>m.group(*group_list)</c> when the row names groups and <c>m[:]</c>
/// when it does not; the second case is spelled <c>"*"</c> here. A group that did not take part
/// in the match is <see langword="null"/>, which is what Python's <c>None</c> becomes.
/// </remarks>
internal static class VariousTable
{
    /// <summary>Returns the values of the groups <paramref name="spec"/> names.</summary>
    /// <param name="match">The match to read.</param>
    /// <param name="spec">
    /// A comma-separated list of group numbers and names, or <c>"*"</c> for every group.
    /// </param>
    /// <returns>One value per named group, <see langword="null"/> where it did not participate.</returns>
    public static string?[] GroupValues(Match match, string spec)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(spec);

        if (string.Equals(spec, "*", StringComparison.Ordinal))
        {
            return [.. match.Groups.Select(Value)];
        }

        return
        [
            .. spec.Split(',')
                .Select(key => int.TryParse(key, out int number) ? match.Groups[number] : match.Groups[key])
                .Select(Value),
        ];
    }

    private static string? Value(Group group) => group.Success ? group.Value : null;
}
