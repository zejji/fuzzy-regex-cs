using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions about
/// conditional patterns <c>(?(id)yes|no)</c>, including the lookaround-condition form
/// <c>(?(?=...)yes|no)</c>.
/// </summary>
public sealed class RegressionsConditionalTests
{
    // Hg issue 40: regex.search("(\()?[^()]+(?(1)\)|)", "(abcd").group(0) returns "bcd" instead of
    // "abcd".
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#18")]
    public void Conditional_with_an_empty_no_branch_still_matches_the_whole_run() =>
        FuzzyRegex.Match("(abcd", @"(\()?[^()]+(?(1)\)|)").Value.Should().Be("abcd");

    // Hg issue 48: regex.search("(a(?(1)\\1)){4}", "a"*10, flags=regex.V1).group(0,1) returns
    // ('aaaaa', 'a') instead of ('aaaaaaaaaa', 'aaaa').
    [Test]
    [Arguments(1, 0, 1, 0, 1)]
    [Arguments(2, 0, 3, 1, 3)]
    [Arguments(3, 0, 6, 3, 6)]
    [Arguments(4, 0, 10, 6, 10)]
    [Property("Upstream", "RegexTests.test_hg_bugs#28-31")]
    public void Backreference_conditional_inside_a_repeated_group_grows_its_capture_each_time(
        int repeatCount,
        int matchStart,
        int matchEnd,
        int group1Start,
        int group1End
    )
    {
        Match m = FuzzyRegex.Match("aaaaaaaaaa", $@"(?V1)(a(?(1)\1)){{{repeatCount}}}");

        m.Index.Should().Be(matchStart);
        m.Length.Should().Be(matchEnd - matchStart);
        m.Groups[1].Index.Should().Be(group1Start);
        m.Groups[1].Length.Should().Be(group1End - group1Start);
    }

    // Hg issue 73: conditional patterns.
    [Test]
    // Upstream's control for #53: no conditional in this pattern, so it is tagged for the
    // optional group it does need.
    [Property("Upstream", "RegexTests.test_hg_bugs#52")]
    public void Optional_non_capturing_group_still_matches_the_longer_alternative() =>
        FuzzyRegex.Match("female", @"(?:fe)?male").Value.Should().Be("female");

    [Test]
    // S21 delivered the GROUP_EXISTS conditional; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#53")]
    public void Conditional_group_selects_the_matching_gender_specific_branch() =>
        FuzzyRegex
            .Matches("female: her dog; male: his cat. asdsasda", @"(fe)?male: h(?(1)(er)|(is)) (\w+)")
            .Select(m => m.Value)
            .Should()
            .Equal("female: her dog", "male: his cat");

    // Issue 23692.
    [Test]
    [Arguments("(?:()|(?(1)()|z)){2}(?(2)a|z)")]
    [Arguments("(?:()|(?(1)()|z)){0,2}(?(2)a|z)")]
    [Property("Upstream", "RegexTests.test_hg_bugs#112-113")]
    public void Nested_conditional_groups_reset_between_repeats_and_still_capture_empty_groups(string pattern)
    {
        Match m = FuzzyRegex.MatchAtStart("a", pattern);

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("");
        m.Groups[2].Value.Should().Be("");
    }

    // Hg issue 146: Forced-fail (?!) works improperly in conditional.
    [Test]
    // The GROUP_EXISTS half landed in S21, but the forced fail is not an optimised-away FAILURE
    // node: upstream compiles '(.)(?(1)(?!))' to '[30, 1, 1, 1, 2, 0, 20, 32, 1, 35, 0, 1, 20, 20,
    // 1]', where 35 is LOOKAROUND over an empty body (probed against regex 2026.7.19, 2026-08-31).
    [Skip("needs:lookaround - '(?!)' compiles to LOOKAROUND, not FAILURE, so this waits for Phase 4")]
    [Property("Upstream", "RegexTests.test_hg_bugs#154")]
    public void Forced_fail_in_the_conditionals_yes_branch_makes_the_whole_match_fail() =>
        FuzzyRegex.MatchAtStart("xy", @"(.)(?(1)(?!))").Success.Should().BeFalse();

    // Groups cleared after failure.
    [Test]
    // S21 delivered the GROUP_EXISTS conditional; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#155")]
    public void Findall_group_one_is_empty_on_every_match_because_it_never_participates() =>
        FuzzyRegex.Matches("ax1y2z3b", @"(y)?(\d)(?(1)\b\B)").Select(m => m.Groups[1].Value).Should().Equal("", "", "");

    [Test]
    // S21 delivered the GROUP_EXISTS conditional; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#155")]
    public void Findall_group_two_captures_each_digit_in_turn() =>
        FuzzyRegex
            .Matches("ax1y2z3b", @"(y)?(\d)(?(1)\b\B)")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("1", "2", "3");

    [Test]
    // S21 delivered the GROUP_EXISTS conditional; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#156")]
    public void Findall_with_a_possessive_optional_group_also_leaves_group_one_empty() =>
        FuzzyRegex
            .Matches("ax1y2z3b", @"(y)?+(\d)(?(1)\b\B)")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("", "", "");

    [Test]
    // S21 delivered the GROUP_EXISTS conditional; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#156")]
    public void Findall_with_a_possessive_optional_group_still_captures_each_digit() =>
        FuzzyRegex
            .Matches("ax1y2z3b", @"(y)?+(\d)(?(1)\b\B)")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("1", "2", "3");

    // Hg issue 163: allow lookarounds in conditionals.
    [Test]
    // Upstream's control for #213: no conditional in this pattern, so it is tagged for the
    // lookahead it does need.
    [Skip("needs:lookaround - the engine has no lookahead opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#212")]
    public void Non_conditional_lookahead_alternation_matches_the_whole_digit_run()
    {
        Match m = FuzzyRegex.MatchAtStart("123abc", @"(?:(?=\d)\d+\b|\w+)");

        m.Index.Should().Be(0);
        m.Length.Should().Be(6);
    }

    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#213")]
    public void Conditional_on_a_bare_lookahead_with_no_else_branch_does_not_match_without_a_boundary() =>
        FuzzyRegex.MatchAtStart("123abc", @"(?(?=\d)\d+\b|\w+)").Success.Should().BeFalse();

    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#214")]
    public void Conditional_choosing_between_two_lookbehind_predicates_matches_the_word_after_love()
    {
        Match m = FuzzyRegex.Match("I love you", @"(?(?<=love\s)you|(?<=hate\s)her)");

        m.Index.Should().Be(7);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#215")]
    public void Findall_with_a_lookbehind_conditional_finds_both_the_loved_and_hated_targets() =>
        FuzzyRegex
            .Matches("I love you but I don't hate her either", @"(?(?<=love\s)you|(?<=hate\s)her)")
            .Select(m => m.Value)
            .Should()
            .Equal("you", "her");

    // Hg issue 217: Core dump in conditional ahead match and matching \! character.
    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#263")]
    public void Conditional_lookahead_with_a_literal_bang_does_not_match_a_lone_bang() =>
        FuzzyRegex.MatchAtStart("!", @"(?(?=.*\!.*)(?P<true>.*\!\w*\:.*)|(?P<false>.*))").Success.Should().BeFalse();

    // Hg issue 251: Segfault with a particular expression.
    [Test]
    [Arguments(@"(?(?=A)A|B)", "A", 0, 1)]
    [Arguments(@"(?(?=A)A|B)", "B", 0, 1)]
    [Arguments(@"(?(?=A)A|)", "B", 0, 0)]
    [Arguments(@"(?(?=X)X|)", "", 0, 0)]
    [Arguments(@"(?(?=X))", "", 0, 0)]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#295-299")]
    public void Conditional_on_a_bare_lookahead_predicate_matches_the_expected_span(
        string pattern,
        string subject,
        int expectedIndex,
        int expectedLength
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        m.Index.Should().Be(expectedIndex);
        m.Length.Should().Be(expectedLength);
    }

    // Git issue 479: Segmentation fault when using conditional pattern.
    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#439")]
    public void Nested_conditional_with_lookbehind_and_a_negative_class_does_not_match_at_the_start() =>
        FuzzyRegex.MatchAtStart("A", @"(?(?<=A)|(?(?![^B])C|D))").Success.Should().BeFalse();

    [Test]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#440")]
    public void Same_nested_conditional_matches_an_empty_span_after_the_A_via_search()
    {
        Match m = FuzzyRegex.Match("A", @"(?(?<=A)|(?(?![^B])C|D))");

        m.Index.Should().Be(1);
        m.Length.Should().Be(0);
    }

    // Git issue 498: Conditional negative lookahead inside positive lookahead fails to match.
    [Test]
    [Arguments(@"(?(?=a).|..)", 1)]
    [Arguments(@"(?(?=b).|..)", 2)]
    [Arguments(@"(?(?!a).|..)", 2)]
    [Arguments(@"(?(?!b).|..)", 1)]
    [Skip(
        "needs:conditionals - S21 delivered GROUP_EXISTS, the group-existence condition; the "
            + "lookaround-condition form is the separate CONDITIONAL opcode and is Phase 4's"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#442-445")]
    public void Conditional_with_no_else_branch_chooses_one_dot_or_two_depending_on_the_lookahead(
        string pattern,
        int expectedLength
    )
    {
        Match m = FuzzyRegex.MatchAtStart("ab", pattern);

        m.Index.Should().Be(0);
        m.Length.Should().Be(expectedLength);
    }
}
