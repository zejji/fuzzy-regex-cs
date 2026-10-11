using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D23, closed as a semantic choice: a lookbehind runs its body right to left, as
/// upstream's engine does, so a <c>(*SKIP)</c> or <c>(*PRUNE)</c> inside it is reached after the
/// items written to its right have matched. PCRE2 10.47 and Perl 5.42 run a lookbehind body left to
/// right and give different answers on these rows. Each row pins the port's answer today.
/// </summary>
/// <remarks>
/// <para>
/// The model: a lookbehind at position p runs its body backwards from p, last item first. A verb in
/// it does what it does anywhere else (ledgers 45 and 47): nothing when it runs, and when
/// backtracking reaches it, it ends the attempt, unless a negative assertion or conditional test
/// encloses it. Only the order in which the body meets the verb differs from PCRE2 and Perl.
/// <c>(*FAIL)</c> has no position, so it gives the same answer in every engine.
/// </para>
/// <para>
/// Each expected value reads "match-at-start search". The trailing comment gives upstream
/// (<c>regex</c> 2026.9.10) and PCRE2 10.47 (pip <c>pcre2</c> 0.7.1, interpreter, with and without
/// <c>NO_START_OPTIMIZE</c>), measured 2026-10-09. PCRE2 refuses an unbounded lookbehind, so the
/// <c>a*</c> rows have a bounded twin with <c>a{0,3}</c>. Survey, cost and the left-to-right
/// design: <c>docs/plan/2026-09-29-verbs-in-lookbehind-design.md</c> on branch
/// <c>design/d23-verb-lookbehind</c>. Reopen rule: if a real user reports this, build that design's
/// option (a), capped at 300 lines and 1.5 days.
/// </para>
/// <para>
/// On the witness, <c>(?m)(?&lt;=$(*SKIP)|a*)</c> over 'ab', the port differs from every surveyed
/// engine. Upstream's (0, 0) there comes from a fault the port removed (ledger 53): its
/// reversed <c>(*SKIP)</c> moves the slice end when it runs, and its multiline <c>$</c> reads the
/// slice end, so <c>$</c> holds at every position. PCRE2 and Perl reach (0, 0) by running the body
/// left to right. The port runs right to left with a <c>$</c> that reads the text end, so its
/// answer is the same with and without (?m).
/// </para>
/// </remarks>
public sealed class VerbInLookbehindTests
{
    private static string Span(Match m) => m.Success ? $"({m.Index},{m.Index + m.Length})" : "None";

    private static string Answer(string pattern, string subject)
    {
        FuzzyRegex regex = new(pattern);

        return $"{Span(regex.MatchAtStart(subject))} {Span(regex.Match(subject))}";
    }

    // The D23 witness and its family: the verb sits after a zero-width item, so a right-to-left run
    // meets the verb first, and a left-to-right run meets it only if the item holds.
    [Test]
    [Arguments(@"(?<=$(*SKIP)|a*)", "ab", "None (2,2)")] // w1: upstream None (2,2); PCRE2 refuses (unbounded)
    [Arguments(@"(?m)(?<=$(*SKIP)|a*)", "ab", "None (2,2)")] // w1 (?m): upstream (0,0) (0,0), from its slice-end fault; PCRE2 refuses
    [Arguments(@"(?<=$(*SKIP)|a{0,3})", "ab", "None (2,2)")] // w5: upstream None (2,2); PCRE2 (0,0) (0,0)
    [Arguments(@"(?m)(?<=$(*SKIP)|a{0,3})", "ab", "None (2,2)")] // w5 (?m): upstream (0,0) (0,0); PCRE2 (0,0) (0,0)
    [Arguments(@"(?m)(?<=$(*SKIP))", "ab", "None (2,2)")] // w11 (?m): upstream (0,0) (0,0); PCRE2 None (2,2)
    [Arguments("(?m)(?<=$(*SKIP))", "a\nb", "None (1,1)")] // w11 over 'a\nb': upstream (0,0) (0,0); PCRE2 None (1,1)
    [Arguments(@"(?<=$(*SKIP)|a)", "ab", "None (2,2)")] // w9: upstream None (2,2); PCRE2 None (1,1)
    [Arguments(@"(?<=(*SKIP)$|a{0,3})", "ab", "(0,0) (0,0)")] // w8, the mirror: upstream (0,0) (0,0); PCRE2 None (2,2)
    [Arguments(@"(?<=(*SKIP)$|a)", "ab", "None (1,1)")] // w10, the mirror: upstream None (1,1); PCRE2 None (2,2)
    [Arguments(@"(?m)(?<=\M(*SKIP)|(*PRUNE)\B)*", ":ab", "None (3,3)")] // D11 grid row: upstream (0,0) (0,0) (ledger 47 scope); PCRE2, \M spelt out, (0,0) (0,0)
    public void A_verb_after_a_zero_width_item_is_reached_first(string pattern, string subject, string expected) =>
        Answer(pattern, subject).Should().Be(expected);

    // A verb between two characters: only the direction that meets the matching character first
    // reaches the verb, so each mirror pair swaps answers between the port and PCRE2.
    [Test]
    [Arguments(@"(?:ab(?<=a(*PRUNE)x|b)|a)", "ab", "(0,2) (0,2)")] // f1: upstream (0,2) (0,2); PCRE2 None None
    [Arguments(@"(?:ab(?<=x(*PRUNE)b|b)|a)", "ab", "None None")] // f2: upstream (0,1) (0,1) (ledger 47); PCRE2 (0,2) (0,2)
    [Arguments(@"b(?<=x(*PRUNE)b|b)", "ab", "None None")] // f9: upstream None None; PCRE2 None (1,2)
    [Arguments(@"b(?<=a(*PRUNE)x|b)", "ab", "None (1,2)")] // f10: upstream None (1,2); PCRE2 None None
    public void A_verb_between_two_characters_is_reached_in_right_to_left_order(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // Controls. (*FAIL) has no position, so direction cannot matter. The last row came from D11's
    // grid with D23's rows, but its verb is in an atomic group, not a lookbehind: it is ledger 47's
    // rule, and PCRE2 agrees with the port.
    [Test]
    [Arguments(@"(?<=$(*FAIL)|a*)", "ab", "(0,0) (0,0)")] // w3: upstream (0,0) (0,0); PCRE2, with a{0,3}, (0,0) (0,0)
    [Arguments(@"(?:ab(?<=a(*FAIL)|b)|a)", "ab", "(0,2) (0,2)")] // f7: upstream (0,2) (0,2); PCRE2 (0,2) (0,2)
    [Arguments(@"(?m)(?>.\M|(*SKIP)(?!(*FAIL)a|\b(*SKIP))*$)?\W*?", ":", "None (1,1)")] // upstream (0,0) (0,0); PCRE2, \M spelt out, None (1,1)
    public void Rows_direction_does_not_decide(string pattern, string subject, string expected) =>
        Answer(pattern, subject).Should().Be(expected);
}
