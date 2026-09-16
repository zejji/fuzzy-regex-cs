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
    /// ponytail: one state per call, so walking a subject with <c>NextMatch</c> costs a vectorised
    /// pass over it per match where <c>Matches</c> costs one in total. That is the cliff DECISIONS
    /// 2026-09-01 describes, confined to the one entry point that cannot avoid it. Lift it by
    /// giving this surface a scanner object that owns a state, if a caller ever wants a lazy walk.
    /// </para>
    /// </remarks>
    /// <param name="regex">The pattern that produced the match.</param>
    /// <param name="input">The subject.</param>
    /// <param name="matchStart">Where the match started, in UTF-16 code units.</param>
    /// <param name="matchEnd">One past where it ended.</param>
    /// <param name="sliceStart">Where the slice it was found in starts.</param>
    /// <param name="sliceEnd">One past where that slice ends.</param>
    /// <param name="overlapped">Whether the scan it came from allowed matches to overlap.</param>
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
        bool overlapped
    ) =>
        Step(
            regex,
            input,
            sliceStart,
            sliceEnd,
            overlapped,
            partial: false,
            regex.PatternLimits,
            resumeAfter: (matchStart, matchEnd)
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
    /// <returns>The match, or an unsuccessful match if there is none.</returns>
    private static Match Step(
        FuzzyRegex regex,
        string input,
        int sliceStart,
        int sliceEnd,
        bool overlapped,
        bool partial,
        MatchLimits limits,
        (int Start, int End)? resumeAfter
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
            limits
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
    /// <returns>The matches, leftmost first.</returns>
    /// <remarks>
    /// <para>
    /// <c>ponytail:</c> <b>One <see cref="MatchState"/> per step, not one per walk</b> - the opposite of
    /// <see cref="Scan"/>, and deliberately. A state owns rented buffers, so a state held across a
    /// <c>yield return</c> is a state an abandoned iterator never returns; building one per step
    /// keeps every rental inside a <c>using</c> that has already run by the time the caller sees
    /// the match. The cost is the one DECISIONS 2026-09-01 measured for
    /// <see cref="Match.NextMatch"/>: a vectorised pass over the subject per match, where
    /// <see cref="Scan"/> pays one in total. See <c>docs/plan/OPTIMISATION-NOTES.md</c>.
    /// </para>
    /// <para>
    /// The same choice makes the time budget PER STEP rather than per walk, because a fresh state
    /// starts a fresh clock. <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// times the whole scan, as upstream's one state does; this times each match. The
    /// cancellation token behaves the same way either way.
    /// </para>
    /// </remarks>
    internal static IEnumerable<Match> Enumerate(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        bool partial,
        MatchLimits limits
    )
    {
        Match match = Step(regex, input, start, end, overlapped, partial, limits, resumeAfter: null);

        while (match.Success)
        {
            yield return match;

            // scanner_search_or_match ends the walk on the turn AFTER a partial (:20886), so a
            // partial is yielded and is always the last thing yielded - the rule Scan runs.
            if (match.PartialMatch)
            {
                yield break;
            }

            match = Step(
                regex,
                input,
                start,
                end,
                overlapped,
                partial,
                limits,
                resumeAfter: (match.Index, match.Index + match.Length)
            );
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
    internal static string?[] Split(FuzzyRegex regex, string input, int maxSplits, MatchLimits limits)
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

        List<string?> list = [];
        int splitCount = 0;
        int lastPos = state.Reverse ? state.TextLength : 0;

        while (splitCount < maxSplit)
        {
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
            list.Add(state.Reverse ? input[state.MatchPos..lastPos] : input[lastPos..state.MatchPos]);

            // Add groups (if any).
            for (int g = 1; g <= regex.GroupCount; g++)
            {
                list.Add(GetGroup(state, input, g));
            }

            splitCount++;
            lastPos = state.TextPos;

            // Upstream's line here (:22326) is the non-overlapped half of AdvancePastMatch, and a
            // splitter's state never has `overlapped` set, so this is the same rule the scanner runs.
            state.AdvancePastMatch();
        }

        // Get segment following last match (even if empty).
        list.Add(state.Reverse ? input[..lastPos] : input[lastPos..]);

        return [.. list];
    }

    /// <summary>
    /// The pieces <see cref="Split"/> returns, produced one at a time. Upstream's
    /// <c>splititer</c>, which is <c>pattern_split</c>'s loop behind a <c>Splitter_Type</c>
    /// instead of a list.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being split on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="maxSplits">The most splits to make, or a negative number for no limit.</param>
    /// <param name="limits">The time budget and cancellation token bounding each step.</param>
    /// <returns>The pieces, with <see langword="null"/> for a group that took no part in a match.</returns>
    /// <remarks>
    /// <para>
    /// Written over <see cref="Enumerate"/> rather than over a state of its own, for the reason
    /// <see cref="Enumerate"/> gives: a splitter holding a state across a <c>yield return</c> is a
    /// splitter whose rented buffers an abandoned <c>foreach</c> never returns. So this is
    /// <see cref="Split"/>'s loop with the state's <c>match_pos</c>/<c>text_pos</c> read off a
    /// finished <see cref="Match"/> instead, which is the same pair - <c>pattern_new_match</c>
    /// swaps the two for a reverse match (<c>:20795</c>) and this swaps them back.
    /// </para>
    /// <para>
    /// The loop is spelled out a second time rather than shared with <see cref="Split"/>, and that
    /// is the cost of the two state models sitting side by side. The test that stops them drifting
    /// is sequence equality over the oracle's own <c>split</c> rows, not the reader's eye.
    /// Phase 7 collapses the two if the per-step state goes.
    /// </para>
    /// </remarks>
    internal static IEnumerable<string?> EnumerateSplits(
        FuzzyRegex regex,
        string input,
        int maxSplits,
        MatchLimits limits
    )
    {
        // Upstream spells "no limit" as maxsplit=0 and reads a negative maxsplit as "no splits at
        // all"; this surface spells no limit as -1. Split does the same inversion, in the same
        // words, because it is the same rule and not a shared helper's worth of code.
        int maxSplit = maxSplits < 0 ? int.MaxValue : maxSplits;

        // Upstream takes no pos/endpos for split at all - its kwlist is string, maxsplit,
        // concurrent, timeout - so the slice is always the whole subject.
        bool reverse = (regex.PatternObject.Flags & Parsing.RegexFlags.Reverse) != 0;
        int lastPos = reverse ? input.Length : 0;
        int splitCount = 0;

        // An explicit enumerator rather than a foreach with a guard inside it: a foreach would
        // have pulled - and so matched - one more time before the count check could stop it, and
        // that extra engine step is observable through a timeout or a cancellation.
        using IEnumerator<Match> matches = Enumerate(
                regex,
                input,
                0,
                input.Length,
                overlapped: false,
                partial: false,
                limits
            )
            .GetEnumerator();

        while (splitCount < maxSplit && matches.MoveNext())
        {
            Match match = matches.Current;
            int matchStart = match.Index;
            int matchEnd = match.Index + match.Length;

            // Get segment before this match. A reverse split walks the subject backwards and
            // upstream does NOT reverse the list afterwards, unlike pattern_subx's join list:
            // verified 2026-09-01, regex.split('(?r)x', 'xaxbxc') is ['c', 'b', 'a', ''].
            yield return reverse ? input[matchEnd..lastPos] : input[lastPos..matchStart];

            // Add groups (if any).
            for (int g = 1; g <= regex.GroupCount; g++)
            {
                Group group = match.Groups[g];
                yield return group.Success ? group.Value : null;
            }

            splitCount++;
            lastPos = reverse ? matchStart : matchEnd;
        }

        // Get segment following last match (even if empty).
        yield return reverse ? input[..lastPos] : input[lastPos..];
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
