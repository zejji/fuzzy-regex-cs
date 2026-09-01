namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Walking a subject to produce more than one match: upstream's scanner, its <c>findall</c> and its
/// <c>split</c>.
/// </summary>
/// <remarks>
/// <para>
/// Port of three loops in <c>upstream/src/_regex.c</c> that say the same thing three times:
/// <c>scanner_search_or_match</c> (<c>:20874</c>), which drives <c>finditer</c> one match per call;
/// <c>pattern_findall</c> (<c>:22360</c>), which is that loop written out; and
/// <c>pattern_split</c> (<c>:22235</c>), which is it again with the text between the matches
/// collected instead of the matches. <see cref="Scan"/> is the first two, because they are
/// measurably the same function - verified 2026-09-01, <c>findall</c> and <c>finditer</c> agree
/// match for match on nine subject/pattern pairs including the overlapped, zero-width and reverse
/// cases where only <c>findall</c> carries the <c>slice_start &lt;= text_pos &lt;= slice_end</c>
/// guard.
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
    /// <returns>The matches.</returns>
    internal static List<Match> FindAll(FuzzyRegex regex, string input, int start, int end, bool overlapped)
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
            visibleCaptures: true,
            onMatch: state => matches.Add(regex.NewMatch(state, input, MatchStatus.Success))
        );

        return matches;
    }

    /// <summary>
    /// How many matches the given part of the subject holds. No upstream counterpart - upstream
    /// spells this <c>len(findall(...))</c> - so this is <see cref="Scan"/> with nothing built per
    /// match.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <returns>The number of matches.</returns>
    internal static int Count(FuzzyRegex regex, string input, int start, int end, bool overlapped) =>
        Scan(regex, input, start, end, overlapped, visibleCaptures: false, onMatch: null);

    /// <summary>
    /// The scan itself: <c>pattern_findall</c> (<c>upstream/src/_regex.c</c> line 22360) less its
    /// argument parsing and its per-match tuple building, which is what
    /// <paramref name="onMatch"/> stands in for.
    /// </summary>
    /// <param name="regex">The pattern being scanned with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="onMatch">Called once per match, with the state holding it.</param>
    /// <returns>How many matches there were.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The scan as a whole ran out of time. Upstream times the scan, not each match, and one state
    /// carries one start time, so this is the same budget.
    /// </exception>
    private static int Scan(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        bool overlapped,
        bool visibleCaptures,
        Action<MatchState>? onMatch
    )
    {
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            start,
            end,
            overlapped,
            partial: false,
            visibleCaptures: visibleCaptures,
            matchAll: false,
            timeout: regex.TimeoutTicks
        );

        int count = 0;

        while (state.IsInSlice())
        {
            int status = Matcher.DoMatch(state, search: true);
            if (status == MatchStatus.Cancelled)
            {
                throw Timeout(regex, input);
            }

            if (status != MatchStatus.Success)
            {
                break;
            }

            count++;
            onMatch?.Invoke(state);

            // The overlapped step is one CODEPOINT from where the match started, because upstream
            // indexes the subject by codepoint. Measured 2026-09-01: regex.finditer('..',
            // '\U0001F600\U0001F601\U0001F602', overlapped=True) gives spans (0, 2) and (1, 3) in
            // codepoints, so the second match starts on the second emoji rather than inside the
            // first one's surrogate pair. A step off either end of the slice fails the loop
            // condition above, exactly as it does upstream.
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
    /// The search ran out of time.
    /// </exception>
    internal static Match Next(
        FuzzyRegex regex,
        string input,
        int matchStart,
        int matchEnd,
        int sliceStart,
        int sliceEnd,
        bool overlapped
    )
    {
        using var state = MatchState.Create(
            regex.PatternObject,
            input,
            sliceStart,
            sliceEnd,
            overlapped,
            partial: false,
            // The Match object, and therefore repeated captures, will be visible.
            visibleCaptures: true,
            matchAll: false,
            timeout: regex.TimeoutTicks
        );

        // Put the state back where the given match left it. A reverse match reports its two ends
        // the other way round (pattern_new_match, :20795), so this is that mapping inverted.
        state.MatchPos = state.Reverse ? matchEnd : matchStart;
        state.TextPos = state.Reverse ? matchStart : matchEnd;
        state.AdvancePastMatch();

        if (!state.IsInSlice())
        {
            return regex.NoMatch(input);
        }

        int status = Matcher.DoMatch(state, search: true);
        if (status == MatchStatus.Cancelled)
        {
            throw Timeout(regex, input);
        }

        return regex.NewMatch(state, input, status);
    }

    /// <summary>
    /// Splits the subject around the matches. Port of <c>pattern_split</c>
    /// (<c>upstream/src/_regex.c</c> line 22235) less its argument parsing.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being split on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="maxSplits">The most splits to make, or a negative number for no limit.</param>
    /// <returns>The pieces, with <see langword="null"/> for a group that took no part in a match.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The split ran out of time.
    /// </exception>
    internal static string?[] Split(FuzzyRegex regex, string input, int maxSplits)
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
            timeout: regex.TimeoutTicks
        );

        List<string?> list = [];
        int splitCount = 0;
        int lastPos = state.Reverse ? state.TextLength : 0;

        while (splitCount < maxSplit)
        {
            int status = Matcher.DoMatch(state, search: true);
            if (status == MatchStatus.Cancelled)
            {
                throw Timeout(regex, input);
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

    /// <summary>The exception a cancelled scan reports, which for this port only ever means a timeout.</summary>
    /// <param name="regex">The pattern being run.</param>
    /// <param name="input">The subject.</param>
    /// <returns>The exception to throw.</returns>
    private static System.Text.RegularExpressions.RegexMatchTimeoutException Timeout(FuzzyRegex regex, string input) =>
        new(input, regex.Pattern, regex.MatchTimeout);
}
