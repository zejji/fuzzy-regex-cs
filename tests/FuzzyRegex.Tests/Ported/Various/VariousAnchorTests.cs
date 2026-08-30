using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 59 rows of the table that wait on <c>anchors</c>: 33 that match and 26 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousAnchorTests
{
    [Test]
    [Arguments("^abc$", "abc", "0", new string?[] { "abc" })]
    [Arguments("^abc", "abcc", "0", new string?[] { "abc" })]
    [Arguments("abc$", "aabc", "0", new string?[] { "abc" })]
    [Arguments("^", "abc", "0", new string?[] { "" })]
    [Arguments("$", "abc", "0", new string?[] { "" })]
    [Arguments("\\ba\\b", "a-", "0", new string?[] { "a" })]
    [Arguments("\\ba\\b", "-a", "0", new string?[] { "a" })]
    [Arguments("\\ba\\b", "-a-", "0", new string?[] { "a" })]
    [Arguments("x\\B", "xyz", "0", new string?[] { "x" })]
    [Arguments("\\Bz", "xyz", "0", new string?[] { "z" })]
    [Arguments("\\By\\b", "xy", "0", new string?[] { "y" })]
    [Arguments("\\by\\B", "yz", "0", new string?[] { "y" })]
    [Arguments("\\By\\B", "xyz", "0", new string?[] { "y" })]
    [Arguments("a\\\\b", "a\\b", "0", new string?[] { "a\\b" })]
    [Arguments("^a(bc+|b[eh])g|.h$", "abh", "0,1", new string?[] { "bh", null })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "effgz", "0,1,2", new string?[] { "effgz", "effgz", null })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "ij", "0,1,2", new string?[] { "ij", "ij", "j" })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "reffgz", "0,1,2", new string?[] { "effgz", "effgz", null })]
    [Arguments("^(.+)?B", "AB", "1", new string?[] { "A" })]
    [Arguments("^abc$", "abc", "0", new string?[] { "abc" })]
    [Arguments("^abc", "abcc", "0", new string?[] { "abc" })]
    [Arguments("abc$", "aabc", "0", new string?[] { "abc" })]
    [Arguments("^", "abc", "0", new string?[] { "" })]
    [Arguments("$", "abc", "0", new string?[] { "" })]
    [Arguments("a\\\\b", "a\\b", "0", new string?[] { "a\\b" })]
    [Arguments("^a(bc+|b[eh])g|.h$", "abh", "0,1", new string?[] { "bh", null })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "effgz", "0,1,2", new string?[] { "effgz", "effgz", null })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "ij", "0,1,2", new string?[] { "ij", "ij", "j" })]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "reffgz", "0,1,2", new string?[] { "effgz", "effgz", null })]
    [Arguments("(([a-z]+):)?([a-z]+)$", "smil", "1,2,3", new string?[] { null, null, "smil" })]
    [Arguments("^((a)c)?(ab)$", "ab", "1,2,3", new string?[] { null, null, "ab" })]
    [Arguments("\\b.\\b", "a", "0", new string?[] { "a" })]
    [Arguments("\\b.\\b", "\u00C4", "0", new string?[] { "\u00C4" })]
    [Skip("needs:anchors - the engine has no anchor opcodes yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#70,72,74-76,103-105,110-111,119-121,129,160-162,165,175,240,242,244-246,279,320-322,325,495,518,522-523"
    )]
    public void Search_returns_the_expected_group_values(
        string pattern,
        string subject,
        string groups,
        string?[] expected
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        m.Success.Should().BeTrue();
        VariousTable.GroupValues(m, groups).Should().Equal(expected);
    }

    [Test]
    [Arguments("^abc$", "abcc")]
    [Arguments("^abc$", "aabc")]
    [Arguments("\\by\\b", "xy")]
    [Arguments("\\by\\b", "yz")]
    [Arguments("\\by\\b", "xyz")]
    [Arguments("x\\b", "xyz")]
    [Arguments("z\\B", "xyz")]
    [Arguments("\\Bx", "xyz")]
    [Arguments("\\Ba\\B", "a-")]
    [Arguments("\\Ba\\B", "-a")]
    [Arguments("\\Ba\\B", "-a-")]
    [Arguments("\\By\\B", "xy")]
    [Arguments("\\By\\B", "yz")]
    [Arguments("$b", "b")]
    [Arguments("^(ab|cd)e", "abcde")]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "effg")]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "bcdd")]
    [Arguments("^abc$", "abcc")]
    [Arguments("^abc$", "aabc")]
    [Arguments("$b", "b")]
    [Arguments("^(ab|cd)e", "abcde")]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "effg")]
    [Arguments("(bc+d$|ef*g.|h?i(j|k))", "bcdd")]
    [Arguments("^abc", "jkl\nabc\nxyz")]
    [Arguments("^.*?$", "one\ntwo\nthree\n")]
    [Arguments("^a*?$", "foo")]
    [Skip("needs:anchors - the engine has no anchor opcodes yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#71,73,106-109,112-118,125,147,163-164,241,243,274,307,323-324,483,515,517"
    )]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
