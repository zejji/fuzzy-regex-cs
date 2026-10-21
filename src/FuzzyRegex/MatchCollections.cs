using System.Collections;
using System.Runtime.ExceptionServices;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// The captures of one group, oldest first. Shaped after
/// <see cref="System.Text.RegularExpressions.CaptureCollection"/>.
/// </summary>
public sealed class CaptureCollection : IReadOnlyList<Capture>
{
    private readonly IReadOnlyList<Capture> _captures;

    internal CaptureCollection(IReadOnlyList<Capture> captures)
    {
        _captures = captures;
    }

    /// <summary>The number of captures.</summary>
    public int Count => _captures.Count;

    /// <summary>Gets a capture by position.</summary>
    /// <param name="index">The zero-based position of the capture.</param>
    /// <returns>The capture at that position.</returns>
    public Capture this[int index] => _captures[index];

    /// <summary>Enumerates the captures, oldest first.</summary>
    /// <returns>An enumerator over the captures.</returns>
    public IEnumerator<Capture> GetEnumerator() => _captures.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The groups of a match, indexable by number or by name. Shaped after
/// <see cref="System.Text.RegularExpressions.GroupCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// Group 0 is the whole match, which is what <see cref="Match"/> itself already is, so it needs no
/// group span of its own and <c>Groups[0]</c> is the match. Groups 1 upwards are read out of the
/// spans the engine recorded - see <c>Match.GroupAt</c>.
/// </para>
/// <para>
/// <b><c>Match.Groups</c> is an <c>IReadOnlyDictionary&lt;string, Group&gt;</c> as well as a
/// list</b>, keyed by every group's name - or its number as text where it has none. It is also
/// an <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed that way, as the built-in
/// <c>GroupCollection</c> has been since .NET 5, so a caller can ask
/// <see cref="TryGetValue"/> about a name the pattern may not have without catching anything.
/// Measured on .NET 10.0.10 (<c>tools/probes/dotnet-groupcollection-dictionary.ps1</c>,
/// 2026-09-16), the built-in's dictionary face is TOTAL rather than named-only:
/// <c>Regex.Match("ab", "(?&lt;a&gt;a)(b)(?&lt;c&gt;c)?").Groups</c> has <c>Count</c> 4 and
/// <c>Keys</c> <c>[0, 1, a, c]</c>, so every group is in it and an unnamed one is keyed by its
/// number as text. This port does the same, which makes <see cref="Keys"/> exactly
/// <see cref="FuzzyRegex.GroupNames"/> in ascending group number, the order <c>Keys</c> came
/// back in there; the same pattern gives <c>[0, a, 2, c]</c> here (measured 2026-09-16), because
/// this port numbers groups left to right as upstream does, where the built-in numbers unnamed
/// groups first. Upstream's <c>groupdict</c> is the named-only view instead, and
/// it is a filter over this one. <b>The cost is a source break</b>: a type with two
/// <see cref="IEnumerable{T}"/> faces makes <c>Groups.Select(...)</c> ambiguous, exactly as it
/// is on the built-in collection.
/// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#matchgroups-is-an-ireadonlydictionarystring-group-as-well-as-a-list">the comparison page</see>.
/// </para>
/// <para>
/// Two places where the built-in's dictionary contract and this port's older behaviour differ, and
/// this port keeps its own: the string indexer throws for a name the pattern does not have, where
/// the built-in returns an unsuccessful group (measured in the same probe, <c>Groups["nope"]</c>
/// has <c>Success</c> false and an empty <c>Name</c>); and <see cref="TryGetValue"/> therefore
/// exists as the non-throwing way to ask, which is what it is for.
/// </para>
/// </remarks>
public sealed class GroupCollection : IReadOnlyList<Group>, IReadOnlyDictionary<string, Group>
{
    private readonly Match _match;
    private readonly int _publicGroupCount;

    internal GroupCollection(Match match, int publicGroupCount)
    {
        _match = match;
        _publicGroupCount = publicGroupCount;
    }

    /// <summary>The number of groups, including group 0.</summary>
    public int Count => _publicGroupCount + 1;

    /// <summary>Gets a group by number, group 0 being the whole match.</summary>
    /// <param name="number">The group number.</param>
    /// <returns>The group, or an unsuccessful group if it did not take part in the match.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pattern has no such group.</exception>
    public Group this[int number]
    {
        get
        {
            if (number < 0 || number > _publicGroupCount)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "the pattern has no such group");
            }

            return number == 0 ? _match : _match.GroupAt(number);
        }
    }

    /// <summary>Gets a group by name.</summary>
    /// <param name="name">
    /// The group name, or - for a group that has none of its own - its number as text, which is
    /// the spelling <see cref="Keys"/> uses.
    /// </param>
    /// <returns>The group, or an unsuccessful group if it did not take part in the match.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pattern has no group of that name.</exception>
    public Group this[string name]
    {
        get
        {
            int number = NumberForKey(name);

            return number >= 0
                ? this[number]
                : throw new ArgumentOutOfRangeException(nameof(name), name, "the pattern has no such group");
        }
    }

    /// <summary>
    /// The group a dictionary key names, or <c>-1</c> if there is none. One rule, shared by the
    /// string indexer, <see cref="ContainsKey"/> and <see cref="TryGetValue"/>, so that every key
    /// <see cref="Keys"/> yields is one the other three answer for - which is the whole of the
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/> contract this type has to keep.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The group number, or <c>-1</c>.</returns>
    /// <remarks>
    /// The round trip through <c>GroupNameFromNumber</c> is what makes the numeric spelling exact
    /// rather than a second way to reach any group: on <c>(?&lt;a&gt;a)(b)</c> the key <c>"2"</c>
    /// resolves and the key <c>"1"</c> does not, because group 1's key is <c>"a"</c>. That is the
    /// built-in <c>GroupCollection</c>'s answer too - measured on .NET 10.0.10,
    /// <c>tools/probes/dotnet-groupcollection-dictionary.ps1</c>, where
    /// <c>ContainsKey("2")</c> is false on a pattern whose group 2 is named.
    /// </remarks>
    private int NumberForKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int named = _match.GroupNumberFromName(key);
        if (named >= 0)
        {
            return named;
        }

        return
            int.TryParse(
                key,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int number
            )
            && number <= _publicGroupCount
            && string.Equals(_match.GroupNameFromNumber(number), key, StringComparison.Ordinal)
            ? number
            : -1;
    }

    /// <summary>Enumerates the groups by ascending number.</summary>
    /// <returns>An enumerator over the groups.</returns>
    public IEnumerator<Group> GetEnumerator()
    {
        for (int number = 0; number < Count; number++)
        {
            yield return this[number];
        }
    }

    /// <summary>
    /// The name of every group, by ascending group number. A group with no name of its own is
    /// listed by its number as text, which is what <see cref="FuzzyRegex.GroupNames"/> holds and
    /// what the built-in <c>GroupCollection.Keys</c> does.
    /// </summary>
    public IEnumerable<string> Keys
    {
        get
        {
            for (int number = 0; number < Count; number++)
            {
                yield return _match.GroupNameFromNumber(number);
            }
        }
    }

    /// <summary>Every group, by ascending group number. The same sequence a <c>foreach</c> gives.</summary>
    public IEnumerable<Group> Values => this;

    /// <summary>Whether the pattern has a group of that name.</summary>
    /// <param name="key">The group name, or a group number as text.</param>
    /// <returns>
    /// <see langword="true"/> if the pattern has such a group - whether or not it took part in
    /// this match.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool ContainsKey(string key) => NumberForKey(key) >= 0;

    /// <summary>Gets a group by name without throwing when the pattern has no such group.</summary>
    /// <param name="key">The group name, or a group number as text.</param>
    /// <param name="value">
    /// Receives the group - unsuccessful if it did not take part in the match - or
    /// <see langword="null"/> if the pattern has no group of that name.
    /// </param>
    /// <returns><see langword="true"/> if the pattern has such a group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Group value)
    {
        int number = NumberForKey(key);
        value = number >= 0 ? this[number] : null;

        return number >= 0;
    }

    /// <summary>
    /// The pairs the dictionary face enumerates. Explicit because a class can offer only one
    /// <see cref="IEnumerable{T}"/> to <c>foreach</c>, and this one offers
    /// <see cref="Group"/> - as the built-in <c>GroupCollection</c> does.
    /// </summary>
    /// <returns>An enumerator over (name, group) pairs, by ascending group number.</returns>
    IEnumerator<KeyValuePair<string, Group>> IEnumerable<KeyValuePair<string, Group>>.GetEnumerator()
    {
        for (int number = 0; number < Count; number++)
        {
            yield return new KeyValuePair<string, Group>(_match.GroupNameFromNumber(number), this[number]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The matches found by <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>,
/// found as they are asked for. Shaped after <see cref="System.Text.RegularExpressions.MatchCollection"/>,
/// which is lazy in the same way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lazy since 2026-10-14</b> (DECISIONS, owner decision; design in
/// <c>docs/plan/2026-10-14-lazy-matches.md</c>). <c>Matches</c> does no matching. Reading a match
/// finds the matches up to it and keeps them, so reading it again costs nothing; <see cref="Count"/>
/// finds every match. Until then the whole scan ran inside <c>Matches</c>, so a caller who wanted
/// two matches of a megabyte paid for 214,493 of them (the <c>MatchesFirstTwo</c> benchmark row).
/// </para>
/// <para>
/// This follows the built-in collection (dotnet/runtime
/// <c>src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/MatchCollection.cs</c>,
/// read at 4e7eb8c7): a list of the matches found so far, and the means to find the next one, used
/// once per missing match. The built-in one keeps only where the next match may start and builds an
/// engine for each match. This one keeps the walk's own engine state instead, because more than a
/// position carries from one match to the next here: a <c>(*SKIP)</c> moves the start of the slice
/// for the rest of the scan, for one. Keeping the state makes the answers exactly those of
/// <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>,
/// match for match. The state goes back to the pattern when the walk ends; a collection that is
/// dropped part way through takes it with it, and its rented buffers go to the garbage collector
/// rather than back to the pool, which <see cref="System.Buffers.ArrayPool{T}"/> allows.
/// </para>
/// <para>
/// <b>Timeouts and cancellation surface where the matching happens</b>: from <see cref="Count"/>,
/// the indexer or enumeration, whichever needs a match not yet found. The budget bounds each match
/// rather than the whole scan, so the time a caller spends between two reads is never charged to
/// it, as with the built-in collection. Once a read has thrown, the matches found before stay
/// readable, and every read that needs a later one throws the same exception again without
/// searching: the state that threw has been given back, and a fresh one cannot be put exactly where
/// it was.
/// </para>
/// <para>
/// <b>Safe to read from several threads at once</b>, each with its own enumerator, which is more
/// than the built-in collection promises ("result objects ... should be used on a single thread",
/// learn.microsoft.com, "Thread safety in regular expressions"). A match already found is read
/// without a lock; finding more takes one, so only one thread advances the walk at a time.
/// </para>
/// </remarks>
public sealed class MatchCollection : IReadOnlyList<Match>
{
    /// <summary>The matches found so far, in order. Written only under <see cref="_gate"/>.</summary>
    private readonly MatchList _found = new();

    /// <summary>Held while the walk is advanced, so only one thread advances it at a time.</summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// The walk that finds the rest, or <see langword="null"/> once it has ended or thrown. Read and
    /// written only under <see cref="_gate"/>.
    /// </summary>
    private IEnumerator<Match>? _walk;

    /// <summary>
    /// What stopped the walk, if it threw: rethrown by every later read that needs a match it did not
    /// find. Read and written only under <see cref="_gate"/>.
    /// </summary>
    private ExceptionDispatchInfo? _fault;

    /// <summary>Creates a collection that will find its matches by pulling them from a walk.</summary>
    /// <param name="walk">
    /// The walk, before its first step: one engine state for every match, disposed when it ends.
    /// </param>
    internal MatchCollection(IEnumerable<Match> walk)
    {
        _walk = walk.GetEnumerator();
    }

    /// <summary>The number of matches. Finds every match not found yet.</summary>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Finding one of the remaining matches ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">The token given to <c>Matches</c> was cancelled.</exception>
    public int Count
    {
        get
        {
            // Under the lock even once the walk has ended, which costs one uncontended lock a call
            // and keeps the collection's own fields lock-only (ThreadSafetyTests' allowlist).
            _ = FindUpTo(int.MaxValue);
            return _found.Count;
        }
    }

    /// <summary>Gets a match by position, finding the matches up to it if they are not found yet.</summary>
    /// <param name="index">The zero-based position of the match.</param>
    /// <returns>The match at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">There is no match at that position.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Finding a match up to that position ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">The token given to <c>Matches</c> was cancelled.</exception>
    public Match this[int index]
    {
        get
        {
            Match? match = index < 0 ? null : MatchAt(index);

            // The exception List<T> throws, as the eager collection threw before 2026-10-14.
            return match
                ?? throw new ArgumentOutOfRangeException(nameof(index), index, "there is no match at that position");
        }
    }

    /// <summary>
    /// Enumerates the matches, leftmost first, finding each one as it is reached. Each enumerator
    /// belongs to the thread that made it; several may read one collection at once.
    /// </summary>
    /// <returns>An enumerator over the matches.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Thrown by <c>MoveNext</c>: finding the next match ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown by <c>MoveNext</c>: the token given to <c>Matches</c> was cancelled.
    /// </exception>
    public IEnumerator<Match> GetEnumerator()
    {
        for (int i = 0; MatchAt(i) is { } match; i++)
        {
            yield return match;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>The match at a position, found if need be, or <see langword="null"/> if there is none.</summary>
    /// <param name="index">The position, zero or more.</param>
    /// <returns>The match, or <see langword="null"/>.</returns>
    private Match? MatchAt(int index) =>
        // The fast path takes no lock: a match below the published count is in place for good.
        _found.TryGet(index, out Match? match)
            ? match
            : FindUpTo(index);

    /// <summary>
    /// Advances the walk until the match at <paramref name="index"/> is found or the walk ends.
    /// </summary>
    /// <param name="index">The position wanted; <see cref="int.MaxValue"/> to run to the end.</param>
    /// <returns>The match at that position, or <see langword="null"/> if there is none.</returns>
    private Match? FindUpTo(int index)
    {
        lock (_gate)
        {
            while (_found.Count <= index)
            {
                if (_walk is null)
                {
                    // Ended, or stopped by an exception that every later read for more gets.
                    _fault?.Throw();
                    return null;
                }

                Advance(_walk);
            }

            return _found[index];
        }
    }

    /// <summary>
    /// Takes one match from the walk into <see cref="_found"/>, or ends the walk. Called only under
    /// <see cref="_gate"/>.
    /// </summary>
    /// <param name="walk">The walk, which is <see cref="_walk"/>.</param>
    private void Advance(IEnumerator<Match> walk)
    {
        System.Diagnostics.Debug.Assert(_gate.IsHeldByCurrentThread, "only one thread may drive the walk");

        bool found;
        try
        {
            found = walk.MoveNext();
        }
        catch (Exception exception)
        {
            // A timeout, a cancellation or anything else: the walk's state is no longer one to
            // resume from, so it goes back now and the exception is kept for later reads.
            _walk = null;
            walk.Dispose();
            _fault = ExceptionDispatchInfo.Capture(exception);
            throw;
        }

        if (found)
        {
            _found.Add(walk.Current);
            return;
        }

        _walk = null;
        walk.Dispose();
    }
}

/// <summary>
/// The matches a <see cref="MatchCollection"/> has found, in the order it found them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kept in chunks of 1,024 rather than in one array that doubles</b>, which is what a
/// <see cref="List{T}"/> did until C2. A doubling array copies every reference it holds each time it
/// grows and leaves the old array behind, 16 to 32 bytes a match in all, and from 8,193 matches on
/// each new array is over the 85,000 bytes that put it on the large object heap, whose allocations
/// count towards a full collection. The <c>MatchesToEnd</c> row's 214,000-odd matches put five arrays
/// on that heap per call, 4 MB in all. Here the first 1,024 grow by doubling, so a scan that finds a
/// handful of matches pays for a handful, and every later 1,024 get an 8 KB array of their own that
/// never moves: 8 bytes a match, nothing copied, nothing on the large object heap.
/// </para>
/// <para>
/// <b>One writer, any number of readers without a lock.</b> The collection adds under its lock;
/// readers call <see cref="TryGet"/> without it. <see cref="Add"/> writes the slot first and
/// publishes the count after it with a release, and a reader reads the count with an acquire before
/// the slot, so a reader that sees a count sees every match below it. A grown array is filled
/// before the reference to it is stored, and the .NET memory model makes storing a reference a
/// release for the array's elements (dotnet/runtime <c>docs/design/specs/Memory-model.md</c>,
/// "Object assignment"), so a reader that reaches the new array sees what was copied into it, and
/// one that reaches the old array finds every slot below the count it read there too.
/// </para>
/// </remarks>
internal sealed class MatchList
{
    /// <summary>log2 of <see cref="_chunkSize"/>.</summary>
    private const int _chunkShift = 10;

    /// <summary>How many matches a chunk holds: 1,024 references, 8 KB on a 64-bit runtime.</summary>
    private const int _chunkSize = 1 << _chunkShift;

    /// <summary>The first <see cref="_chunkSize"/> matches, in an array that doubles up to that size.</summary>
    private Match[] _head = [];

    /// <summary>
    /// Every later run of <see cref="_chunkSize"/> matches, each in an array of its own. The outer
    /// array doubles as chunks are added; its unused tail is null.
    /// </summary>
    private Match[][] _tail = [];

    /// <summary>The number of matches, published after the slot it counts is written.</summary>
    private int _count;

    /// <summary>The number of matches, as last published.</summary>
    internal int Count => Volatile.Read(ref _count);

    /// <summary>Gets a match by position.</summary>
    /// <param name="index">The zero-based position, below <see cref="Count"/>.</param>
    internal Match this[int index]
    {
        get
        {
            bool present = TryGet(index, out Match? match);
            System.Diagnostics.Debug.Assert(present, "only positions below the count are asked for");
            return match!;
        }
    }

    /// <summary>Gets a match by position if it has been added, without a lock.</summary>
    /// <param name="index">The zero-based position.</param>
    /// <param name="match">The match, or <see langword="null"/> when there is none there yet.</param>
    /// <returns>Whether there was one.</returns>
    internal bool TryGet(int index, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Match? match)
    {
        // The unsigned compare also rejects a negative position.
        if ((uint)index >= (uint)Count)
        {
            match = null;
            return false;
        }

        match = index < _chunkSize ? _head[index] : _tail[(index >> _chunkShift) - 1][index & (_chunkSize - 1)];
        return true;
    }

    /// <summary>Adds the next match. One writer at a time; see the remarks.</summary>
    /// <param name="match">The match.</param>
    internal void Add(Match match)
    {
        int count = _count;
        if (count < _chunkSize)
        {
            if (count == _head.Length)
            {
                Match[] grown = new Match[Math.Max(4, count * 2)];
                Array.Copy(_head, grown, count);
                _head = grown;
            }

            _head[count] = match;
        }
        else
        {
            int chunk = (count >> _chunkShift) - 1;
            int slot = count & (_chunkSize - 1);
            if (slot == 0)
            {
                if (chunk == _tail.Length)
                {
                    Match[][] grown = new Match[Math.Max(4, chunk * 2)][];
                    Array.Copy(_tail, grown, chunk);
                    _tail = grown;
                }

                _tail[chunk] = new Match[_chunkSize];
            }

            _tail[chunk][slot] = match;
        }

        Volatile.Write(ref _count, count + 1);
    }
}
