using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Recursion and group calls composed with a fuzzy section - which of the shapes terminate, which
/// ones upstream cannot terminate at all, and what this port answers instead.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read this file's history first: the rule below describes UPSTREAM, and since S47 it no longer
/// describes this port.</b> Ledger entry 14's guard, taken from PCRE2's
/// <c>PCRE2_ERROR_RECURSELOOP</c>, refuses a call that re-enters a group at a text position where a
/// call of that group is already open, which is exactly the condition under which no progress is
/// possible. Every shape the rule predicts a blowup for is now answered here in microseconds, and
/// the answer is the one the non-vanishing sibling gives. Upstream still raises <c>MemoryError</c>
/// on all of them, so these rows are a deliberate divergence - an inherited bug fixed here, which
/// is the third of design spec amendment 16's four outcomes.
/// </para>
/// <para>
/// <b>The rule, measured rather than reasoned.</b> A group that calls ITSELF makes progress only if
/// its body must consume something. A fuzzy section can match the empty string whenever its budget
/// permits as many DELETIONS as the section has atoms, so a budget reaching n deletions of an
/// n-atom section is the dangerous one; <c>{s&lt;=n}</c> and <c>{i&lt;=n}</c> never delete anything,
/// whatever n is. With nothing forcing progress, upstream allocates until it raises
/// <c>MemoryError</c>, in under a second. Whole-pattern recursion is the degenerate case:
/// <c>(?:(?R)){e&lt;=1}</c> is a section whose only content is the recursion, so it matches empty at
/// any budget - which is why <c>(?R)</c> and <c>(?0)</c> blow up even with a base case, where the
/// identical recursion WITHOUT a fuzzy section answers instantly.
/// </para>
/// <para>
/// <b>It is a predictor and not a proof, and the exceptions are stated rather than smoothed over.</b>
/// Across the eleven constraints the generator can draw, the rule accounts for nine.
/// <c>{e&lt;=2,i&lt;=1}</c> and <c>{e&lt;=2,s&lt;=1}</c> cap the total at two, cap no deletions, and
/// are nevertheless safe in 0.00s. Why a compound constraint behaves differently has not been
/// established, so the generator's exclusion rests on the measured table rather than on the rule.
/// </para>
/// <para>
/// It is upstream's 551/554 resource-blowup family, already on Phase 6's triage list, reached by a
/// shape no earlier wave could draw. Re-runnable as
/// <c>python tools/probes/upstream-fuzzy-recursion-blowup.py</c>, whose 23 rows are the evidence
/// for the rule and for the two exceptions to it below.
/// </para>
/// <para>
/// <b>The bound is still there and is still the backstop</b> - <c>ByteStack.Grow</c> raises
/// <c>InvalidOperationException: the regular expression engine's backtracking stack exceeded its 1GB
/// limit</c> - because the guard bounds the DEPTH of a recursion and does nothing about the
/// BRANCHING a fuzzy section offers at every position of every level. It is no longer reachable by
/// any shape in this file, which is the point.
/// </para>
/// </remarks>
public sealed class FuzzyRecursionTests
{
    /// <summary>How long a blowup is allowed to take before the test calls it a hang.</summary>
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The same, for the one test that has to fill a gigabyte before it can assert. That is paced
    /// by the machine's free memory rather than by the engine, so it needs a budget nothing short
    /// of a real hang can exhaust; the measurements are beside the call that uses it.
    /// </summary>
    private static readonly TimeSpan _blowupBudget = TimeSpan.FromMinutes(5);

    [Test]
    public void A_call_to_a_group_that_has_already_closed_is_not_recursion_and_matches()
    {
        // regex.compile(r'(?P<g1>[ab]+)(?:(?&g1)c){e<=1}').search('ababc')  -> (0, 5) in 0.00s
        // The safe half, and the reason the blowup below is about self-reference rather than about
        // group calls: this one calls a group that has finished, so the call consumes what the group
        // captured and the section has a fixed amount of work to do.
        Match m = new FuzzyRegex("(?P<g1>[ab]+)(?:(?&g1)c){e<=1}").Match("ababc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 5));
    }

    [Test]
    public void A_self_recursive_call_whose_section_cannot_vanish_terminates()
    {
        // Eight of the eleven constraints the generator can draw, all measured at 0.00s upstream on
        // `(?P<g1>(?:Ab){C}(?&g1)?)` over 'AbAb' and all answering (0, 4). The last two are the pair
        // the rule MISPREDICTS - they cap the total at two and cap no deletions, so the rule says
        // they should vanish a two-atom section, and they do not.
        foreach (
            string constraint in new[]
            {
                "{e<=1}",
                "{s<=1}",
                "{i<=1}",
                "{d<=1}",
                "{s<=1,i<=1,d<=1}",
                "{1i+2d+1s<=3}",
                "{e<=2,i<=1}",
                "{e<=2,s<=1}",
            }
        )
        {
            Match m = new FuzzyRegex($"(?P<g1>(?:Ab){constraint}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match(
                "AbAb"
            );

            m.Success.Should().BeTrue($"'(?:Ab){constraint}' cannot match the empty string");
            (m.Index, m.Length).Should().Be((0, 4));
        }
    }

    [Test]
    public void A_self_recursive_call_whose_section_can_vanish_answers_the_same_as_one_that_cannot()
    {
        // S47's guard closes this. A section that CAN vanish leaves the call re-entering the very
        // position it was called at, which is the condition PCRE2 names
        // `PCRE2_ERROR_RECURSELOOP` - and refusing that one path is enough: the section's
        // non-vanishing alternatives are still there, so all four of these now answer exactly what
        // the eight constraints above answer, (0, 4), instead of filling a gigabyte.
        //
        // Upstream still raises MemoryError on all four (0.76s to 0.79s, quoted below), so this is
        // a deliberate divergence from upstream and an inherited bug fixed here - ledger entry 14.
        //   (?P<g1>(?:Ab){e<=2}(?&g1)?)        over 'AbAb' -> MemoryError in 0.79s
        //   (?P<g1>(?:Ab){1<=e<=2}(?&g1)?)     over 'AbAb' -> MemoryError in 0.78s
        //   (?P<g1>(?:Ab){2i+1d+1s<=2}(?&g1)?) over 'AbAb' -> MemoryError in 0.78s
        //   (?P<g1>(?:Ab){d<=2}(?&g1)?)        over 'AbAb' -> MemoryError in 0.76s   (not drawn)
        foreach (string constraint in new[] { "{e<=2}", "{1<=e<=2}", "{2i+1d+1s<=2}", "{d<=2}" })
        {
            Match m = new FuzzyRegex($"(?P<g1>(?:Ab){constraint}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match(
                "AbAb"
            );

            m.Success.Should().BeTrue($"'(?:Ab){constraint}' still has a non-vanishing alternative");
            (m.Index, m.Length).Should().Be((0, 4));
        }
    }

    [Test]
    public void A_three_atom_section_moves_the_same_boundary_one_budget_further_out()
    {
        // The rule is about the budget against the ATOM COUNT, not about the budget alone:
        //   (?P<g1>(?:Abc){e<=2}(?&g1)?) over 'AbcAbc' -> (0, 6) in 0.00s   2 errors, 3 atoms
        //   (?P<g1>(?:Abc){e<=3}(?&g1)?) over 'AbcAbc' -> MemoryError in 0.80s
        // `{e<=2}` blows up on the two-atom shape above and is safe here, which is what makes this
        // a rule rather than a list of unlucky constraints - and `{e<=3}` is not one the generator
        // draws, which is why all eleven of its constraints are safe at three atoms.
        Match safe = new FuzzyRegex("(?P<g1>(?:Abc){e<=2}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match("AbcAbc");

        safe.Success.Should().BeTrue();
        (safe.Index, safe.Length).Should().Be((0, 6));

        // `{e<=3}` is the constraint that empties a three-atom section, so it is the one that used
        // to recurse without progressing - and S47's guard cuts it at the re-entered position, so
        // it now answers what its safe sibling answers. Upstream still raises MemoryError in 0.80s.
        Match vanishing = new FuzzyRegex("(?P<g1>(?:Abc){e<=3}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match(
            "AbcAbc"
        );

        vanishing.Success.Should().BeTrue("three deletions empty a three-atom section");
        (vanishing.Index, vanishing.Length).Should().Be((0, 6));
    }

    [Test]
    public void Whole_pattern_recursion_inside_a_fuzzy_section_is_the_degenerate_case_of_the_rule()
    {
        // A section whose only content is the recursion matches empty at ANY budget, so every one
        // of these blows up UPSTREAM - with a base case, without one, and for an error kind that
        // cannot delete anything. Here they are all cut at the position the call re-enters:
        //   (?:(?R)){e<=1}       over 'ab'   -> MemoryError in 0.48s
        //   (?:a(?R)?b){e<=1}    over 'aabb' -> MemoryError in 0.87s
        //   a(?:(?0)){e<=1}b     over 'aabb' -> MemoryError in 0.96s
        //   (?:(?R)){s<=1}       over 'ab'   -> MemoryError in 0.48s
        //   (?=(?:(?R)){e<=1})a  over 'ab'   -> MemoryError in 0.69s
        (string Pattern, string Subject)[] cases =
        [
            ("(?:(?R)){e<=1}", "ab"),
            ("(?:a(?R)?b){e<=1}", "aabb"),
            ("a(?:(?0)){e<=1}b", "aabb"),
            ("(?:(?R)){s<=1}", "ab"),
            ("(?=(?:(?R)){e<=1})a", "ab"),
        ];

        // What each one answers once the re-entry is refused, measured 2026-09-14 and reasoned
        // about rather than merely recorded, because there is no upstream answer to check against:
        //
        //   (?:(?R)){e<=1}       'ab'   -> None    the pattern's only content is a call to itself,
        //   (?:(?R)){s<=1}       'ab'   -> None    so its language is empty however the budget is
        //                                          spent - a section of one un-deletable item
        //   a(?:(?0)){e<=1}b     'aabb' -> None    a REQUIRED recursion with no base case: every
        //                                          level demands another, so nothing terminates it
        //   (?=(?:(?R)){e<=1})a  'ab'   -> None    the first row inside a lookahead, so the
        //                                          lookahead cannot succeed either
        //   (?:a(?R)?b){e<=1}    'aabb' -> (0, 4)  the one with a base case, and it answers exactly
        //                                          what the same recursion without the section does
        (int Index, int Length)?[] expected = [null, (0, 4), null, null, null];

        foreach (((string pattern, string subject), (int Index, int Length)? want) in cases.Zip(expected))
        {
            Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _budget).Match(subject);

            m.Success.Should().Be(want is not null, pattern);

            if (want is not null)
            {
                (m.Index, m.Length).Should().Be(want.Value, pattern);
            }
        }
    }

    [Test]
    public void A_drawn_wave_row_upstream_only_escapes_through_its_prefilter()
    {
        // THE ROW THAT PAID FOR THE GUARD, and the only one in this file that is not hand-built.
        // Row 72179 of the 6000-row `interactions` wave at seed 20260914, minimised from
        // `(?b)\b(?P<g2>(?:(\p{Lu})(?:[[:digit:]]?){s<=1,i<=1,d<=1}){s<=1,i<=1,d<=1}(?P>g2)?)([a]+)`
        // over 'B'. Before the guard this port exhausted its backtracking stack on it and upstream
        // answered `no match`, so it was a live divergence; with the guard the two agree.
        //
        // Upstream's answer is its REQUIRED-STRING PREFILTER and not an answer from its engine,
        // which is worth knowing before anyone reads the agreement as upstream getting this right.
        // Put the required 'a' into the subject and upstream blows up like every other row here
        // (`python tools/probes/upstream-same-position-reentry.py`, regex 2026.9.10, 2026-09-14):
        //   fullmatch 'B'  -> no match     in 0.00s   <- no 'a' anywhere, so the engine never runs
        //   fullmatch 'Ba' -> MemoryError  in 0.93s
        //   search    'Ba' -> MemoryError  in 0.94s
        // This port has no such prefilter - it is Phase 7's - so it reaches the engine on all three
        // and the guard is what carries it.
        var re = new FuzzyRegex(@"(?P<g2>(?:[A-Z]\d?){s<=1,i<=1,d<=1}(?P>g2)?)a", FuzzyRegexOptions.None, _budget);

        re.FullMatch("B").Success.Should().BeFalse("the wave row, and upstream agrees");

        Match full = re.FullMatch("Ba");

        full.Success.Should().BeTrue();
        (full.Index, full.Length).Should().Be((0, 2));

        Match searched = re.Match("Ba");

        searched.Success.Should().BeTrue();
        (searched.Index, searched.Length).Should().Be((0, 2));
    }

    [Test]
    public void The_stack_bound_is_still_what_catches_a_blowup_the_guard_cannot_see()
    {
        // The guard bounds the DEPTH of a recursion; nothing bounds the BRANCHING a fuzzy section
        // offers at every position of every level, so `ByteStack.Grow`'s limit is still the
        // backstop and still has to be reachable. Without this test nothing in the suite reaches
        // it, because S47's guard answers every shape in this file.
        //
        // The row is the one `record-oracle.py` quotes for its own exclusion, and it holds no group
        // call at all - which is why the guard cannot help it. Upstream raises MemoryError in 1.75s
        // to 1.78s on the first three, with and without a ranking flag, and answers the last two at
        // once (measured 2026-09-13, quoted in `_generate_interactions`):
        //   (?b)(?P<g1>\p{L}*)+?(?:ab){e<=1}   MemoryError in 1.78s
        //       (?P<g1>\p{L}+)+?(?:ab){e<=1}   (0, 2)      <- body must consume
        //
        // This one call gets `_blowupBudget` and not the file's 30s `_budget`, and the reason is
        // that reaching the limit means COMMITTING a gigabyte, so how long it takes is a fact about
        // the machine's FREE MEMORY and not about the engine. Measured on one 32GB machine,
        // 2026-09-14, on engines that are byte-identical:
        //   3.4GB free - 63.4s and 17.6s to throw; a third run under a 30s budget never got there
        //                and the engine raised RegexMatchTimeoutException at 30.2s instead
        //   9.3GB free - 15.1s to 18.8s to throw, five runs, no timeout at any of them
        // Under `_budget` that 30s arm lost the race and red-ratcheted a tree whose engine had not
        // changed - identically at c8165b5, three commits earlier - so the failure was never about
        // the code under test.
        //
        // InfiniteMatchTimeout would be the wrong answer even though it removes the race outright.
        // A MatchTimeout is the ONLY thing that can stop this loop: `MatchState.CheckTimedOut`
        // returns false immediately on `NoTimeout`, the engine observes no CancellationToken, and
        // the assembly-wide [Timeout(120_000)] does not touch a CPU-bound synchronous test - a
        // 20-second body passes under a 3-second assembly timeout, and `tools/check-ratchet.ps1`
        // carries the same lesson from a host that ran 44 minutes past `--timeout 20m`. So an
        // infinite budget would turn "the bound stopped being reachable" from a red test into a
        // hung suite. Five minutes is about five times the slowest throw ever measured here, and it
        // still fails LOUDLY if the bound goes away.
        Action blows = static () =>
            new FuzzyRegex(@"(?P<g1>\p{L}*)+?(?:ab){e<=1}", FuzzyRegexOptions.None, _blowupBudget).Match("bb.a\r.");

        blows
            .Should()
            .Throw<InvalidOperationException>("a fuzzy section under a lazy repeat of an empty-matching body")
            .WithMessage("*backtracking stack exceeded its 1GB limit*");

        // The control the record-oracle note carries: make the body consume and the same row
        // answers at once, on both engines.
        Match m = new FuzzyRegex(@"(?P<g1>\p{L}+)+?(?:ab){e<=1}", FuzzyRegexOptions.None, _budget).Match("bb.a\r.");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
    }

    [Test]
    public void The_same_recursion_without_a_fuzzy_section_matches_instantly()
    {
        // regex.compile(r'(?:a(?R)?b)').search('aabb')  -> (0, 4) in 0.00s
        // The control that makes the whole family about the fuzzy section rather than about
        // recursion: delete the constraint from the first row above and the identical recursion
        // terminates at once, on both engines.
        Match m = new FuzzyRegex("(?:a(?R)?b)").Match("aabb");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
    }
}
