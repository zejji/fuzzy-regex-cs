using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_symbolic_refs</c>
/// (lines 226-247).
/// </summary>
public sealed class SymbolicRefsTests
{
    [Test]
    // Upstream asserts a specific message (MISSING_GT = "missing >"); we assert only the
    // exception type since our parser's messages are not decided yet.
    [Property("Upstream", "RegexTests.test_symbolic_refs#1")]
    public void Replace_with_an_unterminated_named_group_reference_throws()
    {
        Action act = static () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", @"\g<a");

        act.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_symbolic_refs#2")]
    public void Replace_with_an_empty_named_group_reference_throws()
    {
        Action act = static () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", @"\g<");

        act.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_symbolic_refs#3")]
    public void Replace_with_a_bare_g_and_no_angle_brackets_throws()
    {
        Action act = static () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", @"\g");

        act.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    [Arguments(@"\g<a a>")]
    [Arguments(@"\g<1a1>")]
    [Property("Upstream", "RegexTests.test_symbolic_refs#4-5")]
    public void Replace_with_a_malformed_group_name_throws(string replacement)
    {
        Action act = () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", replacement);

        act.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    // upstream raises IndexError
    [Property("Upstream", "RegexTests.test_symbolic_refs#6")]
    public void Replace_with_a_reference_to_an_unknown_group_name_throws()
    {
        Action act = static () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", @"\g<ab>");

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_symbolic_refs#7")]
    public void Replace_with_a_named_reference_to_a_group_that_did_not_participate_expands_to_empty() =>
        FuzzyRegex.Replace("xx", "(?P<a>x)|(?P<b>y)", @"\g<b>").Should().Be("");

    [Test]
    [Property("Upstream", "RegexTests.test_symbolic_refs#8")]
    public void Replace_with_a_numbered_reference_to_a_group_that_did_not_participate_expands_to_empty() =>
        FuzzyRegex.Replace("xx", "(?P<a>x)|(?P<b>y)", @"\2").Should().Be("");

    [Test]
    [Property("Upstream", "RegexTests.test_symbolic_refs#9")]
    public void Replace_with_a_negative_group_number_throws()
    {
        Action act = static () => _ = FuzzyRegex.Replace("xx", "(?P<a>x)", @"\g<-1>");

        act.Should().Throw<FuzzyRegexParseException>();
    }
}
