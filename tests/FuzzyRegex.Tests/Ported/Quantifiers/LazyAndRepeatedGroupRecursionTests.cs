using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_418626</c>
/// (lines 773-786) and <c>test_stack_overflow</c> (lines 792-797).
/// </summary>
/// <remarks>
/// Both upstream methods exist to prove that lazy <c>*?</c> and repeated capturing groups do not
/// recurse per character/iteration, which is why both build subjects tens of thousands of
/// characters long. The subjects are reproduced here at the same sizes, verbatim.
/// </remarks>
public sealed class LazyAndRepeatedGroupRecursionTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_418626#1")]
    public void MatchAtStart_lazy_dot_star_c_does_not_overflow_the_stack()
    {
        string subject = string.Concat(Enumerable.Repeat("ab", 10000)) + "cd";

        Match m = FuzzyRegex.MatchAtStart(subject, ".*?c");

        (m.Index + m.Length).Should().Be(20001);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_418626#2")]
    public void MatchAtStart_lazy_dot_star_cd_does_not_overflow_the_stack()
    {
        string subject =
            string.Concat(Enumerable.Repeat("ab", 5000)) + "c" + string.Concat(Enumerable.Repeat("ab", 5000)) + "cde";

        Match m = FuzzyRegex.MatchAtStart(subject, ".*?cd");

        (m.Index + m.Length).Should().Be(20003);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_418626#3")]
    public void MatchAtStart_lazy_dot_star_cd_handles_a_long_repeated_prefix()
    {
        string subject = string.Concat(Enumerable.Repeat("abc", 20000)) + "de";

        Match m = FuzzyRegex.MatchAtStart(subject, ".*?cd");

        (m.Index + m.Length).Should().Be(60001);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_418626#4")]
    public void Match_lazy_alternation_repeat_does_not_overflow_the_stack()
    {
        string subject = string.Concat(Enumerable.Repeat("ab", 10000)) + "cd";

        Match m = FuzzyRegex.Match(subject, "(a|b)*?c");

        (m.Index + m.Length).Should().Be(20001);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_stack_overflow#1")]
    public void MatchAtStart_repeated_group_captures_the_last_iteration_without_overflow()
    {
        string subject = new('x', 50000);

        FuzzyRegex.MatchAtStart(subject, "(x)*").Groups[1].Value.Should().Be("x");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_stack_overflow#2")]
    public void MatchAtStart_repeated_group_before_a_literal_captures_without_overflow()
    {
        string subject = new string('x', 50000) + "y";

        FuzzyRegex.MatchAtStart(subject, "(x)*y").Groups[1].Value.Should().Be("x");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_stack_overflow#3")]
    public void MatchAtStart_lazy_repeated_group_before_a_literal_captures_without_overflow()
    {
        string subject = new string('x', 50000) + "y";

        FuzzyRegex.MatchAtStart(subject, "(x)*?y").Groups[1].Value.Should().Be("x");
    }
}
