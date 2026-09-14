using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// The assertions that only compile a pattern. Upstream writes them as
/// <c>assertEqual(repr(type(regex.compile(p))), self.PATTERN_CLASS)</c>, which says nothing more
/// than "this parsed", so they become a check that construction does not throw.
/// </remarks>
public sealed class FuzzySyntaxTests
{
    [Test]
    [Arguments("(fou){s,e<=1}")]
    [Arguments("(fuu){s}")]
    [Arguments("(fuu){s,e}")]
    [Arguments("(anaconda){1i+1d<1,s<=1}")]
    [Arguments("(anaconda){1i+1d<1,s<=1,e<=10}")]
    [Arguments("(anaconda){s<=1,e<=1,1i+1d<1}")]
    [Arguments("(approximate){s<=3,1i+1d<3}")]
    [Property("Upstream", "RegexTests.test_fuzzy#1-6,18")]
    public void A_fuzzy_constraint_compiles(string pattern)
    {
        Action act = () => _ = Upstream.Compile(pattern);

        act.Should().NotThrow();
    }
}
