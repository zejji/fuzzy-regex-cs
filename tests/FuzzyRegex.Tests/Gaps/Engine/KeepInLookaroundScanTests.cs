using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D87: a <c>\K</c> inside a lookaround can report a match whose span says nothing about where the
/// attempt was, and a scan that steps on from the reported span then finds the same match again,
/// for ever.
/// </summary>
/// <remarks>
/// <para>
/// <c>(?=y\K)</c> over <c>y</c> is the smallest case. The attempt at 0 runs the lookahead, which
/// reads the <c>y</c> and moves the reported start to 1, then puts the text position back to 0. The
/// match is (1, 0): it ends where the attempt began, so it consumed nothing. Upstream's scan rule,
/// "a match that was empty must not be followed by another empty match at the same place", tests
/// the reported span, sees (1, 0) as not empty, and searches again from 0 with nothing banned. The
/// same attempt succeeds the same way, and <c>finditer</c> yields (1, 0) for ever. A caller that
/// lists the results runs out of memory. In reverse, <c>(?r)(?=y\K)</c> reports (0, 1), which looks
/// like an ordinary one-character match, and loops the same way. The overlapped scan steps one
/// character from the reported start, and a <c>\K</c> in a lookbehind puts that start before the
/// attempt, so the step lands where the attempt was and that loops too.
/// </para>
/// <para>
/// Upstream (<c>regex</c> 2026.9.10, measured 2026-10-15, <c>.scratch/s1016/d87/up-probe.py</c>)
/// loops on every red row here; its <c>split</c> and <c>sub</c> time out. Perl 5.42.3 and PCRE2
/// 10.47 refuse <c>\K</c> in a lookaround at compile time. PCRE2 still has an opt-in for it
/// (<c>PCRE2_EXTRA_ALLOW_LOOKAROUND_BSK</c>), and its <c>pcre2_next_match</c> handles exactly this
/// case: a match that ends where the search began but whose start a <c>\K</c> moved does not
/// count as progress.
/// </para>
/// <para>
/// The port's rule: a match that ends where its attempt began consumed nothing, whatever span it
/// reports, so the next search bans an empty match there, exactly as after an ordinary empty
/// match. The overlapped scan steps from where the attempt began whenever the reported start lies
/// behind it. A match with a real width is untouched, so every pattern without a <c>\K</c> inside
/// a lookaround, atomic group or group call answers as before.
/// </para>
/// </remarks>
public sealed class KeepInLookaroundScanTests
{
    // Taking one more than the expected count is what keeps a red run from hanging: the old rule
    // yields the first match for ever, and Take stops after the (n+1)th.
    private static (int, int)[] Spans(FuzzyRegex regex, string subject, bool overlapped, int expected) =>
        [
            .. regex
                .EnumerateMatches(subject, overlapped: overlapped)
                .Take(expected + 1)
                .Select(static m => (m.Index, m.Index + m.Length)),
        ];

    [Test]
    [Arguments(@"(?=y\K)", "y", 1, 0)]
    [Arguments(@"(?r)(?=y\K)", "y", 0, 1)]
    [Arguments(@"(?<=\Ky)", "y", 0, 1)]
    [Arguments(@"(?r)(?<=\Ky)", "y", 1, 0)]
    public void A_scan_moves_on_after_a_match_that_ended_where_its_attempt_began(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Spans(new FuzzyRegex(pattern), subject, overlapped: false, expected: 1).Should().Equal((start, end));
    }

    [Test]
    public void A_search_does_not_step_back_onto_the_attempt_it_just_failed()
    {
        // The second search starts at 1 and may not end there. The lookbehind's \K leaves the
        // reported start at 0, behind the failed attempt, and stepping on from it lands on 1 again:
        // upstream loops inside that one search. The attempt at 2 then finds (1,2).
        Spans(new FuzzyRegex(@"(?<=\Ky)"), "yy", overlapped: false, expected: 2).Should().Equal((0, 1), (1, 2));
        new FuzzyRegex(@"(?<=\Ky)") { ExpiresOnCheck = 1000 }
            .Split("yy", timeout: TimeSpan.FromMinutes(1))
            .Should()
            .Equal("", "", "");
    }

    // The D86 builder's row. Reversed, the lookahead runs first and its \K leaves the reported start
    // at 7, behind each failed attempt at 6, so upstream steps back onto 6 for ever inside the
    // search, under every flag. With the leading \K the match moves its start to its end, (3,3),
    // which upstream also gives; without it the start is the lookahead's, as in upstream's
    // (?r)x(?=z\K) over 'xyxz', (2,4).
    [Test]
    [Arguments(@"(?er)\K\K(?:Cyy){1i+1d+1s<=2}(?=z\K)", 3, 3)]
    [Arguments(@"(?er)(?:Cyy){1i+1d+1s<=2}(?=z\K)", 3, 7)]
    [Arguments(@"(?br)(?:Cyy){1i+1d+1s<=2}(?=z\K)", 3, 7)]
    [Arguments(@"(?r)(?:Cyy){1i+1d+1s<=2}(?=z\K)", 3, 7)]
    public void A_reversed_fuzzy_search_finishes_when_a_lookahead_K_moved_the_start(string pattern, int start, int end)
    {
        Match m = new FuzzyRegex(pattern) { ExpiresOnCheck = 100_000 }.Match(
            "bx-axyz-\U0001D49C",
            timeout: TimeSpan.FromMinutes(1)
        );

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
    }

    [Test]
    [Arguments(@"(?r)(?=By\K)(?:z){e}", "Byz", new[] { 0, 2, 0, 2, 0, 0, 2, 0, 0, 1 })]
    [Arguments(@"(?er)(?=By\K)(?:z){e}", " CxByzzxxyz", new[] { 3, 5, 1, 0, 0, 3, 5, 0, 0, 1 })]
    [Arguments(@"(?br)(?=By\K)(?:z){e}", " CxByzzxxyz", new[] { 3, 5, 1, 0, 0, 3, 5, 0, 0, 1 })]
    public void A_fuzzy_scan_moves_on_after_a_match_that_ended_where_its_attempt_began(
        string pattern,
        string subject,
        int[] flat
    )
    {
        // Each row has two matches with the same span from different attempts. The first consumes
        // text (insertions, or a substitution); the second deletes the z, so it consumes nothing
        // and its span is the \K's. Upstream 2026.9.10 gives these two and then repeats the second
        // for ever ((?br) repeats the first: its re-run walks on from the reported start, D86).
        var expected = flat.Chunk(5).Select(static v => ((v[0], v[1]), new FuzzyCounts(v[2], v[3], v[4]))).ToArray();

        new FuzzyRegex(pattern)
            .EnumerateMatches(subject)
            .Take(expected.Length + 1)
            .Select(static m => ((m.Index, m.Index + m.Length), m.FuzzyCounts))
            .Should()
            .Equal(expected);
    }

    [Test]
    [Arguments(@"(?r)(?=y\K)", "y", new[] { 0, 1 })]
    [Arguments(@"(?<=\Ky)", "y", new[] { 0, 1 })]
    [Arguments(@"(?<=\Ka)b", "abab", new[] { 0, 2, 2, 4 })]
    public void An_overlapped_scan_steps_from_the_attempt_when_the_reported_start_lies_behind_it(
        string pattern,
        string subject,
        int[] flat
    )
    {
        (int, int)[] expected = [.. flat.Chunk(2).Select(static p => (p[0], p[1]))];

        Spans(new FuzzyRegex(pattern), subject, overlapped: true, expected.Length).Should().Equal(expected);
    }

    [Test]
    [Arguments(@"a?\K", "a", false, new[] { 1, 1 })]
    [Arguments(@"(?=\Ky).", "y", false, new[] { 0, 1 })]
    [Arguments(@"(?r)\Ky", "y", false, new[] { 0, 0 })]
    [Arguments(@"a\K.", "aaa", true, new[] { 1, 2 })]
    [Arguments(@"(?=y\K)", "y", true, new[] { 1, 0 })]
    public void Scans_that_already_moved_on_answer_as_upstream(
        string pattern,
        string subject,
        bool overlapped,
        int[] flat
    )
    {
        // The controls, green before and after: upstream 2026.9.10 gives exactly these. The last
        // overlapped row steps from its reported start, which is ahead of the attempt, as upstream.
        (int, int)[] expected = [.. flat.Chunk(2).Select(static p => (p[0], p[1]))];

        Spans(new FuzzyRegex(pattern), subject, overlapped, expected.Length).Should().Equal(expected);
    }

    [Test]
    public void Next_match_walks_the_same_sequence_as_the_scan()
    {
        // NextMatch rebuilds the scan from the match alone, so the match has to carry where its
        // attempt began.
        Match first = new FuzzyRegex(@"(?r)(?=y\K)").Match("y");
        (first.Index, first.Index + first.Length).Should().Be((0, 1));
        first.NextMatch().Success.Should().BeFalse();

        Match lookbehind = new FuzzyRegex(@"(?<=\Ky)").Match("y");
        (lookbehind.Index, lookbehind.Index + lookbehind.Length).Should().Be((0, 1));
        lookbehind.NextMatch().Success.Should().BeFalse();

        Match overlapped = new FuzzyRegex(@"(?<=\Ka)b").Matches("abab", overlapped: true)[0];
        Match next = overlapped.NextMatch();
        (next.Index, next.Index + next.Length).Should().Be((2, 4));
        next.NextMatch().Success.Should().BeFalse();
    }

    [Test]
    public void A_value_match_walk_moves_on_too()
    {
        var spans = new List<(int, int)>();
        foreach (ValueMatch m in new FuzzyRegex(@"(?r)(?=y\K)").EnumerateMatches("y".AsSpan()))
        {
            spans.Add((m.Index, m.Index + m.Length));
            if (spans.Count > 2)
            {
                break;
            }
        }

        spans.Should().Equal((0, 1));
    }

    // Split and Replace scan the same way. The expiry seam stops a red run after a bounded amount
    // of work instead of letting it fill memory. It counts only when the call has a budget, which
    // is why each call passes a minute. The fixed rule needs a handful of checks.
    [Test]
    public void Split_and_replace_finish()
    {
        var forward = new FuzzyRegex(@"(?=y\K)") { ExpiresOnCheck = 1000 };
        var reverse = new FuzzyRegex(@"(?r)(?=y\K)") { ExpiresOnCheck = 1000 };
        TimeSpan minute = TimeSpan.FromMinutes(1);

        // Upstream's own slicing of the crossed match (1,0): the piece before it runs from 0 to its
        // start and the piece after it from its end, so the two overlap. Upstream gives the same
        // with maxsplit=1 and count=1, the only calls of these that it finishes.
        forward.Split("y", timeout: minute).Should().Equal("y", "y");
        forward.Replace("y", "<>", timeout: minute).Should().Be("y<>y");
        reverse.Split("y", timeout: minute).Should().Equal("", "");
        reverse.Replace("y", "<>", timeout: minute).Should().Be("<>");
    }
}
