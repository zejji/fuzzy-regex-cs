using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_ascii_and_unicode_flag</c>
/// (lines 964-989).
/// </summary>
/// <remarks>
/// <para>
/// That method is otherwise not ported - it exists to contrast the <c>ASCII</c> and
/// <c>UNICODE</c> flags, and neither is surfaced on <see cref="FuzzyRegexOptions"/> (see
/// <c>docs/PORTMAP.md</c>). Its loop runs the body twice, once with no flags and once with
/// <c>UNICODE</c>, so the no-flag iteration asserts behaviour that holds without either flag and
/// therefore does port.
/// </para>
/// <para>
/// Only this assertion is rescued. The loop's other one - that <c>À</c> matches <c>à</c> under
/// <c>IGNORECASE</c> - is already covered by <c>test_ignore_case</c> in
/// <c>Ported/CaseFolding/IgnoreCaseTests.cs</c>, whereas nothing else in the ported range pins
/// <c>\w</c> against a non-ASCII letter: <c>test_special_escapes</c> exercises
/// <c>\d\D\w\W\s\S</c> over ASCII only.
/// </para>
/// <para>
/// Verified against the local oracle 2026-08-29:
/// <c>python -c "import regex; print(bool(regex.compile(r'\w').match('\xe0')))"</c> prints
/// <c>True</c>.
/// </para>
/// </remarks>
public sealed class WordClassIsUnicodeByDefaultTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_ascii_and_unicode_flag#2,4")]
    public void Word_class_matches_a_non_ascii_letter_without_any_flag() =>
        // U+00E0 LATIN SMALL LETTER A WITH GRAVE - upstream's '\xe0'.
        Upstream.MatchAtStart("à", @"\w").Success.Should().BeTrue();
}
