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
/// Group 0 is the whole match, which is what <see cref="Match"/> itself already is, so it needs no
/// group span of its own. Groups 1 upwards do, and those arrive with <c>START_GROUP</c> in S18;
/// until then this collection reports how many groups the pattern declares and throws
/// <c>needs:groups</c> for every one of them. Reporting them as absent instead would be a wrong
/// answer rather than a missing one.
/// </remarks>
public sealed class GroupCollection : IReadOnlyList<Group>
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

            return number == 0
                ? _match
                : throw new NotImplementedException("needs:groups - capture groups are not implemented yet");
        }
    }

    /// <summary>Gets a group by name.</summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group, or an unsuccessful group if it did not take part in the match.</returns>
    public Group this[string name] =>
        throw new NotImplementedException("needs:named-groups - named groups are not implemented yet");

    /// <summary>Enumerates the groups by ascending number.</summary>
    /// <returns>An enumerator over the groups.</returns>
    public IEnumerator<Group> GetEnumerator()
    {
        for (int number = 0; number < Count; number++)
        {
            yield return this[number];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The matches found by <see cref="FuzzyRegex.Matches(string, int, int, bool)"/>. Shaped after
/// <see cref="System.Text.RegularExpressions.MatchCollection"/>.
/// </summary>
public sealed class MatchCollection : IReadOnlyList<Match>
{
    internal MatchCollection() { }

    /// <summary>The number of matches.</summary>
    public int Count =>
        throw new NotImplementedException("needs:find-all - iterating over the matches is not implemented yet");

    /// <summary>Gets a match by position.</summary>
    /// <param name="index">The zero-based position of the match.</param>
    /// <returns>The match at that position.</returns>
    public Match this[int index] =>
        throw new NotImplementedException("needs:find-all - iterating over the matches is not implemented yet");

    /// <summary>Enumerates the matches, leftmost first.</summary>
    /// <returns>An enumerator over the matches.</returns>
    public IEnumerator<Match> GetEnumerator() =>
        throw new NotImplementedException("needs:find-all - iterating over the matches is not implemented yet");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
