using System.Buffers;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Walking a subject to produce more than one match: upstream's scanner, its <c>findall</c> and its
/// <c>split</c>.
/// </summary>
/// <remarks>
/// <para>
/// Port of three loops in <c>upstream/src/_regex.c</c>: <c>scanner_search_or_match</c>
/// (<c>:20874</c>), which drives <c>finditer</c> one match per call; <c>pattern_findall</c>
/// (<c>:22360</c>), which is that loop written out; and <c>pattern_split</c> (<c>:22235</c>), which
/// is it again with the text between the matches collected instead of the matches.
/// <see cref="Scan"/> is the <b>scanner</b>, not <c>findall</c>.
/// </para>
/// <para>
/// <b>That distinction is real, and S25 got it wrong.</b> Only <c>pattern_findall</c> carries the
/// <c>slice_start &lt;= text_pos &lt;= slice_end</c> loop guard (<c>:22415</c>); the scanner,
/// <c>pattern_subx</c> (<c>:21859</c>) and <c>pattern_split</c> (<c>:22285</c>) all rely on
/// <c>do_match</c>'s own "is there enough to search" test (<c>:18128</c>) instead, which compares
/// <c>text_pos</c> against <c>slice_end</c> going forward and against <c>slice_start</c> going back.
/// The two agree on every pattern that cannot move <c>slice_start</c> mid-scan, which in S25 was
/// every pattern there was - so the nine pairs it measured could not have separated them.
/// <c>(*SKIP)</c> moves it, and then they part. Measured against regex 2026.7.19 on 2026-09-11:
/// <c>regex.findall(r'[A-Z]*(*SKIP)_', '__BB__B', overlapped=True)</c> gives 3 matches and
/// <c>regex.finditer(...)</c> on the same arguments gives 6, because the third match's
/// <c>(*SKIP)</c> leaves <c>slice_start</c> at 4 while the overlapped step resumes at 3 - which
/// fails <c>findall</c>'s guard and is invisible to <c>do_match</c>'s. S29's oracle wave found it
/// as two diverging rows.
/// </para>
/// <para>
/// <b>One <see cref="MatchState"/> per operation, never one per match.</b> Upstream keeps one
/// across a whole scan and its <c>Scanner_Type</c> owns it, and since the 2026-09-01 quadratic fix
/// this is a performance cliff rather than a style point: building a state costs one vectorised
/// pass over the subject, and a surrogate pair costs a second pass to build
/// <see cref="CharacterIndex"/>. Shared across a scan both amortise to nothing; created per match
/// they make a find-all over a long subject quadratic again, with every correctness test still
/// green. DECISIONS 2026-09-01.
/// </para>
/// <para>
/// Upstream's <c>Scanner_Type</c> and <c>Splitter_Type</c> as public objects, and their
/// <c>copy</c>/<c>deepcopy</c> (<c>:20961-21014</c>, <c>:21264-21466</c>), are not ported: this
/// surface exposes the iteration through <c>Matches</c>/<c>Split</c> instead. See
/// <c>docs/PORTMAP.md</c>.
/// </para>
/// </remarks>
internal static class Iteration
{
    /// <summary>
    /// Every match in the given part of the subject, leftmost first. Upstream <c>finditer</c>.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Upstream's <c>partial</c>, which the scanner takes (<c>:21089</c>).</param>
    /// <param name="limits">The time budget and cancellation token bounding the whole scan.</param>
    /// <returns>The matches.</returns>
    internal static List<Match> FindAll(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        bool partial,
        MatchLimits limits
    )
    {
        List<Match> matches = [];

        // pattern_scanner (:21122): "The MatchObject, and therefore repeated captures, will be
        // visible." pattern_findall passes FALSE there because it never builds one; this always
        // does, so it is the scanner's argument that applies.
        Scan(
            regex,
            input,
            start,
            end,
            overlapped,
            partial,
            visibleCaptures: true,
            onMatch: (state, status) => matches.Add(regex.NewMatch(state, input, status)),
            limits
        );

        return matches;
    }

    /// <summary>
    /// How many matches the given part of the subject holds. No upstream counterpart - upstream
    /// spells this <c>len(finditer(...))</c> - so this is <see cref="Scan"/> with nothing built per
    /// match, and it counts what <see cref="FindAll"/> returns.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="limits">The time budget and cancellation token bounding the whole scan.</param>
    /// <returns>The number of matches.</returns>
    /// <remarks>
    /// No <c>partial</c> argument, deliberately: this counts what <c>findall</c> returns, and
    /// <c>findall</c> refuses one - <c>regex.findall('abc', 'xab', partial=True)</c> raises
    /// <c>ValueError: unused keyword argument 'partial'</c> (measured 2026-09-12, regex 2026.7.19).
    /// </remarks>
    internal static int Count(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        MatchLimits limits
    ) => Scan(regex, input, start, end, overlapped, partial: false, visibleCaptures: false, onMatch: null, limits);

    /// <summary>
    /// The scan itself: <c>scanner_search_or_match</c> (<c>upstream/src/_regex.c</c> line 20874)
    /// driven to exhaustion, with <paramref name="onMatch"/> standing in for what the caller of
    /// <c>finditer</c> does with each match.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether to report a trailing partial match.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="onMatch">Called once per match, with the state holding it and its status.</param>
    /// <param name="limits">The time budget and cancellation token bounding the whole scan.</param>
    /// <returns>How many matches there were.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The scan as a whole ran out of time. Upstream times the scan, not each match, and one state
    /// carries one start time, so this is the same budget.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller's token was cancelled.</exception>
    private static int Scan(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        Action<MatchState, int>? onMatch,
        MatchLimits limits
    )
    {
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            start,
            end,
            overlapped,
            partial: partial,
            visibleCaptures: visibleCaptures,
            matchAll: false,
            limits
        );

        int count = 0;

        // No loop condition, because the scanner has none: `do_match` returns FAILURE once there is
        // nothing left to search, and that is what ends the walk. See the remarks on this class for
        // why this is not `pattern_findall`'s guard.
        while (true)
        {
            int status = Matcher.DoMatch(state, search: true);
            if (status == MatchStatus.Cancelled)
            {
                throw limits.Cancelled(input, regex.Pattern);
            }

            // scanner_search_or_match builds a match for PARTIAL exactly as it does for SUCCESS
            // (:20898) and only ends the walk on the NEXT turn, where it reads the status it kept
            // (:20886). So a partial is yielded, and it is always the last thing yielded.
            if (status is not (MatchStatus.Success or MatchStatus.Partial))
            {
                break;
            }

            count++;
            onMatch?.Invoke(state, status);

            if (status == MatchStatus.Partial)
            {
                break;
            }

            // The overlapped step is one CODEPOINT from where the match started, because upstream
            // indexes the subject by codepoint. Measured 2026-09-01: regex.finditer('..',
            // '\U0001F600\U0001F601\U0001F602', overlapped=True) gives spans (0, 2) and (1, 3) in
            // codepoints, so the second match starts on the second emoji rather than inside the
            // first one's surrogate pair. A step off either end of the slice is caught by
            // `do_match` on the next turn, exactly as it is upstream.
            state.AdvancePastMatch();
        }

        return count;
    }

    /// <summary>
    /// The match after the one given, which is one turn of <c>scanner_search_or_match</c>
    /// (<c>upstream/src/_regex.c</c> line 20874) on a state rebuilt from where that match ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream has no <c>Match.next</c>; a scanner object holds its own state and this port does
    /// not expose one, so a fresh state is built per call. That is why the caller has to hand over
    /// the <b>slice</b> the match was found in and not just where it ended: <c>pos</c> moves
    /// <c>slice_start</c>, and a lookbehind or a <c>\b</c> at the resumption point reads the
    /// subject before it. Measured 2026-09-01: <c>regex.finditer(r'(?&lt;=a)b', 'abab')</c> gives
    /// (1, 2) and (3, 4), and both survive <c>pos=1</c> because <c>text_start</c> stays at 0 - but
    /// <c>regex.compile('^b').finditer('abab', 1)</c> gives nothing, so a slice really is narrower
    /// than the subject and cannot be guessed.
    /// </para>
    /// <para>
    /// A state per call used to cost a vectorised pass over the whole subject per match, looking
    /// for a surrogate pair, which made a <c>NextMatch</c> walk to the end quadratic (DECISIONS
    /// 2026-09-01; S58 measured the pass at 33.6 microseconds a step on 1 MB). Since S61 the match
    /// carries the answer and <paramref name="oneUnitPerCharacter"/> hands it on, so a step costs
    /// what a state costs and no more. The <see cref="CharacterIndex"/> a subject with a surrogate
    /// pair needs is still rebuilt per step, and only if the pattern asks for it.
    /// </para>
    /// </remarks>
    /// <param name="regex">The pattern that produced the match.</param>
    /// <param name="input">The subject.</param>
    /// <param name="matchStart">Where the match started, in UTF-16 code units.</param>
    /// <param name="matchEnd">One past where it ended.</param>
    /// <param name="sliceStart">Where the slice it was found in starts.</param>
    /// <param name="sliceEnd">One past where that slice ends.</param>
    /// <param name="overlapped">Whether the scan it came from allowed matches to overlap.</param>
    /// <param name="oneUnitPerCharacter">
    /// What the state that found the match knew about the subject; see
    /// <see cref="MatchState.Create"/>.
    /// </param>
    /// <returns>The next match, or an unsuccessful match if there is none.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The search ran out of time, against the PATTERN's budget. This is the one matching
    /// operation that takes no per-call timeout and no token, because it takes no arguments at all
    /// - the built-in <c>Regex</c>'s <c>Match.NextMatch</c> is in exactly the same position and
    /// likewise runs under the pattern's <c>MatchTimeout</c>. A caller who needs to bound or cancel
    /// a walk should use <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// or <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>,
    /// which take both (S51, S53b).
    /// </exception>
    internal static Match Next(
        FuzzyRegex regex,
        string input,
        int matchStart,
        int matchEnd,
        int sliceStart,
        int sliceEnd,
        bool overlapped,
        bool oneUnitPerCharacter
    ) =>
        Step(
            regex,
            input,
            sliceStart,
            sliceEnd,
            overlapped,
            partial: false,
            regex.PatternLimits,
            resumeAfter: (matchStart, matchEnd),
            oneUnitPerCharacter
        );

    /// <summary>
    /// One turn of <c>scanner_search_or_match</c> on a state of its own: the first match in the
    /// slice when <paramref name="resumeAfter"/> is <see langword="null"/>, and the match after
    /// that one when it is not.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="sliceStart">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="sliceEnd">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether a trailing partial match may be reported.</param>
    /// <param name="limits">The time budget and cancellation token bounding this step.</param>
    /// <param name="resumeAfter">
    /// The previous match's (start, end) in the subject, or <see langword="null"/> to start the
    /// walk.
    /// </param>
    /// <param name="oneUnitPerCharacter">Passed to <see cref="MatchState.Create"/>.</param>
    /// <returns>The match, or an unsuccessful match if there is none.</returns>
    private static Match Step(
        FuzzyRegex regex,
        string input,
        int sliceStart,
        int sliceEnd,
        bool overlapped,
        bool partial,
        MatchLimits limits,
        (int Start, int End)? resumeAfter,
        bool? oneUnitPerCharacter
    )
    {
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            sliceStart,
            sliceEnd,
            overlapped,
            partial: partial,
            // The Match object, and therefore repeated captures, will be visible.
            visibleCaptures: true,
            matchAll: false,
            limits,
            oneUnitPerCharacter: oneUnitPerCharacter
        );

        if (resumeAfter is { } previous)
        {
            // Put the state back where the given match left it. A reverse match reports its two
            // ends the other way round (pattern_new_match, :20795), so this is that mapping
            // inverted.
            state.MatchPos = state.Reverse ? previous.End : previous.Start;
            state.TextPos = state.Reverse ? previous.Start : previous.End;
            state.AdvancePastMatch();
        }

        // No guard here either: this is one turn of the scanner, and `do_match` answers FAILURE when
        // the step landed outside what is left to search.
        int status = Matcher.DoMatch(state, search: true);
        if (status == MatchStatus.Cancelled)
        {
            throw limits.Cancelled(input, regex.Pattern);
        }

        return regex.NewMatch(state, input, status);
    }

    /// <summary>
    /// Every match in the given part of the subject, produced one at a time. The lazy twin of
    /// <see cref="FindAll"/>, and what upstream's <c>finditer</c> is - a scanner the caller pulls
    /// from rather than a list.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether the walk may end with a partial match.</param>
    /// <param name="limits">The time budget and cancellation token bounding each step.</param>
    /// <param name="pool">Where the state's stacks rent from; see <see cref="MatchState.Create"/>.</param>
    /// <returns>The matches, leftmost first.</returns>
    /// <remarks>
    /// <para>
    /// <b>One <see cref="MatchState"/> for the whole walk</b>, exactly as <see cref="Scan"/> and
    /// upstream's <c>Scanner_Type</c> keep one. S61 moved it here from one state per step, which
    /// cost a vectorised pass over the subject per match and made a walk to the end quadratic (S54
    /// measured 12,643 ms for a 1 MB walk that <see cref="FindAll"/> does in 111 ms). The state's
    /// rented buffers are held across each <c>yield return</c> and handed back by the <c>using</c>,
    /// which runs when the walk ends and also when the caller's <c>foreach</c> breaks, because
    /// breaking disposes the enumerator. <c>PoolDisciplineTests</c> proves both halves against a
    /// pool that counts every rental.
    /// </para>
    /// <para>
    /// <b>The time budget is still per step</b>, which is why the clock is restarted before each
    /// match. A walk's time between two steps belongs to the caller, who may do anything with a
    /// match before asking for the next, so a clock that ran across a <c>yield return</c> would
    /// time out a walk for work it did not do. The built-in <c>Regex</c> draws the same line: its
    /// lazy walks time each match (<c>tools/probes/bcl-lazy-walk-timeout.cs</c>). <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// times the whole scan, as upstream's one state does. The cancellation token behaves the same
    /// way either way.
    /// </para>
    /// </remarks>
    internal static IEnumerable<Match> Enumerate(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        bool partial,
        MatchLimits limits,
        ArrayPool<byte>? pool = null
    )
    {
        // pattern_scanner (:21122): "The MatchObject, and therefore repeated captures, will be
        // visible."
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            start,
            end,
            overlapped,
            partial: partial,
            visibleCaptures: true,
            matchAll: false,
            limits,
            pool
        );

        // Scan's loop, with the match yielded where Scan calls back.
        while (true)
        {
            state.RestartClock();

            int status = Matcher.DoMatch(state, search: true);
            if (status == MatchStatus.Cancelled)
            {
                throw limits.Cancelled(input, regex.Pattern);
            }

            if (status is not (MatchStatus.Success or MatchStatus.Partial))
            {
                yield break;
            }

            yield return regex.NewMatch(state, input, status);

            // scanner_search_or_match ends the walk on the turn AFTER a partial (:20886), so a
            // partial is yielded and is always the last thing yielded - the rule Scan runs.
            if (status == MatchStatus.Partial)
            {
                yield break;
            }

            state.AdvancePastMatch();
        }
    }

    /// <summary>
    /// Splits the subject around the matches. Port of <c>pattern_split</c>
    /// (<c>upstream/src/_regex.c</c> line 22235) less its argument parsing.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being split on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="maxSplits">The most splits to make, or a negative number for no limit.</param>
    /// <param name="limits">The time budget and cancellation token bounding the whole split.</param>
    /// <returns>The pieces, with <see langword="null"/> for a group that took no part in a match.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The split ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller's token was cancelled.</exception>
    /// <remarks>
    /// <see cref="EnumerateSplits"/> drained, with the clock left running for the whole split, as
    /// upstream's one state runs it. S61 made the two one loop: until then the lazy one walked a
    /// state per step and could not share this one's.
    /// </remarks>
    internal static string?[] Split(FuzzyRegex regex, string input, int maxSplits, MatchLimits limits) =>
        [.. EnumerateSplits(regex, input, maxSplits, limits, timeEachStep: false)];

    /// <summary>
    /// The pieces <see cref="Split"/> returns, produced one at a time. Upstream's
    /// <c>splititer</c>, which is <c>pattern_split</c>'s loop behind a <c>Splitter_Type</c>
    /// instead of a list.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being split on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="maxSplits">The most splits to make, or a negative number for no limit.</param>
    /// <param name="limits">The time budget and cancellation token bounding each step.</param>
    /// <param name="timeEachStep">
    /// Whether the clock restarts before each match, which is what a lazy walk wants (see
    /// <see cref="Enumerate"/>), or runs across the whole split, which is what <see cref="Split"/>
    /// wants.
    /// </param>
    /// <returns>The pieces, with <see langword="null"/> for a group that took no part in a match.</returns>
    /// <remarks>
    /// One state for the whole walk, held across each <c>yield return</c> and released by the
    /// <c>using</c> when the walk ends or the caller's <c>foreach</c> breaks - the same shape, for
    /// the same reasons, as <see cref="Enumerate"/>.
    /// </remarks>
    internal static IEnumerable<string?> EnumerateSplits(
        FuzzyRegex regex,
        string input,
        int maxSplits,
        MatchLimits limits,
        bool timeEachStep = true
    )
    {
        // Upstream spells "no limit" as maxsplit=0 and reads a negative maxsplit as "no splits at
        // all" (regex.split(',', 'a,b,c', maxsplit=-1) is ['a,b,c'], measured 2026-09-01). This
        // surface spells no limit as -1, as S01 decided, so the two are swapped here - the same
        // inversion pattern_subx's `count` needed, and at both ends.
        int maxSplit = maxSplits < 0 ? int.MaxValue : maxSplits;

        // Upstream takes no pos/endpos for split at all - its kwlist is string, maxsplit,
        // concurrent, timeout - so the slice is always the whole subject.
        //
        // "The MatchObject, and therefore repeated captures, will not be visible."
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            0,
            input.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: false,
            limits
        );

        int splitCount = 0;
        int lastPos = state.Reverse ? state.TextLength : 0;

        // The count is tested before the engine runs, not after: one more match than the caller
        // asked for is observable through a timeout or a cancellation.
        while (splitCount < maxSplit)
        {
            if (timeEachStep)
            {
                state.RestartClock();
            }

            int status = Matcher.DoMatch(state, search: true);
            if (status == MatchStatus.Cancelled)
            {
                throw limits.Cancelled(input, regex.Pattern);
            }

            if (status != MatchStatus.Success)
            {
                // No more matches.
                break;
            }

            // Get segment before this match. A reverse split walks the subject backwards and
            // upstream does NOT reverse the list afterwards, unlike pattern_subx's join list:
            // verified 2026-09-01, regex.split('(?r)x', 'xaxbxc') is ['c', 'b', 'a', ''].
            yield return state.Reverse ? input[state.MatchPos..lastPos] : input[lastPos..state.MatchPos];

            // Add groups (if any).
            for (int g = 1; g <= regex.GroupCount; g++)
            {
                yield return GetGroup(state, input, g);
            }

            splitCount++;
            lastPos = state.TextPos;

            // Upstream's line here (:22326) is the non-overlapped half of AdvancePastMatch, and a
            // splitter's state never has `overlapped` set, so this is the same rule the scanner runs.
            state.AdvancePastMatch();
        }

        // Get segment following last match (even if empty).
        yield return state.Reverse ? input[..lastPos] : input[lastPos..];
    }

    /// <summary>
    /// Upstream <c>state_get_group</c> (<c>upstream/src/_regex.c</c> line 20818) with
    /// <c>empty</c> false: the text one group captured, or <see langword="null"/> if it took no
    /// part in the match.
    /// </summary>
    /// <param name="state">The state holding the match.</param>
    /// <param name="input">The subject.</param>
    /// <param name="index">The public group number, one-based.</param>
    /// <returns>The captured text, or <see langword="null"/>.</returns>
    private static string? GetGroup(MatchState state, string input, int index)
    {
        GroupData group = state.Groups[index - 1];
        if (group.Current < 0)
        {
            return null;
        }

        GroupSpan span = group.Captures[group.Current];
        return input[span.Start..span.End];
    }

    // The local Timeout helper this file used to carry is gone: a cancelled scan can now mean the
    // clock OR the caller's token, and deciding between them lives on MatchLimits.Cancelled, which
    // is the one place that holds both.
}
