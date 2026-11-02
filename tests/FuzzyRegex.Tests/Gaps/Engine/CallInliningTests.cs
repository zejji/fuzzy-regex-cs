using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101: a group call that nothing can tell from its group written out is compiled as that group
/// written out (<c>Parsing.CallInliner</c>). D40 says a call answers as its group written out at the
/// call site, and D51 says the return puts back every group and repeat, so for such a call only the
/// text it consumed is visible afterwards, which the written-out copy consumes the same way. The
/// call form then costs what the written-out form costs, and the run-outs inside calls that D101
/// answered early cannot arise: there is no call left to run out inside.
/// </summary>
/// <remarks>
/// Each step count below is Debug <c>WorkCounter.Steps</c> for a partial search over the text
/// shown, measured 2026-10-24 on fix/d101 at d64537d6 (B1, no inlining) against the same pattern
/// with the call written out. The bound is the written-out count plus a little; B1's count is in
/// the comment, so the test fails before the change and passes only where the call costs what its
/// written-out form costs.
/// </remarks>
public sealed class CallInliningTests
{
    private static readonly string _xs = new('x', 100);

    private static string Answer(Match m)
    {
        if (!m.Success)
        {
            return "None";
        }

        string kind = m.PartialMatch ? "P" : "F";
        return $"{kind}({m.Index}, {m.Index + m.Length})";
    }

    private static void ShouldAnswerAsWrittenOutWithin(string called, string writtenOut, string text, long maxSteps)
    {
        EngineWork.SkipUnlessCounting();
        string expected = Answer(
            new FuzzyRegex(writtenOut, FuzzyRegexOptions.None, EngineWork.HangGuard).Match(text, partial: true)
        );
        var regex = new FuzzyRegex(called, FuzzyRegexOptions.None, EngineWork.HangGuard);

        EngineWork.ShouldTakeAtMostSteps(
            () => Answer(regex.Match(text, partial: true)).Should().Be(expected),
            maxSteps,
            "a call nothing can tell from its group written out costs what the written-out form costs"
        );
    }

    /// <summary>
    /// D101's adversarial rows. The negative condition test: B1 124,091 steps, written out 3,291
    /// (main 62,104, answering P(0, 100), which no text completes). The positive lookahead, D98's
    /// row: B1 123,503, written out 2,501.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(x)(?(?!(?1)*y)a|b)\Z", @"(x)(?(?!(?:x)*y)a|b)\Z", 3_400)]
    [Arguments(@"(x)(?=(?1)*y)\Z", @"(x)(?=(?:x)*y)\Z", 2_600)]
    public void A_transparent_call_costs_what_its_written_out_form_costs(
        string called,
        string writtenOut,
        long maxSteps
    ) => ShouldAnswerAsWrittenOutWithin(called, writtenOut, _xs, maxSteps);

    /// <summary>
    /// Bodies the transparency rules let through although they hold more than plain items: a verb,
    /// <c>\K</c>, a fuzzy section, a lookaround. Each is the same construct written out, so the copy
    /// answers as the call does under D40. B1: 74,789 (verb), 144,489 (<c>\K</c>), 12,991 and a
    /// different answer, P(0, 10) where the written-out form says P(8, 10) (fuzzy, over ten x's).
    /// Written out: 22,995, 53,292, 5,814.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?<d>x(*PRUNE))(?(?!(?&d)*y)a|b)\Z", @"(?<d>x(*PRUNE))(?(?!(?:x(*PRUNE))*y)a|b)\Z", 100, 23_500)]
    [Arguments(@"(?<d>x\K)(?(?!(?&d)*y)a|b)\Z", @"(?<d>x\K)(?(?!(?:x\K)*y)a|b)\Z", 100, 54_000)]
    [Arguments(@"(?<d>(?:x){i<=1})(?(?!(?&d)*y)a|b)\Z", @"(?<d>(?:x){i<=1})(?(?!(?:(?:x){i<=1})*y)a|b)\Z", 10, 6_000)]
    public void A_body_with_a_verb_keep_or_fuzzy_section_is_written_out_too(
        string called,
        string writtenOut,
        int length,
        long maxSteps
    ) => ShouldAnswerAsWrittenOutWithin(called, writtenOut, new string('x', length), maxSteps);

    /// <summary>
    /// A chain of calls nests its copies: group 1's copy of group 2 holds group 3's copy, and so on.
    /// Copies stop where they would make the tree more than 32 levels deeper than it is as written,
    /// which keeps every later pass over the tree as far inside the stack as the parser's nesting
    /// limit does; the calls left over stay calls. Goes red without the depth cap: the first copy
    /// alone would nest 1,500 groups deep, and the compile runs out of stack.
    /// </summary>
    [Test]
    public void A_chain_of_calls_copies_no_deeper_than_the_cap()
    {
        var chain = new System.Text.StringBuilder();
        for (int i = 1; i < 1_500; i++)
        {
            chain.Append(System.Globalization.CultureInfo.InvariantCulture, $"(a(?{i + 1}))");
        }

        chain.Append("(a)");
        var regex = new FuzzyRegex(chain.ToString(), FuzzyRegexOptions.None, EngineWork.HangGuard);

        // The calls nearest the chain's end are written out, the rest stay calls. A full match
        // needs 1,125,750 a's and every call saves every group (D68), so only a miss is run.
        regex.InlinedCalls.Should().BeInRange(1, 100);
        regex.IsMatch(new string('a', 1_000)).Should().BeFalse();
    }

    /// <summary>
    /// A pattern whose every call is written out compiles to exactly the code of the same pattern
    /// with each call written out by hand: in a reverse pattern, in a lookbehind, in a fuzzy
    /// section, with groups inside the called group, through a call inside a called group, and with
    /// a section that allows no errors, which goes once no call is left, as it would have in the
    /// written-out pattern (the last row goes red if it stays).
    /// </summary>
    [Test]
    [Arguments(@"(?<d>x)(?&d)", @"(?<d>x)(?:x)")]
    [Arguments(@"(?r)(?<d>ab)(?&d)c", @"(?r)(?<d>ab)(?:ab)c")]
    [Arguments(@"(?<d>ab)c(?<=(?&d)c)", @"(?<d>ab)c(?<=(?:ab)c)")]
    [Arguments(@"(?<d>ab)(?:c(?&d)){e<=1}", @"(?<d>ab)(?:c(?:ab)){e<=1}")]
    [Arguments(@"(?<a>(?<b>x)y)(?&a)", @"(?<a>(?<b>x)y)(?:(?:x)y)")]
    [Arguments(@"(?<a>x(?&b))(?<b>y)(?&a)", @"(?<a>x(?:y))(?<b>y)(?:x(?:y))")]
    [Arguments(@"(?:ab){e<=0}(?<d>x)(?&d)(?:c){e<=1}", @"(?:ab){e<=0}(?<d>x)(?:x)(?:c){e<=1}")]
    public void A_pattern_with_every_call_written_out_compiles_as_written_out_by_hand(
        string called,
        string writtenOut
    ) =>
        Fuzzy
            .Text.RegularExpressions.Parsing.PatternCompiler.Compile(called)
            .Code.Should()
            .Equal(Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.Compile(writtenOut).Code);

    /// <summary>
    /// The copy holds no capture group: the copy's groups become plain groups, because a call puts
    /// back every group on return (D51) and on a partial result (D62), so a capture made inside the
    /// call is never seen after it. B1 164,889 steps on the first part, written out 4,089. The
    /// second part goes red if the copy keeps its capture groups: group b would then report the
    /// copy's 'x' at 1, captured twice.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void Captures_made_inside_a_written_out_call_stay_inside_it()
    {
        ShouldAnswerAsWrittenOutWithin(@"((x))(?(?!(?1)*y)a|b)\Z", @"((x))(?(?!(?:x)*y)a|b)\Z", _xs, 4_200);

        Match m = new FuzzyRegex("(?<a>(?<b>x))(?&a)").Match("xx");
        (m.Index, m.Length).Should().Be((0, 2));
        (m.Groups["b"].Index, m.Groups["b"].Length, m.Groups["b"].Captures.Count).Should().Be((0, 1, 1));
    }

    /// <summary>
    /// A body that reads a group set inside the copy stays a call, because the copy holds no capture
    /// groups and the read would see the caller's group instead. Inside the call, group 2 holds the
    /// 'b' the call captured, so <c>(.)\2</c> matches 'bb' and the search answers (0, 4); the copy
    /// <c>(?:.)\2</c> would read the definition's 'a' and answer None. A read of a group set outside
    /// the copy sees the same value in both forms (the call starts from the caller's groups), so
    /// that body is written out: B1 122,661 steps, written out 43,461.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_backreference_to_a_group_inside_the_copy_keeps_the_call()
    {
        ShouldAnswerAsWrittenOutWithin(
            @"(.)(?<d>\1)(?(?!(?&d)*y)a|b)\Z",
            @"(.)(?<d>\1)(?(?!(?:\1)*y)a|b)\Z",
            _xs,
            44_000
        );

        Match m = new FuzzyRegex(@"(?<a>(.)\2)(?&a)").Match("aabb");
        (m.Success, m.Index, m.Length).Should().Be((true, 0, 4));
    }

    /// <summary>
    /// As above for a condition. Over 'xyz' the first call's <c>(x)?</c> takes the 'x', so its
    /// condition takes 'y'; the second call's group starts unset, from the caller's groups, takes
    /// nothing, and its condition takes 'z': (0, 3). Copies whose group 2 is a plain group would
    /// never set it and answer (2, 3). A condition on a group outside the copy is written out:
    /// B1 135,633 steps, written out 56,037.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_condition_on_a_group_inside_the_copy_keeps_the_call()
    {
        ShouldAnswerAsWrittenOutWithin(
            @"(x)?(?<d>(?(1)x|z))(?(?!(?&d)*y)a|b)\Z",
            @"(x)?(?<d>(?(1)x|z))(?(?!(?:(?(1)x|z))*y)a|b)\Z",
            _xs,
            57_000
        );

        Match m = new FuzzyRegex(@"(?(DEFINE)(?<a>(x)?(?(2)y|z)))(?&a)(?&a)").Match("xyz");
        (m.Success, m.Index, m.Length).Should().Be((true, 0, 3));
    }

    /// <summary>
    /// A call that can reach itself stays a call, and a transparent call next to it is still written
    /// out. B1 124,789 steps, written out 4,089: the recursion sits in a branch the text never
    /// enters. The second part goes red if the recursion is unrolled: the pattern then compiles to
    /// more code than with every call kept.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_recursive_call_stays_a_call()
    {
        ShouldAnswerAsWrittenOutWithin(
            @"(?<d>x)(?(?!(?&d)*y)a|b)\Z|q(?<r>a(?&r)?b)",
            @"(?<d>x)(?(?!(?:x)*y)a|b)\Z|q(?<r>a(?&r)?b)",
            _xs,
            4_200
        );

        const string recursive = "(?<r>a(?&r)?b)c";
        FuzzyRegex
            .WithDefaultVersion(
                recursive,
                FuzzyRegexOptions.None,
                EngineWork.HangGuard,
                null,
                Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.DefaultVersion
            )
            .PatternObject.NodeList.Count.Should()
            .Be(
                FuzzyRegex
                    .WithDefaultVersion(
                        recursive,
                        FuzzyRegexOptions.None,
                        EngineWork.HangGuard,
                        null,
                        Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.DefaultVersion,
                        upstreamCalls: true
                    )
                    .PatternObject.NodeList.Count
            );
    }

    /// <summary>
    /// Under upstream's capture rule (<c>PatternObject.KeepCallCaptures</c>, which only the oracle
    /// and these tests set) a call's capture stays on its group's list, so every call is visible and
    /// none may be written out: the pattern must be compiled with its calls kept, and the rule
    /// cannot be switched on afterwards for a pattern whose calls were written out. Goes red if the
    /// calls of such a compile are written out: the list would hold one capture.
    /// </summary>
    [Test]
    public void Upstreams_capture_rule_keeps_every_call()
    {
        GroupCallTests.WithUpstreamCallCaptures("(?<a>x)(?&a)").Match("xx").Groups["a"].Captures.Count.Should().Be(2);
        new FuzzyRegex("(?<a>x)(?&a)").Match("xx").Groups["a"].Captures.Count.Should().Be(1);

        Action switchedOnLate = static () => new FuzzyRegex("(?<a>x)(?&a)").PatternObject.KeepCallCaptures = true;
        switchedOnLate.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Copies multiply with nesting: each level below calls the one before it twice, so writing
    /// every call out would put 2^16 copies of <c>xy|yx</c> in the pattern. Copies stop once the
    /// pattern has grown by three times its size as written (or by 1,024 nodes, whichever is more),
    /// and the calls left over stay calls. The first branch's call comes first and is written out
    /// (B1 124,789 steps; written out, with the chain's branch, 4,089). The node count goes red without the cap: about half
    /// a million nodes instead of a few thousand.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void Copies_stop_at_the_size_cap()
    {
        var chain = new System.Text.StringBuilder("(?<c0>xy|yx)");
        for (int i = 1; i <= 16; i++)
        {
            chain.Append(System.Globalization.CultureInfo.InvariantCulture, $"(?<c{i}>(?&c{i - 1})(?&c{i - 1}))");
        }

        ShouldAnswerAsWrittenOutWithin(
            @"(x)(?(?!(?1)*y)a|b)\Z|q" + chain,
            @"(x)(?(?!(?:x)*y)a|b)\Z|q" + chain,
            _xs,
            4_200
        );

        new FuzzyRegex("q" + chain).PatternObject.NodeList.Count.Should().BeLessThan(10_000);
    }

    /// <summary>
    /// The rows D101's partial contract pinned as partials (<c>PartialContractTests</c> until the
    /// calls were written out), now each the judge's answer. The judge (brute force over every
    /// continuation of up to three characters, on the written-out form,
    /// <c>tools/probes/d11-brute-judge.py</c>, regex 2026.9.29) answers None on the anchored rows
    /// and P(2, 2) on the search: 'xyxx' completes a match from 2 and no text does from 0 (MATRIX.md
    /// in <c>.scratch/s1021/d101</c>). The k43 row's lookahead body <c>(?:a*a){s&lt;=1}</c> matches
    /// 'a' at 0 whatever follows, so the negative lookahead fails and no text completes it. The last
    /// row was D74's: no continuation completes it, and main before D11 and upstream 2026.9.10
    /// answer None (the contract test's own remarks).
    /// </summary>
    [Test]
    [Arguments("match", @"(x)(?(?=y?(?1))a|b)", "xy", -1, -1)]
    [Arguments("match", @"(x)(?>(?=(?1)))\Z", "x", -1, -1)]
    [Arguments("match", @"(x)(?:(?=(?1))\Z){e<=1}", "xy", -1, -1)]
    [Arguments("search", @"(x)(?:(?=(?1))\Z){e<=1}", "xy", 2, 2)]
    [Arguments("match", @"(?!(?:a*a){s<=1})(?P<g1>)(?1)", "a", -1, -1)]
    [Arguments("match", @"(?(?=(?1))x|ab)((?:ab)*(?<=b))?", "ab", -1, -1)]
    public void The_rows_the_partial_contract_pinned_answer_as_the_judge_does(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "match", StringComparison.Ordinal)
            ? regex.MatchAtStart(text, partial: true)
            : regex.Match(text, partial: true);

        bool found = start >= 0;
        (m.Success, m.Success ? m.Index : -1, m.Success ? m.Index + m.Length : -1).Should().Be((found, start, end));
    }

    // D101's open-defect rows (OpenDefectTests until the calls were written out): each call below
    // is transparent, so it is written out and answers as its written-out form, which the judge
    // agrees with.
    // D101, the rows below (moved from OpenDefectTests). A run-out inside a group call that a deciding body opened answers the
    // partial at the attempt's start (Matcher.RunOutInALookaroundBody's SHORTCUT), where the same
    // pattern with the call written out as (?:x) answers as the judge does. Each row's call form
    // and written-out form were measured on main 25c638ff, and the judge (d11-brute-judge.py,
    // regex 2026.9.29, continuations up to 4 characters) agrees with the argument written above
    // it. Matrix: .scratch/s1021/d101/MATRIX.md.

    // D101, a condition's test. Every match ends with 'a' or 'b' right after its 'x', then `\Z`.
    // 'xxy' has neither after any of its x's, so no start inside the text completes, and the
    // leftmost completable start is the edge, 3 ('xxy' + 'xb'). The port answers P(1, 3), and
    // `(x)(?(?=y?(?:x))a|b)\Z` answers P(3, 3).
    [Test]
    public void A_run_out_inside_a_call_a_condition_test_opened_answers_as_written_out()
    {
        Match m = new FuzzyRegex(@"(x)(?(?=y?(?1))a|b)\Z").Match("xxy", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 3, 0));
    }

    // D101, a positive lookahead inside an atomic group. The lookahead needs an 'x' at 1 and `\Z`
    // needs the text to end at 1: no text has both. The port answers P(0, 1), and
    // `(x)(?>(?=(?:x)))\Z` answers None.
    [Test]
    public void A_run_out_inside_a_call_a_lookahead_in_an_atomic_group_opened_answers_as_written_out()
    {
        new FuzzyRegex(@"(x)(?>(?=(?1)))\Z").MatchAtStart("x", partial: true).Success.Should().BeFalse();
    }

    // D101, a positive lookahead in a pattern with a verb. As above, an 'x' at 1 and the text's
    // end at 1 cannot both hold. The port answers P(0, 1); `(x)(?=(?:x))(*PRUNE)\Z` answers None.
    [Test]
    public void A_run_out_inside_a_call_in_a_pattern_with_a_verb_answers_as_written_out()
    {
        new FuzzyRegex(@"(x)(?=(?1))(*PRUNE)\Z").MatchAtStart("x", partial: true).Success.Should().BeFalse();
    }

    // D101, a fuzzy pattern, the call in a plain positive lookahead. The group sits outside the
    // section, so a match starts at an 'x'; from 0 the section must end the text at 1, or at 2
    // with its one error spent on an insertion, and 'xy' + any continuation is at least 3 long.
    // Start 1 is a 'y'. So the anchored call has no completion and the search's leftmost
    // completable start is the edge ('xy' + 'xx' matches from 2). The port answers P(0, 2) to
    // both; `(x)(?:(?=(?:x))\Z){e<=1}` answers None and P(2, 2).
    [Test]
    public void A_run_out_inside_a_call_in_a_fuzzy_pattern_answers_as_written_out()
    {
        var regex = new FuzzyRegex(@"(x)(?:(?=(?1))\Z){e<=1}");

        regex.MatchAtStart("xy", partial: true).Success.Should().BeFalse();
        Match m = regex.Match("xy", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }

    // D101, matrix k43's shape: a fuzzy pattern with a call that the lookahead's body never opens.
    // The first branch is the row above written out, and the second needs a 'q' at the start,
    // which neither 0 nor 1 has. The port answers P(0, 2) to both, because every fuzzy pattern
    // with a call takes the SHORTCUT (Matcher.ACallOpenedInTheBodyIsOpenOrUnmodelled); without the
    // second branch, or with it written out as `q(?:x)`, it answers None and P(2, 2).
    [Test]
    public void A_run_out_in_a_fuzzy_pattern_with_a_call_the_body_never_opens_answers_as_written_out()
    {
        var regex = new FuzzyRegex(@"(x)(?:(?=(?:x))\Z){e<=1}|q(?1)");

        regex.MatchAtStart("xy", partial: true).Success.Should().BeFalse();
        Match m = regex.Match("xy", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }
}
