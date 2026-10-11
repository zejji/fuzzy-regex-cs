using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// An item that every alternative of a branch starts with (or, matching right to left, ends
/// with) is moved out in front of the branch only when that cannot change the answer.
/// </summary>
/// <remarks>
/// <para>
/// <c>Branch._split_common_prefix</c> and <c>_split_common_suffix</c>
/// (<c>upstream/regex/_regex_core.py:2250-2331</c>) rewrite <c>XA|XB</c> as <c>X(?:A|B)</c>. That
/// is the same pattern only when <c>X</c> can match in just one way and backtracking through it
/// does nothing. The two forms try the same paths in a different order: <c>XA|XB</c> tries every
/// way of matching <c>X</c> with <c>A</c> before any with <c>B</c>, while <c>X(?:A|B)</c> tries
/// <c>A</c> then <c>B</c> after each way of matching <c>X</c>. And when the match backtracks into
/// <c>(*SKIP)</c> or <c>(*PRUNE)</c> inside the first alternative, the whole attempt at that start
/// position fails and the second alternative is never tried; moved in front of the branch, the
/// verb is reached only after both alternatives have failed.
/// </para>
/// <para>
/// Upstream guards the rewrite with <c>can_be_affix</c>, which refuses repeats, groups and
/// sequences but lets through the verbs, nested branches, group calls and fuzzy sections, so
/// <c>regex.search(r'(*SKIP)[ab]+|(*SKIP)\b', 'ccb')</c> is <c>(0, 0)</c> where PCRE2 10.47 and
/// Perl 5.42 say <c>(2, 3)</c>, as upstream itself does when the second verb is a
/// <c>(*PRUNE)</c> instead (regex 2026.9.10, measured 2026-09-26). With the rewrite disabled
/// upstream agrees with PCRE2 on every one of 4,312 forward rows over 28 node kinds; with it,
/// it disagrees on 415. This port inherited the fault and fixed it, ledger entry 46.
/// </para>
/// </remarks>
public sealed class AlternationAffixTests
{
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the first span of each row.
    [Test]
    [Property("Upstream", "none - gap test")]
    // Backtracking into the verb ends the attempt, so the \b alternative is never tried.
    [Arguments(@"(*SKIP)[ab]+|(*SKIP)\b", "ccb", 2, 3)] // upstream (0, 0)
    [Arguments(@"(*PRUNE)[ab]+|(*PRUNE)\b", "ccb", 2, 3)] // upstream (0, 0)
    [Arguments(@"a(*SKIP)[ab]+|a(*SKIP)\b", "ba", -1, -1)] // upstream (1, 2)
    // Reversed, the verb is a common suffix instead.
    [Arguments(@"(?r)[ab]+(*SKIP)|\b(*SKIP)", "bcc", 0, 1)] // upstream (3, 3)
    [Arguments(@"(?r)[ab]+(*PRUNE)|\b(*PRUNE)", "bcc", 0, 1)] // upstream (3, 3)
    // A nested branch: 'a\K' then 'c' fails, so 'ab' then 'c' is tried before the second alternative.
    [Arguments(@"(?:a\K|ab)c|(?:a\K|ab)", "abc", 0, 3)] // upstream (1, 1)
    // A group call can match in more than one way too.
    [Arguments(@"(?:(?1)c|(?1))|(a|ab)", "abc", 0, 3)] // upstream (0, 1)
    public void An_item_whose_meaning_depends_on_its_alternative_stays_inside_it(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(pattern, subject).Should().Be((start, end));
    }

    // DIVERGES FROM UPSTREAM, deliberately: upstream answers (0, 1) with no errors, and reversed
    // (2, 3) with no errors, because it tries the empty alternative before the error that lets
    // the first one match.
    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?:a){e<=1}c|(?:a){e<=1}", "abc")]
    [Arguments("(?r)c(?:a){e<=1}|(?:a){e<=1}", "cba")]
    public void A_fuzzy_section_stays_inside_its_alternative(string pattern, string subject)
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Length, m.FuzzyCounts).Should().Be((0, 3, new FuzzyCounts(0, 1, 0)));
    }

    // Upstream agrees: these items match in only one way, so moving them out changes nothing.
    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments(@"\Kb|\Kc", "abc", 1, 2)]
    [Arguments("(*FAIL)b|(*FAIL)c", "abc", -1, -1)]
    [Arguments("(?>a|ab)c|(?>a|ab)", "abc", 0, 1)]
    [Arguments("xab|xac", "zxac", 1, 4)]
    [Arguments("(?r)bax|cax", "zcax", 1, 4)]
    public void An_item_that_matches_one_way_answers_the_same_either_way(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Span(pattern, subject).Should().Be((start, end));
    }

    private static (int Start, int End) Span(string pattern, string subject)
    {
        Match m = new FuzzyRegex(pattern).Match(subject);
        return m.Success ? (m.Index, m.Index + m.Length) : (-1, -1);
    }
}
