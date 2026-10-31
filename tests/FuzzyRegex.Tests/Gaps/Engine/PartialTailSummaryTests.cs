using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101 option D: in a partial call's second pass, a repeat's tail inside a group call whose call
/// site lies in a negative lookaround's body is explored once per key and pass, and later visits
/// with the same key replay its run-out (<c>Matcher.TailAtEnd</c>). These are the rows any such
/// memo must leave alone, and the cost it exists for.
/// </summary>
/// <remarks>
/// <para>
/// The first six rows were found by blind reviews as counterexamples to earlier memo proposals
/// (option C keyed on the repeat, the position and the count, REVIEW-0030; C', REVIEW-0100; C''
/// without every open repeat's count, REVIEW-1100). The judge
/// (<c>tools/probes/d11-brute-judge.py</c>, regex 2026.9.29, continuations of up to four
/// characters, 2026-10-25) agrees on rows 1, 4, 5 and 6. On row 2 it finds no completion because
/// the second branch needs '!', which is outside its alphabet: '!ab' completes it at 4. On row 3 no
/// text completes a match (the lookahead's body matches 'yyw' wherever the last branch could
/// start), and P(3, 3) is the three-valued answer the port gives by its rule: at 3 every item runs
/// out on the held text.
/// </para>
/// <para>
/// The answers are c195e118's, the branch without any tail memo. The witness rows were built by
/// hand, or found by the generated sets in <c>.scratch/s1024/d101/g1</c> (2026-10-25), each against
/// the memo's prototype with one part removed: the answer the mutation gave is in each row's
/// comment. The judge finds no completion of up to four characters for any of them, so the port's
/// P(n, n) at the end is its three-valued answer there; every mutated answer claims an earlier start.
/// </para>
/// </remarks>
public sealed class PartialTailSummaryTests
{
    /// <summary>The partial search's answer and every group, as "P(s,e) g1(i,len) g2-".</summary>
    private static string Answer(Match m)
    {
        string kind = m.PartialMatch ? "P" : "F";
        string span = m.Success ? $"{kind}({m.Index},{m.Index + m.Length})" : "None";
        IEnumerable<string> groups = Enumerable
            .Range(1, m.Groups.Count - 1)
            .Select(i => m.Groups[i].Success ? $"g{i}({m.Groups[i].Index},{m.Groups[i].Length})" : $"g{i}-");
        return string.Join(" ", groups.Prepend(span));
    }

    private static string Answer(string pattern, string text) =>
        Answer(new FuzzyRegex(pattern).Match(text, partial: true));

    /// <summary>The partial search, match and fullmatch answers, separated by " | ".</summary>
    private static string Answers(string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);
        return string.Join(
            " | ",
            Answer(regex.Match(text, partial: true)),
            Answer(regex.MatchAtStart(text, partial: true)),
            Answer(regex.FullMatch(text, partial: true))
        );
    }

    /// <summary>
    /// 1. A tail's failure takes in the caller's frame after a return: the depth-2 tail at 2 fails
    /// because what follows its frame fails, while at depth 1 the same tail goes on to 'y' and 'z'.
    /// 2. The frame's group is part of the state: one repeat sits in the code of groups 1 and 2.
    /// 3. A lazy entry and the changed bit. 4. A positive body's captures leak into the
    /// continuation. 5. "Failed" must mean that nothing touched the text to come. 6. Every open
    /// repeat's count: the inner `x*` tail is the same at the outer repeat's first and second
    /// iteration only if the outer count is ignored.
    /// </summary>
    [Test]
    [Arguments(@"a(?!((?:x(?1)?)*y)z)", "axxyz", "P(5,5) g1-")]
    [Arguments(@"(?!(?1)|a(?2)c)axwcz|!(a((?:xw)*)b)", "axwc", "P(4,4) g1- g2-")]
    [Arguments(@"(?:y|)(?!(?:y??(?:xv)*)*w)yywz", "yyw", "P(3,3)")]
    [Arguments(@"(?:a|ab)(?=b?(?:x(y)?)*z)x", "abxy", "P(0,4) g1(3,1)")]
    [Arguments(@"a(?1)q|(?1)wcd|!((?!(?:wv|w)*..\b))", "awcd", "P(1,4) g1-")]
    [Arguments(@"(?!(?1))w(?!(?!(?1)))|!((?:x*w){2}v|!(?1))", "wxwxw", "P(0,5) g1-")]
    public void A_tail_memo_counterexample_keeps_its_answer(string pattern, string text, string expected) =>
        Answer(pattern, text).Should().Be(expected);

    /// <summary>
    /// One row per part of the memo that a mutation of the prototype showed is needed: with the
    /// part removed, the row's partial search answers as its comment says.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Never returned: a tail whose frame returned is not kept. Kept anyway: P(1,2). Found by
    /// the generated set (1,338 of 220,258 rows changed).</item>
    /// <item>The frame's group: the `(?:xz?)*` tail runs out under group 1 (its `y*q` has no 'q'),
    /// while under group 2 it returns, and the 'y' after the call decides the lookahead. Without the
    /// group: P(0,3).</item>
    /// <item>An enclosing repeat's count: the `(?:yz?)*` tail at 7 runs out when the bounded
    /// `{0,3}` around it can still iterate, and returns through 'q' when it cannot. Without the
    /// enclosing counts: P(0,8).</item>
    /// <item>The repeat's own count, for a bounded repeat: the `(?:xz?){0,3}` tail at 3 after three
    /// iterations runs out ('xq' then no 'w'), after two it iterates once more and returns through
    /// 'q'. Keyed by the minimum alone: P(0,5).</item>
    /// <item>No failure records, and the poison rule: with failures kept and no poison, a loop's
    /// entry (share 2) replays a failure recorded by an end whose own guard had pruned it: F(0,3),
    /// a complete match where none exists (Debug: assert D11 A1). Either rule alone keeps the
    /// answer.</item>
    /// </list>
    /// </remarks>
    [Test]
    [Arguments(@"(?!x(?1)a|(?1)x)xaq|!(!(?1)|(?:x|)+)", "xx", "P(2,2) g1- | None g1- | None g1-")]
    [Arguments(
        @"(?!(?1))(?!(?2)y)xxyw|!(((?:xz?)*(?:!(?2))?)y*q)",
        "xxy",
        "P(3,3) g1- g2- | None g1- g2- | None g1- g2-"
    )]
    [Arguments(
        @"(?!(?1))(?!xy(?1))xyxyxyxqw|!((?:x(?:yz?)*){0,3}(?:(?:xq)*x?w|q)(?:!(?1))?)",
        "xyxyxyxq",
        "P(8,8) g1- | None g1- | None g1-"
    )]
    [Arguments(
        @"(?!(?1))(?!x(?1))xxxxqw|!((?:xz?){0,3}(?:(?:xq)*x?w|q)(?:!(?1))?)",
        "xxxxq",
        "P(5,5) g1- | None g1- | None g1-"
    )]
    [Arguments(@"(?!(?1)b|x(?1)a)xaq|!(!(?1)|(?:(?:x|)+b?)+)", "xaq", "P(3,3) g1- | None g1- | None g1-")]
    public void A_part_of_the_tail_memo_keeps_its_witness(string pattern, string text, string expected) =>
        Answers(pattern, text).Should().Be(expected);

    /// <summary>
    /// The memo's refusal of atomic constructs (an atomic group, a possessive repeat, <c>\X</c>), which
    /// share 1 needs: the compiler's two copies of this lookahead (the `+` loop's first iteration and
    /// the loop) do not reach the same outcome at a position once <c>\X</c> is in the body, and keyed
    /// on the source lookaround the search answers P(4, 4) and the match and fullmatch None (found
    /// by the generated set, 7 of 809,590 rows, every one with <c>\X</c>). The judge's least start
    /// is 4 (completion 'xq'); the port's P(0, 4) is c195e118's, and changing it is not this memo's
    /// business.
    /// </summary>
    [Test]
    [Arguments(@"(?:x(?!(?:(?:\X|(?R)?)+w?)+y)|z)+q", "xxcw")]
    [Arguments(@"(?:x(?!(?:(?:\X|(?R)?)+w?)+y)|z)+q", "xxwx")]
    public void An_atomic_construct_keeps_the_memo_off(string pattern, string text) =>
        Answers(pattern, text).Should().Be("P(0,4) | P(0,4) | P(0,4)");

    /// <summary>
    /// The cost the memo exists for. c195e118 takes 28,157 and 27,521 steps on these rows at
    /// n = 80 (each recursion level explores the same tail again), quadratic in n; main answered at
    /// the first run-out (SHORTCUT, 403 and 323 steps). The memo's prototype took 48 steps per
    /// character on the double negative (4,785 at n = 100) and about 40 on the single one.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 80, 6_000)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 400, 25_000)]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q", 80, 4_500)]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q", 400, 22_000)]
    public void A_recursion_below_a_negative_lookaround_costs_linear_steps(string pattern, int n, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', n);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, n));
            },
            maxSteps,
            "a repeat's tail explored at one key is replayed, not explored again, at every recursion level"
        );
    }

    /// <summary>
    /// Share 2 (design section 9): the loop's entry is keyed like its end, so a deeper frame's tail
    /// at a position meets what a shallower one recorded there at the end. Row 122 at n = 100:
    /// 4,785 Debug steps with the ends alone, 3,997 with the entries too.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 100, 4_300)]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q", 100, 3_500)]
    public void A_loop_entry_replays_what_its_end_recorded(string pattern, int n, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', n);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, n));
            },
            maxSteps,
            "a loop's entry shares its end's key"
        );
    }

    /// <summary>
    /// Share 1 (design section 8): the `+` loop's unrolled first iteration and the loop hold two
    /// copies of the lookahead, which now keep their outcomes under one key. Row 122 at n = 100:
    /// 3,997 Debug steps with each copy decided on its own, 2,903 with the copies sharing.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 100, 3_300)]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q", 100, 2_700)]
    public void The_copies_of_a_lookaround_share_its_outcomes(string pattern, int n, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', n);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, n));
            },
            maxSteps,
            "the compiler's copies of one lookaround share its outcome at a position"
        );
    }

    /// <summary>
    /// Lazy repeats (design section 7): the same rows with a lazy loop. Without the memo at
    /// END_LAZY_REPEAT they took 13,498 and 11,696 Debug steps at n = 40 (main 10,243 and 10,243);
    /// with it 9,946 and 8,144.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y)))+?q", 11_000)]
    [Arguments(@"(?:x(?!(?!y?(?R))))+?q", 9_500)]
    public void A_lazy_loop_below_a_negative_lookaround_replays_its_tails(string pattern, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', 40);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 40));
            },
            maxSteps,
            "a lazy repeat's tail explored at one key is replayed"
        );
    }

    /// <summary>
    /// The poison rule (design section 6) acts on real rows: in this partial finditer a repeat guard
    /// of the frame holds while a tail that ran out across it is followed, so that tail is not kept. No row was found
    /// whose answer the rule changes once failures are not kept (220,258 generated rows, 387 of them
    /// replaying a tail the rule would drop, 2026-10-25), so this count is what goes red without it.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_guard_of_the_frame_keeps_a_tail_out_of_the_memo()
    {
        EngineWork.SkipUnlessCounting();
        long before = WorkCounter.TailsPoisoned;

        var regex = new FuzzyRegex(@"(?!(?1)b|(?1)x)xxaq|!(!(?1)|(?:(?:x|ab)*b?)+)");

        regex.Matches("axa", partial: true).Select(Answer).Should().Equal("P(3,3) g1-");

        (WorkCounter.TailsPoisoned - before).Should().BePositive();
    }

    /// <summary>
    /// The memo's cap: with it lowered to 10 on the state, row 122 at n = 100 keeps exactly 10
    /// tails (98 uncapped) and answers as before; past the cap only replays are lost.
    /// </summary>
    [Test]
    public void The_tail_memo_stops_growing_at_its_cap()
    {
        const string text =
            "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
        var regex = new FuzzyRegex(@"(?:x(?!(?!(?R)?y))|z)+q");
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
        state.TailMemoCapacity = 10;

        int status = Matcher.DoMatch(state, search: true);

        status.Should().Be(MatchStatus.Partial);
        state.TailRanOut.Should().NotBeNull().And.HaveCount(10);
    }

    /// <summary>
    /// The frame cut (FLOOR.md revision 2): rows the cut must leave alone, each with the frame kept
    /// as a call (`(?:!(?1))?` makes the group recursive, so CallInliner does not write it out).
    /// From REVIEW-0100: W-A, the literal checked from the frame's entry, not the run-out (a 'y'
    /// after the entry); W-B to W-D, case folds (KELVIN SIGN, LONG S, full-case ss); W-E, an astral
    /// literal searched as its UTF-16 pair; the three literal-walk rows, a literal only inside a
    /// lookaround, in one alternative, or optional. Each answer is 00e980ef's, before the cut; a
    /// cut that fired wrongly answers P(0, n) (the review's prototype with the predicate always
    /// firing: 333 answers moved in its grid).
    /// </summary>
    [Test]
    [Arguments(@"a(?!(?1)c)|!(.*y(?:!(?1))?)", "axyc", "P(4,4) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(?i:(.*k(?:!(?1))?))", "aKc", "P(3,3) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(?i:(.*s(?:!(?1))?))", "aſc", "P(3,3) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(?fi:(.*ss(?:!(?1))?))", "aßc", "P(3,3) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(?fi:(.*ß(?:!(?1))?))", "assc", "P(4,4) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(.*\U0001D52E(?:!(?1))?)", "a\U0001D52Ec", "P(4,4) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(.*(?!k)y(?:!(?1))?)", "axyc", "P(4,4) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(.*(?:k|y)(?:!(?1))?)", "axyc", "P(4,4) g1- | None g1- | None g1-")]
    [Arguments(@"a(?!(?1)c)|!(.*k?y(?:!(?1))?)", "axyc", "P(4,4) g1- | None g1- | None g1-")]
    public void A_frame_that_can_return_is_not_cut(string pattern, string text, string expected) =>
        Answers(pattern, text).Should().Be(expected);

    /// <summary>
    /// The cut closes the frame only after RunOut has noted the run-out in the frame's call track,
    /// so the call memo keeps "ran out" for the frame's key. Here the positive lookahead's call has
    /// that key: with the track reset before the close (REVIEW-0100 round 2, mutation 4, 3,231
    /// answers moved in its grid) it replays a failure, and the answers become P(1,1), None, None.
    /// </summary>
    [Test]
    public void A_frame_cut_keeps_the_call_memos_run_out() =>
        Answers(@"a(?!(?1)c)(?=(?1))|!(.*q(?:!(?1))?)", "a").Should().Be("P(0,1) g1- | P(0,1) g1- | None g1-");

    /// <summary>
    /// The cut's cost on row 122 and its single-negative sibling: each frame stops at its first
    /// crossing run-out, since no return can pass the 'q' the text lacks. Before it (d447f368)
    /// 2,903 and 2,503 Debug steps at n = 100, 11,603 and 10,003 at n = 400.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 100, 2_200)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q", 400, 8_500)]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q", 100, 1_800)]
    public void A_frame_that_cannot_return_is_cut_at_its_first_run_out(string pattern, int n, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', n);
        long cutsBefore = WorkCounter.FramesCut;

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, n));
            },
            maxSteps,
            "a frame that cannot return stops at its first run-out across it"
        );
        (WorkCounter.FramesCut - cutsBefore).Should().BePositive();
    }

    /// <summary>
    /// The literal walk's property (REVIEW-0100): every text a group matches in full holds the
    /// literal the walk says the group requires. Groups are every one or two of case-sensitive and folded
    /// literals, an astral one, alternations, optional and repeated items and lookarounds; texts are
    /// every string of up to three characters over an alphabet holding each literal, its fold
    /// partners and the astral character. A literal under a fold must never be taken.
    /// </summary>
    [Test]
    public void Every_return_of_a_group_consumes_its_required_literal()
    {
        string[] atoms =
        [
            "x",
            "y",
            "q",
            "(?i:k)",
            "(?i:s)",
            "(?fi:ß)",
            "\U0001D52E",
            "(?:x|y)",
            "(?:q|x)",
            "y?",
            "x*",
            "(?=q)",
            "(?!y)",
            "(?:qy)?",
            ".",
            "q+",
        ];
        string[] alphabet = ["x", "y", "q", "k", "K", "s", "ſ", "ß", "\U0001D52E"];
        var texts = new List<string> { string.Empty };
        for (int length = 1; length <= 3; length++)
        {
            List<string> longer =
            [
                .. texts
                    .Where(t => t.EnumerateRunes().Count() == length - 1)
                    .SelectMany(t => alphabet.Select(a => t + a)),
            ];
            texts.AddRange(longer);
        }

        IEnumerable<string> groups = atoms.Concat(atoms.SelectMany(a => atoms.Select(b => a + b)));
        int checkedGroups = 0;
        int withLiteral = 0;
        foreach (string group in groups)
        {
            var regex = new FuzzyRegex($"x(?!(?1))|!({group}(?:!(?1))?)");
            int[]? literals = regex.PatternObject.TailTables?.GroupRequiredLiterals;
            if (literals is null)
            {
                continue;
            }

            checkedGroups++;
            literals.Should().ContainSingle("the one called group");
            int literal = literals[0];
            if (literal < 0)
            {
                continue;
            }

            withLiteral++;
            string required = char.ConvertFromUtf32(literal);
            var whole = new FuzzyRegex($"(?:{group})");
            foreach (string text in texts.Where(t => whole.FullMatch(t).Success))
            {
                text.Should().Contain(required, $"`{group}` is said to require '{required}'");
            }
        }

        checkedGroups.Should().BeGreaterThan(200);
        withLiteral.Should().BeGreaterThan(30);
    }
}
