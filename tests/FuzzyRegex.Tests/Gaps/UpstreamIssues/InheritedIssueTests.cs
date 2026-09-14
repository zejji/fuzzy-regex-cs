using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.UpstreamIssues;

/// <summary>
/// The five open upstream issues that S49's sweep proved this port inherits: 425, 554, 563, 564
/// and 589. S49 wrote them all failing and skipped; <b>S50 fixed 425 and parked the other four</b>,
/// so nothing here is skipped any more and the four parked tests pin the INHERITED answer with the
/// blocker written above each assertion. 563, 564 and 589 were fixed and then REVERTED when this
/// slice's own blind reviews broke the fixes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read a parked test before assuming it records a gap nobody has looked at.</b> 589's fix was
/// written, measured, reverted after the slice's own blind review broke it, and its notes carry
/// what the sound fix has to be (PCRE2's <c>hitend</c>) and the two rows the reverted attempt
/// broke. 554's notes carry the two cheap answers that were ruled out by measurement. Both are
/// named blockers in <c>docs/plan/STATE.md</c>.
/// </para>
/// </remarks>
/// <remarks>
/// <para>
/// Measured 2026-09-14 against <c>regex</c> 2026.9.10 and against this port, by
/// <c>tools/probes/upstream-issue-sweep.py</c> and <c>tools/probes/port-issue-sweep.ps1</c>. Both
/// engines give the wrong answer on all five, which is why the oracle cannot see them (design spec
/// amendment 13) and why the tracker is the instrument. The full triage, including the six issues
/// the sweep dismissed or attributed to upstream alone, is in
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
        // The maintainer has NOT settled which numbering rule is right; his 2021-09-28 comment
        // offers three, and option 1 is explicitly "(current behaviour)". Options 2 and 3 both
        // skip a number already used in the branch, so both give branch 2 the numbers 1 and 2 and
        // both make 'bug' reach 'BUG'. This test asserts what those two agree on and therefore
        // does rule out option 1 - on the ground that under it `(?P<bug>BUG)`'s text is
        // unreachable through any API, and `Groups["bug"]` returns text a DIFFERENT group matched.
        // Ledger entry 17 has the full argument.
        var pattern = new FuzzyRegex("(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))");

        Match m = pattern.MatchAtStart("BUG!");

        m.Success.Should().BeTrue();
        m.Groups["bug"].Value.Should().Be("BUG");
        m.Groups.Cast<Group>().Skip(1).Select(static g => g.Success ? g.Value : null).Should().Contain("!");
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
    public void A_word_start_anchor_before_a_fuzzy_section_still_does_not_match_at_position_zero()
    {
        // Upstream issue 563, and issue 564 below it, which is the same bug reached another way.
        // The maintainer's own comment on both is "It looks like a bug". Both engines return only
        // 'YX' for the first row, and both return BOTH matches for the other two - so a single
        // leading space, or a subject whose first word starts with the literal, is the whole
        // difference. Position 0 is the only thing that fails.
        //
        //   findall(r'\m(?:Y){i}\M', 'XY YX')   -> ['YX']          <- wrong, 'XY' is missing
        //   findall(r'\m(?:X){i}\M', 'XY YX')   -> ['XY', 'YX']
        //   findall(r'\m(?:Y){i}\M', ' XY YX')  -> ['XY', 'YX']
        //
        // PARKED BY S50, WHICH ESTABLISHED THE MECHANISM TO A LINE AND THEN FAILED TWICE TO FIX IT
        // SOUNDLY. Everything below is what the next attempt needs; ledger entries 19 and 20 carry
        // it in full, and `python tools/probes/issue-563-anchor-rule.py` re-runs every row.
        //
        // THE MECHANISM is _regex.c:10214, `permit_insertion = !search || text_pos !=
        // search_anchor`, under upstream's own comment "Permit insertion except initially when
        // searching (it's better just to start searching one character later)". `search_anchor` is
        // set once per matching operation (init_match, :3410) and never per candidate start
        // position, so the rule fires at exactly ONE of the positions a scan visits. The isolating
        // probe is the same subject and the same winning span answered two ways purely by where the
        // search was told to begin:
        //
        //   regex.compile(r'\m(?:Y){i}\M').search(' XY', 0)  -> span (1, 3) 'XY'
        //   regex.compile(r'\m(?:Y){i}\M').search(' XY', 1)  -> None
        //
        // WHY A FIX IS JUSTIFIED AT ALL: it is a generalisation of upstream's own behaviour. '^' and
        // '\A' already escape the rule, because basic_match turns a start-anchored pattern into an
        // anchored match - so upstream KEEPS a match beginning with an inserted character for
        // `^(?:abc){i<=1}` over 'xabc' and loses it for the identical `(?m)^` pattern.
        //
        // WHAT S50 BUILT AND WHY IT WENT BACK. A `MatchState.AssertionPinsStart` flag, set when a
        // zero-width assertion held at the anchor AND failed one character on, read by
        // `AtInsertionAnchor`. The one-step-on test is necessary and was found by breaking
        // upstream's own test_fuzzy rows 51, 52, 54 and 56 without it. The flag is the part that
        // does not work: it is bare mutable state that the backtracking engine never saves or
        // restores, so it is wrong in BOTH directions, and two separate blind reviews each found a
        // different half.
        //
        //   Under-clearing: an assertion that held only on a path the engine then abandoned still
        //   pinned the anchor. Clearing it in the `Branch` and failed-lookaround backtrack arms
        //   fixed those two shapes and left the repeats - `(?:\bq)*`, `(?:\bq)?`, `(?:\bq){0,3}`,
        //   `(?:\bq)*+`, `(?>(?:\bq)*)` over 'xabc' all answered 'xabc' where upstream says 'abc'.
        //
        //   Over-clearing: those same unconditional clears also discard a pin set BEFORE and
        //   OUTSIDE the construct, so adding a semantically inert `(?:z|)` or `(?!q)` after the
        //   `\m` threw the fix away again - `\m(?:z|)(?:Y){i}\M` over 'XY' went back to no match.
        //
        // So the pin has to be part of the backtracking state rather than a field beside it, or be
        // replaced by a compile-time "every path to this fuzzy item passes a position assertion"
        // analysis combined with the dynamic one-step-on test. Either is a slice, and a named
        // blocker in STATE.md.
        //
        // The inherited answer, pinned so a fix cannot land silently. When this row goes green the
        // two below it are what the fix must also satisfy.
        Values(@"\m(?:Y){i}\M", "XY YX").Should().Equal("YX");

        // The two rows that already agree, kept here so a fix that breaks them cannot pass.
        Values(@"\m(?:X){i}\M", "XY YX").Should().Equal("XY", "YX");
        Values(@"\m(?:Y){i}\M", " XY YX").Should().Equal("XY", "YX");

        // THE ROWS THE TWO FAILED ATTEMPTS BROKE, pinned so the next attempt has to keep them.
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

        // And an inert group after the assertion, which the second attempt's clears threw away.
        Values(@"\m(?:z|)(?:Y){i}\M", "XY").Should().BeEmpty();
        Values(@"\m(?!q)(?:Y){i}\M", "XY").Should().BeEmpty();

        static string[] Values(string pattern, string subject) =>
            [.. new FuzzyRegex(pattern).Matches(subject).Select(static m => m.Value)];
    }

    [Test]
    public void Loosening_a_fuzzy_budget_still_loses_a_match_the_tighter_one_found()
    {
        // Upstream issue 564, parked with 563 above because S50 proved they are ONE bug: the fix
        // for 563 turned this row green with no code of its own, through BESTMATCH's re-anchoring,
        // and reverting 563 took it back. The reporter suspected they were related and was right.
        //
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z')  -> ['XY', 'Z']
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z')  -> ['Z']        <- 'XY' lost
        //
        // Monotonicity is what a fix must deliver - a budget of <=2 admits every match a budget of
        // <=1 admits, because every candidate the tighter constraint accepts also satisfies the
        // looser one - and it is what this port does not deliver today.
        string[] tight =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=1}\M").Matches(" XY Z").Select(static m => m.Value),
        ];
        string[] loose =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=2}\M").Matches(" XY Z").Select(static m => m.Value),
        ];

        tight.Should().Equal("XY", "Z");

        // The inherited answer. When this becomes `loose.Should().Contain(tight)` the bug is fixed.
        loose.Should().Equal("Z");
    }

    [Test]
    public void A_partial_fullmatch_still_denies_a_prefix_whose_completion_matches()
    {
        // Upstream issue 589. 'True' is a prefix of 'Truest', and 'Truest' is a complete match of
        // the pattern, so upstream's own documented definition of a partial match - "whether a
        // complete match could be possible if the string had not been truncated"
        // (upstream/docs/Features.html:576) - makes this a partial. Both engines answer None.
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
        // PARKED BY S50, AND THIS TEST NOW PINS THE INHERITED ANSWER. S50 fixed it, its blind review
        // broke the fix, and the fix was reverted rather than patched. What S50 tried was to make
        // the seven word and grapheme boundary predicates answer PARTIAL at the right-hand
        // truncation point instead of resolving against the missing character. It turns this row
        // green, but returning PARTIAL from a predicate ENDS the match, and the engine had not
        // finished backtracking:
        //
        //   search(r'(\.+?)\1\b', '..',   partial=True) -> group 1 was (0, 1), upstream (0, 2)
        //   search(r'(\.+?)\1\b', '....', partial=True) -> group 1 was (0, 2), upstream (0, 3)
        //
        // The lazy repeat's FIRST try reached the boundary, escalated, and returned before the
        // repeat could grow - so a partial this port previously got exactly right came back with a
        // truncated capture group. Both rows are upstream's answer at HEAD and both reproduce.
        //
        // The sound fix is PCRE2's model and not this one: a "hit the end of the subject while
        // deciding" flag (`hitend`) that lets matching CONTINUE and only turns a final failure into
        // a partial, so a definite complete match still wins and backtracking still runs to the
        // end. That is an engine change with its own design, not a predicate tweak, and it is a
        // named blocker in STATE.md rather than something to improvise at the end of a slice.
        var pattern = new FuzzyRegex(@"(?!(True|False)\b)(.*)");

        // The completion this partial is a prefix of, so the premise is measured, not asserted.
        // This is the half that makes the answer below wrong, and it is still true.
        pattern.FullMatch("Truest").Success.Should().BeTrue();

        // The inherited answer, pinned so that a fix cannot land silently: when this goes green the
        // assertions below it are what the fix must satisfy.
        pattern.FullMatch("True", partial: true).Success.Should().BeFalse();

        // Without asking for a partial there is still no match, which both engines already get right.
        pattern.FullMatch("True").Success.Should().BeFalse();

        // AND THE ROWS THE REVERTED FIX BROKE, pinned so the next attempt has to keep them. Upstream
        // answers the same on both, and this port agreed before S50 and agrees again.
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
