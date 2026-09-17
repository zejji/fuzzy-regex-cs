using System.Collections.Concurrent;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S52b. The structural tests in <see cref="ThreadSafetyTests"/> say what a compiled pattern
/// <i>could</i> do; these run one against real threads and check it does not. PERMANENT, and for
/// the same reason: Phase 7 is where caches appear.
/// </summary>
/// <remarks>
/// The engine is deterministic, so there is nothing to tolerate here - a subject either gives the
/// answer it gave sequentially or it does not. Any mismatch, and any exception, is a race.
/// </remarks>
[SkipUnderStryker]
public sealed class ThreadSafetyStressTests
{
    /// <summary>
    /// The thread count the slice file asks for. Four times the core count rather than one times,
    /// so threads are descheduled mid-match and interleave at points a one-per-core run never
    /// reaches.
    /// </summary>
    private static int ThreadCount => Environment.ProcessorCount * 4;

    [Test]
    public void Every_family_answers_the_same_under_real_parallelism()
    {
        foreach (ThreadSafetyWorkload.Family family in ThreadSafetyWorkload.Families)
        {
            // The baseline is computed on ITS OWN pattern instance, one per subject, and never on
            // the instance the threads go on to share. A baseline read off the shared object would
            // be comparing that object against itself and would agree with any corruption that had
            // already happened.
            IReadOnlyList<string> expected =
            [
                .. family.Subjects.Select(subject => family.Run(family.Compile(), subject)),
            ];

            FuzzyRegex shared = family.Compile();
            var failures = new ConcurrentBag<string>();

            Parallel.For(
                0,
                ThreadCount,
                new ParallelOptions { MaxDegreeOfParallelism = ThreadCount },
                worker =>
                {
                    // Each worker starts at a different subject so the threads are spread across
                    // the work rather than all walking it in step.
                    for (int step = 0; step < family.Subjects.Count; step++)
                    {
                        int index = (worker + step) % family.Subjects.Count;

                        try
                        {
                            string actual = family.Run(shared, family.Subjects[index]);

                            if (!string.Equals(actual, expected[index], StringComparison.Ordinal))
                            {
                                failures.Add(
                                    $"{family.Name}[{index}] '{family.Subjects[index]}': "
                                        + $"expected '{expected[index]}', got '{actual}'"
                                );
                            }
                        }
#pragma warning disable CA1031 // The point of the test is to report ANY exception as a race.
                        catch (Exception exception)
#pragma warning restore CA1031
                        {
                            failures.Add($"{family.Name}[{index}] '{family.Subjects[index]}': {exception}");
                        }
                    }
                }
            );

            failures
                .Should()
                .BeEmpty(
                    "a compiled pattern is documented as safe to share between threads, and this "
                        + "engine is deterministic, so a differing answer or an exception under "
                        + "parallelism is shared mutable state and nothing else"
                );
        }
    }

    [Test]
    public void One_match_can_be_read_from_many_threads_at_once()
    {
        // Match.FuzzyChanges is the only value on the public surface that is computed on first read
        // rather than in the constructor, so it is the only place on a Match where one thread can
        // see what another is part-way through writing. Every thread reads the SAME Match, and a
        // fresh Match each round, because the race window is the first read of each one.
        // REAL THREADS, NOT Parallel.For, AND THAT IS THE WHOLE DIFFERENCE BETWEEN THIS TEST
        // WORKING AND NOT. A barrier needs its participants to exist at the same time, and
        // Parallel.For promises no such thing: it will run iterations one after another on fewer
        // threads than asked for, leaving SignalAndWait holding for participants that arrive only
        // as the pool injects them, one every half-second or so. The first draft did that, and it
        // was both a latent hang and a feeble test - against the pre-S52b field it found the tear
        // once in about 57 seconds and not at all inside a 15-second budget. Rewritten onto 64
        // dedicated threads it runs 20,000 rounds in about two seconds and caught that same defect
        // on three runs out of three. Measured 2026-09-16; the control is in the S52b closing notes.
        const int rounds = 20_000;

        FuzzyRegex pattern = new("(?:kitten){e<=3}");
        var failures = new ConcurrentBag<string>();
        Match current = pattern.Match("sitting");

        current.Success.Should().BeTrue("every round is vacuous without a match");

        // The post-phase action runs on one thread with all the others held at the barrier, so each
        // round hands every thread the same brand-new Match - and the first read of a fresh one is
        // the only moment the race exists.
        using var round = new Barrier(ThreadCount, _ => current = pattern.Match("sitting"));

        var threads = new Thread[ThreadCount];

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                // Every thread runs exactly the same number of rounds. A thread that left early
                // would strand the rest at a barrier that can no longer reach its participant
                // count, which is a hang rather than a failure.
                for (int r = 0; r < rounds; r++)
                {
                    round.SignalAndWait();

                    try
                    {
                        FuzzyChanges changes = current.FuzzyChanges;

                        if (changes.Substitutions is null || changes.Insertions is null || changes.Deletions is null)
                        {
                            failures.Add("a torn read: one of the three lists was null");
                        }
                        else if (changes.Substitutions.Count + changes.Insertions.Count + changes.Deletions.Count == 0)
                        {
                            failures.Add("a torn read: the change lists were all empty");
                        }
                    }
#pragma warning disable CA1031 // Any exception at all is the finding.
                    catch (Exception exception)
#pragma warning restore CA1031
                    {
                        failures.Add(exception.ToString());
                    }
                }
            })
            {
                IsBackground = true,
                Name = $"s52b-match-reader-{i}",
            };

            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        failures
            .Should()
            .BeEmpty(
                "this port documents a Match as readable from any thread, which is a stronger "
                    + "promise than the built-in Regex makes, so every value it exposes has to be "
                    + "published atomically"
            );
    }
}
