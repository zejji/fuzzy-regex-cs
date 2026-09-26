using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The four S20 behaviours no ported test reaches, three of which the differential oracle only
/// found once its generator was widened to look for them. Every expected value here was measured
/// against the installed <c>regex</c> 2026.7.19 on 2026-08-31 and is quoted beside the assertion.
/// </summary>
/// <remarks>
/// Gap tests, not ported ones: they pin our own answers where upstream has no test of its own, so
/// they do not count towards the parity percentage. Each walks the subject with
/// <c>Match(input, beginning)</c> rather than <c>Matches</c>, which is S25.
/// </remarks>
public sealed class BoundaryTests
{
    // U+1F1EC and U+1F1E7 REGIONAL INDICATOR SYMBOL LETTERS G and B - the flag of the United
    // Kingdom. Both are astral, so each is one character and two UTF-16 code units.
    private const string _flagGb = "\U0001F1EC\U0001F1E7";

    // U+1F1EB and U+1F1F7, the flag of France.
    private const string _flagFr = "\U0001F1EB\U0001F1F7";

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Grapheme_cluster_pairs_regional_indicators_by_character_not_by_code_unit()
    {
        // GB12/GB13 do not break between regional indicators when an odd number of them precedes
        // the position. Upstream reads that count off a subtraction of codepoint indices; every
        // regional indicator is two UTF-16 code units, so the same subtraction here would always be
        // even and the rule would never fire - each flag would come back as two clusters.
        //
        // regex 2026.7.19: finditer(r"\X", flagGb + flagFr) -> codepoints (0,2) and (2,4), which in
        // UTF-16 is (index 0, length 4) and (index 4, length 4).
        var pattern = new FuzzyRegex(@"\X");
        string subject = _flagGb + _flagFr;

        Match first = pattern.Match(subject);
        Match second = pattern.Match(subject, 4);

        (first.Index, first.Length).Should().Be((0, 4));
        (second.Index, second.Length).Should().Be((4, 4));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Grapheme_cluster_leaves_an_unpaired_regional_indicator_on_its_own()
    {
        // The odd one out: three regional indicators are one cluster of two and one of one, which
        // is what makes the rule a parity test rather than a "pair them up" test.
        //
        // regex 2026.7.19: finditer(r"\X", flagGb + "\U0001F1EB") -> codepoints (0,2) and (2,3),
        // which in UTF-16 is (0,4) and (4,2).
        var pattern = new FuzzyRegex(@"\X");
        string subject = _flagGb + "\U0001F1EB";

        Match first = pattern.Match(subject);
        Match second = pattern.Match(subject, 4);

        (first.Index, first.Length).Should().Be((0, 4));
        (second.Index, second.Length).Should().Be((4, 2));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Keep_marker_restores_the_match_start_when_the_engine_backtracks_past_it()
    {
        // '\K' moves the reported match start, and a branch that fails after it has to put the old
        // one back. Here the first alternative gets as far as 'b', moves the start to 1, then fails
        // on 'c'; the second alternative matches the whole subject from 0.
        //
        // regex 2026.7.19: search(r"a\Kbc|abd", "abd").span() -> (0, 3).
        Match m = FuzzyRegex.Match("abd", @"a\Kbc|abd");

        m.Index.Should().Be(0);
        m.Value.Should().Be("abd");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Default_word_boundary_breaks_between_an_apostrophe_and_a_vowel()
    {
        // DIVERGES FROM UPSTREAM, deliberately (2026-09-26, ledger entry 40). Upstream keeps an
        // apostrophe and a following vowel together under a rule it labels WB5a, deciding "vowel"
        // by simple-lowercasing, so U+0130 LATIN CAPITAL LETTER I WITH DOT ABOVE counted as one and
        // U+0131 LATIN SMALL LETTER DOTLESS I did not: finditer(r"(?w)\b", "'İ") starts at 0 and 2,
        // and for "'ı" at 0, 1 and 2 (regex 2026.7.19, 2026-08-31). UAX #29 has no such default
        // rule, and WordBreakTest.txt 17.0.0 line 1001 breaks "'A" after the apostrophe, so both
        // subjects break there now. See DefaultWordBoundaryTests.
        var pattern = new FuzzyRegex(@"(?w)\b");

        pattern.Match("'İ", 1).Index.Should().Be(1);
        pattern.Match("'ı", 1).Index.Should().Be(1);
    }
}
