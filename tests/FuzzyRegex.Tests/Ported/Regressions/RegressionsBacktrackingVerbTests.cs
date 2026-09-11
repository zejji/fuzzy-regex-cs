using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about the <c>(*FAIL)</c>, <c>(*PRUNE)</c> and <c>(*SKIP)</c> backtracking-control verbs
/// (Hg issue 153: Request: (*SKIP)).
/// </summary>
/// <remarks>
/// Upstream's <c>(*PRUNE)</c> and <c>(*SKIP)</c> cases here are structured as mirrored pairs:
/// forward, then the same shape again with <c>(?r)</c> for right-to-left. The forward and reverse
/// groups produce identical expected values in every pair, which is upstream's own observation
/// that these two verbs behave the same way in these particular shapes.
/// </remarks>
public sealed class RegressionsBacktrackingVerbTests
{
    [Test]
    [Arguments(@"12(*FAIL)|3", "123", "3")]
    [Arguments(@"(?r)12(*FAIL)|3", "123", "3")]
    [Property("Upstream", "RegexTests.test_hg_bugs#172-173")]
    public void Fail_verb_forces_backtrack_into_the_second_alternative(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#174")]
    public void Prune_verb_commits_the_preceding_run_so_a_trailing_atom_cannot_reuse_it() =>
        FuzzyRegex.Match("123", @"\d+(*PRUNE)\d").Success.Should().BeFalse();

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#175")]
    public void Prune_verb_inside_a_lookahead_does_not_constrain_backtracking_outside_it() =>
        FuzzyRegex.Match("123", @"\d+(?=(*PRUNE))\d").Value.Should().Be("123");

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"\d+(*PRUNE)bcd|[3d]", "123bcd", "123bcd")]
    [Arguments(@"\d+(*PRUNE)bcd|[3d]", "123zzd", "d")]
    [Arguments(@"\d+?(*PRUNE)bcd|[3d]", "123bcd", "3bcd")]
    [Arguments(@"\d+?(*PRUNE)bcd|[3d]", "123zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#176-179")]
    public void Prune_verb_after_a_run_blocks_the_alternative_but_not_a_sibling_branch(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"\d++(?<=3(*PRUNE))zzd|[4d]$", "123zzd", "123zzd")]
    [Arguments(@"\d++(?<=3(*PRUNE))zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"\d++(?<=(*PRUNE)3)zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"\d++(?<=2(*PRUNE)3)zzd|[3d]$", "124zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#180-183")]
    public void Prune_verb_placed_inside_a_possessive_lookbehind_still_blocks_the_failed_alternative(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#184")]
    public void Prune_verb_commits_the_preceding_run_when_searching_right_to_left() =>
        FuzzyRegex.Match("123", @"(?r)\d(*PRUNE)\d+").Success.Should().BeFalse();

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Property("Upstream", "RegexTests.test_hg_bugs#185")]
    public void Prune_verb_inside_a_lookbehind_does_not_constrain_backtracking_outside_it_when_searching_right_to_left() =>
        FuzzyRegex.Match("123", @"(?r)\d(?<=(*PRUNE))\d+").Value.Should().Be("123");

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"(?r)\d+(*PRUNE)bcd|[3d]", "123bcd", "123bcd")]
    [Arguments(@"(?r)\d+(*PRUNE)bcd|[3d]", "123zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#186-187")]
    public void Prune_verb_after_a_run_blocks_the_alternative_when_searching_right_to_left(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"(?r)\d++(?<=3(*PRUNE))zzd|[4d]$", "123zzd", "123zzd")]
    [Arguments(@"(?r)\d++(?<=3(*PRUNE))zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"(?r)\d++(?<=(*PRUNE)3)zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"(?r)\d++(?<=2(*PRUNE)3)zzd|[3d]$", "124zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#188-191")]
    public void Prune_verb_placed_inside_a_possessive_lookbehind_still_blocks_the_failed_alternative_when_searching_right_to_left(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"\d+(*SKIP)bcd|[3d]", "123bcd", "123bcd")]
    [Arguments(@"\d+(*SKIP)bcd|[3d]", "123zzd", "d")]
    [Arguments(@"\d+?(*SKIP)bcd|[3d]", "123bcd", "3bcd")]
    [Arguments(@"\d+?(*SKIP)bcd|[3d]", "123zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#192-195")]
    public void Skip_verb_after_a_run_blocks_the_alternative_the_same_way_as_prune(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"\d++(?<=3(*SKIP))zzd|[4d]$", "123zzd", "123zzd")]
    [Arguments(@"\d++(?<=3(*SKIP))zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"\d++(?<=(*SKIP)3)zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"\d++(?<=2(*SKIP)3)zzd|[3d]$", "124zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#196-199")]
    public void Skip_verb_placed_inside_a_possessive_lookbehind_blocks_the_failed_alternative_the_same_way_as_prune(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"(?r)\d+(*SKIP)bcd|[3d]", "123bcd", "123bcd")]
    [Arguments(@"(?r)\d+(*SKIP)bcd|[3d]", "123zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#200-201")]
    public void Skip_verb_after_a_run_blocks_the_alternative_when_searching_right_to_left(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    [Test]
    [Skip(
        "needs:backtracking-verbs - the matcher has no PRUNE or SKIP opcode yet ((*FAIL) works, it is the FAILURE opcode)"
    )]
    [Arguments(@"(?r)\d++(?<=3(*SKIP))zzd|[4d]$", "123zzd", "123zzd")]
    [Arguments(@"(?r)\d++(?<=3(*SKIP))zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"(?r)\d++(?<=(*SKIP)3)zzd|[4d]$", "124zzd", "d")]
    [Arguments(@"(?r)\d++(?<=2(*SKIP)3)zzd|[3d]$", "124zzd", "d")]
    [Property("Upstream", "RegexTests.test_hg_bugs#202-205")]
    public void Skip_verb_placed_inside_a_possessive_lookbehind_blocks_the_failed_alternative_when_searching_right_to_left(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);
}
