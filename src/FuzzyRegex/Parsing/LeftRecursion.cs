using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// NOT UPSTREAM (known defects D4 and D72): refuses a pattern in which a group can call itself
/// before it has matched any text. Such a call recurses at the position it started from, so the
/// match it stands for can never be found: upstream recurses until it runs out of memory, and
/// PCRE2 stops at match time.
/// </summary>
/// <remarks>
/// <para>
/// The check reads the exact pattern, as written: a fuzzy section is judged by what it matches
/// with no errors. A node of the call graph is a group read in one direction, since a call inside
/// a lookbehind runs the group backwards. A node calls another when the call can be reached from
/// the node's start through items that can match empty. A cycle in that graph is a group that can
/// call itself at the same position, and the pattern is refused.
/// </para>
/// <para>
/// An item can match empty unless it is text, a character, a set, a grapheme or
/// <c>(*FAIL)</c>; this holds inside a fuzzy section too. A named list can if it holds the empty
/// string. A lookaround's contents are read in its own direction from the current position. Both
/// items of a conditional are read, and a lookaround test is read as a lookaround; a
/// <c>DEFINE</c> matches empty and is not read in place, and a repeat with a maximum of 0 is not
/// read at all. A group number can match empty if any group with that number can (a branch reset
/// or a name used twice gives a number several), and a call of it or a backreference to it then
/// can too; a number with no group is read as able to. Conservatively, which groups are set is
/// not followed: a backreference to a group not yet set, or a conditional on one, is read as if
/// it could match empty.
/// </para>
/// <para>
/// What the check does not catch is left to the matcher's runtime rule, which fails a call
/// re-entered at the position it is already at: a fuzzy deletion of the text before a call, a
/// lookbehind stepping back to where a group started
/// (<c>(?&lt;A&gt;a(?&lt;=(?=(?&amp;A))a)|b)</c>), and a lookahead that reads text with a
/// lookbehind inside it stepping back over it (<c>(?&lt;A&gt;(?=a(?&lt;=(?&amp;A)a))|b)</c>).
/// </para>
/// <para>
/// Cost: O(m) for pattern size m, plus the bodies each group's answer rests on. The empty-group
/// fixpoint is a worklist, so a body is read again only when a group it rests on becomes able to
/// match empty; a group written in place is an edge of the graph rather than walked again inside
/// its parent, so each body is walked once in each direction. Patterns with no group call skip the
/// check entirely.
/// </para>
/// </remarks>
internal static class LeftRecursion
{
    /// <summary>Throws if a group of the pattern can call itself before it has matched any text.</summary>
    /// <param name="info">The parse state, with the groups defined and the calls resolved.</param>
    /// <param name="parsed">The parsed pattern.</param>
    /// <param name="pattern">The pattern text, for the error.</param>
    /// <exception cref="FuzzyRegexParseException">The pattern is left-recursive.</exception>
    internal static void Check(Info info, RegexBase parsed, string pattern)
    {
        bool reverse = (info.Flags & RegexFlags.Reverse) != 0;

        // Every body of every group number: a branch reset or a name used twice gives a number
        // several. A body's nullability rests on the bodies nested directly in it and on the
        // numbers it calls or reads back, so each records whom to tell when it changes.
        ReferenceEqualityComparer same = ReferenceEqualityComparer.Instance;
        Dictionary<int, List<RegexBase>> bodies = [];
        Dictionary<RegexBase, int> numberOf = new(same);
        Dictionary<RegexBase, List<RegexBase>> parentsOf = new(same);
        Dictionary<int, List<RegexBase>> readersOf = [];
        Collect(parsed, parsed, 0);
        foreach ((int number, (Group group, _, _)) in info.DefinedGroups)
        {
            if (!bodies.ContainsKey(number))
            {
                Collect(group.Subpattern, group.Subpattern, number);
            }
        }

        // The bodies that can match empty: the least fixpoint, by a worklist, so a body is read
        // again only when something it rests on has changed.
        HashSet<RegexBase> emptyBodies = new(same);
        HashSet<int> empty = [];
        Dictionary<RegexBase, bool>? memo = null;
        Queue<RegexBase> work = new(numberOf.Keys);
        while (work.Count > 0)
        {
            RegexBase body = work.Dequeue();
            if (emptyBodies.Contains(body) || !Nullable(body))
            {
                continue;
            }

            _ = emptyBodies.Add(body);
            parentsOf.GetValueOrDefault(body)?.ForEach(work.Enqueue);
            if (empty.Add(numberOf[body]))
            {
                readersOf.GetValueOrDefault(numberOf[body])?.ForEach(work.Enqueue);
            }
        }

        // The answers are final now, so each node's is kept for the walk below.
        memo = new(same);

        // The call graph, from the pattern's own node out to every node a call can reach. A group
        // written in place is an edge too, with no call, so each body is walked once a direction.
        Dictionary<(int, bool), List<((int, bool) To, CallGroup? Call)>> edges = [];
        Queue<(int, bool)> todo = new([(0, reverse)]);
        HashSet<(int, bool)> reached = [(0, reverse)];
        while (todo.Count > 0)
        {
            (int number, bool back) = todo.Dequeue();
            List<((int, bool), CallGroup?)> outgoing = [];
            edges[(number, back)] = outgoing;
            bodies.GetValueOrDefault(number)?.ForEach(body => Walk(body, back, true, outgoing));
        }

        // A depth-first search: an edge back to a node still on the path closes a cycle.
        Dictionary<(int, bool), bool> onPath = [];
        foreach ((int, bool) root in edges.Keys)
        {
            if (!onPath.TryAdd(root, true))
            {
                continue;
            }

            Stack<((int, bool) Node, int Next)> path = new([(root, 0)]);
            while (path.Count > 0)
            {
                ((int, bool) node, int next) = path.Pop();
                List<((int, bool) To, CallGroup? Call)> outgoing = edges[node];
                if (next == outgoing.Count)
                {
                    onPath[node] = false;
                    continue;
                }

                path.Push((node, next + 1));
                if (onPath.TryAdd(outgoing[next].To, true))
                {
                    path.Push((outgoing[next].To, 0));
                }
                else if (onPath[outgoing[next].To])
                {
                    // Report the call on the cycle nearest its closing edge; nesting alone has no
                    // cycle, so there is one.
                    foreach (((int, bool) on, int taken) in path)
                    {
                        ((int group, bool) to, CallGroup? call) = edges[on][taken - 1];
                        if (call is not null)
                        {
                            throw Refusal(info, pattern, to.group, call);
                        }
                    }
                }
            }
        }

        // Records each group body under its number, and what each body's answer rests on.
        void Collect(RegexBase node, RegexBase body, int number)
        {
            if (ReferenceEquals(node, body))
            {
                numberOf[body] = number;
                (CollectionsMarshal.GetValueRefOrAddDefault(bodies, number, out _) ??= []).Add(body);
            }

            switch (node)
            {
                case Group group:
                    (CollectionsMarshal.GetValueRefOrAddDefault(parentsOf, group.Subpattern, out _) ??= []).Add(body);
                    Collect(group.Subpattern, group.Subpattern, group.GroupNumber);
                    return;
                case CallGroup call:
                    (CollectionsMarshal.GetValueRefOrAddDefault(readersOf, call.GroupNumber, out _) ??= []).Add(body);
                    return;
                case RefGroup reference:
                    (CollectionsMarshal.GetValueRefOrAddDefault(readersOf, reference.GroupNumber, out _) ??= []).Add(
                        body
                    );
                    return;
            }

            foreach (RegexBase child in Children(node))
            {
                Collect(child, body, number);
            }
        }

        // Before the fixpoint ends a body is read afresh each time; after it, once per node.
        bool Nullable(RegexBase node)
        {
            if (memo is not null && memo.TryGetValue(node, out bool known))
            {
                return known;
            }

            bool answer = Empty(node);
            memo?.Add(node, answer);
            return answer;
        }

        bool Empty(RegexBase node) =>
            node switch
            {
                Sequence sequence => sequence.Items.TrueForAll(Nullable),

                // A named list's members are sequences of characters, so it can match empty only
                // if it holds the empty string.
                Branch branch => branch.Branches.Count == 0 || branch.Branches.Exists(Nullable),
                GreedyRepeat repeat => repeat.MinCount == 0 || Nullable(repeat.Subpattern),
                Group group => emptyBodies.Contains(group.Subpattern),
                Atomic atomic => Nullable(atomic.Subpattern),
                Fuzzy section => Nullable(section.Subpattern),
                CallGroup call => empty.Contains(call.GroupNumber),
                Conditional conditional => conditional.IsDefine
                    || Nullable(conditional.YesItem)
                    || Nullable(conditional.NoItem),
                LookAroundConditional conditional => Nullable(conditional.YesItem) || Nullable(conditional.NoItem),
                RefGroup reference => !bodies.ContainsKey(reference.GroupNumber)
                    || empty.Contains(reference.GroupNumber),
                String text => text.Characters.Length == 0,
                Character or Any or Property or Range or SetBase or Grapheme or Failure => false,

                // Lookarounds, anchors and the other verbs are zero-width.
                _ => true,
            };

        // Records the calls reachable from the start ('atStart') and queues every call's target.
        void Walk(RegexBase node, bool back, bool atStart, List<((int, bool), CallGroup?)> outgoing)
        {
            switch (node)
            {
                case Sequence sequence:
                {
                    List<RegexBase> items = sequence.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        RegexBase item = items[back ? items.Count - 1 - i : i];
                        Walk(item, back, atStart, outgoing);
                        atStart = atStart && Nullable(item);
                    }

                    break;
                }
                case StringSet:
                    break;
                case Branch branch:
                    branch.Branches.ForEach(x => Walk(x, back, atStart, outgoing));
                    break;
                case GreedyRepeat repeat when repeat.MaxCount != 0:
                    Walk(repeat.Subpattern, back, atStart, outgoing);
                    break;
                case Group group:
                    Link((group.GroupNumber, back), null, atStart, outgoing);
                    break;
                case Atomic atomic:
                    Walk(atomic.Subpattern, back, atStart, outgoing);
                    break;
                case Fuzzy section:
                    Walk(section.Subpattern, back, atStart, outgoing);
                    break;
                case Conditional conditional when !conditional.IsDefine:
                    Walk(conditional.YesItem, back, atStart, outgoing);
                    Walk(conditional.NoItem, back, atStart, outgoing);
                    break;
                case LookAround look:
                    Walk(look.Subpattern, look.Behind, atStart, outgoing);
                    break;
                case LookAroundConditional conditional:
                    Walk(conditional.Subpattern, conditional.Behind, atStart, outgoing);
                    Walk(conditional.YesItem, back, atStart, outgoing);
                    Walk(conditional.NoItem, back, atStart, outgoing);
                    break;
                case CallGroup call:
                    Link((call.GroupNumber, back), call, atStart, outgoing);
                    break;
            }
        }

        void Link((int, bool) target, CallGroup? call, bool atStart, List<((int, bool), CallGroup?)> outgoing)
        {
            if (atStart)
            {
                outgoing.Add((target, call));
            }

            if (reached.Add(target))
            {
                todo.Enqueue(target);
            }
        }
    }

    private static List<RegexBase> Children(RegexBase node) =>
        node switch
        {
            Sequence sequence => sequence.Items,
            Branch branch => branch.Branches,
            GreedyRepeat repeat => [repeat.Subpattern],
            Atomic atomic => [atomic.Subpattern],
            Fuzzy section => [section.Subpattern],
            Conditional conditional => [conditional.YesItem, conditional.NoItem],
            LookAround look => [look.Subpattern],
            LookAroundConditional conditional => [conditional.Subpattern, conditional.YesItem, conditional.NoItem],
            _ => [],
        };

    private static FuzzyRegexParseException Refusal(Info info, string pattern, int group, CallGroup call)
    {
        string? name = info.GroupIndex.FirstOrDefault(entry => entry.Value == group).Key;
        string who = name is null ? $"group {group}" : $"group '{name}'";
        if (group == 0)
        {
            who = "the pattern";
        }

        return new FuzzyRegexParseException($"{who} can call itself without matching any text", pattern, call.Position);
    }
}
