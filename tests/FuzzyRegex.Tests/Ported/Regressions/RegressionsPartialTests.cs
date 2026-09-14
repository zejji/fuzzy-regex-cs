using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about partial matching (<c>partial=True</c>).
/// </summary>
public sealed class RegressionsPartialTests
{
    // Hg issue 141: crash on a certain partial match.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#148-149")]
    public void Fullmatch_reports_the_span_and_partial_flag_when_a_repeated_group_leaves_the_subject_short()
    {
        Match m = Upstream.Compile("(a)*abc").FullMatch("ab", partial: true);

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
        m.PartialMatch.Should().BeTrue();
    }

    // Hg issue 143: partial matches have incorrect span if prefix is '.' wildcard.
    [Test]
    [Arguments("OXRG", 3, 5)]
    [Arguments(".XRG", 3, 5)]
    [Arguments(".{1,3}XRG", 1, 5)]
    [Property("Upstream", "RegexTests.test_hg_bugs#150-152")]
    public void Search_reports_the_correct_span_for_a_partial_match_behind_a_wildcard_prefix(
        string pattern,
        int start,
        int end
    )
    {
        Match m = Upstream.Compile(pattern).Match("OOGOX", partial: true);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }

    // Hg issue 203: partial matching bug.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#239")]
    public void Search_reports_a_zero_length_partial_match_at_the_end_of_the_subject()
    {
        Match m = Upstream
            .Compile(@"\d\d\d-\d\d-\d\d\d\d")
            .Match("My SSN is 999-89-76, but don't tell.", partial: true);

        (m.Index, m.Index + m.Length).Should().Be((36, 36));
    }

    // Hg issue 276: partial matches yield incorrect matches and bounds.
    [Test]
    [Arguments("[a-z]+ [a-z]*?:")]
    [Arguments("(?r):[a-z]*? [a-z]+")]
    [Property("Upstream", "RegexTests.test_hg_bugs#310-311")]
    public void Search_reports_the_whole_subject_as_a_partial_match_forwards_and_backwards(string pattern)
    {
        Match m = Upstream.Compile(pattern).Match("foo bar", partial: true);

        (m.Index, m.Index + m.Length).Should().Be((0, 7));
    }

    // Hg issue 299: partial gives misleading results with an "open ended" regexp. A repetition
    // that has already reached a point where it could legally stop is never partial, no matter
    // how it is quantified or which direction it reads in.
    [Test]
    [Arguments("(?:ab)*", "ab")]
    [Arguments("(?:ab)*", "abab")]
    [Arguments("(?:ab)*?", "")]
    [Arguments("(?:ab)*+", "ab")]
    [Arguments("(?:ab)*+", "abab")]
    [Arguments("(?:ab)+", "ab")]
    [Arguments("(?:ab)+", "abab")]
    [Arguments("(?:ab)+?", "ab")]
    [Arguments("(?:ab)++", "ab")]
    [Arguments("(?:ab)++", "abab")]
    [Arguments("(?r)(?:ab)*", "ab")]
    [Arguments("(?r)(?:ab)*", "abab")]
    [Arguments("(?r)(?:ab)*?", "")]
    [Arguments("(?r)(?:ab)*+", "ab")]
    [Arguments("(?r)(?:ab)*+", "abab")]
    [Arguments("(?r)(?:ab)+", "ab")]
    [Arguments("(?r)(?:ab)+", "abab")]
    [Arguments("(?r)(?:ab)+?", "ab")]
    [Arguments("(?r)(?:ab)++", "ab")]
    [Arguments("(?r)(?:ab)++", "abab")]
    [Property("Upstream", "RegexTests.test_hg_bugs#330-349")]
    public void Match_after_a_complete_repetition_of_ab_is_never_reported_as_partial(string pattern, string subject) =>
        Upstream.Compile(pattern).MatchAtStart(subject, partial: true).PartialMatch.Should().BeFalse();

    // Same Hg issue 299 report, over a single-character repetition: only a one-or-more quantifier,
    // greedy or not, is partial when no "a" is available to satisfy it.
    [Test]
    [Arguments("a*", "", false)]
    [Arguments("a*?", "", false)]
    [Arguments("a*+", "", false)]
    [Arguments("a+", "", true)]
    [Arguments("a+?", "", true)]
    [Arguments("a++", "", true)]
    [Arguments("a+", "a", false)]
    [Arguments("a+?", "a", false)]
    [Arguments("a++", "a", false)]
    [Arguments("(?r)a*", "", false)]
    [Arguments("(?r)a*?", "", false)]
    [Arguments("(?r)a*+", "", false)]
    [Arguments("(?r)a+", "", true)]
    [Arguments("(?r)a+?", "", true)]
    [Arguments("(?r)a++", "", true)]
    [Arguments("(?r)a+", "a", false)]
    [Arguments("(?r)a+?", "a", false)]
    [Arguments("(?r)a++", "a", false)]
    [Property("Upstream", "RegexTests.test_hg_bugs#350-367")]
    public void Match_of_a_is_partial_only_when_at_least_one_more_a_is_still_required(
        string pattern,
        string subject,
        bool expectedPartial
    ) => Upstream.Compile(pattern).MatchAtStart(subject, partial: true).PartialMatch.Should().Be(expectedPartial);

    // Same Hg issue 299 report, over a compound repetition of whitespace/word/quote groups.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#368")]
    public void Match_of_a_repeated_word_group_on_a_full_word_is_not_partial() =>
        Upstream.Compile(@"(?:\s*\w+'*)+").MatchAtStart("whatever", partial: true).PartialMatch.Should().BeFalse();

    // Git issue 539: bug: partial matching fails on a simple example. A negated class followed by
    // a literal tail only partially matches when the subject is a genuine prefix of the tail; a
    // subject that diverges from the tail (like "b/ccb" against "...b/ccc") does not match at all,
    // not even partially. The case-insensitive pattern behaves the same way.
    [Test]
    [Arguments("[^/]*b/ccc", "b/ccc", 0, 5)]
    [Arguments("[^/]*b/ccc", "b/ccb", null, null)]
    [Arguments("[^/]*b/ccc", "b/cc", 0, 4)]
    [Arguments("[^/]*b/xyz", "b/xy", 0, 4)]
    [Arguments("[^/]*b/xyz", "b/yz", null, null)]
    [Arguments("(?i)[^/]*b/ccc", "b/ccc", 0, 5)]
    [Arguments("(?i)[^/]*b/ccc", "b/ccb", null, null)]
    [Arguments("(?i)[^/]*b/ccc", "b/cc", 0, 4)]
    [Arguments("(?i)[^/]*b/xyz", "b/xy", 0, 4)]
    [Arguments("(?i)[^/]*b/xyz", "b/yz", null, null)]
    [Property("Upstream", "RegexTests.test_hg_bugs#449-458")]
    public void Match_of_a_negated_class_prefix_before_a_literal_tail_only_partially_matches_a_true_prefix(
        string pattern,
        string subject,
        int? expectedIndex,
        int? expectedLength
    )
    {
        Match m = Upstream.Compile(pattern).MatchAtStart(subject, partial: true);

        if (expectedIndex is null)
        {
            m.Success.Should().BeFalse();
        }
        else
        {
            (m.Index, m.Length).Should().Be((expectedIndex.Value, expectedLength!.Value));
        }
    }

    // Git issue 546: partial match not working in some instances with non-greedy capture. Every
    // prefix of the tag, up to and including subject that runs past the closing tag, matches.
    [Test]
    [Arguments("<")]
    [Arguments("<thinking")]
    [Arguments("<thinking>")]
    [Arguments("<thinking>x")]
    [Arguments("<thinking>xyz abc")]
    [Arguments("<thinking>xyz abc foo")]
    [Arguments("<thinking>xyz abc foo ")]
    [Arguments("<thinking>xyz abc foo bar")]
    [Property("Upstream", "RegexTests.test_hg_bugs#459-466")]
    public void Match_of_a_non_greedy_thinking_tag_matches_at_every_prefix_length(string subject) =>
        Upstream.Compile("<thinking>.*?</thinking>").MatchAtStart(subject, partial: true).Success.Should().BeTrue();
}
