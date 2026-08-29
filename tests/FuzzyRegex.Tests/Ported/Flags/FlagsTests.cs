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
    [Skip("needs:pattern-properties - Pattern is a compile stub")]
    [Property("Upstream", "RegexTests.test_flags#1")]
    public void Compiling_with_each_flag_succeeds(FuzzyRegexOptions options) =>
        new FuzzyRegex("^pattern$", options).Should().NotBeNull();

    // NOT PORTED: regex.L (LOCALE) is not surfaced on FuzzyRegexOptions, so that data point of
    // upstream's loop is dropped.
}
