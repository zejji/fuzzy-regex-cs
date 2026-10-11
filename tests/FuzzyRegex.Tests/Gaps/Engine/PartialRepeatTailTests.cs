using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D67: a repeat whose tail test runs out of text enters the tail, so the groups the tail closes
/// before its first item are closed in the partial. Upstream answers the partial at the test, so
/// <c>(?&lt;o&gt;b(?&lt;i&gt;a)?)b</c> over 'ba' reported o unset where every completion ('bab')
/// sets it to (0, 2). D62's rule decides it: a partial reports what every completion of its path
/// would report. See <c>Matcher.TailMayMatch</c>.
/// </summary>
/// <remarks>
/// The grid is <c>tools/probes/d67-repeat-tail-grid.py</c>: 51,200 rows (repeats inside a group,
/// greedy and lazy, one-character and longer bodies, forward and reversed, search and match),
/// judged against every completion of up to five characters. Before the fix 4,424 group values
/// disagreed with the completions' common value, after it none, and no span or partial flag moved
/// (2026-10-08). Upstream 2026.9.10 answers every row below with o unset.
/// </remarks>
public sealed class PartialRepeatTailTests
{
    /// <summary>
    /// One row per tail test site, in both directions. Each closes o before the tail's literal runs
    /// out, and every completion gives o that span. Turning only that site's change off turns its
    /// rows red (measured with a per-site switch, 2026-10-08).
    /// </summary>
    [Test]
    // END_GREEDY_REPEAT.
    [Arguments("(?<o>b(?<i>a)?)b", "ba", 0, 2, 0, 2)]
    [Arguments("(?r)b(?<o>(?<i>a)?)", "a", 0, 1, 0, 1)]
    // END_LAZY_REPEAT.
    [Arguments("(?<o>b(?<i>a)??)b", "ba", 0, 2, 0, 2)]
    [Arguments("(?r)b(?<o>(?<i>a)??)", "a", 0, 1, 0, 1)]
    // GREEDY_REPEAT, at the repeat's start.
    [Arguments("(?<o>b(?<i>a)?)bc", "bb", 0, 2, 0, 1)]
    [Arguments("(?r)cb(?<o>(?<i>a)?)", "b", 0, 1, 1, 1)]
    // LAZY_REPEAT, at the repeat's start.
    [Arguments("(?<o>b(?<i>a)??)bc", "bb", 0, 2, 0, 1)]
    [Arguments("(?r)cb(?<o>(?<i>a)??)", "b", 0, 1, 1, 1)]
    // GREEDY_REPEAT_ONE backing off: reached only after the tail ran out in a lookaround body.
    [Arguments("(?=(?<o>a*)abc)", "ab", 0, 2, 0, 0)]
    [Arguments("(?r)(?<=cba(?<o>a*))", "ba", 0, 2, 2, 2)]
    // LAZY_REPEAT_ONE taking more.
    [Arguments("(?<o>a??)b", "a", 0, 1, 0, 1)]
    [Arguments("(?r)b(?<o>a??)", "a", 0, 1, 0, 1)]
    public void A_group_closed_before_the_tail_runs_out_is_reported(
        string pattern,
        string text,
        int start,
        int end,
        int groupStart,
        int groupEnd
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        Group o = m.Groups["o"];
        (o.Success, o.Index, o.Index + o.Length).Should().Be((true, groupStart, groupEnd));
    }

    /// <summary>
    /// The control: where the body runs out first, o is still open on the partial's path, and its
    /// completions disagree ('bab' gives (0, 2), 'baab' gives (0, 3)), so o stays unset.
    /// </summary>
    [Test]
    public void A_group_the_body_has_not_closed_stays_unset()
    {
        Match m = new FuzzyRegex("(?<o>b(?<i>a)*)b").Match("ba", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
        m.Groups["o"].Success.Should().BeFalse();
    }

    /// <summary>
    /// An error allowance changes nothing: the group is still open on the partial's path ('abx' gives
    /// (1, 2), 'abcx' gives (1, 3)), so it stays unset with or without one. Upstream sets it to (1, 2)
    /// for <c>{s&lt;=1}</c> and <c>{d&lt;=0}</c> but not for the other two (ledger entry 79).
    /// </summary>
    [Test]
    [Arguments(@"(?:a(\w*))x")]
    [Arguments(@"(?:a(\w*)){e<=0}x")]
    [Arguments(@"(?:a(\w*)){s<=1}x")]
    [Arguments(@"(?:a(\w*)){d<=0}x")]
    public void An_unused_error_allowance_leaves_an_open_group_unset(string pattern)
    {
        Match m = new FuzzyRegex(pattern).Match("ab", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
        m.Groups[1].Success.Should().BeFalse();
    }

    /// <summary>
    /// The oracle's ablation gives upstream's answer back, and only the group moves: the span and
    /// the partial flag are the same either way. This is what the oracle's divergence entry keys on.
    /// </summary>
    [Test]
    public void The_ablation_gives_upstreams_answer_and_moves_only_the_group()
    {
        var ablated = new FuzzyRegex("(?<o>b(?<i>a)?)b");
        ablated.PatternObject.TailRunOutAsBeforeD67 = true;

        Match m = ablated.Match("ba", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
        m.Groups["o"].Success.Should().BeFalse();
        (m.Groups["i"].Index, m.Groups["i"].Length).Should().Be((1, 1));
    }

    /// <summary>
    /// The fix moves groups only: over a small grid of repeats inside a group, every row's span and
    /// partial flag equal the ablated answer's, and the groups differ on some partial rows (the grid
    /// reads the construct). Ordinary matches are untouched: a complete match equals the ablated
    /// one, groups included.
    /// </summary>
    [Test]
    public void Only_partial_groups_move_over_a_grid()
    {
        string[] bodies = ["a", "(?<i>a)", "(?:aa)", "(?<i>a|c)"];
        string[] quantifiers = ["?", "*", "+", "{0,2}", "??", "*?", "+?", "{1,2}?"];
        string[] subjects = ["", "a", "b", "aa", "ab", "ba", "bb", "aab", "bab", "bac"];
        int groupsMoved = 0;
        int rows = 0;

        foreach (string body in bodies)
        {
            foreach (string q in quantifiers)
            {
                foreach (string tail in new[] { "b", "bc" })
                {
                    foreach (
                        string pattern in new[] { $"(?<o>b{body}{q}){tail}", $"(?r){Reverse(tail)}(?<o>{body}{q}b)" }
                    )
                    {
                        var ours = new FuzzyRegex(pattern);
                        var ablated = new FuzzyRegex(pattern);
                        ablated.PatternObject.TailRunOutAsBeforeD67 = true;

                        foreach (string text in subjects)
                        {
                            foreach (bool partial in new[] { true, false })
                            {
                                rows++;
                                Match a = ours.Match(text, partial: partial);
                                Match b = ablated.Match(text, partial: partial);
                                string because = $"{pattern} over '{text}', partial {partial}";
                                (a.Success, a.PartialMatch, a.Index, a.Length)
                                    .Should()
                                    .Be((b.Success, b.PartialMatch, b.Index, b.Length), because);

                                bool sameGroups = string.Equals(Groups(a), Groups(b), StringComparison.Ordinal);
                                if (!partial || !a.PartialMatch)
                                {
                                    sameGroups.Should().BeTrue(because);
                                }
                                else if (!sameGroups)
                                {
                                    groupsMoved++;
                                }
                            }
                        }
                    }
                }
            }
        }

        rows.Should().Be(2560);
        groupsMoved.Should().BeGreaterThan(0);
    }

    private static string Reverse(string s) => new([.. s.Reverse()]);

    private static string Groups(Match m) =>
        string.Join(";", m.Groups.Values.Select(static g => g.Success ? $"{g.Index},{g.Length}" : "-"));
}
