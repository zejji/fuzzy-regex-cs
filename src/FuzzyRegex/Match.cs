using System.Globalization;
using System.Text;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// A single stretch of the subject matched by a group. Shaped after
/// <see cref="System.Text.RegularExpressions.Capture"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Indices are UTF-16 code units</b>, where upstream counts codepoints: <see cref="Index"/>
/// and <see cref="Length"/> match the built-in <c>Regex</c> (design spec section 4).
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
    /// Every capture the group made, oldest first. Upstream
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
/// <remarks>
/// <para>
/// <b><c>Match</c> may be read from any thread, where <c>System.Text.RegularExpressions.Match</c>
/// may not</b>. It holds a copy of everything it reports, taken before the engine's state was
/// reused, so it neither shares with the pattern that produced it nor changes after it is
/// handed over.
/// </para>
/// <para>
/// This is deliberately stronger than the built-in <see cref="System.Text.RegularExpressions.Match"/>,
/// whose documentation says result objects "should be used on a single thread" because "their
/// implementations could delay computation of some results". One value here is computed on demand -
/// <see cref="FuzzyChanges"/> - and it is published as a single reference precisely so that this
/// promise holds; see <see cref="_splitChanges"/> for what went wrong when it was not.
/// </para>
/// <para>
/// <b><c>Regex</c>-shaped surface</b>: this type mirrors
/// <see cref="System.Text.RegularExpressions.Match"/> with <c>(Index, Length)</c>-based spans;
/// <c>expand</c> is <see cref="Result(string)"/>, <c>expandf</c> is
/// <see cref="ResultFormat(string)"/>, <c>lastindex</c> is <see cref="LastGroupNumber"/> with
/// Python's <see langword="None"/> rendered as <c>-1</c>, and <c>lastgroup</c> is
/// <see cref="LastGroupName"/> with <see langword="None"/> rendered as <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class Match : Group
{
    private readonly FuzzyRegex _regex;
    private readonly Engine.GroupData[] _groups;
    private readonly int _lastGroup;
    private readonly int _sliceStart;
    private readonly int _sliceEnd;
    private readonly bool _overlapped;
    private readonly bool _oneUnitPerCharacter;
    private readonly Engine.FuzzyChange[] _fuzzyChanges;

    /// <summary>
    /// The cache behind <see cref="FuzzyChanges"/>, boxed so that publishing it is a single
    /// reference write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It was a <c>FuzzyChanges?</c> until S52b, and that was a real defect</b>, because
    /// <see cref="RegularExpressions.FuzzyChanges"/> is a three-reference struct and a nullable
    /// one is four fields wide: writing it is not atomic, so a second thread reading
    /// <see cref="FuzzyChanges"/> could see the "has a value" flag already set and one or more of
    /// the three lists still null. That is not theoretical - <c>ThreadSafetyStressTests</c>'s
    /// <c>One_match_can_be_read_from_many_threads_at_once</c> reproduced it on the first run, and
    /// this port documents a <see cref="Match"/> as readable from any thread.
    /// </para>
    /// <para>
    /// A <see cref="System.Runtime.CompilerServices.StrongBox{T}"/> rather than a
    /// <see cref="Lazy{T}"/> because a <c>Lazy</c> allocates on every match whether or not anyone
    /// reads the property, and rather than computing the lists in the constructor because that
    /// allocates three of them on every match, fuzzy or not. Two threads racing here may each build
    /// a box and one wins; the value is a pure function of the readonly
    /// <see cref="_fuzzyChanges"/>, so the loser's answer was equal to the winner's.
    /// </para>
    /// </remarks>
    private System.Runtime.CompilerServices.StrongBox<FuzzyChanges>? _splitChanges;

    /// <summary>
    /// Port of <c>pattern_new_match</c> (<c>upstream/src/_regex.c</c> line 20738), whose
    /// <c>pos</c>/<c>endpos</c> fields nothing on this surface reports.
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
    /// <param name="sliceStart">
    /// Where the slice this match was found in starts, which <see cref="NextMatch"/> resumes inside
    /// - not where the match starts.
    /// </param>
    /// <param name="sliceEnd">One past where that slice ends.</param>
    /// <param name="overlapped">Whether the scan this match came from allowed matches to overlap.</param>
    /// <param name="lastIndex">The engine's <c>lastindex</c>.</param>
    /// <param name="lastGroup">The engine's <c>lastgroup</c>.</param>
    /// <param name="partial">Whether this is a partial match.</param>
    /// <param name="fuzzyCounts">How many errors of each kind the match used.</param>
    /// <param name="fuzzyChanges">
    /// Every error the match used, in the order it was used, already copied out of the state.
    /// </param>
    /// <param name="oneUnitPerCharacter">
    /// The state's <see cref="Engine.MatchState.OneUnitPerCharacter"/>, which <see cref="NextMatch"/>
    /// hands to the next state instead of scanning the subject again. The default,
    /// <see langword="false"/>, is always correct and only slower: the next state then converts
    /// between characters and positions by table rather than by arithmetic.
    /// </param>
    internal Match(
        FuzzyRegex regex,
        string subject,
        int start,
        int end,
        bool success,
        Engine.GroupData[] groups,
        int sliceStart,
        int sliceEnd,
        bool overlapped,
        int lastIndex = -1,
        int lastGroup = -1,
        bool partial = false,
        FuzzyCounts fuzzyCounts = default,
        Engine.FuzzyChange[]? fuzzyChanges = null,
        bool oneUnitPerCharacter = false
    )
        : base(subject, start, end, success, "0")
    {
        _regex = regex;
        _groups = groups;
        _lastGroup = lastGroup;
        _sliceStart = sliceStart;
        _sliceEnd = sliceEnd;
        _overlapped = overlapped;
        _oneUnitPerCharacter = oneUnitPerCharacter;
        _fuzzyChanges = fuzzyChanges ?? [];
        LastGroupNumber = lastIndex;
        PartialMatch = partial;

        // S47, ledger entry 11. ON A PARTIAL MATCH the counts are TALLIED FROM THE CHANGES instead
        // of being taken from the state's counter, because on that one exit the counter provably is
        // not the whole match's - see the remarks on 'FuzzyCounts'.
        FuzzyCounts = partial ? TallyChanges(_fuzzyChanges) : fuzzyCounts;
    }

    /// <summary>The three counts of a change list, which is what the list says the counts are.</summary>
    /// <param name="changes">The changes, in the order they were recorded.</param>
    /// <returns>How many of each kind it holds.</returns>
    private static FuzzyCounts TallyChanges(Engine.FuzzyChange[] changes)
    {
        int substitutions = 0;
        int insertions = 0;
        int deletions = 0;

        foreach (Engine.FuzzyChange change in changes)
        {
            switch (change.Type)
            {
                case Engine.FuzzyValue.Sub:
                    ++substitutions;
                    break;
                case Engine.FuzzyValue.Ins:
                    ++insertions;
                    break;
                default:
                    ++deletions;
                    break;
            }
        }

        return new FuzzyCounts(substitutions, insertions, deletions);
    }

    /// <summary>The groups of the pattern, group 0 being the whole match.</summary>
    public GroupCollection Groups => new(this, _regex.GroupCount);

    /// <summary>
    /// Port of <c>match_get_group_by_index</c> (<c>upstream/src/_regex.c</c> line 18847) and the
    /// span, start and end getters beside it (<c>:18880-19180</c>), which this surface answers with
    /// one <see cref="RegularExpressions.Group"/> rather than with five separate accessors.
    /// </summary>
    /// <param name="number">The group number, one or more.</param>
    /// <returns>The group.</returns>
    internal Group GroupAt(int number)
    {
        string name = _regex.GroupNameFromNumber(number);

        // An unsuccessful match holds no group data at all (see FuzzyRegex.NoMatch), and every one
        // of its groups is absent.
        if (_groups.Length == 0)
        {
            return new Group(_subject, 0, 0, success: false, name, []);
        }

        // Capture group indexes are 1-based (excluding group 0, which is the entire matched string).
        Engine.GroupData group = _groups[number - 1];

        if (group.Current < 0)
        {
            // The group has no current capture. Upstream reports -1 from start, end and span and
            // None from group; this surface reports Success == false, and an unsuccessful group's
            // span is (0, 0) - the shape the built-in Regex uses, and the one the oracle recorder
            // writes for a group whose Python span is (-1, -1) (tools/record-oracle.py:223).
            //
            // The capture list still goes out in full, because 'current' and the list are separate
            // in upstream too: 'match_get_group_by_index' (:18847) consults 'current' where
            // 'match_get_captures_by_index' (:19137) walks 'count' and never looks at it. A group
            // called with '(?&name)' is exactly the case where the two disagree - GROUP_RETURN
            // restores the caller's 'current' and leaves the callee's captures in place - and
            // upstream reports it that way (verified 2026-09-11, regex 2026.x):
            //
            //     regex.match(r'(?&routine)(?(DEFINE)(?<routine>.))', 'a')
            //     .group('routine') is None, .captures('routine') == ['a']
            return new Group(_subject, 0, 0, success: false, name, group.Captures);
        }

        Engine.GroupSpan span = group.Captures[group.Current];

        return new Group(_subject, span.Start, span.End, success: true, name, group.Captures);
    }

    /// <summary>The number of the group with that name, or <c>-1</c> if the pattern has none.</summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group number.</returns>
    internal int GroupNumberFromName(string name) => _regex.GroupNumberFromName(name);

    /// <summary>
    /// The name of the group with that number, or its number as text when it has none. What
    /// <see cref="GroupCollection"/> keys the dictionary view on.
    /// </summary>
    /// <param name="number">The group number.</param>
    /// <returns>The group's name.</returns>
    internal string GroupNameFromNumber(int number) => _regex.GroupNameFromNumber(number);

    /// <summary>
    /// Whether this is a partial match: the subject ran out before the pattern could either
    /// succeed or fail. Upstream <c>Match.partial</c>, set by matching with <c>partial=True</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only ever true when the match was asked for with <c>partial: true</c>, and only when no
    /// complete match existed there: <c>do_match</c> tries a normal match first and falls back to
    /// the partial one (<c>upstream/src/_regex.c:18140-18162</c>). The scan entry points never set
    /// it, because upstream's <c>finditer</c>/<c>findall</c> take no <c>partial</c> argument.
    /// </para>
    /// <para>
    /// <b>Reversed partial matches run out of text at the slice start</b>. Matched with
    /// <see cref="FuzzyRegexOptions.RightToLeft"/> (upstream's <c>(?r)</c>) and
    /// <c>partial: true</c>, this reports a partial at <c>beginning</c> when the pattern still
    /// needs characters and <c>beginning</c> is where the matchable text ends - upstream reports
    /// one there on some pattern shapes and no match on others, because it holds two
    /// contradictory rules for the same boundary. Nothing else about <c>beginning</c> moves:
    /// <c>^</c> and <c>\A</c> still refuse a non-zero one.
    /// </para>
    /// </remarks>
    public bool PartialMatch { get; }

    /// <summary>
    /// How many errors of each kind the fuzzy match used. Zero throughout for an exact match.
    /// Upstream <c>Match.fuzzy_counts</c> (<c>match_fuzzy_counts</c>, <c>:20493</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On a <see cref="PartialMatch"/> these are tallied from <see cref="FuzzyChanges"/>, where
    /// upstream copies <c>state-&gt;fuzzy_counts</c> either way (<c>:20755-20759</c>). S47, ledger
    /// entry 11, and a deliberate divergence.</b> The two attributes are documented as two views of
    /// one edit script, so a pair that contradicts the other cannot be right whichever half a
    /// caller trusts - and upstream's do contradict.
    /// </para>
    /// <para>
    /// The state's counter is scoped to the innermost OPEN fuzzy section and was never the whole
    /// match's tally: <c>FUZZY</c> zeroes it on entering a section and saves the enclosing
    /// section's on the sstack (<c>:13137</c>, and the <c>memset</c> at <c>:13143</c>), and
    /// <c>END_FUZZY</c> adds the inner back into the
    /// outer on the way out (<c>:12475-12513</c>). On a complete match every section has closed and
    /// the counter therefore IS the total, which is why it is still what a complete match reports.
    /// A partial match returns from inside the section, before any of that unwinding, so the
    /// counter holds the innermost section's errors alone - while the change list, which is global
    /// and is never scoped, holds every one of them.
    /// </para>
    /// <para>
    /// <c>match(r'(?:a\w(?:b\w){e&lt;=3}){i&lt;=1}', 'a ba', partial=True)</c> is the minimised
    /// case: upstream answers <c>(1, 0, 0)</c> with an <b>insertion</b> at 1, because the outer
    /// section's insertion is on its sstack and the inner section's substitution is in the counter.
    /// This port's change list is <c>[ins@1, sub@3]</c>, which is the real edit script - the outer
    /// section inserted the space and the inner substituted the 'a' - so it reports
    /// <c>(1, 1, 0)</c>. <b>Upstream cannot be asked whether its own list holds the same two</b>:
    /// <c>match_fuzzy_changes</c> reports only the first <c>sum(counts)</c> entries, so no public
    /// call on regex 2026.9.10 returns more than the one it does. What IS measurable is that the
    /// one it returns is the insertion its own counts deny.
    /// </para>
    /// <para>
    /// The other door onto the same contradiction is a search restart: see the change-list clear in
    /// <c>Matcher</c>'s <c>start_match</c>, which is the rest of the same fix. The third and fourth -
    /// the change list desynchronising across <c>POSIX</c> and <c>BESTMATCH</c> candidates, and a
    /// lookaround under <c>(?e)</c> - were ledger entry 11's remaining half and <b>are fixed by S48b</b>
    /// (<c>MatchState.PopFuzzyCounts</c> truncates the change list to the length its matching push
    /// recorded, and <c>Matcher.RestoreBestMatch</c> restores the running totals beside the counts).
    /// <b>This tally nevertheless stays confined to the partial exit</b>, because that is not what it
    /// was working around: on a partial match the state's counter provably is not the whole match's,
    /// which is mechanism B and is a divergence in its own right.
    /// </para>
    /// </remarks>
    public FuzzyCounts FuzzyCounts { get; }

    /// <summary>
    /// Where the fuzzy match used each kind of error. Upstream <c>Match.fuzzy_changes</c>
    /// (<c>match_fuzzy_changes</c>, <c>:20504</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Positions are UTF-16 code unit indices into the subject, where upstream's are codepoint
    /// indices; they are the engine's own positions and need no conversion, because the engine
    /// counts in code units throughout.
    /// </para>
    /// <para>
    /// <b>A deletion's position is measured in a restored subject.</b> Upstream shifts each
    /// deletion by the number of deletions recorded before it (<c>:20555-20558</c>), so what is
    /// reported is where the missing character would sit in a string that had them all put back -
    /// which can be past the end of the match, and past the end of the subject.
    /// </para>
    /// <para>
    /// Cached, because upstream builds three fresh lists on every attribute read and a .NET property
    /// that allocates on every get is a trap that a <c>foreach</c> over it falls into. The cache is
    /// published as one reference so that the read is safe from any thread - see
    /// <see cref="_splitChanges"/>.
    /// </para>
    /// </remarks>
    public FuzzyChanges FuzzyChanges => (_splitChanges ??= new(SplitFuzzyChanges())).Value;

    /// <summary>
    /// Upstream <c>match_fuzzy_changes</c> (<c>upstream/src/_regex.c</c> line 20504): one list of
    /// changes in the order they happened, split into three by kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the first <c>Total</c> changes are reported</b>, which is upstream's own bound:
    /// its loop runs <c>for (i = 0; i &lt; count; i++)</c> with <c>count</c> the sum of the three
    /// <b>counts</b> (<c>:20522</c>) rather than the length of the list <c>pattern_new_match</c>
    /// copied. The bound is also a real one here where upstream has none - its <c>count</c> can
    /// exceed the array it allocated, which is a read past the end.
    /// </para>
    /// <para>
    /// <b>Since S48b closed ledger entry 11's last two mechanisms this bound is a no-op on every
    /// path, and it is kept because it is upstream's line rather than because it is load-bearing.</b>
    /// <c>Total</c> is now always the length of this list: on a partial match
    /// <see cref="FuzzyCounts"/> is tallied from it, on a complete match the state's counter is the
    /// whole match's, and the restore sites keep the two in step. Before S48b it was doing real
    /// work - the list could hold a dozen entries for a one-error match across <c>POSIX</c> and
    /// <c>BESTMATCH</c> candidates, and reporting the whole of it would have turned an arbitrary
    /// answer into a plainly wrong one. Removing it now would be a divergence from upstream for no
    /// gain, so it stays.
    /// </para>
    /// </remarks>
    /// <returns>The three lists.</returns>
    private FuzzyChanges SplitFuzzyChanges()
    {
        List<int> substitutions = [];
        List<int> insertions = [];
        List<int> deletions = [];
        int offset = 0;
        int reported = Math.Min(FuzzyCounts.Total, _fuzzyChanges.Length);

        for (int i = 0; i < reported; i++)
        {
            Engine.FuzzyChange change = _fuzzyChanges[i];

            switch (change.Type)
            {
                case Engine.FuzzyValue.Sub:
                    substitutions.Add(change.Pos);
                    break;
                case Engine.FuzzyValue.Ins:
                    insertions.Add(change.Pos);
                    break;
                case Engine.FuzzyValue.Del:
                    deletions.Add(change.Pos + offset);
                    ++offset;
                    break;
                default:
                    // Upstream's 'default: status = 0' (:20554): a change of no known kind is
                    // dropped rather than reported. Unreachable - record_fuzzy is only ever called
                    // with one of the three.
                    break;
            }
        }

        return new FuzzyChanges(substitutions, insertions, deletions);
    }

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
    /// <remarks>
    /// The search resumes inside the same slice this match was found in, and under the same
    /// <c>overlapped</c> setting, so walking a subject with <see cref="NextMatch"/> gives the same
    /// sequence as <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/> over it. A zero-width
    /// match is not allowed to repeat at the same position, which is upstream's
    /// <c>must_advance</c> - see <c>MatchState.AdvancePastMatch</c>, the one place that rule lives.
    /// <para>
    /// An unsuccessful match has no next one, so this returns another unsuccessful match rather
    /// than restarting the scan - which is what <c>Match.Empty.NextMatch()</c> does on the built-in
    /// <c>Regex</c>, and what upstream's scanner does once its status is a failure
    /// (<c>scanner_search_or_match</c>, <c>:20886</c>).
    /// </para>
    /// <para>
    /// <b>A per-call <c>timeout</c> on every input-dependent method</b> is this method's one
    /// exception: it takes neither a timeout nor a <see cref="CancellationToken"/> and instead
    /// runs under the pattern's own <see cref="FuzzyRegex.MatchTimeout"/>, exactly as
    /// <see cref="System.Text.RegularExpressions.Match.NextMatch"/> does.
    /// </para>
    /// </remarks>
    /// <returns>The next match, or an unsuccessful match if there is none.</returns>
    public Match NextMatch() =>
        Success
            ? Engine.Iteration.Next(
                _regex,
                _subject,
                _start,
                _end,
                _sliceStart,
                _sliceEnd,
                _overlapped,
                _oneUnitPerCharacter
            )
            : _regex.NoMatch(_subject);

    /// <summary>
    /// Expands a replacement template against this match, so <c>\1</c> and <c>\g&lt;name&gt;</c>
    /// become the text those groups captured. Upstream <c>Match.expand</c>; the built-in
    /// <c>Regex</c> calls it <c>Result</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Replacement templates speak upstream's language</b> (<c>\1</c>, <c>\g&lt;name&gt;</c>,
    /// <c>\n</c>, <c>\x41</c>, <c>\N{...}</c>), where <c>Regex</c> uses <c>$1</c>: the escape
    /// character is <c>\</c>, so those all mean what they mean upstream, and <c>$</c> is
    /// ordinary text.
    /// </para>
    /// <para>
    /// The two languages cannot both be honoured because they disagree about
    /// <c>\</c> - verified against both engines 2026-08-29: for the template <c>\n</c>,
    /// <c>regex.sub('.', r'\n', 'x')</c> gives a newline where
    /// <c>Regex.Replace("x", ".", @"\n")</c> gives a backslash followed by <c>n</c>.
    /// </para>
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
            expanded.Append(Engine.Substitution.GetMatchReplacement(item, this, _regex.GroupCount));
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

            return number <= _regex.GroupCount ? GroupAt(number) : null;
        }

        int named = _regex.GroupNumberFromName(argument);
        return named < 0 ? null : GroupAt(named);
    }
}
