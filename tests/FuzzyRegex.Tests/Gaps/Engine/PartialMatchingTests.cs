using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// What <c>partial: true</c> does that the ported suite does not pin: which end of the subject the
/// partial sits at, that the slice and not the subject bounds it, what the groups of a partial match
/// report, that a complete match always wins the fallback, and upstream issue 367.
/// </summary>
/// <remarks>
/// Every expected value here was measured against <c>regex</c> 2026.7.19 on 2026-09-12 and is quoted
/// beside the assertion. None of it is derivable from upstream's C by reading: <c>do_match</c> forces
/// <c>text_pos</c> to one end of the slice for a partial (<c>upstream/src/_regex.c:18175-18180</c>),
/// but which end, and what <c>match_pos</c> then holds, is the engine's answer rather than that
/// line's.
/// </remarks>
public sealed class PartialMatchingTests
{
    [Test]
    public void A_partial_match_keeps_the_groups_that_had_already_closed()
    {
        // regex.compile('(a)(b)(c)').match('ab', partial=True)
        //   -> span (0,2), partial True, groups [(0,1), (1,2), (-1,-1)], lastindex 2
        Match m = new FuzzyRegex("(a)(b)(c)").MatchAtStart("ab", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 2));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 1));
        (m.Groups[2].Index, m.Groups[2].Length).Should().Be((1, 1));
        m.Groups[3].Success.Should().BeFalse();
        m.LastGroupNumber.Should().Be(2);

        // The same pattern one character shorter: only group 1 closed.
        //   -> span (0,1), partial True, groups [(0,1), (-1,-1), (-1,-1)], lastindex 1
        Match shorter = new FuzzyRegex("(a)(b)(c)").MatchAtStart("a", partial: true);

        shorter.PartialMatch.Should().BeTrue();
        (shorter.Index, shorter.Index + shorter.Length).Should().Be((0, 1));
        shorter.Groups[2].Success.Should().BeFalse();
        shorter.LastGroupNumber.Should().Be(1);

        // An alternation that took its second branch reports that branch's group, not the first.
        //   regex.compile('(a)|(b)c').match('b', partial=True)
        //     -> span (0,1), partial True, groups [(-1,-1), (0,1)], lastindex 2
        Match branch = new FuzzyRegex("(a)|(b)c").MatchAtStart("b", partial: true);

        branch.PartialMatch.Should().BeTrue();
        branch.Groups[1].Success.Should().BeFalse();
        (branch.Groups[2].Index, branch.Groups[2].Length).Should().Be((0, 1));
        branch.LastGroupNumber.Should().Be(2);
    }

    [Test]
    public void A_repetition_that_could_legally_stop_reports_the_complete_match_and_its_last_capture()
    {
        // regex.compile('(ab)+').match('aba', partial=True)
        //   -> span (0,2), partial FALSE, groups [(0,2)], lastindex 1, captures ['ab']
        // The trailing 'a' is not a partial match, because the repetition had already reached a
        // point where it could stop - the normal match succeeds, so the fallback never runs.
        Match m = new FuzzyRegex("(ab)+").MatchAtStart("aba", partial: true);

        m.PartialMatch.Should().BeFalse();
        (m.Index, m.Index + m.Length).Should().Be((0, 2));
        m.Groups[1].Captures.Select(static c => c.Value).Should().Equal("ab");
    }

    [Test]
    public void The_slice_and_not_the_subject_bounds_a_partial_match()
    {
        // do_match sets text_pos to slice_end for a forward partial (:18179), so narrowing the
        // slice shortens the partial and the engine never reads the text beyond it.
        //   compile('abc').match('abcd', pos=0, endpos=2) -> (0,2) partial True
        //   compile('abc').match('abcd', pos=0, endpos=4) -> (0,3) partial False
        //   compile('abc').match('xabcd', pos=1, endpos=3) -> (1,3) partial True
        Match clipped = new FuzzyRegex("abc").MatchAtStart("abcd", beginning: 0, length: 2, partial: true);

        clipped.PartialMatch.Should().BeTrue();
        (clipped.Index, clipped.Index + clipped.Length).Should().Be((0, 2));

        Match whole = new FuzzyRegex("abc").MatchAtStart("abcd", beginning: 0, length: 4, partial: true);

        whole.PartialMatch.Should().BeFalse();
        (whole.Index, whole.Index + whole.Length).Should().Be((0, 3));

        Match offset = new FuzzyRegex("abc").MatchAtStart("xabcd", beginning: 1, length: 2, partial: true);

        offset.PartialMatch.Should().BeTrue();
        (offset.Index, offset.Index + offset.Length).Should().Be((1, 3));
    }

    [Test]
    public void A_reverse_partial_match_sits_at_the_left_end_of_the_slice()
    {
        // state_init sets partial_side to LEFT when the pattern is reversed (MatchState.cs:381),
        // and do_match forces text_pos to slice_START rather than slice_end (:18177).
        //   compile('(?r)abc').match('bc', partial=True)   -> (0,2) partial True
        //   compile('(?r)abc').search('xbc', partial=True) -> (0,0) partial True
        //   compile('(?r)abc').search('abcd', pos=0, endpos=2, partial=True) -> (0,0) partial True
        Match m = new FuzzyRegex("(?r)abc").MatchAtStart("bc", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 2));

        // 'xbc' cannot be extended on the left into 'abc' - the 'x' is in the way - so the partial
        // is the zero-width one at the start of the slice, not (1, 3).
        Match blocked = new FuzzyRegex("(?r)abc").Match("xbc", partial: true);

        blocked.PartialMatch.Should().BeTrue();
        (blocked.Index, blocked.Index + blocked.Length).Should().Be((0, 0));

        Match clipped = new FuzzyRegex("(?r)abc").Match("abcd", beginning: 0, length: 2, partial: true);

        clipped.PartialMatch.Should().BeTrue();
        (clipped.Index, clipped.Index + clipped.Length).Should().Be((0, 0));

        // A reverse partial still reports the groups that closed, and reports them in the reverse
        // walk's order: compile('(?r)(a)(b)').search('b', partial=True)
        //   -> (0,1) partial True, groups [(-1,-1), (0,1)], lastindex 2
        Match grouped = new FuzzyRegex("(?r)(a)(b)").Match("b", partial: true);

        grouped.PartialMatch.Should().BeTrue();
        (grouped.Index, grouped.Index + grouped.Length).Should().Be((0, 1));
        grouped.Groups[1].Success.Should().BeFalse();
        (grouped.Groups[2].Index, grouped.Groups[2].Length).Should().Be((0, 1));
        grouped.LastGroupNumber.Should().Be(2);
    }

    [Test]
    public void A_complete_match_always_beats_a_longer_partial_one()
    {
        // do_match runs a normal match FIRST with partial_side forced to none and only falls back
        // when that fails (:18140-18162). So an alternation whose short branch already matches is
        // never reported as partial, however much more text the long branch would take.
        //   compile('ab|abcd').match('ab', partial=True)    -> (0,2) partial False
        //   compile('ab|abcd').match('abc', partial=True)   -> (0,2) partial False
        //   compile('ab|abcd').fullmatch('ab', partial=True) -> (0,2) partial False
        var pattern = new FuzzyRegex("ab|abcd");

        pattern.MatchAtStart("ab", partial: true).PartialMatch.Should().BeFalse();
        pattern.MatchAtStart("abc", partial: true).PartialMatch.Should().BeFalse();
        pattern.FullMatch("ab", partial: true).PartialMatch.Should().BeFalse();
        (pattern.MatchAtStart("abc", partial: true).Index, pattern.MatchAtStart("abc", partial: true).Length)
            .Should()
            .Be((0, 2));
    }

    [Test]
    public void Search_reports_the_partial_where_the_candidate_started_not_at_position_zero()
    {
        // compile('ab\\w+').search('xxab', partial=True) -> (2,4) partial True, where match() and
        // fullmatch() on 'ab' both give (0,2) partial True. The partial's START is match_pos, which
        // the search left where the candidate began; only its END is forced to the slice edge.
        var pattern = new FuzzyRegex(@"ab\w+");

        Match searched = pattern.Match("xxab", partial: true);

        searched.PartialMatch.Should().BeTrue();
        (searched.Index, searched.Index + searched.Length).Should().Be((2, 4));

        pattern.MatchAtStart("ab", partial: true).Index.Should().Be(0);
        pattern.FullMatch("ab", partial: true).PartialMatch.Should().BeTrue();
    }

    [Test]
    public void Jointly_unsatisfiable_lookaheads_still_report_a_partial_match_as_upstream_does()
    {
        // Upstream issue 367 ("second even prime"): no continuation of the subject can satisfy both
        // lookaheads, so a partial match here is a true positive that can never become a real one.
        // Ported faithfully and pinned, not fixed - the Phase 6 upstream sweep owns it
        // (docs/plan/2026-08-31-upstream-issue-triage.md, row 367).
        //
        // Measured 2026-09-12, .scratch/probe-issue367.py:
        //   compile('(?=ab)(?=cd)').match('a', partial=True)  -> (0,1) partial True
        //   compile('(?=ab)(?=cd)').match('', partial=True)   -> (0,0) partial True
        //   compile('(?=ab)(?=cd)').match('a')                -> None
        //   compile(r'(?=\d*[02468]$)(?=\d*[13579]$)\d+').match('1234', partial=True) -> (0,4) True
        var impossible = new FuzzyRegex("(?=ab)(?=cd)");

        Match m = impossible.MatchAtStart("a", partial: true);
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 1));

        Match empty = impossible.MatchAtStart("", partial: true);
        empty.PartialMatch.Should().BeTrue();
        (empty.Index, empty.Index + empty.Length).Should().Be((0, 0));

        // Without asking for a partial there is no match at all, which is the correct answer.
        impossible.MatchAtStart("a").Success.Should().BeFalse();

        Match evenPrime = new FuzzyRegex(@"(?=\d*[02468]$)(?=\d*[13579]$)\d+").MatchAtStart("1234", partial: true);
        evenPrime.PartialMatch.Should().BeTrue();
        (evenPrime.Index, evenPrime.Index + evenPrime.Length).Should().Be((0, 4));
    }

    [Test]
    public void A_partial_raised_inside_a_lazy_repeat_leaves_the_enclosing_group_as_it_was()
    {
        // The S31 oracle wave's first finding, seed 31, rows 214 and 518, minimised. A lazy repeat
        // inside a capture group runs out of text while extending: upstream stops inside the repeat
        // and the group keeps whatever it had, where this port entered the tail, re-closed the group
        // and reported it as the whole match. The fix is in Matcher.TryMatch - see its remarks.
        //
        // Measured 2026-09-12, .scratch/min-214.py:
        //   compile(r'(\D*?)z').search('a', partial=True)   -> (0,1) partial True, group 1 (-1,-1)
        //   compile(r'(?r)z(a*?)').search('a', partial=True) -> (0,1) partial True, group 1 (-1,-1)
        //   compile(r'(?r)x(a??)b').search('b', partial=True) -> (0,1) partial True, group 1 (0,0)
        Match lazy = new FuzzyRegex(@"(\D*?)z").Match("a", partial: true);

        lazy.PartialMatch.Should().BeTrue();
        (lazy.Index, lazy.Index + lazy.Length).Should().Be((0, 1));
        lazy.Groups[1].Success.Should().BeFalse();
        lazy.LastGroupNumber.Should().Be(-1);

        Match reversed = new FuzzyRegex(@"(?r)z(a*?)").Match("a", partial: true);

        reversed.PartialMatch.Should().BeTrue();
        reversed.Groups[1].Success.Should().BeFalse();

        // The lazy quantifier that takes zero FIRST does close its group, and keeps it: the partial
        // arrives after the group closed rather than while the repeat was extending.
        Match zeroFirst = new FuzzyRegex(@"(?r)x(a??)b").Match("b", partial: true);

        zeroFirst.PartialMatch.Should().BeTrue();
        (zeroFirst.Groups[1].Index, zeroFirst.Groups[1].Length).Should().Be((0, 0));
    }

    [Test]
    public void A_lazy_repeat_that_cannot_extend_at_all_is_still_a_partial_match()
    {
        // The wave's second finding, seed 7, row 581, minimised. '[^a-f]' matches the first four
        // characters of '__AAb' and refuses the 'b', so the repeat cannot reach the six the tail
        // would need. Upstream answers a partial anyway, because its specialised tail arms ask
        // partial_side BEFORE trying to extend; this port asked MatchOne first, was refused, and
        // reported no match at all. The fix is Matcher.IsTailPartial - see its remarks.
        //
        // Measured 2026-09-12, .scratch/bisect7.jsonl through tools/run-oracle.ps1:
        //   compile(r'([^a-f]{3,}?)x').match('__AAb', partial=True) -> (0,5) partial True, 1 unset
        //   compile(r'([^a-f]{3,}?)_').match('__AAb', partial=True) -> (0,5) partial True, 1 unset
        //   compile(r'(.{3,}?)x').match('abcde', partial=True)      -> (0,5) partial True, 1 unset
        foreach (string pattern in new[] { @"([^a-f]{3,}?)x", @"([^a-f]{3,}?)_", @"([^a-f]{3,}?)_\1" })
        {
            Match m = new FuzzyRegex(pattern).MatchAtStart("__AAb", partial: true);

            m.PartialMatch.Should().BeTrue(pattern);
            (m.Index, m.Index + m.Length).Should().Be((0, 5), pattern);
            m.Groups[1].Success.Should().BeFalse(pattern);
        }
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_bounded_lazy_repeat_that_reaches_its_maximum_loses_its_partial_here()
    {
        // Raised by the S31 blind review and left for a slice of its own, after S31 tried a fix and
        // measured it as a net loss. The bug is real: 'a??' steps to its maximum of one, the tail
        // 'x' fails against the second 'a', the subject has run out, and upstream answers a partial
        // where this port answers none.
        //
        //   compile('ba??x').match('baa', partial=True)        -> (0,3) partial True; ours: None
        //   compile('ba{0,2}?x').match('baaa', partial=True)   -> (0,4) partial True; ours: None
        //   compile('(?r)xa??b').match('aab', partial=True)    -> (0,3) partial True; ours: None
        //   compile('(?r)x(a??)b').search('aab', partial=True) -> (0,3) partial True; ours: (0,0)
        //
        // WHY IT IS NOT FIXED HERE, recorded so the next attempt does not repeat it. The obvious
        // repair - ask IsTailPartial once more before the loop's 'pos == limit' break, where
        // upstream's specialised arms return to the top and ask partial_side before their own limit
        // check (:16545-16549) - fixes these four and breaks a larger set, because those arms also
        // CAP the limit per tail op ('min(limit, slice_end - 1)' for a CHARACTER tail, :16543) and
        // so never reach the position the added guard fires at. Measured by the second blind pass
        // over 20,160 targeted rows: 125 rows fixed, 219 introduced, among them
        //
        //   compile('.{0,2}?x').match('baa', partial=True)  -> None upstream, (0,3) partial with it
        //   compile('a??x').search('baa', partial=True)     -> (2,3) upstream, (1,3) with it
        //
        // So this cannot be repaired by adding guards to the default arm: it needs upstream's
        // specialised LAZY_REPEAT_ONE arms, which are the Phase 7 optimisation the S31 slice file
        // holds out of scope. See docs/PORTMAP.md's row for that sub-switch and DECISIONS
        // 2026-09-12.
        //
        // PERMANENT, decided 2026-09-12: the port is right and UPSTREAM HAS A BUG, so when those arms
        // land this test does NOT invert - a Phase 7 slice that turns it red has ported the bug with
        // the optimisation. See docs/plan/2026-09-12-divergence-research.md. Two things settle it.
        // First the definition, which upstream's own README gives: a partial match is one "that
        // matches up to the end of string, but that string has been truncated and you want to know
        // whether a complete match could be possible if the string had not been truncated" - and
        // 'baa' cannot be extended into a match of 'ba??x', because 'a??' is capped at one. Second
        // the second engine: PCRE2 10.47, called directly (tools/probes/pcre2-partial-and-skip.py,
        // 2026-09-12), answers NO MATCH to 'ba??x' against 'baa', soft and hard, anchored and
        // unanchored. Upstream's own greedy twin 'ba?x' agrees with both of them.
        foreach (
            (string pattern, string subject) in new[] { ("ba??x", "baa"), ("ba{0,2}?x", "baaa"), ("(?r)xa??b", "aab") }
        )
        {
            new FuzzyRegex(pattern)
                .MatchAtStart(subject, partial: true)
                .Success.Should()
                .BeFalse($"upstream answers a partial covering all of {subject} for {pattern}");
        }

        // The reverse form is the "wrong end" case: a zero-width partial at (0, 0) where upstream
        // covers the whole subject.
        Match searched = new FuzzyRegex("(?r)x(a??)b").Match("aab", partial: true);

        searched.PartialMatch.Should().BeTrue();
        (searched.Index, searched.Index + searched.Length).Should().Be((0, 0), "upstream answers (0, 3)");

        // The scan inherits it: regex.compile('ba??x').finditer('abab', partial=True) is
        // [((1, 4), True)] upstream.
        new FuzzyRegex("ba??x")
            .Matches("abab", partial: true)
            .Select(static m => (m.Index, m.Index + m.Length, m.PartialMatch))
            .Should()
            .Equal((3, 4, true));

        // S40c's row, minimised, and it is here rather than in its own test because it is this shape
        // with a class in place of the literal. Row 98191 of a 6000-row `partial` wave at seed
        // 20260913 arrived looking like S40c's own family - a group call inside an
        // opposite-direction lookaround - and is not: upstream answers the SAME (0, 2) partial with
        // the call written out as its body, with the piece holding the call deleted, and with the
        // leading `\K` removed, so none of those is the cause. What is left is `([^a-f]??)`.
        //
        //   search(r'^([^a-f]??)([\ ])$', ' \r', V1|M|I, partial=True) -> (0, 2) partial; ours: None
        //
        // It passes this family's own discriminator: spell the bounded repeat GREEDY, `([^a-f]?)`,
        // and upstream drops the partial too - as does removing the group altogether. Measured
        // 2026-09-13 on regex 2026.7.19.
        var narrowed = new FuzzyRegex(
            @"^([^a-f]??)([\ ])$",
            FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        );

        narrowed.Match(" \r", partial: true).Success.Should().BeFalse("upstream answers (0, 2) partial");

        // The greedy twin, which is the control: both engines answer nothing.
        new FuzzyRegex(
            @"^([^a-f]?)([\ ])$",
            FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        )
            .Match(" \r", partial: true)
            .Success.Should()
            .BeFalse();
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_finds_no_partial_here()
    {
        // The wave's third finding, and the only one S31 did not fix: it is upstream's `search_start`
        // prefilter (upstream/src/_regex.c:8385), which this port does not implement and Phase 7
        // owns. Every `search_start_*` arm can report RE_ERROR_PARTIAL of its own (:8662-:8960); the
        // slow path this port runs has no such arm, and upstream's slow path does not either - which
        // is why upstream's own two doors disagree at the same position:
        //
        //   compile(r'(?r)\b$').search('', partial=True)    -> (0, 0), partial True
        //   compile(r'(?r)\b$').match('', partial=True)     -> None
        //   compile(r'(?r)\b$').fullmatch('', partial=True) -> None
        //
        // Measured 2026-09-12, .scratch/probe-searchstart2.py. The patterns `(?r)\m$`, `(?r)a\b$`
        // and `(?r)([abz]{1})\b$` all behave the same way, and `(?r)$` - no boundary, so no such
        // start test - agrees on all three doors. Same mechanism as S29's four `verbs` rows: see
        // the Generator note in tools/run-oracle.ps1, and DECISIONS 2026-09-12.
        //
        // PERMANENT, decided 2026-09-12: the port is right, upstream is internally inconsistent, and
        // this test does NOT invert when `search_start` lands - a Phase 7 slice that turns it red has
        // ported the bug. See docs/plan/2026-09-12-divergence-research.md. The reasoning is the
        // maintainer's own, from upstream issue 589: `\b` is evaluated against the real string, and
        // the empty string contains no word character, so there is no boundary at position 0 to be
        // partial about. PCRE2 10.47 answers a partial here, but on a different rule of its own -
        // `pcre2partial`'s "the next pattern item must be one that inspects a character" test, which
        // upstream deliberately does not share (README's `\d{4}` example, upstream issue 469) - so it
        // is not a second opinion on the same question. What is decisive is that upstream's own
        // `match` and `fullmatch` answer None to this row, and only the door that consults
        // `search_start` answers otherwise.
        //
        // The `partial` and `partial-sliced` generators ARE on the default oracle list from S33, with
        // these rows classified as `search-start-partial` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        var pattern = new FuzzyRegex(@"(?r)\b$");

        pattern.Match("", partial: true).Success.Should().BeFalse("search_start is not ported");
        pattern.MatchAtStart("", partial: true).Success.Should().BeFalse("upstream agrees here");
        pattern.FullMatch("", partial: true).Success.Should().BeFalse("upstream agrees here");

        // Without the boundary there is no start test for the prefilter to run, and all three doors
        // agree with upstream on a complete zero-width match.
        Match anchored = new FuzzyRegex("(?r)$").Match("", partial: true);

        anchored.Success.Should().BeTrue();
        anchored.PartialMatch.Should().BeFalse();
        (anchored.Index, anchored.Length).Should().Be((0, 0));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_skip_alternation_partial_starts_at_the_leftmost_position_that_matches()
    {
        // S37 found this at 6000 rows of the composed `interactions` wave - three rows, one at seed
        // 4242 and two at 20260912, every one with a `(*SKIP)` - and judged it upstream's
        // `search_start` prefilter alone, pinning this port's answer of (4, 0). S40b RE-JUDGED IT and
        // flipped the assertion, because half of it was this port's own defect and S40a said so:
        // the answer (4, 0) is the one the carried slice left reachable, and this port's own matcher
        // finds a partial at 2. Both engines report a partial here and they report DIFFERENT ONES,
        // so the row was never "upstream saw a partial and this port saw nothing".
        //
        // What changed is only this port's side. With the slice restored before the partial retry
        // (Matcher.cs `DoMatch`) the search no longer skips positions 2 and 3, and it now answers
        // the leftmost partial that exists - which is upstream's OWN answer at that position:
        //
        //   pat = regex.compile(r'(?:\w{2,}(*SKIP)\w|\w)\B')
        //   pat.search('a.Aa', partial=True)          -> (0, 4), partial True
        //   pat.match('a.Aa', 0, 4, partial=True)     -> None      <- upstream denies its own answer
        //   pat.match('a.Aa', 2, partial=True)        -> (2, 4), partial True   <- and this is ours
        //   pat.match('a.Aa', 4, partial=True)        -> (4, 4), partial True
        //
        // So the residual divergence is upstream's prefilter and nothing else: its partial arms set
        // `new_position->text_pos` to the end of the slice (`:8471`, `:8487`) while the match start
        // stays where the SEARCH began, giving a span its own `match` refuses. Measured against
        // regex 2026.7.19 on 2026-09-12, unchanged against 2026.9.10, re-measured 2026-09-13 by
        // tools/probes/upstream-search-start-whole-region-partial.py and
        // tools/probes/upstream-partial-retry-slice-restore.py.
        //
        // That the verb is what puts upstream on the prefilter's path is upstream's own statement
        // too: delete the `(*SKIP)` and its search answers a COMPLETE match at (2, 3); make it
        // `(*PRUNE)`, which moves no bound, and it answers (2, 4) - this port's answer.
        //
        // PERMANENT: a Phase 7 slice that ports `search_start` and turns this red has imported the
        // prefilter's answers along with the prefilter. Classified as `search-start-partial` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        var skipped = new FuzzyRegex(@"(?:\w{2,}(*SKIP)\w|\w)\B");
        Match partial = skipped.Match("a.Aa", partial: true);

        partial.Success.Should().BeTrue();
        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((2, 2), "upstream answers (0, 4)");

        // The leftmost check that S40b's fix is what this test now rests on: the search's answer is
        // the first position at which this port's own anchored matcher answers anything at all.
        foreach (int beginning in new[] { 0, 1 })
        {
            skipped
                .MatchAtStart("a.Aa", beginning, partial: true)
                .Success.Should()
                .BeFalse($"nothing matches at {beginning}, so the search owes its answer at 2");
        }

        Match anchored = skipped.MatchAtStart("a.Aa", beginning: 2, partial: true);
        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((2, 2), "upstream's own match(pos=2) answers this");

        // Without the verb there is a complete match, and both engines find it - which is what says
        // the divergence above belongs to the verb and the prefilter rather than to `\B`.
        Match complete = new FuzzyRegex(@"(?:\w{2,}\w|\w)\B").Match("a.Aa", partial: true);

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((2, 1));
    }

    [Test]
    public void A_reverse_partial_at_the_left_edge_of_a_narrowed_slice_is_found()
    {
        // Raised by the S31 blind review, diagnosed and fixed in S33. Half of upstream's partial arms
        // are bounded by slice_start/slice_end and half by text_start/text_end - compare
        // try_match_STRING (upstream/src/_regex.c:7396, slice_end) with the STRING opcode's own arm
        // in basic_match (:14730, text_end) - and the two agree exactly as long as the slice IS the
        // whole subject. At the top level slice_end IS text_end (do_match sets both to endpos,
        // :18435), so only the reversed half can part company, and it does the moment pos > 0.
        // `Matcher.TryMatch` consulted only the one-character tests, so the slice_start family was
        // never asked here; S33 ported the six try_match_STRING* arms (:7383-:7668).
        //
        // Measured against regex 2026.7.19 on 2026-09-12, .scratch/up-s33-expected.py:
        //   compile(r'(?r)a(bc)*').match('abc', 1, 1, partial=True) -> ((1, 1), partial)
        //   compile(r'(?r)a(bc)*').match('abc', 2, 2, partial=True) -> ((2, 2), partial)
        //   compile(r'(?r)ab|abcd').match('ab', 1, 2, partial=True) -> ((1, 2), partial)
        //   compile(r'(?r)a(bc)*').finditer('abab', 1, 4, partial=True) -> [((2,3), False), ((1,1), True)]
        foreach (int beginning in new[] { 1, 2 })
        {
            Match empty = new FuzzyRegex("(?r)a(bc)*").MatchAtStart("abc", beginning, length: 0, partial: true);

            empty.PartialMatch.Should().BeTrue($"upstream answers a partial at ({beginning}, {beginning})");
            (empty.Index, empty.Length).Should().Be((beginning, 0));
        }

        Match branch = new FuzzyRegex("(?r)ab|abcd").MatchAtStart("ab", beginning: 1, length: 1, partial: true);

        branch.PartialMatch.Should().BeTrue();
        (branch.Index, branch.Index + branch.Length).Should().Be((1, 2));

        // The scan inherits it: the complete match first, then the partial at the left edge.
        new FuzzyRegex("(?r)a(bc)*")
            .Matches("abab", beginning: 1, length: 3, partial: true)
            .Select(static m => (m.Index, m.Length, m.PartialMatch))
            .Should()
            .Equal((2, 1, false), (1, 0, true));

        // Unnarrowed, the same pattern agreed with upstream before the fix and still does.
        new FuzzyRegex("(?r)ab|abcd")
            .MatchAtStart("b", partial: true)
            .PartialMatch.Should()
            .BeTrue();
    }

    [Test]
    public void The_narrowed_slice_partial_is_a_per_opcode_answer_and_not_a_general_rule()
    {
        // The trap in the S33 fix, pinned so nobody "simplifies" it into one rule about slice_start.
        // Upstream's CHARACTER_REV opcode arm (:12190) and its try_match_CHARACTER_REV (:7137) both
        // bound by text_start, so a single reversed character at the left edge of a narrowed slice is
        // NO match; only the STRING family's try_match arms bound by slice_start. Measured
        // 2026-09-12, .scratch/up-rev-partial.py and .scratch/up-rev-partial2.py:
        //
        //   compile(r'(?r)a').match('abc', 1, 1, partial=True)      -> None
        //   compile(r'(?r)a').match('abc', 0, 0, partial=True)      -> ((0, 0), partial)
        //   compile(r'(?r)ab*').match('abc', 1, 1, partial=True)    -> None   (REPEAT_ONE, not STRING)
        //   compile(r'(?r)a(b)*').match('abc', 1, 1, partial=True)  -> None   (one-character body)
        //   compile(r'(?r)a(bc)+').match('abc', 1, 1, partial=True) -> None   (min 1, tail never tried)
        //   compile(r'(?r)qz|qzzz').match('qz', 1, 2, partial=True) -> None   (common suffix 'z' is
        //                                                   factored out, so the test is CHARACTER_REV)
        foreach (string pattern in new[] { "(?r)a", "(?r)ab*", "(?r)a(b)*", "(?r)a(bc)+" })
        {
            new FuzzyRegex(pattern)
                .MatchAtStart("abc", beginning: 1, length: 0, partial: true)
                .Success.Should()
                .BeFalse($"upstream answers None for {pattern} on abc[1:1]");
        }

        new FuzzyRegex("(?r)qz|qzzz")
            .MatchAtStart("qz", beginning: 1, length: 1, partial: true)
            .Success.Should()
            .BeFalse("the branches share the suffix 'z', so the test node is CHARACTER_REV");

        // At pos 0 the slice starts where the subject does, and the character arm answers a partial.
        Match atZero = new FuzzyRegex("(?r)a").MatchAtStart("abc", beginning: 0, length: 0, partial: true);

        atZero.PartialMatch.Should().BeTrue();
        (atZero.Index, atZero.Length).Should().Be((0, 0));
    }

    [Test]
    public void The_width_early_out_that_skips_the_non_partial_pass_counts_characters_not_code_units()
    {
        // S40c. A partial request is answered in TWO passes: `do_match` runs a NON-PARTIAL pass
        // first and only falls back to a partial one if that FAILS (`upstream/src/_regex.c:18142`,
        // and `Matcher.DoMatch`). `do_exact_match` (`:18064`) opens with a width early-out - fewer
        // characters available than `min_width`, fail without matching at all - and that early-out
        // is guarded by `partial_side == RE_PARTIAL_NONE`, so it fires on the FIRST pass only.
        //
        // So on a subject too narrow for `min_width` the non-partial pass never runs, the partial
        // pass does, and the answer is a PARTIAL of a span the first pass would have called
        // complete. That is upstream's design, not an accident: the early-out is the ONLY thing
        // standing between the two passes.
        //
        // THE DEFECT THIS PINS. `available` was a UTF-16 code-unit subtraction, and `min_width` is a
        // CHARACTER count. One astral character is two code units, so `available` read 2 where
        // upstream reads 1, the early-out did not fire, the non-partial pass ran and SUCCEEDED, and
        // the partial retry upstream performs never happened. The comment that stood here claimed
        // the mismatch was safe because "the engine simply does the work and fails in the dispatch
        // loop instead - the same answer". It is not the same answer: a pass that SUCCEEDS is not a
        // pass that fails, and the fallback is what it suppresses.
        //
        // WHAT MAKES A ONE-CHARACTER SUBJECT TOO NARROW AT ALL is a separate upstream oddity, and it
        // is what the family that found this is made of: `min_width` counts a group CALL at the
        // width of the group it calls even inside a LOOKAROUND, which is zero-width. So
        // `(?P<g1>A)(?:(?<=(?P>g1))\w)?` has min_width 2 where the same lookbehind written out has
        // 1. Not fixed here - upstream's answer is reproduced, and the divergence was ours.
        string astral = char.ConvertFromUtf32(0x10400);

        // regex.search(r'(?P<g1>\U00010400)(?:(?<=(?P>g1))\w)?', '\U00010400', partial=True)
        //   -> span (0, 1) codepoints, g1 (0, 1), partial True. Two code units here.
        Match forward = new FuzzyRegex("(?P<g1>" + astral + @")(?:(?<=(?P>g1))\w)?").Match(astral, partial: true);

        forward.Success.Should().BeTrue();
        (forward.Index, forward.Length).Should().Be((0, 2));
        forward.PartialMatch.Should().BeTrue("one character is available and min_width is 2");
        (forward.Groups["g1"].Index, forward.Groups["g1"].Length).Should().Be((0, 2));

        // regex.fullmatch(r'(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?', '\U00010400', partial=True)
        //   -> span (0, 1) codepoints, g1 UNSET, partial True.
        Match reversed = new FuzzyRegex(@"(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?").FullMatch(astral, partial: true);

        reversed.Success.Should().BeTrue();
        (reversed.Index, reversed.Length).Should().Be((0, 2));
        reversed.PartialMatch.Should().BeTrue("the same early-out, through a lookahead under (?r)");
        reversed.Groups["g1"].Success.Should().BeFalse("upstream drops g1 when the partial pass answers");

        // THE THRESHOLD MOVES WITH THE CALLEE'S WIDTH, which is what says the early-out is the
        // mechanism rather than the call. Every row below keeps the tail past the end of the subject
        // - a `\w*` prefix soaks up the spare characters - so the ONLY thing that varies is how many
        // characters the early-out counts. All measured on regex 2026.7.19 and re-run unchanged on
        // 2026.9.10: `python tools/probes/upstream-min-width-partial-retry.py`.
        //
        //   \w*(?P<g1>A)(?:(?<=(?P>g1))\w)?     'A'       partial   min_width 2, 1 available
        //   \w*(?P<g1>A)(?:(?<=(?P>g1))\w)?     'BA'      complete  min_width 2, 2 available
        //   \w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?   'ABC'     partial   min_width 6, 3 available
        //   \w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?   'XXABC'   partial   min_width 6, 5 available
        //   \w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?   'XXXABC'  complete  min_width 6, 6 available
        //   \w*(?P<g1>A)(?:(?<=A)\w)?           'A'       complete  min_width 1, never fires
        //   \w*(?P<g1>ABC)(?:(?<=ABC)\w)?       'ABC'     complete  min_width 3, never fires
        var narrow = new FuzzyRegex(@"\w*(?P<g1>A)(?:(?<=(?P>g1))\w)?");
        narrow.Match("A", partial: true).PartialMatch.Should().BeTrue();
        narrow.Match("BA", partial: true).PartialMatch.Should().BeFalse();

        var wide = new FuzzyRegex(@"\w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?");
        wide.Match("ABC", partial: true).PartialMatch.Should().BeTrue();
        wide.Match("XXABC", partial: true).PartialMatch.Should().BeTrue();
        wide.Match("XXXABC", partial: true).PartialMatch.Should().BeFalse();

        // The same lookarounds written out, where min_width is the body's own width and the
        // early-out never fires. Both engines answer a complete match.
        new FuzzyRegex(@"\w*(?P<g1>A)(?:(?<=A)\w)?")
            .Match("A", partial: true)
            .PartialMatch.Should()
            .BeFalse();
        new FuzzyRegex(@"\w*(?P<g1>ABC)(?:(?<=ABC)\w)?").Match("ABC", partial: true).PartialMatch.Should().BeFalse();

        // And the astral pair from the probe, which is the row that used to answer by character
        // width: one astral character is too narrow, one astral character behind a spare one is not.
        var astralNarrow = new FuzzyRegex(@"\w*(?P<g1>" + astral + @")(?:(?<=(?P>g1))\w)?");
        astralNarrow.Match(astral, partial: true).PartialMatch.Should().BeTrue();
        astralNarrow.Match("B" + astral, partial: true).PartialMatch.Should().BeFalse();
    }

    [Test]
    public void A_skip_in_the_non_partial_pass_does_not_move_the_slice_the_partial_pass_searches()
    {
        // S40a found this and pinned the WRONG answer deliberately; S40b fixed it and flipped the
        // assertions. From row 97927 of a 6000-row seed-7 wave, minimised to four characters.
        //
        // A `partial` search runs two passes over one match attempt (upstream do_match, `:18160`):
        // a non-partial one, then - only if that failed - a partial one from the same `text_pos`.
        // Upstream restores `text_pos` and nothing else, so the `(*SKIP)`'s move of `slice_start`
        // (`:14553`) to 2 was still in force for the second pass and its search retry jumped every
        // start position below 2. This port now restores both slice bounds with `text_pos`.
        //
        //   search(r'\b\D(*SKIP)z', ' A', partial=True)      upstream (1, 1);  here (1, 1) since S40b
        //   this port's own MatchAtStart(' A', 1, partial)             (1, 1)
        //
        // What settled it is the self-refutation rather than upstream: a search that reports a
        // position its OWN anchored matcher beats is not leftmost. Upstream happens to agree here,
        // because its `search_start` prefilter reaches this shape. On the reversed row below it does
        // not, and there upstream keeps the defect while this port no longer has it.
        // tools/probes/upstream-partial-retry-slice-restore.py, regex 2026.7.19, 2026-09-13.
        var skipped = new FuzzyRegex(@"\b\D(*SKIP)z");

        Match search = skipped.Match(" A", partial: true);
        search.PartialMatch.Should().BeTrue();
        (search.Index, search.Length).Should().Be((1, 1), "upstream answers (1, 1) too");

        // The half that made it a defect rather than a divergence, and the check that keeps it one:
        // the search's answer is the leftmost its own matcher can find.
        Match anchored = skipped.MatchAtStart(" A", beginning: 1, partial: true);
        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((1, 1));

        // The control: with the verb gone nothing moves the slice, and the answer is unchanged -
        // which is what says the fix removed the verb's leftover reach and nothing else.
        Match noVerb = new FuzzyRegex(@"\b\Dz").Match(" A", partial: true);
        noVerb.PartialMatch.Should().BeTrue();
        (noVerb.Index, noVerb.Length).Should().Be((1, 1));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reversed_skip_does_not_move_the_slice_end_the_partial_pass_searches()
    {
        // The other end of the fix above, and the row S40a recorded as the reason NOT to make it -
        // "restoring `slice_end` moves what every end-of-subject assertion means". S40b measured the
        // row instead of inheriting that reading, and it says the opposite: UPSTREAM HAS THIS DEFECT
        // TOO, in reverse, where its `search_start` prefilter does not mask it.
        //
        // Under `(?r)` the verb moves `slice_end` rather than `slice_start` (`:14551`), and a
        // reversed search is anchored by its END, so it tries endpos 2, then 1, then 0. The answer a
        // leftmost-equivalent reversed search owes is the first of those that matches - endpos 1.
        // Measured on regex 2026.7.19, 2026-09-13,
        // tools/probes/upstream-partial-retry-slice-restore.py:
        //
        //   search(partial=True)                    (0, 0) partial   <- upstream, and this port before S40b
        //   match(endpos=1, partial=True)           (0, 1) partial   <- upstream's own matcher
        //   match(endpos=0, partial=True)           (0, 0) partial
        //   verb deleted,     search(partial=True)  (0, 1) partial
        //   verb -> (*PRUNE), search(partial=True)  (0, 1) partial
        //
        // The last two lines are what make it the verb's bound move and not the pattern's meaning:
        // `(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, and it gives
        // the endpos-1 answer. So upstream's own matcher and upstream's own verb-free search both
        // name (0, 1), and only upstream's search with the verb answers (0, 0).
        //
        // PERMANENT, and judged in this port's favour: this port answers what upstream's matcher
        // answers. Classified as `partial-retry-reversed-slice` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs, which holds THREE rows of this shape
        // from the gate (`tools/run-oracle.ps1 -Count 6000`, 126,000 rows a seed) - this one at seed
        // 7 row 101560, and two at seed 20260913. The row indices only mean anything against that
        // exact command; the entry itself is keyed on each row's question, not on its index.
        // The gate row carries MULTILINE (flags 0x8) and this test does not, because neither `^` nor
        // `$` appears and the flag changes no answer here; the entry holds the row verbatim.
        var skipped = new FuzzyRegex(@"(?r)\b(?:[^a-f](*SKIP)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})");

        Match search = skipped.Match("a\n", partial: true);
        search.PartialMatch.Should().BeTrue();
        (search.Index, search.Length).Should().Be((0, 1), "upstream answers (0, 0), skipping endpos 1");

        // The self-refutation, at the bound that actually moves a reversed anchor.
        Match anchored = skipped.MatchAtStart("a\n", beginning: 0, length: 1, partial: true);
        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((0, 1));

        // The control, matching the probe's `(*PRUNE)` line: a verb that moves no bound was never
        // affected, and both engines answer the same thing before and after S40b.
        Match pruned = new FuzzyRegex(@"(?r)\b(?:[^a-f](*PRUNE)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})").Match(
            "a\n",
            partial: true
        );
        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 1));
    }

    [Test]
    public void A_forward_skip_does_not_move_the_slice_start_the_partial_pass_searches()
    {
        // The LEFT-TO-RIGHT half of the test above, found by S43's seed-99991 `fuzzy,interactions`
        // wave (row 6897 of `tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions
        // -Seeds 99991`). Same two passes, same unrestored bound, different cost - which is why the
        // two have separate `ExpectedDivergences` entries rather than one, the way
        // `overlapped-skip-stale-slice` and its `-reversed` twin already do.
        //
        // Forwards the verb moves `slice_start` rather than `slice_end` (`:14551`). That does not
        // hide an anchor here: BOTH engines answer a partial at the same span, codepoints (1, 4).
        // What the moved bound costs is which ALTERNATIVE the partial pass can still enter, and so
        // which error is spent and which group captures. Measured on regex 2026.7.19, 2026-09-13,
        // tools/probes/upstream-skip-carried-slice-forward.py:
        //
        //   as the wave drew it       (1,4) partial, insertion at 3, no captures   <- upstream
        //   first verb -> (*PRUNE)    (1,4) partial, substitution at 1, (3,4)      <- this port
        //   first verb deleted        (1,4) partial, substitution at 1, (3,4)      <- this port
        //   no partial asked for      None on all three                            <- both engines
        //
        // `(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, so the bound
        // move is the cause and not the pattern's meaning. The last line is the other half of that:
        // with no partial there is no second pass to carry a bound into, and the divergence is gone.
        //
        // The pattern here is the wave's row with its trailing alternation cut to `\W`, which is the
        // one cut that held - every further shrink tried changed both engines' answers together and
        // lost the divergence, so this is as small as it minimises. The cut also removed the row's
        // SECOND verb, a `(*PRUNE)`, and the answers did not move: the divergence is the first
        // verb's, not "a verb somewhere in the pattern".
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // `partial-retry-carried-slice-forward` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs. Spans below are UTF-16, because the
        // subject opens with an astral character; the probe's are codepoints.
        const string pattern = @"\b(?:(?:\ _(\W)){e<=1}(*SKIP)[A-Z]|[^a])(?:.?(?:(\w+?)){i<=1:.}){e<=2,s<=1:[^a-z]}\W";
        const string subject = "\U0001F600ß_ ";

        Match skipped = new FuzzyRegex(pattern, FuzzyRegexOptions.Multiline).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length).Should().Be((2, 3), "both engines agree on the span");
        skipped
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0), "upstream spends an insertion here, having lost the branch");
        skipped.FuzzyChanges.Substitutions.Should().Equal(2);
        (skipped.Groups[1].Index, skipped.Groups[1].Length)
            .Should()
            .Be((4, 1), "upstream leaves group 1 unset, never reaching the branch that fills it");

        // The control, matching the probe's `(*PRUNE)` line: a verb that moves no bound leaves both
        // engines saying what this port says with the `(*SKIP)`.
        Match pruned = new FuzzyRegex(
            pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal),
            FuzzyRegexOptions.Multiline
        ).Match(subject, partial: true);

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((2, 3));
        pruned.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        (pruned.Groups[1].Index, pruned.Groups[1].Length).Should().Be((4, 1));

        // And the other half: with no partial there is no second pass, so no bound is carried and
        // both engines answer nothing.
        new FuzzyRegex(pattern, FuzzyRegexOptions.Multiline)
            .Match(subject)
            .Success.Should()
            .BeFalse();
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer - which is also upstream
    // 2026.7.19's answer and PCRE2 10.47's.
    [Test]
    public void A_skip_does_not_block_the_repeat_retreat_a_partial_needs()
    {
        // S44, the Phase 6 sync, and the only row of 378,000 whose answer the sync changed: row
        // 98050 of the 6000-row seed-20260913 wave, minimised by hand from
        // `\b([İ]+)\1(?:.{3}?(*SKIP)[^[\p{L}--[a-z]]]|\S)` over 'İİSsS' to three ASCII characters.
        //
        //   regex.compile(r'(a+)\1x(*SKIP)b').search('aax', partial=True)
        //   2026.7.19  -> (0, 3) partial, group 1 == (0, 1)      <- this port's answer
        //   2026.9.10  -> (3, 3) partial, group 1 unset
        //
        // A REGRESSION UPSTREAM INTRODUCED, and it is issue 613's own fix doing it. Commit b77694a
        // clamps the GREEDY_REPEAT_ONE retreat limit down to the current position, which stops the
        // runaway retreat it was written for and also the single legitimate retreat step this match
        // needs once the `(*SKIP)` has moved `slice_start` above the repeat. This port carries both
        // clamps too - S44 ported them - and keeps the match because S40b restores the slice bounds
        // before the partial pass, where upstream does not.
        //
        // The match is plainly reachable: `(a+)` takes 'aa', `\1` cannot match 'aa' at 2, the repeat
        // retreats to 'a', `\1` matches 'a' at 1, 'x' matches at 2, and 'b' runs off the end of the
        // subject - which is exactly what a partial match is.
        //
        // Measured 2026-09-13, tools/probes/upstream-skip-blocks-a-repeat-retreat.py. Ledger entry
        // 15; oracle entry `skip-blocks-a-repeat-retreat-partial`. NOT filed - owner's rule.
        Match partial = new FuzzyRegex(@"(a+)\1x(*SKIP)b").Match("aax", partial: true);

        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((0, 3));
        (partial.Groups[1].Index, partial.Groups[1].Length).Should().Be((0, 1));

        // Control 1: `(*PRUNE)` prunes the same backtracking and moves no bound. BOTH upstream
        // releases answer (0, 3) here, so the moved bound is the cause rather than the pattern's
        // meaning - the argument every entry in this family rests on.
        Match pruned = new FuzzyRegex(@"(a+)\1x(*PRUNE)b").Match("aax", partial: true);

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 3));
        (pruned.Groups[1].Index, pruned.Groups[1].Length).Should().Be((0, 1));

        // Control 2: no verb at all. Both releases answer (0, 3).
        Match plain = new FuzzyRegex(@"(a+)\1xb").Match("aax", partial: true);

        (plain.Index, plain.Length).Should().Be((0, 3));
        (plain.Groups[1].Index, plain.Groups[1].Length).Should().Be((0, 1));

        // Control 3, and the one that decides it: UPSTREAM 2026.9.10 CONTRADICTS ITSELF. Give the
        // same pattern the 'b' it was waiting for and upstream answers the COMPLETE (0, 4) with
        // group 1 at (0, 1) - which needs the identical `a+` retreat it just refused. An engine
        // that takes the retreat to finish a match cannot consistently refuse it to report a
        // partial one. PCRE2 10.47 answers MATCH (0, 4) (0, 1) here and PARTIAL (0, 3) above.
        Match complete = new FuzzyRegex(@"(a+)\1x(*SKIP)b").Match("aaxb", partial: true);

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 4));
        (complete.Groups[1].Index, complete.Groups[1].Length).Should().Be((0, 1));

        // Control 4: a subject that cannot complete. Both releases and this port answer the
        // zero-width partial at the end, so the divergence is not "any (*SKIP) partial row".
        Match unreachable = new FuzzyRegex(@"(a+)\1x(*SKIP)b").Match("aaxyz", partial: true);

        unreachable.PartialMatch.Should().BeTrue();
        (unreachable.Index, unreachable.Length).Should().Be((5, 0));
    }
}
