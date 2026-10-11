using System.Globalization;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 amendment 2: a blocked positive end leaves the saved stack as a normal end leaves it, and
/// <c>REOPEN</c> hands the body back what it had (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>,
/// amendment 2; rows <c>tools/probes/d11-blocked-end-sstack-rows.jsonl</c>).
/// </summary>
/// <remarks>
/// Before the fix the continuation after a blocked lookaround or condition test read the test's
/// parked state as its own: in a called group that threw (C1-C4, C8, C10, N1, N3, G1-G3), and
/// without a call it gave group 1 a wrong span (K1-K3) or a wrong partial (F1). The expected
/// answers are the brute-force judge's (<c>tools/probes/d11-judge-rows.py</c>, measured 2026-09-29),
/// with group 1 taken from the judge's completion, except for the rows the amendment names as
/// sound phantoms: C3's search (the attempt from inside the text to come), F1 (P(0, 2) for match and search, where the judge's search
/// starts at 1), and R4's group 1 (unset: the judge's completion sets it only from the text still
/// to come). C4 and X1 answered a phantom through S57d's escalation until build chunk 9 deleted
/// it, and now answer the judge's None. N2 threw too. The controls (C5-C7, C9, R1-R4) passed
/// before the fix and guard the paths that did not misread. Group 1 is "unset" when it matched
/// nothing, "n/a" when the row does not check it, and otherwise its last capture (a called group's
/// captures are what the judge reports, <c>spans(1)</c>).
/// </remarks>
public sealed class PartialBlockedEndTests
{
    [Test]
    [Arguments("C1", "match", @"(?(DEFINE)(?<c>(?(?=a$)a|a)))(?&c)\n", "a", 0, 1, "0,1", "unset")]
    [Arguments("C1", "search", @"(?(DEFINE)(?<c>(?(?=a$)a|a)))(?&c)\n", "a", 0, 1, "0,1", "unset")]
    [Arguments("C2", "match", @"(?(DEFINE)(?<c>(?=a$)))(?&c)a\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("C2", "search", @"(?(DEFINE)(?<c>(?=a$)))(?&c)a\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("C3", "match", @"(?(DEFINE)(?<c>(?=a$)))(?&c)b", "a", -1, -1, "n/a", "n/a")]
    [Arguments("C3", "search", @"(?(DEFINE)(?<c>(?=a$)))(?&c)b", "a", 1, 1, "unset", "unset")]
    [Arguments("C4", "match", @"(?(DEFINE)(?<c>(?=a\b|a)))(?&c)(b)", "a", -1, -1, "n/a", "n/a")]
    [Arguments("C5", "match", @"(?(DEFINE)(?<c>(?!a\b)))(?&c)a", "a", 0, 1, "0,0", "unset")]
    [Arguments("C6", "match", @"(?(DEFINE)(?<c>(?!a$)))(?&c)a\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("C7", "match", @"(?(DEFINE)(?<c>(?(?!a$)b|a)))(?&c)\n", "a", 0, 1, "0,1", "unset")]
    [Arguments("C8", "match", @"(?(DEFINE)(?<c>(?<=a\b)))a(?&c)\n", "a", 0, 1, "1,1", "unset")]
    [Arguments("C9", "match", @"(?(DEFINE)(?<c>(?>a\b|)))(?&c)\n", "a", 0, 1, "0,1", "unset")]
    [Arguments("C10", "match", @"(?(DEFINE)(?<c>(?=(?>a$|a))))(?&c)(a)\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("C10", "search", @"(?(DEFINE)(?<c>(?=(?>a$|a))))(?&c)(a)\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("N1", "match", @"(?(DEFINE)(?<c>(?=a$))(?<d>(?&c)))(?&d)a\n", "a", 0, 1, "0,0", "unset")]
    [Arguments("N2", "match", @"(?(DEFINE)(?<c>a)(?<d>(?(?=(?&c)\b)(?&c)|b)))(?&d)\n", "a", 0, 1, "0,1", "unset")]
    [Arguments("N3", "match", @"(?(DEFINE)(?<c>(?=a\b))(?<d>x(?&c)))(?&d)a\n", "xa", 0, 2, "1,1", "unset")]
    [Arguments("R1", "match", @"(?:x(?R)|(?=a\b)a)\n", "xa", 0, 2, "n/a", "n/a")]
    [Arguments("R2", "match", @"(?:x(?R)|(?(?=a\b)a|b))\n", "xa", 0, 2, "n/a", "n/a")]
    [Arguments("R3", "match", @"(?:x(?R)|(?=a\b)a)\n", "xxa", 0, 3, "n/a", "n/a")]
    [Arguments("R3", "search", @"(?:x(?R)|(?=a\b)a)\n", "xxa", 0, 3, "n/a", "n/a")]
    [Arguments("R4", "match", @"(?:x(?R)|(?(?=(?>a$|a))a|b))(a)\n", "xa", 0, 2, "unset", "unset")]
    [Arguments("G1", "match", @"(?:((?=a\b)a)|x(?1))\n", "xa", 0, 2, "1,2", "unset")]
    [Arguments("G2", "match", @"(?:((?(?=a\b)a|b))|x(?1))\n", "xa", 0, 2, "1,2", "unset")]
    [Arguments("G3", "match", @"(x(?1)?(?=a\b)a)\n", "xa", 0, 2, "0,2", "0,2")]
    [Arguments("K1", "match", @"(x(?=a$))a\n", "xa", 0, 2, "0,1", "0,1")]
    [Arguments("K2", "match", @"(x(?(?=a$)a|b))\n", "xa", 0, 2, "0,2", "0,2")]
    [Arguments("K3", "match", @"(a(?=b(cd)))", "ab", 0, 2, "0,1", "0,1")]
    [Arguments("F1", "match", @"(?:a(?=b\b)b){1<=e<=1}\n", "ab", 0, 2, "n/a", "n/a")]
    [Arguments("F1", "search", @"(?:a(?=b\b)b){1<=e<=1}\n", "ab", 0, 2, "n/a", "n/a")]
    [Arguments("X1", "match", @"(?(?=(?:a|ab)\b)a|b)(a)(\n)", "ab", -1, -1, "n/a", "n/a")]
    [Arguments("X2", "match", @"a(?=(?:bcd){1<=e<=1}|b)x", "ab", -1, -1, "n/a", "n/a")]
    public void A_blocked_end_leaves_the_saved_stack_as_a_normal_end_does(
        string id,
        string operation,
        string pattern,
        string text,
        int start,
        int end,
        string group1,
        string defaultGroup1
    )
    {
        // Group 1 is the witness that the continuation read the right saved stack, and in most rows
        // it is set only inside a call. Since D51 (ledger entry 63) a call discards its captures on
        // return, so `group1` is read under upstream's rule, which keeps them, as the judge does.
        // `defaultGroup1` is the default rule's answer on the same row, the D11 x D62 path (a
        // partial inside an open call after a blocked end): a group set only inside the call is
        // unset, as in the written-out forms, for example `(?:(?(?=a$)a|a))\n` for C1 (chunk 9
        // review, finding 5, measured 2026-10-03).
        ShouldAnswerUnder(id, operation, pattern, text, start, end, group1, keepCallCaptures: true);
        ShouldAnswerUnder(id, operation, pattern, text, start, end, defaultGroup1, keepCallCaptures: false);
    }

    private static void ShouldAnswerUnder(
        string id,
        string operation,
        string pattern,
        string text,
        int start,
        int end,
        string group1,
        bool keepCallCaptures
    )
    {
        var regex = new FuzzyRegex(pattern);
        regex.PatternObject.KeepCallCaptures = keepCallCaptures;
        id += keepCallCaptures ? " (upstream's capture rule)" : " (D51's capture rule)";
        Match m = operation switch
        {
            "search" => regex.Match(text, partial: true),
            "fullmatch" => regex.FullMatch(text, partial: true),
            _ => regex.MatchAtStart(text, partial: true),
        };

        if (start < 0)
        {
            m.Success.Should().BeFalse(id);
            return;
        }

        (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end), id);
        if (string.Equals(group1, "n/a", StringComparison.Ordinal))
        {
            return;
        }

        Capture[] captures = [.. m.Groups[1].Captures];
        if (string.Equals(group1, "unset", StringComparison.Ordinal))
        {
            captures.Should().BeEmpty(id);
            return;
        }

        string[] span = group1.Split(',');
        captures.Should().NotBeEmpty(id);
        (captures[^1].Index, captures[^1].Index + captures[^1].Length)
            .Should()
            .Be(
                (int.Parse(span[0], CultureInfo.InvariantCulture), int.Parse(span[1], CultureInfo.InvariantCulture)),
                id
            );
    }

    /// <summary>
    /// Repair 1: a blocked end inside a called group under a repeat. The continuation returns from
    /// the call, and backtracking into the return pushes the call's frame again with the caller's
    /// repeat guards as they are then, longer than it went (0 to 64 on the call, 0 to 96 on the
    /// re-push, in the first row), so every saved-stack depth above it moves. REOPEN now rebases its
    /// frame's depth by the distance the innermost call's frame end moved. The first three rows are
    /// the review's (before: P(0, 11) on a one-character text, a NullReferenceException, an
    /// InvalidOperationException); the rest are one per call kind and repeat from
    /// <c>tools/probes/d11-called-repeat-grid.py</c>, each an exception before, and each the judge's
    /// answer. The first row answered P(0, 1) through S57d's escalation until build chunk 9
    /// deleted it; the judge answers None.
    /// </summary>
    // Upstream answers None to every match row and P(|t|, |t|) to every search row.
    [Test]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a\b)))(?&c)*b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?(?=a$)|b)))(?&c)*b", "a", -1, -1)]
    [Arguments("match", @"(?:((?=(?:a|ab)$)a)|x(?1))+(?>a|ab)\n", "xab", -1, -1)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=a\b)))(?&c)*b", "a", 1, 1)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=a\b)))(?&c)*?b", "a", 1, 1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$)))(?&c)+b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?(?=a$)|b)))(?&c)+?b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$)))(?&c){1,3}b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$)))(?&c){2,}b", "a", -1, -1)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=a\b))(?<d>x?(?&c)))(?&d)*b", "a", 1, 1)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=a\b))(?<d>x?(?&c)))(?&d)*?b", "a", 1, 1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$))(?<d>x?(?&c)))(?&d)+b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$))(?<d>x?(?&c)))(?&d)+?b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$))(?<d>x?(?&c)))(?&d){1,3}b", "a", -1, -1)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=a$))(?<d>x?(?&c)))(?&d){2,}b", "a", -1, -1)]
    [Arguments("search", @"(?:((?=a\b))|x(?1))*b", "xa", 2, 2)]
    [Arguments("search", @"(?:((?=a\b))|x(?1))*?b", "xa", 2, 2)]
    [Arguments("match", @"(?:((?=a$))|x(?1))+b", "xa", -1, -1)]
    [Arguments("match", @"(?:((?=a$))|x(?1))+?b", "xa", -1, -1)]
    [Arguments("match", @"(?:((?=a$))|x(?1)){1,3}b", "xa", -1, -1)]
    [Arguments("match", @"(?:((?=a$))|x(?1)){2,}b", "xa", -1, -1)]
    public void A_blocked_end_in_a_called_group_under_a_repeat_follows_the_call_frame(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    ) => ShouldAnswer(operation, pattern, text, start, end);

    /// <summary>
    /// Repair 2: the open-section link a REOPEN restores. A fuzzy section with a minimum, opened
    /// before the lookahead (in the inner call's body, or in an outer call's body around the inner
    /// call), lies below the parked state; the backtracking arms have already put its link back at
    /// the depth the stack has now, and it moves with its own call's frame, not the innermost's, so
    /// REOPEN keeps that value. Only a section the body itself left open (the two <c>bcd</c> rows: a
    /// run-out) lies in the bytes REOPEN puts back and takes the saved link.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each side is pinned. Shifting every link above the innermost call's frame end threw in
    /// InsertionsCanMeetMinimum on the outer-call rows; restoring the saved link unshifted threw on
    /// all 24 call rows; keeping the current link on the two <c>bcd</c> rows breaks the Debug assert
    /// at END_FUZZY; and always taking the saved link moved by the call's shift breaks the same
    /// assert on the last row, search over 'xab' (review of 8f4fefe, a Debug build).
    /// </para>
    /// <para>
    /// The 24 call rows pin today's answer, P(0, 2), which is a phantom: no continuation completes
    /// a match (the judge: match None, search P(1, 2), completed by 'aba\n'). It is D18's class,
    /// the design's limits C3 and C4: two blocked lookaheads read position 2 independently and need
    /// contradictory text there. The call-free twins <c>(?:(?=ab?$)){1&lt;=i&lt;=1}b(?=a)</c> and
    /// <c>(?=ab?$)abx</c> over 'ab' answer the same P(0, 2) (measured 2026-09-30). The partial
    /// contract allows it (<c>PartialContractTests</c>, owner decision 2026-10-08), so these rows
    /// pin the answer as well as the link.
    /// </para>
    /// </remarks>
    [Test]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d)*\n", "ab", 0, 2)]
    [Arguments(
        "search",
        @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d)*\n",
        "ab",
        0,
        2
    )]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c)*\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c)*\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c)*\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c)*\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d)*\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d)*\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d)+\n", "ab", 0, 2)]
    [Arguments(
        "search",
        @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d)+\n",
        "ab",
        0,
        2
    )]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c)+\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c)+\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c)+\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c)+\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d)+\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d)+\n", "ab", 0, 2)]
    [Arguments(
        "match",
        @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d){1,3}\n",
        "ab",
        0,
        2
    )]
    [Arguments(
        "search",
        @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&d){1,3}\n",
        "ab",
        0,
        2
    )]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c){1,3}\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1})(?<z>(?:q){1<=e<=1}))(?&c){1,3}\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c){1,3}\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?:x?(?=ab?$)){1<=e<=1}))(?&c){1,3}\n", "ab", 0, 2)]
    [Arguments("match", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d){1,3}\n", "ab", 0, 2)]
    [Arguments("search", @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:x?(?&c)){1<=e<=1}))(?&d){1,3}\n", "ab", 0, 2)]
    [Arguments("match", @"a(?=(?:bcd|b){1<=e<=1})x", "ab", -1, -1)]
    [Arguments("search", @"a(?=(?:bcd|b){1<=e<=1})x", "ab", 2, 2)]
    [Arguments(
        "search",
        @"(?(DEFINE)(?<c>(?=ab?$))(?<d>(?:(?:(?&c)){1,3}){1<=s<=1})(?<e>(?:(?&d)){e<=1}))(?:(?&e))*q",
        "xab",
        3,
        3
    )]
    public void A_reopened_section_link_is_the_one_the_stack_holds_now(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    ) => ShouldAnswer(operation, pattern, text, start, end);

    /// <summary>
    /// The port grid's counters (VERIFICATION rule 11): K1's blocked end moves the parked state and
    /// its REOPEN puts it back when the yes-path fails on 'a'; K3's body runs out with its group
    /// still open, so the move is longer than the parked state. Debug builds only.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(
        @"(x(?=a$))a
",
        "xa",
        "BlockedEndMovedSstack",
        1
    )]
    [Arguments(@"(x(?=a$))b", "xa", "ReopenRestoredSstack", 1)]
    [Arguments(@"(a(?=b(cd)))", "ab", "BlockedEndMovedMore", 1)]
    public void A_blocked_end_s_move_is_counted_in_a_debug_build(string pattern, string text, string counted, int count)
    {
        var regex = new FuzzyRegex(pattern);
        using MatchState state = MatchState.Create(
            regex.PatternObject,
            text.AsMemory(),
            0,
            text.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: false);

        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(Enum.Parse<PartialEvent>(counted)).Should().Be(count);
    }

    private static void ShouldAnswer(string operation, string pattern, string text, int start, int end)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "search", StringComparison.Ordinal)
            ? regex.Match(text, partial: true)
            : regex.MatchAtStart(text, partial: true);

        if (start < 0)
        {
            m.Success.Should().BeFalse();
        }
        else
        {
            (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        }
    }
}
