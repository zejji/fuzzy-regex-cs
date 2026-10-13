using System.Collections.Frozen;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// A run of 2-way <c>BRANCH</c> nodes, each the <c>next_2</c> of the one before, indexed by the
/// first one or two code units each alternative can start with. <b>This port's own type</b> (S60b
/// item 9).
/// </summary>
/// <remarks>
/// <para>
/// An alternation of <c>n</c> members compiles to <c>n - 1</c> branch nodes in a row, and a named
/// list is one of them (<c>Parsing.StringSet</c>). Upstream enters the run at its head and asks
/// <c>try_match</c> about each alternative in turn, so a list of a thousand words costs a thousand
/// tests at every position it is tried. An alternative whose test is a case-sensitive
/// <c>CHARACTER</c> or <c>STRING</c> that cannot start with the text at the position fails that
/// test and pushes nothing, so jumping straight to the first alternative that can start there
/// gives the same answer.
/// </para>
/// <para>
/// The test compares code points, and the index compares the code units they are written in. A
/// test that succeeds has read the same code units, so units that differ inside the slice mean the
/// test fails. The second unit is used only when it is inside the slice too, because a test that
/// runs out of slice can answer a partial match.
/// </para>
/// <para>
/// sync-divergence: upstream's BRANCH arm tries each alternative's test node in turn / the port
/// jumps over the ones whose first code units differ from the text's / a long alternation was
/// linear in its length at every position. Re-aligning: if upstream changes what
/// <c>try_match</c> answers for a <c>CHARACTER</c> or <c>STRING</c> test inside the slice, the
/// skip must answer the same or be dropped.
/// </para>
/// </remarks>
internal sealed class BranchChain
{
    /// <summary>
    /// The fewest alternatives a run needs before it is indexed. Below it the lookup costs about
    /// what the tests it saves do.
    /// </summary>
    internal const int MinAlternatives = 8;

    /// <summary>The branch nodes, head first.</summary>
    private readonly Node[] _nodes;

    /// <summary>Where the run ends: the last branch node's <c>next_2</c>.</summary>
    private readonly Node _tail;

    /// <summary>
    /// For a first code unit, the alternatives keyed on it, alone or in a pair, in order. For when
    /// the second unit is outside the slice.
    /// </summary>
    private readonly FrozenDictionary<char, int[]> _byFirst;

    /// <summary>For a first code unit, the alternatives keyed on it alone, in order.</summary>
    private readonly FrozenDictionary<char, int[]> _byFirstOnly;

    /// <summary>
    /// For the first two code units, packed high then low, the alternatives keyed on that pair, in
    /// order.
    /// </summary>
    private readonly FrozenDictionary<int, int[]> _byPair;

    /// <summary>The alternatives with no key, in order. Any of them may start anywhere.</summary>
    private readonly int[] _unkeyed;

    private BranchChain(
        Node[] nodes,
        Node tail,
        FrozenDictionary<char, int[]> byFirst,
        FrozenDictionary<char, int[]> byFirstOnly,
        FrozenDictionary<int, int[]> byPair,
        int[] unkeyed
    )
    {
        _nodes = nodes;
        _tail = tail;
        _byFirst = byFirst;
        _byFirstOnly = byFirstOnly;
        _byPair = byPair;
        _unkeyed = unkeyed;
    }

    /// <summary>
    /// The branch node to try instead of the run's node <paramref name="from"/>: the first node from
    /// there whose alternative can start at <paramref name="textPos"/>, or the run's tail when none
    /// can.
    /// </summary>
    /// <param name="from">The index of the branch node being entered.</param>
    /// <param name="text">The subject.</param>
    /// <param name="textPos">The text position, which must be inside the slice.</param>
    /// <param name="sliceEnd">The end of the slice.</param>
    /// <returns>The node to continue at.</returns>
    internal Node Skip(int from, ReadOnlySpan<char> text, int textPos, int sliceEnd)
    {
        char first = text[textPos];
        int next = Next(_unkeyed, from);
        if (textPos + 1 < sliceEnd)
        {
            if (_byPair.TryGetValue((first << 16) | text[textPos + 1], out int[]? pair))
            {
                next = Math.Min(next, Next(pair, from));
            }

            if (_byFirstOnly.TryGetValue(first, out int[]? only))
            {
                next = Math.Min(next, Next(only, from));
            }
        }
        else if (_byFirst.TryGetValue(first, out int[]? any))
        {
            next = Math.Min(next, Next(any, from));
        }

        return next < _nodes.Length ? _nodes[next] : _tail;

        // The first entry at or after from, or past every node when there is none.
        static int Next(int[] candidates, int from)
        {
            int at = Array.BinarySearch(candidates, from);
            if (at < 0)
            {
                at = ~at;
            }

            return at < candidates.Length ? candidates[at] : int.MaxValue;
        }
    }

    /// <summary>
    /// Indexes every run of at least <see cref="MinAlternatives"/> alternatives, most of them keyed,
    /// and sets <see cref="Node.Chain"/> and <see cref="Node.ChainIndex"/> on its branch nodes.
    /// </summary>
    /// <param name="nodes">The compiled pattern's nodes, numbered and with their test nodes set.</param>
    internal static void Build(List<Node> nodes)
    {
        // Node.Index is each node's place in the list, so it keys these flags.
        bool[] continuation = new bool[nodes.Count];
        foreach (Node node in nodes)
        {
            if (node.Op == Opcode.Branch && node.Next2.Node is { Op: Opcode.Branch } next)
            {
                continuation[next.Index] = true;
            }
        }

        foreach (Node head in nodes)
        {
            if (head.Op != Opcode.Branch || continuation[head.Index])
            {
                continue;
            }

            // A run longer than the node list would be a cycle, which the compiler never builds.
            List<Node> run = [head];
            while (run[^1].Next2.Node is { Op: Opcode.Branch } next && next.Chain is null && run.Count < nodes.Count)
            {
                run.Add(next);
            }

            // n - 1 branch nodes carry n alternatives; the tail is the last of them.
            if (run.Count + 1 >= MinAlternatives && run[^1].Next2.Node is { } tail)
            {
                Index(run, tail);
            }
        }
    }

    /// <summary>Builds the index for one run, unless most of its alternatives have no key.</summary>
    /// <param name="run">The branch nodes, head first.</param>
    /// <param name="tail">The last branch node's <c>next_2</c>.</param>
    private static void Index(List<Node> run, Node tail)
    {
        var prefixes = new string?[run.Count];
        List<int> unkeyed = [];
        for (int i = 0; i < run.Count; i++)
        {
            prefixes[i] = Prefix(run[i].Next1.Test);
            if (prefixes[i] is null)
            {
                unkeyed.Add(i);
            }
        }

        // Every lookup still tries each unkeyed alternative, so a run mostly of them gains little.
        if (unkeyed.Count * 2 > run.Count)
        {
            return;
        }

        Dictionary<char, List<int>> byFirst = [];
        Dictionary<char, List<int>> byFirstOnly = [];
        Dictionary<int, List<int>> byPair = [];
        for (int i = 0; i < run.Count; i++)
        {
            if (prefixes[i] is not { } prefix)
            {
                continue;
            }

            Add(byFirst, prefix[0], i);
            if (prefix.Length == 1)
            {
                Add(byFirstOnly, prefix[0], i);
            }
            else
            {
                Add(byPair, (prefix[0] << 16) | prefix[1], i);
            }
        }

        // Each alternative is in at most two lists and each list is in order, because the loop
        // above adds in order. A list that repeated another would cost space in the product of
        // their lengths: 2 GB for 32,000 alternatives, half of them unkeyed.
        var chain = new BranchChain(
            [.. run],
            tail,
            byFirst.ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value.ToArray()),
            byFirstOnly.ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value.ToArray()),
            byPair.ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value.ToArray()),
            [.. unkeyed]
        );
        for (int i = 0; i < run.Count; i++)
        {
            run[i].Chain = chain;
            run[i].ChainIndex = i;
        }

        static void Add<TKey>(Dictionary<TKey, List<int>> map, TKey key, int index)
            where TKey : notnull
        {
            if (!map.TryGetValue(key, out List<int>? list))
            {
                map[key] = list = [];
            }

            list.Add(index);
        }
    }

    /// <summary>
    /// The first one or two code units an alternative with this test node must start with, or
    /// <see langword="null"/> when the test does not decide that: no test, a fuzzy one, or anything
    /// but a case-sensitive forward <c>CHARACTER</c> or <c>STRING</c>.
    /// </summary>
    /// <param name="test">The alternative's test node (<c>next_1.test</c>).</param>
    /// <returns>The code units, or <see langword="null"/>.</returns>
    private static string? Prefix(Node? test)
    {
        if (
            test is null
            || (test.Status & NodeStatus.Fuzzy) != 0
            || test.Values.Length == 0
            || !(test.Op == Opcode.String || (test.Op == Opcode.Character && test.Match))
        )
        {
            return null;
        }

        string units = Units(test.Values[0]);
        if (units.Length == 1 && test.Op == Opcode.String && test.Values.Length > 1)
        {
            units += Units(test.Values[1])[0];
        }

        return units;

        // A value in the surrogate range is one unit, as the text holds a lone surrogate.
        static string Units(uint value) =>
            value > 0xFFFF ? char.ConvertFromUtf32((int)value) : ((char)value).ToString();
    }
}
