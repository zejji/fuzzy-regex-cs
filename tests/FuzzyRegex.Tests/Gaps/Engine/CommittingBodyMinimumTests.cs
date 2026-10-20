using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D117, closed as not a defect: an atomic group or possessive repeat inside a fuzzy section with a
/// minimum error count commits to its body's first match, and that match may spend the error the
/// minimum asks for.
/// </summary>
/// <remarks>
/// <para>
/// The "needed" rule (ledger entry 44) admits an empty deleting iteration while an open section's
/// minimum is unmet, and a greedy repeat tries it before stopping. Inside an atomic body that makes
/// the body's first match the one with the deletion. In <c>(?:(?&gt;a*)c){1&lt;=d&lt;=1}</c> over ''
/// the group commits to deleting an <c>a</c>, <c>c</c> then needs a second deletion, and the search
/// fails, although <c>{d&lt;=1}</c> finds (0, 0) with one deletion. That is the loss
/// <c>fuzzy-budget-monotone-existence</c> already records as by design inside an atomic group
/// (docs/ORACLE-INVARIANTS.md): changing a section's bounds can change what the group commits to.
/// </para>
/// <para>
/// The alternative, ignoring an enclosing section's minimum inside the body, was built as a trial
/// and rejected (DECISIONS 2026-10-20): the body then commits to its exact match, so a section that
/// ends in a possessive or atomic repeat can no longer meet its minimum at the end of a word, and
/// <c>\b(?:\w++){1&lt;=d&lt;=1}</c> over 'hello world' fell from (0, 5) to the empty (5, 5). The
/// rows below are each one the trial changed. Upstream's answers (<c>regex</c> 2026.9.10, measured
/// 2026-10-20) are quoted beside each; where it differs, the cause is ledger entry 44.
/// </para>
/// </remarks>
public sealed class CommittingBodyMinimumTests
{
    // AGREES WITH UPSTREAM 2026.9.10 (rows marked "the same").
    [Test]
    [Arguments(@"\b(?:\w++){1<=d<=1}", "hello world", 0, 5, 0, 0, 1)] // the same
    [Arguments(@"(?:\w++){1<=d<=1}", "hello.", 0, 5, 0, 0, 1)] // the same
    [Arguments(@"(?:colou?r++){1<=e<=1}", "colour", 0, 6, 0, 0, 1)] // the same
    [Arguments(@"(?:\w++\s){1<=d<=1}", "hello world", 0, 6, 0, 0, 1)] // the same
    [Arguments(@"(?:(?>a*)c){1<=d<=1}", "c", 0, 1, 0, 0, 1)] // the same
    [Arguments(@"(?>a*){1<=d<=1}", "", 0, 0, 0, 0, 1)] // the same
    public void A_section_ending_in_a_committing_body_meets_its_minimum_there(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // AGREES WITH UPSTREAM 2026.9.10 (None on each): the body commits to the deletion, and what
    // follows it in the section needs one more. Without the minimum the port finds (0, 0) with one
    // deletion (upstream None, ledger entry 44).
    [Test]
    [Arguments(@"(?:(?>a*)c){1<=d<=1}", "")]
    [Arguments(@"(?:a*+c){1<=d<=1}", "")]
    [Arguments(@"x(?:(?>a*)c){1<=d<=1}y", "xy")]
    public void A_committed_deletion_the_rest_of_the_section_cannot_afford_fails_the_match(
        string pattern,
        string subject
    )
    {
        new FuzzyRegex(pattern).Match(subject).Success.Should().BeFalse();
        new FuzzyRegex(pattern.Replace("1<=d", "d", StringComparison.Ordinal))
            .Match(subject)
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(0, 0, 1));
    }
}
