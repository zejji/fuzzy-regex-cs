using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// The passes upstream runs over a freshly built graph: <c>optimise_pattern</c>
/// (<c>upstream/src/_regex.c</c> line 23821) and its parts.
/// </summary>
/// <remarks>
/// Upstream's <c>RE_CheckStack</c> (<c>:23188-23236</c>) and <c>RE_NodeStack</c>
/// (<c>:23573-23616</c>) are hand-rolled growable stacks of <c>PyMem_Realloc</c>'d arrays.
/// <see cref="Stack{T}"/> is the same data structure with the same push/pop order, so they are not
/// ported as types; <c>docs/PORTMAP.md</c> records the substitution.
/// </remarks>
internal static class Optimiser
{
    /// <summary>
    /// Upstream <c>optimise_pattern</c> (<c>upstream/src/_regex.c</c> lines 23821-23861).
    /// </summary>
    /// <param name="pattern">The pattern whose graph to optimise.</param>
    internal static void OptimisePattern(PatternObject pattern)
    {
        // Building the nodes is made simpler by allowing branches to have a single exit. These
        // need to be removed.
        SkipOneWayBranches(pattern);

        // Add position guards for repeat bodies containing a reference to a group or repeat tails
        // followed at some point by a reference to a group.
        _ = AddRepeatGuards(pattern, pattern.StartNode!);

        // Record the index of repeats and fuzzy sections within the body of atomic and lookaround
        // nodes.
        RecordSubpatternRepeatsAndFuzzySections(null, 0, pattern.RepeatCount, pattern.StartNode);

        foreach (CallRefInfo info in pattern.CallRefInfoList)
        {
            RecordSubpatternRepeatsAndFuzzySections(null, 0, pattern.RepeatCount, info.Node);
        }

        // Discard any unused nodes.
        DiscardUnusedNodes(pattern);

        // Set the test nodes.
        SetTestNodes(pattern);

        // Mark all the group that are named.
        MarkNamedGroups(pattern);

        // NOT UPSTREAM. Withdraw the failure memo from a pattern that can read more than the
        // position; see RepeatInfo.FailureMemo.
        KeepFailureMemosSound(pattern);

        // NOT UPSTREAM (D17). List the groups an empty iteration's state is keyed by.
        CollectTestedGroups(pattern);
        FindFuzzyLimit(pattern);

        // NOT UPSTREAM. Collect the assertions that pin a fuzzy match to the search anchor; see
        // FindAnchorGuards. Last because it reads the graph the passes above leave behind.
        FindAnchorGuards(pattern);

        // NOT UPSTREAM (D61): the assertions that dominate each fuzzy item; see FindAnchorPins.
        FindAnchorPins(pattern, unionAtMerges: false);
    }

    /// <summary>
    /// Fills in <see cref="PatternObject.AnchorGuards"/>: the zero-width position assertions the
    /// pattern must pass before it does anything else. <b>Not an upstream pass</b> - it exists for
    /// the fix to upstream issues 563 and 564, described in <c>docs/DIVERGENCES.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk follows <see cref="Node.Next1"/> from the start node and stops at the first node
    /// that could send matching down more than one path, so everything it collects is passed on
    /// every path through the pattern. That is the whole point: S50 held the same answer in a
    /// <c>MatchState</c> field that the backtracking engine neither saved nor restored, and it was
    /// wrong in both directions - an assertion that held only on an abandoned path still pinned the
    /// anchor, and the clears that fixed that also threw away a pin set outside the construct.
    /// A property of the pattern cannot be wrong about which path the engine is on.
    /// </para>
    /// <para>
    /// It walks through two kinds of node. Group markers carry no test of their own. And a
    /// one-character node with a step of 0 is the test the compiler hoists in front of a leading
    /// anchor - <c>'^a'</c> compiles to <c>CHARACTER(step 0) - START_OF_STRING - CHARACTER(step
    /// 1)</c>, see the remarks on <c>Matcher.Step</c> - so stopping there would miss the anchor it
    /// was hoisted past.
    /// </para>
    /// <para>
    /// An assertion inside a fuzzy section is not collected, and stops the walk: an error can get
    /// past it, so it does not pin every path. The path that passes it without an error is pinned
    /// all the same, and so is a path down one branch of an alternation; for those this sets
    /// <see cref="PatternObject.PinsAnchorOnPath"/> and the matcher decides as it goes (D61).
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    private static void FindAnchorGuards(PatternObject pattern)
    {
        if (!pattern.IsFuzzy)
        {
            return;
        }

        List<Node>? guards = null;
        Node? node = pattern.StartNode;

        // The chain this walk follows cannot loop - it stops at every node that branches or repeats
        // - but it reads a graph four other passes have just rewritten, so it is bounded anyway.
        for (int steps = pattern.NodeList.Count; node is not null && steps > 0; steps--)
        {
            if ((node.Status & NodeStatus.Fuzzy) != 0)
            {
                break;
            }

            if (IsPositionAssertion(node.Op))
            {
                (guards ??= []).Add(node);
            }
            else if (
                node.Op is not (Opcode.StartGroup or Opcode.EndGroup)
                && !(node.Step == 0 && NodeQueries.MatchesOneCharacter(node))
            )
            {
                break;
            }

            node = node.Next1.Node;
        }

        pattern.AnchorGuards = guards;
    }

    /// <summary>
    /// Fills in <see cref="Node.AnchorPins"/> and <see cref="PatternObject.PinsAnchorOnPath"/>: for
    /// each fuzzy item, the assertions other than the <see cref="PatternObject.AnchorGuards"/> that
    /// DOMINATE it, meaning every path from the start of the pattern to the item passes them. <b>Not
    /// an upstream pass</b> - it is known defect D61's fix, described in <c>docs/DIVERGENCES.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why dominance, and not "an assertion the path passed". The ban on an insertion at the search
    /// anchor (<c>upstream/src/_regex.c</c>:10214) is sound when starting one character later finds
    /// the same match minus the insertion. An assertion that held at the anchor and fails one
    /// character on rules that out only if the later start has to pass it too. With another route to
    /// the item it need not: <c>(?:^|x)(?:ab){i&lt;=1}</c> over 'xab' has the exact (0, 3) through
    /// <c>x</c>, and <c>(?:\M|)(?:dog){e&lt;=1}</c> finditer over 'dog dog' must not glue the space to
    /// the second 'dog'. A per-path flag answered both wrongly (refuted in review, 2026-09-30).
    /// </para>
    /// <para>
    /// A forward "must" data-flow over the node graph: what reaches a node is what every predecessor
    /// passes on, and an assertion adds itself. Nothing is lost by ignoring what a path consumes: an
    /// item reached at the anchor was reached by a path that consumed nothing, so every assertion on
    /// it was tested at the anchor. The exceptions are the constructs that put the text position back:
    /// a lookaround's body and a conditional's test. Their end nodes pass nothing on, and the
    /// lookaround's second exit, and the conditional's <see cref="Node.TrueNode"/>, carry what held
    /// before them instead. A group call jumps along an edge the graph does not hold, so a pattern
    /// with one gets no pins at all. SHORTCUT: that is the ceiling for calls; the upgrade is to follow
    /// each call into its group and back.
    /// </para>
    /// <para>
    /// A lookaround pins too when its body is one character test
    /// (<see cref="IsOneCharacterLookaround"/>), which <c>Matcher.LookaroundHoldsAt</c> can answer
    /// one character on without touching the match's state. SHORTCUT: any lookaround whose body reads
    /// no path state (no capture read or backreference, conditional, call, verb, <c>\K</c> or fuzzy
    /// section) could pin the same way, answered in an isolated state; that is the upgrade path.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="unionAtMerges">
    /// Test-only: a planted bug that joins paths by union instead of intersection, so an assertion
    /// on any one path pins. <c>FuzzyAnchorPinTests</c> uses it to prove the rows catch that bug,
    /// which the oracle entry keyed on switching the pins off cannot. The library passes
    /// <see langword="false"/>.
    /// </param>
    internal static void FindAnchorPins(PatternObject pattern, bool unionAtMerges)
    {
        pattern.PinsAnchorOnPath = false;

        if (!pattern.IsFuzzy || pattern.HasGroupCalls || pattern.StartNode is not { } start)
        {
            return;
        }

        // Number the nodes by their place in the list, which PatternObject.Compile does again, in
        // the same order, once the optimiser is done; the data-flow indexes arrays by it.
        List<Node> nodes = pattern.NodeList;
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].Index = i;
        }

        // The candidates, one bit each. SHORTCUT: past 64 the rest are ignored, which only pins
        // less; the upgrade is a wider set.
        int[] bit = new int[nodes.Count];
        List<Node> candidates = [];

        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            bit[i] = -1;

            // The constructs whose end hands back to a node the data-flow must reach another way.
            if (
                (node.Op == Opcode.Lookaround && node.Next2.Node is null)
                || (node.Op == Opcode.Conditional && node.TrueNode is null)
            )
            {
                return;
            }

            if (
                candidates.Count < 64
                && (IsPositionAssertion(node.Op) || IsOneCharacterLookaround(node))
                && pattern.AnchorGuards?.Any(g => ReferenceEquals(g, node)) != true
            )
            {
                bit[i] = candidates.Count;
                candidates.Add(node);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        // What every path to the node has passed; a node not yet reached (reached[i] false) stands
        // for every bit.
        ulong[] reaching = new ulong[nodes.Count];
        bool[] reached = new bool[nodes.Count];
        var work = new Stack<Node>();

        if (!InList(nodes, start))
        {
            return;
        }

        reached[start.Index] = true;
        work.Push(start);

        while (work.Count > 0)
        {
            Node node = work.Pop();
            ulong passed = reaching[node.Index];

            if (bit[node.Index] >= 0)
            {
                passed |= 1UL << bit[node.Index];
            }

            // A lookaround's body and a conditional's test put the position back at their end: the
            // node after them is reached through the construct's own bypass edge instead.
            if (node.Op is Opcode.EndLookaround or Opcode.EndConditional)
            {
                continue;
            }

            foreach (Node next in Successors(node))
            {
                // An edge to a node the list does not hold would be a path this pass cannot see.
                if (!InList(nodes, next))
                {
                    ClearPins(nodes);
                    return;
                }

                int n = next.Index;
                ulong merged = passed;
                if (reached[n])
                {
                    merged = unionAtMerges ? reaching[n] | passed : reaching[n] & passed;
                }

                if (!reached[n] || merged != reaching[n])
                {
                    reached[n] = true;
                    reaching[n] = merged;
                    work.Push(next);
                }
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            node.AnchorPins = null;

            if (!reached[i] || reaching[i] == 0 || (node.Status & NodeStatus.Fuzzy) == 0)
            {
                continue;
            }

            List<Node> pins = [];
            for (int c = 0; c < candidates.Count; c++)
            {
                if ((reaching[i] & (1UL << c)) != 0)
                {
                    pins.Add(candidates[c]);
                }
            }

            node.AnchorPins = pins;
            pattern.PinsAnchorOnPath = true;
        }
    }

    /// <summary>Whether a node is the one at its <see cref="Node.Index"/> in the list.</summary>
    /// <param name="nodes">The pattern's node list.</param>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> if the list holds it there.</returns>
    private static bool InList(List<Node> nodes, Node node) =>
        node.Index >= 0 && node.Index < nodes.Count && ReferenceEquals(nodes[node.Index], node);

    /// <summary>Removes every pin, for a pattern <see cref="FindAnchorPins"/> cannot analyse.</summary>
    /// <param name="nodes">The pattern's node list.</param>
    private static void ClearPins(List<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            node.AnchorPins = null;
        }
    }

    /// <summary>
    /// The nodes matching can go to next from a node, for <see cref="FindAnchorPins"/>. Every
    /// <see cref="Node.Next1"/>, and <see cref="Node.Next2"/> and <see cref="Node.TrueNode"/> except
    /// where the second exit is not control flow: a set's out-of-line members, a single-item
    /// repeat's item, and a fuzzy section's insertion charset. Taking an exit that is not control
    /// flow only adds a path, which can only pin less.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>Its successors.</returns>
    private static IEnumerable<Node> Successors(Node node)
    {
        if (node.Next1.Node is { } next1)
        {
            yield return next1;
        }

        bool secondIsData =
            node.Op
            is Opcode.Fuzzy
                or Opcode.EndFuzzy
                or Opcode.GreedyRepeatOne
                or Opcode.LazyRepeatOne
                or Opcode.SetDiff
                or Opcode.SetDiffIgn
                or Opcode.SetDiffIgnRev
                or Opcode.SetDiffRev
                or Opcode.SetInter
                or Opcode.SetInterIgn
                or Opcode.SetInterIgnRev
                or Opcode.SetInterRev
                or Opcode.SetSymDiff
                or Opcode.SetSymDiffIgn
                or Opcode.SetSymDiffIgnRev
                or Opcode.SetSymDiffRev
                or Opcode.SetUnion
                or Opcode.SetUnionIgn
                or Opcode.SetUnionIgnRev
                or Opcode.SetUnionRev;

        if (!secondIsData && node.Next2.Node is { } next2)
        {
            yield return next2;
        }

        if (node.TrueNode is { } trueNode)
        {
            yield return trueNode;
        }
    }

    /// <summary>
    /// Whether a node is a lookaround whose body is one character test and nothing else, which
    /// <c>Matcher.LookaroundHoldsAt</c> can answer at any position (D61).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> for such a lookaround.</returns>
    private static bool IsOneCharacterLookaround(Node node) =>
        node.Op == Opcode.Lookaround
        && node.Next1.Node is { } body
        && body.Step != 0
        && NodeQueries.MatchesOneCharacter(body)
        && body.Next1.Node?.Op == Opcode.EndLookaround;

    /// <summary>
    /// Whether an opcode asks only where the text position is, so that <c>Matcher</c> can answer it
    /// at any position without matching anything.
    /// </summary>
    /// <remarks>
    /// Exactly the opcodes <c>Matcher.TryMatchZeroWidth</c> answers. A lookaround is deliberately
    /// not one of them: it runs a subpattern, and <c>(?=x)</c> before a fuzzy section is a
    /// character test wearing an assertion's clothes - upstream loses the leading insertion there
    /// and this port agrees with it.
    /// </remarks>
    /// <param name="op">The opcode.</param>
    /// <returns><see langword="true"/> if it is a position assertion.</returns>
    private static bool IsPositionAssertion(Opcode op) =>
        op switch
        {
            Opcode.Boundary
            or Opcode.DefaultBoundary
            or Opcode.DefaultEndOfWord
            or Opcode.DefaultStartOfWord
            or Opcode.EndOfLine
            or Opcode.EndOfLineU
            or Opcode.EndOfString
            or Opcode.EndOfStringLine
            or Opcode.EndOfStringLineU
            or Opcode.EndOfWord
            or Opcode.GraphemeBoundary
            or Opcode.SearchAnchor
            or Opcode.StartOfLine
            or Opcode.StartOfLineU
            or Opcode.StartOfString
            or Opcode.StartOfWord => true,
            _ => false,
        };

    /// <summary>
    /// Upstream <c>skip_one_way_branches</c> (line 23136): a branch with a single exit exists only
    /// to give the builders something to attach to, so nothing should point at one.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    private static void SkipOneWayBranches(PatternObject pattern)
    {
        bool modified;

        // If a node refers to a 1-way branch then make the former refer to the latter's
        // destination. Repeat until they're all done.
        do
        {
            modified = false;

            foreach (Node node in pattern.NodeList)
            {
                // Check the first destination.
                Node? next = node.Next1.Node;
                if (next is not null && next.Op == Opcode.Branch && next.Next2.Node is null)
                {
                    node.Next1.Node = next.Next1.Node;
                    modified = true;
                }

                // Check the second destination.
                next = node.Next2.Node;
                if (next is not null && next.Op == Opcode.Branch && next.Next2.Node is null)
                {
                    node.Next2.Node = next.Next1.Node;
                    modified = true;
                }

                // Check the true branch for CONDITIONAL. NOT UPSTREAM (known defect D55, ledger
                // entry 62): upstream tests the branch's own true_node here (_regex.c:23206), which
                // no BRANCH sets, so every alternation at the start of a negative condition's yes
                // branch lost all but its first alternative. A branch is 1-way when it has no
                // second exit, as the two checks above test.
                next = node.TrueNode;
                if (next is not null && next.Op == Opcode.Branch && next.Next2.Node is null)
                {
                    node.TrueNode = next.Next1.Node;
                    modified = true;
                }
            }
        } while (modified);

        // The start node might be a 1-way branch. Skip over it because it'll be removed. It might
        // even be the first in a chain.
        while (pattern.StartNode!.Op == Opcode.Branch && pattern.StartNode.Next2.Node is null)
        {
            pattern.StartNode = pattern.StartNode.Next1.Node;
        }
    }

    /// <summary>
    /// Upstream <c>add_repeat_guards</c> (line 23237): works out which repeats need their body or
    /// tail guarded against re-matching at a position already tried.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="startNode">Where to start walking.</param>
    /// <returns>The start node's own repeat/reference status.</returns>
    private static uint AddRepeatGuards(PatternObject pattern, Node startNode)
    {
        var stack = new Stack<(Node Node, uint Result)>();

        stack.Push((startNode, NodeStatus.Neither));

        while (stack.Count > 0)
        {
            (Node node, uint result) = stack.Pop();

            if ((node.Status & NodeStatus.VisitedAg) != 0)
            {
                continue;
            }

            switch (node.Op)
            {
                case Opcode.Branch:
                {
                    Node branch1 = node.Next1.Node!;
                    Node branch2 = node.Next2.Node!;
                    bool visitedBranch1 = (branch1.Status & NodeStatus.VisitedAg) != 0;
                    bool visitedBranch2 = (branch2.Status & NodeStatus.VisitedAg) != 0;

                    if (visitedBranch1 && visitedBranch2)
                    {
                        uint branch1Result = branch1.Status & (NodeStatus.Repeat | NodeStatus.Ref);
                        uint branch2Result = branch2.Status & (NodeStatus.Repeat | NodeStatus.Ref);

                        node.Status |= NodeStatus.VisitedAg | NodeStatus.Max3(result, branch1Result, branch2Result);
                    }
                    else
                    {
                        stack.Push((node, result));
                        if (!visitedBranch2)
                        {
                            stack.Push((branch2, NodeStatus.Neither));
                        }

                        if (!visitedBranch1)
                        {
                            stack.Push((branch1, NodeStatus.Neither));
                        }
                    }

                    break;
                }
                case Opcode.EndGreedyRepeat:
                case Opcode.EndLazyRepeat:
                    node.Status |= NodeStatus.VisitedAg;
                    break;
                case Opcode.GreedyRepeat:
                case Opcode.LazyRepeat:
                {
                    bool limited = ~node.Values[2] != 0;

                    Node body = node.Next1.Node!;
                    Node tail = node.Next2.Node!;
                    bool visitedBody = (body.Status & NodeStatus.VisitedAg) != 0;
                    bool visitedTail = (tail.Status & NodeStatus.VisitedAg) != 0;

                    if (visitedBody && visitedTail)
                    {
                        uint bodyResult = body.Status & (NodeStatus.Repeat | NodeStatus.Ref);
                        uint tailResult = tail.Status & (NodeStatus.Repeat | NodeStatus.Ref);

                        RepeatInfo repeatInfo = pattern.RepeatInfoAt((int)node.Values[0]);
                        if (bodyResult != NodeStatus.Ref)
                        {
                            repeatInfo.Status |= NodeStatus.Body;
                        }

                        if (tailResult != NodeStatus.Ref)
                        {
                            repeatInfo.Status |= NodeStatus.Tail;
                        }

                        result = NodeStatus.Max2(result, limited ? NodeStatus.Limited : NodeStatus.Repeat);
                        node.Status |= NodeStatus.VisitedAg | NodeStatus.Max3(result, bodyResult, tailResult);
                    }
                    else
                    {
                        stack.Push((node, result));
                        if (!visitedTail)
                        {
                            stack.Push((tail, NodeStatus.Neither));
                        }

                        if (!visitedBody)
                        {
                            if (limited)
                            {
                                body.Status |= NodeStatus.VisitedAg | NodeStatus.Limited;
                            }
                            else
                            {
                                stack.Push((body, NodeStatus.Neither));
                            }
                        }
                    }

                    break;
                }
                case Opcode.GreedyRepeatOne:
                case Opcode.LazyRepeatOne:
                {
                    Node tail = node.Next1.Node!;
                    bool visitedTail = (tail.Status & NodeStatus.VisitedAg) != 0;

                    if (visitedTail)
                    {
                        bool limited = ~node.Values[2] != 0;

                        uint tailResult = tail.Status & (NodeStatus.Repeat | NodeStatus.Ref);

                        RepeatInfo repeatInfo = pattern.RepeatInfoAt((int)node.Values[0]);

                        // Removing guard here to fix issue 494 and prevent regression of issue 495.
                        // repeat_info->status |= RE_STATUS_BODY;

                        if (tailResult != NodeStatus.Ref)
                        {
                            repeatInfo.Status |= NodeStatus.Tail;
                        }

                        result = NodeStatus.Max2(result, limited ? NodeStatus.Limited : NodeStatus.Repeat);
                        node.Status |= NodeStatus.VisitedAg | NodeStatus.Max3(result, NodeStatus.Repeat, tailResult);
                    }
                    else
                    {
                        stack.Push((node, result));
                        stack.Push((tail, NodeStatus.Neither));
                    }

                    break;
                }
                case Opcode.GroupExists:
                {
                    Node branch1 = node.Next1.Node!;
                    Node branch2 = node.Next2.Node!;
                    bool visitedBranch1 = (branch1.Status & NodeStatus.VisitedAg) != 0;
                    bool visitedBranch2 = (branch2.Status & NodeStatus.VisitedAg) != 0;

                    if (visitedBranch1 && visitedBranch2)
                    {
                        uint branch1Result = branch1.Status & (NodeStatus.Repeat | NodeStatus.Ref);
                        uint branch2Result = branch2.Status & (NodeStatus.Repeat | NodeStatus.Ref);

                        node.Status |=
                            NodeStatus.VisitedAg
                            | NodeStatus.Max4(result, branch1Result, branch2Result, NodeStatus.Ref);
                    }
                    else
                    {
                        stack.Push((node, result));
                        if (!visitedBranch2)
                        {
                            stack.Push((branch2, NodeStatus.Neither));
                        }

                        if (!visitedBranch1)
                        {
                            stack.Push((branch1, NodeStatus.Neither));
                        }
                    }

                    break;
                }
                case Opcode.RefGroup:
                case Opcode.RefGroupFld:
                case Opcode.RefGroupFldRev:
                case Opcode.RefGroupIgn:
                case Opcode.RefGroupIgnRev:
                case Opcode.RefGroupRev:
                {
                    Node tail = node.Next1.Node!;
                    bool visitedTail = (tail.Status & NodeStatus.VisitedAg) != 0;

                    if (visitedTail)
                    {
                        node.Status |= NodeStatus.VisitedAg | NodeStatus.Ref;
                    }
                    else
                    {
                        stack.Push((node, result));
                        stack.Push((tail, NodeStatus.Neither));
                    }

                    break;
                }
                case Opcode.Success:
                    node.Status |= NodeStatus.VisitedAg | result;
                    break;
                default:
                {
                    Node? tail = node.Next1.Node;

                    if (tail is null)
                    {
                        node.Status |= NodeStatus.VisitedAg | result;
                        break;
                    }

                    if ((tail.Status & NodeStatus.VisitedAg) != 0)
                    {
                        uint tailResult = tail.Status & (NodeStatus.Repeat | NodeStatus.Ref);
                        node.Status |= NodeStatus.VisitedAg | tailResult;
                    }
                    else
                    {
                        stack.Push((node, result));
                        stack.Push((node.Next1.Node!, result));
                    }

                    break;
                }
            }
        }

        return startNode.Status & (NodeStatus.Repeat | NodeStatus.Ref);
    }

    /// <summary>
    /// Upstream <c>add_index</c> (line 23482): appends an index to a node's values unless it is
    /// already there.
    /// </summary>
    /// <param name="node">The node to append to, or null.</param>
    /// <param name="offset">Where the index count sits within the values.</param>
    /// <param name="index">The index to add.</param>
    private static void AddIndex(Node? node, int offset, int index)
    {
        if (node is null)
        {
            return;
        }

        uint indexCount = node.Values[offset];
        int firstIndex = offset + 1;

        // Is the index already present?
        for (int i = 0; i < indexCount; i++)
        {
            if (node.Values[firstIndex + i] == index)
            {
                return;
            }
        }

        // Allocate more space for the new index.
        node.Values.Add(0);

        node.Values[firstIndex + (int)node.Values[offset]] = (uint)index;
        node.Values[offset]++;
    }

    /// <summary>
    /// Upstream <c>record_subpattern_repeats_and_fuzzy_sections</c> (line 23517).
    /// </summary>
    /// <remarks>
    /// <b>Both call sites pass a null parent</b> (<c>:23837</c> and <c>:23845</c>), so
    /// <see cref="AddIndex"/> returns immediately every time and the walk's only lasting effect is
    /// the <see cref="NodeStatus.VisitedRep"/> mark it leaves behind. Ported as written, parent and
    /// all: the parameter is what an upstream release that restores the atomic and lookaround call
    /// sites would need, and dropping it would make that diff stop mapping onto this file.
    /// </remarks>
    /// <param name="parentNode">The atomic or lookaround node to record against, or null.</param>
    /// <param name="offset">Where the index count sits within the parent's values.</param>
    /// <param name="repeatCount">How many repeats the pattern has, which offsets the fuzzy indexes.</param>
    /// <param name="node">Where to start walking.</param>
    private static void RecordSubpatternRepeatsAndFuzzySections(
        Node? parentNode,
        int offset,
        int repeatCount,
        Node? node
    )
    {
        while (node is not null)
        {
            if ((node.Status & NodeStatus.VisitedRep) != 0)
            {
                return;
            }

            node.Status |= NodeStatus.VisitedRep;

            switch (node.Op)
            {
                case Opcode.Branch:
                case Opcode.GroupExists:
                    RecordSubpatternRepeatsAndFuzzySections(parentNode, offset, repeatCount, node.Next1.Node);
                    node = node.Next2.Node;
                    break;
                case Opcode.EndFuzzy:
                    node = node.Next1.Node;
                    break;
                case Opcode.EndGreedyRepeat:
                case Opcode.EndLazyRepeat:
                    return;
                case Opcode.Fuzzy:
                    // Record the fuzzy index.
                    AddIndex(parentNode, offset, repeatCount + (int)node.Values[0]);
                    node = node.Next1.Node;
                    break;
                case Opcode.GreedyRepeat:
                case Opcode.LazyRepeat:
                    // Record the repeat index.
                    AddIndex(parentNode, offset, (int)node.Values[0]);
                    RecordSubpatternRepeatsAndFuzzySections(parentNode, offset, repeatCount, node.Next1.Node);
                    node = node.Next2.Node;
                    break;
                case Opcode.GreedyRepeatOne:
                case Opcode.LazyRepeatOne:
                    // Record the repeat index.
                    AddIndex(parentNode, offset, (int)node.Values[0]);
                    node = node.Next1.Node;
                    break;
                default:
                    node = node.Next1.Node;
                    break;
            }
        }
    }

    /// <summary>Upstream <c>use_nodes</c> (line 23617): marks the nodes that are reachable.</summary>
    /// <param name="node">Where to start.</param>
    private static void UseNodes(Node? node)
    {
        var stack = new Stack<Node>();

        while (node is not null)
        {
            while (node is not null && (node.Status & NodeStatus.Used) == 0)
            {
                node.Status |= NodeStatus.Used;
                if ((node.Status & NodeStatus.String) == 0 && node.Next2.Node is not null)
                {
                    stack.Push(node.Next2.Node);
                }

                node = node.Next1.Node;
            }

            node = stack.Count > 0 ? stack.Pop() : null;
        }
    }

    /// <summary>
    /// Upstream <c>discard_unused_nodes</c> (line 23641): optimising can leave nodes nothing points
    /// at.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    private static void DiscardUnusedNodes(PatternObject pattern)
    {
        // Mark the nodes which are being used.
        UseNodes(pattern.StartNode);

        foreach (CallRefInfo info in pattern.CallRefInfoList)
        {
            UseNodes(info.Node);
        }

        // Upstream frees the rest; here dropping the reference is the same act.
        _ = pattern.NodeList.RemoveAll(static node => (node.Status & NodeStatus.Used) == 0);
    }

    /// <summary>
    /// Clears <see cref="RepeatInfo.FailureMemo"/> on every repeat when the pattern has anything
    /// that makes the rest of a match depend on more than the position it resumes from. <b>Not an
    /// upstream pass.</b>
    /// </summary>
    /// <remarks>
    /// These are the pattern-wide conditions, so one of them anywhere withdraws the memo
    /// everywhere: a group can be set before a repeat and read after it. A backreference or a
    /// group-exists conditional reads the captures; a group call returns to wherever it was called
    /// from; <c>(*PRUNE)</c> and <c>(*SKIP)</c> cut the backtracking stack and <c>(*SKIP)</c> moves
    /// the slice; fuzzy matching can come back to a position with a different error budget; and
    /// POSIX matching goes on after a success, so a path that ended in a match is recorded as a
    /// failure; and a <c>\K</c> inside an atomic group or a lookaround leaves the moved start
    /// behind when the path that moved it fails (<see cref="PatternObject.KeepInSubmatch"/>). The
    /// compiler's per-repeat half is in <c>NodeCompiler.BuildRepeat</c>.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    private static void KeepFailureMemosSound(PatternObject pattern)
    {
        bool unsafePattern =
            pattern.IsFuzzy
            || pattern.KeepInSubmatch
            || (pattern.Flags & RegexFlags.Posix) != 0
            || pattern.NodeList.Exists(static node =>
                node.Op
                    is Opcode.RefGroup
                        or Opcode.RefGroupFld
                        or Opcode.RefGroupFldRev
                        or Opcode.RefGroupIgn
                        or Opcode.RefGroupIgnRev
                        or Opcode.RefGroupRev
                        or Opcode.GroupExists
                        or Opcode.GroupCall
                        or Opcode.CallRef
                        or Opcode.Prune
                        or Opcode.Skip
            );

        if (!unsafePattern)
        {
            return;
        }

        foreach (RepeatInfo info in pattern.RepeatInfoList)
        {
            info.FailureMemo = false;
        }
    }

    /// <summary>
    /// Fills in <see cref="PatternObject.TestedGroups"/>: the groups whose spans the rest of a match
    /// can read, which are the ones <c>START_GROUP</c> and <c>END_GROUP</c> bump the capture-change
    /// counter for. <b>Not an upstream pass</b> (D17); see <c>Matcher.RevisitsEmptyIterationState</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    private static void CollectTestedGroups(PatternObject pattern)
    {
        List<int> tested = [];
        for (int group = 1; group <= pattern.TrueGroupCount; group++)
        {
            if (pattern.GroupInfoAt(group).Referenced)
            {
                tested.Add(group);
            }
        }

        pattern.TestedGroups = [.. tested];
    }

    /// <summary>
    /// Fills in <see cref="PatternObject.LargestFuzzyLimit"/> from every fuzzy section's limits.
    /// <b>Not an upstream pass</b> (D17); see <c>Matcher.ErrorCountCap</c>.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    private static void FindFuzzyLimit(PatternObject pattern)
    {
        long largest = -1;
        foreach (Node node in pattern.NodeList)
        {
            if (node.Op != Opcode.Fuzzy)
            {
                continue;
            }

            for (int i = FuzzyValue.MinBase; i <= FuzzyValue.MaxErr; i++)
            {
                largest = Matcher.LargerFiniteBound(largest, node.Values[i]);
            }

            largest = Matcher.LargerFiniteBound(largest, node.Values[FuzzyValue.MaxCost]);
        }

        pattern.LargestFuzzyLimit = largest;
    }

    /// <summary>Upstream <c>mark_named_groups</c> (line 23672).</summary>
    /// <param name="pattern">The pattern.</param>
    private static void MarkNamedGroups(PatternObject pattern)
    {
        // Upstream asks `PyDict_Contains(pattern->indexgroup, i + 1)`. indexgroup is groupindex
        // inverted (CompiledPattern's remarks), so a group has a name exactly when its number is a
        // value of groupindex.
        HashSet<int> named = [.. pattern.GroupIndex.Values];

        for (int i = 0; i < pattern.PublicGroupCount; i++)
        {
            pattern.GroupInfoAt(i + 1).HasName = named.Contains(i + 1);
        }
    }

    /// <summary>
    /// Upstream <c>can_test_past</c> (line 23697): whether the matcher may look past this node when
    /// testing.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> if it can be looked past.</returns>
    private static bool CanTestPast(Node node) =>
        node.Op switch
        {
            Opcode.EndGroup or Opcode.StartGroup => true,
            Opcode.GreedyRepeat or Opcode.LazyRepeat => node.Values[1] > 0,
            _ => false,
        };

    /// <summary>
    /// Upstream <c>set_test_node</c> (line 23716): the test node lets the matcher look ahead in the
    /// pattern, so it can avoid the cost of housekeeping only to find that what follows does not
    /// match anyway.
    /// </summary>
    /// <remarks>
    /// The match loop's fast path that consults this is deferred to Phase 7 (DECISIONS
    /// 2026-08-31). The marking is done now anyway, so the graph has upstream's shape and Phase 7
    /// is a switch to flip rather than a re-plumb.
    /// </remarks>
    /// <param name="next">The slot to fill in.</param>
    private static void SetTestNode(NextNode next)
    {
        Node? node = next.Node;

        next.Test = node;
        next.MatchNext = node;
        next.MatchStep = 0;

        if (node is null)
        {
            return;
        }

        Node test = node;
        while (CanTestPast(test))
        {
            test = test.Next1.Node!;
        }

        next.Test = test;

        if (test != node)
        {
            return;
        }

        switch (test.Op)
        {
            case Opcode.Any:
            case Opcode.AnyAll:
            case Opcode.AnyAllRev:
            case Opcode.AnyRev:
            case Opcode.AnyU:
            case Opcode.AnyURev:
            case Opcode.Boundary:
            case Opcode.Character:
            case Opcode.CharacterIgn:
            case Opcode.CharacterIgnRev:
            case Opcode.CharacterRev:
            case Opcode.DefaultBoundary:
            case Opcode.DefaultEndOfWord:
            case Opcode.DefaultStartOfWord:
            case Opcode.EndOfLine:
            case Opcode.EndOfLineU:
            case Opcode.EndOfString:
            case Opcode.EndOfStringLine:
            case Opcode.EndOfStringLineU:
            case Opcode.EndOfWord:
            case Opcode.GraphemeBoundary:
            case Opcode.Property:
            case Opcode.PropertyIgn:
            case Opcode.PropertyIgnRev:
            case Opcode.PropertyRev:
            case Opcode.Range:
            case Opcode.RangeIgn:
            case Opcode.RangeIgnRev:
            case Opcode.RangeRev:
            case Opcode.SearchAnchor:
            case Opcode.SetDiff:
            case Opcode.SetDiffIgn:
            case Opcode.SetDiffIgnRev:
            case Opcode.SetDiffRev:
            case Opcode.SetInter:
            case Opcode.SetInterIgn:
            case Opcode.SetInterIgnRev:
            case Opcode.SetInterRev:
            case Opcode.SetSymDiff:
            case Opcode.SetSymDiffIgn:
            case Opcode.SetSymDiffIgnRev:
            case Opcode.SetSymDiffRev:
            case Opcode.SetUnion:
            case Opcode.SetUnionIgn:
            case Opcode.SetUnionIgnRev:
            case Opcode.SetUnionRev:
            case Opcode.StartOfLine:
            case Opcode.StartOfLineU:
            case Opcode.StartOfString:
            case Opcode.StartOfWord:
            case Opcode.String:
            case Opcode.StringFld:
            case Opcode.StringFldRev:
            case Opcode.StringIgn:
            case Opcode.StringIgnRev:
            case Opcode.StringRev:
                next.MatchNext = test.Next1.Node;
                next.MatchStep = test.Step;
                break;
            case Opcode.GreedyRepeatOne:
            case Opcode.LazyRepeatOne:
                if (test.Values[1] > 0)
                {
                    next.Test = test;
                }

                break;
        }
    }

    /// <summary>Upstream <c>set_test_nodes</c> (line 23805).</summary>
    /// <param name="pattern">The pattern.</param>
    private static void SetTestNodes(PatternObject pattern)
    {
        foreach (Node node in pattern.NodeList)
        {
            SetTestNode(node.Next1);
            if ((node.Status & NodeStatus.String) == 0)
            {
                SetTestNode(node.Next2);
            }
        }
    }
}
