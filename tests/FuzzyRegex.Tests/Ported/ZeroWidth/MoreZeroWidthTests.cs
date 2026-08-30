using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.ZeroWidth;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_more_zerowidth</c>
/// (lines 4520-4527): a zero-width word boundary alternated with a run of colons, and a
/// multiline lazy zero-width <c>$</c> match on consecutive blank lines.
/// </summary>
/// <remarks>
/// Every assertion here sits inside upstream's <c>if sys.version_info &gt;= (3, 7, 0):</c> guard.
/// That is the branch that actually runs under any Python this port cares about, so it is the one
/// ported; there is no pre-3.7 branch to omit.
/// </remarks>
public sealed class MoreZeroWidthTests
{
    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_more_zerowidth#1")]
    public void Splitting_on_a_word_boundary_or_colon_run_keeps_the_empty_pieces() =>
        FuzzyRegex.Split("a::bc", @"\b|:+").Should().Equal("", "a", "", "", "bc", "");

    [Test]
    [Skip("needs:substitution - Pattern.Replace is not implemented yet")]
    [Property("Upstream", "RegexTests.test_more_zerowidth#2")]
    public void Replacing_a_word_boundary_or_colon_run_inserts_a_dash_at_every_zero_width_position() =>
        FuzzyRegex.Replace("a::bc", @"\b|:+", "-").Should().Be("-a---bc-");

    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_more_zerowidth#3-4")]
    public void Matches_of_a_word_boundary_or_colon_run_have_the_expected_values_and_spans()
    {
        Match[] matches = [.. FuzzyRegex.Matches("a::bc", @"\b|:+")];

        matches.Select(m => m.Value).Should().Equal("", "", "::", "", "");
        matches.Select(m => (m.Index, m.Index + m.Length)).Should().Equal((0, 0), (1, 1), (1, 3), (3, 3), (5, 5));
    }

    [Test]
    [Skip("needs:line-boundaries - the engine has no multiline zero-width $ opcode yet")]
    [Property("Upstream", "RegexTests.test_more_zerowidth#5")]
    public void Multiline_lazy_zero_width_whitespace_to_end_of_line_matches_at_each_blank_line_position() =>
        FuzzyRegex
            .Matches("foo\n\n\nbar", @"(?m)^\s*?$")
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((4, 4), (4, 5), (5, 5));
}
