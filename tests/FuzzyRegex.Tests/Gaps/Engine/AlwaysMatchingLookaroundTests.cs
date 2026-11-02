using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101: a lookaround whose body can match the empty string without reading anything (no anchor,
/// lookaround, backreference, condition, call, verb, <c>\K</c> or fuzzy section on that path, and no
/// verb or <c>\K</c> anywhere in the body) succeeds at every position whatever the text, so
/// a negative one is folded to <c>(*FAIL)</c> and a positive one with no capture group to nothing
/// (<c>Parsing.LookaroundFolder</c>). The partial second pass then has nothing to search there.
/// </summary>
public sealed class AlwaysMatchingLookaroundTests
{
    /// <summary>
    /// The review's two rows (REVIEW-0030): `(?R)?` matches empty, so the inner negative always
    /// fails and the outer always holds; the patterns are `(?:x|z)+q` and `(?:x)*q`. Main's SHORTCUT
    /// took 203 steps over 'x' * 40; fix/d101 before the fold 12,340 and 3,883 (negative memo), the
    /// same 'x' * 40 under `(?:x|z)+q` about 160.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?))|z)+q")]
    [Arguments(@"(?:x(?!(?!(?R)?)))*q")]
    public void A_negative_lookaround_whose_body_always_matches_is_decided_at_compile_time(string pattern)
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
            400,
            "the lookarounds are folded away"
        );
    }

    /// <summary>
    /// Each exclusion's witness: folding would change the answer.
    /// <list type="bullet">
    /// <item>A capture group in a positive body: its first path sets group 1 where 'a' follows.</item>
    /// <item>A verb in the body: the first path passes `(*PRUNE)` and fails on 'b', and the verb then
    /// fails the attempt (ledger entry 47), so there is no match at 0; folded, `a` would match.</item>
    /// <item>An anchor on the empty path: `\b` does not hold between two letters.</item>
    /// <item>A fuzzy section around the lookaround: kept, since a failed lookaround in a section is
    /// offered an insertion in front of it (ledger entry 50).</item>
    /// </list>
    /// </summary>
    [Test]
    public void A_body_with_a_group_a_verb_or_an_anchor_is_not_folded()
    {
        new FuzzyRegex(@"(?=(a)?)\w").Match("a").Groups[1].Success.Should().BeTrue();
        new FuzzyRegex(@"(?=(?:a(*PRUNE)b)?)a").Match("ac").Success.Should().BeFalse();
        new FuzzyRegex(@"a(?!\b?)\w").Match("ab").Success.Should().BeFalse();
        new FuzzyRegex(@"a(?!\b)\w").Match("ab").Success.Should().BeTrue();
    }

    /// <summary>
    /// Two more limits, each found by the corpus grid (2026-10-25). A body that defines a group is
    /// kept whatever its sense, since the group is numbered and called elsewhere: folded away, this
    /// pattern threw IndexOutOfRangeException at match time. And the left-recursion refusal is read
    /// on the tree as written: folding the lookbehind removed the only call, and the pattern compiled.
    /// </summary>
    [Test]
    public void A_body_that_defines_a_group_or_makes_the_pattern_left_recursive_keeps_its_effect()
    {
        Match m = new FuzzyRegex(@"a+(?<!(?1)(?P<g1>a*))").Match("baa", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 1, 2));

        Action compile = static () => _ = new FuzzyRegex(@"\w(?<!(?:b(?R)?)*+)");
        compile.Should().Throw<FuzzyRegexParseException>();
    }

    /// <summary>
    /// The fold walks the whole parsed tree, so on a pattern without a lookaround it is pure compile
    /// cost: the BDN gate of 2026-10-31 found <c>CompileLargePattern</c> (300 alternatives, no
    /// lookaround) 1.06-1.08x slower than main, and the WPR profile put the fold, with its write of
    /// every child back into its list, at the top of the difference. A pattern the parser built no
    /// lookaround in is not walked at all; the second row, with one lookaround, shows the counter
    /// does count.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="walked">Whether the fold walks it.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:word000|word001|word002|word003)[0-9]{2,4}(?:[A-Za-z_]\w*)?\b", false)]
    [Arguments(@"(?:word000|word001|word002|word003)[0-9]{2,4}(?=\w)", true)]
    [Arguments(@"(?(?=a)ab|c)", true)]
    public void The_fold_walks_only_a_pattern_that_has_a_lookaround(string pattern, bool walked)
    {
        EngineWork.SkipUnlessCounting();
        long before = Fuzzy.Text.RegularExpressions.Engine.WorkCounter.LookaroundFoldVisits;

        _ = new FuzzyRegex(pattern);

        (Fuzzy.Text.RegularExpressions.Engine.WorkCounter.LookaroundFoldVisits - before != 0).Should().Be(walked);
    }
}
