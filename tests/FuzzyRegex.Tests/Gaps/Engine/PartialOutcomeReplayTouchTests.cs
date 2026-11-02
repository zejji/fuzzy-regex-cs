using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101: a decision kept for the whole pass and replayed in a later attempt must leave the attempt
/// as its original evaluation did. In particular, it must set the flag that turns a second-pass
/// success into a partial (<c>MatchState.AttemptTouchedTheTextToCome</c>, reset at each attempt's
/// start). Found on paper by the blind review of the condition-test memo design (REVIEW-0320, F1
/// and F1b).
/// </summary>
/// <remarks>
/// Each row uses the frame `x(?1)q|(T)|(e)`. Attempt 0 runs the construct T at 1 through the call;
/// the read of the text to come happens inside T's body (a `\Z` decided false at the edge, or a
/// run-out). The attempt then fails on 'q'. Attempt 1 starts at 1 and meets T again: with the memo
/// it is replayed, and the branch after it succeeds on the held text. The first pass finds no
/// match, so that success must be a partial, and with the replay's flag missing it is reported
/// complete. Each row's answer with the memo is compared with the same pattern compiled with the
/// memo switched off, and the answer the memo-free run gives, P(1,3), is checked on its own (the review derived P(1,2); measured P(1,3)).
/// </remarks>
public sealed class PartialOutcomeReplayTouchTests
{
    private static (bool Success, bool Partial, int Index, int Length) Search(FuzzyRegex regex, string text)
    {
        Match m = regex.Match(text, partial: true);
        return (m.Success, m.PartialMatch, m.Index, m.Length);
    }

    /// <summary>The same pattern with every negative lookaround's outcome memo switched off.</summary>
    private static FuzzyRegex WithoutTheNegativeMemo(string pattern)
    {
        var regex = new FuzzyRegex(pattern);
        foreach (Node node in regex.PatternObject.NodeList)
        {
            node.MemoisesNegativeOutcome = false;
        }

        return regex;
    }

    /// <summary>
    /// W3 (REVIEW-0320 F1b), the existing negative-lookaround memo. At 1 in attempt 0, the lookahead's
    /// body `c.\Z` meets `\Z` at the edge, which is decided false but reads the text to come (it sets
    /// the flag), so the lookahead passes decided and is kept as such. In attempt 1 the replay skips
    /// the body, so the flag stays false, and `c` then succeeds over (1,2). With 'e' added, the body
    /// would match and the lookahead would fail, so the answer must be partial: P(1,3), as the memo-free run gives.
    /// </summary>
    [Test]
    public void A_replayed_negative_lookaround_leaves_the_attempt_touched()
    {
        // `(?:!(?2))?` keeps group 2 recursive, so CallInliner leaves the call in the body and the
        // lookahead is memoised. Measured 2026-10-26 on a4e2cc29: memo off P(1,3), memo on F(1,2) in
        // Release, and assert D11 A1 in Debug. Main 92bf4d78 has no such memo and answers P(1,3).
        const string pattern = @"x(?1)q|((?!c.\Z(?2)?)c)|(e(?:!(?2))?)";

        Search(WithoutTheNegativeMemo(pattern), "xcw").Should().Be((true, true, 1, 2));
        Search(new FuzzyRegex(pattern), "xcw").Should().Be((true, true, 1, 2));
    }

    /// <summary>
    /// W1, W2, W4 (REVIEW-0320 F1): the same shape with a condition's test. Today no condition test
    /// is memoised, so these pin today's answer, P(1,3), which the condition-test memo must keep.
    /// W1 is a negative test decided through `\Z`, W2 a blocked negative test (a run-out inside the
    /// call the body opened), and W4 a positive test whose run-out waits (B2').
    /// </summary>
    [Test]
    [Arguments(@"x(?1)q|((?(?!c.\Z(?2)?)c|d))|(e)")]
    [Arguments(@"x(?1)q|((?(?!c.(?2))d|c))|(e)")]
    [Arguments(@"x(?1)q|((?(?=c.(?2))c|d))|(e)")]
    public void A_replayed_condition_test_leaves_the_attempt_touched(string pattern) =>
        Search(new FuzzyRegex(pattern), "xcw").Should().Be((true, true, 1, 2));
}
