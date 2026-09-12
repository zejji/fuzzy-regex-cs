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
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));

        // Without overlapping, the scan resumes where the match ended rather than one on from where
        // it started, so the moved slice start is never behind it:
        // [m.span() for m in regex.finditer(r'[A-Z]*(*SKIP)_', '__BB__B')] is
        // [(0, 1), (1, 2), (2, 5), (5, 6)].
        new FuzzyRegex("[A-Z]*(*SKIP)_")
            .Matches("__BB__B")
            .Select(m => (m.Index, m.Length))
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
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 4), (1, 3), (2, 2), (3, 1));

        // [m.span() for m in regex.finditer(r'(?>abc(*SKIP)d)|abc', 'ABCABC', regex.I)] is
        // [(0, 3), (3, 6)]: the SKIP is never reached, because 'd' fails inside the atomic group,
        // and the fallback branch matches at both positions.
        new FuzzyRegex("(?>abc(*SKIP)d)|abc", FuzzyRegexOptions.IgnoreCase)
            .Matches("ABCABC")
            .Select(m => (m.Index, m.Length))
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
            .Match<Match>(m => m.Index == 4 && m.Length == 4);
        FuzzyRegex.Match("aaaaxx", "(?:aa(*SKIP)x|M)x").Should().Match<Match>(m => m.Index == 2 && m.Length == 4);
    }

    [Test]
    public void Skip_under_reverse_tries_a_start_position_upstreams_search_start_skips()
    {
        // PERMANENT: the port is right, see docs/plan/2026-09-12-divergence-research.md. A change
        // here is a regression, and the earlier instruction to invert it in Phase 7 is WITHDRAWN on
        // the same grounds as the test above. Found 2026-09-11 by the S29 oracle wave (generator
        // 'verbs', seed 20260913, rows 502, 504, 519 and 863), and NOT the required-string
        // interaction the test above pins - the two were conflated in S29's first verdict.
        //
        // The second engine cannot be quoted on this one: PCRE2 has no reverse matching, so there is
        // nothing to ask. What settles it instead is that UPSTREAM DISAGREES WITH ITSELF - its
        // 'search_start_END_OF_LINE_rev' bounds by text_end while its own slow path bounds by
        // slice_end, and emulating the fast path in front of each attempt reproduces upstream here
        // exactly (S29's notes, and the emulation paragraph below). A prefilter is supposed to skip
        // positions that cannot match, not positions whose answer it disagrees with.
        //
        // These rows are classified as 'search-start-skip-slice' in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs from S33, but the generator is still
        // NOT on the default oracle list - the S33 blind review found a fifth, unjudged '(*SKIP)'
        // family at seeds S29 never ran. See the Generator note in tools/run-oracle.ps1.
        //
        // Upstream's 'search_start' (upstream/src/_regex.c:8385) is the fast scan for the next
        // plausible start position, and 'basic_match' takes it whenever the start test has a
        // 'search_start_*' twin (:11819). The two halves do not agree about the slice:
        // 'search_start_END_OF_LINE_rev' (:8055) bounds itself with TEXT_end, while
        // 'try_match_END_OF_LINE' (:7108) - which 'basic_match' consults - bounds itself with
        // SLICE_end. Nothing else moves the slice inside an attempt, so the two agree on every
        // pattern there is, until a '(*SKIP)' moves it. Then upstream's fast path walks straight past
        // a start position its own slow path would accept.
        //
        // This port does not implement 'search_start' at all (Matcher.cs, the 'next_match_2' block),
        // so it has only the slow path and tries that position. Measured against regex 2026.7.19 on
        // 2026-09-11:
        //
        //   [m.span() for m in regex.finditer(r'(?r)(?:a*(*SKIP)b|[^a-f])$', '\nb', regex.M)]
        //   is [(1, 2)] - one match; this port finds (1,1) and then (0,1).
        //
        // Confirmed by emulating 'search_start_END_OF_LINE_rev' in front of each attempt: the port
        // then reproduces upstream exactly on every case here, including the three-match subject
        // below, where it must NOT lose upstream's second match.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\nb")
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1), (0, 1));

        // Upstream: [(6, 7), (4, 5)]. This port adds (5, 1) between them.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\rbbb\r\nb")
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((6, 1), (5, 1), (4, 1));

        // The control that says this is not simply "the port finds too many": with no '(*SKIP)' the
        // slice never moves, the two halves agree, and both sides answer the same.
        new FuzzyRegex("(?r)(?:a*b|[^a-f])$", FuzzyRegexOptions.Multiline)
            .Matches("\nb")
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1));

        // And '$' without MULTILINE agrees too, which is the discriminator: that is END_OF_STRING_LINE,
        // whose try_match (:7127) and reversed search_start twin (:8113) BOTH bound themselves with
        // text_end and final_newline, so they cannot disagree about a moved slice.
        new FuzzyRegex("(?r)(?:a*(*SKIP)b|[^a-f])$")
            .Matches("\nb")
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((1, 1));
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
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 1), (1, 1), (2, 3), (3, 2), (4, 1), (5, 1));
    }
}
