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
/// <c>(*FAIL)</c>; this holds inside a fuzzy section too. A lookaround's contents are read in its
/// own direction from the current position. Both items of a conditional are read, and a
/// lookaround test is read as a lookaround; a <c>DEFINE</c> matches empty and is not read in
/// place. A backreference can match empty only if the group it reads can, or is not defined.
/// </para>
/// <para>
/// What the check does not catch, such as a lookbehind stepping back to where a group started
/// (<c>(?&lt;A&gt;a(?&lt;=(?=(?&amp;A))a)|b)</c>), is left to the matcher's runtime rule, which
/// fails a call re-entered at the position it is already at.
/// </para>
/// <para>
/// Cost: the empty-group fixpoint reads every group body once per round, and a round adds at least
/// one group, so it is O(g·m) for g groups and pattern size m; the walk and the cycle search are
/// O(m) and O(g + calls). Patterns with no group call skip the check entirely.
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
        Dictionary<int, RegexBase> bodies = new() { [0] = parsed };
        foreach ((int number, (Group group, _, _)) in info.DefinedGroups)
        {
            bodies[number] = group.Subpattern;
        }

        // The groups that can match empty: the least fixpoint, as a group's answer can rest on
        // the groups it calls.
        HashSet<int> empty = [];
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach ((int number, RegexBase body) in bodies)
            {
                if (!empty.Contains(number) && Nullable(body))
                {
                    _ = empty.Add(number);
                    grew = true;
                }
            }
        }

        // The call graph, from the pattern's own node out to every node a call can reach.
        Dictionary<(int, bool), List<((int, bool) To, CallGroup Call)>> edges = [];
        Queue<(int, bool)> todo = new([(0, reverse)]);
        HashSet<(int, bool)> reached = [(0, reverse)];
        while (todo.Count > 0)
        {
            (int number, bool back) = todo.Dequeue();
            List<((int, bool), CallGroup)> outgoing = [];
            edges[(number, back)] = outgoing;
            if (bodies.TryGetValue(number, out RegexBase? body))
            {
                Walk(body, back, true, outgoing);
            }
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
                List<((int, bool) To, CallGroup Call)> outgoing = edges[node];
                if (next == outgoing.Count)
                {
                    onPath[node] = false;
                    continue;
                }

                path.Push((node, next + 1));
                ((int group, bool) to, CallGroup call) = outgoing[next];
                if (onPath.TryAdd(to, true))
                {
                    path.Push((to, 0));
                }
                else if (onPath[to])
                {
                    throw Refusal(info, pattern, to.group, call);
                }
            }
        }

        bool Nullable(RegexBase node) =>
            node switch
            {
                Sequence sequence => sequence.Items.TrueForAll(Nullable),
                StringSet => true,
                Branch branch => branch.Branches.Count == 0 || branch.Branches.Exists(Nullable),
                GreedyRepeat repeat => repeat.MinCount == 0 || Nullable(repeat.Subpattern),
                Group group => Nullable(group.Subpattern),
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
        void Walk(RegexBase node, bool back, bool atStart, List<((int, bool), CallGroup)> outgoing)
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
                case GreedyRepeat repeat:
                    Walk(repeat.Subpattern, back, atStart, outgoing);
                    break;
                case Group group:
                    Walk(group.Subpattern, back, atStart, outgoing);
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
                    (int, bool) target = (call.GroupNumber, back);
                    if (atStart)
                    {
                        outgoing.Add((target, call));
                    }

                    if (reached.Add(target))
                    {
                        todo.Enqueue(target);
                    }

                    break;
            }
        }
    }

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
