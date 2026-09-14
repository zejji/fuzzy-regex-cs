using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_turkic</c> (lines 2532-2557):
/// case-insensitive matching among the four Turkish dotted/dotless I forms.
/// </summary>
/// <remarks>
/// <para>
/// Upstream builds its 4-character alphabet and its 10-pair "matching" set from a small loop
/// rather than a literal table, then nested-loops over all 16 (ch1, ch2) combinations, failing
/// only on a mismatch - so there is no literal table to copy. The 16 rows below were instead
/// produced by running upstream's own loop against the local oracle 2026-08-30
/// (<c>regex 2026.7.19</c>) and recording every observed outcome:
/// U+0049 'I', U+0069 'i', U+0131 'ı' (LATIN SMALL LETTER DOTLESS I),
/// U+0130 'İ' (LATIN CAPITAL LETTER I WITH DOT ABOVE). All four characters are in the BMP, so no
/// UTF-16 index translation applies (only <c>Success</c> is asserted, not a span).
/// </para>
/// <para>
/// <b>FOUR OF THE SIXTEEN NOW ANSWER DIFFERENTLY HERE, ON PURPOSE.</b> S45 replaced upstream's
/// Turkic case data with the default one <c>CaseFolding.txt</c> specifies - the file says of the
/// <c>T</c> rows that "the default option is to exclude them", and upstream's table builder merges
/// them in. So <c>I</c>~<c>ı</c>, <c>ı</c>~<c>I</c>, <c>i</c>~<c>İ</c> and <c>İ</c>~<c>i</c> match
/// upstream and do not match here; PCRE2 10.47, Perl 5.42.2 and .NET 10.0.10 were each run on the
/// same grid on 2026-09-14 and none of the three matches them either. The reasoning, the quoted
/// rows and the engine grid are in <c>Unicode.TurkicDefaults</c>; the behaviour is pinned in
/// <c>Gaps.Engine.CaseFoldingTests</c>.
/// </para>
/// <para>
/// Both columns are kept below rather than one, and the test asserts BOTH: the upstream column so
/// this stays a faithful record of what <c>test_turkic</c> requires, and this port's so the four
/// deliberate cells cannot quietly become three. <c>test_turkic</c> is therefore recorded as a
/// KNOWN DIVERGENCE for the parity board, not as a pass.
/// </para>
/// </remarks>
public sealed class TurkicTests
{
    /// <param name="ch1">The pattern character.</param>
    /// <param name="ch2">The subject character.</param>
    /// <param name="upstreamMatch">What <c>regex</c> answers - upstream's own assertion.</param>
    /// <param name="expectedMatch">What this port answers under the default case data.</param>
    [Test]
    [Arguments("I", "I", true, true)]
    [Arguments("I", "i", true, true)]
    // DIVERGES: 0049; T; 0131 is Turkic-only, so I and ı are unrelated by default.
    [Arguments("I", "ı", true, false)]
    [Arguments("I", "İ", false, false)]
    [Arguments("i", "I", true, true)]
    [Arguments("i", "i", true, true)]
    [Arguments("i", "ı", false, false)]
    // DIVERGES: the other half of the same T row, reached through İ's case set.
    [Arguments("i", "İ", true, false)]
    // DIVERGES: 0049; T; 0131 again, this time with ı as the pattern.
    [Arguments("ı", "I", true, false)]
    [Arguments("ı", "i", false, false)]
    [Arguments("ı", "ı", true, true)]
    [Arguments("ı", "İ", false, false)]
    [Arguments("İ", "I", false, false)]
    // DIVERGES: 0130; T; 0069 is Turkic-only. Under the default data İ simple-folds to itself.
    [Arguments("İ", "i", true, false)]
    [Arguments("İ", "ı", false, false)]
    [Arguments("İ", "İ", true, true)]
    [Property("Upstream", "RegexTests.test_turkic#1-2")]
    public void Dotted_and_dotless_I_forms_fold_under_ignore_case_by_the_default_case_data(
        string ch1,
        string ch2,
        bool upstreamMatch,
        bool expectedMatch
    )
    {
        Match m = Upstream.MatchAtStart(ch2, "(?i)\\A" + ch1 + "\\Z");

        m.Success.Should().Be(expectedMatch);

        // The four cells where the two columns differ are S45's whole point; asserting the count
        // here means dropping one of them from the table cannot go unnoticed.
        (upstreamMatch != expectedMatch)
            .Should()
            .Be(
                (ch1, ch2) is ("I", "ı") or ("ı", "I") or ("i", "İ") or ("İ", "i"),
                "only the four Turkic-only cells diverge from upstream"
            );
    }
}
