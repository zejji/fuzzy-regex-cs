using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_3629</c> (lines 141-144).
/// </summary>
/// <remarks>
/// Upstream also asserts <c>repr(type(...))</c> equals its <c>PATTERN_CLASS</c> constant; there is
/// no equivalent surface in this API, so the only portable assertion is that construction
/// succeeds.
/// </remarks>
public sealed class ConstructionTests
{
    [Test]
    [Skip("needs:conditionals - the parser has no conditional-group support yet")]
    [Property("Upstream", "RegexTests.test_bug_3629#1")]
    public void Constructing_a_conditional_referencing_a_named_group_does_not_throw()
    {
        Action act = () => _ = new FuzzyRegex("(?P<quote>)(?(quote))");

        act.Should().NotThrow();
    }
}
