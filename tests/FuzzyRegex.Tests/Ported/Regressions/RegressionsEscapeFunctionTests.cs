using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about <c>Escape</c>.
/// </summary>
public sealed class RegressionsEscapeFunctionTests
{
    // Hg issue 97: behaviour of regex.escape's special_only is wrong.
    // Hg issue 244: Make `special_only=True` the default in `regex.escape()`.
    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#65")]
    public void Escaping_every_character_escapes_the_non_special_punctuation_too() =>
        FuzzyRegex.Escape("foo!?", specialOnly: false).Should().Be(@"foo\!\?");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#66")]
    public void Escaping_only_special_characters_leaves_the_exclamation_mark_alone() =>
        FuzzyRegex.Escape("foo!?", specialOnly: true).Should().Be(@"foo!\?");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#67")]
    public void Escape_defaults_to_special_only() => FuzzyRegex.Escape("foo!?").Should().Be(@"foo!\?");

    // Hg issue 249: Add an option to regex.escape() to not escape spaces.
    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#290")]
    public void Escaping_every_character_with_spaces_not_literal_also_escapes_the_space() =>
        FuzzyRegex.Escape(" ,0A[", specialOnly: false, literalSpaces: false).Should().Be(@"\ \,0A\[");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#291")]
    public void Escaping_every_character_with_literal_spaces_leaves_the_space_alone() =>
        FuzzyRegex.Escape(" ,0A[", specialOnly: false, literalSpaces: true).Should().Be(@" \,0A\[");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#292")]
    public void Escaping_only_special_characters_with_spaces_not_literal_still_escapes_the_space() =>
        FuzzyRegex.Escape(" ,0A[", specialOnly: true, literalSpaces: false).Should().Be(@"\ ,0A\[");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#293")]
    public void Escaping_only_special_characters_with_literal_spaces_leaves_the_space_alone() =>
        FuzzyRegex.Escape(" ,0A[", specialOnly: true, literalSpaces: true).Should().Be(@" ,0A\[");

    [Test]
    [Skip("needs:escape-function - FuzzyRegex.Escape is unimplemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#294")]
    public void Escape_with_default_arguments_escapes_the_space_but_not_the_comma() =>
        FuzzyRegex.Escape(" ,0A[").Should().Be(@"\ ,0A\[");
}
