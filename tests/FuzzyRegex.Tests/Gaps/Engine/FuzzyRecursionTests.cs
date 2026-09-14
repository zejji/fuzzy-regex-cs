using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Recursion and group calls composed with a fuzzy section - which of the shapes terminate, which
/// exhaust their budget, and that the port fails the unbounded ones SAFELY rather than by running
/// the machine out of memory.
/// </summary>
/// <remarks>
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
/// shape no earlier wave could draw. This port INHERITS it - which is the right outcome for a
/// faithful port - but fails safely: a bounded backtracking stack with a stated limit, rather than
/// an allocator that keeps asking. Re-runnable as
/// <c>python tools/probes/upstream-fuzzy-recursion-blowup.py</c>, whose 23 rows are the evidence
/// for the rule and for the two exceptions to it below.
/// </para>
/// <para>
/// The generator does not draw a self-recursive call round a fuzzy section at all, and a guard that
/// forces progress was tried and is NOT sufficient - see <c>INTERACTION_FUZZY_WRAPPERS</c> in
/// <c>tools/record-oracle.py</c>. So this file is the whole of the recursion-times-fuzzy coverage.
/// </para>
/// </remarks>
public sealed class FuzzyRecursionTests
{
    /// <summary>How long a blowup is allowed to take before the test calls it a hang.</summary>
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(30);

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
    public void A_self_recursive_call_whose_section_can_vanish_fails_with_a_stated_limit()
    {
        // The three of the eleven that DO blow up, where two deletions empty a two-atom section -
        // `{2i+1d+1s<=2}` prices a deletion at 1 against a budget of 2, so it reaches two as well:
        //   (?P<g1>(?:Ab){e<=2}(?&g1)?)        over 'AbAb' -> MemoryError in 0.79s
        //   (?P<g1>(?:Ab){1<=e<=2}(?&g1)?)     over 'AbAb' -> MemoryError in 0.78s
        //   (?P<g1>(?:Ab){2i+1d+1s<=2}(?&g1)?) over 'AbAb' -> MemoryError in 0.78s
        //   (?P<g1>(?:Ab){d<=2}(?&g1)?)        over 'AbAb' -> MemoryError in 0.76s   (not drawn)
        //
        // This port inherits the non-termination and bounds it. What is asserted is the BOUND, not
        // a match: that the engine gives up with an exception naming its own limit, rather than
        // taking the machine down or running past the deadline.
        //
        // THE EXCEPTION IS NAMED, AND THAT IS THE POINT OF THE ASSERTION (S47, sitting 2). It used
        // to be `Throw<Exception>` with a non-empty message, which a `RegexMatchTimeoutException`
        // from the 30-second budget above satisfies just as well - so the test could not tell the
        // stack limit from the clock, and a regression that made the engine merely SLOW would have
        // passed it. Measured 2026-09-14: all four raise `InvalidOperationException` carrying
        // ByteStack's own message, in 0.63s to 1.52s against a 30-second deadline.
        foreach (string constraint in new[] { "{e<=2}", "{1<=e<=2}", "{2i+1d+1s<=2}", "{d<=2}" })
        {
            Action search = () =>
                new FuzzyRegex($"(?P<g1>(?:Ab){constraint}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match("AbAb");

            search
                .Should()
                .Throw<InvalidOperationException>(
                    $"'(?:Ab){constraint}' can match the empty string, so the recursion never progresses"
                )
                .WithMessage("*backtracking stack exceeded its 1GB limit*");
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

        Action blows = static () =>
            new FuzzyRegex("(?P<g1>(?:Abc){e<=3}(?&g1)?)", FuzzyRegexOptions.None, _budget).Match("AbcAbc");

        // The named exception, for the reason the test above states: a bare `Throw<Exception>` also
        // passes on the deadline, so it cannot tell the bound from the clock. 1.10s of 30, 2026-09-14.
        blows
            .Should()
            .Throw<InvalidOperationException>("three deletions empty a three-atom section")
            .WithMessage("*backtracking stack exceeded its 1GB limit*");
    }

    [Test]
    public void Whole_pattern_recursion_inside_a_fuzzy_section_is_the_degenerate_case_of_the_rule()
    {
        // A section whose only content is the recursion matches empty at ANY budget, so every one
        // of these blows up - with a base case, without one, and for an error kind that cannot
        // delete anything:
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

        foreach ((string pattern, string subject) in cases)
        {
            Action search = () => new FuzzyRegex(pattern, FuzzyRegexOptions.None, _budget).Match(subject);

            // Named, for the reason the two tests above state. Measured 2026-09-14: 0.25s to 0.81s
            // against a 30-second deadline, so it is the stack limit that fires and not the clock.
            search
                .Should()
                .Throw<InvalidOperationException>($"'{pattern}' recurses without consuming")
                .WithMessage("*backtracking stack exceeded its 1GB limit*");
        }
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
