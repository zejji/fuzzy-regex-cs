using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about named lists.
/// </summary>
public sealed class RegressionsNamedListTests
{
    // U+017F LATIN SMALL LETTER LONG S.
    private const string _longS = "ſ";

    // U+FB06 LATIN SMALL LIGATURE ST.
    private const string _ligatureSt = "ﬆ";

    // U+FB05 LATIN SMALL LIGATURE LONG S T.
    private const string _ligatureLongSt = "ﬅ";

    // Hg issue 50: not all keywords are found by named list with overlapping keywords when full
    // Unicode casefolding is required.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#33")]
    public void Full_case_folding_finds_every_casefold_variant_of_overlapping_keywords()
    {
        var regex = Upstream.Compile(
            @"(?fi)\L<keywords>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["keywords"] = ["post", "pos"],
            }
        );
        string subject = $"POST, Post, post, po{_longS}t, po{_ligatureSt}, and po{_ligatureLongSt}";

        regex
            .Matches(subject)
            .Select(static m => m.Value)
            .Should()
            .Equal("POST", "Post", "post", $"po{_longS}t", $"po{_ligatureSt}", $"po{_ligatureLongSt}");
    }

    // Hg issue 192: Named lists reverse matching doesn't work with IGNORECASE and V1.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#220")]
    public void Reverse_case_insensitive_named_list_matches_under_V0()
    {
        var regex = Upstream.Compile(
            @"(?irV0)\L<kw>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["kw"] = ["1"] }
        );
        Match m = regex.MatchAtStart("21");

        m.Index.Should().Be(1);
        m.Length.Should().Be(1);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#221")]
    public void Reverse_case_insensitive_named_list_matches_under_V1()
    {
        var regex = Upstream.Compile(
            @"(?irV1)\L<kw>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["kw"] = ["1"] }
        );
        Match m = regex.MatchAtStart("21");

        m.Index.Should().Be(1);
        m.Length.Should().Be(1);
    }

    // Hg issue 205: Named list and (?ri) flags.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#246")]
    public void Case_insensitive_named_list_matches_the_longer_overlapping_entry()
    {
        var regex = Upstream.Compile(
            @"(?i)\L<aa>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["121", "22"] }
        );

        regex.Match("22").Success.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#247")]
    public void Reverse_case_insensitive_named_list_matches_the_longer_overlapping_entry()
    {
        var regex = Upstream.Compile(
            @"(?ri)\L<aa>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["121", "22"] }
        );

        regex.Match("22").Success.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#248")]
    public void Full_case_folding_named_list_matches_the_longer_overlapping_entry()
    {
        var regex = Upstream.Compile(
            @"(?fi)\L<aa>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["121", "22"] }
        );

        regex.Match("22").Success.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#249")]
    public void Reverse_full_case_folding_named_list_matches_the_longer_overlapping_entry()
    {
        var regex = Upstream.Compile(
            @"(?fri)\L<aa>",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["121", "22"] }
        );

        regex.Match("22").Success.Should().BeTrue();
    }

    // Hg issue 208: Named list, (?ri) flags, Backreference.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#250")]
    public void Reverse_match_with_a_named_list_inside_a_lookbehind_backreference_finds_the_whole_span()
    {
        var regex = Upstream.Compile(
            @"(?r)\1dog..(?<=(\L<aa>))$",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["bcb", "cc"] }
        );
        Match m = regex.Match("ccdogcc");

        m.Index.Should().Be(0);
        m.Length.Should().Be(7);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#251")]
    public void Reverse_case_insensitive_match_with_a_named_list_inside_a_lookbehind_backreference_finds_the_whole_span()
    {
        var regex = Upstream.Compile(
            @"(?ir)\1dog..(?<=(\L<aa>))$",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["aa"] = ["bcb", "cc"] }
        );
        Match m = regex.Match("ccdogcc");

        m.Index.Should().Be(0);
        m.Length.Should().Be(7);
    }

    // Git issue 525: segfault when fuzzy matching empty list.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#446")]
    public void Fuzzy_matching_an_empty_named_list_matches_the_empty_string_without_crashing()
    {
        var regex = Upstream.Compile(
            @"(\L<foo>){e<=5}",
            FuzzyRegexOptions.None,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["foo"] = [] }
        );
        Match m = regex.MatchAtStart("blah");

        m.Index.Should().Be(0);
        m.Length.Should().Be(0);
    }
}
