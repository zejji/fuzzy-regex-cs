using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 25 rows of the table that wait on <c>backrefs</c>: 24 that match and 1 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousBackrefTests
{
    [Test]
    [Arguments("[\\1]", "\u0001", "0", new string?[] { "\u0001" })]
    [Arguments("\\141", "a", "0", new string?[] { "a" })]
    [Arguments(
        "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)\\119",
        "abcdefghijklk9",
        "0,11",
        new string?[] { "abcdefghijklk9", "k" }
    )]
    [Arguments(
        "^\\w+=(\\\\[\\000-\\277]|[^\\n\\\\])*",
        "SRC=eval.c g.c blah blah blah \\\\\n\tapes.c",
        "0",
        new string?[] { "SRC=eval.c g.c blah blah blah \\\\" }
    )]
    [Arguments("(abc)\\1", "abcabc", "1", new string?[] { "abc" })]
    [Arguments("([a-c]*)\\1", "abcabc", "1", new string?[] { "abc" })]
    [Arguments("(a+).\\1$", "aaaaa", "0,1", new string?[] { "aaaaa", "aa" })]
    [Arguments("(abc)\\1", "abcabc", "0,1", new string?[] { "abcabc", "abc" })]
    [Arguments("([a-c]+)\\1", "abcabc", "0,1", new string?[] { "abcabc", "abc" })]
    [Arguments("(a)\\1", "aa", "0,1", new string?[] { "aa", "a" })]
    [Arguments("(a+)\\1", "aa", "0,1", new string?[] { "aa", "a" })]
    [Arguments("(a+)+\\1", "aa", "0,1", new string?[] { "aa", "a" })]
    [Arguments("(a).+\\1", "aba", "0,1", new string?[] { "aba", "a" })]
    [Arguments("(a)ba*\\1", "aba", "0,1", new string?[] { "aba", "a" })]
    [Arguments("(aa|a)a\\1$", "aaa", "0,1", new string?[] { "aaa", "a" })]
    [Arguments("(a|aa)a\\1$", "aaa", "0,1", new string?[] { "aaa", "a" })]
    [Arguments("(a+)a\\1$", "aaa", "0,1", new string?[] { "aaa", "a" })]
    [Arguments("([abc]*)\\1", "abcabc", "0,1", new string?[] { "abcabc", "abc" })]
    [Arguments("(.)\\g<1>", "gg", "0", new string?[] { "gg" })]
    [Arguments("(.)\\g<1>", "gg", "*", new string?[] { "gg", "g" })]
    [Arguments("((((((((((a))))))))))\\10", "aa", "0", new string?[] { "aa" })]
    [Arguments("(abc)\\1", "abcabc", "1", new string?[] { "abc" })]
    [Arguments("([a-c]*)\\1", "abcabc", "1", new string?[] { "abc" })]
    [Arguments("[\\41]", "!", "0", new string?[] { "!" })]
    [Property("Upstream", "RegexTests.test_various#20,22-23,39,173-174,176,178-188,206-207,327,337-338,499")]
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
    [Arguments("^(a+).\\1$", "aaaa")]
    [Property("Upstream", "RegexTests.test_various#177")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
