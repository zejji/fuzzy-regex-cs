using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_sub_template_numeric_escape</c>
/// (lines 146-201).
/// </summary>
/// <remarks>
/// Upstream asserts the same result for the template <c>\1111</c> written two ways (as the
/// literal escape and as <c>\111</c> concatenated with a literal <c>1</c>); both are the same
/// three-digit octal escape followed by a literal digit, so assertions #8 and #9 fold onto one
/// row here, as the exemplar's remarks section for a folded pair recommends.
/// </remarks>
public sealed class SubTemplateNumericEscapeTests
{
    [Test]
    [Arguments(@"\0", "\0")]
    [Arguments(@"\000", "\0")]
    [Arguments(@"\001", "\x1")]
    [Arguments(@"\008", "\08")]
    [Arguments(@"\009", "\09")]
    [Arguments(@"\111", "I")]
    [Arguments(@"\117", "O")]
    [Arguments(@"\1111", "I1")]
    [Arguments(@"\00", "\0")]
    [Arguments(@"\07", "\a")]
    [Arguments(@"\08", "\08")]
    [Arguments(@"\09", "\09")]
    [Arguments(@"\0a", "\0a")]
    [Arguments(@"\400", "\x100")]
    [Arguments(@"\777", "\x1FF")]
    [Skip("needs:substitution - octal numeric escapes in Pattern.Replace templates are not implemented yet")]
    [Property("Upstream", "RegexTests.test_sub_template_numeric_escape#1-16")]
    public void Replace_expands_an_octal_numeric_escape(string replacement, string expected) =>
        FuzzyRegex.Replace("x", "x", replacement).Should().Be(expected);

    // NOT PORTED: assertions #17-18 (lines 167-168) use bytes patterns, templates and subjects
    // (`b'x'`, `br'\400'`, `br'\777'`); this port is char-based only.

    [Test]
    // Upstream asserts a specific message (INVALID_GROUP_REF = "invalid group reference"); we
    // assert only the exception type since our parser's messages are not decided yet.
    [Arguments(@"\1")]
    [Arguments(@"\8")]
    [Arguments(@"\9")]
    [Arguments(@"\11")]
    [Arguments(@"\18")]
    [Arguments(@"\1a")]
    [Arguments(@"\90")]
    [Arguments(@"\99")]
    [Arguments(@"\118")] // r'\11' + '8'
    [Arguments(@"\11a")]
    [Arguments(@"\181")] // r'\18' + '1'
    [Arguments(@"\800")] // r'\80' + '0'
    [Skip("needs:parse-errors - Pattern.Replace does not yet validate numeric group references in templates")]
    [Property("Upstream", "RegexTests.test_sub_template_numeric_escape#19-30")]
    public void Replace_with_an_invalid_numeric_group_reference_throws(string replacement)
    {
        Action act = () => _ = FuzzyRegex.Replace("x", "x", replacement);

        act.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    [Arguments("(((((((((((x)))))))))))", @"\11", "x", "x")]
    [Arguments("((((((((((y))))))))))(.)", @"\118", "xyz", "xz8")]
    [Arguments("((((((((((y))))))))))(.)", @"\11a", "xyz", "xza")]
    [Skip(
        "needs:substitution - two-digit group references resolved against deeply nested groups are not implemented yet"
    )]
    [Property("Upstream", "RegexTests.test_sub_template_numeric_escape#31-33")]
    public void Replace_resolves_a_two_digit_group_reference_against_deeply_nested_groups(
        string pattern,
        string replacement,
        string subject,
        string expected
    ) => FuzzyRegex.Replace(subject, pattern, replacement).Should().Be(expected);
}
