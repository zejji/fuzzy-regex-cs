using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S27's <c>LOOKAROUND</c> and <c>END_LOOKAROUND</c>: what a lookaround does to the
/// captures its body made, and what it does to the text position - the two pieces of state the
/// opcode saves and restores, and the two an opcode-by-opcode port gets wrong on the backtrack path
/// while passing every simple test.
/// </summary>
/// <remarks>
/// Every expected value below was probed against <c>regex</c> 2026.7.19 on 2026-09-11 and is quoted
/// beside the assertion as Python spells it.
/// </remarks>
public sealed class LookaroundTests
{
    [Test]
    public void A_capture_made_inside_a_positive_lookahead_is_visible_after_it()
    {
        // Upstream: regex.search(r'(?=(a))\1', 'aa').span() == (0, 1) with group(1) == 'a'. The
        // lookahead consumes nothing, so the backreference reads the 'a' the lookahead captured and
        // matches it at position 0. A port that discarded the body's captures on the way out of
        // END_LOOKAROUND would fail this outright.
        Match m = FuzzyRegex.Match("aa", @"(?=(a))\1");

        (m.Index, m.Length).Should().Be((0, 1));
        m.Groups[1].Value.Should().Be("a");
    }

    [Test]
    public void A_capture_made_inside_a_positive_lookbehind_is_visible_after_it()
    {
        // Upstream: regex.search(r'(?<=(a))b', 'ab').span() == (1, 2) with group(1) == 'a'. The
        // lookbehind's body is compiled reversed, but what it captures survives it just as a
        // lookahead's does.
        Match m = FuzzyRegex.Match("ab", @"(?<=(a))b");

        (m.Index, m.Length).Should().Be((1, 1));
        m.Groups[1].Value.Should().Be("a");
    }

    [Test]
    public void A_capture_made_inside_a_negative_lookahead_is_discarded_when_the_assertion_fails()
    {
        // Upstream: regex.search(r'(?:(?!(a))b|c)', 'ac').span() == (1, 2) with group(1) None. At
        // position 0 the body matches the 'a' and captures it, which makes the *negative* assertion
        // fail; the match is found by the other branch, and the capture must not survive. The pop
        // that does it is END_LOOKAROUND's negative arm, though the enclosing BRANCH's own restore
        // covers this particular shape too: measured 2026-09-11, control S27-B's mutation is caught
        // by six ported tests but not by this one, which pins the rule rather than the mechanism.
        Match m = FuzzyRegex.Match("ac", @"(?:(?!(a))b|c)");

        (m.Index, m.Length).Should().Be((1, 1));
        m.Value.Should().Be("c");
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void A_capture_made_inside_a_positive_lookahead_is_discarded_when_the_match_backtracks_past_it()
    {
        // Upstream: regex.search(r'(?:(?=(a))b|c)', 'ac').span() == (1, 2) with group(1) None. The
        // assertion *succeeds* at position 0 and captures the 'a'; the branch then fails on 'b' and
        // the engine backtracks past the whole lookahead. This is the END_LOOKAROUND backtrack arm -
        // the half that a port restoring captures only on the success path leaves undone, and which
        // no test that never backtracks past a lookaround can see.
        Match m = FuzzyRegex.Match("ac", @"(?:(?=(a))b|c)");

        (m.Index, m.Length).Should().Be((1, 1));
        m.Value.Should().Be("c");
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void A_negative_lookahead_whose_body_consumed_before_failing_resumes_where_it_started()
    {
        // Upstream: regex.search(r'(?!ab)a.', 'ax').span() == (0, 2). The body matches the 'a',
        // moving the text position to 1, and then fails on 'b'; the assertion therefore succeeds,
        // and must carry on from 0 rather than from 1. A single-atom body can never show this,
        // because it fails without having moved - which is why the oracle generator emits
        // multi-atom lookaround bodies (control S27-A).
        Match m = FuzzyRegex.Match("ax", @"(?!ab)a.");

        (m.Index, m.Length).Should().Be((0, 2));
        m.Value.Should().Be("ax");
    }

    [Test]
    public void An_empty_negative_lookahead_never_matches()
    {
        // Upstream: regex.search(r'(?!)', 'abc') is None and regex.search(r'a(?!)|b', 'ab').span()
        // == (1, 2). '(?!)' compiles to a LOOKAROUND with an empty body, which always succeeds, so
        // the negative assertion always fails - upstream's own way of spelling "fail here".
        FuzzyRegex.Match("abc", "(?!)").Success.Should().BeFalse();

        Match alternative = FuzzyRegex.Match("ab", "a(?!)|b");

        (alternative.Index, alternative.Length).Should().Be((1, 1));
    }

    [Test]
    public void A_lookbehind_at_the_start_and_a_lookahead_at_the_end_read_nothing_rather_than_past_the_string()
    {
        // Upstream: regex.search(r'(?<=a)b', 'b') is None, regex.search(r'(?<!a)b', 'b').span() ==
        // (0, 1), regex.search(r'a(?=b)', 'a') is None and regex.search(r'a(?!b)', 'a').span() ==
        // (0, 1). The body runs out of text rather than reading past either end.
        FuzzyRegex.Match("b", "(?<=a)b").Success.Should().BeFalse();
        FuzzyRegex.Match("b", "(?<!a)b").Success.Should().BeTrue();
        FuzzyRegex.Match("a", "a(?=b)").Success.Should().BeFalse();
        FuzzyRegex.Match("a", "a(?!b)").Success.Should().BeTrue();
    }

    [Test]
    public void A_lookaround_sees_outside_the_slice_the_search_is_confined_to()
    {
        // Upstream: regex.compile(r'(?<=a)b').search('ab', 1).span() == (1, 2). The search starts at
        // 1, so the 'a' is outside the slice - and the lookbehind must still see it. That is what
        // the LOOKAROUND arm's widening of slice_start/slice_end to text_start/text_end is for, and
        // a port that left the slice alone would answer None here.
        var pattern = new FuzzyRegex("(?<=a)b");
        Match m = pattern.Match("ab", 1);

        (m.Index, m.Length).Should().Be((1, 1));
    }
}
