using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM (the failed-call memo): compares the entry keys in
/// <see cref="MatchState.FailedCalls"/> by content, and lets the set be asked about a key still in
/// <see cref="MatchState.CallMemoKey"/> without copying it first.
/// </summary>
/// <remarks>
/// The keys are compared exactly, not by a hash alone: two different calls whose hashes collided
/// would otherwise share an answer, and one of them could be a call that matches.
/// </remarks>
internal sealed class FailedCallKeyComparer
    : IEqualityComparer<long[]>,
        IAlternateEqualityComparer<ReadOnlySpan<long>, long[]>
{
    /// <inheritdoc />
    public bool Equals(long[]? x, long[]? y) =>
        ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

    /// <inheritdoc />
    public int GetHashCode(long[] obj) => Hash(obj);

    /// <inheritdoc />
    public bool Equals(ReadOnlySpan<long> alternate, long[] other) => alternate.SequenceEqual(other);

    /// <inheritdoc />
    public int GetHashCode(ReadOnlySpan<long> alternate) => Hash(alternate);

    /// <inheritdoc />
    public long[] Create(ReadOnlySpan<long> alternate) => alternate.ToArray();

    private static int Hash(ReadOnlySpan<long> key)
    {
        var hash = new HashCode();
        hash.AddBytes(MemoryMarshal.AsBytes(key));
        return hash.ToHashCode();
    }
}
