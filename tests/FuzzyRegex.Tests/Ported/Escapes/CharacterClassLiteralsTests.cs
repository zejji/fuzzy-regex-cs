using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Escapes;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c>
/// <c>test_sre_character_class_literals</c> (lines 741-751).
/// </summary>
/// <remarks>
/// Upstream's <c>for i in [0, 8, 16, 32, 64, 127, 128, 255]:</c> loop is unrolled into
/// <c>[Arguments(...)]</c> rows, as in <see cref="CharacterLiteralsTests"/>. Unlike that file, the
/// trailing literal character goes inside the set alongside the escape, not appended to the
/// subject: each row's subject is always just the escaped character alone.
/// </remarks>
public sealed class CharacterClassLiteralsTests
{
    [Test]
    [Arguments(0, @"[\000]")]
    [Arguments(8, @"[\010]")]
    [Arguments(16, @"[\020]")]
    [Arguments(32, @"[\040]")]
    [Arguments(64, @"[\100]")]
    [Arguments(127, @"[\177]")]
    [Arguments(128, @"[\200]")]
    [Arguments(255, @"[\377]")]
    [Skip("needs:escapes - the parser has no octal literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#1")]
    public void Set_with_bare_octal_escape_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"[\0000]")]
    [Arguments(8, @"[\0100]")]
    [Arguments(16, @"[\0200]")]
    [Arguments(32, @"[\0400]")]
    [Arguments(64, @"[\1000]")]
    [Arguments(127, @"[\1770]")]
    [Arguments(128, @"[\2000]")]
    [Arguments(255, @"[\3770]")]
    [Skip("needs:escapes - the parser has no octal literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#2")]
    public void Set_with_octal_escape_plus_a_literal_zero_member_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"[\0008]")]
    [Arguments(8, @"[\0108]")]
    [Arguments(16, @"[\0208]")]
    [Arguments(32, @"[\0408]")]
    [Arguments(64, @"[\1008]")]
    [Arguments(127, @"[\1778]")]
    [Arguments(128, @"[\2008]")]
    [Arguments(255, @"[\3778]")]
    [Skip("needs:escapes - the parser has no octal literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#3")]
    public void Set_with_octal_escape_plus_a_literal_eight_member_matches_the_character(
        int codepoint,
        string pattern
    ) => FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"[\x00]")]
    [Arguments(8, @"[\x08]")]
    [Arguments(16, @"[\x10]")]
    [Arguments(32, @"[\x20]")]
    [Arguments(64, @"[\x40]")]
    [Arguments(127, @"[\x7f]")]
    [Arguments(128, @"[\x80]")]
    [Arguments(255, @"[\xff]")]
    [Skip("needs:escapes - the parser has no hex literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#4")]
    public void Set_with_bare_hex_escape_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"[\x000]")]
    [Arguments(8, @"[\x080]")]
    [Arguments(16, @"[\x100]")]
    [Arguments(32, @"[\x200]")]
    [Arguments(64, @"[\x400]")]
    [Arguments(127, @"[\x7f0]")]
    [Arguments(128, @"[\x800]")]
    [Arguments(255, @"[\xff0]")]
    [Skip("needs:escapes - the parser has no hex literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#5")]
    public void Set_with_hex_escape_plus_a_literal_zero_member_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"[\x00z]")]
    [Arguments(8, @"[\x08z]")]
    [Arguments(16, @"[\x10z]")]
    [Arguments(32, @"[\x20z]")]
    [Arguments(64, @"[\x40z]")]
    [Arguments(127, @"[\x7fz]")]
    [Arguments(128, @"[\x80z]")]
    [Arguments(255, @"[\xffz]")]
    [Skip("needs:escapes - the parser has no hex literal escapes yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#6")]
    public void Set_with_hex_escape_plus_a_literal_z_member_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    // Upstream asserts the error message matches self.BAD_OCTAL_ESCAPE; we do not assert message
    // text, per the port's own error-message conventions (not yet decided).
    [Test]
    // The parse error itself arrived with S13 - measured against regex 2026.7.19 on 2026-08-31,
    // '[\\911]' -> error msg='bad escape \\9' pos=5, and PatternCompiler raises exactly that -
    // but MatchAtStart is still a bare stub that never compiles its pattern.
    [Skip("needs:basic-matching - FuzzyRegex.MatchAtStart does not compile the pattern yet")]
    [Property("Upstream", "RegexTests.test_sre_character_class_literals#7")]
    public void Set_with_an_invalid_octal_escape_fails_to_compile()
    {
        Action act = () => FuzzyRegex.MatchAtStart("", @"[\911]");

        act.Should().Throw<FuzzyRegexParseException>();
    }
}
