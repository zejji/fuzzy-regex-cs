using System.Runtime.ExceptionServices;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Compiling a pattern that starts by turning on a global inline flag throws nothing (D140).
/// Upstream signals "a global flag was turned on" by raising <c>_UnscopedFlagSet</c> and parsing
/// the pattern again; the port did the same with an exception, about 3 us a throw, so
/// <c>(?e)abc</c> took several times as long to compile as <c>abc</c> with the option. The port now
/// reads the leading flag groups before the first parse.
/// </summary>
public sealed class LeadingGlobalFlagsWithoutExceptionTests
{
    /// <summary>
    /// No first-chance exception is raised on this thread while the pattern compiles. The handler
    /// filters on the thread because other tests run in parallel and may throw on purpose.
    /// </summary>
    /// <param name="pattern">A pattern whose leading groups turn on global flags.</param>
    [Test]
    [Arguments("(?e)abc")]
    [Arguments("(?be)(?:hello){e<=2}")]
    [Arguments("(?b)(?e)x")]
    [Arguments("(?r)abc")]
    [Arguments("(?V1)[[a-z]--[aeiou]]")]
    public void Compiling_a_pattern_with_leading_global_flags_raises_no_exception(string pattern)
    {
        int thread = Environment.CurrentManagedThreadId;
        List<string> raised = [];

        void OnFirstChance(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == thread)
            {
                raised.Add(e.Exception.GetType().Name);
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
        try
        {
            _ = new FuzzyRegex(pattern);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance;
        }

        raised.Should().BeEmpty();
    }

    /// <summary>
    /// The flags of every leading flag group reach the pattern, as the parse's restarts made them.
    /// </summary>
    [Test]
    public void Leading_global_flags_reach_the_pattern()
    {
        new FuzzyRegex("(?b)(?e)x")
            .Options.Should()
            .HaveFlag(FuzzyRegexOptions.BestMatch)
            .And.HaveFlag(FuzzyRegexOptions.EnhanceMatch);
    }

    /// <summary>
    /// Only groups at the very start are read before the parse: <c>(?e)</c> inside a set is three
    /// characters, not a flag group.
    /// </summary>
    [Test]
    public void A_flag_group_lookalike_after_the_start_sets_nothing()
    {
        new FuzzyRegex("[(?e)]abc").Options.Should().NotHaveFlag(FuzzyRegexOptions.EnhanceMatch);
    }

    /// <summary>
    /// A group that turns a flag off is left to the parse, which rejects turning off a global flag
    /// before it would turn on the version. Reading <c>V0</c> first would pair it with the caller's
    /// <c>V1</c> and fail differently.
    /// </summary>
    /// <param name="pattern">A group that turns on <c>V0</c> and off a global flag.</param>
    [Test]
    [Arguments("(?V0-b)")]
    [Arguments("(?V0-V0)")]
    public void A_group_that_turns_a_global_flag_off_keeps_its_error(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.Version1);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .WithMessage("bad inline flags: cannot turn off global flag*");
    }

    /// <summary>
    /// A scoped flag in a leading group stays scoped: only the group's global flags are read
    /// ahead, so <c>i</c> in <c>(?ie:a)</c> does not reach the <c>b</c> after it.
    /// </summary>
    [Test]
    public void A_scoped_flag_in_a_leading_group_stays_scoped()
    {
        new FuzzyRegex("(?ie:a)b").IsMatch("AB").Should().BeFalse();
    }
}
