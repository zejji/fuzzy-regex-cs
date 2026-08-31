using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 81 rows of the table that wait on <c>quantifiers</c>: 69 that match and 12 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousQuantifierTests
{
    [Test]
    [Arguments("ab*c", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab*bc", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab*bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab*bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab+bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab+bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab?bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab?bc", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab?c", "abc", "0", new string?[] { "abc" })]
    [Arguments("a.*c", "axyzc", "0", new string?[] { "axyzc" })]
    [Arguments("a\\(*b", "ab", "0", new string?[] { "ab" })]
    [Arguments("a\\(*b", "a((b", "0", new string?[] { "a((b" })]
    [Arguments("a+b+c", "aabbabc", "0", new string?[] { "abc" })]
    [Arguments("(a+|b)*", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b)+", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b)?", "ab", "0,1", new string?[] { "a", "a" })]
    [Arguments("a*", "", "0", new string?[] { "" })]
    [Arguments("abcd*efg", "abcdefg", "0", new string?[] { "abcdefg" })]
    [Arguments("ab*", "xabyabbbz", "0", new string?[] { "ab" })]
    [Arguments("ab*", "xayabbbz", "0", new string?[] { "a" })]
    [Arguments("(a|b)c*d", "abcd", "0,1", new string?[] { "bcd", "b" })]
    [Arguments("(ab|ab*)bc", "abc", "0,1", new string?[] { "abc", "a" })]
    [Arguments("(ab|a)b*c", "abc", "0,1", new string?[] { "abc", "ab" })]
    [Arguments("(.*)c(.*)", "abcde", "0,1,2", new string?[] { "abcde", "ab", "de" })]
    [Arguments("\\((.*), (.*)\\)", "(a, b)", "2,1", new string?[] { "b", "a" })]
    [Arguments("(a)+x", "aaax", "0,1", new string?[] { "aaax", "a" })]
    [Arguments("(a)+b|aac", "aac", "0,1", new string?[] { "aac", null })]
    [Arguments("ab*c", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab*bc", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab*bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab*bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab{0,}bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab+bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab+bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab{1,}bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab{1,3}bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab{3,4}bc", "abbbbc", "0", new string?[] { "abbbbc" })]
    [Arguments("ab?bc", "abbc", "0", new string?[] { "abbc" })]
    [Arguments("ab?bc", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab{0,1}bc", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab?c", "abc", "0", new string?[] { "abc" })]
    [Arguments("ab{0,1}c", "abc", "0", new string?[] { "abc" })]
    [Arguments("a.*c", "axyzc", "0", new string?[] { "axyzc" })]
    [Arguments("a\\(*b", "ab", "0", new string?[] { "ab" })]
    [Arguments("a\\(*b", "a((b", "0", new string?[] { "a((b" })]
    [Arguments("a+b+c", "aabbabc", "0", new string?[] { "abc" })]
    [Arguments("a{1,}b{1,}c", "aabbabc", "0", new string?[] { "abc" })]
    [Arguments("a.+?c", "abcabc", "0", new string?[] { "abc" })]
    [Arguments("(a+|b)*", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b){0,}", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b)+", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b){1,}", "ab", "0,1", new string?[] { "ab", "b" })]
    [Arguments("(a+|b)?", "ab", "0,1", new string?[] { "a", "a" })]
    [Arguments("(a+|b){0,1}", "ab", "0,1", new string?[] { "a", "a" })]
    [Arguments("a*", "", "0", new string?[] { "" })]
    [Arguments("abcd*efg", "abcdefg", "0", new string?[] { "abcdefg" })]
    [Arguments("ab*", "xabyabbbz", "0", new string?[] { "ab" })]
    [Arguments("ab*", "xayabbbz", "0", new string?[] { "a" })]
    [Arguments("(a|b)c*d", "abcd", "0,1", new string?[] { "bcd", "b" })]
    [Arguments("(ab|ab*)bc", "abc", "0,1", new string?[] { "abc", "a" })]
    [Arguments("(ab|a)b*c", "abc", "0,1", new string?[] { "abc", "ab" })]
    [Arguments("(.*)c(.*)", "abcde", "0,1,2", new string?[] { "abcde", "ab", "de" })]
    [Arguments("\\((.*), (.*)\\)", "(a, b)", "2,1", new string?[] { "b", "a" })]
    [Arguments("a(?:b|c|d)(.)", "ace", "1", new string?[] { "e" })]
    [Arguments("a(?:b|c|d)*(.)", "ace", "1", new string?[] { "e" })]
    [Arguments("a(?:b|c|d)+?(.)", "ace", "1", new string?[] { "e" })]
    [Arguments("a(?:b|(c|e){1,2}?|d)+?(.)", "ace", "1,2", new string?[] { "c", "e" })]
    [Arguments(".*d", "abc\nabd", "0", new string?[] { "abd" })]
    [Arguments("(x?)?", "x", "0", new string?[] { "x" })]
    [Property(
        "Upstream",
        "RegexTests.test_various#58-62,65-67,69,79,127-128,132-135,139,142-144,149-150,157,169-170,190,199,220-225,229-232,234-236,238-239,249,277-278,284-285,287-293,297,302-304,309-310,317,333-334,470-473,497,500"
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
    [Arguments("a.*b", "acc\nccb")]
    [Arguments("a.{4,5}b", "acc\nccb")]
    [Arguments("ab+bc", "abc")]
    [Arguments("ab+bc", "abq")]
    [Arguments("ab?bc", "abbbbc")]
    [Arguments("a.*c", "axyzd")]
    [Arguments("ab+bc", "abc")]
    [Arguments("ab+bc", "abq")]
    [Arguments("ab{1,}bc", "abq")]
    [Arguments("ab{4,5}bc", "abbbbc")]
    [Arguments("ab?bc", "abbbbc")]
    [Arguments("a.*c", "axyzd")]
    [Property("Upstream", "RegexTests.test_various#42-43,63-64,68,80,226-228,233,237,250")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
