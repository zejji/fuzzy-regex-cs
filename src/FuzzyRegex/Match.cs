using System.Globalization;
using System.Text;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// A single stretch of the subject matched by a group. Shaped after
/// <see cref="System.Text.RegularExpressions.Capture"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Index"/> and <see cref="Length"/> are UTF-16 code units, matching the built-in
/// <c>Regex</c>. Upstream reports codepoints; see design spec section 4.
/// </para>
/// <para>
/// The engine works in <c>(start, end)</c> throughout, keeping upstream's names. This class and the
/// recorder in <c>tools/record-oracle.py</c> are the only two places that convert to
/// <c>(Index, Length)</c> (DECISIONS 2026-08-31), which is why a slip at either end shows up as an
/// oracle divergence rather than as a silent agreement.
/// </para>
/// </remarks>
public class Capture
{
    /// <summary>The subject the span points into.</summary>
    private protected readonly string _subject;

    /// <summary>The engine's <c>start</c>, a UTF-16 code unit index.</summary>
    private protected readonly int _start;

    /// <summary>The engine's <c>end</c>, one past the last code unit.</summary>
    private protected readonly int _end;

    internal Capture(string subject, int start, int end)
    {
        _subject = subject;
        _start = start;
        _end = end;
    }

    /// <summary>The position in the subject at which the captured text starts.</summary>
    public int Index => _start;

    /// <summary>The length of the captured text.</summary>
    public int Length => _end - _start;

    /// <summary>The captured text.</summary>
    public string Value => _subject[_start.._end];

    /// <summary>The captured text, without copying it out of the subject.</summary>
    public ReadOnlySpan<char> ValueSpan => _subject.AsSpan(_start, _end - _start);

    /// <summary>Returns <see cref="Value"/>.</summary>
    /// <returns>The captured text.</returns>
    public override string ToString() => Value;
}

/// <summary>
/// The result of one capturing group. Shaped after
/// <see cref="System.Text.RegularExpressions.Group"/>, but <see cref="Captures"/> is the full
/// capture list, which is an mrab-regex feature the built-in engine does not have.
/// </summary>
public class Group : Capture
{
    /// <summary>
    /// Every span this group captured, or <see langword="null"/> for group 0, whose single capture
    /// is the group itself - which is exactly what <c>match_get_captures_by_index</c> answers for
    /// index 0 (<c>upstream/src/_regex.c</c> line 19137).
    /// </summary>
    private readonly Engine.GroupSpan[]? _captures;

    internal Group(string subject, int start, int end, bool success, string name, Engine.GroupSpan[]? captures = null)
        : base(subject, start, end)
    {
        Success = success;
        Name = name;
        _captures = captures;
    }

    /// <summary>Whether the group took part in the match.</summary>
    public bool Success { get; }

    /// <summary>The group's name, or its number as text if it has none.</summary>
    public string Name { get; }

    /// <summary>
    /// Every capture the group made, oldest first, not just the last one. Upstream
    /// <c>Match.captures(group)</c>; the built-in <c>Regex</c> only keeps this for groups inside
    /// a repeated construct, whereas mrab-regex keeps it always.
    /// </summary>
    /// <remarks>
    /// Group 0 is the whole match, which is captured exactly once, so its list is a single span and
    /// needs none of the machinery real capture lists do. Upstream's own accessor special-cases
    /// index 0 the same way (<c>match_get_spans_by_index</c>, <c>:19081</c>).
    /// </remarks>
    public CaptureCollection Captures =>
        _captures is null
            ? new([this])
            : new([.. _captures.Select(span => new Capture(_subject, span.Start, span.End))]);
}

/// <summary>
/// The result of a matching operation. Shaped after
/// <see cref="System.Text.RegularExpressions.Match"/>, plus the mrab-regex-only
/// <see cref="FuzzyCounts"/>, <see cref="FuzzyChanges"/> and <see cref="PartialMatch"/>.
/// </summary>
public sealed class Match : Group
{
    private readonly FuzzyRegex _regex;
    private readonly Engine.GroupData[] _groups;
    private readonly int _lastGroup;

    /// <summary>
    /// Port of <c>pattern_new_match</c> (<c>upstream/src/_regex.c</c> line 20738), whose
    /// <c>fuzzy_counts</c>/<c>fuzzy_changes</c> half is Phase 5's and whose <c>pos</c>/<c>endpos</c>
    /// fields nothing on this surface reports.
    /// </summary>
    /// <param name="regex">The pattern that produced this match, for its group names.</param>
    /// <param name="subject">The subject matched against.</param>
    /// <param name="start">Where the match starts, in UTF-16 code units.</param>
    /// <param name="end">One past where it ends.</param>
    /// <param name="success">Whether the pattern matched at all.</param>
    /// <param name="groups">
    /// The engine's group data, already copied out of the state - the state's arrays are reused by
    /// the next match, so a match that shared them would change under its owner.
    /// </param>
    /// <param name="lastIndex">The engine's <c>lastindex</c>.</param>
    /// <param name="lastGroup">The engine's <c>lastgroup</c>.</param>
    /// <param name="partial">Whether this is a partial match.</param>
    internal Match(
        FuzzyRegex regex,
        string subject,
        int start,
        int end,
        bool success,
        Engine.GroupData[] groups,
        int lastIndex = -1,
        int lastGroup = -1,
        bool partial = false
    )
        : base(subject, start, end, success, "0")
    {
        _regex = regex;
        _groups = groups;
        _lastGroup = lastGroup;
        LastGroupNumber = lastIndex;
        PartialMatch = partial;
    }

    /// <summary>The groups of the pattern, group 0 being the whole match.</summary>
    public GroupCollection Groups => new(this, _groups.Length);

    /// <summary>
    /// Port of <c>match_get_group_by_index</c> (<c>upstream/src/_regex.c</c> line 18847) and the
    /// span, start and end getters beside it (<c>:18880-19180</c>), which this surface answers with
    /// one <see cref="RegularExpressions.Group"/> rather than with five separate accessors.
    /// </summary>
    /// <param name="number">The group number, one or more.</param>
    /// <returns>The group.</returns>
    internal Group GroupAt(int number)
    {
        // Capture group indexes are 1-based (excluding group 0, which is the entire matched string).
        Engine.GroupData group = _groups[number - 1];
        string name = _regex.GroupNameFromNumber(number);

        if (group.Current < 0)
        {
            // The group took no part in the match. Upstream reports -1 from start, end and span and
            // None from group; this surface reports Success == false, and an unsuccessful group's
            // span is (0, 0) - the shape the built-in Regex uses, and the one the oracle recorder
            // writes for a group whose Python span is (-1, -1) (tools/record-oracle.py:223).
            return new Group(_subject, 0, 0, success: false, name, []);
        }

        Engine.GroupSpan span = group.Captures[group.Current];

        return new Group(_subject, span.Start, span.End, success: true, name, group.Captures);
    }

    /// <summary>The number of the group with that name, or <c>-1</c> if the pattern has none.</summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group number.</returns>
    internal int GroupNumberFromName(string name) => _regex.GroupNumberFromName(name);

    /// <summary>
    /// Whether this is a partial match: the subject ran out before the pattern could either
    /// succeed or fail. Upstream <c>Match.partial</c>, set by matching with <c>partial=True</c>.
    /// </summary>
    /// <remarks>
    /// Always <see langword="false"/> until Phase 4: the only way to get a partial match is to ask
    /// for one, and every entry point refuses <c>partial: true</c> with a <c>needs:partial</c> seam.
    /// The field is here rather than a constant so that Phase 4 has somewhere to put the answer.
    /// </remarks>
    public bool PartialMatch { get; }

    /// <summary>
    /// How many errors of each kind the fuzzy match used. Zero throughout for an exact match.
    /// Upstream <c>Match.fuzzy_counts</c>.
    /// </summary>
    public FuzzyCounts FuzzyCounts =>
        throw new NotImplementedException("needs:fuzzy-counts - fuzzy matching is not implemented yet");

    /// <summary>
    /// Where the fuzzy match used each kind of error. Upstream <c>Match.fuzzy_changes</c>.
    /// </summary>
    public FuzzyChanges FuzzyChanges =>
        throw new NotImplementedException("needs:fuzzy-changes - fuzzy matching is not implemented yet");

    /// <summary>
    /// The number of the last group that took part in the match, or <c>-1</c> if no group did.
    /// Upstream <c>Match.lastindex</c>.
    /// </summary>
    /// <remarks>
    /// Not derivable from <see cref="Groups"/>: verified against the local oracle 2026-08-29,
    /// <c>regex.match('((a))', 'a').lastindex</c> is <c>1</c> even though groups 1 and 2 both
    /// succeed and both span <c>(0, 1)</c>, so nothing about the groups distinguishes them.
    /// </remarks>
    public int LastGroupNumber { get; }

    /// <summary>
    /// The name of the last <em>named</em> group that took part in the match, or
    /// <see langword="null"/> if none did. Upstream <c>Match.lastgroup</c>.
    /// </summary>
    /// <remarks>
    /// Also not derivable: <c>regex.match('(?P&lt;a&gt;a(b))', 'ab').lastgroup</c> is <c>'a'</c>
    /// although the unnamed group 2 succeeded later, at span <c>(1, 2)</c>.
    /// <para>
    /// Upstream looks the number up in <c>indexgroup</c>, its inverted <c>groupindex</c>
    /// (<c>match_lastgroup</c>, <c>:20442</c>). Only a named group is ever recorded as
    /// <c>lastgroup</c>, so the lookup always finds a real name and never falls back to the number
    /// that <see cref="FuzzyRegex.GroupNameFromNumber"/> gives an unnamed group.
    /// </para>
    /// </remarks>
    public string? LastGroupName => _lastGroup >= 0 ? _regex.GroupNameFromNumber(_lastGroup) : null;

    /// <summary>Finds the next match, starting where this one ended.</summary>
    /// <returns>The next match, or an unsuccessful match if there is none.</returns>
    public Match NextMatch() =>
        throw new NotImplementedException("needs:find-all - iterating over the matches is not implemented yet");

    /// <summary>
    /// Expands a replacement template against this match, so <c>\1</c> and <c>\g&lt;name&gt;</c>
    /// become the text those groups captured. Upstream <c>Match.expand</c>; the built-in
    /// <c>Regex</c> calls it <c>Result</c>.
    /// </summary>
    /// <remarks>
    /// The template language is upstream's, not <c>Regex</c>'s: the escape character is
    /// <c>\</c>, so <c>\1</c>, <c>\g&lt;name&gt;</c>, <c>\n</c>, <c>\x41</c> and
    /// <c>\N{LATIN CAPITAL LETTER A}</c> all mean what they mean upstream, and <c>$</c> is
    /// ordinary text. The two languages cannot both be honoured because they disagree about
    /// <c>\</c> - verified against both engines 2026-08-29: for the template <c>\n</c>,
    /// <c>regex.sub('.', r'\n', 'x')</c> gives a newline where
    /// <c>Regex.Replace("x", ".", @"\n")</c> gives a backslash followed by <c>n</c>.
    /// </remarks>
    /// <param name="replacement">The replacement template, in upstream's syntax.</param>
    /// <returns>The expanded text.</returns>
    /// <exception cref="FuzzyRegexParseException">The template is not valid.</exception>
    /// <exception cref="ArgumentException">It references a group the pattern has not got.</exception>
    public string Result(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        // match_expand (upstream/src/_regex.c line 19902). A template with no backslash in it is
        // returned as it is, without ever reaching the template compiler.
        if (Engine.Substitution.IsLiteralTemplate(replacement, '\\'))
        {
            return replacement;
        }

        var expanded = new StringBuilder(replacement.Length);
        foreach (object item in _regex.CompileReplacement(replacement))
        {
            expanded.Append(Engine.Substitution.GetMatchReplacement(item, this, _groups.Length));
        }

        return expanded.ToString();
    }

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
    /// <exception cref="FormatException">The template is malformed.</exception>
    /// <exception cref="ArgumentException">It references a group the pattern has not got.</exception>
    public string ResultFormat(string format)
    {
        ArgumentNullException.ThrowIfNull(format);

        // match_expandf (upstream/src/_regex.c line 20045) hands the template straight to
        // str.format, with none of the literal shortcut Result has: verified 2026-09-01,
        // `regex.match(r'(\w+)', 'ab').expandf('}')` raises where `regex.subf(r'(\w+)', '}', 'ab')`
        // returns '}', because only subf checks for a '{' first.
        return Engine.Substitution.ExpandFormat(format, ResolveFormatArgument);
    }

    /// <summary>
    /// One argument of the <c>str.format</c> call <c>match_expandf</c> makes: the positional
    /// arguments are one capture object per group with group 0 first (<c>:20065</c>), and the
    /// keyword arguments are the named groups (<c>make_capture_dict</c>, <c>:19983</c>).
    /// </summary>
    /// <param name="argument">The field name: a run of digits, or a group name.</param>
    /// <returns>The group, or <see langword="null"/> if this match has no such argument.</returns>
    private Group? ResolveFormatArgument(string argument)
    {
        // NumberStyles.None accepts digits and nothing else, which is CPython's own rule for
        // telling a positional field from a named one. A number too long for an int falls through
        // to the name lookup and fails there, which is upstream's answer for it too.
        if (int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            if (number == 0)
            {
                return this;
            }

            return number <= _groups.Length ? GroupAt(number) : null;
        }

        int named = _regex.GroupNumberFromName(argument);
        return named < 0 ? null : GroupAt(named);
    }
}
