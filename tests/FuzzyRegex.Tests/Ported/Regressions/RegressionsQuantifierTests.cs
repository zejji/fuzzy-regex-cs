using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c> (lines 3084-4410),
/// the assertions about quantifiers.
/// </summary>
public sealed class RegressionsQuantifierTests
{
    private const string _quotedBugSubject = "\"Erm....yes. T..T...Thank you for that.\"";

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#15")]
    public void Group_repeated_zero_to_zero_times_matches_empty_and_leaves_the_group_unset()
    {
        // Hg issue 37: regex.search("^(a){0,0}", "abc").group(0,1) returns ('a', 'a') instead of
        // ('', None).
        Match m = FuzzyRegex.Match("abc", "^(a){0,0}");

        m.Value.Should().Be("");
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    [Arguments("a", 1, 1)]
    [Arguments("aa", 2, 2)]
    [Arguments("aaa", 3, 3)]
    [Property("Upstream", "RegexTests.test_hg_bugs#19-21")]
    public void Nested_star_group_captures_an_empty_final_iteration_at_the_end_of_the_run(
        string subject,
        int expectedStart,
        int expectedEnd
    )
    {
        // Hg issue 42: regex.search("(a*)*", "a", flags=regex.V1).span(1) returns (0, 1) instead
        // of (1, 1).
        Group g = FuzzyRegex.Match(subject, "(a*)*").Groups[1];

        (g.Index, g.Index + g.Length).Should().Be((expectedStart, expectedEnd));
    }

    [Test]
    [Skip("needs:lookaround - '(?=abc){3}abc' repeats a lookahead, and the matcher has no LOOKAROUND yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#23")]
    public void A_repeated_lookahead_group_does_not_throw_nothing_to_repeat()
    {
        // Hg issue 44: regex.compile("(?=abc){3}abc") causes "_regex_core.error: nothing to
        // repeat".
        Match m = FuzzyRegex.Match("abcabcabc", "(?=abc){3}abc");

        (m.Index, m.Index + m.Length).Should().Be((0, 3));
    }

    [Test]
    [Arguments("a", 0, 1)]
    [Arguments("aa", 0, 2)]
    [Property("Upstream", "RegexTests.test_hg_bugs#24-25")]
    public void A_repeated_group_containing_an_empty_group_does_not_throw_nothing_to_repeat(
        string subject,
        int expectedStart,
        int expectedEnd
    )
    {
        // Hg issue 45: regex.compile("^(?:a(?:(?:))+)+") causes "_regex_core.error: nothing to
        // repeat".
        Match m = FuzzyRegex.Match(subject, "^(?:a(?:(?:))+)+");

        (m.Index, m.Index + m.Length).Should().Be((expectedStart, expectedEnd));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#39")]
    public void A_repeated_optional_empty_alternative_matches_without_running_out_of_memory()
    {
        // Hg issue 53: regex.search("(a|)+", "a") causes MemoryError.
        Match m = FuzzyRegex.Match("a", "(a|)+");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#40")]
    public void A_repeated_optional_empty_alternative_before_a_digit_fails_without_running_out_of_memory() =>
        // Hg issue 54: regex.search("(a|)*\\d", "a"*80) causes MemoryError.
        FuzzyRegex.Match(new string('a', 80), @"(a|)*\d").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#41")]
    public void A_repeated_pair_of_optional_characters_fails_quickly_instead_of_taking_a_long_time() =>
        // Hg issue 55: regex.search("^(?:a?b?)*$", "ac") takes a very long time.
        FuzzyRegex.Match("ac", "^(?:a?b?)*$").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#44")]
    public void A_group_repeated_two_or_more_times_matches_two_repetitions_correctly() =>
        // Hg issue 60: regex.search("(q1|.)*(q2|.)*(x(a|bc)*y){2,}", "xayxay") returns None
        // incorrectly.
        FuzzyRegex.Match("xayxay", "(q1|.)*(q2|.)*(x(a|bc)*y){2,}").Value.Should().Be("xayxay");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#57")]
    public void A_plus_quantifier_before_a_literal_slash_only_matches_the_line_containing_it() =>
        // Hg issue 83: slash handling in presence of a quantifier.
        FuzzyRegex.Matches("cA/c\ncAb/c", "c..+/c").Select(m => m.Value).Should().Equal("cAb/c");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#274")]
    public void Group_repeated_one_to_three_times_captures_the_whole_run_for_each_repetition() =>
        // Hg issue 238: Not fully re backward compatible.
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1,3}")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("Erm....", "T...");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#274")]
    public void Group_repeated_one_to_three_times_captures_the_word_for_each_repetition() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1,3}")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("Erm", "T");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#274")]
    public void Group_repeated_one_to_three_times_captures_the_dots_for_each_repetition() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1,3}")
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("....", "...");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#275")]
    public void Group_repeated_exactly_three_times_never_matches_the_subject() =>
        FuzzyRegex.Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){3}").Select(m => m.Value).Should().BeEmpty();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#276")]
    public void Group_repeated_exactly_two_times_captures_the_whole_run() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){2}")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("T...");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#276")]
    public void Group_repeated_exactly_two_times_captures_the_word() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){2}")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("T");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#276")]
    public void Group_repeated_exactly_two_times_captures_the_dots() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){2}")
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("...");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#277")]
    public void Group_repeated_exactly_once_captures_the_whole_run_for_each_match() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1}")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("Erm....", "T..", "T...");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#277")]
    public void Group_repeated_exactly_once_captures_the_word_for_each_match() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1}")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("Erm", "T", "T");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#277")]
    public void Group_repeated_exactly_once_captures_the_dots_for_each_match() =>
        FuzzyRegex
            .Matches(_quotedBugSubject, @"((\w{1,3})(\.{2,10})){1}")
            .Select(m => m.Groups[3].Value)
            .Should()
            .Equal("....", "..", "...");
}
