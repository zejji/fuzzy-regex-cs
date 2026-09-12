using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S30's <c>CALL_REF</c>, <c>GROUP_CALL</c> and <c>GROUP_RETURN</c>: what a call does
/// to the caller's group spans and to its capture lists, which the ported suite asserts only
/// through <c>(?(DEFINE)...)</c> patterns; the recursion depth a <c>(?R)</c> can reach, which no
/// ported test measures; and the direction bug upstream issue 614 is about.
/// </summary>
/// <remarks>
/// Every expected value below was probed against <c>regex</c> 2026.7.19 on 2026-09-11 and is quoted
/// beside the assertion as Python spells it.
/// </remarks>
public sealed class GroupCallTests
{
    [Test]
    public void A_call_leaves_its_capture_behind_but_restores_the_callers_current_span()
    {
        // This is the split GROUP_RETURN creates and nothing else in the engine does: 'pop_groups'
        // (upstream/src/_regex.c:2662) restores each group's 'current' from the caller's saved copy
        // and never touches its 'count', so a called group's capture stays on the list while the
        // span the group reports goes back to whatever the caller had.
        //
        // Measured: for m = regex.search(r'(?<x>a)(?&x)*', 'aaa'),
        // m.span() == (0, 3), m.group('x') == 'a', m.captures('x') == ['a', 'a', 'a'].
        Match m = FuzzyRegex.Match("aaa", "(?<x>a)(?&x)*");

        (m.Index, m.Length).Should().Be((0, 3));
        m.Groups["x"].Value.Should().Be("a");
        m.Groups["x"].Captures.Select(static c => c.Value).Should().Equal("a", "a", "a");
    }

    [Test]
    public void A_group_that_only_ever_ran_inside_a_call_reports_no_span_but_keeps_its_captures()
    {
        // The same split seen from the other side, and without a '(?(DEFINE)...)' wrapper: the
        // group is called before it is reached, the call captures, and then the optional group
        // itself never runs - so 'current' is back to -1 while the capture list holds one entry.
        // Upstream reports the two separately, because 'match_get_group_by_index' (:18847) consults
        // 'current' where 'match_get_captures_by_index' (:19137) walks 'count'.
        //
        // Measured: for m = regex.search(r'(?&x)(?<x>a)?', 'a'),
        // m.group('x') is None and m.captures('x') == ['a'].
        Match m = FuzzyRegex.Match("a", "(?&x)(?<x>a)?");

        m.Success.Should().BeTrue();
        m.Groups["x"].Success.Should().BeFalse();
        m.Groups["x"].Captures.Select(static c => c.Value).Should().Equal("a");
    }

    [Test]
    public void A_called_group_matches_again_from_where_the_caller_left_off()
    {
        // A greedy body inside the called group consumes everything on the first pass, so the call
        // matches empty - which it is allowed to do, and which pins that the call resumes at the
        // caller's text position rather than at the group's own.
        //
        // Measured: for m = regex.search(r'(?<x>a*)(?&x)', 'aaa'),
        // m.span() == (0, 3) and m.captures('x') == ['aaa', ''].
        Match m = FuzzyRegex.Match("aaa", "(?<x>a*)(?&x)");

        (m.Index, m.Length).Should().Be((0, 3));
        m.Groups["x"].Captures.Select(static c => c.Value).Should().Equal("aaa", "");
    }

    [Test]
    public void Whole_pattern_recursion_nests_ten_thousand_deep_without_a_stack_overflow()
    {
        // Upstream recurses on its own heap stacks rather than on the C stack, and GROUP_CALL is
        // ported the same way - it reassigns 'node' and pushes to the ByteStack, so a deep '(?R)'
        // costs heap and not .NET stack frames. A matcher that called itself would take a
        // StackOverflowException here, which .NET cannot catch and which kills the process.
        //
        // Measured: regex.match(r'\((?:[^()]|(?R))*\)', '(' * 10000 + ')' * 10000).span()
        // == (0, 20000).
        string subject = new string('(', 10000) + new string(')', 10000);

        Match m = FuzzyRegex.MatchAtStart(subject, @"\((?:[^()]|(?R))*\)");

        (m.Index, m.Length).Should().Be((0, 20000));
    }

    [Test]
    public void A_group_called_from_inside_a_lookbehind_agrees_with_upstream_where_the_call_is_wider_than_the_text()
    {
        // Upstream issue 614's area: a group reached through a call from inside a lookbehind. Both
        // of these fail to match upstream and fail here, so the port reproduces them.
        //
        // Measured: both are None under upstream.
        //   regex.search(r'(?(DEFINE)(?<ab>ab))..(?<=(?&ab))', 'ab')
        //   regex.search(r'(?<x>ab)(?<=(?&x))', 'ab')
        FuzzyRegex.Match("ab", "(?(DEFINE)(?<ab>ab))..(?<=(?&ab))").Success.Should().BeFalse();
        FuzzyRegex.Match("ab", "(?<x>ab)(?<=(?&x))").Success.Should().BeFalse();

        // And where the lookbehind is the whole pattern, both engines match:
        // regex.match(r'(?(DEFINE)(?<func>.)).(?<=(?&func))', 'abc').captures('func') == ['a'].
        FuzzyRegex
            .MatchAtStart("abc", "(?(DEFINE)(?<func>.)).(?<=(?&func))")
            .Groups["func"]
            .Captures.Select(static c => c.Value)
            .Should()
            .Equal("a");
    }

    [Test]
    public void A_group_called_from_a_lookbehind_with_anything_after_it_matches_here_and_not_upstream()
    {
        // KNOWN DIVERGENCE, and the only one S30 found. Upstream matches a lookbehind containing a
        // '(?&name)' call only when that lookbehind is the entire pattern; put any other node in the
        // sequence, before it or after it, and upstream fails where this port matches. Minimised:
        //
        //     regex.compile(r'(?(DEFINE)(?<a>a))(?<=(?&a))c').match('ac', pos=1)   # None
        //
        // and here it is (1, 2), which is the answer 'a' precedes 'c' asks for.
        //
        // Upstream is internally inconsistent about it in two separate ways, which is what says the
        // defect is upstream's rather than this port's. All measured against regex 2026.7.19 on
        // 2026-09-11, every compile with cache_pattern=False so none of it is a cache artefact.
        //
        // First, upstream's own search and match disagree at the same position. For
        // r'(?(DEFINE)(?<ab>ab))(?<=(?&ab))c' on 'abcd', search finds (2, 3) with
        // captures('ab') == ['ab'] while match(pos=2) on that same compiled pattern returns None.
        //
        // Second, making the tail optional makes upstream match the very text it had just refused.
        // With base = r'(?(DEFINE)(?<a>a))(?<=(?&a))' and match('ac', pos=1), the tails 'c?' and 'c*'
        // both return (1, 2) - they consume the 'c' - while 'c', 'c+', '[c]' and '(?:c)' all return
        // None. An engine that can match 'c' there cannot consistently refuse to.
        //
        // Two explanations are ruled out. It is not our parser: this port's bytecode for the pattern
        // is upstream's, code for code, modulo the order of two characters inside a SET_UNION member
        // that tools/record-compile-corpus.py sorts for determinism - and upstream's own dump carries
        // the reversed copy of the called group ('CALL_REF 0, GROUP 0 1 1, CHARACTER_REV b,
        // CHARACTER_REV a, END, END'), so the direction did reach the bytecode. And it is not the
        // required-string prefilter this port does not implement: switching upstream's off the way
        // tools/record-oracle.py does for 'verbs' leaves the answer None.
        //
        // NOT COVERED BY ISSUE 614's FIX, and S34's notes assumed it was. Re-run on 2026-09-12
        // against regex 2026.9.10 - the newest release, which carries commit 9398a6d - the same
        // pattern is still None: regex.compile(r'(?(DEFINE)(?<a>a))(?<=(?&a))c').match('ac', pos=1).
        // So the Phase 6 sync will not make this go away, and nothing here should say it is waiting
        // for one.
        //
        // Nothing is filed upstream. Phase 6 owns the open-issue sweep, this sits next to issue 614
        // in it, and the rule is that the owner approves the report text first.
        FuzzyRegex
            .Match("ac", "(?(DEFINE)(?<a>a))(?<=(?&a))c")
            .Success.Should()
            .BeTrue();
        FuzzyRegex.Match("abcd", "(?(DEFINE)(?<ab>ab))(?<=(?&ab))cd").Success.Should().BeTrue();
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_group_called_from_a_lookahead_under_reverse_matches_forwards_here()
    {
        // The mirror image of the test above, found by S33's blind review at seed 4242 and judged in
        // S34: a group called from a LOOKAHEAD inside a reversed pattern. The lookahead runs forward
        // - upstream agrees, and says so itself wherever the called body is not a variable repeat -
        // but upstream ran the called body backwards, and then recorded the span it had walked
        // without swapping its ends. All measured against regex 2026.7.19 on 2026-09-12,
        // tools/probes/upstream-reversed-group-call.py.
        //
        //   regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('abbaa').spans('g')
        //   # [(2, 1), (0, 2)] - the first span's END PRECEDES ITS START, and upstream renders it
        //   #                    as the empty string, which '[ab]+' cannot match
        //
        // Upstream's own inline copy of the called body answers what this port answers:
        //
        //   regex.compile(r'(?r)(?<g>[ab]+)(?=([ab]+))b').search('abbaa')   # g2 == [(2, 5)] 'baa'
        //
        // and a fixed-count body through the call is right too, which is what places the fault in
        // the repeat rather than in the lookahead: '(?r)(?<g>[ab]{2})(?=(?&g))b' records (2, 4).
        //
        // ALREADY FIXED UPSTREAM, unlike the lookbehind case above, so there is nothing to report:
        // this is issue 614, `build_GROUP()` not propagating the match direction, fixed on
        // 2026-08-30 by commit 9398a6d - one line, `subargs.forward = forward;` - and released in
        // 2026.8.30, which is past the version this oracle records against. The Phase 6 sync is
        // where upstream stops diverging here, and the strict divergence list is what must notice:
        // the oracle entry is `reverse-group-call-direction`.
        Match reversed = new FuzzyRegex("(?r)(?<g>[ab]+)(?=(?&g))b").Match("abbaa");

        (reversed.Index, reversed.Index + reversed.Length).Should().Be((0, 3));
        reversed
            .Groups["g"]
            .Captures.Select(static c => (c.Index, c.Index + c.Length))
            .Should()
            // The call's capture is the forward '[ab]+' upstream's own inline copy also finds.
            .Equal((2, 5), (0, 2));

        // The same pattern with the call written out, which upstream and this port agree on. Without
        // it the assertion above would be this port marking its own homework.
        Match inlined = new FuzzyRegex("(?r)(?<g>[ab]+)(?=([ab]+))b").Match("abbaa");

        (inlined.Groups["g"].Index, inlined.Groups["g"].Length).Should().Be((0, 2));
        (inlined.Groups[2].Index, inlined.Groups[2].Length).Should().Be((2, 3));

        // And the shapes upstream gets right through the call, so this is a statement about the
        // variable repeat and not about group calls under '(?r)' in general.
        new FuzzyRegex("(?r)(?<g>[ab]{2})(?=(?&g))b")
            .Match("abbaa")
            .Groups["g"]
            .Captures.Select(static c => (c.Index, c.Index + c.Length))
            .Should()
            .Equal((2, 4), (0, 2));
        new FuzzyRegex("(?r)(?<g>[ab])(?=(?&g))b")
            .Match("abb")
            .Groups["g"]
            .Captures.Select(static c => (c.Index, c.Index + c.Length))
            .Should()
            .Equal((2, 3), (1, 2));
    }

    // NOT TESTED, deliberately: left recursion. '(?R)?b' against 'b' and '(?<x>(?&x)?a)' against
    // 'aaa' both recurse without consuming, and neither engine guards against it - upstream grows
    // its stack until re_alloc fails and raises MemoryError, and this port grows the ByteStack until
    // it hits the 1GB RE_MEMORY_LIMIT and throws
    // InvalidOperationException("the regular expression engine's backtracking stack exceeded its 1GB
    // limit") from ByteStack.Grow. So the behaviour is bounded and matches upstream's shape, but
    // asserting it costs a 1GB allocation and several seconds on every ratchet run, which is a poor
    // trade against a suite that runs in ten.
    //
    // Ceiling: nothing here proves the limit still bites if a future slice changes the per-call
    // push. Upgrade path: assert it in the Phase 6 hardening pass, where a long-running,
    // memory-hungry test already has a home, or give ByteStack an injectable limit so the same
    // assertion costs a kilobyte.
}
