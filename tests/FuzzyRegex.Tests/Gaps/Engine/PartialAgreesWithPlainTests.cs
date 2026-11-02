using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D131: a partial call answers a complete match only if the same call without partial finds it.
/// </summary>
/// <remarks>
/// <para>
/// A partial call runs a complete pass first and a partial pass only when that finds nothing
/// (<c>Matcher.DoMatch</c>, as upstream's <c>do_match</c>), so a complete answer from the partial pass is
/// one the complete pass missed. Before D133 a committing construct (lookahead, atomic group,
/// possessive repeat, <c>(*PRUNE)</c>, <c>(*SKIP)</c>) committed to a body path an enclosing section's
/// maximum forbids, and the plain call failed; the partial pass's run-out took another path, and its
/// success came back complete, tripping design assert A1 in Debug ("a second-pass success read the
/// text to come"). D133 checks every enclosing maximum when an error is made, so the plain call now
/// finds that path itself and both calls agree.
/// </para>
/// <para>
/// Measured 2026-11-02 over the D133 grid (6,860 pattern and text pairs, <c>.scratch/s1101/d133</c>) and a
/// grid of lookaround, condition and verb forms (3,780 pairs, <c>.scratch/s1101/d131p</c>): main before
/// D133 (965417f7, Release) gave a partial-call complete answer the plain call did not on 150 pairs,
/// main with D133 on none, in Release and Debug (no assert). Each row below was one of the 150. The
/// expected answers are the eager model's (<c>tools/probes/fuzzy-reference-matcher.py</c> with
/// <c>EAGER_MAXIMA</c>) for the rows it supports; for the lookahead rows, the body deletes the 'a'
/// and matches the 'b' (in the third, <c>c?</c> then substitutes the 'a' after it), within both
/// sections.
/// </para>
/// </remarks>
public sealed class PartialAgreesWithPlainTests
{
    /// <summary>The partial call's answer, at the start and searching, is the plain call's, and complete.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The text.</param>
    /// <param name="start">The match's start.</param>
    /// <param name="end">The match's end.</param>
    /// <param name="substitutions">Its substitutions.</param>
    /// <param name="deletions">Its deletions.</param>
    [Test]
    [Arguments(@"(?:(?=(?:ab){e<=2})){d<=1}", "b", 0, 0, 0, 1)] // the D131 witness
    [Arguments(@"(?:(?=(?:ab){e<=2})b?){d<=1}", "b", 0, 1, 0, 1)]
    [Arguments(@"(?:(?=(?:ab){e<=2}c?)){s<=1,d<=1}", "ba", 0, 0, 1, 1)]
    [Arguments(@"(?:(?:ab){i<=1,d<=1}(*SKIP)c?){d<=1}", "ba", 0, 1, 0, 1)]
    [Arguments(@"(?:(?:ab){e<=2}(*PRUNE)){d<=1}", "bb", 0, 1, 0, 1)]
    [Arguments(@"(?:(?:ab){e<=2}(*PRUNE)){d<=1,i<=1}", "b", 0, 1, 0, 1)]
    [Arguments(@"(?:(?:(?:ab){e<=2})?+){d<=1}", "b", 0, 1, 0, 1)]
    [Arguments(@"(?:a{e<=1}b(*PRUNE)){d<=1}", "bb", 0, 1, 0, 1)]
    public void A_partial_call_answers_the_complete_match_the_plain_call_finds(
        string pattern,
        string text,
        int start,
        int end,
        int substitutions,
        int deletions
    )
    {
        var regex = new FuzzyRegex(pattern);
        var expected = (true, false, start, end, new FuzzyCounts(substitutions, 0, deletions));

        foreach (
            Match m in new[]
            {
                regex.MatchAtStart(text),
                regex.MatchAtStart(text, partial: true),
                regex.Match(text),
                regex.Match(text, partial: true),
            }
        )
        {
            (m.Success, m.PartialMatch, m.Index, m.Index + m.Length, m.FuzzyCounts).Should().Be(expected);
        }
    }
}
