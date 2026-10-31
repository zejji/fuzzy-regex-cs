using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101, option B2': a run-out inside a group call that a positive condition test or a positive
/// lookahead opened, where the call stays a call (a recursion), waits for the body's other paths
/// as a run-out in a call-free body does (<c>Matcher.WaitsAcrossCalls</c>), so the construct is
/// decided by a success on the held text or goes on undetermined. Main and B1 answered the partial
/// at the attempt's start (were <c>OpenDefectTests</c>).
/// </summary>
public sealed class PartialPositiveCallRunOutTests
{
    // A condition's test. After r takes the x's the test reads 'y' at 1 and then needs an 'x'; if
    // it holds the next item is 'a' at 1, otherwise 'b' at 1, and 1 is the 'y' either way: no text
    // completes a match from 0. Main and B1 answered P(0, 2); `(?<r>x+)(?(?=y?x+)a|b)` answers None.
    [Test]
    public void A_run_out_inside_a_recursive_call_a_condition_test_opened_answers_as_written_out()
    {
        new FuzzyRegex(@"(?<r>x(?&r)?)(?(?=y?(?&r))a|b)").MatchAtStart("xy", partial: true).Success.Should().BeFalse();
    }

    // The (?R) grid's positive constructs (MATRIX.md; the judge, d11-brute-judge.py on regex
    // 2026.9.29, ran on the call form). The condition: each match is 'x' then 'a' or 'b' then the
    // end, and 'xx' has neither at 1, so no text completes it from 0, and 'xx' + 'b' completes it
    // from 1. The atomic group: the lookahead needs an 'x' at 1 and `\Z` needs the end
    // at 1. Main and B1 answered P(0, 2) and P(0, 1); the judge None and None (and P(1, 2) for the
    // condition's search).
    [Test]
    [Arguments("match", @"x(?(?=(?R)?y)a|b)\Z", "xx", -1, -1)]
    [Arguments("search", @"x(?(?=(?R)?y)a|b)\Z", "xx", 1, 2)]
    [Arguments("match", @"x(?>(?=(?R)?y))\Z", "x", -1, -1)]
    public void A_run_out_inside_a_recursion_a_positive_deciding_body_opened_answers_as_the_judge(
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
}
