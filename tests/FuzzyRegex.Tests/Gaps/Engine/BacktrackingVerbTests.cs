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
