using System.Collections;

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
/// It is also an <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed by group name, as the
/// built-in <c>GroupCollection</c> has been since .NET 5, so a caller can ask
/// <see cref="TryGetValue"/> about a name the pattern may not have without catching anything.
/// Measured on .NET 10.0.10 (<c>tools/probes/dotnet-groupcollection-dictionary.ps1</c>,
/// 2026-09-16), the built-in's dictionary face is TOTAL rather than named-only:
/// <c>Regex.Match("ab", "(?&lt;a&gt;a)(b)(?&lt;c&gt;c)?").Groups</c> has <c>Count</c> 4 and
/// <c>Keys</c> <c>[0, 1, a, c]</c>, so every group is in it and an unnamed one is keyed by its
/// number as text. This port does the same, which makes <see cref="Keys"/> exactly
/// <see cref="FuzzyRegex.GroupNames"/> - in ascending group number, which is the order
/// <c>Keys</c> came back in there. Upstream's <c>groupdict</c> is the named-only view instead, and
/// it is a filter over this one.
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
/// The matches found by <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>. Shaped after
/// <see cref="System.Text.RegularExpressions.MatchCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// The whole scan runs before this is handed back, where the built-in <c>MatchCollection</c> is
/// lazy and finds the next match as it is asked for. That is upstream's <c>findall</c> rather than
/// its <c>finditer</c>, and it is what this shape can honestly offer: <c>IReadOnlyList</c> promises
/// a <see cref="Count"/>, which no lazy scan can answer without running to the end anyway, and the
/// engine's state owns rented buffers that a half-enumerated iterator would never return.
/// </para>
/// <para>
/// Eager, so a caller that wants the first two matches of a huge subject pays for all of them.
/// That is what <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
/// is for (S53b); this type stays eager on purpose, because making it lazy would either break
/// <see cref="Count"/> or leak the state.
/// </para>
/// </remarks>
public sealed class MatchCollection : IReadOnlyList<Match>
{
    private readonly IReadOnlyList<Match> _matches;

    internal MatchCollection(IReadOnlyList<Match> matches)
    {
        _matches = matches;
    }

    /// <summary>The number of matches.</summary>
    public int Count => _matches.Count;

    /// <summary>Gets a match by position.</summary>
    /// <param name="index">The zero-based position of the match.</param>
    /// <returns>The match at that position.</returns>
    public Match this[int index] => _matches[index];

    /// <summary>Enumerates the matches, leftmost first.</summary>
    /// <returns>An enumerator over the matches.</returns>
    public IEnumerator<Match> GetEnumerator() => _matches.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
