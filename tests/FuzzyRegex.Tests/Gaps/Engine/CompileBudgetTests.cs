using System.Diagnostics;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The compile budget S56b put on the node graph. <c>BuildRepeat</c> unrolls the minimum count of
/// every counted repeat into the graph (upstream <c>build_REPEAT</c>, <c>upstream/src/_regex.c</c>
/// lines 25166-25197), so nested counted repeats allocate the PRODUCT of their counts and a pattern
/// such as <c>((a{1000}){1000}){1000}</c> exhausts memory before it can match anything.
/// </summary>
/// <remarks>
/// <para>
/// The budget itself has no upstream answer - upstream unrolls without limit and dies - so its
/// provenance is the <c>docs/DIVERGENCES.md</c> row "Compile budget", decided by the owner on
/// 2026-09-18. The MATCHING answers below, which the budget must not change, are upstream's, run on
/// 2026-09-18 against regex 2026.9.10 by
/// <c>tools/probes/s56b-compile-budget-upstream-answers.py</c> and quoted beside each assertion.
/// </para>
/// <para>
/// <b>The budget counts nodes CREATED, not nodes kept.</b> <c>Optimiser.DiscardUnusedNodes</c>
/// (<c>Engine/Optimiser.cs</c>) prunes the graph afterwards, and for a counted repeat it removes
/// about half of it: <c>(a{100}){100}</c> creates 20,913 nodes and keeps 10,509. Created is the
/// number that has to be bounded, because the memory is spent before the pruning runs. Every count
/// in this file was measured on 2026-09-18 by bisecting the budget.
/// </para>
/// <para>
/// Every pattern here is bounded on purpose: the largest admitted graph creates about 200,000 nodes
/// (about 50 MB) and the one refused graph stops at the default budget (about 250 MB). Do not raise
/// these counts - the machine this port is built on crashed twice on 2026-09-18 running the
/// unbounded version.
/// </para>
/// </remarks>
public sealed class CompileBudgetTests
{
    /// <summary>
    /// The pattern that crashed the owner's machine, scaled down to the smallest nesting that still
    /// cannot compile: 150^3 = 3,375,000 body copies against a budget of 1,000,000 nodes. The
    /// investigation measured it reaching OutOfMemory in 9 s against a 1 GB heap cap.
    /// </summary>
    private const string _cubedRepeat = "((a{150}){150}){150}";

    [Test]
    [NotInParallel]
    public void The_default_budget_refuses_a_nested_counted_repeat_instead_of_exhausting_memory()
    {
        // Before S56b this allocated until the process died: 3,375,000 body copies at about 250
        // bytes each is about 840 MB of nodes, and the cubed {1000} pattern that prompted the slice
        // passed 15 GB. The budget stops it at 1,000,000 nodes - 249 MB and about 0.7 s (681 ms
        // and 763 ms on two Debug-build runs, 2026-09-18) - so the
        // assertion that matters is the exception type, and the time bound is here to catch a check
        // that lets the compile run on.
        var clock = Stopwatch.StartNew();

        Action compile = static () => _ = new FuzzyRegex(_cubedRepeat);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Message.Should()
            .Contain(FuzzyRegex.DefaultMaxCompiledNodes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the budget bounds the cost of a refused pattern");
    }

    [Test]
    [NotInParallel]
    public void A_large_counted_repeat_below_the_budget_still_compiles_and_matches()
    {
        // 100,000 unrolled copies, about 50 MB while compiling: comfortably inside the default
        // budget, and far larger than anything in the corpus (largest single count {65535}).
        // Upstream, 2026-09-18, regex 2026.9.10:
        //   regex.match(r'(a{100000})?b', 'b')  -> span=(0, 1) groups=(None,)
        Match optional = FuzzyRegex.MatchAtStart("b", "(a{100000})?b");

        (optional.Index, optional.Length).Should().Be((0, 1));
        optional.Groups[1].Success.Should().BeFalse("the optional repeat matched no text");

        // A nested counted repeat is admitted too, as long as the product stays under the budget:
        // 1000 * 100 creates 202,713 nodes here and keeps 101,409. Upstream, same run:
        //   regex.match(r'(a{1000}){100}', 'a' * 99)  -> None
        FuzzyRegex
            .MatchAtStart(new string('a', 99), "(a{1000}){100}")
            .Success.Should()
            .BeFalse("the pattern needs 100,000 characters");
    }

    [Test]
    public void A_budget_the_caller_chooses_rejects_what_the_default_admits()
    {
        // The knob: at a budget of 200 nodes, a{200} (406 created) no longer fits and a{50} (106)
        // still does. Nothing about the pattern changed, only the number the caller passed.
        Action tooBig = static () =>
            _ = new FuzzyRegex(
                "a{200}",
                FuzzyRegexOptions.None,
                FuzzyRegex.InfiniteMatchTimeout,
                maxCompiledNodes: 200
            );

        tooBig.Should().Throw<FuzzyRegexParseException>().Which.Message.Should().Contain("200");

        var small = new FuzzyRegex(
            "a{50}",
            FuzzyRegexOptions.None,
            FuzzyRegex.InfiniteMatchTimeout,
            maxCompiledNodes: 200
        );

        // Upstream, 2026-09-18, regex 2026.9.10:
        //   regex.match(r'a{50}', 'a' * 99)  -> span=(0, 50) groups=()
        Match m = small.MatchAtStart(new string('a', 99));
        (m.Index, m.Length).Should().Be((0, 50));
        small.MaxCompiledNodes.Should().Be(200, "the pattern reports the budget it was compiled with");
    }

    [Test]
    public void The_refusal_names_the_budget_the_reason_and_the_way_to_raise_it()
    {
        // Pinned in full: this message is the only thing a caller who hits the budget has to work
        // from, and it is a port-original message with no upstream equivalent to fall back on.
        FuzzyRegexParseException error = new Action(static () =>
            _ = new FuzzyRegex(
                "(a{100}){100}",
                FuzzyRegexOptions.None,
                FuzzyRegex.InfiniteMatchTimeout,
                maxCompiledNodes: 500
            )
        )
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which;

        error
            .Message.Should()
            .Be(
                "compiling this pattern needs more than 500 nodes, the limit maxCompiledNodes was "
                    + "set to. A counted repeat is expanded into one copy of its body per repetition, so "
                    + "nested counted repeats multiply: their counts are multiplied together. Raise "
                    + "maxCompiledNodes on the FuzzyRegex constructor to compile this pattern, or reduce "
                    + "the repeat counts."
            );
        error.Pattern.Should().Be("(a{100}){100}", "the caller gets the pattern back, as for any parse failure");
        error.Offset.Should().Be(-1, "no single position in the pattern is at fault");
    }

    [Test]
    public void The_budget_counts_every_node_in_the_graph_and_admits_exactly_that_many()
    {
        // Where the boundary sits, measured rather than asserted in the abstract: '(a{100}){100}'
        // creates 20,913 nodes, so it is admitted at 20,913 and refused at 20,912. This is the test
        // that pins the off-by-one, and it is also what pins the budget to nodes CREATED: the graph
        // this pattern KEEPS is 10,509 nodes, and a budget of 10,509 does not compile it.
        Action atTheLimit = static () =>
            _ = new FuzzyRegex(
                "(a{100}){100}",
                FuzzyRegexOptions.None,
                FuzzyRegex.InfiniteMatchTimeout,
                maxCompiledNodes: 20_913
            );

        atTheLimit.Should().NotThrow();

        Action oneBelow = static () =>
            _ = new FuzzyRegex(
                "(a{100}){100}",
                FuzzyRegexOptions.None,
                FuzzyRegex.InfiniteMatchTimeout,
                maxCompiledNodes: 20_912
            );

        oneBelow.Should().Throw<FuzzyRegexParseException>();

        Action keptButNotCreated = static () =>
            _ = new FuzzyRegex(
                "(a{100}){100}",
                FuzzyRegexOptions.None,
                FuzzyRegex.InfiniteMatchTimeout,
                maxCompiledNodes: 10_509
            );

        keptButNotCreated
            .Should()
            .Throw<FuzzyRegexParseException>("the optimiser's pruning comes after the memory has been spent");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public void A_budget_that_is_not_positive_is_rejected_at_the_trust_boundary(int budget)
    {
        // Same treatment as matchTimeout: argument validation first, before any compiling.
        Action compile = () =>
            _ = new FuzzyRegex("a", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, maxCompiledNodes: budget);

        compile.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxCompiledNodes");
    }

    [Test]
    public void The_default_budget_is_a_million_nodes_and_is_what_a_pattern_gets_when_none_is_asked_for()
    {
        // About 250 MB at 250 bytes a node, and about 0.7 s to reach (measured 2026-09-18), which is
        // the ceiling a server compiling a caller's pattern is agreeing to.
        FuzzyRegex.DefaultMaxCompiledNodes.Should().Be(1_000_000);
        new FuzzyRegex("a").MaxCompiledNodes.Should().Be(FuzzyRegex.DefaultMaxCompiledNodes);
    }
}
