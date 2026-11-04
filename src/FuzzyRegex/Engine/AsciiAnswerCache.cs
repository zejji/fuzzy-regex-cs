using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// The <see cref="Node.AsciiAnswers"/> of one-character tests that are not sets, shared by every
/// node with the same <see cref="Key"/>, in one pattern or many. <b>This port's own type</b>
/// (D141): upstream keeps no ASCII answers at all.
/// </summary>
/// <remarks>
/// <para>
/// A case-insensitive character, a case-insensitive range or a property answers the same 128 bits
/// wherever it appears, so filling its table again for every node, in every compile, was most of
/// what compiling an ordinary pattern cost (docs/plan/2026-11-04-phase11-profile.md). A set's
/// answers depend on its members, which a key of fixed size cannot name, so a set fills its own.
/// </para>
/// <para>
/// This is the library's second piece of shared mutable state, after <see cref="PatternCache"/>,
/// and for the same reason it is a class of its own. It is written only while a pattern compiles,
/// never while one matches. A table is filled completely before it is published, and nothing
/// writes it after, so the nodes that share it read an immutable array; the dictionary itself is a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </para>
/// <para>
/// SHORTCUT: the cache stops growing at <see cref="Capacity"/> keys (a few threads that miss at
/// the same moment can each add one more), and a miss after that fills the node a table of its
/// own, as every node did before D141. The ceiling is a process compiling more distinct
/// case-insensitive characters, ranges and properties than that; the upgrade path is an eviction
/// policy, if a measured workload ever reaches it.
/// </para>
/// </remarks>
internal sealed class AsciiAnswerCache
{
    /// <summary>The most keys the cache takes: at about 100 bytes a key, under half a megabyte.</summary>
    internal const int Capacity = 4096;

    /// <summary>The cache every compile shares.</summary>
    internal static readonly AsciiAnswerCache Shared = new();

    private readonly ConcurrentDictionary<Key, ulong[]> _tables = new();

    private int _count;

    /// <summary>Gets how many keys the cache holds.</summary>
    internal int Count => Volatile.Read(ref _count);

    /// <summary>
    /// The key of <paramref name="node"/>, or <see langword="null"/> for a set: everything
    /// <see cref="Matcher.MatchesOneUncached"/> reads of a node that is not a set, so two nodes
    /// with equal keys have equal answers.
    /// </summary>
    /// <remarks>
    /// The predicates and what they read: <see cref="Matcher.MatchesCharacterIgn"/> the encoding
    /// passed in and <c>Values[0]</c>; <see cref="Matcher.MatchesRangeIgn"/> the encoding and
    /// <c>Values[0..1]</c>; <see cref="Matcher.MatchesProperty"/> and
    /// <see cref="Matcher.MatchesPropertyIgn"/> the encoding after
    /// <see cref="Matcher.NodeEncoding(CaseEncoding, Node)"/> has applied <see cref="Node.Status"/>,
    /// and <c>Values[0]</c>. The encoding passed in is <see cref="Node.Encoding"/>: the fill passes
    /// it, and <c>Matcher.MatchesOne</c> asserts that its callers do. The Unicode tables and case
    /// data beneath the predicates are static. A <c>_REV</c> opcode runs the same predicate as its
    /// forward twin, so the two share a key.
    /// </remarks>
    /// <param name="node">A node whose opcode keeps ASCII answers.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    internal static Key? KeyOf(Node node)
    {
        switch (node.Op)
        {
            case Opcode.CharacterIgn or Opcode.CharacterIgnRev:
                Debug.Assert(node.Values.Length >= 1, "a character has its codepoint");
                return new Key(Opcode.CharacterIgn, node.Encoding, node.Values[0], 0);
            case Opcode.RangeIgn or Opcode.RangeIgnRev:
                Debug.Assert(node.Values.Length >= 2, "a range has its bounds");
                return new Key(Opcode.RangeIgn, node.Encoding, node.Values[0], node.Values[1]);
            case Opcode.Property or Opcode.PropertyRev:
                Debug.Assert(node.Values.Length >= 1, "a property has its value");
                return new Key(Opcode.Property, Matcher.NodeEncoding(node.Encoding, node), node.Values[0], 0);
            case Opcode.PropertyIgn or Opcode.PropertyIgnRev:
                Debug.Assert(node.Values.Length >= 1, "a property has its value");
                return new Key(Opcode.PropertyIgn, Matcher.NodeEncoding(node.Encoding, node), node.Values[0], 0);
            default:
                Debug.Assert(
                    node.Op is >= Opcode.SetDiff and <= Opcode.SetUnionRev,
                    "every other opcode that keeps answers is a set"
                );
                return null;
        }
    }

    /// <summary>Looks up the table for <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="table">Receives the table, if there is one.</param>
    /// <returns><see langword="true"/> if there is one.</returns>
    internal bool TryGet(Key key, [NotNullWhen(true)] out ulong[]? table) => _tables.TryGetValue(key, out table);

    /// <summary>
    /// Publishes <paramref name="table"/>, filled, for <paramref name="key"/>, unless the cache is
    /// full.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="table">The filled table, which nothing writes again.</param>
    /// <returns>
    /// The table to use: another thread's, if it published the same key first, otherwise
    /// <paramref name="table"/>.
    /// </returns>
    internal ulong[] Add(Key key, ulong[] table)
    {
        if (Count >= Capacity)
        {
            return table;
        }

        if (_tables.TryAdd(key, table))
        {
            Interlocked.Increment(ref _count);
            return table;
        }

        return _tables.TryGetValue(key, out ulong[]? first) ? first : table;
    }

    /// <summary>What a test that is not a set reads: see <see cref="KeyOf"/>.</summary>
    /// <param name="Family">The forward opcode of the predicate.</param>
    /// <param name="Encoding">The encoding the predicate uses.</param>
    /// <param name="Value0">The node's first value.</param>
    /// <param name="Value1">The node's second value, for a range; otherwise 0.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Key(Opcode Family, CaseEncoding Encoding, uint Value0, uint Value1);
}
