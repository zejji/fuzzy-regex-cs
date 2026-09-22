using System.Buffers;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S52b. A double return hands one pooled buffer to two callers, and through them to two threads
/// that each believe they own it - so pool discipline is a thread-safety property, not a tidiness
/// one. PERMANENT.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is scoped to <see cref="ByteStack"/> rather than run over the whole suite.</b> The
/// slice file asked for a debug pool wrapper the entire suite runs under. Doing that needs a seam
/// the engine reads at the point it builds its stacks, and the only such seam that reaches every
/// <see cref="MatchState"/> the suite creates is a settable static on the library - which is
/// precisely the shared mutable state
/// <see cref="ThreadSafetyTests.Every_static_field_in_the_library_is_readonly_or_const"/> forbids,
/// and which S52b exists to keep out. It is not a trade worth making, because the thing it would
/// buy is already covered: every <c>ArrayPool</c> call in <c>src/FuzzyRegex</c> is the one
/// <c>Rent</c> and the two <c>Return</c>s inside <see cref="ByteStack"/> (measured 2026-09-16), so
/// exercising that class exhaustively against a tracking pool covers the same code the whole-suite
/// run would have.
/// </para>
/// <para>
/// What the suite-wide run would additionally have caught - a caller that leaks a state without
/// disposing it - is covered by
/// <see cref="Disposing_a_match_state_releases_every_one_of_its_stacks"/>.
/// </para>
/// </remarks>
public sealed class PoolDisciplineTests
{
    [Test]
    public void Growing_a_stack_returns_the_buffer_it_outgrew_and_never_the_same_one_twice()
    {
        var pool = new TrackingPool();
        using var stack = new ByteStack(pool);

        // Enough pushes to force several growth steps: the first block allocates 256 and every
        // later one doubles, so this crosses the boundary repeatedly.
        for (int i = 0; i < 4_000; i++)
        {
            stack.Push((byte)(i % 256));
        }

        pool.Rented.Should().BeGreaterThan(1, "growing past 64 bytes has to rent more than once");
        pool.Outstanding.Should().Be(1, "exactly the buffer currently in use is still out");
        pool.DoubleReturns.Should().BeEmpty();
        pool.ForeignReturns.Should().BeEmpty();
    }

    [Test]
    public void Disposing_a_stack_returns_its_buffer_exactly_once()
    {
        var pool = new TrackingPool();
        var stack = new ByteStack(pool);

        stack.PushBlock(new byte[1_000]);
        stack.Dispose();

        pool.Outstanding.Should().Be(0, "a disposed stack holds nothing");
        pool.DoubleReturns.Should().BeEmpty();
    }

    [Test]
    public void Disposing_a_stack_twice_does_not_return_its_buffer_twice()
    {
        var pool = new TrackingPool();
        var stack = new ByteStack(pool);

        stack.PushBlock(new byte[1_000]);
        stack.Dispose();
        stack.Dispose();

        pool.DoubleReturns.Should()
            .BeEmpty(
                "a buffer returned twice is owned by two callers at once, which is the pool bug "
                    + "that turns into a data race"
            );
        pool.Outstanding.Should().Be(0);
    }

    [Test]
    public void Disposing_a_stack_that_never_grew_returns_nothing()
    {
        var pool = new TrackingPool();
        var stack = new ByteStack(pool);

        stack.Dispose();

        pool.Rented.Should().Be(0, "an untouched stack rents nothing");
        pool.ForeignReturns.Should().BeEmpty("and so has nothing to give back");
    }

    [Test]
    public void Resetting_a_stack_keeps_its_buffer_rather_than_returning_it()
    {
        var pool = new TrackingPool();
        using var stack = new ByteStack(pool);

        stack.PushBlock(new byte[1_000]);
        int rentedBeforeReset = pool.Rented;
        stack.Reset();
        stack.PushBlock(new byte[1_000]);

        pool.Rented.Should().Be(rentedBeforeReset, "Reset is upstream's ByteStack_reset, which keeps the storage");
        pool.Outstanding.Should().Be(1);
    }

    [Test]
    public void Disposing_a_match_state_releases_every_one_of_its_stacks()
    {
        // The leak half of the rule. A state owns three stacks and returns their buffers in its own
        // Dispose; a stack this list misses would keep its buffer for the garbage collector to
        // find, which is a leak rather than a race, but it is the same one-line mistake.
        FuzzyRegex pattern = new("(a)+b");

        System.Reflection.FieldInfo[] stackFields =
        [
            .. typeof(MatchState)
                .GetFields(
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.DeclaredOnly
                )
                .Where(static field => field.FieldType == typeof(ByteStack)),
        ];

        stackFields.Should().HaveCount(3, "a MatchState owns the three byte stacks upstream's RE_State does");

        MatchState state = MatchState.Create(
            pattern.PatternObject,
            "aaab",
            0,
            4,
            overlapped: false,
            partial: false,
            visibleCaptures: true,
            matchAll: false,
            pattern.PatternLimits
        );

        foreach (System.Reflection.FieldInfo field in stackFields)
        {
            ((ByteStack)field.GetValue(state)!).PushBlock(new byte[1_000]);
        }

        state.Dispose();

        foreach (System.Reflection.FieldInfo field in stackFields)
        {
            StorageOf((ByteStack)field.GetValue(state)!)
                .Should()
                .BeEmpty($"MatchState.Dispose must release {field.Name}");
        }
    }

    [Test]
    public void A_lazy_walk_rents_one_state_for_the_whole_walk_rather_than_one_per_match()
    {
        // S61. Before it, each step of EnumerateMatches built and disposed a state of its own, so a
        // walk rented its stacks once per match. Holding one state across the walk rents them once:
        // a stack that grows rents again, so the bound is "a handful", not "one", and a walk of
        // two hundred matches sits far above it under the old shape.
        var pool = new TrackingPool();
        (FuzzyRegex words, string subject) = AMegabyteOfWords();

        int matches = Iteration
            .Enumerate(words, subject, 0, subject.Length, overlapped: false, partial: false, words.PatternLimits, pool)
            .Take(200)
            .Count();

        matches.Should().Be(200);
        pool.Rented.Should().BeGreaterThan(0, "the walk has to use its stacks for the bound below to mean anything");
        pool.Rented.Should()
            .BeLessThan(10, "one state serves the whole walk, so its stacks are rented once, not per match");
    }

    [Test]
    public void A_lazy_walk_abandoned_after_two_matches_returns_every_buffer_it_rented()
    {
        // The hazard S58 named: a state held across a yield return is a state an abandoned iterator
        // must still release. A foreach that breaks disposes its enumerator, and the enumerator's
        // Dispose runs the walk's `using`, which is what returns the stacks.
        var pool = new TrackingPool();
        (FuzzyRegex words, string subject) = AMegabyteOfWords();

        using (
            IEnumerator<Match> walk = Iteration
                .Enumerate(
                    words,
                    subject,
                    0,
                    subject.Length,
                    overlapped: false,
                    partial: false,
                    words.PatternLimits,
                    pool
                )
                .GetEnumerator()
        )
        {
            walk.MoveNext().Should().BeTrue();
            walk.MoveNext().Should().BeTrue();

            pool.Outstanding.Should()
                .BeGreaterThan(0, "mid-walk the state is still held, with its stacks, for the next match");
        }

        pool.Outstanding.Should().Be(0, "disposing the abandoned walk hands back every buffer it rented");
        pool.DoubleReturns.Should().BeEmpty();
        pool.ForeignReturns.Should().BeEmpty();
    }

    /// <summary>
    /// A pattern whose every match pushes onto the backtracking stack, and a subject of about a
    /// million characters holding two hundred thousand matches of it.
    /// </summary>
    /// <returns>The pattern and the subject.</returns>
    private static (FuzzyRegex Pattern, string Subject) AMegabyteOfWords() =>
        (new FuzzyRegex(@"(\w+)\s"), string.Concat(Enumerable.Repeat("word ", 200_000)));

    /// <summary>The backing array a stack currently holds, which is empty once it has let it go.</summary>
    /// <param name="stack">The stack to read.</param>
    /// <returns>The array.</returns>
    private static byte[] StorageOf(ByteStack stack) =>
        (byte[])
            typeof(ByteStack)
                .GetField(
                    "_storage",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .GetValue(stack)!;

    /// <summary>
    /// An <see cref="ArrayPool{T}"/> that refuses to forget anything: it records every buffer it
    /// has handed out and complains when one comes back that it did not lend, or comes back twice.
    /// </summary>
    /// <remarks>
    /// The sets use the default comparer on purpose: an array does not override
    /// <see cref="object.Equals(object)"/>, so that comparer already is reference identity, which is
    /// the only notion of "the same buffer" a pool has.
    /// </remarks>
    private sealed class TrackingPool : ArrayPool<byte>
    {
        private readonly HashSet<byte[]> _out = [];
        private readonly HashSet<byte[]> _everSeen = [];
        private readonly List<string> _doubleReturns = [];
        private readonly List<string> _foreignReturns = [];
        private readonly Lock _gate = new();

        /// <summary>How many buffers have been rented in total.</summary>
        internal int Rented { get; private set; }

        /// <summary>How many rented buffers have not come back.</summary>
        internal int Outstanding
        {
            get
            {
                lock (_gate)
                {
                    return _out.Count;
                }
            }
        }

        /// <summary>Every buffer returned while it was not rented.</summary>
        internal IReadOnlyList<string> DoubleReturns
        {
            get
            {
                lock (_gate)
                {
                    return [.. _doubleReturns];
                }
            }
        }

        /// <summary>Every buffer returned that this pool never lent.</summary>
        internal IReadOnlyList<string> ForeignReturns
        {
            get
            {
                lock (_gate)
                {
                    return [.. _foreignReturns];
                }
            }
        }

        public override byte[] Rent(int minimumLength)
        {
            // Allocated rather than pooled, deliberately: a pool that reused buffers could not tell
            // "returned twice" from "rented again", which is the distinction under test.
            var buffer = new byte[Math.Max(minimumLength, 1)];

            lock (_gate)
            {
                Rented++;
                _out.Add(buffer);
                _everSeen.Add(buffer);
            }

            return buffer;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            lock (_gate)
            {
                if (_out.Remove(array))
                {
                    if (clearArray)
                    {
                        Array.Clear(array);
                    }

                    return;
                }

                // Distinguishing the two matters: a double return is a lifetime bug in the caller,
                // a foreign return is a buffer belonging to somebody else entirely.
                (_everSeen.Contains(array) ? _doubleReturns : _foreignReturns).Add($"length {array.Length}");
            }
        }
    }
}
