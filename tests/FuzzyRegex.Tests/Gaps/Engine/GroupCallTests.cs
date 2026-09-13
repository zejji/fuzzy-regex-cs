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

    // NO LONGER DIVERGES: upstream agrees with this port from 2026.8.30, and S44 moved the pin to
    // 2026.9.10. Kept as a plain regression test - see the closing note inside.
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
        // ALREADY FIXED UPSTREAM, unlike the lookbehind case above, so there was nothing to report:
        // this is issue 614, `build_GROUP()` not propagating the match direction, fixed on
        // 2026-08-30 by commit 9398a6d - one line, `subargs.forward = forward;` - and released in
        // 2026.8.30.
        //
        // AND THE SYNC HAS NOW HAPPENED. S44 moved the pin to 2026.9.10, and the strict divergence
        // list noticed exactly as it was built to: re-recording the oracle entry's example rows
        // turned the (2, 1) above into (2, 5), so `group-call-direction` was DELETED. This port's
        // answers below have not moved since S34 - upstream's came to meet them. The test stays,
        // because a test that pins the right answer is worth having whoever else agrees with it;
        // the version numbers quoted above are history now, not a live divergence.
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

    // NO LONGER DIVERGES: it diverged at the old pin only, and S44's sync moved the pin to
    // 2026.9.10, which gives this port's answer. Kept as a plain regression test.
    [Test]
    public void A_group_called_from_a_lookbehind_records_its_capture_inside_the_subject_here()
    {
        // The forward mirror of the test above, found by S36's composed `interactions` wave at seed 7
        // and the reason the oracle entry lost its `reverse-` prefix: issue 614 is the match direction
        // not reaching a called group, so it needs the call and the pattern to run opposite ways, and
        // '(?r)' plus a lookahead is only one way to arrange that. A lookbehind in an ordinary forward
        // pattern is the other.
        //
        //   regex.compile(r'(?P<g1>A*)(?<=(?&g1))').finditer('A')            # regex 2026.7.19
        //   # g1's captures are [(0, 1), (2, 1)] - a start PAST THE END of a one-character subject,
        //   # with an end before its own start
        //   # regex 2026.9.10 gives [(0, 1), (0, 1)], which is what this port has always given
        //
        // S44 pinned the oracle at 2026.9.10, so this row now AGREES and the divergence entry
        // `group-call-direction` is gone. Re-recording the entry's example is what caught it.
        //
        // Two things came out of that one row. The first is this assertion. The second is that the
        // recorder could not write the row down at all - `_to_index_length` indexed a two-entry
        // codepoint table with 2 and raised IndexError, so one upstream bug failed a whole 2000-row
        // wave; it now extends the index instead, and tools/record-oracle.py's `_utf16_index` says
        // why.
        MatchCollection matches = new FuzzyRegex("(?P<g1>A*)(?<=(?&g1))").Matches("A");

        matches.Select(static m => (m.Index, m.Length)).Should().Equal((0, 1), (1, 0));
        matches
            .SelectMany(static m => m.Groups["g1"].Captures.Select(static c => (c.Index, c.Length)))
            .Should()
            // Every capture inside the one-character subject, which is the whole claim.
            .Equal((0, 1), (0, 1), (1, 0), (0, 1));
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_zero_width_piece_holding_a_group_call_cannot_remove_a_match_here()
    {
        // The same defect as the lookbehind test above - a group reached by a call from a lookaround
        // running the other way round from the pattern - seen through the composed 'interactions'
        // wave, which drew four more rows of it at 6000 rows (S37). Upstream does not record a bad
        // capture here; it LOSES the match outright.
        //
        // UPSTREAM CONTRADICTS ITSELF ON EACH ROW, which is what places the defect there and needs no
        // second engine. In every one of them the piece that holds the call can match ZERO-WIDTH, so
        // deleting it cannot add a match - and deleting it is exactly what gives upstream the match
        // it had refused. All measured against regex 2026.7.19 on 2026-09-12 and re-run unchanged
        // against 2026.9.10, so issue 614's fix does not cover this either
        // (tools/probes/upstream-group-call-loses-matches.py).
        //
        //   pat = r'\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*'
        //   regex.finditer(pat, 'İİ\nİİﬁﬁ ', flags, overlapped=True)     # (0, 4) only
        //   regex.finditer(pat[:pat.index('(?:(?(2)')], ...)              # (0, 4) AND (3, 7)
        //
        // The trailing piece is a '*' repeat, so zero iterations is always available and the second
        // answer is the one that must also be the first's.
        MatchCollection forward = new FuzzyRegex(
            @"\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*",
            FuzzyRegexOptions.IgnoreCase
                | FuzzyRegexOptions.Multiline
                | FuzzyRegexOptions.Version1
                | FuzzyRegexOptions.FullCase
        ).Matches("İİ\nİİﬁﬁ ", overlapped: true);

        forward.Select(static m => (m.Index, m.Length)).Should().Equal((0, 4), (3, 4));

        // And the reversed arrangement, where the mismatching lookaround is a LOOKAHEAD. Upstream
        // finds nothing at all; drop the '{3}' piece and it answers (0, 1), which is this port's
        // answer with the piece present. That the piece is zero-width capable is upstream's own
        // statement: with the call replaced by the class it calls, upstream matches (0, 1) too - one
        // character for a pattern whose '{3}' would have to consume three if it were not empty.
        //
        //   regex.finditer(r'(?r)\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\S)){3}(\p{Nd}+?)?', 'AA..0',
        //                  regex.I | regex.M, overlapped=True)               # nothing
        //   ... with '(?&g1)' written out as '[A]'                           # (0, 1)
        MatchCollection reversed = new FuzzyRegex(
            @"(?r)\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\S)){3}(\p{Nd}+?)?",
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline
        ).Matches("AA..0", overlapped: true);

        reversed.Select(static m => (m.Index, m.Length)).Should().Equal((0, 1));

        // S40c's third row of the same shape, row 74396 of a 6000-row `interactions` wave at seed
        // 20260913. The wave drew it as a SUBSTITUTION, where the divergence reads as a count -
        // upstream replaced once, this port twice - and `finditer` is what says why: the second
        // replacement is a match upstream lost. The piece holding the call is a `*` repeat, so zero
        // iterations is always available and it cannot remove a match; delete it, or write the call
        // out as the class it calls, and upstream finds both. Measured 2026-09-13 on regex 2026.7.19.
        //
        //   (?r)\b\m(?P<g1>[𝔘a])(?:(?(1)(?=(?P>g1))\p{L}))*[a]*  over '𝔘𝔘\r\raa𐐨𐐨' (V1)
        //     upstream                     (4, 6)
        //     piece deleted, or inlined    (4, 6) AND (0, 1)   <- this port's answer either way
        string mathematicalU = char.ConvertFromUtf32(0x1D518);
        string deseretSmall = char.ConvertFromUtf32(0x10428);
        string subject = mathematicalU + mathematicalU + "\r\raa" + deseretSmall + deseretSmall;

        MatchCollection called = new FuzzyRegex(
            @"(?r)\b\m(?P<g1>[" + mathematicalU + @"a])(?:(?(1)(?=(?P>g1))\p{L}))*[a]*",
            FuzzyRegexOptions.Version1
        ).Matches(subject);

        // UTF-16: the two leading astral characters occupy 0..4, so upstream's (4, 6) in codepoints
        // is (6, 2) here and the match it loses is the first astral character at (0, 2).
        // Upstream finds only the first of these.
        called.Select(static m => (m.Index, m.Length)).Should().Equal((6, 2), (0, 2));

        // S43's row, row 10201 of a 6000-row seed-99991 `fuzzy,interactions` wave, and the STRONGEST
        // reproduction this family has. Every row above needs a wave-sized pattern to show the
        // defect and resists minimisation; this one minimises to two constructs and a subject of
        // repeated 'a's, and then loses the match at EVERY subject length rather than at one.
        // Measured 2026-09-13 on regex 2026.7.19, plain `search` with no partial asked for anywhere
        // (tools/probes/upstream-group-call-loses-matches.py):
        //
        //   pat = r'(?P<g1>[[:alpha:]])(?:(?(1)(?<=(?&g1))[^\p{L}]|[A-Z]))([^\d]*)'
        //   subject      'a '     'aa '    'aaa '   'aaaa '  'aaaaa ' 'aaaaaa '
        //   upstream     None     None     None     None     None     None
        //   inlined      (0, 2)   (1, 3)   (2, 4)   (3, 5)   (4, 6)   (5, 7)
        //
        // "inlined" is the same pattern with '(?&g1)' written out as the class it calls, which is
        // semantically the same lookbehind. Six lengths, six lost matches, no exceptions. The
        // lookbehind consumes nothing, so it cannot remove a match - this family's own argument, in
        // its cleanest form and without a zero-width REPEAT to reason about.
        //
        // The WAVE drew the row with partial=True, where upstream degrades the complete match to a
        // partial and leaves the trailing group unset instead of losing it outright. That shape is
        // keyed individually in `ExpectedDivergences`; the non-partial form above is classified by
        // the entry's own predicate, unaided, which is an independent check on this judgement.
        MatchCollection plain = new FuzzyRegex(
            @"(?P<g1>[[:alpha:]])(?:(?(1)(?<=(?&g1))[^\p{L}]|[A-Z]))([^\d]*)"
        ).Matches("aaaa ");

        plain.Select(static m => (m.Index, m.Length)).Should().Equal((3, 2));

        // The trailing group captures empty at the end of the subject, which upstream's inline copy
        // also reports and its call copy never reaches.
        (plain[0].Groups[2].Success, plain[0].Groups[2].Index, plain[0].Groups[2].Length)
            .Should()
            .Be((true, 5, 0));

        // AND CUTTING THAT ROW WENT ALL THE WAY DOWN, which no earlier row of this family did. Three
        // items - a named group, a lookbehind that CALLS it, and one more item - and upstream still
        // loses the match at every subject length from 'a ' to 'aaaaaaa '. The ledger entry's "the
        // minimal form is not established" paragraph was written before this row existed and is
        // corrected there: every earlier attempt shrank a row holding the call inside a CONDITIONAL
        // inside a REPEAT, and cut away the repeat or the conditional - the pieces the surrounding
        // match needed - rather than the call's own setting.
        //
        //   regex.search(r'(?P<g1>\w)(?<=(?&g1))\W', 'aa ')   ->  None
        //   regex.search(r'(?P<g1>\w)(?<=\w)\W',     'aa ')   ->  (1, 3)     <- this port, both
        //
        // Measured 2026-09-13 on regex 2026.7.19 AND on 2026.9.10, the newest release, so issue
        // 614's fix does not reach the minimal form either.
        //
        // THE SUBJECT IS THREE CHARACTERS AND NOT TWO, because a SECOND defect sits on top of this
        // one at exactly one length. On 'a ' this port also answers None - not because it shares
        // this bug, but because a call counts towards `min_width` at the width of the group it calls
        // even inside a zero-width lookaround, so `min_width` is 3 here and `do_exact_match`'s width
        // early-out refuses a two-character subject before matching starts. That inflation is
        // upstream's, this port reproduces it deliberately, and the test below this one pins it.
        // One more character separates the two. This test was first written with 'a ' and failed,
        // which is how the mask was found rather than assumed.
        Match minimal = new FuzzyRegex(@"(?P<g1>\w)(?<=(?&g1))\W").Match("aa ");

        minimal.Success.Should().BeTrue("upstream answers None to this and (1, 3) with the call written out");
        (minimal.Index, minimal.Length).Should().Be((1, 2));

        // The mask itself, stated as an assertion rather than as prose: one character shorter and
        // both engines answer nothing, for the unrelated reason above.
        new FuzzyRegex(@"(?P<g1>\w)(?<=(?&g1))\W")
            .Match("a ")
            .Success.Should()
            .BeFalse();

        // The isolation, in the two directions that matter. A call inside a lookaround running the
        // SAME way as the pattern is unaffected on both engines, which is what makes the title's
        // "opposite direction" the precondition rather than "a call" or "a lookaround". The subject
        // is 'aaaa ' here because the lookahead has to have something to look at.
        Match sameDirection = new FuzzyRegex(@"(?P<g1>\w)(?=(?&g1))\w").Match("aaaa ");
        sameDirection.Success.Should().BeTrue();
        (sameDirection.Index, sameDirection.Length).Should().Be((0, 2));

        // And a call where it CONSUMES, with no lookaround at all: both engines agree.
        Match consuming = new FuzzyRegex(@"(?P<g1>\w)(?&g1)\W").Match("aaa ");
        (consuming.Index, consuming.Length).Should().Be((1, 3));
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

    // AGREES WITH UPSTREAM. S40c closed the verdict S40a left open, and it went the other way from
    // both of that slice's readings: there is no group-call defect here at all.
    [Test]
    public void A_group_call_counts_towards_min_width_even_inside_a_zero_width_lookaround()
    {
        // S40a, rows 98956 (`partial`) and 103926 (`partial-sliced`) of a 6000-row seed-7 wave,
        // minimised by hand. It shares the two tests above's precondition - a group reached by a
        // CALL from a lookaround running the other way round from the pattern - and for two sessions
        // that was read as a third symptom of ledger entry 8. IT IS NOT. The call's direction is not
        // involved, the lookaround is not involved, and upstream is right on every row.
        //
        // WHAT IS ACTUALLY HAPPENING, traced through this port in S40c and then predicted and
        // confirmed on upstream by tools/probes/upstream-min-width-partial-retry.py:
        //
        //  1. `min_width` counts a group CALL at the width of the group it calls, even when the call
        //     sits inside a LOOKAROUND, which consumes nothing. So
        //     `(?P<g1>A)(?:(?<=(?P>g1))\w)?` has min_width 2 where the same lookbehind written out
        //     has 1. That is the only thing the call contributes, and this port reproduces it.
        //  2. `do_match` answers a partial request in two passes, non-partial first, and
        //     `do_exact_match`'s width early-out is guarded by `partial_side == RE_PARTIAL_NONE`
        //     (`upstream/src/_regex.c:18069`), so it fires on the first pass only. A subject too
        //     narrow for the inflated min_width therefore SKIPS the non-partial pass entirely, and
        //     the partial pass answers instead.
        //
        // So upstream's extra PARTIAL is the min_width inflation showing through the two-pass
        // structure, and it depends on how many characters are AVAILABLE rather than on the call
        // being present: give the same pattern one more character and the partial goes away, widen
        // the called group and the threshold moves by exactly the callee's width. Both measured, on
        // 2026.7.19 and on 2026.9.10 alike.
        //
        // THE PORT'S OWN DEFECT WAS ELSEWHERE, and S40a's "this port loses upstream's partial on an
        // astral subject" was a true observation of it: `do_exact_match` counted `available` in
        // UTF-16 code units, so one astral character read as two, the early-out did not fire, the
        // non-partial pass ran and succeeded, and the retry never happened. Fixed in S40c and pinned
        // by `PartialMatchingTests.The_width_early_out_that_skips_the_non_partial_pass_counts_
        // characters_not_code_units`, which is where the mechanism lives. What is left here is the
        // group call's half: that a call inside a zero-width lookaround still counts.
        //
        // What this replaced: S40a's `..._loses_upstreams_partial_only_on_an_astral_subject`, which
        // asserted this port's astral answers and said in terms that it was measurements rather than
        // a judgement. Every one of those assertions has flipped.
        string astral = char.ConvertFromUtf32(0x10400);

        // The two wave rows, now agreeing. Upstream's spans are codepoints; U+10400 is one codepoint
        // and two UTF-16 code units.
        //
        //   search(r'(?P<g1>\U00010400)(?:(?<=(?P>g1))\w)?', '\U00010400', partial=True)
        //     upstream (0, 1) g1 (0, 1) PARTIAL          here (0, 2) g1 (0, 2) PARTIAL
        //   fullmatch(r'(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?', '\U00010400', partial=True)
        //     upstream (0, 1) g1 UNSET  PARTIAL          here (0, 2) g1 UNSET  PARTIAL
        Match forward = new FuzzyRegex("(?P<g1>" + astral + @")(?:(?<=(?P>g1))\w)?").Match(astral, partial: true);

        forward.Success.Should().BeTrue();
        (forward.Index, forward.Length).Should().Be((0, 2));
        forward.PartialMatch.Should().BeTrue("min_width is 2 and one character is available");
        forward.Groups["g1"].Success.Should().BeTrue();

        Match reversed = new FuzzyRegex(@"(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?").FullMatch(astral, partial: true);

        reversed.Success.Should().BeTrue();
        (reversed.Index, reversed.Length).Should().Be((0, 2));
        reversed.PartialMatch.Should().BeTrue("the same early-out, through a lookahead under (?r)");
        reversed.Groups["g1"].Success.Should().BeFalse("upstream drops g1 when the partial pass answers");

        // THE CONTROL THAT ISOLATES THE CALL, and it is the one S40a read as evidence of a
        // group-call defect. Write the call out as the body it calls and upstream's partial goes
        // away - `search(r'(?P<g1>\U00010400)(?:(?<=\U00010400)\w)?', '\U00010400', partial=True)`
        // is partial=False. That is not upstream contradicting itself between a call and its body:
        // the inlined lookbehind is zero-width, so min_width drops from 2 to 1, one character is
        // enough, and the early-out never fires. Same for the bare optional tail below.
        new FuzzyRegex("(?P<g1>" + astral + ")(?:(?<=" + astral + @")\w)?")
            .Match(astral, partial: true)
            .PartialMatch.Should()
            .BeFalse("min_width is 1 with the lookbehind written out");
        new FuzzyRegex("(?P<g1>" + astral + @")\w?").Match(astral, partial: true).PartialMatch.Should().BeFalse();

        // The ASCII spellings, which agreed all along and still do. They are here because they are
        // what a regression would break first: the fix changed a character count, so the rows with
        // no astral character in them must not move at all.
        new FuzzyRegex(@"(?P<g1>A)(?:(?<=(?P>g1))\w)?")
            .Match("A", partial: true)
            .PartialMatch.Should()
            .BeTrue();
        new FuzzyRegex(@"(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?")
            .FullMatch("a", partial: true)
            .PartialMatch.Should()
            .BeTrue();

        // A REQUIRED tail is a partial in both engines whatever min_width does, because the tail
        // itself asks for a character past the end. These rows never depended on the early-out and
        // are the other half of the regression guard.
        new FuzzyRegex("(?P<g1>" + astral + @")\w")
            .Match(astral, partial: true)
            .PartialMatch.Should()
            .BeTrue();
        new FuzzyRegex(@"(?P<g1>A)\w").Match("A", partial: true).PartialMatch.Should().BeTrue();
        new FuzzyRegex("(?P<g1>" + astral + @")(?:(?<=(?P>g1))\w)")
            .Match(astral, partial: true)
            .PartialMatch.Should()
            .BeTrue();
    }
}
