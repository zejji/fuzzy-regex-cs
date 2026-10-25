using System.Buffers;
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
/// <para>
/// The class runs alone (D112). The rest of the suite, running alongside, collected often enough
/// that every one of a measurement's ten attempts saw a collection, twice in 30 full runs; alone,
/// a collection lands in an attempt only when this thread's own allocations cause it. Some tests
/// here also collect on purpose, which would disturb each other's measurements.
/// </para>
/// </remarks>
[SkipUnderStryker]
[NotInParallel]
public sealed class AllocationTests
{
    [Test]
    [Arguments(@"(\w+)@(\w+)\.com", "write to someone@example.com today", FuzzyRegexOptions.None)]
    [Arguments(@"(\w+)@(\w+)\.com", "no address in this one", FuzzyRegexOptions.None)]
    [Arguments("(?:amber lantern works){e<=2}", "the Amber Lantern Wroks are shut", FuzzyRegexOptions.IgnoreCase)]
    [Arguments("(?:amber lantern works){e<=2}", "nothing close to it here", FuzzyRegexOptions.IgnoreCase)]
    // D17: a repeat around a tested group, with no empty iteration, records no state.
    [Arguments(@"(a)(?:\1|b)*c", "abababac", FuzzyRegexOptions.None)]
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

    /// <summary>
    /// D103: every entry to a conditional saves each repeat's guard lists (upstream's
    /// <c>push_repeats</c>), and the search enters this one about 10 n² times. D85's keyed tail
    /// lists were saved as a new dictionary of copied lists, so a warm call allocated 105,408,000
    /// bytes at n = 200 (0 before D85). They now go onto the byte stack like a plain list's spans.
    /// </summary>
    [Test]
    public void A_conditional_inside_a_repeat_with_a_keyed_tail_allocates_nothing_when_warm()
    {
        string subject = string.Concat(Enumerable.Repeat("aaaa ", 200)) + "!";
        bool actual = true;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"(\w)(?:(?(?=\w)\w+|\s))*\1$");
            _ = regex.IsMatch(subject);
            return () => actual = regex.IsMatch(subject);
        });

        actual.Should().BeFalse();
        allocated.Should().Be(0, "a warm predicate allocated nothing on this pattern before D85");
    }

    /// <summary>
    /// D103 with many keys: group 1 takes every word in turn, so each save holds up to 24 keyed
    /// lists. Main allocated 191,812,104 bytes for a warm <c>Match</c> here (2026-10-19, against
    /// 120 now, the match object); the lists now go onto the byte stack instead.
    /// </summary>
    [Test]
    public void A_conditional_saving_many_keyed_tail_lists_allocates_nothing_when_warm()
    {
        string subject = string.Concat(Enumerable.Range(0, 50).Select(static i => Word(i) + " ")) + "!";
        bool actual = true;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"(?:(\w+) )*(?:(?(?=\w)\w+|\s))*\1$");
            _ = regex.IsMatch(subject);
            return () => actual = regex.IsMatch(subject);
        });

        actual.Should().BeFalse();
        allocated.Should().Be(0, "the keyed lists are saved on the byte stack, which a warm call reuses");

        // a, b, ..., z, ab, bb, ...: distinct words, so group 1 never repeats a value.
        static string Word(int i)
        {
            var sb = new System.Text.StringBuilder();
            do
            {
                sb.Append((char)('a' + (i % 26)));
                i /= 26;
            } while (i > 0);

            return sb.ToString();
        }
    }

    [Test]
    public void The_empty_iteration_record_allocates_only_for_the_states_it_keeps()
    {
        // D17. Every position here makes one empty iteration that sets group 1, so the record
        // keeps one state per position. A kept state is one array of seven longs (the position, the
        // count, three error counts and group 1's two ends), 24 + 7 * 8 = 80 bytes, and nothing
        // else: a lookup must not allocate a key of its own, and with a key per lookup, 10,000
        // characters allocated 6.6 GB (review, 2026-09-29). The difference of two lengths is the
        // part that grows with the subject, so a cost paid once per call does not blur it.
        //
        // D32: both subjects are short enough that the backtracking stack stays inside the 64 KB a
        // warm state keeps (MatchState._cachedStackLimit; 256 characters still rented nothing,
        // 300 rented 12 buffers). A longer one grows the stack from ArrayPool<byte>.Shared on every
        // call, and that pool empties its per-thread cache after a gen2 collection when memory is
        // short, so the call then allocates the stack afresh: 961,600 B instead of 437,064 B at
        // 2,000 characters, reproduced with DOTNET_GCHighMemPercent set just above the machine's
        // memory load (2026-09-29).
        (long shorter, bool shorterMatched) = Measure(64);
        (long longer, bool longerMatched) = Measure(128);

        shorterMatched.Should().BeFalse();
        longerMatched.Should().BeFalse();
        (longer - shorter).Should().Be(64 * 80, "each extra position keeps one state, an array of seven longs");

        static (long Allocated, bool Matched) Measure(int length)
        {
            string subject = new string('a', length) + "b";
            bool actual = true;

            long allocated = AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"^(?:(?=(a))|a)*\1?$");
                _ = regex.IsMatch(subject);
                return () => actual = regex.IsMatch(subject);
            });

            return (allocated, actual);
        }
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

    /// <summary>
    /// D124 added partial-matching tables built at compile time. A pattern with no lookaround or
    /// condition test, or with no atomic group around a positive lookaround, never reads them, so
    /// compiling it allocates no more than before D124. The ceilings are a16e35a0's Debug counts
    /// (the BDN gate of 2026-10-25 found +760 B on <c>UncachedFuzzyIsMatch</c> and +8 B on every
    /// compile row).
    /// </summary>
    /// <param name="pattern">The pattern, one of the BDN compile rows'.</param>
    /// <param name="ceiling">a16e35a0's bytes for one compile, Debug.</param>
    [Test]
    [Arguments(@"(?:needle){e<=1}", 13_344)]
    [Arguments(@"(quick|slow)\s+(?<colour>[a-z]+)\s+fox", 33_176)]
    public void Compiling_a_pattern_without_lookarounds_allocates_what_it_did_before_D124(string pattern, long ceiling)
    {
        _ = new FuzzyRegex(pattern);

        long allocated = AllocatedBy(() => () => _ = new FuzzyRegex(pattern));

        allocated.Should().BeLessThanOrEqualTo(ceiling, "nothing reads the partial-matching tables here");
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
        // The pool is the test's own (D32): ArrayPool<char>.Shared empties its per-thread cache
        // after a gen2 collection when memory is short, and the second call would then allocate
        // the 2 MB buffer again, which says nothing about the copy. The public overloads pass
        // Shared to these.
        char[] buffer = new char[1 << 20];
        buffer.AsSpan().Fill('a');
        "cat".CopyTo(buffer.AsSpan(buffer.Length - 10));
        bool found = false;
        int count = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new("cat");
            ArrayPool<char> pool = ArrayPool<char>.Create();
            _ = regex.IsMatch(buffer.AsSpan(), pool, timeout: null, CancellationToken.None);
            _ = regex.Count(buffer.AsSpan(), pool, timeout: null, CancellationToken.None);
            return () =>
            {
                found = regex.IsMatch(buffer.AsSpan(), pool, timeout: null, CancellationToken.None);
                count = regex.Count(buffer.AsSpan(), pool, timeout: null, CancellationToken.None);
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
        // its copy and its state, so a warm walk to the end has nothing left to allocate. Its own
        // pool, as in the test above (D32).
        string words = string.Concat(Enumerable.Repeat("word ", 20_000));
        int count = 0;

        long allocated = AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"\w+");
            ArrayPool<char> pool = ArrayPool<char>.Create();
            _ = Walk(regex, words, pool);
            return () => count = Walk(regex, words, pool);
        });

        count.Should().Be(20_000);
        allocated.Should().Be(0);

        static int Walk(FuzzyRegex regex, ReadOnlySpan<char> subject, ArrayPool<char> pool)
        {
            int n = 0;
            foreach (ValueMatch match in regex.EnumerateMatches(subject, pool, timeout: null, CancellationToken.None))
            {
                n += match.Length == 4 ? 1 : 0;
            }

            return n;
        }
    }

    [Test]
    public void A_match_of_a_pattern_without_groups_costs_no_more_than_its_match_object()
    {
        // The string walk builds a Match per match and nothing else. A pattern without groups shares
        // one empty group array (24 B a match before), and D83's scan record sits in Match's own
        // fields rather than a struct the runtime pads (16 B a match as a struct, 8 apart). The
        // difference of two lengths is the part that grows with the matches. Measured 2026-10-11:
        // 160 B on main, 176 with D83's struct, 128 with both changes; 120 on 9b3819c4, where the
        // Match object is 8 B smaller. D87's attempt start as a field of its own took it back to 128
        // (measured 2026-10-15 on d5910d10), so it rides in the spans array instead, and only for a
        // pattern that needs it.
        long shorter = PerWalk(1_000);
        long longer = PerWalk(2_000);

        ((longer - shorter) / 1_000).Should().BeLessThanOrEqualTo(120);

        static long PerWalk(int words)
        {
            string subject = string.Concat(Enumerable.Repeat("ab ", words));
            int count = 0;

            long allocated = AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"\w+");
                _ = regex.EnumerateMatches(subject).Count();
                return () => count = regex.EnumerateMatches(subject).Count();
            });

            count.Should().Be(words);
            return allocated;
        }
    }

    [Test]
    public void An_eager_collection_of_a_pattern_without_groups_costs_its_match_objects_and_their_list()
    {
        // The MatchesToEnd row: Matches(...).Count builds every Match up front and keeps them in a
        // list, which adds 8 B a match for the slot that holds it. Measured 2026-10-15: 128 B a
        // match on 9b3819c4, 136 on d5910d10 (the D87 field).
        long shorter = PerCollection(1_000);
        long longer = PerCollection(2_000);

        ((longer - shorter) / 1_000).Should().BeLessThanOrEqualTo(128);

        static long PerCollection(int words)
        {
            string subject = string.Concat(Enumerable.Repeat("ab ", words));
            int count = 0;

            long allocated = AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"\w+");
                _ = regex.Matches(subject).Count;
                return () => count = regex.Matches(subject).Count;
            });

            count.Should().Be(words);
            return allocated;
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
        // A gen0 collection, so only the pause, not a gen2 count, can force the repeat.
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
                    GC.Collect(0);
#pragma warning restore S1215
                }
            };
        });

        allocated.Should().BeGreaterThanOrEqualTo(1024, "the rebuilt scenario allocates again in the repeat");
        attempt.Should().BeGreaterThan(1, "the collection inside the first attempt forces a repeat");
    }

    [Test]
    public void A_gen2_collection_between_the_warm_up_and_the_measured_call_forces_a_repeat()
    {
        // The flake of 2026-10-18, on demand. A gen2 collection under high memory pressure empties
        // ArrayPool.Shared's per-thread slots, so a warm template Replace rented a fresh 8,192-piece
        // array and counted 131 KB more than its answer. Here the first attempt's warm-up fills a
        // cache, a gen2 collection follows, and the cache is dropped as the pool's trim drops it.
        int attempt = 0;
        byte[]? cache = null;

        long allocated = AllocatedBy(() =>
        {
            attempt++;
            cache ??= new byte[1024];
            if (attempt == 1)
            {
#pragma warning disable S1215 // A gen2 collection after the warm-up is the test.
                GC.Collect();
#pragma warning restore S1215
                cache = null;
            }

            return () => cache ??= new byte[1024];
        });

        allocated.Should().Be(0, "the repeat measures the warm call");
        attempt.Should().BeGreaterThan(1, "the gen2 collection after the warm-up forces a repeat");
    }

    [Test]
    public void A_pool_trim_still_pending_from_an_earlier_collection_lands_before_the_warm_up()
    {
        // The pool trims in a gen2 callback, which runs on the finalizer thread after the
        // collection, so a collection just before the measurement can trim in the middle of it.
        // Here a finalizer stands in for that callback: it is queued before the measurement and
        // drops the cache a moment later, by when the warm-up has filled it.
        TrimStandIn trim = new();
        QueueTrimStandIn(trim);
#pragma warning disable S1215 // Queueing a finalizer before the measurement is the test.
        GC.Collect();
#pragma warning restore S1215

        long allocated = AllocatedBy(() =>
        {
            trim.Cache ??= new byte[1024];
            trim.Ran.Wait(TimeSpan.FromSeconds(10));
            return () => trim.Cache ??= new byte[1024];
        });

        trim.Ran.IsSet.Should().BeTrue("the stand-in trim ran");
        allocated.Should().Be(0, "the trim landed before the warm-up, which refilled the cache");
    }

    [Test]
    public void A_call_that_a_collection_disturbs_on_every_attempt_is_reported_rather_than_measured()
    {
        // D112. Under the full parallel suite a run of collections can disturb all ten attempts,
        // and the helper used to hand back the last one's figure anyway. That figure can carry up
        // to 8 KB of a retired allocation context (see AllocatedBy), so a warm span walk counted
        // 7,592 B and the empty-iteration test's shorter subject counted 4,408 B too many. Here a
        // collection inside the call disturbs every attempt on purpose.
        Action measure = static () =>
            AllocatedBy(static () =>
                static () =>
                {
#pragma warning disable S1215 // A collection inside every attempt is the test.
                    GC.Collect(0);
#pragma warning restore S1215
                }
            );

        measure.Should().Throw<InvalidOperationException>("no attempt measured the call alone");
    }

    /// <summary>Makes a finalizable object that drops <paramref name="trim"/>'s cache when it is finalized.</summary>
    /// <param name="trim">The cache the finalizer drops.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void QueueTrimStandIn(TrimStandIn trim) => _ = new TrimOnFinalize(trim);

    /// <summary>A cache, and a signal that the stand-in trim has dropped it.</summary>
    private sealed class TrimStandIn
    {
        internal byte[]? Cache { get; set; }

        internal ManualResetEventSlim Ran { get; } = new();
    }

    /// <summary>Drops a cache from its finalizer, after a pause, as a late gen2 callback would.</summary>
    /// <param name="trim">The cache to drop.</param>
    private sealed class TrimOnFinalize(TrimStandIn trim)
    {
#pragma warning disable MA0055 // The finalizer stands in for the pool's gen2 callback, itself a finalizer.
        ~TrimOnFinalize()
#pragma warning restore MA0055
        {
            Thread.Sleep(200);
            trim.Cache = null;
            trim.Ran.Set();
        }
    }

    /// <summary>
    /// The bytes the call that <paramref name="arrange"/> returns allocates on this thread, from a
    /// run of it that no garbage collection paused.
    /// </summary>
    /// <remarks>
    /// <para>
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
    /// the repeat too. If all ten attempts are disturbed, the helper throws rather than return a
    /// figure it knows may be wrong (D112): under the full parallel suite that happened twice in
    /// 30 runs, and the last attempt's figure then carried 4,408-7,592 B that the call never
    /// allocated. The tests that use it run alone (<c>[NotInParallel]</c>) for the same reason.
    /// </para>
    /// <para>
    /// The warm-up inside <paramref name="arrange"/> can also be undone before the call.
    /// <see cref="System.Buffers.ArrayPool{T}.Shared"/> trims itself from a gen2 callback, and
    /// under high memory pressure (90% of the high-load threshold) that trim empties every
    /// thread's slots at once (dotnet/runtime v10.0.12
    /// <c>src/libraries/System.Private.CoreLib/src/System/Buffers/SharedArrayPool.cs</c> :183-231,
    /// registered at :288; <c>Utilities.cs</c> :40-47), so a call that rents a large array
    /// allocates it afresh. A loaded full-suite run saw exactly that on 2026-10-18: a template
    /// Replace counted one fresh 8,192-piece array, 131 KB. So an attempt is also repeated when the
    /// gen2 collection count moves between the start of its warm-up and the end of its call,
    /// and each attempt first waits for pending finalizers, which is where the gen2 callbacks run,
    /// so a trim left over from an earlier collection lands before the warm-up, not after it.
    /// </para>
    /// </remarks>
    /// <param name="arrange">
    /// Builds the scenario from scratch (the pattern, its warm-up calls, anything that disturbs it)
    /// and returns the call to measure. It may run more than once, each time before its own call.
    /// </param>
    /// <returns>The bytes allocated.</returns>
    internal static long AllocatedBy(Func<Action> arrange)
    {
        long allocated = 0;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            GC.WaitForPendingFinalizers();
            int gen2Collections = GC.CollectionCount(2);
            Action call = arrange();
            TimeSpan paused = GC.GetTotalPauseDuration();
            long before = GC.GetAllocatedBytesForCurrentThread();
            call();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (GC.GetTotalPauseDuration() == paused && GC.CollectionCount(2) == gen2Collections)
            {
                return allocated;
            }
        }

        throw new InvalidOperationException(
            $"A collection disturbed all ten attempts, so none measured the call alone; the last counted {allocated:N0} B. A call that allocates enough to collect every time does this too."
        );
    }
}
