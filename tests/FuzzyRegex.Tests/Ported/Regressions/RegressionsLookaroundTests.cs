using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about lookaround.
/// </summary>
public sealed class RegressionsLookaroundTests
{
    // Hg issue 34.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#12")]
    public void Lookahead_that_captures_a_group_reports_that_capture_in_the_overall_groups()
    {
        Match m = Upstream.Match("abde", "^(?=ab(de))(abd)(e)");

        m.Groups.Values.Skip(1).Select(static g => g.Success ? g.Value : null).Should().Equal("de", "abd", "e");
    }

    // Hg issue 157: regression: segfault on complex lookaround.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#208")]
    public void Nested_possessive_lookaheads_with_unicode_properties_do_not_crash()
    {
        Match m = Upstream.MatchAtStart(
            "AAaa11!!",
            @"(?V1w)(?=(?=[^A-Z]*+[A-Z])(?=[^a-z]*+[a-z]))(?=\D*+\d)(?=\p{Alphanumeric}*+\P{Alphanumeric})\A(?s:.){8,255}+\Z"
        );

        m.Value.Should().Be("AAaa11!!");
    }

    // Hg issue 320: Abnormal performance.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#374")]
    public void Lookahead_followed_by_the_same_literal_matches_without_hanging() =>
        Upstream.Match("a", "(?=a)a").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#375")]
    public void Negative_lookahead_that_does_not_match_lets_the_literal_through() =>
        Upstream.Match("a", "(?!b)a").Success.Should().BeTrue();

    // Hg issue 71: non-greedy quantifier in lookbehind.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#48")]
    public void Lookbehind_with_a_greedy_one_or_more_finds_both_words() =>
        Upstream.Matches(":9 abc :10 def", @"(?<=:\S+ )\w+").Select(static m => m.Value).Should().Equal("abc", "def");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#49")]
    public void Lookbehind_with_a_greedy_zero_or_more_finds_both_words() =>
        Upstream.Matches(":9 abc :10 def", @"(?<=:\S* )\w+").Select(static m => m.Value).Should().Equal("abc", "def");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#50")]
    public void Lookbehind_with_a_lazy_one_or_more_finds_both_words() =>
        Upstream.Matches(":9 abc :10 def", @"(?<=:\S+? )\w+").Select(static m => m.Value).Should().Equal("abc", "def");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#51")]
    public void Lookbehind_with_a_lazy_zero_or_more_finds_both_words() =>
        Upstream.Matches(":9 abc :10 def", @"(?<=:\S*? )\w+").Select(static m => m.Value).Should().Equal("abc", "def");

    // Hg Issue 216: Invalid match when using negative lookbehind and pipe.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#259")]
    public void Positive_lookbehind_after_the_literal_it_repeats_still_matches() =>
        Upstream.MatchAtStart("foo", "foo(?<=foo)").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#260")]
    public void Negative_lookbehind_after_the_literal_it_repeats_fails_to_match() =>
        Upstream.MatchAtStart("foo", "foo(?<!foo)").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#261")]
    public void Positive_lookbehind_with_an_alternation_still_matches() =>
        Upstream.MatchAtStart("foo", "foo(?<=foo|x)").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#262")]
    public void Negative_lookbehind_with_an_alternation_fails_to_match() =>
        Upstream.MatchAtStart("foo", "foo(?<!foo|x)").Success.Should().BeFalse();
}
