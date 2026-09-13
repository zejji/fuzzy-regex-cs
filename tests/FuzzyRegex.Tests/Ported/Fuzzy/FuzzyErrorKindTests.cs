using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// <para>
/// Upstream's "match whole string, allow only 1 error" block, split by which kind of error the
/// single permitted edit turns out to be. Which kind each subject needs is not stated upstream -
/// it was measured, by reading <c>fuzzy_counts</c> back from the local Python oracle on
/// 2026-08-30:
/// </para>
/// <list type="bullet">
/// <item><c>xfoobar</c>, <c>foobarx</c>, <c>fooxbar</c> give <c>(0, 1, 0)</c> - one insertion.</item>
/// <item><c>foxbar</c>, <c>xoobar</c>, <c>foobax</c> give <c>(1, 0, 0)</c> - one substitution.</item>
/// <item><c>oobar</c>, <c>fobar</c>, <c>fooba</c> give <c>(0, 0, 1)</c> - one deletion.</item>
/// </list>
/// <para>
/// The exact match (#28) and the five subjects that need two errors (#38-42) are in
/// <see cref="FuzzyMatchingTests"/>, since neither is about a particular kind of error.
/// </para>
/// </remarks>
public sealed class FuzzyErrorKindTests
{
    [Test]
    [Arguments("xfoobar", 7)]
    [Arguments("foobarx", 7)]
    [Arguments("fooxbar", 7)]
    [Property("Upstream", "RegexTests.test_fuzzy#29-31")]
    public void One_insertion_is_within_a_single_error_budget(string subject, int end) => AssertSpan(subject, end);

    [Test]
    [Arguments("foxbar", 6)]
    [Arguments("xoobar", 6)]
    [Arguments("foobax", 6)]
    [Property("Upstream", "RegexTests.test_fuzzy#32-34")]
    public void One_substitution_is_within_a_single_error_budget(string subject, int end) => AssertSpan(subject, end);

    [Test]
    [Arguments("oobar", 5)]
    [Arguments("fobar", 5)]
    [Arguments("fooba", 5)]
    [Property("Upstream", "RegexTests.test_fuzzy#35-37")]
    public void One_deletion_is_within_a_single_error_budget(string subject, int end) => AssertSpan(subject, end);

    private static void AssertSpan(string subject, int end)
    {
        Match m = FuzzyRegex.Match(subject, "^(foobar){e<=1}$");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((0, end));
    }
}
