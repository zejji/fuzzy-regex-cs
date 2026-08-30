using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_issue_18468</c>
/// (lines 2955-3030), the <c>regex.sub</c> assertions at the top of the method.
/// </summary>
/// <remarks>
/// Upstream's point is that <c>sub</c> returns a plain <c>str</c> even when handed a <c>str</c>
/// subclass, and the same for the <c>bytes</c> family - a Python typing question with no C#
/// equivalent, since <c>string</c> cannot be subclassed. Only #1, the plain <c>str</c> case, has
/// anything to assert here; #2 is the <c>StrSubclass</c> repeat and #3-6 are the <c>bytes</c>,
/// <c>BytesSubclass</c>, <c>bytearray</c> and <c>memoryview</c> repeats. See
/// <c>docs/PORTMAP.md</c>.
/// </remarks>
public sealed class Issue18468Tests
{
    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_issue_18468#1")]
    public void Sub_replaces_a_literal() => FuzzyRegex.Replace("xyz", "y", "a").Should().Be("xaz");
}
