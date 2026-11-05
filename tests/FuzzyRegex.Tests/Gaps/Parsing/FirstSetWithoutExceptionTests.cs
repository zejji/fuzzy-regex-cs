using System.Runtime.ExceptionServices;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Compiling a pattern whose start has no first set throws nothing (D139). Upstream signals "no
/// first set" by raising <c>_FirstSetError</c> and catching it in <c>_compile</c>; the port used to
/// do the same with an exception, and a CPU profile on 2026-11-04 put about 70% of a
/// construct-and-match call for <c>(?:needle){e&lt;=1}</c> in .NET exception dispatch. Every fuzzy
/// pattern and every pattern like <c>.*foo</c> took that path, so the port returns
/// <see langword="null"/> instead.
/// </summary>
public sealed class FirstSetWithoutExceptionTests
{
    /// <summary>
    /// No first-chance exception is raised on this thread while the pattern compiles. The handler
    /// filters on the thread because other tests run in parallel and may throw on purpose.
    /// </summary>
    /// <param name="pattern">A pattern with no first set: a fuzzy section, or a leading repeat of any character.</param>
    [Test]
    [Arguments("(?:needle){e<=1}")]
    [Arguments(".*foo")]
    [Arguments("(?:abc){i<=1,d<=1}")]
    public void Compiling_a_pattern_with_no_first_set_raises_no_exception(string pattern)
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
}
