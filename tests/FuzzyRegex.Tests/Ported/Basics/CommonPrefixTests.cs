using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Basics;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_common_prefix</c>
/// (lines 2485-2491).
/// </summary>
/// <remarks>
/// Upstream's single assertion checks that <c>regex.compile(...)</c> returns a <c>Pattern</c>
/// rather than raising - there is no distinct "compiled type" to inspect on this side, so the
/// equivalent behaviour is that construction succeeds. Verified against the local oracle
/// 2026-08-30: the 499-character pattern this produces compiles fine upstream.
/// </remarks>
public sealed class CommonPrefixTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_common_prefix#1")]
    public void Compiling_an_alternation_with_a_very_long_common_prefix_succeeds()
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string side = string.Concat(Enumerable.Repeat(lower + digits + upper, 4));
        string pattern = $"({side}|{side})";

        Action construct = () => _ = new FuzzyRegex(pattern);

        construct.Should().NotThrow();
    }
}
