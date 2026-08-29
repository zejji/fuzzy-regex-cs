using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Escapes;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_special_escapes</c>
/// (lines 491-522): the <c>\d \D \w \W \s \S</c> run. The <c>\b \B ^ $ \A \Z</c> assertions from
/// the same method live in <c>Ported/Anchors</c> instead.
/// </summary>
public sealed class SpecialEscapesCharacterClassesTests
{
    // Upstream's second assertion repeats the first with an explicit regex.UNICODE flag, which is
    // incidental here (str patterns are Unicode by default and the flag is not surfaced on
    // FuzzyRegexOptions); folded into one test.
    [Test]
    [Skip("needs:character-classes - the engine has no \\d \\D \\w \\W \\s \\S matching yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#15,17")]
    public void Digit_word_space_classes_and_their_negations_match_in_sequence() =>
        FuzzyRegex.Match("1aa! a", "\\d\\D\\w\\W\\s\\S").Value.Should().Be("1aa! a");
}
