using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 77 rows of the table that wait on <c>character-classes</c>: 60 that match and 17 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousCharacterClassTests
{
    [Test]
    [Arguments("[\\0a]", "\u0000", "0", new string?[] { "\u0000" })]
    [Arguments("[a\\0]", "\u0000", "0", new string?[] { "\u0000" })]
    [Arguments("\\a[\\b]\\f\\n\\r\\t\\v", "\a\b\f\n\r\t\v", "0", new string?[] { "\a\b\f\n\r\t\v" })]
    [Arguments("[\\a][\\b][\\f][\\n][\\r][\\t][\\v]", "\a\b\f\n\r\t\v", "0", new string?[] { "\a\b\f\n\r\t\v" })]
    [Arguments("[\\t][\\n][\\v][\\r][\\f][\\b]", "\t\n\v\r\f\b", "0", new string?[] { "\t\n\v\r\f\b" })]
    [Arguments("a[bc]d", "abd", "0", new string?[] { "abd" })]
    [Arguments("a[b-d]e", "ace", "0", new string?[] { "ace" })]
    [Arguments("a[b-d]", "aac", "0", new string?[] { "ac" })]
    [Arguments("a[-b]", "a-", "0", new string?[] { "a-" })]
    [Arguments("a[\\-b]", "a-", "0", new string?[] { "a-" })]
    [Arguments("a[b-]", "a-", "0", new string?[] { "a-" })]
    [Arguments("a[]]b", "a]b", "0", new string?[] { "a]b" })]
    [Arguments("a[]]b", "a]b", "0", new string?[] { "a]b" })]
    [Arguments("a[^bc]d", "aed", "0", new string?[] { "aed" })]
    [Arguments("a[^-b]c", "adc", "0", new string?[] { "adc" })]
    [Arguments("a[^]b]c", "adc", "0", new string?[] { "adc" })]
    [Arguments("[^ab]*", "cde", "0", new string?[] { "cde" })]
    [Arguments("[abhgefdc]ij", "hij", "0", new string?[] { "hij" })]
    [Arguments("a([bc]*)c*", "abc", "0,1", new string?[] { "abc", "bc" })]
    [Arguments("a([bc]*)(c*d)", "abcd", "0,1,2", new string?[] { "abcd", "bc", "d" })]
    [Arguments("a([bc]+)(c*d)", "abcd", "0,1,2", new string?[] { "abcd", "bc", "d" })]
    [Arguments("a([bc]*)(c+d)", "abcd", "0,1,2", new string?[] { "abcd", "b", "cd" })]
    [Arguments("a[bcd]*dcdcde", "adcdcde", "0", new string?[] { "adcdcde" })]
    [Arguments("[a-zA-Z_][a-zA-Z0-9_]*", "alpha", "0", new string?[] { "alpha" })]
    [Arguments("a[-]?c", "ac", "0", new string?[] { "ac" })]
    [Arguments("([ac])+x", "aacx", "0,1", new string?[] { "aacx", "c" })]
    [Arguments(
        "([^/]*/)*sub1/",
        "d:msgs/tdir/sub1/trial/away.cpp",
        "0,1",
        new string?[] { "d:msgs/tdir/sub1/", "tdir/" }
    )]
    [Arguments(
        "([^.]*)\\.([^:]*):[T ]+(.*)",
        "track1.title:TBlah blah blah",
        "0,1,2,3",
        new string?[] { "track1.title:TBlah blah blah", "track1", "title", "Blah blah blah" }
    )]
    [Arguments("([^N]*N)+", "abNNxyzN", "0,1", new string?[] { "abNNxyzN", "xyzN" })]
    [Arguments("([^N]*N)+", "abNNxyz", "0,1", new string?[] { "abNN", "N" })]
    [Arguments("([abc]*)x", "abcx", "0,1", new string?[] { "abcx", "abc" })]
    [Arguments("([xyz]*)x", "abcx", "0,1", new string?[] { "x", "" })]
    [Arguments("a[bc]d", "abd", "0", new string?[] { "abd" })]
    [Arguments("a[b-d]e", "ace", "0", new string?[] { "ace" })]
    [Arguments("a[b-d]", "aac", "0", new string?[] { "ac" })]
    [Arguments("a[-b]", "a-", "0", new string?[] { "a-" })]
    [Arguments("a[b-]", "a-", "0", new string?[] { "a-" })]
    [Arguments("a[]]b", "a]b", "0", new string?[] { "a]b" })]
    [Arguments("a[^bc]d", "aed", "0", new string?[] { "aed" })]
    [Arguments("a[^-b]c", "adc", "0", new string?[] { "adc" })]
    [Arguments("a[^]b]c", "adc", "0", new string?[] { "adc" })]
    [Arguments("[^ab]*", "cde", "0", new string?[] { "cde" })]
    [Arguments("([abc])*d", "abbbcd", "0,1", new string?[] { "abbbcd", "c" })]
    [Arguments("([abc])*bcd", "abcd", "0,1", new string?[] { "abcd", "a" })]
    [Arguments("[abhgefdc]ij", "hij", "0", new string?[] { "hij" })]
    [Arguments("a([bc]*)c*", "abc", "0,1", new string?[] { "abc", "bc" })]
    [Arguments("a([bc]*)(c*d)", "abcd", "0,1,2", new string?[] { "abcd", "bc", "d" })]
    [Arguments("a([bc]+)(c*d)", "abcd", "0,1,2", new string?[] { "abcd", "bc", "d" })]
    [Arguments("a([bc]*)(c+d)", "abcd", "0,1,2", new string?[] { "abcd", "b", "cd" })]
    [Arguments("a[bcd]*dcdcde", "adcdcde", "0", new string?[] { "adcdcde" })]
    [Arguments("[a-zA-Z_][a-zA-Z0-9_]*", "alpha", "0", new string?[] { "alpha" })]
    [Arguments("a[-]?c", "ac", "0", new string?[] { "ac" })]
    [Arguments("[\\w]+", "--ab_cd0123--", "0", new string?[] { "ab_cd0123" })]
    [Arguments("[\\D]+", "1234abc5678", "0", new string?[] { "abc" })]
    [Arguments("[\\da-fA-F]+", "123abc", "0", new string?[] { "123abc" })]
    [Arguments("([\\s]*)([\\S]*)([\\s]*)", " testing!1972", "3,2,1", new string?[] { "", "testing!1972", " " })]
    [Arguments("[\\w-]+", "laser_beam", "0", new string?[] { "laser_beam" })]
    [Arguments("a[ ]*?\\ (\\d+).*", "a   10", "0", new string?[] { "a   10" })]
    [Arguments("a[ ]*?\\ (\\d+).*", "a    10", "0", new string?[] { "a    10" })]
    [Arguments("\"(?:\\\\\"|[^\"])*?\"", "\"\\\"\"", "0", new string?[] { "\"\\\"\"" })]
    [Skip("needs:character-classes - the engine has no character-class opcode yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#25-26,28-29,38,82,84-88,95-97,99,102,137,146,151-155,159,172,191-196,198,252,254-257,262-263,265,268,295,298-299,306,311-315,319,336,489,491-493,505,507-508,514"
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
    [Arguments("[^a\\0]", "\u0000")]
    [Arguments("a[bc]d", "abc")]
    [Arguments("a[b-d]e", "abd")]
    [Arguments("a[^bc]d", "abd")]
    [Arguments("a[^-b]c", "a-c")]
    [Arguments("a[^]b]c", "a]c")]
    [Arguments("a[bcd]+dcdcde", "adcdcde")]
    [Arguments("[k]", "ab")]
    [Arguments("([abc]*)x", "abc")]
    [Arguments("a[bc]d", "abc")]
    [Arguments("a[b-d]e", "abd")]
    [Arguments("a[^bc]d", "abd")]
    [Arguments("a[^-b]c", "a-c")]
    [Arguments("a[^]b]c", "a]c")]
    [Arguments("a[bcd]+dcdcde", "adcdcde")]
    [Arguments("[k]", "ab")]
    [Arguments("a[^>]*?b", "a>b")]
    [Skip("needs:character-classes - the engine has no character-class opcode yet")]
    [Property("Upstream", "RegexTests.test_various#27,81,83,98,100-101,156,171,197,251,253,264,266-267,316,335,516")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
