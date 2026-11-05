namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// A bounded most-recently-used cache of compiled patterns, behind
/// <see cref="FuzzyRegex.CacheSize"/>. Upstream caches module-globally and exposes <c>purge</c> and
/// <c>cache_all</c> to control it; the built-in <see cref="System.Text.RegularExpressions.Regex"/>
/// keeps its most recent static patterns behind <c>Regex.CacheSize</c>. This is the latter shape,
/// because a .NET caller already knows it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the library's only shared mutable state</b>, which is why it is a class of its own
/// rather than a handful of statics on <see cref="FuzzyRegex"/>: the thread-safety contract in
/// <see cref="FuzzyRegex"/>'s remarks is permanent, and every path that can mutate the cache is in
/// this file and holds <see cref="_gate"/> while it does. A <see cref="FuzzyRegex"/> is immutable,
/// so an entry handed out is safe to share; only the dictionary and the order list need guarding.
/// </para>
/// <para>
/// Compiling happens OUTSIDE the lock. Two threads that miss on the same key therefore both
/// compile, and the loser's instance is thrown away rather than published - the alternative,
/// holding the lock across a compile, would serialise every first use of every pattern behind the
/// slowest one. <c>Regex</c>'s own cache makes the same trade.
/// </para>
/// </remarks>
internal sealed class PatternCache
{
    /// <summary>
    /// The bound a new cache starts at. Fifteen, because <c>Regex.CacheSize</c> is fifteen -
    /// measured on .NET 10.0.10 by <c>tools/probes/bcl-regex-cachesize.ps1</c> (2026-09-19)
    /// rather than read off a documentation page.
    /// </summary>
    internal const int DefaultSize = 15;

    private readonly Lock _gate = new();

    /// <summary>The entries, by key. Its count is the number of patterns held.</summary>
    private readonly Dictionary<Key, LinkedListNode<Entry>> _byKey = [];

    /// <summary>The same entries in use order, most recently used first.</summary>
    private readonly LinkedList<Entry> _order = new();

    private int _size = DefaultSize;

    /// <summary>
    /// The most patterns the cache may hold. Setting it evicts down to the new bound immediately,
    /// and setting it to zero clears the cache and stops it storing anything.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    internal int Size
    {
        get
        {
            lock (_gate)
            {
                return _size;
            }
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            lock (_gate)
            {
                _size = value;
                Trim();
            }
        }
    }

    /// <summary>
    /// How many patterns the cache currently holds. Exists so the eviction order can be tested at
    /// all: nothing about a cache is visible from its answers, which are the same either way.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _byKey.Count;
            }
        }
    }

    /// <summary>
    /// Whether a pattern compiled with these arguments is in the cache. For the same reason as
    /// <see cref="Count"/>, and race-free where a count is not: a test can ask about its own key
    /// while other work is using the same cache.
    /// </summary>
    /// <param name="pattern">The pattern text.</param>
    /// <param name="options">The options as the caller passed them, before the pattern's own
    /// inline flags are folded in.</param>
    /// <param name="matchTimeout">The instance-level match timeout.</param>
    /// <param name="defaultVersion">The version the pattern falls back to.</param>
    /// <returns><see langword="true"/> if that exact key is held.</returns>
    internal bool Contains(string pattern, FuzzyRegexOptions options, TimeSpan matchTimeout, int defaultVersion)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        lock (_gate)
        {
            return _byKey.ContainsKey(new Key(pattern, (int)options, defaultVersion, matchTimeout));
        }
    }

    /// <summary>
    /// Returns the compiled pattern for these arguments, compiling it if the cache does not hold
    /// it, and makes it the most recently used entry.
    /// </summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="options">Options that change how the pattern is compiled and matched.</param>
    /// <param name="matchTimeout">How long a single matching operation may run.</param>
    /// <param name="defaultVersion">
    /// The version the pattern gets when neither the flags nor an inline <c>(?V0)</c> /
    /// <c>(?V1)</c> pick one.
    /// </param>
    /// <returns>The compiled pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="FuzzyRegexParseException">The pattern is not valid.</exception>
    internal FuzzyRegex GetOrAdd(string pattern, FuzzyRegexOptions options, TimeSpan matchTimeout, int defaultVersion)
    {
        // The caller's own validation, kept here so a null pattern fails exactly as
        // new FuzzyRegex(null) does rather than as a NullReferenceException from hashing the key.
        ArgumentNullException.ThrowIfNull(pattern);

        // The key is the RAW options integer the caller passed, never FuzzyRegex.Options: that
        // property reports the flags the pattern's own inline prefix and the version defaults
        // added, and masks off the ones this port does not name, so two different calls can share
        // it while compiling to different graphs (S53b item 2). "(?i)a" with None and "(?i)a" with
        // IgnoreCase are the worked example, pinned in PatternCacheTests.
        var key = new Key(pattern, (int)options, defaultVersion, matchTimeout);

        lock (_gate)
        {
            if (_byKey.TryGetValue(key, out LinkedListNode<Entry>? hit))
            {
                Touch(hit);
                return hit.Value.Compiled;
            }
        }

        FuzzyRegex compiled = FuzzyRegex.WithDefaultVersion(pattern, options, matchTimeout, null, defaultVersion);

        lock (_gate)
        {
            // Disabled, so nothing is stored - but the caller still gets its pattern.
            if (_size == 0)
            {
                return compiled;
            }

            // Another thread compiled the same key while this one was compiling. Its instance is
            // already published, so that is the one everybody gets; this thread's copy is dropped.
            if (_byKey.TryGetValue(key, out LinkedListNode<Entry>? raced))
            {
                Touch(raced);
                return raced.Value.Compiled;
            }

            _byKey.Add(key, _order.AddFirst(new Entry(key, compiled)));
            Trim();
            return compiled;
        }
    }

    /// <summary>Makes an entry the most recently used one. The caller holds <see cref="_gate"/>.</summary>
    /// <param name="node">The entry to move to the front of the order.</param>
    private void Touch(LinkedListNode<Entry> node)
    {
        if (!ReferenceEquals(node, _order.First))
        {
            _order.Remove(node);
            _order.AddFirst(node);
        }
    }

    /// <summary>Evicts from the back until the bound holds. The caller holds <see cref="_gate"/>.</summary>
    private void Trim()
    {
        while (_byKey.Count > _size)
        {
            LinkedListNode<Entry> evicted = _order.Last!;
            _order.RemoveLast();
            _byKey.Remove(evicted.Value.Key);
        }
    }

    /// <summary>
    /// What makes two calls the same call: the pattern text, the options exactly as the caller
    /// passed them, the fallback version and the instance-level match timeout. The same four
    /// <c>Regex</c>'s cache key holds, less the culture it needs for its own case folding.
    /// </summary>
    /// <remarks>
    /// Two compile inputs are deliberately absent because no static convenience can vary them:
    /// <c>maxCompiledNodes</c>, which only the constructors take, and <c>namedLists</c>, which the
    /// five conveniences that accept one route around this cache rather than key on. <b>A slice
    /// that adds a convenience taking either owes this record a field</b> - a key that omits an
    /// input the caller can change is a cache that answers the wrong question.
    /// </remarks>
    /// <param name="Pattern">The pattern text.</param>
    /// <param name="Options">The caller's options, as a raw integer.</param>
    /// <param name="DefaultVersion">The version the pattern falls back to.</param>
    /// <param name="MatchTimeout">The instance-level match timeout.</param>
    private readonly record struct Key(string Pattern, int Options, int DefaultVersion, TimeSpan MatchTimeout);

    /// <summary>One cached pattern, with the key it was filed under so eviction can unfile it.</summary>
    /// <param name="Key">The key this entry is filed under.</param>
    /// <param name="Compiled">The compiled pattern.</param>
    private readonly record struct Entry(Key Key, FuzzyRegex Compiled);
}
