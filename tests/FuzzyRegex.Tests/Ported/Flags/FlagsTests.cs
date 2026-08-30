using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Flags;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_flags</c> (lines 720-723).
/// </summary>
/// <remarks>
/// Upstream loops over <c>[I, M, X, S, L]</c> and checks each compiles to a <c>Pattern</c>; the
/// type check is trivially true in C# (the constructor always returns a <see cref="FuzzyRegex"/>
/// or throws), so the port asserts the meaningful part instead - that compiling with each flag
/// succeeds.
/// </remarks>
public sealed class FlagsTests
{
    [Test]
    [Arguments(FuzzyRegexOptions.IgnoreCase)]
    [Arguments(FuzzyRegexOptions.Multiline)]
    [Arguments(FuzzyRegexOptions.IgnorePatternWhitespace)]
    [Arguments(FuzzyRegexOptions.Singleline)]
    // Retagged in S07 from needs:pattern-properties. The pattern-level properties this slice
    // delivers are not what holds this test back: '^' and '$' are, and the first-set optimisation
    // upstream then runs over "pattern" needs the SetUnion node too. Both arrive in S08, and a
    // skip reason that names the wrong capability sends the next slice to the wrong place.
    [Skip("needs:anchors - ^ and $ have no zero-width position nodes yet")]
    [Property("Upstream", "RegexTests.test_flags#1")]
    public void Compiling_with_each_flag_succeeds(FuzzyRegexOptions options) =>
        new FuzzyRegex("^pattern$", options).Should().NotBeNull();

    // NOT PORTED: regex.L (LOCALE) is not surfaced on FuzzyRegexOptions, so that data point of
    // upstream's loop is dropped.
}
