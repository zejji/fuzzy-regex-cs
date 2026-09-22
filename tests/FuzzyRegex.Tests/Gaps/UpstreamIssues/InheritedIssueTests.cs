using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.UpstreamIssues;

/// <summary>
/// The five open upstream issues that S49's sweep proved this port inherits: 425, 554, 563, 564
/// and 589. S49 wrote them all failing and skipped; <b>S50 fixed 425 and parked the other four</b>,
/// so nothing here is skipped any more. <b>S57c then fixed 563 and 564, which are one bug</b>, so
/// those two tests now pin THIS PORT'S answer and upstream's is quoted beside it; 554 and 589 are
/// still parked and still pin the INHERITED answer, with the blocker written above each assertion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read a parked test before assuming it records a gap nobody has looked at.</b> 589's fix was
/// written, measured, reverted after the slice's own blind review broke it, and its notes carry
/// what the sound fix has to be (PCRE2's <c>hitend</c>) and the two rows the reverted attempt
/// broke. 554's notes carry the two cheap answers that were ruled out by measurement. Both are
/// named blockers in <c>docs/plan/STATE.md</c>.
/// </para>
/// <para>
/// Measured 2026-09-14 against <c>regex</c> 2026.9.10 and against this port, by
/// <c>tools/probes/upstream-issue-sweep.py</c> and <c>tools/probes/port-issue-sweep.ps1</c>; 563
/// and 564 re-measured 2026-09-21 by <c>tools/probes/issue-563-anchor-rule.py</c> and
/// <c>tools/probes/port-issue-563-anchor-rule.ps1</c>. Both engines gave the wrong answer on all
/// five, which is why the oracle could not see them (design spec amendment 13) and why the tracker
/// was the instrument; now that 563 and 564 diverge, the oracle can, and
/// <c>ExpectedDivergences</c> holds their family. The full triage, including the six issues the
/// sweep dismissed or attributed to upstream alone, is in
/// <c>docs/plan/upstream-issues/2026-09-14-triage.md</c>.
/// </para>
/// <para>
/// What each test asserts is deliberately narrower than "what the reporter wanted". Upstream has
/// not decided 425's numbering rule and this slice does not get to invent one, so the assertion is
/// only what the two of the maintainer's three candidate rules that would change anything agree
/// on - his option 1 is today's behaviour. The same discipline gives 564
/// a monotonicity bar rather than an expected match list, and 554 a bar taken from what the two
/// Python engines already manage rather than from a memory figure that would vary by machine.
/// </para>
/// </remarks>
public sealed class InheritedIssueTests
{
    [Test]
    public void Branch_reset_numbers_two_groups_in_the_same_branch_differently()
    {
        // Upstream issue 425. On 'BUG!' both engines answer bug='!', because the second branch
        // gives BOTH (?P<bug>BUG) and (!) the number 1 and the later write wins:
        //
        //   regex:     {'groups': ('!', None), 'bug': '!', 'groupindex': {'bug': 1}, 'ngroups': 2}
        //   this port:  bug='!' bugIndex=1 | g1='!' g2=None
        //
        // The maintainer's 2021-09-28 comment offers three numbering rules, and option 1 is
        // explicitly "(current behaviour)". Options 2 and 3 both skip a number already used in the
        // branch, so both give branch 2 the numbers 1 and 2 and both make 'bug' reach 'BUG'. This
        // test asserts what those two agree on and therefore does rule out option 1 - on the
        // ground that under it `(?P<bug>BUG)`'s text is not reachable by number, and
        // `Groups["bug"]` returns text a DIFFERENT group matched. Ledger entry 17 has the full
        // argument. S50 fixed this shape with option 2; S82 replaced that with option 3, which
        // fixes the mirror-image shapes below as well, so the port now has one rule and not two.
        var pattern = new FuzzyRegex("(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))");

        Match m = pattern.MatchAtStart("BUG!");

        m.Success.Should().BeTrue();
        m.Groups["bug"].Value.Should().Be("BUG");
        m.Groups.Cast<Group>().Skip(1).Select(static g => g.Success ? g.Value : null).Should().Contain("!");
    }

    /// <summary>
    /// The three shapes option 2 could not reach, where the unnamed group is written before the
    /// reused name. S82 fixes them with option 3, and this port's numbering is now a divergence
    /// from upstream rather than a bug shared with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream numbers each branch from the same start and skips nothing, so the second branch's
    /// unnamed group takes the number the name already owns and the two write to one slot.
    /// Measured on <c>regex</c> 2026.9.10, 2026-09-22, by
    /// <c>tools/probes/s82-branch-reset-option3.py</c>:
    /// </para>
    /// <code>
    /// (?|(?P&lt;bug&gt;xxx)(!)|(!)(?P&lt;bug&gt;BUG))  on '!BUG'  groups=('BUG', None)
    /// (?|(?P&lt;n&gt;a)(b)|(c)(?P&lt;n&gt;d))          on 'cd'    groups=('d', None)
    /// (?|(?P&lt;n&gt;a)(b)(c)|(x)(?P&lt;n&gt;y)(z))    on 'xyz'   groups=('y', 'z', None)
    /// </code>
    /// <para>
    /// The rule and the reason are in <c>docs/DIVERGENCES.md</c>; what the third row adds is that
    /// the displaced group can be in the middle of the branch, not only at its start.
    /// </para>
    /// </remarks>
    /// <param name="pattern">A branch reset whose second branch reuses a name after an unnamed group.</param>
    /// <param name="subject">A subject the second branch matches.</param>
    /// <param name="expected">The group values this port gives, in order from group 1.</param>
    [Test]
    [Arguments("(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))", "!BUG", new[] { "BUG", "!" })]
    [Arguments("(?|(?P<n>a)(b)|(c)(?P<n>d))", "cd", new[] { "d", "c" })]
    [Arguments("(?|(?P<n>a)(b)(c)|(x)(?P<n>y)(z))", "xyz", new[] { "y", "x", "z" })]
    public void Branch_reset_leaves_a_number_for_a_name_the_rest_of_the_branch_will_use(
        string pattern,
        string subject,
        string[] expected
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(subject);

        m.Success.Should().BeTrue();
        m.Groups.Cast<Group>().Skip(1).Select(static g => g.Value).Should().Equal(expected);
    }

    [Test]
    public void A_long_repeated_capture_group_costs_the_backtracking_stack_a_block_per_repetition()
    {
        // Upstream issue 554. Measured 2026-09-14, fullmatch('(ab)*', 'ab' * n):
        //
        //   n           stdlib re      regex 2026.9.10     this port
        //   1,000,000   ok (99 B/rep)  ok (192 B/rep)      ok (611 B/rep, 583 MB)
        //   2,000,000   ok (96 B/rep)  ok (192 B/rep)      ok
        //   4,000,000   ok (94 B/rep)  ok (192 B/rep)      InvalidOperationException, 1GB bound
        //   6,000,000   ok (98 B/rep)  ok (161 B/rep)      (not reached)
        //   10,000,000  ok (92 B/rep)  MemoryError         (not reached)
        //
        // All three columns exclude the subject string: each probe builds it before it starts
        // measuring, so these are engine allocations per repetition rather than totals. Only the
        // port's FIRST row is quotable - the engine rents its backtracking buffer from a
        // process-wide pool, so a later call in the same process may reuse it and appear to
        // allocate half as much. Ledger entry 18 has the detail.
        //
        // Where the bytes go, from the stack of the run that proved this: Matcher.BasicMatch ->
        // PushMatchBodyTailStateData -> ByteStack.PushSize -> PushBlock -> Grow. One
        // MatchBodyTailStateData block per repetition, never popped while the repeat is running.
        //
        // PARKED BY S50, and this test pins the ceiling this port actually reaches rather than the
        // one S49 asked for. The bar S49 wrote was upstream's own n=6,000,000; meeting it needs the
        // per-repetition cost roughly halved, and that is an engine optimisation with a
        // memory-for-throughput trade-off, which Phase 7 owns behind benchmarks. What S50 checked
        // before parking it (.scratch probes re-run 2026-09-14, and the numbers are in the closing
        // notes) is that neither of the two cheap answers is the right one:
        //
        //   - The capture group is where the cost is. `(?:ab)*` reaches n=4,000,000 and fails at
        //     6,000,000; `(ab)*` fails at 4,000,000. Atomic `(?>(ab)*)` and possessive `(ab)*+`
        //     both still fail at 4,000,000, so the blocks are pushed inside the group either way.
        //   - Raising or reshaping the bound would be a patch on the symptom. The 1GB limit is
        //     tested against the DOUBLED capacity (ByteStack.Grow), so the largest usable stack is
        //     just under 512MB - but that is upstream's own check, ported faithfully
        //     (_regex.c:2357), and upstream reaches 6,000,000 under the same cap because its
        //     per-repetition cost is lower. Clamping the capacity would make this test pass while
        //     leaving the actual defect untouched.
        //
        // The failure MODE is already better than upstream's, which is what issue 554 reports: a
        // clear exception naming a documented bound, where upstream raises MemoryError at
        // n=10,000,000 and stdlib `re` still succeeds there. So what is left is a performance gap,
        // not a wrong answer, and STATE.md carries it as a named blocker.
        var repeated = new FuzzyRegex("(ab)*", FuzzyRegexOptions.None, TimeSpan.FromMinutes(2));

        // The size this port does manage. 4,000,000 chars of subject, so not free, but ~2s.
        string reached = string.Concat(Enumerable.Repeat("ab", 2_000_000));
        Match m = repeated.FullMatch(reached);

        m.Success.Should().BeTrue();
        m.Length.Should().Be(reached.Length);

        // And the size it does not, failing at the documented bound rather than dying. This is the
        // line that goes green when Phase 7 halves the per-repetition cost, and it must then be
        // rewritten to upstream's 6,000,000 rather than deleted.
        string beyond = string.Concat(Enumerable.Repeat("ab", 4_000_000));

        Assert
            .Throws<InvalidOperationException>(() => repeated.FullMatch(beyond))
            .Message.Should()
            .Contain("backtracking stack");
    }

    [Test]
    public void A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero_here()
    {
        // FIXED BY S57c, and the fix is a DELIBERATE DIVERGENCE: `docs/DIVERGENCES.md`, row
        // "fuzzy-insertion-at-a-pinned-anchor", which also says how to get upstream's answer back.
        // Upstream issues 563 and 564, still open, still reproduced by `regex` 2026.9.10. Every
        // upstream answer quoted below was measured on 2026-09-21 by
        // `python tools/probes/issue-563-anchor-rule.py`; this port's by
        // `pwsh -File tools/probes/port-issue-563-anchor-rule.ps1`.
        //
        // THE BUG. Upstream forbids a fuzzy section from opening with an inserted character at the
        // search anchor, under its own comment at _regex.c:10213: "Permit insertion except
        // initially when searching (it's better just to start searching one character later)".
        // `search_anchor` is set once per matching operation (init_match, :3410) and never per
        // candidate start position, so the rule fires at exactly ONE of the positions a scan
        // visits. A single leading space is the whole difference:
        //
        //   findall(r'\m(?:Y){i}\M', 'XY YX')   -> ['YX']          <- upstream, 'XY' missing
        //   findall(r'\m(?:Y){i}\M', ' XY YX')  -> ['XY', 'YX']    <- upstream, same shape, found
        //
        // THE RULE THIS PORT APPLIES INSTEAD. "Start searching one character later" is the same
        // match minus an insertion only while the pattern still fits one character later. So the
        // prohibition is lifted where a zero-width position assertion at the head of the pattern
        // holds at the anchor and FAILS one character on, and nowhere else. Both halves are load
        // bearing: drop the one-character-on test and four of the ported suite's tests go red,
        // upstream's own test_fuzzy rows 51 and 56 among them, where the assertion holds one
        // character on too (measured 2026-09-21; S50 found the narrowing the same way, against a
        // different implementation of the pin).
        //
        // WHERE THE ANSWER LIVES, which is what S50 got wrong twice. It is a property of the
        // PATTERN - `PatternObject.AnchorGuards`, filled by `Optimiser.FindAnchorGuards` - not a
        // flag in `MatchState`. S50 used the flag, and because the backtracking engine neither
        // saves nor restores it, it was wrong in both directions: an assertion that held only on
        // an abandoned path still pinned the anchor, and the backtrack-arm clears that fixed that
        // also threw away a pin set outside the construct. The guards are collected by walking the
        // pattern's leading chain and stopping at the first node that can send matching down more
        // than one path, so an assertion the engine can abandon is never collected in the first
        // place. Every shape those two attempts broke is still below, and still green.

        // THE ROW THE ISSUE REPORTS. Upstream answers ['YX'].
        Values(@"\m(?:Y){i}\M", "XY YX").Should().Equal("XY", "YX");

        // The same shape one character along, where upstream's rule never fires and both engines
        // have always agreed. This port now gives one answer to both, which is the point.
        Values(@"\m(?:X){i}\M", "XY YX").Should().Equal("XY", "YX");
        Values(@"\m(?:Y){i}\M", " XY YX").Should().Equal("XY", "YX");

        // Upstream already allows a RUNAWAY leading insertion at every position except the anchor,
        // so these two subjects, one leading space apart, get different answers from it at the same
        // relative position: findall(r'\m(?:Y){i}\M', 'q XY YX') -> ['XY', 'YX'] and over
        // ' q XY YX' -> ['q XY', 'YX']. Here they agree. The runaway itself is upstream's own
        // reading of an unbounded `{i}` and this port keeps it; what the fix removes is the
        // position that answered differently from all the others.
        Values(@"\m(?:Y){i}\M", "q XY YX").Should().Equal("q XY", "YX");
        Values(@"\m(?:Y){i}\M", " q XY YX").Should().Equal("q XY", "YX");

        // A MATCH UPSTREAM LOSES ALTOGETHER rather than shortens, which is the clearest form of the
        // bug: there is no match one character later to fall back on, so "start searching one
        // character later" costs the whole answer. Upstream gives [] to all five.
        Values(@"\b(?:abc){i<=1}", "xabc").Should().Equal("xabc");
        Values(@"\m(?:YZ){i}\M", "XYZ ZY").Should().Equal("XYZ");
        Values(@"\G(?:abc){i<=1}", "xabc").Should().Equal("xabc");
        Values(@"\m(?:z|)(?:Y){i}\M", "XY").Should().Equal("XY");
        Values(@"\m(?!q)(?:Y){i}\M", "XY").Should().Equal("XY");

        // THE INCONSISTENCY INSIDE UPSTREAM that says the fix is a generalisation of its own
        // behaviour rather than a new rule. '^' and '\A' already escape the prohibition, because
        // basic_match turns a start-anchored pattern into an anchored match and stops searching, so
        // upstream KEEPS a match that opens with an inserted character for the first two rows and
        // loses it for the third - the same assertion, at the same position, under a flag that
        // should not matter here. Upstream: ['xabc'], ['xabc'], [].
        Values("^(?:abc){i<=1}", "xabc").Should().Equal("xabc");
        Values(@"\A(?:abc){i<=1}", "xabc").Should().Equal("xabc");
        Values("(?m)^(?:abc){i<=1}", "xabc").Should().Equal("xabc");

        // AND THE SAME IN REVERSE, which is the fix being a property of the pattern rather than of
        // the direction. Reversed, the anchor is the end of the subject and the guard is the
        // pattern's trailing assertion, so upstream loses the match at THAT end instead:
        // findall(r'(?r)\m(?:Y){i}\M', 'XY YX') -> ['XY']. Here both directions find both matches,
        // in their own order.
        Values(@"(?r)\m(?:Y){i}\M", "XY YX").Should().Equal("YX", "XY");

        // THE ROWS THE TWO FAILED ATTEMPTS BROKE, pinned so no attempt can break them again.
        // Every one is upstream's answer, measured against regex 2026.9.10 on 2026-09-14, and every
        // one is this port's answer today.
        //
        // No assertion before the fuzzy item, so "start searching one character later" really is
        // the same match minus an insertion and upstream's rule is doing its job.
        Values("(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values("(?<![0-9])(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values("(?:Y){i}", "qXY").Should().Equal("XY");

        // An assertion that holds one character on too - what the one-step-on test exists for.
        Values(@"\b(?:abc){i<=2}", "x abc").Should().Equal(" abc");
        Values(@"\b(?:abc){i<=2}", "ab abc").Should().Equal(" abc");
        Values(@"\B(?:abc){i<=2}", "xy abc").Should().Equal("y abc");

        // A LOOKAROUND IS NOT A POSITION ASSERTION, deliberately: it runs a subpattern, and
        // `(?=x)` before a fuzzy section is a character test wearing an assertion's clothes.
        // Upstream loses the leading insertion here and this port agrees with it.
        Values("(?=x)(?:abc){i<=1}", "xabc").Should().BeEmpty();

        // An assertion that holds at position 0 but only on a path the engine abandons: an
        // alternative it gives up on, the body of a NEGATIVE lookaround, or a repeat that matches
        // nothing. The first attempt answered 'xabc' on the first three; the second answered
        // 'xabc' on the last five.
        Values(@"(?:\bq|)(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?:(?=\b)(?!)|)(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?!\bz)(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?:\bq)*(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?:\bq)?(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?:\bq){0,3}(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?:\bq)*+(?:abc){i<=1}", "xabc").Should().Equal("abc");
        Values(@"(?>(?:\bq)*)(?:abc){i<=1}", "xabc").Should().Equal("abc");

        // The inert group after the assertion, which the second attempt's clears threw away, is
        // now up with the rows the fix answers: a guard the pattern always passes is not something
        // a later `(?:z|)` or `(?!q)` can take away.
        static string[] Values(string pattern, string subject) =>
            [.. new FuzzyRegex(pattern).Matches(subject).Select(static m => m.Value)];
    }

    [Test]
    public void A_reversed_fuzzy_match_may_insert_at_the_end_of_the_subject()
    {
        // The same bug at the other end of the subject, found by the differential oracle rather
        // than by the issue reports: seed 20260921 row 3752, minimised. Under `(?r)` the search
        // anchor is where a reversed search starts, which is the END of the subject, so upstream's
        // one-position rule bans a TRAILING insertion instead of a leading one and the direction
        // alone decides whether the match exists.
        //
        // Upstream regex 2026.9.10, measured 2026-09-21 by
        // `python tools/probes/issue-563-anchor-rule.py`, section 7:
        //
        //   search(r'(?r)^a(?:b){i<=1}$', 'ab\r')  -> None
        //   search(r'^a(?:b){i<=1}$', 'ab\r')      -> (0, 3), one insertion
        //
        // One pattern, one subject, one answer each way. The match is the same in both: 'a', then
        // the fuzzy section matching 'b' and absorbing the '\r' as an insertion, then '$' at the
        // end of the subject. '$' does not match before a '\r' - only before a final '\n' - so the
        // '\r' has to be consumed for the pattern to reach the end at all.
        Span(@"(?r)^a(?:b){i<=1}$", "ab\r").Should().Be((0, 3, 1));
        Span(@"^a(?:b){i<=1}$", "ab\r").Should().Be((0, 3, 1));

        // The leading '^' is not what pins it. Reversed, the pattern's trailing '$' is the first
        // thing matched, so it is the guard, and upstream loses this row too.
        Span(@"(?r)a(?:b){i<=1}$", "ab\r").Should().Be((0, 3, 1));

        // The two controls that say the divergence needs the insertion. With nothing to absorb,
        // and with a budget that cannot absorb it, both engines agree. Upstream: (0, 2) with no
        // errors, then None.
        Span(@"(?r)^a(?:b){i<=1}$", "ab").Should().Be((0, 2, 0));
        Span(@"(?r)^a(?:b){s<=1}$", "ab\r").Should().Be((-1, -1, -1));

        static (int Start, int End, int Insertions) Span(string pattern, string subject)
        {
            Match m = new FuzzyRegex(pattern).Match(subject);

            return m.Success ? (m.Index, m.Index + m.Length, m.FuzzyCounts.Insertions) : (-1, -1, -1);
        }
    }

    [Test]
    public void Loosening_a_fuzzy_budget_keeps_every_match_the_tighter_one_found()
    {
        // Upstream issue 564. FIXED BY S57c with no code of its own, which is S50's finding
        // confirmed a second time: 563 and 564 are ONE bug, reached two ways. BESTMATCH re-anchors
        // its candidate walk at the match start when the budget is loosened, so the looser budget
        // meets upstream's "no insertion at the search anchor" rule where the tighter one never
        // did, and loses a match it should by definition still find. Reverting the 563 fix takes
        // this row straight back, which is how the two were tied together in the first place.
        //
        // Upstream regex 2026.9.10, measured 2026-09-21 by
        // `python tools/probes/issue-563-anchor-rule.py`:
        //
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z')  -> ['XY', 'Z']
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z')  -> ['Z']        <- 'XY' lost
        //
        // Monotonicity is the bar, rather than an expected match list: a budget of <=2 admits
        // every match a budget of <=1 admits, because every candidate the tighter constraint
        // accepts also satisfies the looser one. Covered by `docs/DIVERGENCES.md`, row
        // "fuzzy-insertion-at-a-pinned-anchor", with 563.
        string[] tight =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=1}\M").Matches(" XY Z").Select(static m => m.Value),
        ];
        string[] loose =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=2}\M").Matches(" XY Z").Select(static m => m.Value),
        ];

        tight.Should().Equal("XY", "Z");

        // Monotonicity, which is the assertion the bug denied. Upstream answers ['Z'].
        loose.Should().Contain(tight);
        loose.Should().Equal("XY", "Z");
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void A_partial_fullmatch_reports_a_prefix_whose_completion_matches()
    {
        // Upstream issue 589, ledger entry 21, FIXED HERE by S57d. 'True' is a prefix of 'Truest',
        // and 'Truest' is a complete match of the pattern, so upstream's own documented definition
        // of a partial match - "whether a complete match could be possible if the string had not
        // been truncated" (upstream/docs/Features.html:576) - makes this a partial. Upstream
        // answers None; this port now answers the partial, and docs/DIVERGENCES.md carries the row.
        //
        // The maintainer's defence is that `\b` matches at the end of 'True', so the negative
        // lookahead fails. That resolves an assertion against text the caller has said is
        // truncated. PCRE2 10.47 does not (tools/probes/pcre2-partial-truncation-assertions.py):
        //
        //   (?!(True|False)\b)(.*)  over 'True'    SOFT=PARTIAL (0,4)   HARD=PARTIAL (0,4)
        //   True\b                  over 'True'    SOFT=match   (0,4)   HARD=PARTIAL (0,4)
        //   True\B                  over 'True'    SOFT=PARTIAL (0,4)   HARD=PARTIAL (0,4)
        //
        // The `True\b` row is the mechanism: PCRE2 treats a boundary at the end of the available
        // text as unresolved and escalates, rather than deciding it against text it has not seen.
        // Note that this is the FALSE NEGATIVE direction, which is the dangerous one for the
        // incremental-input use case upstream's documentation advertises. Issue 367 is the false
        // positive in the same machinery and is NOT a bug - PCRE2 does the same thing and deciding
        // it in general is undecidable. See Gaps/Engine/PartialMatchingTests.cs.
        //
        // S50 FIXED THIS AND ITS OWN BLIND REVIEW BROKE THE FIX, so the shape of the fix matters as
        // much as the answer. S50 made the seven word and grapheme boundary predicates answer
        // PARTIAL at the truncation point. That turns this row green, but returning PARTIAL from a
        // predicate ENDS the match, and the engine had not finished backtracking:
        //
        //   search(r'(\.+?)\1\b', '..',   partial=True) -> group 1 was (0, 1), upstream (0, 2)
        //   search(r'(\.+?)\1\b', '....', partial=True) -> group 1 was (0, 2), upstream (0, 3)
        //
        // The lazy repeat's FIRST try reached the boundary, escalated, and returned before the
        // repeat could grow, so a partial this port had exactly right came back with a truncated
        // capture group. S57d takes PCRE2's model instead: the predicate sets MatchState.HitEnd and
        // answers as it always did, matching and backtracking run to the end, and only a FINAL
        // failure becomes a partial. The rows below hold both halves of that.
        var pattern = new FuzzyRegex(@"(?!(True|False)\b)(.*)");

        // The completion this partial is a prefix of, so the premise is measured, not asserted.
        pattern.FullMatch("Truest").Success.Should().BeTrue();

        // THE FIX. PCRE2 10.47 answers PARTIAL (0,4) here under both partial options, and the span
        // is the whole of the available text.
        Match prefix = pattern.FullMatch("True", partial: true);

        prefix.Success.Should().BeTrue();
        prefix.PartialMatch.Should().BeTrue();
        (prefix.Index, prefix.Length).Should().Be((0, 4));

        // A complete match still wins over the escalation, which is what makes this PCRE2's SOFT
        // semantics rather than its HARD ones: asking for a partial does not cost 'Truest' its
        // complete match. PCRE2 10.47 answers the same row SOFT=match (0,6) and HARD=PARTIAL (0,6)
        // - measured by tools/probes/pcre2-partial-truncation-assertions.py, not read off the man
        // page, which illustrates the two options with a date rather than this pattern. Upstream's
        // `partial` has only the soft sense.
        Match complete = pattern.FullMatch("Truest", partial: true);

        complete.Success.Should().BeTrue();
        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 6));

        // Without asking for a partial there is still no match, which both engines already get right.
        pattern.FullMatch("True").Success.Should().BeFalse();

        // AND THE ROWS THE REVERTED FIX BROKE. Upstream answers the same on both, and this port
        // agreed before S50, agreed again after the revert, and still agrees now. A partial found
        // the ORDINARY way keeps its own span and its own capture groups; the hitend flag is set
        // here too, and is never read, because the match did not fail.
        var lazyBackreference = new FuzzyRegex(@"(\.+?)\1\b");

        foreach ((string subject, int groupLength) in new[] { ("..", 2), ("....", 3) })
        {
            Match m = lazyBackreference.Match(subject, 0, subject.Length, partial: true);

            m.PartialMatch.Should().BeTrue();
            (m.Index, m.Length).Should().Be((0, subject.Length));
            (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, groupLength));
        }
    }
}
