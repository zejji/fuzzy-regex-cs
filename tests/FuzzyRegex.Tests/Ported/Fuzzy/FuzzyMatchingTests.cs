using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// <para>
/// The assertions that exercise fuzzy matching without singling out one error kind, one cost
/// equation or one of the <c>(?b)</c>/<c>(?e)</c> flags. Several also need a second capability -
/// named lists, right-to-left search or backreferences - which the skip reason names; each of
/// those capabilities already has its own tests on the board, so nothing is hidden by tagging
/// these as fuzzy (see DECISIONS.md, 2026-08-29).
/// </para>
/// <para>
/// Every span, group and list here was read back from the local Python oracle on 2026-08-30.
/// </para>
/// </remarks>
public sealed class FuzzyMatchingTests
{
    // Some tests borrowed from the TRE library's tests.
    [Test]
    [Arguments("(?:\\bznacnda){e<=2}")]
    [Arguments("(?:\\bnacnda){e<=2}")]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#10-11")]
    public void An_error_budget_finds_a_misspelled_word(string pattern) =>
        FuzzyRegex.Match(FuzzyTestData.Molasses, pattern).Value.Should().Be("anaconda");

    // No cost limit at all: {e} permits any number of errors.
    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#19")]
    public void An_unbounded_error_budget_matches_at_the_leftmost_position() =>
        AssertSpans("xirefoabralfobarxie", "(foobar){e}", (0, 6), (0, 6));

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#22")]
    public void At_most_two_errors_matches_a_subject_needing_two() =>
        AssertSpans("xirefoabrzlfd", "(foobar){e<=2}", (4, 9), (4, 9));

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#23")]
    public void At_most_two_errors_rejects_a_subject_needing_three() =>
        FuzzyRegex.Match("xirefoabzlfd", "(foobar){e<=2}").Success.Should().BeFalse();

    // Find the best whole-word match for "foobar". Without (?b) the leftmost one wins; the
    // BESTMATCH sibling of #26 is #27.
    [Test]
    [Arguments("zfoobarz", 0, 8)]
    [Arguments("boing zfoobarz goobar woop", 0, 6)]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#25-26")]
    public void Word_boundaries_bound_a_fuzzy_match(string subject, int start, int end) =>
        AssertSpans(subject, "\\b(foobar){e}\\b", (start, end), (start, end));

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#28")]
    public void An_exact_subject_needs_none_of_its_error_budget() =>
        AssertSpans("foobar", "^(foobar){e<=1}$", (0, 6), (0, 6));

    // Two errors, so a budget of one is not enough. The one-error subjects are in
    // FuzzyErrorKindTests, split by which kind of error each needs.
    [Test]
    [Arguments("xfoobarx")]
    [Arguments("foobarxx")]
    [Arguments("xxfoobar")]
    [Arguments("xfoxbar")]
    [Arguments("foxbarx")]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#38-42")]
    public void A_subject_needing_two_errors_is_rejected_by_a_budget_of_one(string subject) =>
        FuzzyRegex.Match(subject, "^(foobar){e<=1}$").Success.Should().BeFalse();

    // Partially fuzzy: only the inner group carries an error budget.
    [Test]
    [Arguments("foobarzap", 0, 9, 3, 6)]
    [Arguments("foobrzap", 0, 8, 3, 5)]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#45,47")]
    public void Only_the_group_carrying_the_constraint_may_be_inexact(
        string subject,
        int start,
        int end,
        int groupStart,
        int groupEnd
    ) => AssertSpans(subject, "foo(bar){e<=1}zap", (start, end), (groupStart, groupEnd));

    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#46")]
    public void An_error_outside_the_fuzzy_group_is_not_forgiven() =>
        FuzzyRegex.Match("fobarzap", "foo(bar){e<=1}zap").Success.Should().BeFalse();

    // The ENHANCEMATCH sibling of #48 is #49, which pulls group 1 onto (93, 100).
    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#48")]
    public void A_greedy_prefix_leaves_the_fuzzy_group_empty_at_the_end() =>
        AssertSpans(FuzzyTestData.Hosts, "(?s)^.*(dot.org){e}.*$", (0, 120), (120, 120));

    // Without (?s) the leading .* stops at the last newline, so the whole match is one shorter.
    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#50")]
    public void Without_dotall_the_fuzzy_group_spans_most_of_the_subject() =>
        AssertSpans(FuzzyTestData.Hosts, "^.*(dot.org){e}.*$", (0, 119), (24, 101));

    // Upstream's comment: "Behaviour is unexpected, but arguably not wrong. It first finds the
    // best match, then the best in what follows, etc." The leading and trailing spaces in the
    // expected values are upstream's, and were confirmed against the oracle.
    [Test]
    [Skip("needs:fuzzy-matching - needs fuzzy matching and named lists; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#51")]
    public void A_fuzzy_named_list_finds_each_word_in_turn() =>
        AssertMatches(" book cot dog desk ", "\\b\\L<words>{e<=1}\\b", "cot", "dog");

    [Test]
    [Skip("needs:fuzzy-matching - needs fuzzy matching and named lists; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#52")]
    public void A_fuzzy_named_list_can_swallow_a_leading_space() =>
        AssertMatches(" book dog cot desk ", "\\b\\L<words>{e<=1}\\b", " dog", "cot");

    [Test]
    [Skip(
        "needs:fuzzy-matching - needs fuzzy matching, named lists and right-to-left search; the engine has none of them yet"
    )]
    [Property("Upstream", "RegexTests.test_fuzzy#54")]
    public void A_fuzzy_named_list_searching_backwards_can_swallow_a_trailing_space() =>
        AssertMatches(" book cot dog desk ", "(?r)\\b\\L<words>{e<=1}\\b", "dog ", "cot");

    [Test]
    [Skip(
        "needs:fuzzy-matching - needs fuzzy matching, named lists and right-to-left search; the engine has none of them yet"
    )]
    [Property("Upstream", "RegexTests.test_fuzzy#56")]
    public void A_fuzzy_named_list_searching_backwards_reports_matches_leftmost_first() =>
        AssertMatches(" book dog cot desk ", "(?r)\\b\\L<words>{e<=1}\\b", "cot", "dog");

    [Test]
    [Skip("needs:fuzzy-matching - needs fuzzy matching and backreferences; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#63")]
    public void A_backreference_can_carry_an_error_budget()
    {
        Match m = FuzzyRegex.Match("foo fou", "(\\w+) (\\1{e<=1})");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("foo");
        m.Groups[2].Value.Should().Be("fou");
    }

    [Test]
    [Skip(
        "needs:fuzzy-matching - needs fuzzy matching, backreferences and right-to-left search; the engine has none of them yet"
    )]
    [Property("Upstream", "RegexTests.test_fuzzy#64")]
    public void A_forward_backreference_can_carry_an_error_budget_searching_backwards()
    {
        Match m = FuzzyRegex.Match("foo fou", "(?r)(\\2{e<=1}) (\\w+)");

        m.Success.Should().BeTrue();
        m.Groups[1].Value.Should().Be("foo");
        m.Groups[2].Value.Should().Be("fou");
    }

    // An unbounded budget lets a pattern that shares no character with the subject match all of
    // it, and then match empty at the end.
    [Test]
    [Arguments("(?:(?:QR)+){e}", "abcde")]
    [Arguments("(?:Q+){e}", "abc")]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#66-67")]
    public void An_unbounded_budget_matches_the_whole_subject_and_then_empty(string pattern, string subject) =>
        FuzzyRegex.Matches(subject, pattern).Select(static m => m.Value).Should().Equal(subject, "");

    // Fuzzy constraints are ignored when a branch is checked for a common prefix or suffix, so
    // the second branch's larger budget is the one that decides this.
    [Test]
    [Skip("needs:fuzzy-matching - the engine has no fuzzy matching yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#79")]
    public void A_branch_with_a_wider_budget_still_matches() =>
        FuzzyRegex.MatchAtStart("FO", "(?:fo){e<=1}|(?:fo){e<=2}").Success.Should().BeTrue();

    private static void AssertSpans(
        string subject,
        string pattern,
        (int Start, int End) whole,
        (int Start, int End) group1
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be(whole);
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be(group1);
    }

    private static void AssertMatches(string subject, string pattern, params string[] expected) =>
        FuzzyRegex
            .Matches(subject, pattern, FuzzyRegexOptions.None, FuzzyTestData.Words)
            .Select(static m => m.Value)
            .Should()
            .Equal(expected);
}
