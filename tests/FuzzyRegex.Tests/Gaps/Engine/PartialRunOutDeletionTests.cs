using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D124: a fuzzy item that runs out of text inside a negative lookaround's body, or a condition's
/// test, keeps its deletion, an error that needs no text (<c>Matcher.KeepTheDeletionOfAnItemThatRanOut</c>).
/// </summary>
/// <remarks>
/// <para>
/// A body that matches the empty string by deleting its items, such as <c>(?:x){d&lt;=1}</c>,
/// matches whatever follows the edge, so the negative lookaround fails for every continuation.
/// The run-out went on undecided and the deletion was never tried, so the run-out before the
/// lookaround was answered as a partial that no text completes: <c>(x)(?!(?:x){d&lt;=1})</c> over
/// 'xx' was P(1, 2), as upstream 2026.9.29 answers, where <c>(x)(?!x?)</c>, whose body also
/// matches empty, gives the search's edge partial P(2, 2).
/// </para>
/// <para>
/// The expected answers are the port's own: its non-partial answers over every continuation of up
/// to three characters (<c>.scratch/s1024/d124/g/judge.cs</c>), or the search's edge partial
/// (P(n, n) forward, P(0, 0) reversed) where no continuation completes, which the partial
/// contract allows. The comment on each row gives main's answer before the fix.
/// </para>
/// </remarks>
public sealed class PartialRunOutDeletionTests
{
    /// <summary>One witness per kind of item, forward and reversed.</summary>
    /// <param name="kind">The item kind the row reaches.</param>
    /// <param name="operation">search, match or fullmatch.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The text.</param>
    /// <param name="start">The partial's start, or -1 for no match.</param>
    /// <param name="end">The partial's end.</param>
    [Test]
    // The register's witness. Main: P(1, 2).
    [Arguments("character", "search", "(x)(?!(?:x){d<=1})", "xx", 2, 2)]
    // Main: P(0, 1) on every row down to the folded ones.
    [Arguments("character", "match", "(x)(?!(?:x){d<=1})", "x", -1, -1)]
    [Arguments("character", "fullmatch", "(x)(?!(?:x){d<=1})y", "x", -1, -1)]
    [Arguments("character, reversed", "search", "(?r)(?<!(?:x){d<=1})(x)", "x", 0, 0)]
    [Arguments("any", "search", "(x)(?!(?:.){d<=1})", "x", 1, 1)]
    [Arguments("set", "search", "(x)(?!(?:[xy]){d<=1})", "x", 1, 1)]
    [Arguments("property", "search", @"(x)(?!(?:\w){d<=1})", "x", 1, 1)]
    [Arguments("string", "search", "(x)(?!(?:xx){d<=2})", "x", 1, 1)]
    [Arguments("string, reversed", "search", "(?r)(?<!(?:xx){d<=2})(x)", "x", 0, 0)]
    [Arguments("string ignoring case", "match", "(?i)(x)(?!(?:xx){d<=2})", "x", -1, -1)]
    [Arguments("reference", "search", @"(x)(?!(?:\1){d<=1})", "x", 1, 1)]
    [Arguments("reference, reversed", "search", @"(?r)(?<!(?:\1){d<=1})(x)", "x", 0, 0)]
    [Arguments("condition's test", "search", "(x)(?(?!(?:x){d<=1})|(?!))", "x", 1, 1)]
    // Full case folding: the arm re-enters at the edge with an empty folding.
    [Arguments("folded string", "search", "(?fi)(x)(?!(?:ss){d<=2})", "x", 1, 1)]
    [Arguments("folded string, reversed", "search", "(?rfi)(?<!(?:ss){d<=2})(x)", "x", 0, 0)]
    [Arguments("folded string", "match", "(?fi)(x)(?!(?:ß){d<=2})", "x", -1, -1)]
    // Main: P(0, 2).
    [Arguments("folded reference", "match", @"(?fi)(ss)(?!(?:\1){d<=2})", "ss", -1, -1)]
    [Arguments("folded reference, reversed", "match", @"(?rfi)(?<!(?:\1){d<=2})(ss)", "ss", -1, -1)]
    // A captured ß is one deletion (D24's whole deletion of a group character). Main: P(1, 2) and
    // P(0, 1); 'ßß' + 'ssz' completes the search at 2.
    [Arguments("folded reference, whole character", "search", @"(?fi)(ß)(?!(?:\1){d<=1})", "ßß", 2, 2)]
    [Arguments("folded reference, whole character, reversed", "search", @"(?rfi)(?<!(?:\1){d<=1})(ß)", "ßß", 0, 0)]
    // Part way through a group character's folding: the first s of the ß deleted, then the second.
    // Main: P(1, 2).
    [Arguments("folded reference, inside a folding", "search", @"(?fi)(ß)(?!(?:\1){d<=2})", "ßß", 2, 2)]
    // The text ends after the group's ß, before its x: the x is the character the arm stands on,
    // not yet folded (the ß's folding is used up). 'ßxß' + 'xz' completes at 2. Main: P(0, 3).
    [Arguments("folded reference, between characters", "search", @"(?fi)(ßx)(?!(?:\1){d<=1})", "ßxß", 2, 3)]
    public void A_negative_body_that_deletes_its_way_to_empty_decides_at_the_edge(
        string kind,
        string operation,
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        _ = kind;
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(subject, partial: true),
            "fullmatch" => regex.FullMatch(subject, partial: true),
            _ => regex.Match(subject, partial: true),
        };

        if (start < 0)
        {
            m.Success.Should().BeFalse();
            return;
        }

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
    }

    /// <summary>
    /// Controls, which answered the same before the fix: the non-fuzzy twin, a body whose budget
    /// cannot empty it (the kept deletion spends the budget, so the text to come still decides),
    /// and the same pattern without <c>partial</c>.
    /// </summary>
    [Test]
    public void The_kept_deletion_keeps_to_the_budget_and_leaves_the_rest_alone()
    {
        // The twin without the fuzzy section, which the fixed answer now equals.
        Match twin = new FuzzyRegex("(x)(?!x?)").Match("xx", partial: true);
        (twin.Success, twin.PartialMatch, twin.Index, twin.Length).Should().Be((true, true, 2, 0));

        // One deletion leaves the y: 'x' + 'z' completes at 0.
        Match budget = new FuzzyRegex("(x)(?!(?:xy){d<=1})z").Match("x", partial: true);
        (budget.Success, budget.PartialMatch, budget.Index, budget.Length).Should().Be((true, true, 0, 1));

        // The same in a folded string: 'x' + 'z' completes at 0.
        Match folded = new FuzzyRegex("(?fi)(x)(?!(?:ss){d<=1})z").Match("x", partial: true);
        (folded.Success, folded.PartialMatch, folded.Index, folded.Length).Should().Be((true, true, 0, 1));

        new FuzzyRegex("(x)(?!(?:x){d<=1})").Match("xx").Success.Should().BeFalse();
    }

    /// <summary>
    /// A reference to a capture a run-out left unknown runs out where it stands, away from the
    /// edge, and keeps no deletion: it presumes the rest of the text (build chunk 8). The lookahead
    /// at 1 runs out with <c>a+</c> wanting more, so group 1 is unknown in the negative lookahead.
    /// </summary>
    [Test]
    public void A_reference_to_an_unknown_capture_keeps_no_deletion()
    {
        Match m = new FuzzyRegex(@"x(?=(a+))(?!(?:\1){d<=1}q)b").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }
}
