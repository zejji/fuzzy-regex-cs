using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.ZeroWidth;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_zerowidth</c>
/// (lines 1336-1392).
/// </summary>
/// <remarks>
/// <para>
/// Assertions are numbered in source order over every <c>self.assert</c> line, including the
/// branches of a version guard that this port does not use, so a number here always names the same
/// upstream line. Each <c>if sys.version_info >= (3, 7, 0):</c> guard (lines 1338, 1364, 1374) is
/// ported from its <c>&gt;= 3.7</c> branch only; the older branch produced a different split count
/// for a zero-width match and is not a useful oracle.
/// </para>
/// <para>
/// Upstream pairs <c>regex.findall</c> with <c>[m[0] for m in regex.finditer(...)]</c> for the same
/// pattern. Both are <c>Matches</c> here, so each pair is one test carrying both assertion numbers.
/// The <c>(?V1)</c> and <c>(?rV1)</c> patterns are <em>not</em> folded into their unflagged
/// siblings: the expected value is the same, but the pattern text is different and the parser has
/// to accept it, which is the whole point of upstream asserting both.
/// </para>
/// </remarks>
public sealed class ZeroWidthTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_zerowidth#1")]
    public void Split_on_a_word_boundary_yields_the_word_the_gap_and_empty_ends() =>
        new FuzzyRegex(@"\b").Split("a b").Should().Equal("", "a", " ", "b", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V1) yet; also needs anchors and splitting")]
    [Property("Upstream", "RegexTests.test_zerowidth#3")]
    public void Split_on_a_word_boundary_under_the_V1_flag_gives_the_same_pieces() =>
        new FuzzyRegex(@"(?V1)\b").Split("a b").Should().Equal("", "a", " ", "b", "");

    [Test]
    [Property("Upstream", "RegexTests.test_zerowidth#4,5")]
    public void Matches_value_for_start_anchor_or_word_run_scans_forward() =>
        FuzzyRegex.Matches("foo bar", @"^|\w+").Select(m => m.Value).Should().Equal("", "foo", "bar");

    [Test]
    [Property("Upstream", "RegexTests.test_zerowidth#6,7")]
    public void Matches_value_for_start_anchor_or_word_run_scans_backward() =>
        FuzzyRegex.Matches("foo bar", @"(?r)^|\w+").Select(m => m.Value).Should().Equal("bar", "foo", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V1) yet; also needs anchors (^)")]
    [Property("Upstream", "RegexTests.test_zerowidth#8,9")]
    public void Matches_value_for_start_anchor_or_word_run_under_the_V1_flag_scans_forward() =>
        FuzzyRegex.Matches("foo bar", @"(?V1)^|\w+").Select(m => m.Value).Should().Equal("", "foo", "bar");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?rV1) yet; also needs right-to-left and anchors (^)")]
    [Property("Upstream", "RegexTests.test_zerowidth#10,11")]
    public void Matches_value_for_start_anchor_or_word_run_under_the_V1_flag_scans_backward() =>
        FuzzyRegex.Matches("foo bar", @"(?rV1)^|\w+").Select(m => m.Value).Should().Equal("bar", "foo", "");

    [Test]
    [Property("Upstream", "RegexTests.test_zerowidth#12")]
    public void Split_on_an_empty_pattern_yields_every_char_with_empty_ends() =>
        new FuzzyRegex("").Split("xaxbxc").Should().Equal("", "x", "a", "x", "b", "x", "c", "");

    [Test]
    [Property("Upstream", "RegexTests.test_zerowidth#16")]
    public void Split_on_a_reversed_empty_pattern_yields_every_char_in_reverse_with_empty_ends() =>
        new FuzzyRegex("(?r)").Split("xaxbxc").Should().Equal("", "c", "x", "b", "x", "a", "x", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V1) yet; also needs splitting")]
    [Property("Upstream", "RegexTests.test_zerowidth#20")]
    public void Split_on_an_empty_V1_pattern_yields_every_char_with_empty_ends() =>
        new FuzzyRegex("(?V1)").Split("xaxbxc").Should().Equal("", "x", "a", "x", "b", "x", "c", "");

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?rV1) yet; also needs right-to-left and splitting")]
    [Property("Upstream", "RegexTests.test_zerowidth#22")]
    public void Split_on_a_reversed_empty_V1_pattern_yields_every_char_in_reverse_with_empty_ends() =>
        new FuzzyRegex("(?rV1)").Split("xaxbxc").Should().Equal("", "c", "x", "b", "x", "a", "x", "");

    // NOT PORTED: #2, #14, #15, #18, #19 - the pre-3.7 branch of each version guard.
    // NOT PORTED: #13, #17, #21, #23 - regex.splititer, deferred; Split covers the same pieces.
}
