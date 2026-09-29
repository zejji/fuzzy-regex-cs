using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Ledger entry 47: what a <c>(*PRUNE)</c> or <c>(*SKIP)</c> does when backtracking reaches it
/// inside a group that has not finished. Every row of the engine survey in
/// <c>docs/plan/2026-09-26-verb-confinement-survey.md</c> is pinned here, the ones the change moves
/// and the ones it leaves alone.
/// </summary>
/// <remarks>
/// <para>
/// DIVERGES FROM UPSTREAM, deliberately. Upstream makes every atomic group, possessive repeat,
/// lookaround and conditional test a scope: the verb fails only the innermost one
/// (<c>top_bstack</c>, <c>_regex.c:2811</c>). The port now follows PCRE2's model (pcre2pattern
/// 10.47, "Verbs that act after backtracking" and "Backtracking verbs in assertions"): backtracking
/// onto the verb unwinds to the innermost enclosing negative assertion, which becomes true, or
/// conditional test, which becomes false if positive and true if negative; with neither, the whole
/// attempt fails. Atomic groups, possessive repeats and positive lookarounds are transparent while
/// unfinished, and a finished one is never backtracked into.
/// </para>
/// <para>
/// Each expected value is PCRE2 10.47's answer (interpreter, <c>PCRE2_NO_START_OPTIMIZE</c>),
/// measured 2026-09-26 and quoted in the trailing comment, with two kinds of exception. Called
/// groups stay transparent (see
/// <see cref="A_verb_in_a_called_group_passes_through_the_call"/>). And the port runs a lookbehind
/// body right to left, as upstream does, where PCRE2 runs it left to right, so a lookbehind row
/// reaches the verb only in the engine whose direction matches: the model is applied in the
/// port's direction (b09r, b09, lb1, lb2, nl1, nl1r, cl1, cl1r, cn2, lbs1).
/// </para>
/// </remarks>
public sealed class VerbScopeTests
{
    private static string Answer(string pattern, string subject)
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        return m.Success ? $"({m.Index},{m.Index + m.Length})" : "None";
    }

    // Unfinished atomic group or possessive repeat, alone or nested: transparent, so the attempt
    // fails. `(?>a(*PRUNE)b)|a` over 'ac' was (0,1), the group failing and the second alternative
    // matching; now position 0 is abandoned and 'c' matches neither branch.
    [Test]
    [Arguments(@"(?>aa(*SKIP)x(*PRUNE)y)|a", "aaxz", "(1,2)")] // b01: PCRE2 (1,2), before (0,1)
    [Arguments(@"(?>a(*PRUNE)b)|a", "ac", "None")] // b02: PCRE2 None, before (0,1)
    [Arguments(@"(?>aa(*SKIP)b)|a", "aaca", "(3,4)")] // b03: PCRE2 (3,4), before (0,1)
    [Arguments(@"(?>(*SKIP)ab)|a", "ac", "None")] // b04: PCRE2 None, before (0,1)
    [Arguments(@"(?:a(*PRUNE)b)++|a", "ac", "None")] // b05: PCRE2 None, before (0,1)
    [Arguments(@"(?:aa(*SKIP)b)++|a", "aaca", "(3,4)")] // b06: PCRE2 (3,4), before (0,1)
    [Arguments(@"(?>a(*SKIP)b)|a", "ac", "None")] // sk1: PCRE2 None, before (0,1)
    [Arguments(@"x|(?>a(*SKIP)ab)|a", "aac", "None")] // sk2: PCRE2 None, before (0,1)
    [Arguments(@"a(?>(*SKIP)b)|a", "ac", "None")] // x06: PCRE2 None, before (0,1)
    [Arguments(@"(?>a(*PRUNE)b|ac)|a", "ac", "None")] // alt1: PCRE2 None, before (0,1)
    [Arguments(@"(?:a(*PRUNE)b|ac)++|a", "ac", "None")] // alt2: PCRE2 None, before (0,1)
    [Arguments(@"a+(?>(*PRUNE)x)|a", "aab", "None")] // ap1: PCRE2 None, before (0,1)
    [Arguments(@"(?>a+(*PRUNE)b)|a", "aac", "None")] // ap2: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?>a(*PRUNE)b)|ac)|a", "ac", "None")] // b12: PCRE2 None, before (0,2)
    [Arguments(@"(?>(?>a(*SKIP)ab)|aa)|a", "aac", "None")] // b12b: PCRE2 None, before (0,2)
    [Arguments(@"(?>(?>(?>a(*PRUNE)b)))|a", "ac", "None")] // d1: PCRE2 None, before (0,1)
    [Arguments(@"(?>(a(*PRUNE)b))|a", "ac", "None")] // cg1: PCRE2 None, before (0,1)
    [Arguments(@"(?:(?>a(*PRUNE)b)|a)c", "ac", "None")] // gr3: PCRE2 None, before (0,2)
    [Arguments(@"(?:a(*SKIP)b|a)++|a", "aac", "None")] // ps2: PCRE2 None, before (0,1)
    [Arguments(@"(?:(?>a(*PRUNE)b)|a)++c|a", "ac", "None")] // ps3: PCRE2 None, before (0,2)
    // D31 (2026-09-29): a quantifier that could exit empty does not catch the unwind either. PCRE2
    // 10.47 with and without NO_START_OPTIMIZE; upstream answers as noted. The last row is D31's
    // found row with \m dropped, which PCRE2 lacks; with \m the port and upstream answer the same.
    [Arguments(@"(?>(*SKIP)a)?", "b", "None")] // q1: PCRE2 None, upstream (0,0)
    [Arguments(@"(?>(*PRUNE)a)?", "b", "None")] // q2: PCRE2 None, upstream (0,0)
    [Arguments(@"(?>(*SKIP)a)*", "ab", "None")] // q3: PCRE2 None, upstream (0,1)
    [Arguments(@"(?>(*SKIP)a)*+", "ab", "None")] // q4: PCRE2 None, upstream (0,1)
    [Arguments(@"(?>(*PRUNE)a)*", "ab", "None")] // q5: PCRE2 None, upstream (0,1)
    [Arguments(@"(?>(?>(*SKIP)a))?", "b", "None")] // q6: PCRE2 None, upstream (0,0)
    [Arguments(@"((?>(*SKIP)\$))?", "b", "None")] // q7: PCRE2 None, upstream (0,0)
    [Arguments(@"(?>(*SKIP)a)?", "ab", "(0,1)")] // q8: PCRE2 (0,1), upstream (0,1)
    [Arguments(@"(?:(*SKIP)a)?", "b", "None")] // q9: PCRE2 None, upstream None
    [Arguments(@"(?m)(?> |(?>(*SKIP)\ba|ab)\w(?:\Z|.))*+(?>\B|(?>aa|(*SKIP)a)(?:a*?|\X)(?: ?+|a$))?", "\n", "None")] // q10: PCRE2 None, upstream (0,0)
    public void A_verb_in_an_unfinished_atomic_group_or_possessive_repeat_fails_the_attempt(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // Unfinished positive lookahead or lookbehind: transparent too. pcre2pattern: the verbs "are not
    // treated specially if they appear in a standalone positive assertion".
    [Test]
    [Arguments(@"(?=a(*PRUNE)b)..|a", "ac", "None")] // b07: PCRE2 None, before (0,1)
    [Arguments(@"(?=aa(*SKIP)b)aa|a", "aaca", "(3,4)")] // b08: PCRE2 (3,4), before (0,1)
    [Arguments(@"(?=a(*PRUNE)b|ac)..|a", "ac", "None")] // alt3: PCRE2 None, before (0,1)
    [Arguments(@"(?=(?:a(*PRUNE)b|ac))..|a", "ac", "None")] // va3: PCRE2 None, before (0,1)
    [Arguments(@"(?=x|a(*PRUNE)b|ac)..|a", "ac", "None")] // va4: PCRE2 None, before (0,1)
    [Arguments(@"(?=(*SKIP)ab)|a", "ac", "None")] // ss1: PCRE2 None, before (0,1)
    [Arguments(@"(?=a(*PRUNE)(*FAIL))a|a", "ac", "None")] // fl1: PCRE2 None, before (0,1)
    [Arguments(@"(?=(?>a(*PRUNE)b))..|a", "ac", "None")] // n01: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?=a(*PRUNE)b)..)|a", "ac", "None")] // n02: PCRE2 None, before (0,1)
    [Arguments(@"(?<=(?=a(*PRUNE)b)a)c|c", "ac", "None")] // pb2: PCRE2 None, before (1,2)
    public void A_verb_in_an_unfinished_positive_lookaround_fails_the_attempt(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // Groups between the verb and a negative lookaround are crossed, and the negative lookaround
    // becomes true "without considering any further alternative branches". A negative lookaround
    // or condition inside a positive group stops the unwind there.
    [Test]
    [Arguments(@"(?!(?>a(*PRUNE)b)|a)a", "ac", "(0,1)")] // n03: PCRE2 (0,1), before None
    [Arguments(@"(?!(?=a(*PRUNE)b)|a)a", "ac", "(0,1)")] // n04: PCRE2 (0,1), before None
    [Arguments(@"(?!(?>(?=(?>a(*PRUNE)b)))|a)a", "ac", "(0,1)")] // d2: PCRE2 (0,1), before None
    [Arguments(@"(?<!(?=a(*PRUNE)b)a|a)c", "ac", "(1,2)")] // nb1: PCRE2 (1,2), before None
    [Arguments(@"(?<!(?>(?=a(*PRUNE)b)a)|a)c", "ac", "(1,2)")] // nb2: PCRE2 (1,2), before None
    [Arguments(@"(?<!(?>(?>(?=a(*PRUNE)b)a))|a)c", "ac", "(1,2)")] // nb3: PCRE2 (1,2), before None
    [Arguments(@"(?!(?=aa(*SKIP)b)|a)a", "aac", "(0,1)")] // sn0: PCRE2 (0,1), before None
    [Arguments(@"(?>(?!(?>a(*PRUNE)b)|a)a)|x", "ac", "(0,1)")] // d3: PCRE2 (0,1), before None
    [Arguments(@"(?=(?!(?=a(*PRUNE)b)|a))a|ac", "ac", "(0,1)")] // d4: PCRE2 (0,1), before (0,2)
    [Arguments(@"(?=(?>(?!(?=a(*PRUNE)b)|a)))a|ac", "ac", "(0,1)")] // d5: PCRE2 (0,1), before (0,2)
    public void A_verb_unwinds_through_nested_groups_to_the_innermost_negative_assertion(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // A quantifier's fallback is backtracking like any other, so the verb forbids it: the
    // zero-iteration path of `(?>a(*PRUNE)b)?a` is never taken.
    [Test]
    [Arguments(@"(?:(?>a(*PRUNE)b)|)a", "ac", "None")] // p03: PCRE2 None, before (0,1)
    [Arguments(@"(?>a(*PRUNE)b)?a", "ac", "None")] // x07: PCRE2 None, before (0,1)
    [Arguments(@"(?>a(*PRUNE)b){0,1}a", "ac", "None")] // x07c: PCRE2 None, before (0,1)
    [Arguments(@"(?>a(*PRUNE)b)*a", "ac", "None")] // x07d: PCRE2 None, before (0,1)
    [Arguments(@"(?:(?>a(*PRUNE)b))?a", "ac", "None")] // x07e: PCRE2 None, before (0,1)
    [Arguments(@"(?=a(*PRUNE)b)?a", "ac", "None")] // x07f: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?:a(*PRUNE))*b)|a", "aac", "None")] // rep1: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?:a(*SKIP))*b)|a", "aaca", "None")] // rep2: PCRE2 None, before (0,1)
    [Arguments(@"(?:(?:a(*PRUNE))*b)++|a", "aac", "None")] // rep3: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?:a(*PRUNE))*?b)|a", "aac", "None")] // lz1: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?:a(*PRUNE))+?b)|a", "aac", "None")] // lz2: PCRE2 None, before (0,1)
    [Arguments(@"(?>a(*PRUNE)b)??c|a", "ac", "(1,2)")] // lz3: PCRE2 (1,2), before (0,1)
    [Arguments(@"(?>(?:a(*SKIP))*?c)|a", "aab", "None")] // lz4: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?:a(*SKIP))+b)|a", "aaca", "None")] // gr1: PCRE2 None, before (0,1)
    [Arguments(@"(?>(?>a(*PRUNE)b)*)c|a", "ac", "(1,2)")] // gr2: PCRE2 (1,2), before (0,1)
    [Arguments(@"(?:a(*PRUNE)b|a)*+c|a", "ac", "(1,2)")] // ps1: PCRE2 (1,2), before (0,1)
    public void A_verb_in_a_quantified_group_forbids_the_quantifier_s_fallback(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // The port runs a lookbehind body right to left, so here it reaches the verb, which PCRE2
    // (left to right) never does: 'c' matches, the verb runs, 'x' fails against 'a', and the
    // positive lookbehind is transparent. Upstream and the old port confined the verb: (0,1).
    [Test]
    [Arguments(@"a.(?<=x(*PRUNE)c)|a", "ac", "None")] // b09r: PCRE2 (0,1), before (0,1)
    public void A_verb_in_a_lookbehind_is_applied_in_the_port_s_direction(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    // DELIBERATE CHOICE, not PCRE2's: a called group ((?1), (?&name), (?R)) is transparent, so a
    // verb in it acts on whatever encloses the call. PCRE2 alone makes the call fail and discards a
    // (*SKIP) position recorded inside it ("Backtracking verbs in subroutines"); Perl 5.42, Boost,
    // upstream and this port all agree with each other here, so the port keeps their answer.
    [Test]
    [Arguments(@"(?1)|a|(a(*PRUNE)b)", "ac", "None")] // rc1: PCRE2 (0,1)
    [Arguments(@"(?&g)|a|(?<g>a(*PRUNE)b)", "ac", "None")] // rc2: PCRE2 (0,1)
    [Arguments(@"(?1)c|a|(a(*SKIP)ab)", "aac", "None")] // rc3: PCRE2 (0,1)
    [Arguments(@"(?:(?1)|x)|a|(a(*PRUNE)b)", "ac", "None")] // rc4: PCRE2 (0,1)
    [Arguments(@"a(?R)?b(*PRUNE)c|a", "aabbx", "None")] // rc5: PCRE2 (0,1)
    [Arguments(@"aa(*SKIP)x|ab", "aab", "None")] // sr0: PCRE2 None
    [Arguments(@"(?1)|ab(?(DEFINE)(aa(*SKIP)x))", "aab", "None")] // sr1: PCRE2 (1,3)
    [Arguments(@"(?1)|ab(?(DEFINE)(aa(*PRUNE)x))", "aab", "(1,3)")] // sr2: PCRE2 (1,3)
    [Arguments(@"(?1)x|a|(aa(*SKIP)b)", "aac", "None")] // sr3: PCRE2 (0,1)
    public void A_verb_in_a_called_group_passes_through_the_call(string pattern, string subject, string expected) =>
        Answer(pattern, subject).Should().Be(expected);

    // A called group inside an atomic group: both are transparent, so the attempt fails. Perl
    // 5.42.3 agrees (None); PCRE2 (the call fails) and upstream (the atomic group fails) do not.
    [Test]
    [Arguments(@"(?>(?1))|a|(a(*PRUNE)b)", "ac", "None")] // PCRE2 (0,1), before (0,1)
    [Arguments(@"(?>(?&g))?(?=(?P<cap>a))(?&g)(?(DEFINE)(?P<g>a(*PRUNE)(?P=cap)))", "aa", "None")] // PCRE2 (0,2), before (0,2)
    public void A_verb_in_a_called_group_inside_an_atomic_group_passes_through_both(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    [Test]
    public void A_condition_the_verb_unwinds_to_drops_the_captures_its_test_made()
    {
        // PCRE2 10.47: (0,1), group 1 unset, group 2 (0,1). The unwind crosses the atomic group
        // without running its arm; the CONDITIONAL arm puts the captures back.
        Match m = new FuzzyRegex("(?(?=(?>(a)(*PRUNE)b))ab|(a))").Match("ac");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 1));
        m.Groups[1].Success.Should().BeFalse();
        (m.Groups[2].Success, m.Groups[2].Index).Should().Be((true, 0));
    }

    // (*SKIP) positions that are not ahead of the attempt, or run in the other direction. Forward:
    // the lookbehind reaches the verb at 1, left of the attempt at 2, so the ordinary bump-along
    // follows (PCRE2 and Perl (3,4)). Under (?r) a lookahead's verb lies right of the attempt, so
    // likewise; the third row's verb lies left of the attempt, so the next start moves past 2.
    [Test]
    [Arguments(@"c(?<=x(*SKIP)bc)|d", "abcd", "(3,4)")]
    [Arguments(@"(?r)b(?=c(*SKIP)x)|a", "abcd", "(0,1)")]
    [Arguments(@"(?r)(?>x(*SKIP)aa)|a", "baa", "None")]
    public void A_skip_moves_the_next_start_only_ahead_in_the_search_direction(
        string pattern,
        string subject,
        string expected
    ) => Answer(pattern, subject).Should().Be(expected);

    [Test]
    public void An_unwind_from_a_lookahead_puts_the_callers_slice_back()
    {
        // The lookahead widened the slice to the whole text; without the restore the reverse
        // search would go on left of the caller's start and find 'c' at 0.
        new FuzzyRegex("(?r)(?=a(*PRUNE)b)a|c")
            .Match("cax", 1)
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_skip_past_the_callers_start_leaves_no_attempt()
    {
        // The lookbehind's verb is at 1, left of the slice [2, 4): no start is left, so the empty
        // alternative never matches at 2.
        new FuzzyRegex("(?r)(?<=x(*SKIP)ab)c|")
            .Match("zabc", 2)
            .Success.Should()
            .BeFalse();
    }

    // The rest of the battery: finished groups, verbs directly in a negative lookaround or a
    // conditional test, verbs in a conditional's branch, (*FAIL), top-level verbs, and lookbehind
    // rows where the direction keeps the verb out of reach. None of them moves.
    [Test]
    [Arguments(@"a.(?<=a(*PRUNE)b)|a", "ac", "(0,1)")] // b09: PCRE2 None
    [Arguments(@"(?!a(*PRUNE)b|a)a", "ac", "(0,1)")] // b10: PCRE2 (0,1)
    [Arguments(@"(?(?=a(*PRUNE)b)ab|a)", "ac", "(0,1)")] // b11: PCRE2 (0,1)
    [Arguments(@"(?>a(*FAIL))|a", "ac", "(0,1)")] // b13: PCRE2 (0,1)
    [Arguments(@"(?>a(*PRUNE))b|a", "ac", "(0,1)")] // b14: PCRE2 (0,1)
    [Arguments(@"(?=a(*SKIP))ab|a", "ac", "(0,1)")] // b15: PCRE2 (0,1)
    [Arguments(@"(?:a(*PRUNE)b)|a", "ac", "None")] // b18: PCRE2 None
    [Arguments(@"aa(*SKIP)x(*PRUNE)y|a", "aaxz", "(1,2)")] // x01: PCRE2 (1,2)
    [Arguments(@"(?>a(*SKIP))b|a", "ac", "(0,1)")] // x02: PCRE2 (0,1)
    [Arguments(@"(?=a(*PRUNE))ab|a", "ac", "(0,1)")] // x03: PCRE2 (0,1)
    [Arguments(@"(?=a(*SKIP))..|a", "ac", "(0,2)")] // x04: PCRE2 (0,2)
    [Arguments(@"(?:a(*PRUNE)b)*+a", "ac", "None")] // x08: PCRE2 None
    [Arguments(@"(?>(?!a(*PRUNE)b|a)a)|a", "ac", "(0,1)")] // n05: PCRE2 (0,1)
    [Arguments(@"(?<=a(*SKIP)x)c|c", "abc", "(2,3)")] // lb1: PCRE2 None
    [Arguments(@"(?<=a(*PRUNE)x)c|c", "abc", "(2,3)")] // lb2: PCRE2 None
    [Arguments(@"b(?<=a(*SKIP)xb)|b", "ab", "(1,2)")] // lb3: PCRE2 (1,2)
    [Arguments(@"(?:a(*PRUNE)b)?+a", "ac", "None")] // pos1: PCRE2 None
    [Arguments(@"(?:a(*PRUNE)b){0,1}+a", "ac", "None")] // pos3: PCRE2 None
    [Arguments(@"(?>a(*PRUNE)b)??a", "ac", "(0,1)")] // x07b: PCRE2 (0,1)
    [Arguments(@"(?>(?:a(*PRUNE)b)?)a", "ac", "None")] // x07g: PCRE2 None
    [Arguments(@"(?>a(*FAIL)|a)|b", "ab", "(0,1)")] // f1: PCRE2 (0,1)
    [Arguments(@"(?>aa(*SKIP))x", "aaax", "(1,4)")] // f01: PCRE2 (1,4)
    [Arguments(@"(?=aa(*SKIP))aax", "aaax", "(1,4)")] // f02: PCRE2 (1,4)
    [Arguments(@"(?>aa(*SKIP))x|b", "aaab", "(3,4)")] // f03: PCRE2 (3,4)
    [Arguments(@"(?=aa(*SKIP))...|b", "aab", "(0,3)")] // f04: PCRE2 (0,3)
    [Arguments(@"a+(*PRUNE)x|a", "aab", "None")] // ap3: PCRE2 None
    [Arguments(@"a+(*SKIP)x|a", "aab", "None")] // ap4: PCRE2 None
    [Arguments(@"(?(?=(?>a(*PRUNE)b))ab|a)", "ac", "(0,1)")] // c1: PCRE2 (0,1)
    [Arguments(@"(?>(?(?=a(*PRUNE)b)ab|a))|x", "ac", "(0,1)")] // c2: PCRE2 (0,1)
    [Arguments(@"..(?<!a(*PRUNE)b|ac)", "ac", "None")] // nl1: PCRE2 (0,2)
    [Arguments(@"..(?<!x(*PRUNE)c|ac)", "ac", "(0,2)")] // nl1r: PCRE2 None
    [Arguments(@"(?!(?>(?=a(*PRUNE)b)))a", "ac", "(0,1)")] // nl2: PCRE2 (0,1)
    [Arguments(@"(?<=(?!a(*PRUNE)b|a)a)c", "ac", "(1,2)")] // pb1: PCRE2 (1,2)
    [Arguments(@"(?>.(?<=a(*PRUNE)b))|a", "ac", "(0,1)")] // pb3: PCRE2 (0,1)
    [Arguments(@"..(?(?<=a(*PRUNE)c|b)b|z)", "abz", "None")] // cl1: PCRE2 (0,3)
    [Arguments(@"..(?(?<=x(*PRUNE)b|b)b|z)", "abz", "(0,3)")] // cl1r: PCRE2 None
    [Arguments(@"(?(?!a(*PRUNE)b|a)a|ac)", "ac", "(0,1)")] // cn1: PCRE2 (0,1)
    [Arguments(@"..(?(?<!a(*PRUNE)b|a)b|c)", "aab", "None")] // cn2: PCRE2 (0,3)
    [Arguments(@"(?(?=(?!a(*PRUNE)b|a))a|ac)", "ac", "(0,1)")] // cn3: PCRE2 (0,1)
    [Arguments(@"(?!(?(?=a(*PRUNE)b)ab|a))a|ac", "ac", "(0,2)")] // cn4: PCRE2 (0,2)
    [Arguments(@"(?(?=a)a(*PRUNE)b|a)|a", "ac", "None")] // cb1: PCRE2 None
    [Arguments(@"(?(?=a)ab|a(*PRUNE)b)|a", "ac", "(0,1)")] // cb2: PCRE2 (0,1)
    [Arguments(@"(?!x|a(*PRUNE)b|a)a", "ac", "(0,1)")] // va1: PCRE2 (0,1)
    [Arguments(@"(?!a(*SKIP)b|a)a", "ac", "(0,1)")] // va2: PCRE2 (0,1)
    [Arguments(@"c(?<=a(*SKIP)xc)|c", "abc", "(2,3)")] // lbs1: PCRE2 None
    [Arguments(@"(?<=a(*SKIP)xb)c|b", "axbc", "(2,3)")] // lbs2: PCRE2 (2,3)
    [Arguments(@"(?!(*SKIP)x|b)b", "b", "(0,1)")] // ss2: PCRE2 (0,1)
    [Arguments(@"(?!a(*PRUNE)(*FAIL)|a)a", "ac", "(0,1)")] // fl2: PCRE2 (0,1)
    [Arguments(@"(?>a(*FAIL)|a(*PRUNE)(*F)|a)|x", "ac", "None")] // fl3: PCRE2 None
    [Arguments(@"(?!a(*FAIL)|b)a", "ac", "(0,1)")] // fl4: PCRE2 (0,1)
    [Arguments(@"(?!(?!a(*PRUNE)b|a))a", "ac", "None")] // nn1: PCRE2 None
    [Arguments(@"(?!aa(*SKIP)b)aax", "aaax", "(1,4)")] // sn1: PCRE2 (1,4)
    [Arguments(@"(?!aa(*SKIP)b)ax", "aaxx", "(1,3)")] // sn2: PCRE2 (1,3)
    [Arguments(@"(?(?=aa(*SKIP)b)aab|a)x", "aaxx", "(1,3)")] // sc1: PCRE2 (1,3)
    [Arguments(@"(?(?!ab|a)a|ac)", "ac", "(0,2)")] // cn1c: PCRE2 (0,2)
    public void Rows_the_verb_scope_leaves_alone(string pattern, string subject, string expected) =>
        Answer(pattern, subject).Should().Be(expected);

    // Verbs mrab lacks stay unknown.
    [Test]
    [Arguments(@"(?>a(*COMMIT)b)|a")] // b16
    [Arguments(@"(?>a(*THEN)b)|a")] // b17
    public void Verbs_upstream_lacks_are_still_rejected(string pattern) =>
        FluentActions.Invoking(() => new FuzzyRegex(pattern)).Should().Throw<FuzzyRegexParseException>();
}
