using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_545855</c> (lines 767-771).
/// </summary>
public sealed class BadSetTests
{
    // Upstream asserts the error message matches self.BAD_SET; we do not assert message text, per
    // the port's own error-message conventions (not yet decided).
    [Test]
    [Property("Upstream", "RegexTests.test_bug_545855#1")]
    public void Unterminated_set_fails_to_compile()
    {
        Action act = () => _ = new FuzzyRegex("foo[a-");

        act.Should().Throw<FuzzyRegexParseException>();
    }
}
