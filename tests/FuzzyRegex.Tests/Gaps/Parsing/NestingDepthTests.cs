using System.Text;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// D110: a deeply nested pattern used to overflow the stack while compiling, and a .NET stack
/// overflow cannot be caught, so <c>new FuzzyRegex(new string('(', 1500) + "a" + new string(')',
/// 1500))</c> ended the process. Two guards now stand in the way. The parser refuses more than
/// <see cref="_limit"/> levels of groups and sets, so the answer is the same on every thread with
/// a normal stack; and every pass that recurses over the pattern checks the stack it has left, so
/// a thread with a small stack gets a <see cref="FuzzyRegexParseException"/> too, at whatever
/// depth its stack runs out.
/// </summary>
/// <remarks>
/// <para>
/// Upstream has no limit of its own: Python's recursion limit stops it with a catchable
/// <c>RecursionError</c>, at 140 to 330 levels depending on the construct (regex 2026.9.10 on
/// Python 3.14, 2026-10-19, <c>.scratch/s1019/d110/probe/up.py</c>), so every pattern upstream
/// compiles is under the limit here. The <c>docs/DIVERGENCES.md</c> row "Nesting limit" records
/// the different exception.
/// </para>
/// <para>
/// <b>These tests cannot crash the test host.</b> Before the fix, the depths here that compile on
/// the host's 1 MB thread were measured to compile (1,100 to 1,450 levels; the limit is 500), and
/// the 100,000-level and small-stack cases overflowed in a child process
/// (<c>.scratch/s1019/d110/probe</c>, exit code -1073741571, "Stack overflow."); those two are
/// the cases a missing guard would turn into a crash, and they were run there first.
/// </para>
/// </remarks>
public sealed class NestingDepthTests
{
    private const int _limit = 500;

    private static string Nest(string lead, string open, string close, int depth)
    {
        var builder = new StringBuilder(lead.Length + ((open.Length + close.Length) * depth) + 1);
        _ = builder.Append(lead).Insert(lead.Length, open, depth).Append('a');
        return builder.Insert(builder.Length, close, depth).ToString();
    }

    [Test]
    [Arguments("", "(", ")")]
    [Arguments("", "(?:", ")")]
    [Arguments("", "(?P<g>", ")")]
    [Arguments("", "(?=", ")")]
    [Arguments("", "(?!", ")")]
    [Arguments("", "(?<=", ")")]
    [Arguments("", "(?<!", ")")]
    [Arguments("", "(?>", ")")]
    [Arguments("(a)", "(?(1)", ")")]
    [Arguments("(?V1)", "[", "]")]
    [Arguments("", "(?:", ")?")]
    [Arguments("", "(?:", ")*")]
    [Arguments("", "(?:a|", ")")]
    [Arguments("", "(?i:", ")")]
    [Arguments("", "(?:", "){e<=1}")]
    [Arguments("(?r)", "(", ")")]
    [Arguments("(?r)", "(?<=", ")")]
    public void A_pattern_nested_to_the_limit_compiles_and_matches(string lead, string open, string close)
    {
        string pattern = Nest(lead, open, close, _limit);

        var regex = new FuzzyRegex(pattern);

        regex.Match("aa").Success.Should().BeTrue();
    }

    [Test]
    [Arguments("", "(", ")")]
    [Arguments("", "(?:", ")")]
    [Arguments("", "(?P<g>", ")")]
    [Arguments("", "(?=", ")")]
    [Arguments("", "(?!", ")")]
    [Arguments("", "(?<=", ")")]
    [Arguments("", "(?<!", ")")]
    [Arguments("", "(?>", ")")]
    [Arguments("(a)", "(?(1)", ")")]
    [Arguments("(?V1)", "[", "]")]
    [Arguments("", "(?:", ")?")]
    [Arguments("", "(?:", ")*")]
    [Arguments("", "(?:a|", ")")]
    [Arguments("", "(?i:", ")")]
    [Arguments("", "(?:", "){e<=1}")]
    [Arguments("(?r)", "(", ")")]
    [Arguments("(?r)", "(?<=", ")")]
    public void One_level_past_the_limit_is_refused_at_the_opening_that_exceeds_it(
        string lead,
        string open,
        string close
    )
    {
        string pattern = Nest(lead, open, close, _limit + 1);

        Action compile = () => _ = new FuzzyRegex(pattern);

        FuzzyRegexParseException refused = compile.Should().Throw<FuzzyRegexParseException>().Which;
        refused.Message.Should().Be($"too deeply nested: more than {_limit} levels of groups and sets");
        refused.Pattern.Should().Be(pattern);
        refused.Offset.Should().Be(lead.Length + (open.Length * _limit));
    }

    [Test]
    public void Groups_and_sets_count_towards_the_same_limit()
    {
        string pattern = "(?V1)" + new string('(', _limit - 1) + "[[a]]" + new string(')', _limit - 1);

        Action compile = () => _ = new FuzzyRegex(pattern);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Offset.Should()
            .Be("(?V1)".Length + _limit, "the inner set is the 501st level");
    }

    [Test]
    public void Siblings_do_not_add_up()
    {
        // Depth is nesting, not the number of groups: 2,000 groups side by side, each 400 deep.
        string one = Nest("", "(?:", ")", 400);
        string pattern = string.Concat(Enumerable.Repeat(one, 5)) + "|" + one;

        var regex = new FuzzyRegex(pattern);

        regex.Match("aaaaa").Value.Should().Be("aaaaa");
    }

    [Test]
    public void A_pattern_a_hundred_thousand_levels_deep_is_refused_not_a_crash()
    {
        // Before D110's fix this ended the process at 1,440 levels on a 1 MB stack.
        string pattern = new string('(', 100_000) + "a" + new string(')', 100_000);

        Action compile = () => _ = new FuzzyRegex(pattern);

        compile.Should().Throw<FuzzyRegexParseException>().Which.Offset.Should().Be(_limit);
    }

    [Test]
    [Arguments("", "(", ")")]
    [Arguments("", "(?:", ")")]
    [Arguments("", "(?=", ")")]
    [Arguments("(a)", "(?(1)", ")")]
    [Arguments("(?V1)", "[", "]")]
    [Arguments("", "(?:", "){e<=1}")]
    [Arguments("(?r)", "(?<=", ")")]
    public void A_thread_with_a_small_stack_gets_a_parse_error_under_the_limit(string lead, string open, string close)
    {
        // 256 KB holds about 150 to 210 levels (measured before the fix, child process), so 500
        // levels, which the limit allows, must be stopped by the stack check.
        string pattern = Nest(lead, open, close, _limit);

        Exception? thrown = OnThreadWithStack(() => _ = new FuzzyRegex(pattern));

        FuzzyRegexParseException refused = thrown.Should().BeOfType<FuzzyRegexParseException>().Which;
        refused.Message.Should().Be("too deeply nested to compile on this thread: its stack ran out");
        refused.InnerException.Should().BeOfType<InsufficientExecutionStackException>();
    }

    [Test]
    public void A_small_stack_still_compiles_a_shallow_pattern()
    {
        string pattern = Nest("", "(", ")", 20);

        Exception? thrown = OnThreadWithStack(() => new FuzzyRegex(pattern).Match("a").Success.Should().BeTrue());

        thrown.Should().BeNull();
    }

    [Test]
    public void A_deep_unterminated_set_gets_its_own_error_on_a_1_MB_thread()
    {
        // An unterminated set makes the compiler try the pattern again under version 0 to choose
        // its advice. That retry used to run inside the catch block, on top of the failed parse's
        // frames, so at 450 levels on a 1 MB thread it ran out of stack and reported the wrong
        // error (found by the D110 design review, 2026-10-19).
        string pattern = new string('(', 450) + "[a" + new string(')', 450);

        Exception? thrown = OnThreadWithStack(() => _ = new FuzzyRegex(pattern), 1024);

        thrown.Should().BeOfType<FuzzyRegexParseException>().Which.Message.Should().Be("unterminated character set");
    }

    [Test]
    public void A_long_run_of_conditionals_compiles_on_a_small_stack()
    {
        // Not nesting at all, so the limit does not apply: the optimiser's walk for repeats and
        // fuzzy sections recursed once per conditional in a row, and before D110's fix 12,000 of
        // them overflowed a 1 MB stack and 1,900 a 256 KB one (child process, exit code 127).
        // Upstream compiles 61,440 and overflows its C stack at about 62,000 (2026-10-19).
        const int count = 20_000;
        string pattern = "(a)?" + string.Concat(Enumerable.Repeat("(?(1)a|b)", count));
        Match? match = null;

        Exception? thrown = OnThreadWithStack(() => match = new FuzzyRegex(pattern).Match(new string('a', count + 1)));

        thrown.Should().BeNull();
        (match!.Index, match.Length).Should().Be((0, count + 1));
    }

    [Test]
    public void Group_calls_recurse_on_the_heap_at_match_time()
    {
        // The matcher keeps its own stack, so a call 20,000 deep needs no thread stack: the same
        // pattern matched 524,288 levels on a 256 KB thread in the D110 probe. Upstream answers
        // the same span (2026-10-19, regex 2026.9.10, n up to 524,288).
        const int depth = 20_000;
        string subject = new string('a', depth) + new string('b', depth);
        Match? match = null;

        Exception? thrown = OnThreadWithStack(() => match = new FuzzyRegex("(a(?1)?b)").Match(subject));

        thrown.Should().BeNull();
        (match!.Index, match.Length).Should().Be((0, 2 * depth));
    }

    private static Exception? OnThreadWithStack(Action action, int kilobytes = 256)
    {
        Exception? thrown = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    thrown = exception;
                }
            },
            kilobytes * 1024
        );
        thread.Start();
        thread.Join();
        return thrown;
    }
}
