using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about basic matching.
/// </summary>
public sealed class RegressionsBasicMatchingTests
{
    // Verbatim from upstream: a relative-time pattern compiled once and searched several times
    // (upstream line 4259), flags=regex.I | regex.V0.
    private const string _relativeTimePattern =
        @"(?<=(?:\A|\W|_))(\d+ decades? ago|\d+ minutes ago|\d+ seconds ago|in \d+ decades?|\d+ months ago|in \d+ minutes|\d+ minute ago|in \d+ seconds|\d+ second ago|\d+ years ago|in \d+ months|\d+ month ago|\d+ weeks ago|\d+ hours ago|in \d+ minute|in \d+ second|in \d+ years|\d+ year ago|in \d+ month|in \d+ weeks|\d+ week ago|\d+ days ago|in \d+ hours|\d+ hour ago|in \d+ year|in \d+ week|in \d+ days|\d+ day ago|in \d+ hour|\d+ min ago|\d+ sec ago|\d+ yr ago|\d+ mo ago|\d+ wk ago|in \d+ day|\d+ hr ago|in \d+ min|in \d+ sec|in \d+ yr|in \d+ mo|in \d+ wk|in \d+ hr)(?=(?:\Z|\W|_))";

    // Hg issue 144: Latest version problem with matching 'R|R'.
    [Test]
    [Skip("needs:alternation - alternation is not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#153")]
    public void Alternation_of_two_identical_branches_still_matches()
    {
        Match m = FuzzyRegex.MatchAtStart("R", "R|R");

        m.Index.Should().Be(0);
        m.Length.Should().Be(1);
    }

    // Hg issue 88: regex.match() hangs.
    [Test]
    // Retagged in S16: the spine matches literals and '.', but '.*' is a repeat.
    [Skip("needs:quantifiers - '.*a.*ba.*aa' is three greedy repeats")]
    [Property("Upstream", "RegexTests.test_hg_bugs#59")]
    public void Pattern_that_used_to_hang_fails_to_match_without_hanging() =>
        FuzzyRegex.MatchAtStart("ababba", @".*a.*ba.*aa").Success.Should().BeFalse();

    // Hg issue 139: Regular expression with multiple wildcards where first should match empty
    // string does not always work.
    [Test]
    // Retagged in S16: two capture groups, each holding a repeated negated set. The assertion
    // reads Groups[1] and Groups[2], so S18 is the last thing it waits on.
    [Skip("needs:groups - '([^L]*)([^R]*R)' needs sets (S17), repeats (S19) and capture groups")]
    [Property("Upstream", "RegexTests.test_hg_bugs#143")]
    public void First_wildcard_group_is_allowed_to_match_empty_so_the_second_can_reach_the_anchor()
    {
        Match m = FuzzyRegex.Match("LtR", "([^L]*)([^R]*R)");

        m.Groups.Skip(1).Select(g => g.Success ? g.Value : null).Should().Equal("", "LtR");
    }

    [Test]
    // Retagged in S16: '[ ]*' is a repeated set.
    [Skip("needs:quantifiers - '[ ]* Name[ ]*\\* ' repeats a set twice")]
    [Property("Upstream", "RegexTests.test_hg_bugs#411")]
    public void Trailing_space_in_the_pattern_that_is_absent_from_the_subject_fails_to_match() =>
        new FuzzyRegex(@"[ ]* Name[ ]*\* ").Match("  Name *").Success.Should().BeFalse();

    [Test]
    // Retagged in S16: the pattern is an alternation, which the matcher has no BRANCH case for.
    [Skip("needs:alternation - 'a|\\.*pb\\.py' is a branch, and its second arm is a repeat too")]
    [Property("Upstream", "RegexTests.test_hg_bugs#412")]
    public void Alternation_with_a_literal_dot_branch_does_not_falsely_match() =>
        new FuzzyRegex(@"a|\.*pb\.py").Match(".geojs").Success.Should().BeFalse();

    [Test]
    [Arguments("1 month ago", "1 month ago")]
    [Arguments("9 hours 1 minute ago", "1 minute ago")]
    [Arguments("10 months 1 hour ago", "1 hour ago")]
    [Arguments("1 month 10 hours ago", "10 hours ago")]
    // Retagged in S16: the pattern opens with a lookbehind and closes with a lookahead.
    [Skip("needs:lookbehind - the pattern is '(?<=...)(alternation)(?=...)'")]
    [Property("Upstream", "RegexTests.test_hg_bugs#413-416")]
    public void Relative_time_pattern_finds_the_rightmost_recognised_phrase(string subject, string expected) =>
        new FuzzyRegex(_relativeTimePattern, FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version0)
            .Match(subject)
            .Value.Should()
            .Be(expected);

    // Hg issue 327: .fullmatch() causes MemoryError.
    [Test]
    // Retagged in S16: FullMatch itself works now; '((\d)*?)*?' is nested lazy repeats.
    [Skip("needs:quantifiers - '((\\d)*?)*?' is two nested lazy repeats over a set")]
    [Property("Upstream", "RegexTests.test_hg_bugs#376")]
    public void Nested_lazy_star_groups_fully_match_without_exhausting_memory()
    {
        Match m = FuzzyRegex.FullMatch("123", @"((\d)*?)*?");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }
}
