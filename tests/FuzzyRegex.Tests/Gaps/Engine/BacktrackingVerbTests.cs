using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S29's <c>(*PRUNE)</c> and <c>(*SKIP)</c>: what <c>(*SKIP)</c> moving the slice
/// start does to the <i>next</i> match, which the ported suite never looks at because every one of
/// its verb cases is a single <c>match</c>, and the atomic-group shape upstream issue 613 is about.
/// </summary>
/// <remarks>
/// Every expected value below was probed against <c>regex</c> 2026.7.19 on 2026-09-11 and is quoted
/// beside the assertion as Python spells it.
/// </remarks>
public sealed class BacktrackingVerbTests
{
    [Test]
    public void Skip_moves_the_slice_start_and_a_later_match_in_the_same_scan_sees_it()
    {
        // '(*SKIP)' sets slice_start to the text position (upstream/src/_regex.c:14555), and nothing
        // puts it back between matches of one scan - so the third match here leaves slice_start at
        // 4 and every later match reports pos=4.
        //
        // Measured: for m in regex.finditer(r'[A-Z]*(*SKIP)_', '__BB__B', overlapped=True), the
        // (span, m.pos) pairs are ((0,1),0) ((1,2),1) ((2,5),4) ((3,5),4) ((4,5),4) ((5,6),5).
        new FuzzyRegex("[A-Z]*(*SKIP)_")
            .Matches("__BB__B", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));

        // Without overlapping, the scan resumes where the match ended rather than one on from where
        // it started, so the moved slice start is never behind it:
        // [m.span() for m in regex.finditer(r'[A-Z]*(*SKIP)_', '__BB__B')] is
        // [(0, 1), (1, 2), (2, 5), (5, 6)].
        new FuzzyRegex("[A-Z]*(*SKIP)_")
            .Matches("__BB__B")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (1, 1), (2, 3), (5, 1));
    }

    [Test]
    public void The_scanner_does_not_carry_findalls_slice_start_guard()
    {
        // This is the shape S29's oracle wave caught, and it is the one case where upstream's own
        // 'findall' and 'finditer' disagree. Only pattern_findall guards its loop with
        // 'slice_start <= text_pos <= slice_end' (:22415); scanner_search_or_match (:20903),
        // pattern_subx (:21859) and pattern_split (:22285) leave it to do_match (:18128), which
        // compares text_pos against slice_end going forward and never against slice_start.
        //
        // Measured: regex.findall(r'[A-Z]*(*SKIP)_', '__BB__B', overlapped=True) gives three
        // matches, and regex.finditer on the same arguments gives six. This surface has no
        // 'findall' - Matches returns Match objects - so it is the scanner that it has to agree
        // with, and Count counts what Matches returns.
        new FuzzyRegex("[A-Z]*(*SKIP)_")
            .Matches("__BB__B", overlapped: true)
            .Should()
            .HaveCount(6);

        new FuzzyRegex("[A-Z]*(*SKIP)_").Count("__BB__B", overlapped: true).Should().Be(6);
    }

    [Test]
    public void NextMatch_walks_the_same_sequence_as_a_scan_when_skip_has_moved_the_slice_start()
    {
        // Match.NextMatch rebuilds a state from the *slice* the match was found in, so a moved
        // slice start is carried into it - and it has to be, because the walk would otherwise stop
        // at the third match where the scan does not. Same six spans as the test above. The walk is
        // seeded from the collection, which is what hands NextMatch the overlapped setting.
        MatchCollection collection = new FuzzyRegex("[A-Z]*(*SKIP)_").Matches("__BB__B", overlapped: true);

        List<(int Index, int Length)> walked = [];
        for (Match m = collection[0]; m.Success; m = m.NextMatch())
        {
            walked.Add((m.Index, m.Length));
        }

        walked.Should().Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));
    }

    [Test]
    public void Skip_inside_an_atomic_group_is_the_shape_upstream_issue_613_is_about()
    {
        // Upstream issue 613: '(*SKIP)' inside an atomic group leaves a stale backtrack limit, and a
        // specialised IGNORECASE scan loop then stops only on 'pos == limit' and walks off the
        // buffer - a heap-buffer-overflow READ in C. It cannot reproduce here yet for two reasons:
        // a .NET index is bounds-checked, and the scan loop in question is the string_search family
        // that Phase 7 has not ported. This pins what upstream answers *today* for the shape, so the
        // Phase 6 sweep has a behavioural anchor rather than only a report.
        //
        // NOT the issue's own reproduction: 'gh' is out of the driver's allowlist, so the issue text
        // could not be read from this session. These cases are derived from the one-line triage in
        // docs/plan/2026-08-31-upstream-issue-triage.md, and Phase 6 must still fetch the real one.
        //
        // Measured: regex.search(r'(?>a*(*SKIP))b', 'aaab') spans (0, 4), with and without
        // IGNORECASE, and the overlapped scan gives (0, 4), (1, 4), (2, 4), (3, 4).
        FuzzyRegex.Match("aaab", "(?>a*(*SKIP))b").Value.Should().Be("aaab");
        FuzzyRegex.Match("aaab", "(?>a*(*SKIP))b", FuzzyRegexOptions.IgnoreCase).Value.Should().Be("aaab");

        new FuzzyRegex("(?>a*(*SKIP))b")
            .Matches("aaab", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 4), (1, 3), (2, 2), (3, 1));

        // [m.span() for m in regex.finditer(r'(?>abc(*SKIP)d)|abc', 'ABCABC', regex.I)] is
        // [(0, 3), (3, 6)]: the SKIP is never reached, because 'd' fails inside the atomic group,
        // and the fallback branch matches at both positions.
        new FuzzyRegex("(?>abc(*SKIP)d)|abc", FuzzyRegexOptions.IgnoreCase)
            .Matches("ABCABC")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 3), (3, 3));

        // And reversed: regex.search(r'(?r)(?>[a-z]+(*SKIP))abc', 'abcabc', regex.I) spans (0, 6).
        // Under (?r) the verb moves slice_end rather than slice_start (:14553).
        FuzzyRegex
            .Match("abcabc", "(?r)(?>[a-z]+(*SKIP))abc", FuzzyRegexOptions.IgnoreCase)
            .Value.Should()
            .Be("abcabc");
    }

    [Test]
    public void Skip_past_a_required_string_tries_a_start_position_upstreams_prefilter_skips()
    {
        // PERMANENT: the port is right, see docs/plan/2026-09-12-divergence-research.md. A change
        // here is a regression. The earlier instruction to invert this test when Phase 7 lands is
        // WITHDRAWN (owner rule, 2026-09-12, DECISIONS and ROADMAP's Phase 7 entry): Phase 7 ports
        // upstream's prefilters without importing their answers, so a Phase 7 slice that turns this
        // red has ported a bug, and the fix is to make the prefilter honour the slice the verb moved
        // the way upstream's own slow path does.
        //
        // The second engine, quoted. PCRE2 10.47 called directly through libpcre2-8-0.dll
        // (tools/probes/pcre2-partial-and-skip.py, 2026-09-12) answers (4, 8) to the first case here,
        // with its own start optimiser on AND with PCRE2_NO_START_OPTIMIZE - so it agrees with this
        // port either way, and its own manual says why upstream and Perl do not: "When one of these
        // optimizations bypasses the running of a match, any included backtracking verbs will not, of
        // course, be processed... Experiments with Perl suggest that it too has similar
        // optimizations" (pcre2pattern, "Optimizations that affect backtracking verbs").
        //
        // Upstream's 'locate_required_string' (upstream/src/_regex.c:11082) moves the *first* attempt
        // to 'found_pos - req_offset', so positions before that are never tried. Skipping a position
        // that cannot match changes nothing - except that a '(*SKIP)' moves slice_start from wherever
        // the attempt began, so the attempt upstream skips is an attempt with a different answer.
        //
        // Measured against regex 2026.7.19 on 2026-09-11. Upstream's own compile call carries
        // req_offset=3, req_chars=('x',) for the first pattern (intercept regex._regex.compile), so
        // 'x' at 6 puts the first attempt at 3, the verb steps 3 -> 5, and position 4 is never tried:
        //
        //   regex.compile(r'(?:..(*SKIP)x|q)x').search('ab cd xx')     is None
        //   regex.compile(r'(?:..(*SKIP)x|q)x').match('ab cd xx', 4)   spans (4, 8)
        //
        // Perl agrees with upstream bit for bit ('use re "debug"' prints `Found floating substr "x"
        // at offset 6 (rx_origin now 3)'), and PCRE2 documents the class under "Optimizations that
        // affect backtracking verbs" - so this is not a bug on either side. This port has no
        // prefilter until Phase 7 and answers what upstream answers with its prefilter switched off.
        // When Phase 7 ports 'locate_required_string' the 'prefilter-free' wrapper in
        // tools/record-oracle.py goes at the same time - but these two assertions do NOT change,
        // because the prefilter has to be made to honour the moved slice rather than to reproduce
        // upstream's answer. Upstream's own '..(*SKIP)xx' case is the proof that its answer is wrong:
        // it retries at a position BELOW the one the verb committed past, which PCRE2's definition of
        // '(*SKIP)' forbids outright (upstream (1,5); PCRE2, Perl and this port (2,6)).
        FuzzyRegex
            .Match("ab cd xx", "(?:..(*SKIP)x|q)x")
            .Should()
            .Match<Match>(static m => m.Index == 4 && m.Length == 4);
        FuzzyRegex
            .Match("aaaaxx", "(?:aa(*SKIP)x|M)x")
            .Should()
            .Match<Match>(static m => m.Index == 2 && m.Length == 4);
    }

    [Test]
    public void Multiline_dollar_after_a_skip_reads_the_text_end_and_not_the_moved_slice()
    {
        // PERMANENT, and the OPPOSITE of what S29 and S33 concluded. Found 2026-09-11 by the S29
        // oracle wave (generator 'verbs', seed 20260913, rows 502, 504, 519 and 863); called "port
        // right by construction" then; REVERSED on 2026-09-12 by an independent, blind,
        // specification-grounded verification, and fixed here in S35.
        //
        // What settles it is the definition of '$', not either engine's internals. Under MULTILINE
        // '$' is true at the end of the text and before a newline, and nowhere else. In '\nb' at
        // position 1 the next character is 'b', so '$' is FALSE there, so the second match this port
        // used to report - (0, 1), whose reversed attempt tests '$' at 1 before consuming '\n'
        // backwards - could never have been right. Upstream reports the one match, and does so from
        // its 'search_start_END_OF_LINE_rev' fast path (upstream/src/_regex.c:8055), which bounds
        // itself with text_end; its own slow-path predicate 'try_match_END_OF_LINE' (:7108) bounds
        // itself with SLICE_end and has the identical fault. S29 read that internal disagreement as
        // upstream being inconsistent and this port being right; the disagreement is real, but it is
        // the slow path that is wrong on both sides.
        //
        // Mechanism: '(*SKIP)' under '(?r)' sets slice_end to the text position (:14545, ported at
        // Matcher.cs's RE_OP_SKIP arm) so the next attempt starts there. A verb moves where the next
        // attempt STARTS and nothing else - PCRE2 pcre2pattern, "Verbs that act after backtracking" -
        // so an assertion about the text must not read a bound a verb has moved. Every other
        // zero-width assertion here already reads text_start/text_end; END_OF_LINE was the one that
        // did not, and it now does too (Matcher.cs, TryMatchEndOfLine).
        //
        // Measured against regex 2026.7.19 and 2026.9.10 on 2026-09-12:
        //
        //   [m.span() for m in regex.finditer(r'(?r)(?:a*(*SKIP)b|[^a-f])$', '\nb', regex.M)]
        //   is [(1, 2)] - one match. This port agreed after S35 and found (1,1) then (0,1) before.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\nb")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1));

        // The control that says the fix did not simply make the engine find fewer matches: upstream
        // gives [(6, 7), (4, 5)] here and the port used to insert a spurious (5, 1) between them.
        // Both of upstream's matches survive.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\rbbb\r\nb")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((6, 1), (4, 1));

        // With no '(*SKIP)' the slice never moves, so this shape could never have diverged and must
        // still not: it is the isolating control for the verb itself.
        new FuzzyRegex("(?r)(?:a*b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\nb")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1));

        // '$' without MULTILINE is END_OF_STRING_LINE, whose try_match (:7127) already bounded itself
        // with text_end and final_newline, so it never diverged and must not start.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$")
            .Matches("\nb")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1));

        // '\b' after the same verb finds two matches on both sides, and still must: the boundary
        // between '\n' and 'b' is a real one, so (0, 1) is a correct match here where it was not
        // above. This is what distinguishes "the assertion read a moved bound" from "reverse scans
        // over-report".
        new FuzzyRegex(@"(?r)(?:a*(*SKIP)b|[^a-f])\b", FuzzyRegexOptions.Multiline)
            .Matches("\nb")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1), (0, 1));

        // Forward '(*SKIP)' moves slice_start, and '^' under MULTILINE reads text_start, so the
        // mirror shape has to agree with upstream as well:
        // [m.span() for m in regex.finditer(r'^(?:b(*SKIP)a*|[^a-f])', 'b\n', regex.M)] is [(0, 1)].
        new FuzzyRegex("^(?:b(*SKIP)a*|[^a-f])", FuzzyRegexOptions.Multiline)
            .Matches("b\n")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1));
    }

    [Test]
    public void Prune_leaves_the_slice_alone_where_skip_moves_it()
    {
        // The two verbs are the same opcode body but for the two lines SKIP has first (:14552-14555),
        // so the only thing that can tell them apart is the slice - and the only place the slice
        // shows is the next match of a scan.
        //
        // Measured: [m.span() for m in regex.finditer(r'[A-Z]*(*PRUNE)_', '__BB__B',
        // overlapped=True)] is the same six spans as the SKIP version, because the moved slice start
        // never gets in the scanner's way; but m.pos stays 0 on all six where SKIP's reads
        // 0, 1, 4, 4, 4, 5. That is also why findall agrees with finditer here and not there:
        // regex.findall on this pattern gives six matches overlapped, against SKIP's three.
        new FuzzyRegex("[A-Z]*(*PRUNE)_")
            .Matches("__BB__B", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void An_overlapped_reversed_scan_of_a_skip_keeps_every_span_where_upstreams_own_single_shot_door_puts_it()
    {
        // The reversed half of the test below, and the two rows left over when S35 deleted the
        // 'search-start-skip-slice' entry: they were classified under that entry's verdict, they are
        // not the '$'-reads-the-slice defect S35 fixed, and they are not a prefilter at all. Under
        // '(?r)' a '(*SKIP)' moves slice_end (upstream/src/_regex.c:14545) and nothing puts it back
        // between the matches of one scan, so upstream's next attempt starts later than it should and
        // every span it reports moves right.
        //
        // UPSTREAM IS WRONG HERE, on three measured counts (regex 2026.7.19, 2026-09-12).
        //
        // One: its own single-shot door gives THIS PORT's answer.
        //   regex.compile(pat).search('AAAA00', 0, 5)   is (0, 5) with g1 at (4, 5)
        // where its overlapped scan reports g1 at (5, 6) for the same match.
        //
        // Two: (5, 6) lies OUTSIDE the match (0, 5) it belongs to, and there is no lookaround in the
        // pattern that could put a capture there.
        //
        // Three: the scan is memory-unsafe, not merely wrong. Printing each match as it arrives
        // SEGFAULTS the interpreter - exit 139, .scratch/s35-row863b.py, the quiet list-comprehension
        // form completing first and printing the two spans above. That is the same instability the
        // forward case records as a gc.collect() between iterations changing the answer.
        //
        // Found by the S29 wave at seed 20260913, rows 863 and 519, and quoted here as the wave draws
        // them rather than minimised: a shorter pattern loses the second '(*SKIP)' that makes the
        // carry-over observable, and a reproduction nobody can run is not evidence.
        MatchCollection first = new FuzzyRegex(
            @"(?r)(?:\p{L}+(*SKIP)\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\w\s]))"
        ).Matches("AAAA00", overlapped: true);

        first.Select(static m => (m.Index, m.Length)).Should().Equal((0, 6), (0, 5));
        first.Select(static m => (m.Groups["g1"].Index, m.Groups["g1"].Length)).Should().Equal((5, 1), (4, 1));

        // Row 519, upstream: (2,2) (2,1) (0,2), the middle one starting where the first did and one
        // character long for a pattern whose minimum width is two. This port walks the scan down.
        new FuzzyRegex(
            @"(?r)((?:\s*(*SKIP)[[:alpha:]]|[[:digit:]]))(?:\S{2,4}(*PRUNE)[a\d]|ı)",
            FuzzyRegexOptions.IgnoreCase
        )
            .Matches("ıııı\r\nS", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((2, 2), (1, 2), (0, 2));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void An_overlapped_reversed_scan_of_a_skip_stops_where_upstreams_own_extra_matches_refute_themselves()
    {
        // The three rows S35 left for S36 to judge, all found by the `verbs` generator at 2000 rows
        // (seeds 4242 and 7). Same mechanism as the test above - under '(?r)' a '(*SKIP)' moves
        // slice_end (upstream/src/_regex.c:14545) and nothing puts it back between the matches of one
        // scan - but seen as upstream reporting MATCHES THIS PORT DOES NOT, rather than as spans that
        // moved right. Measured 2026-09-12 against regex 2026.7.19, recorded prefilter-free as the
        // `verbs` generator always is; the probe is
        // tools/probes/upstream-reversed-overlapped-skip.py.
        //
        // ONE: the assertion case, row 1567, minimised from an eight-codepoint astral subject to
        // three ASCII characters. Upstream's second match needs '$' to hold at index 2 of 'bxA',
        // where the subject has an 'A'.
        //
        //   regex.finditer(r'(?r)(?:.{2}(*SKIP)A|x)$', 'bxA', regex.M, overlapped=True)
        //   # upstream (0, 3) then (1, 2); ours (0, 3) alone
        //
        // The control is the same pattern with the verb removed, and with it replaced by '(*PRUNE)',
        // which moves no bound: both give (0, 3) alone, upstream included. So the extra match exists
        // only because '(*SKIP)' moved slice_end to 2 and upstream's '$' read it - which is exactly
        // the defect S35 fixed on this side, where 'TryMatchEndOfLine' now reads TextEnd. Asked
        // whether '$' can hold there at all with no verb in the pattern, upstream says no:
        // regex.finditer(r'(?r)\U0001F600$', subject, regex.M, overlapped=True) finds nothing on the
        // original row's subject, where its scan of the '(*SKIP)' pattern reported a match ending
        // inside it.
        new FuzzyRegex("(?r)(?:.{2}(*SKIP)A|x)$", FuzzyRegexOptions.Multiline)
            .Matches("bxA", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 3));

        // TWO: the capture case, row 1863. Upstream's extra match is (0, 5) and it carries group 2 at
        // (4, 6) - a capture ending one character OUTSIDE the match it belongs to, in a pattern with
        // no lookaround that could put one there. Its own doors deny the match: search('b0 0\n A',
        // 0, 5) and match('b0 0\n A', 0, 5) are both None. Removing the verbs, or making them
        // '(*PRUNE)', removes the extra match from upstream too.
        MatchCollection captureCase = new FuzzyRegex(@"(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))").Matches(
            "b0 0\n A",
            overlapped: true
        );

        captureCase.Select(static m => (m.Index, m.Length)).Should().Equal((0, 6));
        captureCase
            .Select(static m => (m.Groups[1].Index, m.Groups[1].Length, m.Groups[2].Index, m.Groups[2].Length))
            .Should()
            .Equal((0, 4, 4, 2));

        // THREE: row 1439, the same capture tell twice over - upstream's two extra matches are (0, 5)
        // and (0, 4) and both carry group 1 at (5, 7). The S36 slice file suspected this row's ground
        // truth depended on CALL ORDER, because the wave recorded three matches where a run on the row
        // alone gave one. It does not: `verbs` rows are recorded with upstream's required-string
        // prefilter off (tools/record-oracle.py) and the isolated run was not. Recompiled
        // prefilter-free, upstream gives the same three matches every time, before and after a
        // gc.collect(). So the recorder needs no per-row isolation, and this is one family, not two.
        MatchCollection prefilterCase = new FuzzyRegex(
            @"(?r)(?:[a\d]*(*SKIP)\D|\p{Nd})(?:[\p{L}\p{N}]{1,3}(*SKIP)\S|.)((?>\s+(*PRUNE)A))"
        ).Matches("İİAAA AS", overlapped: true);

        prefilterCase.Select(static m => (m.Index, m.Length)).Should().Equal((0, 7));
        prefilterCase.Select(static m => (m.Groups[1].Index, m.Groups[1].Length)).Should().Equal((5, 2));

        // FOUR: the capture tell again, in a pattern that holds a NEGATIVE lookbehind - row 5543 of a
        // 6000-row seed-7 `interactions` wave, added by S37. Upstream's extra match is (3, 5) with g2
        // at (5, 6), one character outside it. Deleting the verb, or making it '(*PRUNE)', leaves
        // upstream with (3, 6) alone; writing the called group out leaves the extra match; and
        // upstream's own search and match at every start position answer (3, 6) and never (3, 5)
        // (measured 2026-09-12, and unchanged against 2026.9.10).
        //
        // It is here because the classifier used to refuse it. `CarriesACaptureOutsideItself` in
        // ExpectedDivergences.cs excluded any pattern holding a lookaround, and a NEGATIVE lookaround
        // cannot put a capture anywhere: it only succeeds when its body fails, so nothing it matched
        // survives. Upstream and this port agree on that, which is the other half of the assertion
        // below and what the widened clause rests on.
        MatchCollection negativeLookaroundCase = new FuzzyRegex(
            @"(?r)(?:\D{1,1}(*SKIP)[\p{ASCII}&&\p{L}]|[[a-f]~~[d-k]])(?P<g1>.*)??(?P<g2>[A])(?:(?(2)(?<!(?&g2))\p{Nd}))\b",
            FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline
        ).Matches("A\r\nAAA", overlapped: true);

        negativeLookaroundCase.Select(static m => (m.Index, m.Length)).Should().Equal((3, 3));

        // regex.search(r'(a)(?!(?:(b))x)b', 'ab') is (0, 2) with group 1 ['a'] and group 2 EMPTY, and
        // regex.search(r'(?!(a))b', 'b') is (0, 1) with group 1 empty.
        Match negated = new FuzzyRegex("(a)(?!(?:(b))x)b").Match("ab");

        (negated.Index, negated.Length).Should().Be((0, 2));
        negated.Groups[1].Value.Should().Be("a");
        negated.Groups[2].Success.Should().BeFalse("a negative lookaround that succeeds captured nothing");
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void An_overlapped_scan_of_a_skip_inside_a_bounded_repeat_matches_what_upstreams_own_matcher_accepts()
    {
        // Found by S33's blind review, running `verbs` at seeds S29 never used, and judged in S34.
        // A `(*SKIP)` moves `slice_start` mid-attempt (upstream/src/_regex.c:14553) and nothing puts
        // it back - `init_match` (:3404), `do_match` (:18121) and `scanner_search_or_match` (:20874)
        // all leave it alone - so a scanner carries it from one match into the next. An overlapped
        // scan then resumes BELOW it, at `match_pos + 1` (:20903), which no other path can produce.
        //
        // UPSTREAM IS WRONG HERE, on three counts, and none of them is a judgement call.
        //
        // One: its own doors disagree. regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ',
        // regex.M, overlapped=True) gives (0,3) (1,4) (2,4) (3,4) (4,7) (5,7), while its own
        // `match` at positions 0..5 gives (0,3) (1,4) (2,5) (3,6) (4,7) (5,7) - which is this test's
        // first expectation, span for span.
        //
        // Two: it returns a match NARROWER THAN THE PATTERN'S MINIMUM WIDTH. On the smaller case
        // below, '(?:[^\d](*SKIP)){2}' must match two characters and upstream's third match is one:
        //
        //     regex.compile(r'(?:[^\d](*SKIP)){2}').finditer('abcde', overlapped=True)
        //     # (0,2) (1,3) (2,3) (3,5)   - and its own .match('abcde', 2) is (2, 4)
        //
        // Three: its answer is not stable. A `gc.collect()` between iterations of the first case
        // changes it to (0,3) (3,6) (4,7) (5,7), and an `open()` to (0,3) (3,6) - three different
        // answers to one call, varying only in unrelated interleaved work (tools/probes/upstream-overlapped-skip-instability.py,
        // 2026-09-12).
        //
        // Upstream has since patched one consequence of exactly this carry-over: commit b77694a,
        // issue 613, which clamps the retreat limit in GREEDY_REPEAT_ONE's backtrack down to the
        // current position, because a stale slice could raise that limit above it and make the
        // equality-only stop unreachable, letting a repeat unmatch below its minimum. That is in
        // the 2026.8.30 release, past the pin - THIS PORT STILL CARRIES THE PRE-FIX CODE at
        // Matcher.cs's GreedyRepeatOne backtrack arm, and the Phase 6 sync is what ports it,
        // test-first. Classified in the oracle as `overlapped-skip-stale-slice`.
        new FuzzyRegex("(?:[^\\d](*SKIP)){2,3}", FuzzyRegexOptions.Multiline)
            .Matches("\r\naabb ", overlapped: true)
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 3), (1, 4), (2, 5), (3, 6), (4, 7), (5, 7));

        new FuzzyRegex("(?:[^\\d](*SKIP)){2}")
            .Matches("abcde", overlapped: true)
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 2), (1, 3), (2, 4), (3, 5));

        // Every span above is one this port also produces when asked at that position on its own,
        // which is what makes the scan self-consistent where upstream's is not. Asserted rather
        // than said, because self-consistency is the whole claim.
        var bounded = new FuzzyRegex("(?:[^\\d](*SKIP)){2}");
        Enumerable
            .Range(0, 4)
            .Select(i => bounded.MatchAtStart("abcde", i))
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 2), (1, 3), (2, 4), (3, 5));

        // The control: with the verb removed the slice never moves, and upstream's overlapped scan
        // agrees with this port - regex.finditer(r'(?:[^\d]){2,3}', '\r\naabb ', regex.M,
        // overlapped=True) is the same six spans as the first assertion.
        new FuzzyRegex("(?:[^\\d]){2,3}", FuzzyRegexOptions.Multiline)
            .Matches("\r\naabb ", overlapped: true)
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 3), (1, 4), (2, 5), (3, 6), (4, 7), (5, 7));
    }
}
