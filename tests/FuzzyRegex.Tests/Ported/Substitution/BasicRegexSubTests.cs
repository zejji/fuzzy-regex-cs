using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_basic_regex_sub</c>
/// (lines 82-111).
/// </summary>
public sealed class BasicRegexSubTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#1")]
    public void Replace_honours_an_inline_case_insensitive_flag() =>
        FuzzyRegex.Replace("bbbb BBBB", "(?i)b+", "x").Should().Be("x x");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#2")]
    public void Replace_with_an_evaluator_transforms_every_match() =>
        FuzzyRegex
            .Replace("08.2 -2 23x99y", @"\d+", m => (int.Parse(m.Value) + 1).ToString())
            .Should()
            .Be("9.3 -3 24x100y");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#3")]
    public void Replace_with_an_evaluator_and_a_count_stops_early() =>
        new FuzzyRegex(@"\d+")
            .Replace("08.2 -2 23x99y", m => (int.Parse(m.Value) + 1).ToString(), 3)
            .Should()
            .Be("9.3 -3 23x99y");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#4")]
    public void Replace_with_an_evaluator_does_not_expand_escapes_in_its_return_value() =>
        FuzzyRegex.Replace("x", ".", _ => "\\n").Should().Be("\\n");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#5")]
    public void Replace_with_a_string_template_expands_escapes() =>
        FuzzyRegex.Replace("x", ".", @"\n").Should().Be("\n");

    [Test]
    [Arguments(@"\g<a>\g<a>")]
    [Arguments(@"\g<a>\g<1>")]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#6-7")]
    public void Replace_a_named_group_by_name_or_number_in_the_template(string replacement) =>
        FuzzyRegex.Replace("xx", "(?P<a>x)", replacement).Should().Be("xxxx");

    [Test]
    [Arguments(@"\g<unk>\g<unk>")]
    [Arguments(@"\g<1>\g<1>")]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#8-9")]
    public void Replace_a_group_named_unk_by_name_or_number_in_the_template(string replacement) =>
        FuzzyRegex.Replace("xx", "(?P<unk>x)", replacement).Should().Be("xxxx");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#10")]
    public void Replace_expands_control_character_escapes_in_a_verbatim_template() =>
        FuzzyRegex.Replace("a", "a", @"\t\n\v\r\f\a\b").Should().Be("\t\n\v\r\f\a\b");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#11-12")]
    public void Replace_passes_literal_control_characters_in_a_template_through() =>
        // Unlike #10, upstream's template here is a plain string holding the control characters
        // themselves, with no \b. Upstream asserts it twice, the second time spelling the expected
        // text as chr(9) + chr(10) + chr(11) + chr(13) + chr(12) + chr(7); in C# "\t\n\v\r\f\a"
        // already *is* that explicit sequence, so the two assertions collapse into one.
        FuzzyRegex.Replace("a", "a", "\t\n\v\r\f\a").Should().Be("\t\n\v\r\f\a");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#13")]
    public void Replace_with_an_anchored_star_quantifier_matches_the_empty_prefix() =>
        FuzzyRegex.Replace("test", @"^\s*", "X").Should().Be("Xtest");

    [Test]
    [Arguments(@"\x0A")]
    // Upstream's four-hex-digit Unicode escape template (backslash, lowercase u, then the four
    // hex digits for line feed) is built here by concatenation so it is unambiguous in source.
    [Arguments(@"\" + "u000A")]
    [Arguments(@"\U0000000A")]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#14-16")]
    public void Replace_expands_a_numeric_character_escape_in_a_template(string replacement) =>
        FuzzyRegex.Replace("x", "x", replacement).Should().Be("\n");

    [Test]
    [Property("Upstream", "RegexTests.test_basic_regex_sub#17")]
    public void Replace_expands_a_named_unicode_escape_in_a_template() =>
        FuzzyRegex.Replace("x", "x", @"\N{LATIN CAPITAL LETTER A}").Should().Be("A");

    // NOT PORTED: assertion #18 (line 111) uses a bytes pattern, template and subject
    // (`br"..."` / `b"..."`); this port is char-based only.
}
