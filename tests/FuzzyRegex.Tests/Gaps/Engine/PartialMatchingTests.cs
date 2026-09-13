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
    public void A_skip_alternation_partial_starts_where_this_port_ran_out_of_text()
    {
        // The same `search_start` prefilter as the test above, in its SECOND symptom, which S37 found
        // at 6000 rows of the composed `interactions` wave - three rows, one at seed 4242 and two at
        // 20260912, every one of them with a `(*SKIP)` in it. Here both engines report a partial and
        // they report DIFFERENT ONES, so the row is not "upstream saw a partial and this port saw
        // nothing".
        //
        // Upstream's partial covers the whole subject, from the search start to the end of the text,
        // and its own `match` over that very span denies it. This port answers the zero-width partial
        // at the end, where `\w` ran out of text - which is upstream's own answer once it is asked at
        // that position instead. All measured against regex 2026.7.19 on 2026-09-12 and unchanged
        // against 2026.9.10, tools/probes/upstream-search-start-whole-region-partial.py:
        //
        //   pat = regex.compile(r'(?:\w{2,}(*SKIP)\w|\w)\B')
        //   pat.search('a.Aa', partial=True)          -> (0, 4), partial True
        //   pat.match('a.Aa', 0, 4, partial=True)     -> None
        //   pat.match('a.Aa', 4, partial=True)        -> (4, 4), partial True
        //
        // That the verb is what puts upstream on the prefilter's path is upstream's own statement
        // too: delete the `(*SKIP)` and its search answers a COMPLETE match at (2, 3); make it
        // `(*PRUNE)`, which moves no bound, and it answers this port's partial.
        //
        // PERMANENT, on the same reasoning as the test above: a Phase 7 slice that ports
        // `search_start` and turns this red has imported the prefilter's answers along with the
        // prefilter. Classified as `search-start-partial` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        Match partial = new FuzzyRegex(@"(?:\w{2,}(*SKIP)\w|\w)\B").Match("a.Aa", partial: true);

        partial.Success.Should().BeTrue();
        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((4, 0), "upstream answers (0, 4)");

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

    // PINS A KNOWN PORT DEFECT, deliberately, and the assertions below are the WRONG answers.
    [Test]
    public void A_skip_in_the_non_partial_pass_moves_the_slice_and_the_partial_pass_is_no_longer_leftmost()
    {
        // S40a, from row 97927 of a 6000-row seed-7 wave, minimised to four characters. Unlike every
        // other divergence in this file this one is NOT judged in this port's favour: the search is
        // not leftmost, and it contradicts this port's own matcher, which settles it without
        // upstream. Upstream happens to agree with the matcher.
        //
        //   search(r'\b\D(*SKIP)z', ' A', partial=True)      upstream (1, 1);  here (2, 0)
        //   this port's own MatchAtStart(' A', 1, partial)             (1, 1)
        //
        // The mechanism is written out at the `state.TextPos = textPos` line in Matcher.cs's
        // `DoMatch`: the non-partial pass runs first, its `(*SKIP)` moves `slice_start` to 2, and
        // the partial pass then re-runs from 1 with that slice still in force, so the search retry
        // jumps every start position below 2. Dropping any one of `\b`, `\D`, `(*SKIP)` or `partial`
        // makes the two engines agree.
        //
        // WHY IT IS PINNED RATHER THAN FIXED. Restoring the slice alongside `text_pos` fixes this
        // row and introduces another - a reversed partial search, where restoring `slice_end` moves
        // what every end-of-subject assertion means - and turns
        // `A_skip_alternation_partial_starts_where_this_port_ran_out_of_text` above red. That test
        // is the SAME defect seen from S37: it pins (4, 0) where this port's own matcher answers
        // (2, 2) at an earlier position, so S37's "port right" verdict on it needs re-judging too.
        // The three belong in one slice that can weigh them together; S40a measured them and left
        // the engine alone. THIS TEST GOES RED WHEN THAT SLICE LANDS, and that is the point of it.
        var skipped = new FuzzyRegex(@"\b\D(*SKIP)z");

        Match search = skipped.Match(" A", partial: true);
        search.PartialMatch.Should().BeTrue();
        (search.Index, search.Length).Should().Be((2, 0), "the wrong answer, pinned until S40b fixes it");

        // The half that makes the line above a defect rather than a divergence: this port's own
        // matcher finds the leftmost partial the search skipped, and that is upstream's answer too.
        Match anchored = skipped.MatchAtStart(" A", beginning: 1, partial: true);
        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((1, 1));

        // The control: with the verb gone nothing moves the slice, and the search is leftmost again.
        Match noVerb = new FuzzyRegex(@"\b\Dz").Match(" A", partial: true);
        noVerb.PartialMatch.Should().BeTrue();
        (noVerb.Index, noVerb.Length).Should().Be((1, 1));
    }
}
