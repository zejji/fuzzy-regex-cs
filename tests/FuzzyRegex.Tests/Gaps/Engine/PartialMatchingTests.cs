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
        //
        // S49's sweep judged the issue on 2026-09-14 and DISMISSED IT, so this pin is now permanent
        // rather than provisional: PCRE2 10.47 answers PARTIAL on the identical rows, including the
        // reporter's own `(?!.+).*` over '1', so reporting a partial no continuation can complete is
        // what every engine does (tools/probes/pcre2-partial-truncation-assertions.py). Deciding it
        // in general is not possible - the issue's own example encodes primality. What is wrong is
        // upstream's documented promise, "whether a complete match could be possible if the string
        // had not been truncated" (upstream/docs/Features.html:576), which no engine keeps. See
        // docs/plan/upstream-issues/2026-09-14-triage.md, row 367, reclassified C -> A.
        //
        // Issue 589 is the SAME machinery failing the other way and IS a bug - a prefix denied
        // although its completion exists, where PCRE2 answers PARTIAL. It is ledger entry 21 and
        // Gaps/UpstreamIssues/InheritedIssueTests.cs, and it is not this row.
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
        // PERMANENT, decided 2026-09-12, RE-JUDGED and upheld by S57d on 2026-09-21 on different
        // reasoning: the port is right, upstream is internally inconsistent, and this test does NOT
        // invert when `search_start` lands - a Phase 7 slice that turns it red has ported the bug.
        // See docs/plan/2026-09-12-divergence-research.md.
        //
        // Two of the three reasons first given here have since been measured and do not hold. The
        // maintainer's own argument from upstream issue 589, that `\b` is evaluated against the real
        // string and the empty string holds no word character to be partial about, is the argument
        // ledger entry 21 rejects and S57d now departs from: this port answers a partial for a
        // boundary decided at the end of the available text. And PCRE2's partial here was put down
        // to a rule of PCRE2's own, `pcre2partial`'s "the next pattern item must be one that
        // inspects a character" test. It is not: a bare `\b` has no next pattern item and PCRE2
        // still answers a partial, `\b\b` likewise, and PCRE2_NO_START_OPTIMIZE changes neither
        // (tools/probes/pcre2-hitend-partial-span.py section E, PCRE2 10.47, 2026-09-21). PCRE2 is
        // answering on the boundary, which is the same question, so it is a second opinion after
        // all - and it is against us on this row.
        //
        // What upholds the pin is the first reason, now with upstream's rule behind it. Upstream
        // reports a partial when a node runs out of TEXT and at no other time
        // (tools/probes/upstream-partial-needs-text-exhaustion.py, regex 2026.9.10, 2026-09-21).
        // Nothing in `(?r)\b$` over '' asks for a character, so upstream's own rule says None here,
        // its `match` and `fullmatch` doors say None, and only the door that consults `search_start`
        // says otherwise. S57d's rule reaches the same answer from the other side: the attempt that
        // reached the boundary had consumed nothing, and a zero-width partial at the truncation
        // point is the one case where this port stops short of PCRE2. So both engines' stated rules
        // agree on None and one door of one engine dissents.
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

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reversed_skip_partial_answers_where_upstreams_own_anchored_matcher_does()
    {
        // S52 found this at 300 rows a generator of the `partial-long` wave, seed 7 - and the length
        // is not what found it. Drawn on a 3,363-character subject, it delta-debugs to the THREE
        // codepoints below with the whole signature intact, so what the long wrapper contributed is
        // its astral alphabet over this pattern shape, not its length.
        //
        // The same `search_start` prefilter as the test above, in reverse. Upstream (2026.9.10):
        //
        //   pat = regex.compile(r'(?r)(?:[a-f](*PRUNE)\d|[[:digit:]])(?(?<![[:digit:]])[abz])'
        //                       r'(?:\p{Nd}(*SKIP)\s|\p{L})')
        //   pat.search(s, partial=True)         -> (0, 3) codepoints, partial   <- the WHOLE region
        //   pat.match(s, 0, 3, partial=True)    -> None       <- upstream denies its own answer
        //   pat.match(s, 0, 1, partial=True)    -> (0, 1) codepoints, partial   <- and this is ours
        //   verb deleted,  search(partial=True) -> (0, 1)                       <- ours again
        //   verb -> (*PRUNE), search(partial)   -> (0, 1)                       <- and again
        //
        // A reversed match anchors at the END, so the sweep that finds this port's answer varies the
        // slice end rather than the start - `MatchAtStart(subject, 0, length: n)` here, upstream's
        // `match(s, 0, n)` there. This row and the forward one below are the only two in the arm on
        // which EVERY control returns this port's answer exactly, span and partial flag both: on the
        // rows S37 judged the verb-free spelling answers a COMPLETE match instead, and on one of
        // them the anchor sweep never lands on this port's answer at all.
        //
        // PERMANENT: a Phase 7 slice that ports `search_start` and turns this red has imported the
        // prefilter's answers along with the prefilter. Classified as `search-start-partial` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs. Measured 2026-09-15 on regex
        // 2026.9.10 by tools/probes/upstream-search-start-whole-region-partial.py. Spans below are
        // UTF-16 and the probe's are codepoints: every character here is astral or a line break, so
        // codepoint 1 is UTF-16 offset 2.
        const string pattern = @"(?r)(?:[a-f](*PRUNE)\d|[[:digit:]])(?(?<![[:digit:]])[abz])(?:\p{Nd}(*SKIP)\s|\p{L})";
        const string subject = "\U0001D518\U0001F600\n";

        Match skipped = new FuzzyRegex(pattern, FuzzyRegexOptions.Version0).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length).Should().Be((0, 2), "upstream answers the whole region, (0, 5)");

        // The anchored control, on the bound a reversed match actually anchors on. This is upstream's
        // own answer at that slice end, which is what makes the prefilter the only thing left.
        Match anchored = new FuzzyRegex(pattern, FuzzyRegexOptions.Version0).MatchAtStart(
            subject,
            beginning: 0,
            length: 2,
            partial: true
        );

        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((0, 2), "upstream's own match(s, 0, 1) answers this");

        // And the verb is not what makes this port's answer: a verb that moves no bound, and no verb
        // at all, both leave it where it was - which is upstream's own answer on both spellings too.
        foreach (
            string spelling in new[]
            {
                pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal),
                pattern.Replace("(*SKIP)", "", StringComparison.Ordinal),
            }
        )
        {
            Match other = new FuzzyRegex(spelling, FuzzyRegexOptions.Version0).Match(subject, partial: true);

            other.PartialMatch.Should().BeTrue();
            (other.Index, other.Length).Should().Be((0, 2), $"the verb moves no bound in '{spelling}'");
        }
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_forward_skip_partial_answers_at_the_leftmost_position_anything_matches()
    {
        // S52's second `partial-long` row, seed 20260915, and the forward twin of the one above. It
        // was drawn on an 18,759-character subject and delta-debugs to THREE astral codepoints, so
        // again the long wrapper's alphabet found it rather than its length.
        //
        //   pat = regex.compile(r'(?:[\p{L}\p{N}](*SKIP)\p{Nd}|\p{Ll})(\S)*?(?P<g2>\S?)'
        //                       r'(?:(?(2)(?=(?P>g2))\p{Nd}|.))', regex.M)
        //   pat.search(s, partial=True)         -> (0, 3) codepoints, partial   <- the WHOLE region
        //   pat.match(s, 0, 3, partial=True)    -> None       <- upstream denies its own answer
        //   pat.match(s, 2, partial=True)       -> (2, 3) codepoints, partial   <- and this is ours
        //   pat.match(s, 3, partial=True)       -> (3, 3) zero-width partial
        //   verb deleted,  search(partial=True) -> (2, 3)                       <- ours again
        //   verb -> (*PRUNE), search(partial)   -> (2, 3)                       <- and again
        //
        // PERMANENT, same reason and same classification as the reversed row above. Measured
        // 2026-09-15 on regex 2026.9.10. Spans are UTF-16; every character is astral, so codepoint 2
        // is UTF-16 offset 4.
        const string pattern = @"(?:[\p{L}\p{N}](*SKIP)\p{Nd}|\p{Ll})(\S)*?(?P<g2>\S?)(?:(?(2)(?=(?P>g2))\p{Nd}|.))";
        const string subject = "\U00010400\U0001F3FB\U00010400";
        const FuzzyRegexOptions options = FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version0;

        Match skipped = new FuzzyRegex(pattern, options).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length).Should().Be((4, 2), "upstream answers the whole region, (0, 6)");

        // A forward search anchors at the start, so this is the sweep that finds our answer - and 4
        // is the LEFTMOST position at which anything matches, which is what the search owes.
        foreach (int beginning in new[] { 0, 2 })
        {
            new FuzzyRegex(pattern, options)
                .MatchAtStart(subject, beginning, partial: true)
                .Success.Should()
                .BeFalse($"nothing matches at {beginning}, so the search owes its answer at 4");
        }

        Match anchored = new FuzzyRegex(pattern, options).MatchAtStart(subject, beginning: 4, partial: true);

        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((4, 2), "upstream's own match(s, 2) answers this");

        // And again the verb is not what makes this port's answer.
        foreach (
            string spelling in new[]
            {
                pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal),
                pattern.Replace("(*SKIP)", "", StringComparison.Ordinal),
            }
        )
        {
            Match other = new FuzzyRegex(spelling, options).Match(subject, partial: true);

            other.PartialMatch.Should().BeTrue();
            (other.Index, other.Length).Should().Be((4, 2), $"the verb moves no bound in '{spelling}'");
        }
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
    public void The_narrowed_slice_partial_is_one_rule_about_the_slice_start_and_upstream_holds_two()
    {
        // S33 read these five cells as "a per-opcode answer, not a general rule about slice_start"
        // and pinned upstream's None. Ledger entry 24 read the SAME numbers as upstream contradicting
        // itself - its CHARACTER_REV arms bound by text_start (upstream/src/_regex.c:12190, :7137)
        // while the STRING family's try_match arms bound by slice_start - and the owner ruled on
        // 2026-09-15 for Option B of docs/plan/upstream-reports/ledger-24-briefing.md: there is one
        // rule, and the bound is the slice start. So the five now answer a partial here, DELIBERATELY
        // and against upstream, which is the DIVERGENCES.md row "Reversed partial matches run out of
        // text at the slice start". S52d, 2026-09-16.
        //
        // Upstream, re-measured on 2026.9.10 by tools/probes/upstream-s33-per-opcode-cells.py:
        //   compile(r'(?r)a').match('abc', 1, 1, partial=True)      -> None
        //   compile(r'(?r)ab*').match('abc', 1, 1, partial=True)    -> None   (REPEAT_ONE, not STRING)
        //   compile(r'(?r)a(b)*').match('abc', 1, 1, partial=True)  -> None   (one-character body)
        //   compile(r'(?r)a(bc)+').match('abc', 1, 1, partial=True) -> None   (min 1, tail never tried)
        //   compile(r'(?r)qz|qzzz').match('qz', 1, 2, partial=True) -> None   (common suffix 'z' is
        //                                                   factored out, so the test is CHARACTER_REV)
        // and the same four at a slice that starts where the subject does, where upstream's two rules
        // cannot part company, ALL of them partial at (0, 0):
        //   compile(r'(?r)a').match('abc', 0, 0, partial=True)      -> ((0, 0), partial)
        //   compile(r'(?r)ab*')/(r'(?r)a(b)*')/(r'(?r)a(bc)+')      -> ((0, 0), partial)
        // That second block is what makes the first a contradiction rather than a design: moving an
        // empty slice from 0 to 1 cannot decide whether the pattern could still be completed.
        foreach (string pattern in new[] { "(?r)a", "(?r)ab*", "(?r)a(b)*", "(?r)a(bc)+" })
        {
            Match narrowed = new FuzzyRegex(pattern).MatchAtStart("abc", beginning: 1, length: 0, partial: true);

            narrowed
                .PartialMatch.Should()
                .BeTrue($"the ruling answers a partial for {pattern} on abc[1:1], where upstream answers None");
            (narrowed.Index, narrowed.Length).Should().Be((1, 0));

            // The unnarrowed twin, which upstream and this port have always agreed on.
            Match atZero = new FuzzyRegex(pattern).MatchAtStart("abc", beginning: 0, length: 0, partial: true);

            atZero.PartialMatch.Should().BeTrue();
            (atZero.Index, atZero.Length).Should().Be((0, 0));
        }

        // The branch whose common suffix is factored out, so the test node is CHARACTER_REV: the 'z'
        // matches inside the slice and the 'q' then runs out at the slice start.
        Match branch = new FuzzyRegex("(?r)qz|qzzz").MatchAtStart("qz", beginning: 1, length: 1, partial: true);

        branch.PartialMatch.Should().BeTrue("the 'q' runs out at the slice start, where upstream answers None");
        (branch.Index, branch.Index + branch.Length).Should().Be((1, 2));

        // The control that says this is a run-out and not a match invented out of nothing: at the
        // slice (0, 1) of "qz" the reversed 'z' meets a 'q', which is a real mismatch and not a
        // shortage of text, and BOTH engines answer None.
        //   compile(r'(?r)qz|qzzz').match('qz', 0, 1, partial=True) -> None
        new FuzzyRegex("(?r)qz|qzzz")
            .MatchAtStart("qz", beginning: 0, length: 1, partial: true)
            .Success.Should()
            .BeFalse("the 'z' meets a 'q', so there is nothing to complete");
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

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reversed_skip_that_moves_the_slice_end_can_lengthen_upstreams_partial_too()
    {
        // S52, row 33858 of the seed-20260915 2000-row default wave
        // (`tools/run-oracle.ps1 -Count 2000`, 42,000 rows a seed). The SAME moved `slice_end` as the
        // test above, and worth its own test because the SYMPTOM IS INVERTED: there upstream answers
        // a SHORTER partial than this port, here it answers a LONGER one - the whole subject. A bound
        // the verb redrew changes which region the second pass searches, and which way the answer
        // then moves depends on where the anchors fall in it, so one direction of symptom is not the
        // mechanism's signature and a test that only ever saw the short one would say it was.
        //
        // Measured on regex 2026.9.10, 2026-09-15,
        // tools/probes/upstream-partial-retry-reversed-longer.py:
        //
        //   as drawn, (*SKIP)   search(partial=True)  (0, 5) partial, g1 (1, 2)  <- upstream
        //   verb -> (*PRUNE)    search(partial=True)  (0, 2) partial, g1 (1, 2)  <- this port
        //   verb deleted        search(partial=True)  (0, 2) COMPLETE, g1 (1, 2)
        //   match(0, endpos, partial=True)            None at 5..1, (0, 0) at 0
        //
        // THE ANCHORED SWEEP DOES NOT JUDGE THIS ROW - upstream's own matcher names neither answer -
        // so unlike the test above the `(*PRUNE)` control is the whole of the evidence rather than a
        // corroboration. It is decisive on its own: `(*PRUNE)` prunes backtracking exactly as
        // `(*SKIP)` does and moves NO bound, and it gives this port's span, group and partial flag
        // to the code unit. Classified as row 5 of `partial-retry-reversed-slice` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        var skipped = new FuzzyRegex(@"(?r)(?:[A-Z](*SKIP).|\d)([^\p{L}])\B");

        Match search = skipped.Match("00a .", partial: true);
        search.PartialMatch.Should().BeTrue();
        (search.Index, search.Length).Should().Be((0, 2), "upstream answers (0, 5), the whole subject");
        (search.Groups[1].Index, search.Groups[1].Length).Should().Be((1, 1));

        // The control, and here the entire argument: the same pruning with no bound moved is what
        // upstream answers this port's way.
        Match pruned = new FuzzyRegex(@"(?r)(?:[A-Z](*PRUNE).|\d)([^\p{L}])\B").Match("00a .", partial: true);
        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 2));

        // And with no verb at all the span is the same again and the match is COMPLETE, on both
        // engines - so the verb costs the completeness and the moved bound costs the span.
        Match noVerb = new FuzzyRegex(@"(?r)(?:[A-Z].|\d)([^\p{L}])\B").Match("00a .", partial: true);
        noVerb.PartialMatch.Should().BeFalse();
        (noVerb.Index, noVerb.Length).Should().Be((0, 2));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reversed_skip_does_not_lengthen_an_anchored_fuzzy_partial_either()
    {
        // S57, the Phase 6 exit gate's last red row: row 525 of `tools/run-oracle.ps1
        // -Generator fuzzy,interactions -Seeds 99991,57057`, drawn again as row 225 of the 6000-row
        // `interactions` wave at the same seed. The same moved `slice_end` as the two tests above,
        // and worth its own test because it is the first of the family that is ANCHORED (`^`) and
        // carries a FUZZY section: those two are exactly what a reader would suspect of a partial
        // span that grew, so both are ablated here and neither is the cause.
        //
        // Measured on regex 2026.9.10, 2026-09-20,
        // tools/probes/upstream-partial-retry-reversed-anchored.py (port half
        // tools/probes/s57-skip-partial-span.cs):
        //
        //   as drawn, (*SKIP)   search(partial=True)  (0, 2) partial   <- upstream
        //   verb -> (*PRUNE)    search(partial=True)  (0, 1) partial   <- this port
        //   verb deleted        search(partial=True)  (0, 1) partial
        //   match(0, endpos, partial=True)            None at 3 and 2, (0, 1) at 1, (0, 0) at 0
        //   fuzzy section deleted, verb kept          (0, 1) partial
        //   anchor deleted, verb kept                 (0, 2) partial
        //
        // BOTH of this family's arguments hold here, which is why the row needs no new one: the
        // `(*PRUNE)` control gives this port's span, and so does upstream's own matcher at the
        // highest `endpos` that matches at all - the first anchor a reversed search tries. Deleting
        // the fuzzy section leaves upstream at this port's answer and deleting the anchor leaves it
        // at its own, so neither is what moves the span; the verb is. Classified as row 13 of
        // `partial-retry-reversed-slice` in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        //
        // Spans are codepoints upstream and UTF-16 here. The subject's only astral character is its
        // last, past every span below except the forward twin's, which is upstream's (0, 3) as (0, 4).
        const string subject = "\r\n\U0001F600";
        const string body = @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}";

        Match search = new FuzzyRegex(body + @"(?:[a-f](*SKIP)\s|\p{Nd})").Match(subject, partial: true);
        search.PartialMatch.Should().BeTrue();
        (search.Index, search.Length).Should().Be((0, 1), "upstream answers (0, 2), a code unit the verb moved");

        // The control that judges it: the same pruning with no bound moved, and upstream agrees.
        Match pruned = new FuzzyRegex(body + @"(?:[a-f](*PRUNE)\s|\p{Nd})").Match(subject, partial: true);
        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 1));

        // The corroboration: upstream's own matcher at the highest endpos that matches at all.
        Match anchored = new FuzzyRegex(body + @"(?:[a-f](*SKIP)\s|\p{Nd})").MatchAtStart(
            subject,
            beginning: 0,
            length: 1,
            partial: true
        );
        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((0, 1));

        // The two ablations, in the order the probe runs them: neither the fuzzy section nor the
        // anchor is what upstream's extra code unit comes from.
        const string noFuzzy = @"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]]))(?:[a-f](*SKIP)\s|\p{Nd})";
        Match unfuzzy = new FuzzyRegex(noFuzzy).Match(subject, partial: true);
        unfuzzy.PartialMatch.Should().BeTrue();
        (unfuzzy.Index, unfuzzy.Length).Should().Be((0, 1), "upstream answers (0, 1) here too");

        // Forwards the verb moves `slice_start`, which this subject's anchors do not expose, and the
        // two engines agree: upstream's codepoint (0, 3) is this port's UTF-16 (0, 4).
        Match forward = new FuzzyRegex(
            @"^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})"
        ).Match(subject, partial: true);
        forward.PartialMatch.Should().BeTrue();
        (forward.Index, forward.Length).Should().Be((0, 4), "upstream answers the same span, codepoints (0, 3)");
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

    [Test]
    public void A_forward_skip_does_not_cost_the_partial_its_start()
    {
        // THE SAME MECHANISM WITH A SYMPTOM THE TEST ABOVE DOES NOT COVER, and the test above says
        // so in as many words - "BOTH engines answer a partial at the same span". Here they do not.
        // Seed 7 row 99850 of the 6000-row three-seed gate, S52 sitting 8: upstream answers the
        // ZERO-WIDTH partial at the far end of what it searched, where its own bound-free spellings
        // answer the wider partial this port answers. So the moved `slice_start` costs a START
        // here, which is the reversed entry's symptom appearing on a forward pattern - the one
        // thing the split-by-direction convention did not predict.
        //
        // Measured 2026-09-15 on regex 2026.9.10, tools/probes/gate-divergence-doors.py, in
        // codepoints (the subject opens with two astral characters, so the spans below are UTF-16):
        //
        //   as the wave drew it       (5, 5) partial, group 1 unset        <- upstream
        //   (*SKIP) -> (*PRUNE)       (3, 5) partial, group 1 at (4, 4)    <- this port's
        //   verb deleted              (3, 5) partial, group 1 at (4, 4)    <- this port's
        //   match(pos=3, partial)     (3, 5) partial                       <- this port's
        //   no partial asked for      None                                 <- both engines
        //
        // `(*PRUNE)` prunes the same backtracking and moves no bound, so the bound move is the
        // cause; the anchor sweep says upstream's own matcher reaches this port's start; and the
        // last line says there is no divergence without the second pass to carry a bound into.
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // `partial-retry-carried-slice-forward` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        const string pattern = @"^A([^a-f]*)(?:\D(*SKIP)\p{ASCII}|\s)";
        const string subject = "\U00010400\U00010400\nAA";
        const FuzzyRegexOptions options =
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.FullCase;

        Match skipped = new FuzzyRegex(pattern, options).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length)
            .Should()
            .Be((5, 2), "upstream reports the zero-width partial at UTF-16 7 instead");
        (skipped.Groups[1].Index, skipped.Groups[1].Length)
            .Should()
            .Be((6, 0), "upstream leaves group 1 unset, never entering the branch that fills it");

        // The control: a verb that moves no bound, and upstream then answers what this port answers.
        Match pruned = new FuzzyRegex(pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), options).Match(
            subject,
            partial: true
        );

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((5, 2));
        (pruned.Groups[1].Index, pruned.Groups[1].Length).Should().Be((6, 0));

        // And with no partial asked for there is no second pass to carry a bound into.
        new FuzzyRegex(pattern, options)
            .Match(subject)
            .Success.Should()
            .BeFalse();
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_forward_skip_costs_the_partial_its_start_without_costing_the_match()
    {
        // THE SAME MECHANISM AGAIN, and the symptom one step past the test above's. That test's own
        // comment says upstream answers "the ZERO-WIDTH partial at the far end of what it searched",
        // and until S52's eighteenth sitting every row of this family did. This one does not:
        // upstream answers a ONE-CODEPOINT partial at a LATER START, so the moved `slice_start` costs
        // a start without costing the whole match, and "a zero-width partial at the far end" is a
        // symptom this family often shows rather than one it always shows.
        //
        // Row 22 of tools/probes/sweep-divergence-rows.jsonl - row 32949 of the eight-seed seed
        // sweep's seed 655924813. Measured 2026-09-15 on regex 2026.9.10 by
        // tools/probes/upstream-partial-anchor-reachability.py, IN CODEPOINTS (the subject's four
        // middle characters are astral, so the spans asserted below are UTF-16):
        //
        //   as the wave drew it       (5, 6) partial, g1 (5, 5), g2 (5, 6)   <- upstream
        //   (*SKIP) -> (*PRUNE)       (4, 6) partial, g1 (4, 5), g2 (5, 6)   <- this port's
        //   match(pos=4, partial)     (4, 6) partial, g1 (4, 5), g2 (5, 6)   <- this port's
        //   no partial asked for      None                                   <- both engines
        //
        // WHAT JUDGES IT is the UNCAPPED anchor sweep. A forward search tries the lowest `pos`
        // first, so the answer it owes is the first anchor at which its own anchored matcher answers
        // at all; upstream's answers at pos 4, 5 and 6, and its search returns pos 5's. `(*PRUNE)`
        // prunes the same backtracking and moves no bound, so the bound move is the cause rather
        // than what the pattern means. The verb-free spelling is NOT a control and is not asserted
        // here: deleting the verb prunes nothing, so it may reach a match the pruned spellings
        // cannot, and here it answers a COMPLETE match at codepoints (1, 5).
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // `partial-retry-carried-slice-forward` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        const string pattern = @"(?P<g1>\w{0,1}){1,3}?([^[\p{L}--[a-z]]]?)(?:\p{Lu}(*SKIP)[[a-f]~~[d-k]]|\p{Ll})\b";
        const string subject = " \U0001D7EE\U0001D7EE\U00010400\U00010400 ";
        const FuzzyRegexOptions options =
            FuzzyRegexOptions.IgnoreCase
            | FuzzyRegexOptions.Multiline
            | FuzzyRegexOptions.Version1
            | FuzzyRegexOptions.FullCase;

        Match skipped = new FuzzyRegex(pattern, options).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length)
            .Should()
            .Be((7, 3), "upstream starts at UTF-16 9 instead, one codepoint further in");
        (skipped.Groups[1].Index, skipped.Groups[1].Length)
            .Should()
            .Be((7, 2), "upstream's group 1 is the empty span at UTF-16 9");

        // The control: a verb that moves no bound, and upstream then answers what this port answers.
        Match pruned = new FuzzyRegex(pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), options).Match(
            subject,
            partial: true
        );

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((7, 3));
        (pruned.Groups[1].Index, pruned.Groups[1].Length).Should().Be((7, 2));

        // And with no partial asked for there is no second pass to carry a bound into.
        new FuzzyRegex(pattern, options)
            .Match(subject)
            .Success.Should()
            .BeFalse();
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_search_only_partial_need_not_cover_the_whole_searched_region()
    {
        // `search-start-partial`'s second arm, and the first row of it whose span is NOT the whole
        // searched region. Every row of that arm from S37 to S52's eighth sitting had upstream
        // reporting a partial over the entire region - which is the prefilter's usual fingerprint,
        // and which the entry's own prose called the shape of the family. Here the region is
        // codepoints (0, 4) and upstream answers (0, 2). The DISCRIMINATOR still holds and it is the
        // recorded one: upstream's own anchored matcher denies the span its search reported.
        //
        // Row 36 of tools/probes/sweep-divergence-rows.jsonl - row 33723 of the eight-seed seed
        // sweep's seed 793244924. Measured 2026-09-15 on regex 2026.9.10 by
        // tools/probes/upstream-partial-anchor-reachability.py:
        //
        //   as the wave drew it        (0, 2) partial            <- upstream
        //   match over (0, 2)          None                      <- upstream denies its own answer
        //   every (pos, endpos) pair   only (0, 0) and (0, 1)    <- (0, 2) is reachable NOWHERE
        //   match(endpos=1, partial)   (0, 1) partial            <- this port's
        //   (*SKIP) -> (*PRUNE)        (0, 1) partial            <- this port's
        //
        // A reversed match anchors at its END, so the anchor a reversed search tries first is the
        // HIGHEST `endpos` that answers - here 1, and its answer is this port's. The subject is
        // CRLF followed by a zero-width joiner and an astral emoji modifier, and this port's answer
        // is the CR alone.
        //
        // `(?a)` is written inline rather than passed as an option because ASCII is not a public
        // FuzzyRegexOptions member; upstream compiles the two spellings to the identical flag word
        // 0x2488 and answers both calls identically, checked 2026-09-15 on regex 2026.9.10.
        //
        // PERMANENT, and judged in this port's favour. Classified as `search-start-partial` in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        const string pattern = @"(?a)(?r)\m(?:\d?(*SKIP)[\w\s]|\w)";

        // U+200D ZERO WIDTH JOINER as a char code rather than in the literal: it is invisible in a
        // source file, and S2479 refuses a control character in a string literal for that reason.
        string subject = "\r\n" + (char)0x200D + "\U0001F3FB";

        Match skipped = new FuzzyRegex(pattern, FuzzyRegexOptions.Multiline).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length)
            .Should()
            .Be((0, 1), "upstream reports (0, 2), a span its own anchored matcher never produces");

        // The control: a verb that moves no bound, and upstream then answers what this port answers.
        new FuzzyRegex(pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), FuzzyRegexOptions.Multiline)
            .Match(subject, partial: true)
            .Should()
            .Match<Match>(static m => m.PartialMatch && m.Index == 0 && m.Length == 1);
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

    /// <summary>
    /// Seed 7 row 24018 of S52's wave: a partial <c>match</c> whose <c>(*SKIP)</c> upstream answers
    /// with its VERB-FREE answer, and with the partial flag clear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same two-pass carry as
    /// <see cref="A_forward_skip_does_not_move_the_slice_start_the_partial_pass_searches"/>, and the
    /// row that closes the "but a <c>(*SKIP)</c> is allowed to differ on a scan" objection for the
    /// whole family: a <c>match</c> is ONE attempt, so there is no next attempt for the verb to move
    /// the start of, and inside one attempt <c>(*SKIP)</c> prunes exactly what <c>(*PRUNE)</c>
    /// prunes.
    /// </para>
    /// <para>
    /// Upstream refutes itself in two calls, with no model of the engine needed. Measured 2026-09-15
    /// on regex 2026.9.10, <c>tools/probes/upstream-skip-carried-slice-doors.py</c>:
    /// <code>
    /// match(partial=True)   (0, 2) NOT partial, two deletions   &lt;- upstream
    /// match()               None                                &lt;- upstream
    /// (*PRUNE), partial     (0, 2) PARTIAL, two substitutions   &lt;- this port
    /// verb deleted, either  (0, 2) NOT partial, two deletions
    /// </code>
    /// `partial=True` is documented to ALSO allow a partial match, so it cannot answer a COMPLETE
    /// one the same engine denies without it. Classified by
    /// <c>ExpectedDivergences.partial-retry-carried-slice-forward</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void A_partial_match_of_a_skip_is_not_the_verb_free_answer()
    {
        // The wave drew it with no flags at all; the recorder resolves a version-less pattern under
        // upstream's own default, so Version0 here.
        Dictionary<string, IReadOnlyCollection<string>> lists = new(StringComparer.Ordinal)
        {
            ["w1"] = ["sı", "İ", "ﬁ", "ﬁı"],
        };
        FuzzyRegex pattern = new(@"\L<w1>{e<=2}(?:\D(*SKIP)\S|\p{Lu})", FuzzyRegexOptions.Version0, lists);

        Match m = pattern.MatchAtStart("ßß", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(0, 1);

        // The control, run here rather than only quoted: with the verb spelled (*PRUNE) - the same
        // pruning, no bound moved - this port answers the identical thing, so the two verbs agree
        // inside one attempt exactly as they must.
        Match pruned = new FuzzyRegex(
            @"\L<w1>{e<=2}(?:\D(*PRUNE)\S|\p{Lu})",
            FuzzyRegexOptions.Version0,
            lists
        ).MatchAtStart("ßß", partial: true);

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 2));
        pruned.FuzzyChanges.Substitutions.Should().Equal(0, 1);

        // And the other half of upstream's contradiction, asked of this port: with no partial
        // requested there is no match, which is upstream's own answer on all three of its lines.
        pattern.MatchAtStart("ßß").Success.Should().BeFalse();
    }

    /// <summary>
    /// Seed 7 row 24737 of S52's wave: the same defect as
    /// <see cref="A_partial_match_of_a_skip_is_not_the_verb_free_answer"/> on a <c>search</c>.
    /// </summary>
    /// <remarks>
    /// Upstream's partial search answers a complete match at codepoints (5, 5) - the pattern ends in
    /// <c>\K</c>, so its reported start is reset - and its own non-partial search answers
    /// <c>None</c>. This port answers upstream's own <c>(*PRUNE)</c> line, codepoints (3, 7) partial
    /// with group 1 at (5, 6), which is UTF-16 (5, 11) and (8, 10) on this astral subject. Measured
    /// 2026-09-15, <c>tools/probes/upstream-skip-carried-slice-doors.py</c>.
    /// </remarks>
    [Test]
    public void A_partial_search_of_a_skip_is_not_the_verb_free_answer()
    {
        const string subject = "\U0001F600\U0001F600aa\U00010428\U00010428 ";
        FuzzyRegex pattern = new(
            @"(?:(?:a[\p{L}\p{N}]?(?:(.+?)){e<=2:\s}){1i+2d+1s<=3}(*SKIP)\W|\w)(\p{Ll}{3,3}?)+\K",
            FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version0
        );

        Match m = pattern.Match(subject, partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((5, 6));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((8, 2));
        m.Groups[2].Success.Should().BeFalse();

        // The same two controls as the row above.
        Match pruned = new FuzzyRegex(
            @"(?:(?:a[\p{L}\p{N}]?(?:(.+?)){e<=2:\s}){1i+2d+1s<=3}(*PRUNE)\W|\w)(\p{Ll}{3,3}?)+\K",
            FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version0
        ).Match(subject, partial: true);

        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((5, 6));

        pattern.Match(subject).Success.Should().BeFalse();
    }

    /// <summary>
    /// Seed 31337 row 3633 - an EXTRA seed S60 sitting 2 ran beyond the gate, not one of
    /// <c>run-oracle.ps1</c>'s three defaults (7, 4242 and the date, <c>:256</c>) - and the first
    /// PARTIAL SEARCH in <c>end-of-line-reads-a-skip-moved-slice</c>, whose sixth row it is. Every
    /// other row of that entry is a split, a scan or a substitution, so the <c>$</c> tell had never
    /// been read on this operation before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream answers a partial ENDING AT codepoint 1, where its own <c>$</c> is false: asked one
    /// anchored position at a time, upstream's <c>$</c> is true at 0 and 4 alone. A reversed
    /// <c>(*SKIP)</c> writes <c>slice_end</c> (<c>upstream/src/_regex.c:14553</c>) and
    /// <c>try_match_END_OF_LINE</c> (<c>:7110</c>) reads it, so the verb manufactures a line end at
    /// the moved bound - this entry's whole signature.
    /// </para>
    /// <para>
    /// The anchor sweep is NOT the control here and points the other way: upstream's own
    /// <c>match(0, 1, partial=True)</c> does answer (0, 1), but passing that <c>endpos</c> sets
    /// <c>slice_end</c> to 1 itself and makes <c>$</c> true there, so it reproduces the defect
    /// rather than testing it. What does run is the <c>(?w)</c> twin, which reads <c>text_end</c>
    /// instead: <c>(?w)$</c> is true at [0, 4] too, so 1 is not a line end the twin would create.
    /// Measured 2026-09-20 on regex 2026.9.10,
    /// <c>tools/probes/upstream-skip-partial-anchor-grid.py</c>, in CODEPOINTS:
    /// <code>
    /// search(partial=True)          (0, 1) g1 unset PARTIAL   &lt;- upstream
    /// (*SKIP) -&gt; (*PRUNE)           (0, 0) g1 unset PARTIAL   &lt;- THIS PORT'S ANSWER
    /// (?w)$                         (0, 0) g1 unset PARTIAL
    /// $ spelled (?:(?=\n)|(?!\n|.)) (0, 0) g1 unset PARTIAL
    /// the verb deleted              (1, 4) g1=(1, 2) COMPLETE  &lt;- printed, not a control
    /// </code>
    /// The verb-free line is printed and not treated as a control, as everywhere in this family:
    /// deleting a verb prunes nothing, so it may reach a match neither pruned spelling can.
    /// </para>
    /// </remarks>
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_reversed_partial_search_of_a_skip_ends_where_the_line_really_ends()
    {
        // The row as the wave drew it: flags 264 is MULTILINE | VERSION1, and `[\p{L}||\p{N}]` is a
        // set union, which only Version1 parses.
        const string subject = "\n\U0001D7EE\U0001D518\U0001D518";
        const string pattern = @"(?r)^(?P<g1>[\p{L}||\p{N}]){1,}(?:[a\d](*SKIP)[\w\s]|\w)$";
        FuzzyRegexOptions options = FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version1;

        Match skipped = new FuzzyRegex(pattern, options).Match(subject, partial: true);

        skipped.PartialMatch.Should().BeTrue();
        (skipped.Index, skipped.Length).Should().Be((0, 0), "upstream ends at codepoint 1, where its own `$` is false");
        skipped.Groups[1].Success.Should().BeFalse();

        // The controls, run here rather than only quoted. `(*PRUNE)` prunes what `(*SKIP)` prunes
        // and moves no bound; `(?w)$` reads `text_end` rather than the moved `slice_end`; and `$`
        // spelled out has no end-of-line opcode to read a bound at all. All three agree with the
        // answer above, on this port as they do on upstream.
        Match pruned = new FuzzyRegex(pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), options).Match(
            subject,
            partial: true
        );
        pruned.PartialMatch.Should().BeTrue();
        (pruned.Index, pruned.Length).Should().Be((0, 0));

        Match wordTwin = new FuzzyRegex(pattern, options | FuzzyRegexOptions.Word).Match(subject, partial: true);
        wordTwin.PartialMatch.Should().BeTrue();
        (wordTwin.Index, wordTwin.Length).Should().Be((0, 0));

        Match spelledOut = new FuzzyRegex(
            pattern.Replace("$", @"(?:(?=\n)|(?!\n|.))", StringComparison.Ordinal),
            options
        ).Match(subject, partial: true);
        spelledOut.PartialMatch.Should().BeTrue();
        (spelledOut.Index, spelledOut.Length).Should().Be((0, 0));

        // Not a control, and the reason the three above are not vacuous: with no verb at all both
        // engines reach a COMPLETE match over codepoints (1, 4), UTF-16 (1, 6). A zero-width partial
        // at 0 is this pattern's answer to the verb, not this port's answer to everything.
        Match noVerb = new FuzzyRegex(pattern.Replace("(*SKIP)", "", StringComparison.Ordinal), options).Match(
            subject,
            partial: true
        );
        noVerb.PartialMatch.Should().BeFalse();
        (noVerb.Index, noVerb.Length).Should().Be((1, 6));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_reversed_partial_stops_at_the_anchor_where_the_text_really_ran_out()
    {
        // Sweep row 6 of tools/probes/sweep-divergence-rows.jsonl, the last of the seed sweep's 37
        // rows to be judged (S52's nineteenth sitting, `reversed-partial-its-own-pattern-cannot-
        // produce`). Upstream answers a partial over the WHOLE subject; this port answers one
        // codepoint shorter, and the difference is whether the match ran out of text or mismatched
        // on text it had.
        //
        // PROVENANCE, measured 2026-09-16 on regex 2026.9.10 by .scratch/prov.py and
        // .scratch/anchors2.py, in CODEPOINTS:
        //
        //   search(partial=True)          (0, 3) g1 unset PARTIAL   <- upstream
        //   match(0, 3, partial=True)     (0, 3) g1 unset PARTIAL   <- the same phantom anchored
        //   match(0, 2, partial=True)     (0, 2) g1=(1, 2) PARTIAL  <- THIS PORT'S ANSWER
        //   search()                      None                      <- no complete match either way
        //   the (*PRUNE) deleted          (0, 3) g1 unset PARTIAL   <- the verb is not involved
        //   of 56 one- and two-character prefixes over {space, newline, '0', 'a', U+1D518, '_',
        //     tab}, four match anywhere at all and NONE completes the match at the text end
        //
        // WHY (0, 3) CANNOT BE A PARTIAL. A partial promises that more text would complete the
        // match, and a reversed match runs out of text on the LEFT - so the completing text is a
        // PREFIX. The pattern needs a literal SPACE immediately left of its alternation, the
        // alternation can only end at codepoint 3 by consuming one or two characters, so the space
        // would have to be the '\n' at 0 or the astral letter at 1. Both are characters the subject
        // already has, and no prefix can change them. At codepoint 2 the story is different and the
        // partial is real: `[^\d]` consumes the '\n' at 0, the literal space then needs codepoint
        // -1, and THAT is running out of text.
        //
        // PERMANENT, and judged in this port's favour.
        const string pattern = @"(?r)(?p)\b\ (?:.??(*PRUNE)[^\d]|\p{Nd})(\p{L}+?)?";
        string subject = "\n" + char.ConvertFromUtf32(0x1D518) + " ";

        Match partial = new FuzzyRegex(pattern, FuzzyRegexOptions.IgnoreCase).Match(subject, partial: true);

        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((0, 3), "upstream answers (0, 4), the whole subject");
        (partial.Groups[1].Index, partial.Groups[1].Length)
            .Should()
            .Be((1, 2), "upstream's group 1 is unset in its phantom answer");

        // With no partial asked for there is no match at all, on either engine.
        new FuzzyRegex(pattern, FuzzyRegexOptions.IgnoreCase)
            .Match(subject)
            .Success.Should()
            .BeFalse();

        // The control that says the verb is not in this: deleting it moves neither engine.
        Match verbless = new FuzzyRegex(
            pattern.Replace("(*PRUNE)", "", StringComparison.Ordinal),
            FuzzyRegexOptions.IgnoreCase
        ).Match(subject, partial: true);

        verbless.PartialMatch.Should().BeTrue();
        (verbless.Index, verbless.Length).Should().Be((0, 3));
    }

    /// <summary>
    /// A repeat whose body ran out of subject part-way through its next repetition, with the
    /// minimum already met: the match so far is partial, not complete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written because S57's coverage backstop found <c>AtEnd</c> (<c>Matcher.cs:2066</c>,
    /// upstream <c>at_end</c> at <c>_regex.c:11628</c>) wholly unreached, and this is the shape its
    /// one caller's guard describes - "the body came back partial, the repeat has had its minimum,
    /// and we are at the end of the slice" (<c>Matcher.cs:5796</c>). It does NOT reach it:
    /// a coverage run over this class alone, 2026-09-20, still reports <c>:5796</c> and
    /// <c>:2067</c> at zero hits. That agrees with the comment already standing at the call site -
    /// only <c>try_match</c>'s test-node arm, which Phase 7 restores, can answer PARTIAL there -
    /// so the port answers this shape by another route and the guard stays as ported.
    /// </para>
    /// <para>
    /// The trailing anchor is what makes the answer partial rather than complete: without it the
    /// shorter complete match wins and the engine never asks the question. Measured on regex
    /// 2026.9.10, 2026-09-20: <c>regex.compile(r"(?:abc)+$").search("abcab", partial=True)</c>
    /// gives <c>span=(0, 5), match='abcab', partial=True</c>, and the same call for
    /// <c>(?:abc)+\Z</c> and for <c>^(?:abc)+$</c> gives the same; with no anchor,
    /// <c>regex.compile(r"(?:abc)+").search("abcabcab", partial=True)</c> gives the complete
    /// <c>span=(0, 6), match='abcabc'</c>.
    /// </para>
    /// </remarks>
    [Test]
    [Arguments(@"(?:abc)+$")]
    [Arguments(@"(?:abc)+\Z")]
    [Arguments(@"^(?:abc)+$")]
    public void A_repeat_that_ran_out_mid_body_at_the_end_is_partial(string pattern)
    {
        Match partial = new FuzzyRegex(pattern).Match("abcab", partial: true);

        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((0, 5));

        // The control: with room to finish a repetition the complete match wins and nothing is
        // partial, which is what says the assertion above is about running out and not about the
        // pattern.
        Match complete = new FuzzyRegex(@"(?:abc)+").Match("abcabcab", partial: true);

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 6));
    }

    /// <summary>
    /// A fuzzy match searched right to left, which a deletion walks off the left-hand end of: the
    /// subject ran out on the LEFT, so the answer is partial.
    /// </summary>
    /// <remarks>
    /// Written because S57's coverage backstop found <c>SteppedPastTheLeft</c>
    /// (<c>Matcher.cs:3342</c>) reached by nothing, though two call sites lead to it (<c>:3353</c>
    /// and <c>:5192</c>). It does not reach them either - the same coverage run reports both at
    /// zero hits - so this records the behaviour rather than the line, and S57's notes carry the
    /// two as unreached. Measured on regex
    /// 2026.9.10, 2026-09-20:
    /// <c>regex.compile(r"(?r)(?:abcd){e&lt;=1}").search("cd", partial=True)</c> gives
    /// <c>span=(0, 2), match='cd', partial=True</c> and
    /// <c>regex.compile(r"(?r)(?:abcd){e&lt;=2}").search("d", partial=True)</c> gives
    /// <c>span=(0, 1), match='d', partial=True</c>. The control is the same pattern with an error
    /// budget the subject can spend without running out:
    /// <c>regex.compile(r"(?r)(?:abc){e&lt;=1}").search("bc", partial=True)</c> gives
    /// <c>span=(0, 2), match='bc', fuzzy_counts=(0, 0, 1)</c> - a complete match with one deletion,
    /// not a partial.
    /// </remarks>
    [Test]
    [Arguments(@"(?r)(?:abcd){e<=1}", "cd", 2)]
    [Arguments(@"(?r)(?:abcd){e<=2}", "d", 1)]
    public void A_reversed_fuzzy_match_that_ran_off_the_left_is_partial(
        string pattern,
        string subject,
        int expectedLength
    )
    {
        Match partial = new FuzzyRegex(pattern).Match(subject, partial: true);

        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Length).Should().Be((0, expectedLength));

        Match complete = new FuzzyRegex(@"(?r)(?:abc){e<=1}").Match("bc", partial: true);

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 2));
    }

    /// <summary>
    /// A word or grapheme boundary decided at the end of the available text makes a partial match
    /// here, where upstream reports nothing at all.
    /// </summary>
    /// <remarks>
    /// The deliberate divergence S57d adds, and the rule behind it is upstream's rather than this
    /// port's: upstream reports a partial when a node runs out of TEXT, never when a boundary runs
    /// out of CONTEXT. <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c> is the
    /// measurement, on regex 2026.9.10, 2026-09-21, and its decisive pair is
    /// <c>a+\B</c> against <c>aa\B</c> over 'aa': both reach the same <c>\B</c> at position 2 and
    /// both fail there, and only the one whose repeat can ask for a third character answers a
    /// partial. So upstream is not making a judgement about the boundary here; the boundary plays
    /// no part in its answer.
    /// <para>
    /// Ledger entry 21 is the case for changing that, and PCRE2 is the second engine that already
    /// has: <c>pcre2partial(3)</c> names <c>\z</c>, <c>\Z</c>, <c>\b</c>, <c>\B</c> and <c>$</c> as
    /// the constructs that "always give a partial match", because the end of the buffer need not be
    /// the end of the data. A caller feeding a stream one chunk at a time gets a wrong answer
    /// otherwise: upstream answers None to <c>(?!(True|False)\b)(.*)</c> over the chunk 'True', and a
    /// next chunk of 's' makes the word 'Trues', which the pattern matches (measured, regex
    /// 2026.9.10, 2026-09-21). Note that a complete match still wins, so this changes nothing for
    /// <c>True\b</c> over 'True': that is a complete match in both engines.
    /// </para>
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    // A boundary that fails at the end of the text, with the attempt having consumed the whole span.
    [Arguments(@"aa\B", "aa", 2)]
    [Arguments(@"a{2}\B", "aa", 2)]
    [Arguments(@"True\B", "True", 4)]
    [Arguments(@"a?\B", "a", 1)]
    // A boundary that SUCCEEDS at the end of the text, where what fails is the construct around it.
    // Both rows need this: entry 21's own row is a lookahead whose inner `\b` succeeded, and `\X`
    // ends at a grapheme boundary that more text could move.
    [Arguments(@"(?!(True|False)\b)(.*)", "True", 4)]
    [Arguments(@"\X(?<!a)", "a", 1)]
    public void A_boundary_decided_at_the_end_of_the_text_reports_a_partial_upstream_denies(
        string pattern,
        string subject,
        int expectedLength
    )
    {
        // Upstream answers None to every row here on both of these doors. The probe prints, for
        // example, 'aa\B' 'aa' match=None fullmatch=None and 'True\B' 'True' match=None
        // fullmatch=None.
        var compiled = new FuzzyRegex(pattern);

        foreach (
            Match m in new[]
            {
                compiled.MatchAtStart(subject, partial: true),
                compiled.FullMatch(subject, partial: true),
            }
        )
        {
            m.PartialMatch.Should().BeTrue();

            // The span runs from the start of the attempt that reached the end of the text to the
            // end of the text, which is PCRE2's answer for the same rows:
            // tools/probes/pcre2-hitend-partial-span.py, PCRE2 10.47, 2026-09-21.
            (m.Index, m.Length)
                .Should()
                .Be((0, expectedLength));
        }
    }

    /// <summary>
    /// The search door usually agrees with upstream anyway, because a search that can retry at the
    /// end of the subject finds upstream's own partial there first.
    /// </summary>
    /// <remarks>
    /// This is why the divergence above is nearly invisible to the differential oracle, whose
    /// generators mostly search. Over 'aa' the attempt at 0 reaches <c>\B</c> at the end and fails,
    /// but the search then retries at 1, where the second <c>a</c> runs out of text and both engines
    /// report the ordinary partial (1, 2). A start anchor removes the retry, and then the two
    /// engines differ: upstream measured None for <c>^a\B</c> over 'a' by
    /// <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c>, regex 2026.9.10, 2026-09-21.
    /// </remarks>
    [Test]
    public void A_partial_found_the_ordinary_way_is_kept_in_place_of_the_boundary_one()
    {
        // regex.compile(r'aa\B').search('aa', partial=True) -> (1, 2), partial True
        Match ordinary = new FuzzyRegex(@"aa\B").Match("aa", partial: true);

        ordinary.PartialMatch.Should().BeTrue();
        (ordinary.Index, ordinary.Length).Should().Be((1, 1));

        // DIVERGES FROM UPSTREAM, deliberately: with `^` there is no later start to retry from, so
        // nothing runs out of text and only the boundary is left to answer.
        Match anchored = new FuzzyRegex(@"^a\B").Match("a", partial: true);

        anchored.PartialMatch.Should().BeTrue();
        (anchored.Index, anchored.Length).Should().Be((0, 1));
    }

    /// <summary>
    /// A partial derived from a boundary reports no groups, even for a group that had closed before
    /// the boundary was reached.
    /// </summary>
    /// <remarks>
    /// PCRE2 defines none: on a partial match "only the first pair in the ovector is set", and the
    /// rest is undefined (<c>pcre2partial(3)</c>, read 2026-09-21 at
    /// <see href="https://www.pcre.org/current/doc/html/pcre2partial.html"/>). Measured, that is
    /// uninitialised memory - <c>tools/probes/pcre2-hitend-partial-span.py</c> section C prints
    /// <c>(8819262122025316210,4981658938864334708)</c> for the two groups of <c>(a)(b)\B</c>. So
    /// there is nothing to copy, and reporting a group whose span the model does not define would
    /// be inventing an answer. Upstream reports None for this row altogether.
    /// <para>
    /// A partial found the ordinary way is unaffected and still carries its groups, which
    /// <see cref="A_partial_match_keeps_the_groups_that_had_already_closed"/> pins.
    /// </para>
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    // The `(*SKIP)` row is the one that makes the clearing load-bearing, and it took a negative
    // control to find: without `(*SKIP)` the backtracking unwinds every group on the way out, so a
    // port that never cleared them would pass anyway. The verb prunes that unwind and group 1
    // survives the failure at (0, 1) with `lastindex` 1. Measured with `state.ClearGroups()`
    // removed, 2026-09-21. Upstream answers None to both rows.
    [Arguments(@"(a)(b)\B")]
    [Arguments(@"(a)(*SKIP)(b)\B")]
    public void A_boundary_partial_reports_no_groups(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("ab", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Success.Should().BeFalse();
    }

    /// <summary>
    /// A complete match still wins. The boundary answer is a fallback, consulted only once the whole
    /// scan has failed.
    /// </summary>
    /// <remarks>
    /// PCRE2 calls this the soft option, and it is the default there and the only behaviour here:
    /// "the partial match is remembered, but matching continues as normal", and the partial is
    /// returned only "if no complete match can be found" (<c>pcre2partial(3)</c>, read 2026-09-21).
    /// Its hard option, which returns the partial even when a complete match exists, has no
    /// equivalent in this port or upstream. Both rows agree with upstream, measured by
    /// <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c> on regex 2026.9.10, 2026-09-21.
    /// </remarks>
    [Test]
    public void A_complete_match_beats_a_boundary_partial()
    {
        // regex.compile(r'True\b').match('True', partial=True) -> (0, 4), partial False
        Match complete = new FuzzyRegex(@"True\b").MatchAtStart("True", partial: true);

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 4));

        // The same within one scan: `a+` reaches `\B` at the end of 'aa' and fails there, then
        // backtracks to a shorter repeat that matches completely.
        // regex.compile(r'a+\B').match('aa', partial=True) -> (0, 1), partial False
        Match backtracked = new FuzzyRegex(@"a+\B").MatchAtStart("aa", partial: true);

        backtracked.PartialMatch.Should().BeFalse();
        (backtracked.Index, backtracked.Length).Should().Be((0, 1));
    }

    /// <summary>
    /// A boundary that fails without the attempt having consumed anything reports no partial, and
    /// this is where the port stops short of PCRE2.
    /// </summary>
    /// <remarks>
    /// PCRE2 escalates these: <c>\b</c> over '' is <c>PARTIAL (0,0)</c> there, with or without
    /// <c>PCRE2_NO_START_OPTIMIZE</c> (<c>tools/probes/pcre2-hitend-partial-span.py</c> sections D
    /// and E, PCRE2 10.47, 2026-09-21). This port follows upstream instead, for two reasons. A
    /// zero-width partial at the truncation point tells a caller nothing it did not already know -
    /// there is always more text that might match - and the port already pins upstream's own
    /// zero-width answer as wrong in <c>docs/DIVERGENCES.md</c> under <c>search-start-partial</c>,
    /// so producing one here would contradict that. Measured against upstream, which agrees on
    /// every row below:
    /// <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c>, regex 2026.9.10, 2026-09-21.
    /// </remarks>
    [Test]
    // Nothing to consume, so the boundary is the whole pattern and the attempt is empty.
    [Arguments(@"\b", "")]
    [Arguments(@"\b\b", "")]
    // `\B` fails at 1, the end of 'a', with the attempt there having started at 1 as well.
    [Arguments(@"\B", "a")]
    public void A_boundary_that_consumed_nothing_reports_no_partial(string pattern, string subject)
    {
        var compiled = new FuzzyRegex(pattern);

        compiled.Match(subject, partial: true).Success.Should().BeFalse();
        compiled.MatchAtStart(subject, partial: true).Success.Should().BeFalse();
        compiled.FullMatch(subject, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// The three rows the differential oracle found for this divergence, one at each seed, kept as
    /// the wave generated them.
    /// </summary>
    /// <remarks>
    /// The default wave of 2026-09-21 at seeds 7, 4242 and 20260921 diverged on one row each, and
    /// all three are this rule reaching a pattern nobody would write by hand: a conditional whose
    /// condition fails so the whole group matches empty, a fuzzy section whose budget goes unused, a
    /// reversed lookbehind. Each reduces to a boundary that runs out of context after the attempt
    /// has consumed text. They are pinned here whole rather than minimised, so that the
    /// <c>boundary-at-the-end-of-the-text</c> entry in
    /// <c>tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs</c> has tests carrying its own examples
    /// and a later engine change cannot quietly move them. Upstream answers no match to all three,
    /// which is what the wave recorded from regex 2026.9.10.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    // Seed 7, `partial` row 4867, search. `\m` is a word start, and there is none at the end of the
    // subject, which is two astral uppercase letters.
    [Arguments(@"^(\p{Lu}+?)(?(?<=\p{Nd})[A-Z])\m$", "\U00010400\U0001D518", "search", 4)]
    // Seed 4242, `fuzzy` row 6027, fullmatch. The deletion budget is never spent.
    [Arguments(@"(?fi)(?:[ab]+\B){d<=1}", "ba", "fullmatch", 2)]
    // Seed 20260921, `partial` row 4830, fullmatch. The lookbehind condition is false after 'b', so
    // the conditional contributes nothing and the pattern is `b\B`.
    [Arguments(@"b\B(?(?<![\w\s])\p{Ll})", "b", "fullmatch", 1)]
    public void A_boundary_partial_is_what_the_oracle_waves_found(
        string pattern,
        string subject,
        string door,
        int expectedLength
    )
    {
        var compiled = new FuzzyRegex(pattern);
        Match m = string.Equals(door, "search", StringComparison.Ordinal)
            ? compiled.Match(subject, partial: true)
            : compiled.FullMatch(subject, partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, expectedLength));
    }

    /// <summary>
    /// Under <c>(?r)</c> the end of the available text is its start, and the same divergence appears
    /// there.
    /// </summary>
    /// <remarks>
    /// A reversed match travels right to left, so it runs out of text at position 0, and the far end
    /// of a reversed partial is the slice start rather than the slice end
    /// (<c>Matcher.cs</c>, "We've matched up to the limit of the slice"). Upstream's rule turns round
    /// with it: rows 11 and 12 of
    /// <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c> are the reversed twin of the
    /// decisive pair, measured on regex 2026.9.10, 2026-09-21.
    /// <code>
    ///   '(?r)\Ba'  'a'   search=PARTIAL (0, 0)  match=None            fullmatch=None
    ///   '(?r)\Ba+' 'a'   search=PARTIAL (0, 1)  match=PARTIAL (0, 1)  fullmatch=PARTIAL (0, 1)
    /// </code>
    /// Both consume the 'a' backwards and then ask <c>\B</c> at 0. Only <c>a+</c> can ask for a
    /// character before the text and be told it has run out, so only <c>a+</c> gets a partial from
    /// upstream. This port answers a partial to both.
    /// </remarks>
    [Test]
    public void A_reversed_boundary_at_the_start_of_the_text_reports_a_partial_upstream_denies()
    {
        // DIVERGES FROM UPSTREAM, deliberately. Upstream answers None on both anchored doors.
        var denied = new FuzzyRegex(@"(?r)\Ba");

        foreach (Match m in new[] { denied.MatchAtStart("a", partial: true), denied.FullMatch("a", partial: true) })
        {
            m.PartialMatch.Should().BeTrue();
            (m.Index, m.Length).Should().Be((0, 1));
        }

        // The twin, where upstream agrees, so the test above is about the boundary rather than about
        // reversed partials in general.
        var granted = new FuzzyRegex(@"(?r)\Ba+");

        foreach (Match m in new[] { granted.MatchAtStart("a", partial: true), granted.FullMatch("a", partial: true) })
        {
            m.PartialMatch.Should().BeTrue();
            (m.Index, m.Length).Should().Be((0, 1));
        }
    }

    /// <summary>
    /// The reversed row the 6,000-row gate found, kept as the wave generated it.
    /// </summary>
    /// <remarks>
    /// Seed 20260921, <c>partial</c> row 98169 of the three-seed 6,000-row gate of 2026-09-21, the
    /// one reversed row of the four the slice's waves turned up. Read backwards the pattern matches
    /// the '\n' into group 1, has <c>\K</c> drop it from the reported span, matches the '\r' through
    /// the conditional's else branch, and then asks <c>\b</c> at position 0, where '\r' is not a word
    /// character and the boundary fails. Upstream answers no match on this door, which is what the
    /// wave recorded from regex 2026.9.10 and what row 13 of
    /// <c>tools/probes/upstream-partial-needs-text-exhaustion.py</c> re-measures.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_reversed_boundary_partial_is_what_the_gate_wave_found()
    {
        Match m = new FuzzyRegex(
            @"(?r)\b(?(?!\p{L}).|[^a])\K(\s)",
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version1
        ).FullMatch("\r\n", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));

        // No capture group survives the escalation, `\K` or no `\K`.
        m.Groups[1].Success.Should().BeFalse();
    }
}
