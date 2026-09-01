using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Splitting;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_qualified_re_split</c>
/// (lines 307-318).
/// </summary>
/// <remarks>
/// Only the <c>sys.version_info >= (3, 7, 0)</c> branch of upstream's version guard (assertion
/// #4) is ported; the pre-3.7 branch produced a different split count for a zero-width match and
/// is not a useful oracle.
/// </remarks>
public sealed class QualifiedSplitTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_qualified_re_split#1")]
    public void Split_with_a_max_split_count_stops_early_and_keeps_the_remainder_whole() =>
        new FuzzyRegex(":").Split(":a:b::c", 2).Should().Equal("", "a", "b::c");

    [Test]
    [Property("Upstream", "RegexTests.test_qualified_re_split#2")]
    public void Split_with_a_max_split_count_of_two_on_a_different_subject() =>
        new FuzzyRegex(":").Split("a:b:c:d", 2).Should().Equal("a", "b", "c:d");

    [Test]
    [Property("Upstream", "RegexTests.test_qualified_re_split#3")]
    public void Split_with_a_capturing_group_and_a_max_split_count_keeps_the_captured_delimiters() =>
        new FuzzyRegex("(:)").Split(":a:b::c", 2).Should().Equal("", ":", "a", ":", "b::c");

    [Test]
    [Property("Upstream", "RegexTests.test_qualified_re_split#4")]
    public void Split_with_a_star_quantified_capturing_group_and_a_max_split_count() =>
        new FuzzyRegex("(:*)").Split(":a:b::c", 2).Should().Equal("", ":", "", "", "a:b::c");
}
