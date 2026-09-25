using System.Diagnostics;
using System.Runtime;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S61. What a repeated call on one compiled pattern allocates once the pattern is warm. The
/// benchmarks measure the same thing over 100,000 calls (<c>ManyInputsBenchmarks</c>); these pin
/// the floor so a change that brings back a per-call allocation fails here, not in a report.
/// </summary>
/// <remarks>
/// The count is <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which counts only this thread's
/// allocations, so tests running in parallel on other threads do not disturb it; <see cref="AllocatedBy"/>
/// works around the one way they can. "Warm" means the
/// pattern has already run once on the same kind of subject: the first call builds the state the
/// pattern then keeps, as upstream does with its <c>groups_storage</c>, <c>repeats_storage</c> and
/// <c>stack_storage</c> (<c>upstream/src/_regex.c</c> :577-579).
/// </remarks>
[SkipUnderStryker]
public sealed class AllocationTests
{
    [Test]
    [Arguments(@"(\w+)@(\w+)\.com", "write to someone@example.com today", FuzzyRegexOptions.None)]
    [Arguments(@"(\w+)@(\w+)\.com", "no address in this one", FuzzyRegexOptions.None)]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut", FuzzyRegexOptions.IgnoreCase)]
    [Arguments("(?:amber lantern works){e<=2}", "nothing close to it here", FuzzyRegexOptions.IgnoreCase)]
    public void A_warm_IsMatch_allocates_nothing(string pattern, string subject, FuzzyRegexOptions options)
    {
        bool expected = false;
        bool actual = false;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(pattern, options);
            expected = regex.IsMatch(subject);
            return () => actual = regex.IsMatch(subject);
        });

        actual.Should().Be(expected);
        allocated.Should().Be(0, "a predicate on a warm pattern has nothing it needs to build");
    }

    [Test]
    [Arguments(@"\d+", "call 0123 456 7890 after six", FuzzyRegexOptions.None)]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut", FuzzyRegexOptions.IgnoreCase)]
    public void A_warm_Count_allocates_nothing(string pattern, string subject, FuzzyRegexOptions options)
    {
        // Count walks the subject with the scanner and builds no Match, so the walk's state is
        // the only thing it could allocate, and the pattern's cache already holds one.
        int expected = 0;
        int actual = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(pattern, options);
            expected = regex.Count(subject);
            return () => actual = regex.Count(subject);
        });

        actual.Should().Be(expected);
        allocated.Should().Be(0, "a count on a warm pattern has nothing it needs to build");
    }

    [Test]
    [Arguments(@"(\w+)@(\w+)\.com", "write to someone@example.com today")]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut")]
    public void A_warm_call_allocates_nothing_after_another_thread_has_emptied_the_shared_pool(
        string pattern,
        string subject
    )
    {
        // The flake of 2026-09-25: under the parallel native-AOT run a warm call now and then
        // allocated 280 B, a byte[256]. A warm state gave its three stack buffers back to
        // ArrayPool<byte>.Shared after every call, which keeps one array of a size per thread and
        // leaves the rest where any thread can take them; this does the taking on purpose. It
        // allocated 560 B here before the state kept its buffers, as upstream's state_fini keeps
        // the stack's storage on the pattern (upstream/src/_regex.c :18684).
        bool expected = false;
        bool actual = false;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(pattern, FuzzyRegexOptions.IgnoreCase);
            expected = regex.IsMatch(subject);
            Thread other = new(static () =>
            {
                List<byte[]> held = [];
                for (int i = 0; i < 200; i++)
                {
                    foreach (int size in (int[])[16, 32, 64, 128, 256, 512, 1024])
                    {
                        held.Add(System.Buffers.ArrayPool<byte>.Shared.Rent(size));
                    }
                }

                GC.KeepAlive(held);
            });
            other.Start();
            other.Join();
            return () => actual = regex.IsMatch(subject);
        });

        actual.Should().Be(expected);
        allocated.Should().Be(0, "a warm pattern keeps what its state needs, whatever other threads rent");
    }

    [Test]
    public void The_memory_overloads_read_a_megabyte_buffer_without_copying_it()
    {
        // The span overloads copy their input to a string, two bytes a character; these read the
        // buffer where it is. A 1 MB slice of a larger array makes a copy impossible to miss.
        char[] buffer = new char[(1 << 20) + 64];
        buffer.AsSpan().Fill('a');
        ReadOnlyMemory<char> slice = buffer.AsMemory(16, 1 << 20);
        // One inside the slice, near its end, and one after it that must not be seen.
        "cat".CopyTo(buffer.AsSpan(16 + (1 << 20) - 10));
        "cat".CopyTo(buffer.AsSpan(buffer.Length - 20));
        bool found = false;
        int count = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new("cat");
            _ = regex.IsMatch(slice);
            _ = regex.Count(slice);
            return () =>
            {
                found = regex.IsMatch(slice);
                count = regex.Count(slice);
            };
        });

        found.Should().BeTrue();
        count.Should().Be(1);
        allocated.Should().Be(0, "the engine reads the caller's buffer in place");
    }

    [Test]
    public void The_span_overloads_copy_a_megabyte_into_a_pooled_buffer_rather_than_a_new_string()
    {
        // A span cannot be kept, so these two still copy it, but into a buffer rented from
        // ArrayPool<char>.Shared and returned when the call ends. Before, the copy was a string of
        // two bytes a character: 2,097,176 B over this subject (owner's option (c), 2026-09-23).
        char[] buffer = new char[1 << 20];
        buffer.AsSpan().Fill('a');
        "cat".CopyTo(buffer.AsSpan(buffer.Length - 10));
        bool found = false;
        int count = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new("cat");
            _ = regex.IsMatch(buffer.AsSpan());
            _ = regex.Count(buffer.AsSpan());
            return () =>
            {
                found = regex.IsMatch(buffer.AsSpan());
                count = regex.Count(buffer.AsSpan());
            };
        });

        found.Should().BeTrue();
        count.Should().Be(1);
        allocated.Should().Be(0, "the second call takes the buffer the first one gave back to the pool");
    }

    [Test]
    public void A_warm_span_walk_over_many_matches_allocates_nothing()
    {
        // The owner's gate for ValueMatchEnumerator (DECISIONS 2026-09-22): a measured gain. The
        // string walk builds a Match per match; this one yields an index and a length, and borrows
        // its copy and its state, so a warm walk to the end has nothing left to allocate.
        string words = string.Concat(Enumerable.Repeat("word ", 20_000));
        int count = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"\w+");
            _ = Walk(regex, words);
            return () => count = Walk(regex, words);
        });

        count.Should().Be(20_000);
        allocated.Should().Be(0);

        static int Walk(FuzzyRegex regex, ReadOnlySpan<char> subject)
        {
            int n = 0;
            foreach (ValueMatch match in regex.EnumerateMatches(subject))
            {
                n += match.Length == 4 ? 1 : 0;
            }

            return n;
        }
    }

    [Test]
    public void A_background_collection_that_pauses_the_measured_call_is_not_counted_as_its_allocation()
    {
        // The flake of 2026-09-25, on demand. A warm span walk under the parallel native-AOT run
        // counted 6,904-8,304 B now and then, in 5 full runs of 20. This thread takes a
        // fresh allocation context just after a background collection has started, and the call
        // below allocates nothing while it waits for that collection's closing pause, which
        // empties the context without taking its unused part off the count (see AllocatedBy).
        // Measured without the repeat it counted 8,112 B, about one allocation quantum, every time.
        if (GCSettings.LatencyMode == GCLatencyMode.Batch)
        {
            TUnit.Core.Skip.Test("background collections are turned off in this process");
        }

        // Enough live objects that marking them outlasts everything up to the measured call.
        object[] graph = new object[500_000];
        for (int i = 0; i < graph.Length; i++)
        {
            graph[i] = new byte[16];
        }

        // Only the first attempt starts a collection and waits for its pause; the repeat that
        // pause forces measures an empty call.
        int attempt = 0;

        long allocated = AllocatedBy(() =>
        {
            attempt++;
            if (attempt > 1)
            {
                return static () => { };
            }

#pragma warning disable S1215 // Starting a background collection at a known moment is the test.
            GC.Collect(); // A blocking collection first, so no background one is still running.
            GC.Collect(2, GCCollectionMode.Forced, blocking: false);
#pragma warning restore S1215
            TimeSpan started = GC.GetTotalPauseDuration();
            graph[0] = new object(); // The fresh context, nearly all of it unused.
            return () =>
            {
                long giveUp = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                while (GC.GetTotalPauseDuration() == started && Stopwatch.GetTimestamp() < giveUp)
                {
                    // Allocates nothing; waits at most a second for the next pause.
                }
            };
        });

        GC.KeepAlive(graph);
        allocated.Should().Be(0, "the call allocated nothing; only a collection paused it");
    }

    [Test]
    public void An_allocation_made_only_by_the_first_call_is_counted_when_a_pause_lands_in_it()
    {
        // The call allocates 1 KB the first time it runs after its setup, as a warm call does when
        // it re-rents a pool buffer another thread took, and the first attempt also collects inside
        // the call, so a pause lands in it. Repeating only the call would count 0 in the repeat.
        int attempt = 0;

        long allocated = AllocatedBy(() =>
        {
            attempt++;
            bool collect = attempt == 1;
            bool allocate = true;
            return () =>
            {
                if (allocate)
                {
                    allocate = false;
                    GC.KeepAlive(new byte[1024]);
                }

                if (collect)
                {
#pragma warning disable S1215 // A pause inside the first attempt is the test.
                    GC.Collect();
#pragma warning restore S1215
                }
            };
        });

        allocated.Should().BeGreaterThanOrEqualTo(1024, "the rebuilt scenario allocates again in the repeat");
        attempt.Should().BeGreaterThan(1, "the collection inside the first attempt forces a repeat");
    }

    /// <summary>
    /// The bytes the call that <paramref name="arrange"/> returns allocates on this thread, from a
    /// run of it that no garbage collection paused.
    /// </summary>
    /// <remarks>
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/> counts a thread's allocation context as
    /// allocated when the thread takes it, and a collection that retires the context takes the unused
    /// part back off (<c>fix_allocation_context</c>, dotnet/runtime v10.0.12
    /// <c>src/coreclr/gc/gc.cpp</c> :8029-8031). The pause that ends a background collection's marking
    /// retires every thread's context through <c>void_allocation</c> (:8053, called at :39486), which
    /// skips that step, so a thread that allocated nothing is charged whatever its context had left,
    /// up to about 8 KB. It happens under JIT and native AOT alike. Only a pause can cause it, and the
    /// collector adds a pause to <see cref="GC.GetTotalPauseDuration"/> before it lets the threads go
    /// (:46874-46876), so a run that saw no pause is exact. A run that saw one is repeated, and the
    /// repeat rebuilds the whole scenario, so an allocation it causes, once or every time, shows in
    /// the repeat too. After ten attempts the last one's figure stands, pause or not.
    /// </remarks>
    /// <param name="arrange">
    /// Builds the scenario from scratch (the pattern, its warm-up calls, anything that disturbs it)
    /// and returns the call to measure. It may run more than once, each time before its own call.
    /// </param>
    /// <returns>The bytes allocated.</returns>
    private static long AllocatedBy(Func<Action> arrange)
    {
        long allocated = 0;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Action call = arrange();
            TimeSpan paused = GC.GetTotalPauseDuration();
            long before = GC.GetAllocatedBytesForCurrentThread();
            call();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (GC.GetTotalPauseDuration() == paused)
            {
                break;
            }
        }

        return allocated;
    }
}
