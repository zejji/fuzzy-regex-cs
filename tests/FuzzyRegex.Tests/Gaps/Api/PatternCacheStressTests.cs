using System.Collections.Concurrent;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S59. The pattern cache is the first shared mutable state in this library, so it is tested the
/// way <see cref="ThreadSafetyStressTests"/> tests a shared compiled pattern: against real threads,
/// checking every answer and the bound.
/// </summary>
/// <remarks>
/// <para>
/// Deterministic, as the engine is: a subject either gives the answer it gave sequentially or it
/// does not, so any mismatch and any exception is a race rather than something to tolerate.
/// </para>
/// <para>
/// The bound test runs on its own <see cref="PatternCache"/> rather than on
/// <see cref="FuzzyRegex"/>'s shared one, because a bound set on the shared cache would be read by
/// whatever else the test host is running in parallel and the test would be measuring that too.
/// The second test does use the shared cache, at its default size and without touching it, which is
/// the path a caller's program actually takes.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class PatternCacheStressTests
{
    /// <summary>Four per core, so threads are descheduled mid-compile and interleave.</summary>
    private static int ThreadCount => Environment.ProcessorCount * 4;

    /// <summary>More distinct patterns than the bound below, so eviction runs throughout.</summary>
    private static readonly (string Pattern, FuzzyRegexOptions Options, string Subject)[] _work =
    [
        ("a(b)c", FuzzyRegexOptions.None, "xxabcxx"),
        ("a(b)c", FuzzyRegexOptions.IgnoreCase, "xxABCxx"),
        (@"\d+", FuzzyRegexOptions.None, "abc 1234 def"),
        (@"(\w+)\s+\1", FuzzyRegexOptions.None, "the the cat"),
        ("(?i)a", FuzzyRegexOptions.None, "zzAzz"),
        ("(?i)a", FuzzyRegexOptions.IgnoreCase, "zzAzz"),
        ("(foo|bar)+", FuzzyRegexOptions.None, "foobarfoo"),
        ("(?:a+)(*SKIP)b|ac", FuzzyRegexOptions.None, "aaac"),
        ("(?<year>[0-9]{4})-(?<month>[0-9]{2})", FuzzyRegexOptions.None, "on 2026-09 ok"),
        ("colou?r", FuzzyRegexOptions.None, "the colour red"),
        ("(?e)(cat){e<=1}", FuzzyRegexOptions.None, "the bat sat"),
        (@"[[:alpha:]]+", FuzzyRegexOptions.None, "12 abc 34"),
    ];

    private const int _bound = 4;

    [Test]
    public void Many_threads_sharing_one_cache_get_the_right_pattern_and_never_exceed_the_bound()
    {
        // The baseline is compiled outside the cache, one instance per row, so it cannot agree with
        // a corruption that has already happened.
        string[] expected = [.. _work.Select(row => Render(new FuzzyRegex(row.Pattern, row.Options), row.Subject))];

        var cache = new PatternCache { Size = _bound };
        var failures = new ConcurrentBag<string>();

        Parallel.For(
            0,
            ThreadCount,
            new ParallelOptions { MaxDegreeOfParallelism = ThreadCount },
            worker =>
            {
                // Each worker starts at a different row, so the threads are spread over the work
                // instead of walking it in step and all missing on the same key.
                for (int step = 0; step < _work.Length * 4; step++)
                {
                    int index = (worker + step) % _work.Length;
                    (string pattern, FuzzyRegexOptions options, string subject) = _work[index];

                    try
                    {
                        FuzzyRegex compiled = cache.GetOrAdd(
                            pattern,
                            options,
                            FuzzyRegex.InfiniteMatchTimeout,
                            Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.DefaultVersion
                        );

                        // The cache handed back SOMETHING; these two lines are what say it handed
                        // back the right thing, which a key collision would not.
                        if (!string.Equals(compiled.Pattern, pattern, StringComparison.Ordinal))
                        {
                            failures.Add($"row {index}: asked for '{pattern}', got '{compiled.Pattern}'");
                        }

                        string actual = Render(compiled, subject);
                        if (!string.Equals(actual, expected[index], StringComparison.Ordinal))
                        {
                            failures.Add($"row {index} '{pattern}' on '{subject}': {actual} != {expected[index]}");
                        }

                        int count = cache.Count;
                        if (count > _bound)
                        {
                            failures.Add($"row {index}: cache held {count} entries, bound is {_bound}");
                        }
                    }
#pragma warning disable CA1031 // Any exception at all is a race, and the test reports it as one.
                    catch (Exception error)
#pragma warning restore CA1031
                    {
                        failures.Add($"row {index} '{pattern}': {error}");
                    }
                }
            }
        );

        failures.Should().BeEmpty();
        cache.Count.Should().BeLessThanOrEqualTo(_bound);
    }

    [Test]
    public void Many_threads_going_through_the_static_conveniences_all_get_the_right_answer()
    {
        string[] expected = [.. _work.Select(row => Render(new FuzzyRegex(row.Pattern, row.Options), row.Subject))];
        var failures = new ConcurrentBag<string>();

        Parallel.For(
            0,
            ThreadCount,
            new ParallelOptions { MaxDegreeOfParallelism = ThreadCount },
            worker =>
            {
                for (int step = 0; step < _work.Length * 4; step++)
                {
                    int index = (worker + step) % _work.Length;
                    (string pattern, FuzzyRegexOptions options, string subject) = _work[index];

                    try
                    {
                        string actual = ThreadSafetyWorkload.Render(FuzzyRegex.Match(subject, pattern, options));
                        if (!string.Equals(actual, expected[index], StringComparison.Ordinal))
                        {
                            failures.Add($"row {index} '{pattern}' on '{subject}': {actual} != {expected[index]}");
                        }
                    }
#pragma warning disable CA1031 // Any exception at all is a race, and the test reports it as one.
                    catch (Exception error)
#pragma warning restore CA1031
                    {
                        failures.Add($"row {index} '{pattern}': {error}");
                    }
                }
            }
        );

        failures.Should().BeEmpty();
    }

    /// <summary>
    /// One row's answer, rendered by the same helper <see cref="ThreadSafetyStressTests"/> uses:
    /// span, text, partial flag and every group, so a wrong pattern or a lost group changes it.
    /// </summary>
    /// <param name="compiled">The pattern to apply.</param>
    /// <param name="subject">The subject to match.</param>
    /// <returns>The rendering.</returns>
    private static string Render(FuzzyRegex compiled, string subject) =>
        ThreadSafetyWorkload.Render(compiled.Match(subject));
}
