using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c> (lines 3084-4410),
/// the assertions about full case folding.
/// </summary>
/// <remarks>
/// <para>
/// Assertions #74-81 (Hg issue 101) compile two patterns with <c>regex.FULLCASE | regex.UNICODE</c>
/// and <c>regex.FULLCASE | regex.IGNORECASE | regex.UNICODE</c>; there is no <c>UNICODE</c> option
/// (it is upstream's always-on default), so they are ported with <see cref="FuzzyRegexOptions.FullCase"/>
/// alone, and with <c>FullCase | IgnoreCase</c> for the second pattern. Verified against the local
/// oracle 2026-08-30: <c>regex.findall(r'(?f)xxx', 'yxxx')</c> gives the same <c>['xxx']</c> as the
/// <c>FULLCASE|UNICODE</c> compile. Upstream repeats each of the eight checks once for a literal
/// string and once for a variable holding an identical string, which is a Python object-identity
/// concern with no C# counterpart; all eight are still ported, one test each, so the assertion
/// count stays honest, with the repeats named to make the duplication obvious.
/// </para>
/// <para>
/// Assertion #474 compiles with <c>(?ifu)</c>; the trailing <c>u</c> is <c>UNICODE</c>, which again
/// has no option and is always on, so it is dropped from the ported pattern's inline flags rather
/// than invented as an option.
/// </para>
/// </remarks>
public sealed class RegressionsCaseFoldingTests
{
    // U+017F LATIN SMALL LETTER LONG S.
    private const string _longS = "ſ";

    // U+FB06 LATIN SMALL LIGATURE ST.
    private const string _ligatureSt = "ﬆ";

    // U+FB05 LATIN SMALL LIGATURE LONG S T.
    private const string _ligatureLongSt = "ﬅ";

    // U+00F6 LATIN SMALL LETTER O WITH DIAERESIS.
    private const string _oWithDiaeresis = "ö";

    // U+00E4 LATIN SMALL LETTER A WITH DIAERESIS.
    private const string _aWithDiaeresis = "ä";

    private static readonly string _postSubject =
        "POST, Post, post, po" + _longS + "t, po" + _ligatureSt + ", and po" + _ligatureLongSt;

    private static readonly FuzzyRegex _xxxFullCase = new("xxx", FuzzyRegexOptions.FullCase);

    private static readonly FuzzyRegex _xxxFullCaseIgnoreCase = new(
        "xxx",
        FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase
    );

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#34")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void First_alternative_wins_when_it_can_be_matched_without_expanding_a_ligature() =>
        FuzzyRegex
            .Matches(_postSubject, @"(?fi)pos|post")
            .Select(m => m.Value)
            .Should()
            .Equal("POS", "Pos", "pos", "po" + _longS, "po" + _ligatureSt, "po" + _ligatureLongSt);

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#35")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Longer_alternative_still_wins_when_tried_first() =>
        FuzzyRegex
            .Matches(_postSubject, @"(?fi)post|pos")
            .Select(m => m.Value)
            .Should()
            .Equal("POST", "Post", "post", "po" + _longS + "t", "po" + _ligatureSt, "po" + _ligatureLongSt);

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#36")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Post_alternative_matches_even_when_the_other_branch_cannot() =>
        FuzzyRegex
            .Matches(_postSubject, @"(?fi)post|another")
            .Select(m => m.Value)
            .Should()
            .Equal("POST", "Post", "post", "po" + _longS + "t", "po" + _ligatureSt, "po" + _ligatureLongSt);

    // Hg issue 101: findall() broken (seems like memory corruption).
    //
    // Eight upstream assertions, one body here. Upstream checks the same compiled pattern through
    // both finditer and findall, and repeats each over a literal and over a variable holding an
    // identical string. `Matches` is the single C# call behind finditer and findall, and the
    // literal/variable repeat is a Python object-identity concern with no C# counterpart, so all
    // eight differ only in which pattern they use. They stay eight test cases rather than being
    // folded into two, so that the count on the status board still matches upstream's assertion
    // count. The upstream index is carried as a row value because eight identical [Arguments]
    // rows would not be distinguishable from one another.
    [Test]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#74-81")]
    [Arguments(74, false)]
    [Arguments(75, false)]
    [Arguments(76, false)]
    [Arguments(77, false)]
    [Arguments(78, true)]
    [Arguments(79, true)]
    [Arguments(80, true)]
    [Arguments(81, true)]
    public void Full_case_pattern_matches_xxx_through_finditer_and_findall_alike(
        int upstreamAssertion,
        bool ignoreCase
    ) =>
        (ignoreCase ? _xxxFullCaseIgnoreCase : _xxxFullCase)
            .Matches("yxxx")
            .Select(m => m.Value)
            .Should()
            .Equal(["xxx"], "upstream assertion #{0} expects a single xxx match", upstreamAssertion);

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#224")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Full_case_backreference_matches_the_same_word_repeated_in_lowercase()
    {
        // Hg issue 194: .FULLCASE and Backreference.
        Match m = FuzzyRegex.Match("<cli><cli>", @"(?if)<(CLI)><\1>");

        (m.Index, m.Index + m.Length).Should().Be((0, 10));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#225")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Full_case_backreference_matches_the_word_repeated_in_a_different_case()
    {
        Match m = FuzzyRegex.Match("<cli><clI>", @"(?if)<(CLI)><\1>");

        (m.Index, m.Index + m.Length).Should().Be((0, 10));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#226")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Full_case_backreference_matches_right_to_left_with_the_word_repeated_in_a_different_case()
    {
        Match m = FuzzyRegex.Match("<cli><clI>", @"(?ifr)<\1><(CLI)>");

        (m.Index, m.Index + m.Length).Should().Be((0, 10));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#271")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Optional_leading_character_under_full_case_and_ignore_case_still_finds_the_later_match()
    {
        // Hg issue 227: Incorrect behavior for ? operator with UNICODE + IGNORECASE.
        Match m = FuzzyRegex.Match("xxxxyz", "a?yz", FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase);

        (m.Index, m.Index + m.Length).Should().Be((4, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#474")]
    [Skip("needs:case-folding - the engine has no full case folding yet")]
    public void Full_case_group_followed_by_a_literal_that_never_occurs_in_the_subject_does_not_match()
    {
        string subject =
            "Yrkesh"
            + _oWithDiaeresis
            + "gskola"
            + string.Concat(Enumerable.Repeat(" . Studie" + _aWithDiaeresis + "mnen", 7));

        FuzzyRegex
            .Match(subject, @"(?if)(H\N{LATIN SMALL LETTER O WITH DIAERESIS}gskolan?)[\\s\\S]*p")
            .Success.Should()
            .BeFalse();
    }
}
