using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_turkic</c> (lines 2532-2557):
/// case-insensitive matching among the four Turkish dotted/dotless I forms.
/// </summary>
/// <remarks>
/// Upstream builds its 4-character alphabet and its 10-pair "matching" set from a small loop
/// rather than a literal table, then nested-loops over all 16 (ch1, ch2) combinations, failing
/// only on a mismatch - so there is no literal table to copy. The 16 rows below were instead
/// produced by running upstream's own loop against the local oracle 2026-08-30
/// (<c>regex 2026.7.19</c>) and recording every observed outcome:
/// U+0049 'I', U+0069 'i', U+0131 'ı' (LATIN SMALL LETTER DOTLESS I),
/// U+0130 'İ' (LATIN CAPITAL LETTER I WITH DOT ABOVE). All four characters are in the BMP, so no
/// UTF-16 index translation applies (only <c>Success</c> is asserted, not a span).
/// </remarks>
public sealed class TurkicTests
{
    [Test]
    [Arguments("I", "I", true)]
    [Arguments("I", "i", true)]
    [Arguments("I", "ı", true)]
    [Arguments("I", "İ", false)]
    [Arguments("i", "I", true)]
    [Arguments("i", "i", true)]
    [Arguments("i", "ı", false)]
    [Arguments("i", "İ", true)]
    [Arguments("ı", "I", true)]
    [Arguments("ı", "i", false)]
    [Arguments("ı", "ı", true)]
    [Arguments("ı", "İ", false)]
    [Arguments("İ", "I", false)]
    [Arguments("İ", "i", true)]
    [Arguments("İ", "ı", false)]
    [Arguments("İ", "İ", true)]
    [Skip("needs:case-folding - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_turkic#1-2")]
    public void Dotted_and_dotless_I_forms_fold_under_ignore_case_exactly_as_upstream_observed(
        string ch1,
        string ch2,
        bool expectedMatch
    )
    {
        Match m = FuzzyRegex.MatchAtStart(ch2, "(?i)\\A" + ch1 + "\\Z");

        m.Success.Should().Be(expectedMatch);
    }
}
