using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>\X</c> under <c>(?r)</c>, and inside a lookbehind, which runs backwards too: a grapheme
/// cluster is the same span whichever way the match runs.
/// </summary>
/// <remarks>
/// <para>
/// Upstream's README says <c>\X</c> matches a grapheme cluster as UAX #29 defines it
/// (<c>upstream/README.rst:989-992</c>), and its own comment on the node says the match "is the
/// same whether matching forwards or backwards" (<c>upstream/regex/_regex_core.py:2921-2922</c>).
/// It is not. Upstream compiles <c>\X</c> as one or more characters followed by a boundary test,
/// and a reversed sequence runs its items from the right, so going backwards the boundary is
/// tested where the cluster starts matching and never where it ends. The match then stops after
/// one codepoint: <c>regex.findall(r'(?r)\X', 'e\u0301a')</c> is <c>['a', '\u0301']</c> and the
/// 'e' is lost. Upstream fails 465 of the 766 lines of <c>GraphemeBreakTest.txt</c> 17.0.0 in
/// reverse and none forwards (regex 2026.9.10, measured 2026-09-26). This port inherited the fault
/// and fixed it the same day, ledger entry 43.
/// </para>
/// <para>
/// The file rows are the file's own, one per rule, with its line number. Each is checked both
/// ways: forwards the clusters come out in order, and backwards the same clusters come out last
/// first.
/// </para>
/// </remarks>
public sealed class ReverseGraphemeTests
{
    // DIVERGES FROM UPSTREAM, deliberately: upstream's reverse match stops after one codepoint.
    [Test]
    [Property("Upstream", "none - gap test")]
    // GB9: an Extend character joins what comes before it.
    [Arguments(147, new[] { "\u094D\u094D" })]
    // GB9a: so does a SpacingMark.
    [Arguments(159, new[] { "\u094D\u0903" })]
    // GB9c: a consonant, a virama and a consonant (the Indic conjunct rule).
    [Arguments(774, new[] { "\u0915\u094D\u0924" })]
    // GB6: Hangul L joins another L.
    [Arguments(427, new[] { "\u1100\u1100" })]
    // GB9b: a Prepend character joins what comes after it.
    [Arguments(347, new[] { "\u06DD\u06DD" })]
    // GB12: two regional indicators make one flag.
    [Arguments(307, new[] { "\U0001F1E6\U0001F1E6" })]
    // GB11: an emoji ZWJ sequence.
    [Arguments(769, new[] { "\U0001F6D1\u200D\U0001F6D1" })]
    // GB3: CR LF is one cluster.
    [Arguments(29, new[] { "\r\n" })]
    // GB4 breaks after CR, then GB9 joins the two marks after it.
    [Arguments(34, new[] { "\r", "\u0308\u094D" })]
    public void Reversed_grapheme_clusters_follow_GraphemeBreakTest(int line, string[] clusters)
    {
        _ = line;
        string subject = string.Concat(clusters);

        Values(@"\X", subject).Should().Equal(clusters);
        Values(@"(?r)\X", subject).Should().Equal(clusters.Reverse());
    }

    // DIVERGES FROM UPSTREAM, deliberately: upstream answers ['a', '\u0301'].
    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reversed_grapheme_keeps_its_base_character()
    {
        Values(@"(?r)\X", "e\u0301a").Should().Equal("a", "e\u0301");
    }

    // DIVERGES FROM UPSTREAM, deliberately. A lookbehind matches backwards from where it stands, so
    // the '\X' inside one had the same fault. Upstream's answers, regex 2026.9.10, 2026-09-26:
    // '(?<=^\X)b' None, '(?<!^\X)b' (2, 3), '(?r)^\X' over CR LF None, '(?r)\X{2}' (1, 3), and
    // fullmatch of '(?r)\X' over 'e\u0301' None.
    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_grapheme_read_backwards_takes_the_whole_cluster()
    {
        // CR LF is one cluster, so 'b' has exactly one cluster before it.
        Span(new FuzzyRegex(@"(?<=^\X)b").Match("\r\nb")).Should().Be((2, 1));
        new FuzzyRegex(@"(?<!^\X)b").Match("\r\nb").Success.Should().BeFalse();
        Span(new FuzzyRegex(@"(?r)^\X").Match("\r\n")).Should().Be((0, 2));

        // Two clusters, 'e' with its accent and 'a', and one cluster that fills the text.
        Span(new FuzzyRegex(@"(?r)\X{2}").Match("e\u0301a")).Should().Be((0, 3));
        Span(new FuzzyRegex(@"(?r)\X").FullMatch("e\u0301")).Should().Be((0, 2));
    }

    // DIVERGES FROM UPSTREAM, deliberately: upstream answers a partial match at (0, 0).
    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reversed_grapheme_that_matches_is_not_partial()
    {
        // 'e\u0301' is one cluster that starts at the start of the text, so '^\X' matches all of
        // it and nothing ran out. Forwards, upstream agrees: search(r'^\X', 'e\u0301',
        // partial=True) is (0, 2) and not partial. The direction of the search does not change
        // which text the pattern matches.
        Match m = new FuzzyRegex(@"(?r)^\X").Match("e\u0301", partial: true);

        Span(m).Should().Be((0, 2));
        m.PartialMatch.Should().BeFalse();
    }

    // DIVERGES FROM UPSTREAM, deliberately: upstream answers (1, 3), putting 'e' where 'a' is.
    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reversed_grapheme_inside_a_fuzzy_match_takes_the_whole_cluster()
    {
        // Reading from the end, '\X' takes the one cluster 'e\u0301' and then 'a' meets 'b', one
        // substitution. Forwards, upstream finds this same span with the same count.
        Match m = new FuzzyRegex(@"(?r)(?:a\X){s<=1}").Match("be\u0301");

        Span(m).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    private static string[] Values(string pattern, string subject) =>
        [.. new FuzzyRegex(pattern).Matches(subject).Select(static m => m.Value)];

    private static (int Index, int Length)? Span(Match m) => m.Success ? (m.Index, m.Length) : null;
}
