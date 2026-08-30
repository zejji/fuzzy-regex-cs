using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 125 rows of the table that wait on <c>ignore-case</c>: 100 that match and 25 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousIgnoreCaseTests
{
    [Test]
    [Arguments("(?i)abc", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)abc", "XABCY", "0", new string?[] { "ABC" })]
    [Arguments("(?i)abc", "ABABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab*c", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab*bc", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab*bc", "ABBC", "0", new string?[] { "ABBC" })]
    [Arguments("(?i)ab*?bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab{0,}?bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab+?bc", "ABBC", "0", new string?[] { "ABBC" })]
    [Arguments("(?i)ab+bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab{1,}?bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab{1,3}?bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab{3,4}?bc", "ABBBBC", "0", new string?[] { "ABBBBC" })]
    [Arguments("(?i)ab??bc", "ABBC", "0", new string?[] { "ABBC" })]
    [Arguments("(?i)ab??bc", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab{0,1}?bc", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab??c", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)ab{0,1}?c", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)^abc$", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)^abc", "ABCC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)abc$", "AABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)^", "ABC", "0", new string?[] { "" })]
    [Arguments("(?i)$", "ABC", "0", new string?[] { "" })]
    [Arguments("(?i)a.c", "ABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)a.c", "AXC", "0", new string?[] { "AXC" })]
    [Arguments("(?i)a.*?c", "AXYZC", "0", new string?[] { "AXYZC" })]
    [Arguments("(?i)a[bc]d", "ABD", "0", new string?[] { "ABD" })]
    [Arguments("(?i)a[b-d]e", "ACE", "0", new string?[] { "ACE" })]
    [Arguments("(?i)a[b-d]", "AAC", "0", new string?[] { "AC" })]
    [Arguments("(?i)a[-b]", "A-", "0", new string?[] { "A-" })]
    [Arguments("(?i)a[b-]", "A-", "0", new string?[] { "A-" })]
    [Arguments("(?i)a]", "A]", "0", new string?[] { "A]" })]
    [Arguments("(?i)a[]]b", "A]B", "0", new string?[] { "A]B" })]
    [Arguments("(?i)a[^bc]d", "AED", "0", new string?[] { "AED" })]
    [Arguments("(?i)a[^-b]c", "ADC", "0", new string?[] { "ADC" })]
    [Arguments("(?i)a[^]b]c", "ADC", "0", new string?[] { "ADC" })]
    [Arguments("(?i)ab|cd", "ABC", "0", new string?[] { "AB" })]
    [Arguments("(?i)ab|cd", "ABCD", "0", new string?[] { "AB" })]
    [Arguments("(?i)()ef", "DEF", "0,1", new string?[] { "EF", "" })]
    [Arguments("(?i)a\\(b", "A(B", "*", new string?[] { "A(B" })]
    [Arguments("(?i)a\\(*b", "AB", "0", new string?[] { "AB" })]
    [Arguments("(?i)a\\(*b", "A((B", "0", new string?[] { "A((B" })]
    [Arguments("(?i)a\\\\b", "A\\B", "0", new string?[] { "A\\B" })]
    [Arguments("(?i)((a))", "ABC", "0,1,2", new string?[] { "A", "A", "A" })]
    [Arguments("(?i)(a)b(c)", "ABC", "0,1,2", new string?[] { "ABC", "A", "C" })]
    [Arguments("(?i)a+b+c", "AABBABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)a{1,}b{1,}c", "AABBABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)a.+?c", "ABCABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)a.*?c", "ABCABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)a.{0,5}?c", "ABCABC", "0", new string?[] { "ABC" })]
    [Arguments("(?i)(a+|b)*", "AB", "0,1", new string?[] { "AB", "B" })]
    [Arguments("(?i)(a+|b){0,}", "AB", "0,1", new string?[] { "AB", "B" })]
    [Arguments("(?i)(a+|b)+", "AB", "0,1", new string?[] { "AB", "B" })]
    [Arguments("(?i)(a+|b){1,}", "AB", "0,1", new string?[] { "AB", "B" })]
    [Arguments("(?i)(a+|b)?", "AB", "0,1", new string?[] { "A", "A" })]
    [Arguments("(?i)(a+|b){0,1}", "AB", "0,1", new string?[] { "A", "A" })]
    [Arguments("(?i)(a+|b){0,1}?", "AB", "0,1", new string?[] { "", null })]
    [Arguments("(?i)[^ab]*", "CDE", "0", new string?[] { "CDE" })]
    [Arguments("(?i)a*", "", "0", new string?[] { "" })]
    [Arguments("(?i)([abc])*d", "ABBBCD", "0,1", new string?[] { "ABBBCD", "C" })]
    [Arguments("(?i)([abc])*bcd", "ABCD", "0,1", new string?[] { "ABCD", "A" })]
    [Arguments("(?i)a|b|c|d|e", "E", "0", new string?[] { "E" })]
    [Arguments("(?i)(a|b|c|d|e)f", "EF", "0,1", new string?[] { "EF", "E" })]
    [Arguments("(?i)abcd*efg", "ABCDEFG", "0", new string?[] { "ABCDEFG" })]
    [Arguments("(?i)ab*", "XABYABBBZ", "0", new string?[] { "AB" })]
    [Arguments("(?i)ab*", "XAYABBBZ", "0", new string?[] { "A" })]
    [Arguments("(?i)(ab|cd)e", "ABCDE", "0,1", new string?[] { "CDE", "CD" })]
    [Arguments("(?i)[abhgefdc]ij", "HIJ", "0", new string?[] { "HIJ" })]
    [Arguments("(?i)(abc|)ef", "ABCDEF", "0,1", new string?[] { "EF", "" })]
    [Arguments("(?i)(a|b)c*d", "ABCD", "0,1", new string?[] { "BCD", "B" })]
    [Arguments("(?i)(ab|ab*)bc", "ABC", "0,1", new string?[] { "ABC", "A" })]
    [Arguments("(?i)a([bc]*)c*", "ABC", "0,1", new string?[] { "ABC", "BC" })]
    [Arguments("(?i)a([bc]*)(c*d)", "ABCD", "0,1,2", new string?[] { "ABCD", "BC", "D" })]
    [Arguments("(?i)a([bc]+)(c*d)", "ABCD", "0,1,2", new string?[] { "ABCD", "BC", "D" })]
    [Arguments("(?i)a([bc]*)(c+d)", "ABCD", "0,1,2", new string?[] { "ABCD", "B", "CD" })]
    [Arguments("(?i)a[bcd]*dcdcde", "ADCDCDE", "0", new string?[] { "ADCDCDE" })]
    [Arguments("(?i)(ab|a)b*c", "ABC", "0,1", new string?[] { "ABC", "AB" })]
    [Arguments("(?i)((a)(b)c)(d)", "ABCD", "1,2,3,4", new string?[] { "ABC", "A", "B", "D" })]
    [Arguments("(?i)[a-zA-Z_][a-zA-Z0-9_]*", "ALPHA", "0", new string?[] { "ALPHA" })]
    [Arguments("(?i)^a(bc+|b[eh])g|.h$", "ABH", "0,1", new string?[] { "BH", null })]
    [Arguments("(?i)(bc+d$|ef*g.|h?i(j|k))", "EFFGZ", "0,1,2", new string?[] { "EFFGZ", "EFFGZ", null })]
    [Arguments("(?i)(bc+d$|ef*g.|h?i(j|k))", "IJ", "0,1,2", new string?[] { "IJ", "IJ", "J" })]
    [Arguments("(?i)(bc+d$|ef*g.|h?i(j|k))", "REFFGZ", "0,1,2", new string?[] { "EFFGZ", "EFFGZ", null })]
    [Arguments("(?i)((((((((((a))))))))))", "A", "10", new string?[] { "A" })]
    [Arguments("(?i)((((((((((a))))))))))\\10", "AA", "0", new string?[] { "AA" })]
    [Arguments("(?i)(((((((((a)))))))))", "A", "0", new string?[] { "A" })]
    [Arguments("(?i)(?:(?:(?:(?:(?:(?:(?:(?:(?:(a))))))))))", "A", "1", new string?[] { "A" })]
    [Arguments("(?i)(?:(?:(?:(?:(?:(?:(?:(?:(?:(a|b|c))))))))))", "C", "1", new string?[] { "C" })]
    [Arguments("(?i)multiple words", "MULTIPLE WORDS, YEAH", "0", new string?[] { "MULTIPLE WORDS" })]
    [Arguments("(?i)(.*)c(.*)", "ABCDE", "0,1,2", new string?[] { "ABCDE", "AB", "DE" })]
    [Arguments("(?i)\\((.*), (.*)\\)", "(A, B)", "2,1", new string?[] { "B", "A" })]
    [Arguments("(?i)a[-]?c", "AC", "0", new string?[] { "AC" })]
    [Arguments("(?i)(abc)\\1", "ABCABC", "1", new string?[] { "ABC" })]
    [Arguments("(?i)([a-c]*)\\1", "ABCABC", "1", new string?[] { "ABC" })]
    [Arguments("w(?i)", "w", "0", new string?[] { "w" })]
    [Arguments("(?i)w", "W", "0", new string?[] { "W" })]
    [Arguments("(?i)M+", "MMM", "0", new string?[] { "MMM" })]
    [Arguments("(?i)m+", "MMM", "0", new string?[] { "MMM" })]
    [Arguments("(?i)[M]+", "MMM", "0", new string?[] { "MMM" })]
    [Arguments("(?i)[m]+", "MMM", "0", new string?[] { "MMM" })]
    [Skip("needs:ignore-case - the engine does no case-insensitive matching yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#339,343-350,354-357,359-361,363-365,367,369-374,377,379-382,386-388,390,393-396,401-404,407-410,412-421,423,425-434,436-443,445-450,453-458,460-462,464-466,480-481,510-513"
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
    [Arguments("(?i)abc", "XBC")]
    [Arguments("(?i)abc", "AXC")]
    [Arguments("(?i)abc", "ABX")]
    [Arguments("(?i)ab+bc", "ABC")]
    [Arguments("(?i)ab+bc", "ABQ")]
    [Arguments("(?i)ab{1,}bc", "ABQ")]
    [Arguments("(?i)ab{4,5}?bc", "ABBBBC")]
    [Arguments("(?i)ab??bc", "ABBBBC")]
    [Arguments("(?i)^abc$", "ABCC")]
    [Arguments("(?i)^abc$", "AABC")]
    [Arguments("(?i)a.*c", "AXYZD")]
    [Arguments("(?i)a[bc]d", "ABC")]
    [Arguments("(?i)a[b-d]e", "ABD")]
    [Arguments("(?i)a[^bc]d", "ABD")]
    [Arguments("(?i)a[^-b]c", "A-C")]
    [Arguments("(?i)a[^]b]c", "A]C")]
    [Arguments("(?i)$b", "B")]
    [Arguments("(?i)abc", "")]
    [Arguments("(?i)^(ab|cd)e", "ABCDE")]
    [Arguments("(?i)a[bcd]+dcdcde", "ADCDCDE")]
    [Arguments("(?i)(bc+d$|ef*g.|h?i(j|k))", "EFFG")]
    [Arguments("(?i)(bc+d$|ef*g.|h?i(j|k))", "BCDD")]
    [Arguments("(?i)multiple words of text", "UH-UH")]
    [Arguments("(?i)[k]", "AB")]
    [Arguments("w(?i)", "W")]
    [Skip("needs:ignore-case - the engine does no case-insensitive matching yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#340-342,351-353,358,362,366,368,375-376,378,389,391-392,399,424,435,444,451-452,459,463,479"
    )]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
