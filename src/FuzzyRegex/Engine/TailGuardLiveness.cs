using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM (D85): what a repeat's tail guard must be keyed on, as worked out when the pattern
/// is compiled. <see langword="null"/> on <see cref="RepeatInfo.TailKey"/> means the rest of the
/// match after the repeat reads no group state, so the plain position-only guard is exact.
/// </summary>
/// <remarks>
/// A tail guard says "the rest of the match, entered after this repeat at position p, fails". That
/// is a fact about p only when the rest of the match reads nothing else. Here it can also read the
/// value of a group (a backreference reads its text, a group condition whether it is set), and the
/// capture-change counter, which an enclosing repeat's progress test reads and which a capture
/// bumps only when the new span differs from the group's current one. The key holds exactly those.
/// </remarks>
/// <param name="Groups">The groups the key holds, as indexes into <c>MatchState.Groups</c>.</param>
/// <param name="Modes">What the key holds of each group, a set of <see cref="TailGuardLiveness"/> flags.</param>
/// <param name="ProgressRepeats">
/// The repeats enclosing this one whose body captures a group something reads, so whose progress test
/// can depend on the counter; the key holds one bit for each (see <c>MatchState.TailGuardKeyOf</c>).
/// </param>
/// <param name="Forward">Whether the repeat matches forwards.</param>
/// <param name="ExactSpans">Whether every group is keyed on its exact span (group calls, lookbehind captures).</param>
internal sealed record TailGuardKeySpec(
    int[] Groups,
    byte[] Modes,
    int[] ProgressRepeats,
    bool Forward,
    bool ExactSpans
)
{
    /// <summary>
    /// EXPERIMENT (D85 night, open): for each keyed group with the repeat inside its brackets, how
    /// many capture groups enclosing the repeat sit inside it, so its parked start is that many
    /// entries below the top of the save stack; -1 for a group the repeat is not inside, or whose
    /// place is unknown.
    /// </summary>
    internal int[] InsideDepths { get; init; } = [];

    /// <summary>EXPERIMENT (D85 night, open): some inside group's place on the save stack is unknown (-2).</summary>
    internal bool HasUnknownInside => InsideDepths.Contains(-2);

    /// <summary>The key's length: a value per group, a bit per progress repeat, an open start per group.</summary>
    internal int KeyLength => (2 * Groups.Length) + ProgressRepeats.Length;

    /// <summary>EXPERIMENT (D85 night, ioff): whether some keyed group has the repeat inside its brackets.</summary>
    internal bool HasInside { get; } = Modes.Any(static m => (m & TailGuardLiveness.Inside) != 0);

    /// <summary>
    /// Whether the key can differ between two positions with the same captures: a group keyed on
    /// whether its span starts at or after the position, or a progress bit.
    /// </summary>
    internal bool DependsOnPosition { get; } =
        ProgressRepeats.Length > 0
        || (
            !ExactSpans
            && Modes.Any(static m =>
                (m & (TailGuardLiveness.Span | TailGuardLiveness.Inside)) == TailGuardLiveness.Span
            )
        );
}

/// <summary>
/// NOT UPSTREAM (D85): the static liveness pass behind <see cref="TailGuardKeySpec"/>. For every
/// repeat with a tail guard, which of the groups the pattern reads are live at its tail: some path
/// from the tail reads the group (or compares a new capture of it with its current span) before
/// any node captures it again. Standard backward dataflow over the node graph, the way a compiler
/// finds live variables, so a group that every path re-captures before reading is left out of the
/// key and the guard keeps the sharing that makes the position-only guard fast.
/// </summary>
internal static class TailGuardLiveness
{
    /// <summary>A backreference reads the group's text.</summary>
    internal const byte Text = 1;

    /// <summary>A group condition reads whether the group is set.</summary>
    internal const byte Set = 2;

    /// <summary>
    /// A capture of the group compares its new span with the current one, to decide whether to bump
    /// the capture-change counter (<c>Matcher.cs</c>, the START_GROUP/END_GROUP arm).
    /// </summary>
    internal const byte Span = 4;

    /// <summary>
    /// The group's closing capture node is reachable without its opening one: the repeat sits inside
    /// the group's brackets, so a new capture can start before the tail does and equal the current span.
    /// </summary>
    internal const byte Inside = 8;

    /// <summary>How many groups the bit masks have room for: four flags each in a <see langword="ulong"/>.</summary>
    private const int _maxSlots = 16;

    /// <summary>Fills in <see cref="RepeatInfo.TailKey"/> for every repeat whose tail is guarded.</summary>
    /// <param name="pattern">The pattern, after <c>Optimiser.CollectTestedGroups</c>.</param>
    internal static void FindTailKeys(PatternObject pattern)
    {
        int[] tested = pattern.TestedGroups;
        if (tested.Length == 0)
        {
            return;
        }

        List<Node> nodes = pattern.NodeList;
        var indexOf = new Dictionary<object, int>(nodes.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < nodes.Count; i++)
        {
            indexOf[nodes[i]] = i;
        }

        // A group call returns to wherever it was called from, which the graph does not show, and
        // more groups than the masks hold would need wider ones: in both cases every read group is
        // live at every tail, keyed on its exact span. SHORTCUT: the ceiling is 16 read groups and
        // no group calls; following call and return edges would narrow the second.
        bool everything = pattern.HasGroupCalls || tested.Length > _maxSlots;
        ulong[] liveIn = everything ? [] : Solve(nodes, indexOf, tested);
        Dictionary<int, List<int>> progress = FindProgressRepeats(pattern, tested);
        Dictionary<object, int[]>? enclosing = null;

        foreach (Node repeat in nodes)
        {
            if (
                repeat.Op
                is not (Opcode.GreedyRepeat or Opcode.LazyRepeat or Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne)
            )
            {
                continue;
            }

            int index = (int)repeat.Values[0];
            RepeatInfo info = pattern.RepeatInfoAt(index);
            if ((info.Status & NodeStatus.Tail) == 0)
            {
                continue;
            }

            bool one = repeat.Op is Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne;
            Node? tail = one ? repeat.Next1.Node : repeat.Next2.Node;
            ulong live = ulong.MaxValue;
            if (!everything)
            {
                live = tail is null ? 0 : liveIn[indexOf[tail]];
            }

            var groups = new List<int>();
            var modes = new List<byte>();
            for (int slot = 0; slot < tested.Length; slot++)
            {
                byte mode = everything
                    ? (byte)(Span | Inside | ReadOf(pattern, tested[slot]))
                    : (byte)((live >> (4 * slot)) & 0xF);
                if (mode != 0)
                {
                    groups.Add(tested[slot] - 1);
                    modes.Add(mode);
                }
            }

            int[] around = progress.TryGetValue(index, out List<int>? list) ? [.. list] : [];
            if (groups.Count == 0 && around.Length == 0)
            {
                continue;
            }

            int[] depths = new int[groups.Count];
            enclosing ??= EnclosingGroups(pattern);
            for (int i = 0; i < groups.Count; i++)
            {
                // Count the capture groups enclosing the repeat inside this one; -2 when the repeat
                // is not seen inside it, so its parked start cannot be found.
                int depth = (modes[i] & Inside) == 0 ? -1 : DepthOf(enclosing, repeat, groups[i] + 1);
                if (everything && depth == -2)
                {
                    // Every group is marked inside here; one that does not enclose the repeat is not.
                    depth = -1;
                }
                else if (pattern.HasGroupCalls && depth >= 0)
                {
                    // SHORTCUT: a call's frames sit on the save stack too, so the parked start
                    // cannot be found by depth; the guard is off instead.
                    depth = -2;
                }

                depths[i] = depth;
            }

            info.TailKey = new TailGuardKeySpec(
                [.. groups],
                [.. modes],
                around,
                repeat.Values[3] != 0,
                everything || pattern.CapturesAgainstDirection
            )
            {
                InsideDepths = depths,
            };
        }

        MarkBodiesHoldingKeyedTails(pattern);
    }

    /// <summary>
    /// EXPERIMENT (D85 night, kbody): marks each repeat whose body holds a repeat with a keyed tail.
    /// Its body guard ("the body never got to its end from p") then depends on group state too:
    /// a keyed tail guard inside can stop the body under one value of a group the rest reads.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    private static void MarkBodiesHoldingKeyedTails(PatternObject pattern)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var todo = new Stack<Node>();
        foreach (Node loop in pattern.NodeList)
        {
            if (loop.Op is not (Opcode.GreedyRepeat or Opcode.LazyRepeat))
            {
                continue;
            }

            uint index = loop.Values[0];
            seen.Clear();
            todo.Clear();
            todo.Push(loop.Next1.Node!);
            while (todo.Count > 0)
            {
                Node n = todo.Pop();
                if (!seen.Add(n) || (n.Op is Opcode.EndGreedyRepeat or Opcode.EndLazyRepeat && n.Values[0] == index))
                {
                    continue;
                }

                if (
                    n.Op is Opcode.GreedyRepeat or Opcode.LazyRepeat or Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne
                    && pattern.RepeatInfoAt((int)n.Values[0]).TailKey is not null
                )
                {
                    pattern.RepeatInfoAt((int)index).BodyHoldsKeyedTail = true;
                    break;
                }

                foreach (Node next in Successors(n))
                {
                    todo.Push(next);
                }
            }
        }
    }

    /// <summary>EXPERIMENT (D85 night, open): how many groups enclosing the repeat sit inside the group.</summary>
    /// <param name="enclosing">The enclosing groups of each node.</param>
    /// <param name="repeat">The repeat.</param>
    /// <param name="group">The group, one-based.</param>
    /// <returns>The depth, or -2 when the repeat is not seen inside the group.</returns>
    private static int DepthOf(Dictionary<object, int[]> enclosing, Node repeat, int group)
    {
        if (!enclosing.TryGetValue(repeat, out int[]? stack))
        {
            return -2;
        }

        int at = Array.LastIndexOf(stack, group);
        return at < 0 ? -2 : stack.Length - 1 - at;
    }

    /// <summary>
    /// EXPERIMENT (D85 night, open): the capture groups open around each node, outermost first, by a
    /// walk from the start that parks a group at the node that opens it in the matching direction
    /// and drops it at the node that captures. Nesting is syntactic, so the first visit decides.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>Node to its enclosing groups.</returns>
    private static Dictionary<object, int[]> EnclosingGroups(PatternObject pattern)
    {
        var result = new Dictionary<object, int[]>(ReferenceEqualityComparer.Instance);
        var todo = new Stack<(Node Node, int[] Open)>();
        todo.Push((pattern.StartNode!, []));
        while (todo.Count > 0)
        {
            (Node node, int[] open) = todo.Pop();
            if (!result.TryAdd(node, open))
            {
                continue;
            }

            int[] after = open;
            if (node.Op is Opcode.StartGroup or Opcode.EndGroup)
            {
                int group = (int)node.Values[0];
                if (node.Values[2] == 0)
                {
                    after = [.. open, group];
                }
                else if (open.Length > 0 && open[^1] == group)
                {
                    after = open[..^1];
                }
            }

            foreach (Node next in Successors(node))
            {
                todo.Push((next, after));
            }
        }

        return result;
    }

    /// <summary>What reading a group means when nothing narrower is known.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="group">The group, one-based.</param>
    /// <returns><see cref="Text"/> for a backreferenced group, else <see cref="Set"/>.</returns>
    private static byte ReadOf(PatternObject pattern, int group) => pattern.GroupInfoAt(group).TextRead ? Text : Set;

    /// <summary>
    /// The backward dataflow: for every node, the (group, flag) pairs live on entry to it, as four
    /// bits per read group.
    /// </summary>
    /// <param name="nodes">The pattern's nodes.</param>
    /// <param name="indexOf">Each node's place in <paramref name="nodes"/>.</param>
    /// <param name="tested">The read groups, one-based; slot i is <c>tested[i]</c>.</param>
    /// <returns>The live-in mask of each node.</returns>
    private static ulong[] Solve(List<Node> nodes, Dictionary<object, int> indexOf, int[] tested)
    {
        var slotOf = new Dictionary<int, int>();
        for (int i = 0; i < tested.Length; i++)
        {
            slotOf[tested[i]] = i;
        }

        HashSet<object> inTest = NodesInsideTests(nodes);
        ulong[] liveIn = new ulong[nodes.Count];

        // Iterate to the fixed point. Each pass goes backwards through the list, which is roughly
        // the order the compiler built the nodes in, so most facts settle in a pass or two.
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                Node node = nodes[i];
                ulong live = 0;
                foreach (Node next in Successors(node))
                {
                    if (indexOf.TryGetValue(next, out int j))
                    {
                        live |= liveIn[j];
                    }
                }

                live = Transfer(node, live, slotOf, inTest);
                if (live != liveIn[i])
                {
                    liveIn[i] = live;
                    changed = true;
                }
            }
        }

        return liveIn;
    }

    /// <summary>One node's effect on what is live: reads add, a capture outside a test kills.</summary>
    /// <param name="node">The node.</param>
    /// <param name="liveOut">What is live after it.</param>
    /// <param name="slotOf">The slot of each read group.</param>
    /// <param name="inTest">The nodes inside a lookaround body or a condition's test.</param>
    /// <returns>What is live before it.</returns>
    private static ulong Transfer(Node node, ulong liveOut, Dictionary<int, int> slotOf, HashSet<object> inTest)
    {
        switch (node.Op)
        {
            case Opcode.RefGroup:
            case Opcode.RefGroupFld:
            case Opcode.RefGroupFldRev:
            case Opcode.RefGroupIgn:
            case Opcode.RefGroupIgnRev:
            case Opcode.RefGroupRev:
                return slotOf.TryGetValue((int)node.Values[0], out int read) ? liveOut | Bit(read, Text) : liveOut;
            case Opcode.GroupExists:
                return node.Values[0] != 0 && slotOf.TryGetValue((int)node.Values[0], out int cond)
                    ? liveOut | Bit(cond, Set)
                    : liveOut;
            case Opcode.StartGroup:
            case Opcode.EndGroup:
            {
                if (!slotOf.TryGetValue((int)node.Values[0], out int slot))
                {
                    return liveOut;
                }

                if (node.Values[2] == 0)
                {
                    // The opening node: a closing node reached after it is the repeat's own group
                    // only if the repeat sits inside the brackets, which this path does not show.
                    return liveOut & ~Bit(slot, Inside);
                }

                // The closing node captures. It reads the current span (the counter's comparison)
                // and, outside a lookaround body or a condition's test, replaces the value every
                // later read sees. Inside one the capture may be thrown away (a negative test), so
                // it kills nothing there.
                ulong kept =
                    inTest.Contains(node) || D85Switches.MutateNoKill
                        ? liveOut
                        : liveOut & ~Bit(slot, (byte)(Text | Set | Span));
                return kept | Bit(slot, (byte)(Span | Inside));
            }
            default:
                return liveOut;
        }
    }

    /// <summary>The bit for one flag of one slot.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The mask.</returns>
    private static ulong Bit(int slot, byte flags) => (ulong)flags << (4 * slot);

    /// <summary>Every node a matcher can go to next from <paramref name="node"/>.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Up to three successors.</returns>
    private static IEnumerable<Node> Successors(Node node)
    {
        if (D85Switches.MutateStopAtEnd && node.Op is Opcode.EndGreedyRepeat or Opcode.EndLazyRepeat)
        {
            yield break;
        }

        if (node.Next1.Node is Node next1)
        {
            yield return next1;
        }

        if ((node.Status & NodeStatus.String) == 0 && node.Next2.Node is Node next2)
        {
            yield return next2;
        }

        if (node.TrueNode is Node trueNode)
        {
            yield return trueNode;
        }
    }

    /// <summary>The nodes inside any lookaround body or condition test, ends excluded.</summary>
    /// <param name="nodes">The pattern's nodes.</param>
    /// <returns>The set.</returns>
    private static HashSet<object> NodesInsideTests(List<Node> nodes)
    {
        var inside = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var todo = new Stack<Node>();
        foreach (Node test in nodes)
        {
            if (test.Op is not (Opcode.Lookaround or Opcode.Conditional) || test.Next1.Node is null)
            {
                continue;
            }

            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            todo.Push(test.Next1.Node);
            while (todo.Count > 0)
            {
                Node n = todo.Pop();
                if (
                    n.Op is Opcode.EndLookaround or Opcode.EndConditional
                    || ReferenceEquals(n, test.LookaroundEnd)
                    || !seen.Add(n)
                )
                {
                    continue;
                }

                _ = inside.Add(n);
                foreach (Node next in Successors(n))
                {
                    todo.Push(next);
                }
            }
        }

        return inside;
    }

    /// <summary>
    /// For each repeat, the repeats enclosing it whose body captures a read group: their progress
    /// test (<c>END_GREEDY_REPEAT</c>, "advanced or changed a capture") reads the counter, and so
    /// whether the enclosing iteration has already made progress when the inner tail is entered.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="tested">The read groups, one-based.</param>
    /// <returns>Inner repeat index to its enclosing repeats' indexes.</returns>
    private static Dictionary<int, List<int>> FindProgressRepeats(PatternObject pattern, int[] tested)
    {
        var result = new Dictionary<int, List<int>>();
        var testedSet = new HashSet<int>(tested);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var todo = new Stack<Node>();
        var inner = new List<int>();

        foreach (Node loop in pattern.NodeList)
        {
            if (loop.Op is not (Opcode.GreedyRepeat or Opcode.LazyRepeat))
            {
                continue;
            }

            uint index = loop.Values[0];
            seen.Clear();
            inner.Clear();
            bool captures = false;
            todo.Push(loop.Next1.Node!);
            while (todo.Count > 0)
            {
                Node n = todo.Pop();
                if (!seen.Add(n) || (n.Op is Opcode.EndGreedyRepeat or Opcode.EndLazyRepeat && n.Values[0] == index))
                {
                    continue;
                }

                if (
                    n.Op is Opcode.StartGroup or Opcode.EndGroup
                    && n.Values[2] != 0
                    && testedSet.Contains((int)n.Values[0])
                )
                {
                    captures = true;
                }

                if (n.Op is Opcode.GreedyRepeat or Opcode.LazyRepeat or Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne)
                {
                    inner.Add((int)n.Values[0]);
                }

                foreach (Node next in Successors(n))
                {
                    todo.Push(next);
                }
            }

            if (!captures)
            {
                continue;
            }

            foreach (int r in inner)
            {
                if (!result.TryGetValue(r, out List<int>? list))
                {
                    result[r] = list = [];
                }

                list.Add((int)index);
            }
        }

        return result;
    }
}
