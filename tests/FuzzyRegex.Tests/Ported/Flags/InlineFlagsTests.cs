using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Flags;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_inline_flags</c>
/// (lines 891-916).
/// </summary>
/// <remarks>
/// <c>chr(0x1ea0)</c> and <c>chr(0x1ea1)</c> (Latin Capital/Small Letter A with Dot Below,
/// U+1EA0/U+1EA1) are both in the BMP, so the C# literals below need no index recomputation.
/// Every construct-time <c>regex.U</c> flag in the upstream source is dropped as incidental (a
/// str pattern is Unicode by default upstream), and inline <c>"(?iu)"</c> is ported as
/// <c>"(?i)"</c> for the same reason.
/// </remarks>
public sealed class InlineFlagsTests
{
    private const string _upperAWithDotBelow = "Ạ";
    private const string _lowerAWithDotBelow = "ạ";

    [Test]
    [Arguments(_upperAWithDotBelow, _lowerAWithDotBelow)]
    [Arguments(_lowerAWithDotBelow, _upperAWithDotBelow)]
    [Property("Upstream", "RegexTests.test_inline_flags#1-2")]
    public void MatchAtStart_ignore_case_folds_A_with_dot_below(string pattern, string subject)
        // Upstream also passes regex.U; incidental for a str pattern, which is Unicode by default.
        =>
        new FuzzyRegex(pattern, FuzzyRegexOptions.IgnoreCase).IsMatchAtStart(subject).Should().BeTrue();

    [Test]
    [Arguments(_upperAWithDotBelow, _lowerAWithDotBelow)]
    [Arguments(_lowerAWithDotBelow, _upperAWithDotBelow)]
    [Property("Upstream", "RegexTests.test_inline_flags#3-4")]
    public void MatchAtStart_inline_ignore_case_flag_folds_A_with_dot_below(string pattern, string subject)
        // Upstream also passes regex.U as a construct-time flag; incidental for a str pattern.
        =>
        new FuzzyRegex("(?i)" + pattern).IsMatchAtStart(subject).Should().BeTrue();

    [Test]
    [Arguments(_upperAWithDotBelow, _lowerAWithDotBelow)]
    [Arguments(_lowerAWithDotBelow, _upperAWithDotBelow)]
    [Property("Upstream", "RegexTests.test_inline_flags#5-6")]
    public void MatchAtStart_inline_iu_flags_fold_A_with_dot_below(string pattern, string subject)
        // Upstream's inline "(?iu)"; u is incidental for a str pattern (Unicode by default), so
        // the inline flag group ported here is "(?i)".
        =>
        new FuzzyRegex("(?i)" + pattern).IsMatchAtStart(subject).Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_inline_flags#7")]
    public void MatchAtStart_inline_ignore_case_flag_at_the_start_of_the_pattern_applies() =>
        FuzzyRegex.MatchAtStart("A", "(?i)a").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_inline_flags#8")]
    public void MatchAtStart_inline_ignore_case_flag_after_the_pattern_content_does_not_apply() =>
        FuzzyRegex.MatchAtStart("A", "a(?i)").Success.Should().BeFalse();
}
