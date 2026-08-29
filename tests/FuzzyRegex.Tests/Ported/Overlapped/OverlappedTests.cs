using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Overlapped;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_overlapped</c>
/// (lines 1503-1525).
/// </summary>
/// <remarks>
/// Upstream repeats every check once via <c>regex.findall</c> and again via
/// <c>[m[0] for m in regex.finditer(...)]</c> (or <c>[m.groups() for m in regex.finditer(...)]</c>
/// for the group-tuple case); each pair is folded into one test here with both upstream assertion
/// numbers, following the same convention as <c>Ported/Anchors/SpecialEscapesAnchorTests.cs</c>.
/// </remarks>
public sealed class OverlappedTests
{
    [Test]
    [Skip("needs:find-all - the engine has no repeat opcodes or a Matches enumerator yet")]
    [Property("Upstream", "RegexTests.test_overlapped#1,6")]
    public void Matches_value_for_two_char_runs_without_overlap() =>
        FuzzyRegex.Matches("abcde", "..").Select(m => m.Value).Should().Equal("ab", "cd");

    [Test]
    [Skip("needs:overlapped - the engine has no overlapped-match mode yet")]
    [Property("Upstream", "RegexTests.test_overlapped#2,7")]
    public void Matches_value_for_two_char_runs_with_overlap() =>
        new FuzzyRegex("..")
            .Matches("abcde", overlapped: true)
            .Select(m => m.Value)
            .Should()
            .Equal("ab", "bc", "cd", "de");

    [Test]
    [Skip("needs:right-to-left - the engine has no right-to-left match direction yet")]
    [Property("Upstream", "RegexTests.test_overlapped#3,8")]
    public void Matches_value_for_reversed_two_char_runs_without_overlap() =>
        FuzzyRegex.Matches("abcde", "(?r)..").Select(m => m.Value).Should().Equal("de", "bc");

    [Test]
    [Skip("needs:right-to-left - the engine has no right-to-left match direction yet; also needs overlapped matching")]
    [Property("Upstream", "RegexTests.test_overlapped#4,9")]
    public void Matches_value_for_reversed_two_char_runs_with_overlap() =>
        new FuzzyRegex("(?r)..")
            .Matches("abcde", overlapped: true)
            .Select(m => m.Value)
            .Should()
            .Equal("de", "cd", "bc", "ab");

    [Test]
    [Skip("needs:overlapped - the engine has no overlapped-match mode yet")]
    [Property("Upstream", "RegexTests.test_overlapped#5,10")]
    public void Matches_group_one_value_for_an_overlapped_three_group_pattern() =>
        new FuzzyRegex("(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("a", "b");

    [Test]
    [Skip("needs:overlapped - the engine has no overlapped-match mode yet")]
    [Property("Upstream", "RegexTests.test_overlapped#5,10")]
    public void Matches_group_two_value_for_an_overlapped_three_group_pattern() =>
        new FuzzyRegex("(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("-", "-");

    [Test]
    [Skip("needs:overlapped - the engine has no overlapped-match mode yet")]
    [Property("Upstream", "RegexTests.test_overlapped#5,10")]
    public void Matches_group_three_value_for_an_overlapped_three_group_pattern() =>
        new FuzzyRegex("(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("b", "c");

    [Test]
    [Skip("needs:right-to-left - the engine has no right-to-left match direction yet; also needs overlapped matching")]
    [Property("Upstream", "RegexTests.test_overlapped#11")]
    public void Matches_group_one_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("b", "a");

    [Test]
    [Skip("needs:right-to-left - the engine has no right-to-left match direction yet; also needs overlapped matching")]
    [Property("Upstream", "RegexTests.test_overlapped#11")]
    public void Matches_group_two_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("-", "-");

    [Test]
    [Skip("needs:right-to-left - the engine has no right-to-left match direction yet; also needs overlapped matching")]
    [Property("Upstream", "RegexTests.test_overlapped#11")]
    public void Matches_group_three_value_for_a_reversed_overlapped_three_group_pattern() =>
        new FuzzyRegex("(?r)(.)(-)(.)")
            .Matches("a-b-c", overlapped: true)
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("c", "b");
}
