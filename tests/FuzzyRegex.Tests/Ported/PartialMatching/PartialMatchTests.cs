using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.PartialMatching;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_partial</c> (lines 3031-3083).
/// </summary>
/// <remarks>
/// <para>
/// A partial match is one that ran out of subject before it ran out of pattern: it could still
/// succeed if more text arrived. Upstream asks for one with <c>partial=True</c> and reports it
/// through <c>Match.partial</c>; here that is the <c>partial</c> argument of
/// <c>MatchAtStart</c> and the <c>PartialMatch</c> property. A complete match is still returned
/// when one exists, with <c>PartialMatch</c> false, so the two assertions upstream makes per case
/// - the flag and the span - stay together in one test.
/// </para>
/// <para>
/// Assertions <c>#11-19</c> also need named lists; that capability has its own tests in
/// <c>Ported/NamedLists/</c>, so nothing is hidden by tagging these <c>partial</c>
/// (DECISIONS.md, 2026-08-29). Every subject is BMP-only - U+FB06 (ﬆ) is a single UTF-16 code
/// unit - so upstream's codepoint spans are also the UTF-16 spans. Values read back from the
/// local Python oracle on 2026-08-30.
/// </para>
/// </remarks>
public sealed class PartialMatchTests
{
    // U+FB06 is the "st" ligature. Written as a code-unit cast so it cannot be confused with the
    // two-letter sequence it folds to.
    private static readonly string _postLigature = "po" + (char)0xFB06; // poﬆ

    private static readonly Dictionary<string, IReadOnlyCollection<string>> _post = new(StringComparer.Ordinal)
    {
        ["words"] = ["post"],
    };

    private static readonly Dictionary<string, IReadOnlyCollection<string>> _postLigatureList = new(
        StringComparer.Ordinal
    )
    {
        ["words"] = ["po" + (char)0xFB06],
    };

    private static readonly Dictionary<string, IReadOnlyCollection<string>> _pos = new(StringComparer.Ordinal)
    {
        ["words"] = ["POS"],
    };

    [Test]
    [Arguments("ab", "a", 1)]
    [Arguments("cats", "cat", 3)]
    [Arguments("abc\\w{3}", "abcde", 5)]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#1-4,8-9")]
    public void A_subject_that_runs_out_early_is_a_partial_match(string pattern, string subject, int end)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
    }

    [Test]
    [Arguments("abc\\w{3}", "abcdef", 6)]
    [Arguments("\\d{4}$", "1234", 4)]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#6-7,10")]
    public void A_complete_match_is_still_reported_as_complete(string pattern, string subject, int end)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeFalse();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
    }

    // "catch" diverges from "cats" at the fourth character, so there is nothing more text could
    // fix - not a partial match, no match at all.
    [Test]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#5")]
    public void A_subject_that_diverges_is_not_a_partial_match() =>
        new FuzzyRegex("cats").MatchAtStart("catch", partial: true).Success.Should().BeFalse();

    [Test]
    [Skip("needs:partial - needs partial matching and named lists; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_partial#11-12")]
    public void A_named_list_entry_matched_in_full_is_a_complete_match() =>
        AssertPartial("\\L<words>", "post", _post, partial: false, end: 4);

    [Test]
    [Skip("needs:partial - needs partial matching and named lists; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_partial#13-14")]
    public void A_prefix_of_a_named_list_entry_is_a_partial_match() =>
        AssertPartial("\\L<words>", "pos", _post, partial: true, end: 3);

    // (?f) full case-folding: "poﬆ" (three code units) folds to "post", so all four characters
    // of "POST" are consumed by the three-code-unit entry.
    [Test]
    [Skip("needs:partial - needs partial matching, named lists and full case-folding; the engine has none of them yet")]
    [Property("Upstream", "RegexTests.test_partial#15-16")]
    public void Full_case_folding_completes_a_named_list_match_of_a_different_length() =>
        AssertPartial("(?fi)\\L<words>", "POST", _postLigatureList, partial: false, end: 4);

    [Test]
    [Skip("needs:partial - needs partial matching, named lists and full case-folding; the engine has none of them yet")]
    [Property("Upstream", "RegexTests.test_partial#17-18")]
    public void Full_case_folding_still_reports_a_prefix_as_partial() =>
        AssertPartial("(?fi)\\L<words>", "POS", _postLigatureList, partial: true, end: 3);

    // The other direction does not match: "POS" is not a prefix of the folded "poﬆ" once the
    // ligature has been expanded, so there is no partial match either.
    [Test]
    [Skip("needs:partial - needs partial matching, named lists and full case-folding; the engine has none of them yet")]
    [Property("Upstream", "RegexTests.test_partial#19")]
    public void A_folded_ligature_subject_does_not_match_a_shorter_entry() =>
        new FuzzyRegex("(?fi)\\L<words>", FuzzyRegexOptions.None, _pos)
            .MatchAtStart(_postLigature, partial: true)
            .Success.Should()
            .BeFalse();

    // [a-z]*4R$ - the anchor means a complete match needs the whole "4R" and the end of subject.
    [Test]
    [Arguments("a", 1)]
    [Arguments("ab", 2)]
    [Arguments("ab4", 3)]
    [Arguments("a4", 2)]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#20-23")]
    public void An_anchored_pattern_reports_every_viable_prefix_as_partial(string subject, int end)
    {
        Match m = new FuzzyRegex("[a-z]*4R$").MatchAtStart(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
    }

    [Test]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#24")]
    public void An_anchored_pattern_completes_at_the_end_of_the_subject()
    {
        Match m = new FuzzyRegex("[a-z]*4R$").MatchAtStart("a4R", partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeFalse();
        (m.Index, m.Index + m.Length).Should().Be((0, 3));
    }

    [Test]
    [Arguments("4a")]
    [Arguments("a44")]
    [Skip("needs:partial - the engine cannot report a partial match yet")]
    [Property("Upstream", "RegexTests.test_partial#25-26")]
    public void A_subject_that_can_never_complete_is_not_a_partial_match(string subject) =>
        new FuzzyRegex("[a-z]*4R$").MatchAtStart(subject, partial: true).Success.Should().BeFalse();

    private static void AssertPartial(
        string pattern,
        string subject,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> namedLists,
        bool partial,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, namedLists).MatchAtStart(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().Be(partial);
        (m.Index, m.Index + m.Length).Should().Be((0, end));
    }
}
