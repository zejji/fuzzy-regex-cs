using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Escapes;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_sre_character_literals</c>
/// (lines 725-739).
/// </summary>
/// <remarks>
/// Upstream's <c>for i in [0, 8, 16, 32, 64, 127, 128, 255]:</c> loop is unrolled into
/// <c>[Arguments(...)]</c> rows. The octal (<c>%03o</c>) and hex (<c>%02x</c>) pattern text for
/// each <c>i</c> is plain base conversion, computed by hand: e.g. 127 in octal is 177, in hex 7f.
/// </remarks>
public sealed class CharacterLiteralsTests
{
    [Test]
    [Arguments(0, @"\000")]
    [Arguments(8, @"\010")]
    [Arguments(16, @"\020")]
    [Arguments(32, @"\040")]
    [Arguments(64, @"\100")]
    [Arguments(127, @"\177")]
    [Arguments(128, @"\200")]
    [Arguments(255, @"\377")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#1")]
    public void Octal_escape_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"\0000")]
    [Arguments(8, @"\0100")]
    [Arguments(16, @"\0200")]
    [Arguments(32, @"\0400")]
    [Arguments(64, @"\1000")]
    [Arguments(127, @"\1770")]
    [Arguments(128, @"\2000")]
    [Arguments(255, @"\3770")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#2")]
    public void Octal_escape_followed_by_a_literal_zero_matches_both(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint) + "0", pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"\0008")]
    [Arguments(8, @"\0108")]
    [Arguments(16, @"\0208")]
    [Arguments(32, @"\0408")]
    [Arguments(64, @"\1008")]
    [Arguments(127, @"\1778")]
    [Arguments(128, @"\2008")]
    [Arguments(255, @"\3778")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#3")]
    public void Octal_escape_followed_by_a_literal_eight_matches_both(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint) + "8", pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"\x00")]
    [Arguments(8, @"\x08")]
    [Arguments(16, @"\x10")]
    [Arguments(32, @"\x20")]
    [Arguments(64, @"\x40")]
    [Arguments(127, @"\x7f")]
    [Arguments(128, @"\x80")]
    [Arguments(255, @"\xff")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#4")]
    public void Hex_escape_matches_the_character(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint).ToString(), pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"\x000")]
    [Arguments(8, @"\x080")]
    [Arguments(16, @"\x100")]
    [Arguments(32, @"\x200")]
    [Arguments(64, @"\x400")]
    [Arguments(127, @"\x7f0")]
    [Arguments(128, @"\x800")]
    [Arguments(255, @"\xff0")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#5")]
    public void Hex_escape_followed_by_a_literal_zero_matches_both(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint) + "0", pattern).Success.Should().BeTrue();

    [Test]
    [Arguments(0, @"\x00z")]
    [Arguments(8, @"\x08z")]
    [Arguments(16, @"\x10z")]
    [Arguments(32, @"\x20z")]
    [Arguments(64, @"\x40z")]
    [Arguments(127, @"\x7fz")]
    [Arguments(128, @"\x80z")]
    [Arguments(255, @"\xffz")]
    [Property("Upstream", "RegexTests.test_sre_character_literals#6")]
    public void Hex_escape_followed_by_a_literal_z_matches_both(int codepoint, string pattern) =>
        FuzzyRegex.MatchAtStart(((char)codepoint) + "z", pattern).Success.Should().BeTrue();

    // Upstream asserts the error message matches self.INVALID_GROUP_REF; we do not assert message
    // text, per the port's own error-message conventions (not yet decided).
    [Test]
    // The parse error itself arrived with S13 - measured against regex 2026.7.19 on 2026-08-31,
    // '\\911' -> error msg='invalid group reference' pos=3, and PatternCompiler raises exactly
    // that. S16 made MatchAtStart compile its pattern, so the rejection now reaches the caller.
    [Property("Upstream", "RegexTests.test_sre_character_literals#7")]
    public void Escape_with_no_matching_group_fails_to_compile()
    {
        Action act = static () => FuzzyRegex.MatchAtStart("", @"\911");

        act.Should().Throw<FuzzyRegexParseException>();
    }
}
