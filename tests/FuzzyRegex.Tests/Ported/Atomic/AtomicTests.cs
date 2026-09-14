using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Atomic;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_atomic</c> (lines 1306-1308).
/// </summary>
public sealed class AtomicTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_atomic#1")]
    public void Atomic_group_does_not_backtrack_into_the_star_it_wraps() =>
        Upstream.Match("aa", "(?>a*)a").Success.Should().BeFalse();
}
