using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_unmatched_in_sub</c>
/// (lines 1465-1484).
/// </summary>
/// <remarks>
/// Assertions are numbered in source order over every <c>self.assert</c> line, including the
/// branches of a version guard that this port does not use, so a number here always names the same
/// upstream line. #2, #5 and #8 are those unused pre-3.7 branches.
/// <para>Each <c>(?V0)</c> assertion is guarded upstream by <c>sys.version_info &gt;= (3, 7, 0)</c>;
/// only that branch is ported here (a group that never took part still expands to an empty
/// string, not a dropped placeholder). The <c>(?V1)</c> assertions are unguarded upstream.</para>
/// </remarks>
public sealed class UnmatchedInSubTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#1")]
    public void V0_replacement_expands_an_unmatched_trailing_group_to_empty() =>
        FuzzyRegex.Replace("xy", "(?V0)(x)?(y)?", @"\2-\1").Should().Be("y-x-");

    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#3")]
    public void V1_replacement_expands_an_unmatched_trailing_group_to_empty() =>
        FuzzyRegex.Replace("xy", "(?V1)(x)?(y)?", @"\2-\1").Should().Be("y-x-");

    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#4")]
    public void V0_replacement_expands_an_unmatched_leading_and_trailing_group_to_empty() =>
        FuzzyRegex.Replace("x", "(?V0)(x)?(y)?", @"\2-\1").Should().Be("-x-");

    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#6")]
    public void V1_replacement_expands_an_unmatched_leading_and_trailing_group_to_empty() =>
        FuzzyRegex.Replace("x", "(?V1)(x)?(y)?", @"\2-\1").Should().Be("-x-");

    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#7")]
    public void V0_replacement_expands_an_unmatched_leading_group_to_empty() =>
        FuzzyRegex.Replace("y", "(?V0)(x)?(y)?", @"\2-\1").Should().Be("y--");

    [Test]
    [Property("Upstream", "RegexTests.test_unmatched_in_sub#9")]
    public void V1_replacement_expands_an_unmatched_leading_group_to_empty() =>
        FuzzyRegex.Replace("y", "(?V1)(x)?(y)?", @"\2-\1").Should().Be("y--");
}
