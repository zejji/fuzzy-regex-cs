namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// A single stretch of the subject matched by a group. Shaped after
/// <see cref="System.Text.RegularExpressions.Capture"/>.
/// </summary>
/// <remarks>
/// <see cref="Index"/> and <see cref="Length"/> are UTF-16 code units, matching the built-in
/// <c>Regex</c>. Upstream reports codepoints; see design spec section 4.
/// </remarks>
public class Capture
{
    internal Capture() { }

    /// <summary>The position in the subject at which the captured text starts.</summary>
    public int Index => throw new NotImplementedException();

    /// <summary>The length of the captured text.</summary>
    public int Length => throw new NotImplementedException();

    /// <summary>The captured text.</summary>
    public string Value => throw new NotImplementedException();

    /// <summary>The captured text, without copying it out of the subject.</summary>
    public ReadOnlySpan<char> ValueSpan => throw new NotImplementedException();

    /// <summary>Returns <see cref="Value"/>.</summary>
    /// <returns>The captured text.</returns>
    public override string ToString() => throw new NotImplementedException();
}

/// <summary>
/// The result of one capturing group. Shaped after
/// <see cref="System.Text.RegularExpressions.Group"/>, but <see cref="Captures"/> is the full
/// capture list, which is an mrab-regex feature the built-in engine does not have.
/// </summary>
public class Group : Capture
{
    internal Group() { }

    /// <summary>Whether the group took part in the match.</summary>
    public bool Success => throw new NotImplementedException();

    /// <summary>The group's name, or its number as text if it has none.</summary>
    public string Name => throw new NotImplementedException();

    /// <summary>
    /// Every capture the group made, oldest first, not just the last one. Upstream
    /// <c>Match.captures(group)</c>; the built-in <c>Regex</c> only keeps this for groups inside
    /// a repeated construct, whereas mrab-regex keeps it always.
    /// </summary>
    public CaptureCollection Captures => throw new NotImplementedException();
}

/// <summary>
/// The result of a matching operation. Shaped after
/// <see cref="System.Text.RegularExpressions.Match"/>, plus the mrab-regex-only
/// <see cref="FuzzyCounts"/>, <see cref="FuzzyChanges"/> and <see cref="PartialMatch"/>.
/// </summary>
public sealed class Match : Group
{
    internal Match() { }

    /// <summary>The groups of the pattern, group 0 being the whole match.</summary>
    public GroupCollection Groups => throw new NotImplementedException();

    /// <summary>
    /// Whether this is a partial match: the subject ran out before the pattern could either
    /// succeed or fail. Upstream <c>Match.partial</c>, set by matching with <c>partial=True</c>.
    /// </summary>
    public bool PartialMatch => throw new NotImplementedException();

    /// <summary>
    /// How many errors of each kind the fuzzy match used. Zero throughout for an exact match.
    /// Upstream <c>Match.fuzzy_counts</c>.
    /// </summary>
    public FuzzyCounts FuzzyCounts => throw new NotImplementedException();

    /// <summary>
    /// Where the fuzzy match used each kind of error. Upstream <c>Match.fuzzy_changes</c>.
    /// </summary>
    public FuzzyChanges FuzzyChanges => throw new NotImplementedException();

    /// <summary>
    /// The number of the last group that took part in the match, or <c>-1</c> if no group did.
    /// Upstream <c>Match.lastindex</c>.
    /// </summary>
    /// <remarks>
    /// Not derivable from <see cref="Groups"/>: verified against the local oracle 2026-08-29,
    /// <c>regex.match('((a))', 'a').lastindex</c> is <c>1</c> even though groups 1 and 2 both
    /// succeed and both span <c>(0, 1)</c>, so nothing about the groups distinguishes them.
    /// </remarks>
    public int LastGroupNumber => throw new NotImplementedException();

    /// <summary>
    /// The name of the last <em>named</em> group that took part in the match, or
    /// <see langword="null"/> if none did. Upstream <c>Match.lastgroup</c>.
    /// </summary>
    /// <remarks>
    /// Also not derivable: <c>regex.match('(?P&lt;a&gt;a(b))', 'ab').lastgroup</c> is <c>'a'</c>
    /// although the unnamed group 2 succeeded later, at span <c>(1, 2)</c>.
    /// </remarks>
    public string? LastGroupName => throw new NotImplementedException();

    /// <summary>Finds the next match, starting where this one ended.</summary>
    /// <returns>The next match, or an unsuccessful match if there is none.</returns>
    public Match NextMatch() => throw new NotImplementedException();

    /// <summary>
    /// Expands a replacement template against this match, so <c>$1</c> and <c>${name}</c> become
    /// the text those groups captured. Upstream <c>Match.expand</c>; the built-in <c>Regex</c>
    /// calls it <c>Result</c>.
    /// </summary>
    /// <param name="replacement">The replacement template.</param>
    /// <returns>The expanded text.</returns>
    public string Result(string replacement) => throw new NotImplementedException();

    /// <summary>
    /// Expands a <c>str.format</c>-style template against this match, where <c>{0}</c> is the
    /// whole match, <c>{1}</c> is group 1 and <c>{name}</c> is a named group. Upstream
    /// <c>Match.expandf</c>, which is a separate templating language from the one
    /// <see cref="Result(string)"/> takes - not a formatting option on it.
    /// </summary>
    /// <remarks>
    /// Verified against the local oracle 2026-08-29:
    /// <c>regex.match(r'(\w+) (\w+)', 'a b').expandf('{1} {0}')</c> is <c>'a a b'</c>, which
    /// shows <c>{0}</c> standing for the whole match rather than for group 1.
    /// </remarks>
    /// <param name="format">The format template.</param>
    /// <returns>The expanded text.</returns>
    public string ResultFormat(string format) => throw new NotImplementedException();
}
