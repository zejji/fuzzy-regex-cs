using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Possessive;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_possessive</c>
/// (lines 1310-1334).
/// </summary>
/// <remarks>
/// The non-possessive half of each pair is ordinary backtracking (tagged
/// <c>needs:quantifiers</c>); only the possessive half needs a possessive-quantifier opcode
/// (tagged <c>needs:possessive</c>).
/// </remarks>
public sealed class PossessiveTests
{
    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#1")]
    public void Optional_a_then_a_backtracks_to_match_a_single_a()
    {
        Match m = FuzzyRegex.Match("a", "a?a");

        m.Index.Should().Be(0);
        m.Length.Should().Be(1);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#2")]
    public void Star_a_then_a_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("aaa", "a*a");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#3")]
    public void Plus_a_then_a_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("aaa", "a+a");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#4")]
    public void Bounded_repeat_a_then_a_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("aaa", "a{1,3}a");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#5")]
    public void Optional_group_then_group_backtracks_to_match_a_single_group()
    {
        Match m = FuzzyRegex.Match("ab", "(?:ab)?ab");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#6")]
    public void Star_group_then_group_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("ababab", "(?:ab)*ab");

        m.Index.Should().Be(0);
        m.Length.Should().Be(6);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#7")]
    public void Plus_group_then_group_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("ababab", "(?:ab)+ab");

        m.Index.Should().Be(0);
        m.Length.Should().Be(6);
    }

    [Test]
    [Skip("needs:quantifiers - nested repeats need a repeat opcode")]
    [Property("Upstream", "RegexTests.test_possessive#8")]
    public void Bounded_repeat_group_then_group_backtracks_to_match_the_whole_run()
    {
        Match m = FuzzyRegex.Match("ababab", "(?:ab){1,3}ab");

        m.Index.Should().Be(0);
        m.Length.Should().Be(6);
    }

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#9")]
    public void Possessive_optional_a_then_a_does_not_backtrack() =>
        FuzzyRegex.Match("a", "a?+a").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#10")]
    public void Possessive_star_a_then_a_does_not_backtrack() =>
        FuzzyRegex.Match("aaa", "a*+a").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#11")]
    public void Possessive_plus_a_then_a_does_not_backtrack() =>
        FuzzyRegex.Match("aaa", "a++a").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#12")]
    public void Possessive_bounded_repeat_a_then_a_does_not_backtrack() =>
        FuzzyRegex.Match("aaa", "a{1,3}+a").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#13")]
    public void Possessive_optional_group_then_group_does_not_backtrack() =>
        FuzzyRegex.Match("ab", "(?:ab)?+ab").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#14")]
    public void Possessive_star_group_then_group_does_not_backtrack() =>
        FuzzyRegex.Match("ababab", "(?:ab)*+ab").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#15")]
    public void Possessive_plus_group_then_group_does_not_backtrack() =>
        FuzzyRegex.Match("ababab", "(?:ab)++ab").Success.Should().BeFalse();

    [Test]
    [Skip("needs:possessive - the engine has no possessive-quantifier opcode yet")]
    [Property("Upstream", "RegexTests.test_possessive#16")]
    public void Possessive_bounded_repeat_group_then_group_does_not_backtrack() =>
        FuzzyRegex.Match("ababab", "(?:ab){1,3}+ab").Success.Should().BeFalse();
}
