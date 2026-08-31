using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Boundaries;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_word_class</c>
/// (lines 1192-1203).
/// </summary>
public sealed class WordClassTests
{
    // Devanagari "हिन्दी" (U+0939 U+093F U+0928 U+094D U+0926 U+0940), written as char casts
    // rather than pasting the literal script into source.
    private const char _ha = (char)0x0939;
    private const char _vowelI = (char)0x093F;
    private const char _na = (char)0x0928;
    private const char _virama = (char)0x094D;
    private const char _da = (char)0x0926;
    private const char _vowelIi = (char)0x0940;
    private static readonly string _hindi = $"{_ha}{_vowelI}{_na}{_virama}{_da}{_vowelIi}";
    private static readonly string _subject = $" {_hindi},";

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_word_class#1")]
    public void Word_class_findall_matches_the_whole_devanagari_run() =>
        FuzzyRegex.Matches(_subject, @"\w+").Select(m => m.Value).Should().Equal(_hindi);

    [Test]
    [Skip("needs:find-all - the class matches; Matches/Count are S25")]
    [Property("Upstream", "RegexTests.test_word_class#2")]
    public void Non_word_class_findall_matches_the_surrounding_space_and_comma() =>
        FuzzyRegex.Matches(_subject, @"\W+").Select(m => m.Value).Should().Equal(" ", ",");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_class#3")]
    public void Splitting_on_a_word_boundary_isolates_the_devanagari_run() =>
        FuzzyRegex.Split(_subject, @"(?V1)\b").Should().Equal(" ", _hindi, ",");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_word_class#4")]
    public void Splitting_on_a_non_word_boundary_splits_between_every_devanagari_character() =>
        FuzzyRegex
            .Split(_subject, @"(?V1)\B")
            .Should()
            .Equal("", $" {_ha}", $"{_vowelI}", $"{_na}", $"{_virama}", $"{_da}", $"{_vowelIi},", "");
}
