namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// Renderings that let a dictionary or a nested collection be compared with
/// <c>Should().Equal(...)</c> instead of <c>Should().BeEquivalentTo(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// S53. <c>BeEquivalentTo</c> is the one part of AwesomeAssertions that cannot run under Native
/// AOT: its equivalency engine reaches <c>MethodInfo.MakeGenericMethod()</c>, which has no
/// native code to call. Measured 2026-09-16 on the published suite - 1,547 of 6,153 tests failed
/// with <c>NotSupportedException: '...GenericEnumerableEquivalencyStep.HandleImpl[...]' is
/// missing native code</c>, every one of them at a <c>BeEquivalentTo</c> call and not one of them
/// inside <c>src/FuzzyRegex</c>. Nothing else in the library failed, so the fix belongs here
/// rather than in a switch of assertion library.
/// </para>
/// <para>
/// Rendering to sorted strings keeps what <c>BeEquivalentTo</c> was being used for - equality
/// that does not care about enumeration order - and keeps the failure readable, because
/// <c>Equal</c> on two sorted line lists reports the first line that differs. It is deliberately
/// not a generic deep comparer: every caller here compares a flat map of strings to strings,
/// numbers or sets, and a real recursive comparer is exactly the reflection this has to avoid.
/// </para>
/// </remarks>
internal static class Equivalence
{
    /// <summary>
    /// The delimiter <see cref="RowLines"/> joins a row with: U+001F UNIT SEPARATOR, ASCII's own
    /// field separator. It is the right character because no element any caller joins here can
    /// contain it, so two different rows can never render to the same line.
    /// </summary>
    /// <remarks>
    /// Written as a code point rather than as the raw control character, which would be invisible
    /// in this file and in every diff of it. Not as a backslash-u escape either, although that was
    /// what was asked for: the editing tools used to write this file silently turn such an escape
    /// back into the character it denotes, so that spelling does not survive being saved. The cast
    /// is the spelling that stays legible.
    /// </remarks>
    private const char _unitSeparator = (char)0x1F;

    /// <summary>One <c>key=value</c> line per entry, ordinal-sorted by key.</summary>
    /// <typeparam name="TValue">The value type, rendered with its invariant <c>ToString</c>.</typeparam>
    /// <param name="map">The map to render.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> Lines<TValue>(IEnumerable<KeyValuePair<string, TValue>> map) =>
        [
            .. map.Select(static entry =>
                    $"{entry.Key}={Convert.ToString(entry.Value, System.Globalization.CultureInfo.InvariantCulture)}"
                )
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>
    /// One <c>key=[member, member]</c> line per entry, ordinal-sorted by key and within each entry,
    /// so that a map whose values are sets compares as set equality on both levels.
    /// </summary>
    /// <param name="map">The map to render.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> SetLines(IEnumerable<KeyValuePair<string, IReadOnlySet<string>>> map) =>
        [
            .. map.Select(static entry =>
                    $"{entry.Key}=[{string.Join(", ", entry.Value.Order(StringComparer.Ordinal))}]"
                )
                .Order(StringComparer.Ordinal),
        ];

    /// <summary>The same strings, ordinal-sorted, so two collections compare without their order.</summary>
    /// <param name="values">The collection to sort.</param>
    /// <returns>The sorted collection.</returns>
    internal static IReadOnlyList<string> Sorted(IEnumerable<string> values) =>
        [.. values.Order(StringComparer.Ordinal)];

    /// <summary>
    /// One line per row, each row's elements joined, so a sequence of sequences compares in order
    /// on the outside and element by element within a row.
    /// </summary>
    /// <param name="rows">The rows to render.</param>
    /// <returns>The lines, in the order given.</returns>
    internal static IReadOnlyList<string> RowLines(IEnumerable<IEnumerable<string>> rows) =>
        [.. rows.Select(static row => string.Join(_unitSeparator, row))];
}
