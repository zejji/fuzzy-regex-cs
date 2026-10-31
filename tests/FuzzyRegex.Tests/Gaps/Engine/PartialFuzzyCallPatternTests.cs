using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101, B3a: in a fuzzy pattern that keeps a group call, a run-out in a lookaround or test that no
/// call can run is taken as in a pattern without calls (<c>PatternObject.MarkWhatACallCanRun</c>,
/// <c>Matcher.ACallOpenedInTheBodyIsOpenOrUnmodelled</c>). Until B3a every fuzzy pattern with a call
/// answered such a run-out at the attempt's start (matrix k43; was <c>OpenDefectTests</c>). A
/// construct a call can run keeps that answer: its section depends on the call site.
/// </summary>
public sealed class PartialFuzzyCallPatternTests
{
    // Matrix k43's shape with the call left in place by a recursion in a branch the text never
    // enters: every fuzzy pattern that keeps a call takes the SHORTCUT, whether or not the body
    // opened one (Matcher.ACallOpenedInTheBodyIsOpenOrUnmodelled). The first branch is the row above
    // with no call at all. Main answered P(0, 2) to both; without the second branch, None and P(2, 2).
    [Test]
    public void A_run_out_in_a_fuzzy_pattern_that_keeps_a_call_the_body_never_opens_answers_as_written_out()
    {
        var regex = new FuzzyRegex(@"(x)(?:(?=(?:x))\Z){e<=1}|q(?<r>a(?&r)?)");

        regex.MatchAtStart("xy", partial: true).Success.Should().BeFalse();
        Match m = regex.Match("xy", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }
}
