using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.UpstreamIssues;

/// <summary>
/// The five open upstream issues that S49's sweep proved this port inherits: 425, 554, 563, 564
/// and 589. Each test asserts the CORRECT behaviour, so every one fails today; S50 removes the
/// <c>[Skip]</c> attributes as it fixes them.
/// </summary>
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
    [Skip("needs:issue-425 - branch reset gives two groups in one branch the same number")]
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
    [Skip("needs:issue-554 - a repeated capture group costs hundreds of bytes per repetition")]
    public void A_long_repeated_capture_group_does_not_exhaust_the_backtracking_stack()
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
        // So the port inherits the growth AND amplifies it: it gives up at 4,000,000 where
        // upstream still manages 6,000,000. The bar here is upstream's own ceiling rather than a
        // byte figure, because a byte figure would vary by machine and this one does not - the
        // 1GB backtracking bound is a fixed constant, so this test is deterministic, not a race.
        //
        // It is however genuinely heavy once un-skipped: 8,000,000 chars of subject (16 MB, UTF-16)
        // and an allocation run up to the 1GB bound. Timings vary a lot - 3.4 s to 8.6 s across
        // runs of the same call - so budget for the slow end. If S50 finds that too much for CI,
        // lower the bar to the largest n that still fails - do not delete the test.
        //
        // Where the bytes go, from the stack of the run that proved this test fails (2026-09-14):
        // Matcher.BasicMatch (Matcher.cs:5406) -> PushMatchBodyTailStateData (:2679) -> ByteStack
        // .PushSize -> PushBlock -> Grow (ByteStack.cs:290). One MatchBodyTailStateData block per
        // repetition of the group, never popped while the repeat is still running.
        var pattern = new FuzzyRegex("(ab)*", FuzzyRegexOptions.None, TimeSpan.FromMinutes(2));
        string subject = string.Concat(Enumerable.Repeat("ab", 4_000_000));

        Match m = pattern.FullMatch(subject);

        m.Success.Should().BeTrue();
        m.Length.Should().Be(subject.Length);
    }

    [Test]
    [Skip(@"needs:issue-563 - \m before a fuzzy section does not match at position 0")]
    public void A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero()
    {
        // Upstream issue 563; the maintainer's own comment is "It looks like a bug". Both engines
        // return only 'YX' for the first row below, and both return BOTH matches for the other
        // two - so a single leading space, or a subject whose first word starts with the literal,
        // is the whole difference. Position 0 is the only thing that fails.
        //
        //   findall(r'\m(?:Y){i}\M', 'XY YX')   -> ['YX']          <- wrong, 'XY' is missing
        //   findall(r'\m(?:X){i}\M', 'XY YX')   -> ['XY', 'YX']
        //   findall(r'\m(?:Y){i}\M', ' XY YX')  -> ['XY', 'YX']
        new FuzzyRegex(@"\m(?:Y){i}\M")
            .Matches("XY YX")
            .Select(static m => m.Value)
            .Should()
            .Equal("XY", "YX");

        // The two rows that already agree, kept here so a fix that breaks them cannot pass.
        new FuzzyRegex(@"\m(?:X){i}\M")
            .Matches("XY YX")
            .Select(static m => m.Value)
            .Should()
            .Equal("XY", "YX");
        new FuzzyRegex(@"\m(?:Y){i}\M").Matches(" XY YX").Select(static m => m.Value).Should().Equal("XY", "YX");
    }

    [Test]
    [Skip("needs:issue-564 - loosening a fuzzy budget loses a match")]
    public void Loosening_a_fuzzy_budget_never_loses_a_match_the_tighter_one_found()
    {
        // Upstream issue 564; the maintainer's own comment is again "It looks like a bug". Both
        // engines:
        //
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z')  -> ['XY', 'Z']
        //   findall(r'(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z')  -> ['Z']        <- 'XY' lost
        //
        // The assertion is monotonicity rather than an expected list: a budget of <=2 admits every
        // match a budget of <=1 admits, because every candidate the tighter constraint accepts
        // also satisfies the looser one. That is true whatever the right answer set turns out to
        // be, so it does not commit S50 to a spans list this slice has no authority to fix.
        string[] tight =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=1}\M").Matches(" XY Z").Select(static m => m.Value),
        ];
        string[] loose =
        [
            .. new FuzzyRegex(@"(?b)\m(?:Y){1i+1d+1s<=2}\M").Matches(" XY Z").Select(static m => m.Value),
        ];

        tight.Should().Equal("XY", "Z");
        loose.Should().Contain(tight);
    }

    [Test]
    [Skip("needs:issue-589 - a partial fullmatch denies a prefix whose completion exists")]
    public void A_partial_fullmatch_admits_a_prefix_whose_completion_matches()
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
        var pattern = new FuzzyRegex(@"(?!(True|False)\b)(.*)");

        // The completion this partial is a prefix of, so the premise is measured, not asserted.
        pattern.FullMatch("Truest").Success.Should().BeTrue();

        Match partial = pattern.FullMatch("True", partial: true);

        partial.Success.Should().BeTrue();
        partial.PartialMatch.Should().BeTrue();
        (partial.Index, partial.Index + partial.Length).Should().Be((0, 4));

        // Without asking for a partial there is still no match, which both engines already get right.
        pattern.FullMatch("True").Success.Should().BeFalse();
    }
}
