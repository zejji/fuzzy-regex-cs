using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D93 (fixed as F87): inside a fuzzy section, a group call answers as the group written
/// out there (D40), with one exception (DECISIONS, 2026-10-17). The written-out answers were measured
/// with this port at depths 4, 5, 6 and 10 on 2026-10-16 (all four agree); upstream 2026.9.10's
/// written-out forms gave the same span and counts where the battery's triage ran them
/// (<c>.scratch/battery/20261016-1/triage-matrix.md</c>, NEW-1).
/// </summary>
public sealed class CallInFuzzySectionTests
{
    /// <summary>
    /// The exception D93 pins (DECISIONS, 2026-10-17): a call that re-enters a call of the same group
    /// already open at the same position, where the re-entry opens a new fuzzy section, is refused, as
    /// left recursion is (Perl and PCRE2 raise an error on recursion at the same position). Each row is
    /// the whole-pattern call <c>(?R)</c> inside the section, so the call re-opens the section; the
    /// comment gives the written-out form's answer, which the refusal does not reach.
    /// </summary>
    [Test]
    // Written out: (1,1) s0i0d2 (upstream's call form too). A deeper level passes the (*SKIP) or
    // (*PRUNE) on a path its enclosing section then rejects, and failing back across it moves the search.
    [Arguments("(?:a(*SKIP)|(?:a(?R)|b){2s+1d<=2})", "b", "(0,0) s0i0d2")]
    [Arguments("(?:a(*SKIP)|(?:a(?R)|bb){2s+1d<=2})", "b", "(0,0) s0i0d2")]
    [Arguments("(?:a(*PRUNE)|(?:a(?R)|bb){2s+1d<=2})", "b", "(0,0) s0i0d2")]
    // Written out: (2,2) s0i0d2.
    [Arguments("(?:a(*SKIP)|(?:a(?R)|b){2s+1d<=2})", "bb", "(1,1) s0i0d2")]
    // No verb. Written out: (0,2) s0i0d2, the deeper level's first fit.
    [Arguments("(?:(?:[ab](?R)|(?(?<=[ab]).|a*))){2i+2d+1s<=4}", "ab", "(0,2) s0i0d1")]
    // A minimum on the section. Written out: None at depths 3 to 10.
    [Arguments("(?:(?:a+(*SKIP)|ab)(?R)?){1<=d<=2}", "ababaaaa", "(0,1) s0i0d2")]
    public void A_reentry_that_opens_a_new_section_is_refused(string pattern, string subject, string expected) =>
        Answer(new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard).Match(subject))
            .Should()
            .Be(expected);

    [Test]
    // Controls, which agree with their written-out forms.
    [Arguments("(?:a|(?:a(?R)|bb){2s+1d<=2})", "b", "(0,0) s0i0d2")]
    [Arguments("(?:a|(?:a(?R)|b){2s+1d<=1})", "bb", "(0,1) s0i0d1")]
    [Arguments("(?:a(*SKIP)|(?:a(?R)|bb){1s+1d<=2})", "b", "(0,1) s1i0d1")]
    public void A_call_in_a_fuzzy_section_answers_as_its_written_out_form(
        string pattern,
        string subject,
        string expected
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard).Match(subject);

        Answer(m).Should().Be(expected);
    }

    [Test]
    // A call of a numbered group written inside the section.
    [Arguments(
        "(?:(?P<g1>(?:(?:ab|a)a+)*?b(?1)?)){1i+1d<=2}",
        "abaababa",
        "(0,5) s0i0d2 | (5,8) s0i0d2 | (8,8) s0i0d1"
    )]
    // The exception (DECISIONS, 2026-10-17): the (?R) re-opens the section, so the re-entry is refused.
    // Written out: (0,1) | (4,4) s0i0d2.
    [Arguments("(?:a+(*SKIP)|(?:(?:a(?R)|[ab]b)){2s+1d<=2})", "abbb", "(0,1) | (3,3) s0i0d2 | (4,4) s0i0d2")]
    public void A_call_in_a_fuzzy_section_finds_what_its_written_out_form_finds(
        string pattern,
        string subject,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        string.Join(" | ", regex.Matches(subject).Select(Answer)).Should().Be(expected);
    }

    /// <summary>
    /// A call that re-enters its group at the same position inside the same section, after spending
    /// errors there. Upstream 2026.9.10 and the written-out forms (depths 4 and 8) agree on every
    /// row (blind review of D93, 2026-10-16, <c>.scratch/s1016/d93/review/</c>); main answered
    /// None for the full matches and a one-deletion (0, 2) for the searches.
    /// </summary>
    [Test]
    [Arguments("(?:(?P<g>a(?&g)b|c)){d<=2}", "cbb", true, "(0,3) s0i0d2")]
    [Arguments("(?:(?P<g>a(?&g)b|c)){d<=2}", "cbb", false, "(0,3) s0i0d2")]
    [Arguments("(?:(?P<g>a(?&g)b|c)){d<=3}", "cbbb", true, "(0,4) s0i0d3")]
    [Arguments(@"(?:(?P<g>\((?&g)\)|x)){d<=2}", "x))", true, "(0,3) s0i0d2")]
    [Arguments(@"(?:(?P<g>\((?&g)\)|x)){d<=2}", "x))", false, "(0,3) s0i0d2")]
    // The orchestrator's lead (2026-10-17): main answered (0,1) with one deletion. Upstream and the
    // written-out form at depths 4 to 8 give three, the section's last error spent on each level.
    [Arguments("(?:(?P<g1>q(?1)|x)){1<=d<=3}", "x", false, "(0,1) s0i0d3")]
    // Controls, which main already answers as upstream does.
    [Arguments("(?:(?P<g>a(?&g)b|c)){e<=2}", "cbb", true, "(0,3) s2i0d0")]
    [Arguments("(?:(?P<g>a(?&g)b|c)){d<=1}", "cb", true, "(0,2) s0i0d1")]
    public void A_call_reentering_its_section_after_an_error_answers_as_written_out(
        string pattern,
        string subject,
        bool full,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        Answer(full ? regex.FullMatch(subject) : regex.Match(subject)).Should().Be(expected);
    }

    /// <summary>
    /// The guard compares a call's section counts, but a count the section never limits only up to
    /// its minimums: kept in full, each level's extra deletion made the call new, and this recursion
    /// went 52 levels deep before it found its fit (measured 2026-10-16). Its written-out forms
    /// differ at every depth, one deletion per level, so they set no answer here; this is main's.
    /// </summary>
    [Test]
    [Arguments("(?:(?P<g1>q(?1)|x)){d}", "x", "(0,1) s0i0d1")]
    public void A_recursion_spending_unlimited_errors_at_one_position_is_still_refused(
        string pattern,
        string subject,
        string expected
    ) =>
        Answer(new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard).Match(subject))
            .Should()
            .Be(expected);

    private static string Answer(Match match)
    {
        if (!match.Success)
        {
            return "None";
        }

        string span = $"({match.Index},{match.Index + match.Length})";
        FuzzyCounts counts = match.FuzzyCounts;
        return counts == default ? span : $"{span} s{counts.Substitutions}i{counts.Insertions}d{counts.Deletions}";
    }
}
