using System.Collections;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// The captures of one group, oldest first. Shaped after
/// <see cref="System.Text.RegularExpressions.CaptureCollection"/>.
/// </summary>
public sealed class CaptureCollection : IReadOnlyList<Capture>
{
    internal CaptureCollection()
    {
    }

    /// <summary>The number of captures.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>Gets a capture by position.</summary>
    /// <param name="index">The zero-based position of the capture.</param>
    /// <returns>The capture at that position.</returns>
    public Capture this[int index] => throw new NotImplementedException();

    /// <summary>Enumerates the captures, oldest first.</summary>
    /// <returns>An enumerator over the captures.</returns>
    public IEnumerator<Capture> GetEnumerator() => throw new NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The groups of a match, indexable by number or by name. Shaped after
/// <see cref="System.Text.RegularExpressions.GroupCollection"/>.
/// </summary>
public sealed class GroupCollection : IReadOnlyList<Group>
{
    internal GroupCollection()
    {
    }

    /// <summary>The number of groups, including group 0.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>Gets a group by number, group 0 being the whole match.</summary>
    /// <param name="number">The group number.</param>
    /// <returns>The group, or an unsuccessful group if it did not take part in the match.</returns>
    public Group this[int number] => throw new NotImplementedException();

    /// <summary>Gets a group by name.</summary>
    /// <param name="name">The group name.</param>
    /// <returns>The group, or an unsuccessful group if it did not take part in the match.</returns>
    public Group this[string name] => throw new NotImplementedException();

    /// <summary>Enumerates the groups by ascending number.</summary>
    /// <returns>An enumerator over the groups.</returns>
    public IEnumerator<Group> GetEnumerator() => throw new NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The matches found by <see cref="FuzzyRegex.Matches(string, int, int, bool)"/>. Shaped after
/// <see cref="System.Text.RegularExpressions.MatchCollection"/>.
/// </summary>
public sealed class MatchCollection : IReadOnlyList<Match>
{
    internal MatchCollection()
    {
    }

    /// <summary>The number of matches.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>Gets a match by position.</summary>
    /// <param name="index">The zero-based position of the match.</param>
    /// <returns>The match at that position.</returns>
    public Match this[int index] => throw new NotImplementedException();

    /// <summary>Enumerates the matches, leftmost first.</summary>
    /// <returns>An enumerator over the matches.</returns>
    public IEnumerator<Match> GetEnumerator() => throw new NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
