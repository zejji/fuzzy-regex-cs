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
        // Match.NextMatch rebuilds a state from the *slice* the match was found in, and that slice
        // is the one the CALLER asked for - `FuzzyRegex.NewMatch` records
        // `InitialSliceStart`/`InitialSliceEnd`, not whatever a `(*SKIP)` left behind. Same six
        // spans as the test above. The walk is seeded from the collection, which is what hands
        // NextMatch the overlapped setting.
        MatchCollection collection = new FuzzyRegex("[A-Z]*(*SKIP)_").Matches("__BB__B", overlapped: true);

        List<(int Index, int Length)> walked = [];
        for (Match m = collection[0]; m.Success; m = m.NextMatch())
        {
            walked.Add((m.Index, m.Length));
        }

        walked.Should().Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));

        // A SECOND SHAPE, because the one above cannot tell the two slices apart and this one can.
        // S40a's blind review found exactly that: when `DoMatch` began putting the slice back at the
        // start of every match, `Matches` picked up the match at 3 and a `NextMatch` walk did not,
        // because `NewMatch` was still handing the MOVED slice on and `MatchState.Create` then
        // recorded it as the one to restore. The scan said (0,4) (1,3) (3,1) (4,0) and the walk said
        // (0,4) (1,3) (4,0), which is the invariant in Match.NextMatch's own remarks broken on one
        // path. Upstream's answer is the scan's: regex.finditer(r'\b(?:[^a](*SKIP))*', 'b\n\rS',
        // overlapped=True) gives spans (0,4) (1,4) (3,4) (4,4).
        var carried = new FuzzyRegex(@"\b(?:[^a](*SKIP))*");
        MatchCollection scan = carried.Matches("b\n\rS", overlapped: true);

        List<(int Index, int Length)> scanned = [.. scan.Select(static m => (m.Index, m.Length))];
        scanned.Should().Equal((0, 4), (1, 3), (3, 1), (4, 0));

        List<(int Index, int Length)> walkedAgain = [];
        for (Match m = scan[0]; m.Success; m = m.NextMatch())
        {
            walkedAgain.Add((m.Index, m.Length));
        }

        walkedAgain.Should().Equal(scanned, "NextMatch and Matches must walk the same sequence");
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

        // FIVE: neither tell exists - row 117071 of the 6000-row seed-4242 gate, added by S40d. The
        // pattern has no '$' and no groups at all, so there is no trailing assertion to falsify and no
        // capture to find outside its match. What refutes upstream is its OWN reversed search over the
        // truncated subject, which is legitimate HERE and nowhere near it in general: the pattern holds
        // no end-sensitive item, so moving `endpos` cannot change what anything in it means.
        //
        //   pat, S = r'(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}', '_ ___\U00010400\U00010400\U00010400'
        //   upstream's scan  (3, 8) (3, 7) (3, 6)      # codepoints, prefilter-free
        //   its own search(S, 0, 8)  (3, 8)            # the match this port also finds
        //   its own search(S, 0, 7)  None              # for a match it reports as ending at 7
        //   its own search(S, 0, 6)  None              # and at 6
        //
        // Making both verbs '(*PRUNE)' leaves upstream with (3, 8) alone; deleting them leaves (3, 8)
        // and (3, 7). Measured 2026-09-13, tools/probes/upstream-reversed-skip-scan-shapes.py, and the
        // walk itself is tools/probes/upstream-reversed-walk-step.py.
        new FuzzyRegex(@"(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}", FuzzyRegexOptions.Multiline)
            .Matches("_ ___\U00010400\U00010400\U00010400", overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((3, 8));

        // SIX: the same family through a SUBSTITUTION - row 116388 of the 6000-row seed-20260913 gate,
        // added by S40d. Upstream replaces three times and this port once. The '$' tell settles it once
        // the spans are asked for separately: upstream replaces at codepoint (3, 4), (2, 3) and (1, 2),
        // and '$' is true at the end of 'aa\U0001D518\U0001D518' and nowhere else, so two of the three
        // need '$' where the subject has a character. Making the verb '(*PRUNE)' leaves upstream
        // replacing once, at (3, 4), which is this port's answer.
        string replaced = new FuzzyRegex(
            "(?r)(?:\\d*?(*SKIP)\U0001D518|a)$",
            FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        ).Replace("aa\U0001D518\U0001D518", "<\\t", -1, out int replacements);

        replacements.Should().Be(1);
        replaced.Should().Be("aa\U0001D518<\t");
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void An_overlapped_reversed_scan_of_a_skip_keeps_the_match_upstreams_own_stepwise_door_still_finds()
    {
        // The THIRD and last symptom of the reversed carried slice (ledger entry 5), found by S40d at
        // seed 20260914 while re-running a control at a seed no slice had used - which is VERIFICATION
        // rule 7a one level up. The two tests above have upstream inventing matches or moving spans;
        // here the moved 'slice_end' is too SHORT, so upstream's next attempt runs in a view of the
        // subject that cannot hold the match and its scan ends one match early.
        //
        //   pat, S = r'(?r)\p{Lu}*(*SKIP)B(?P<g1>(?:\D{2,4}(*SKIP)a|.))', 'B_\ra'
        //   upstream's overlapped scan          (0, 4)                    # prefilter-free
        //   both verbs made '(*PRUNE)'          (0, 4) (0, 2)             # no bound moves
        //   both verbs deleted                  (0, 4) (0, 2)
        //   its own search(S, 0, 4)             (0, 4)
        //   its own search(S, 0, 3)             (0, 2)                    # the match its scan loses
        //
        // Measured 2026-09-13 against regex 2026.7.19,
        // tools/probes/upstream-reversed-skip-scan-shapes.py.
        //
        // THIS PORT GIVES THE SAME ANSWER WITH THE VERBS DELETED, and that is the point rather than a
        // weakness in the assertion - S40d's second blind review raised it, so here is what was
        // measured. The port answers (0, 4) and (0, 2) for '(*SKIP)', for '(*PRUNE)' and for no verb
        // at all, because a verb's moved slice does not survive into the next match here. What the
        // assertion is a second alarm for is precisely that: delete `state.SliceEnd =
        // state.InitialSliceEnd;` from Matcher.cs's per-match reset and this test fails (measured,
        // `--treenode-filter` on this method alone: total 1, failed 1). The sibling that pins the
        // other half - a verb still moving the slice for the rest of its OWN attempt - is
        // `Skip_moves_the_slice_start_and_a_later_match_in_the_same_scan_sees_it`.
        MatchCollection lost = new FuzzyRegex(@"(?r)\p{Lu}*(*SKIP)B(?P<g1>(?:\D{2,4}(*SKIP)a|.))").Matches(
            "B_\ra",
            overlapped: true
        );

        lost.Select(static m => (m.Index, m.Length)).Should().Equal((0, 4), (0, 2));
        lost.Select(static m => (m.Groups["g1"].Index, m.Groups["g1"].Length)).Should().Equal((1, 3), (1, 1));
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

    [Test]
    public void A_skip_inside_an_atomic_group_after_an_optional_item_answers_where_upstream_loops_for_ever()
    {
        // S40a. Found at 6000 rows a generator, `verbs` row 5944 at seed 4242, where it killed the
        // whole wave silently - the recorder had no per-row deadline until this slice, so a row
        // upstream never finishes meant no wave file at all. Minimised by hand from
        // `[^a]?\U0001f600(?>[a\d]{1,3}(*SKIP)\p{Ll})` to four characters.
        //
        // Measured: regex.compile('.?x(?>a(*SKIP)z)').search('xzxa') never returns on 2026.7.19,
        // and answers None on 2026.9.10.
        //
        // UPSTREAM'S BUG, AND UPSTREAM HAS ALREADY FIXED IT: commit b77694a, issue 613, "(*SKIP)
        // inside an atomic group, plus an equality-only scan stop", released in 2026.8.30 - past
        // the pin. It clamps the retreat limit down to the current position - one new line per
        // direction in the GREEDY_REPEAT_ONE backtrack arm - which is what stops the retreat loop
        // running away once a (*SKIP) has raised that limit above the position the retreat starts
        // from, leaving the arm's equality-only stop unreachable. There is nothing to file, ledger
        // entry 10 records it.
        //
        // S44 PORTED BOTH CLAMPS with the rest of the 2026.9.10 sync, and measured what they change
        // here, because "ported faithfully" and "changes nothing" are different claims:
        //
        //   - the branch IS live. A probe throwing wherever the new clamp would fire was hit by
        //     exactly one test in the 5,875 - this one - at pos=2, limit=4, sliceStart=4.
        //   - the unclamped retreat DOES find a tail match below that limit (pos=1), so the clamp
        //     is not merely bounding a walk that was already stopping on its own.
        //   - and it still changes no ANSWER anywhere measured: the 1,296-call grid below agrees
        //     with 2026.9.10 row for row BOTH before and after the clamps, and the full suite and
        //     every oracle wave are unchanged by them.
        //
        // So the clamps are here for the reason upstream added them - an unbounded retreat that
        // reads off the end of the buffer in C - and not because this port answered anything
        // differently. A future sync that finds an answer moving on this shape should treat that
        // as news rather than as this fix arriving late.
        //
        // The bounded timeout is the assertion. A regression into upstream's loop would otherwise
        // hang the whole suite instead of failing one test, which is the same reason
        // OracleComparer.RowTimeout exists. Five seconds against a four-character subject.
        var bounded = new FuzzyRegex(".?x(?>a(*SKIP)z)", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5));

        bounded.Match("xzxa").Success.Should().BeFalse("2026.9.10 answers None and 2026.7.19 answers nothing at all");

        // The three controls that isolate the shape, each of which upstream answers on BOTH
        // versions: it needs all of a leading optional item, an atomic group, and (*SKIP) inside.
        // They are here so a future reader can tell this test pins the hanging shape rather than
        // "some pattern with a verb in it".
        new FuzzyRegex(".?x(?>a(*PRUNE)z)")
            .Match("xzxa")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex(".?x(?:a(*SKIP)z)").Match("xzxa").Success.Should().BeFalse();
        new FuzzyRegex("x(?>a(*SKIP)z)").Match("xzxa").Success.Should().BeFalse();

        // And the shape swept rather than sampled: "it does not hang on one row" is not "it cannot
        // hang". `tools/probes/upstream-skip-in-atomic-hang.py --grid` puts the identical
        // 1296-call grid to upstream, where 2026.7.19 hangs on 70 of them and 2026.9.10 on none
        // (measured 2026-09-13). The two rows below are the sharpest of those 70 - a bounded
        // repeat rather than `.?`, which is where upstream's runaway is widest - and each is
        // answered here in microseconds.
        //
        // THE WHOLE GRID IS RE-RUNNABLE against this port, which is what S40a could not leave
        // behind (its grid script was scratch and is gone, so only the hang count survived):
        //
        //   python tools/probes/upstream-skip-in-atomic-hang.py --oracle-rows > .scratch/grid.jsonl
        //   pwsh -File tools/run-oracle.ps1 -Rows .scratch/grid.jsonl
        //
        // S44 ran it either side of porting the clamps: agree 1296, diverge 0, both times.
        new FuzzyRegex(".{1,3}x(?>[ab](*SKIP)z)", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5))
            .Match("xzxa")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex(".{1,3}x(?>a{1,2}(*SKIP)z)", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5))
            .Match("xzxaa")
            .Success.Should()
            .BeFalse();
    }

    /// <summary>
    /// Seed 20260915 row 24224 of S52's wave: a reversed <c>split</c> whose second separator
    /// upstream ends where its own <c>$</c> is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seven of upstream's eight text-edge predicates read a TEXT bound and
    /// <c>try_match_END_OF_LINE</c> (<c>upstream/src/_regex.c:7110</c>) alone reads
    /// <c>slice_end</c> - which is the field <c>RE_OP_SKIP</c> writes under <c>(?r)</c>
    /// (<c>:14553</c>). The seven include <c>$</c>'s own Unicode twin,
    /// <c>try_match_END_OF_LINE_U</c> (<c>:7117</c>, through <c>at_line_end</c> at <c>:922</c> and
    /// <c>:1966</c>), so upstream's <c>$</c> disagrees with itself in one file. S35 made every
    /// assertion here read the text bound.
    /// </para>
    /// <para>
    /// Measured 2026-09-15 on regex 2026.9.10,
    /// <c>tools/probes/upstream-skip-carried-slice-doors.py</c>: upstream's own <c>$</c> is true at
    /// 3 and 10 alone, asked one anchored position at a time, and its split takes separators (8, 10)
    /// and (3, 8) - the second ending at 8. Spelling <c>$</c> out as what <c>$</c> is defined to be,
    /// or spelling the verb <c>(*PRUNE)</c>, or deleting it, each leaves upstream with the one
    /// separator this port finds. Classified by
    /// <c>ExpectedDivergences.end-of-line-reads-a-skip-moved-slice</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void A_reversed_split_of_a_skip_does_not_end_a_separator_where_the_line_does_not_end()
    {
        // Flags 0x8, MULTILINE, and Version0 because the recorder resolves a version-less pattern
        // under upstream's own default.
        const string subject = "ﬀﬀ\r\nﬀﬀss\rS";
        FuzzyRegexOptions options = FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version0;

        new FuzzyRegex(@"(?r)(?:\s*?(*SKIP)\W|[^a])(\S{1,})$", options)
            .Split(subject, -1)
            .Should()
            .Equal("", "S", "ﬀﬀ\r\nﬀﬀss");

        // Control one: the verb spelled (*PRUNE), which prunes the same backtracking and moves no
        // bound. Upstream answers this too, which is what says the bound is the cause.
        new FuzzyRegex(@"(?r)(?:\s*?(*PRUNE)\W|[^a])(\S{1,})$", options)
            .Split(subject, -1)
            .Should()
            .Equal("", "S", "ﬀﬀ\r\nﬀﬀss");

        // Control two, and the one that names the defect: `$` written out as end-of-text-or-before-
        // a-line-terminator, in a spelling no slice bound can answer. Upstream agrees here.
        new FuzzyRegex(@"(?r)(?:\s*?(*SKIP)\W|[^a])(\S{1,})(?:(?=\n)|(?!\n|.))", options)
            .Split(subject, -1)
            .Should()
            .Equal("", "S", "ﬀﬀ\r\nﬀﬀss");

        // Control three: this port's own `$` is false at 8, so the separator upstream reports is
        // one this engine could not have made whatever the scan did. The subject is all BMP, so
        // these indices are both codepoints and UTF-16 code units.
        new FuzzyRegex("$", options)
            .MatchAtStart(subject, 8, -1)
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("$", options).MatchAtStart(subject, 3, -1).Success.Should().BeTrue();

        // AND THE CONTROL THAT DOES NOT WORK HERE, asserted rather than described so nobody adds it
        // later. `(?w)` swaps `$` for END_OF_LINE_U, the twin that reads `text_end`, which is the
        // sharpest control this family has - see
        // A_reversed_substitution_of_a_skip_replaces_nothing_where_the_line_does_not_end. It moves
        // the line ends on that row too, so that is not what disqualifies it here; what does is
        // that on THIS row the phantom separator's own end, 8, is one of the positions it moves.
        // `(?w)` makes 8 a genuine line end, so a `(?w)` run could not tell a bound that stopped
        // being read from a line end that started existing.
        new FuzzyRegex("(?w)$", options)
            .MatchAtStart(subject, 8, -1)
            .Success.Should()
            .BeTrue();
    }

    /// <summary>
    /// Seed 20260915 row 38101 of S52's wave: the same <c>$</c> defect on a reversed substitution,
    /// and inside a SINGLE search rather than across the matches of a scan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's own <c>$</c> is true at 2 and 10 alone, and it reports one match ending at 5.
    /// Nothing crosses between matches here: a single <c>search(subject, 0, 10)</c> gives (0, 5) as
    /// drawn and <c>None</c> with the verb spelled <c>(*PRUNE)</c>, so the bound a FAILED attempt
    /// moved is read by a later attempt inside one call.
    /// </para>
    /// <para>
    /// The wave recorded upstream's answer as an <c>IndexError</c>, and that is incidental: the
    /// drawn template holds <c>{0[-1]}</c>, which a match object answers with one, so upstream
    /// raises precisely when it finds a match and returns the subject unchanged when it does not.
    /// The divergence is whether a match exists. Measured 2026-09-15,
    /// <c>tools/probes/upstream-skip-carried-slice-doors.py</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void A_reversed_substitution_of_a_skip_replaces_nothing_where_the_line_does_not_end()
    {
        // Flags 0xA: IGNORECASE and MULTILINE, plus Version0 for the recorder's default.
        const string subject = "a\r\na\U0001D518\U0001D518\U00010428\r\U00010428\U0001D518";
        const string drawn = @"(?r)(\D+(*PRUNE)[^\p{L}])(?:[^a-f](*PRUNE)){1,3}?((?>\p{Lu}{1,3}?(*SKIP)\D))$";
        FuzzyRegexOptions options =
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version0;

        string replaced = new FuzzyRegex(drawn, options).ReplaceFormat(
            subject,
            "-{0[0]}{0[-1]}{0[-2]}",
            -1,
            out int replacements
        );

        replacements.Should().Be(0);
        replaced.Should().Be(subject);

        // There is no match to replace, which is the whole of it - and the three controls say why.
        new FuzzyRegex(drawn, options)
            .Match(subject)
            .Success.Should()
            .BeFalse();

        // Control one: the (*SKIP) spelled (*PRUNE). Upstream answers None here too.
        new FuzzyRegex(drawn.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), options)
            .Match(subject)
            .Success.Should()
            .BeFalse();

        // Control two: `$` spelled out. Upstream answers None here too.
        new FuzzyRegex(drawn.Replace("$", "(?:(?=\n)|(?!\n|.))", StringComparison.Ordinal), options)
            .Match(subject)
            .Success.Should()
            .BeFalse();

        // Control three: this port's `$` is false where upstream's only match ends, and true at the
        // two positions upstream's own anchored `$` agrees with. UPSTREAM'S (0, 5) IS IN CODEPOINTS
        // and this subject is astral, so the end to ask about is UTF-16 6 - index 5 is the low
        // surrogate of the first U+1D518 and would be false for a reason that is not the finding.
        // Upstream's `$` is true at codepoints 2 and 10, which are UTF-16 2 and 15.
        new FuzzyRegex("$", options)
            .MatchAtStart(subject, 6, -1)
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("$", options).MatchAtStart(subject, 2, -1).Success.Should().BeTrue();
        new FuzzyRegex("$", options).MatchAtStart(subject, 15, -1).Success.Should().BeTrue();

        // Control four, the sharpest and the one that names the predicate: `(?w)` compiles `$` to
        // END_OF_LINE_U, the twin that reads `text_end` - and upstream then answers None, this
        // port's answer, where its plain `$` found the phantom.
        //
        // WHY IT IS A CONTROL HERE AND NOT ON THE REVERSED SPLIT ABOVE, because the obvious reason
        // is the wrong one. `(?w)` moves the line ends on BOTH rows: here `(?w)$` is true at
        // codepoints 1, 7 and 10 where `$` is true at 2 and 10, and on the split row it is true at
        // 2, 8 and 10 where `$` is true at 3 and 10. What decides it is the PHANTOM POSITION alone -
        // there, 8 becomes a genuine `(?w)` line end and the control cannot tell a bound that
        // stopped being read from a line end that started existing; here, codepoint 5 is a line end
        // under neither spelling, so None means the bound was the only thing holding the match up.
        // The two assertions below are that fact rather than the prose: UTF-16 6 is codepoint 5.
        new FuzzyRegex("(?w)$", options)
            .MatchAtStart(subject, 6, -1)
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?w)" + drawn, options).Match(subject).Success.Should().BeFalse();
    }

    /// <summary>
    /// Seed 7 row 25854 of S52's wave: a FORWARD, non-overlapped scan that upstream ends four
    /// matches early because the <c>(*SKIP)</c> left <c>slice_start</c> above them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The carried-slice defect the <c>overlapped-skip-*</c> entries judge, on a row none of them
    /// can key on: the recorder writes <c>anchoredScan</c> for overlapped rows only, because a
    /// non-overlapped walk needs <c>must_advance</c> and no Python call carries it.
    /// </para>
    /// <para>
    /// The walk is sound on THIS row and the reason is narrow: <c>must_advance</c> is set only after
    /// a zero-width match (<c>state-&gt;must_advance = state-&gt;text_pos == state-&gt;match_pos</c>,
    /// <c>upstream/src/_regex.c:20932</c>) and no match here is zero-width, so
    /// <c>search(subject, m.end())</c> is the scanner's own step. Measured 2026-09-15: upstream's
    /// scan finds one match, its stepwise scan finds the three this port finds, the
    /// <c>(*PRUNE)</c> line finds the same three, and deleting the verb finds five - so both verbs
    /// really do prune two. Classified by
    /// <c>ExpectedDivergences.skip-carried-slice-on-a-scan-with-no-walk</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void A_forward_scan_of_a_skip_keeps_the_matches_upstreams_own_stepwise_door_still_finds()
    {
        // Flags 0x2, IGNORECASE, plus Version0 for the recorder's default.
        const string subject = "b\r\nabA\n_";
        FuzzyRegexOptions options = FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version0;

        new FuzzyRegex(@"(?b)(?:(?:\W{2,}[^\d]*?){1<=e<=2}(*SKIP)\D|\w)(\p{Lu}{2,3}){0,0}", options)
            .Matches(subject)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (3, 1), (4, 1));

        // Control one: the verb spelled (*PRUNE). Upstream answers these same three.
        new FuzzyRegex(@"(?b)(?:(?:\W{2,}[^\d]*?){1<=e<=2}(*PRUNE)\D|\w)(\p{Lu}{2,3}){0,0}", options)
            .Matches(subject)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (3, 1), (4, 1));

        // Control two: the verb deleted, which upstream and this port both answer with FIVE. That
        // is what says both verbs prune two real matches, so this port is not simply ignoring the
        // verb - the divergence is about the two upstream prunes beyond them.
        new FuzzyRegex(@"(?b)(?:(?:\W{2,}[^\d]*?){1<=e<=2}\D|\w)(\p{Lu}{2,3}){0,0}", options)
            .Matches(subject)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (3, 1), (4, 1), (5, 1), (7, 1));
    }

    /// <summary>
    /// Seed 7 row 38151 of S52's wave: a reversed overlapped scan whose second match upstream loses,
    /// on a pattern whose lookahead is why the recorder records no walk for it.
    /// </summary>
    /// <remarks>
    /// <c>_reads_the_end_of_the_subject</c> in <c>tools/record-oracle.py</c> refuses <c>(?=</c> and
    /// <c>(?!</c> along with <c>$</c> and the boundary escapes, because a reversed walk moves
    /// <c>endpos</c> and every one of them changes meaning on a truncated subject. The refusal is
    /// deliberately crude and costs a classification here. Computed by hand for this row on
    /// 2026-09-15: upstream's scan finds (0, 8) alone, its own stepwise reversed overlapped scan -
    /// each step <c>state-&gt;text_pos = state-&gt;match_pos + step</c> with a step of -1 under
    /// <c>(?r)</c>, <c>upstream/src/_regex.c:20927-20928</c> - finds (0, 8) and (0, 5),
    /// and so do the <c>(*PRUNE)</c> and verb-free lines. Classified by
    /// <c>ExpectedDivergences.skip-carried-slice-on-a-scan-with-no-walk</c>.
    /// </remarks>
    [Test]
    public void A_reversed_overlapped_scan_behind_a_lookahead_keeps_its_second_match()
    {
        // Flags 0x4002: FULLCASE and IGNORECASE, plus Version0 for the recorder's default.
        const string subject = "aas\rs\r\ns";
        const string drawn = @"(?r)\p{ASCII}{1,3}(?![a](*SKIP))s(?:[^\p{L}]*+(*SKIP)\W|s)[^a]*(?<=\W(*PRUNE))[A-Z]";
        FuzzyRegexOptions options =
            FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version0;

        new FuzzyRegex(drawn, options)
            .Matches(subject, overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 8), (0, 5));

        // The two controls, both of which upstream answers with the same two matches: the verbs
        // spelled (*PRUNE), and deleted. The pattern carries a (*PRUNE) of its own already, so the
        // second control leaves that one alone and only removes the two (*SKIP)s.
        new FuzzyRegex(drawn.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), options)
            .Matches(subject, overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 8), (0, 5));

        new FuzzyRegex(drawn.Replace("(*SKIP)", "", StringComparison.Ordinal), options)
            .Matches(subject, overlapped: true)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((0, 8), (0, 5));
    }
}
