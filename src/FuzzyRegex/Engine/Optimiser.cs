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

        // NOT UPSTREAM. Collect the assertions that pin a fuzzy match to the search anchor; see
        // FindAnchorGuards. Last because it reads the graph the passes above leave behind.
        FindAnchorGuards(pattern);
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
    /// past it, so it pins nothing.
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

                // Check the true branch for CONDITIONAL.
                next = node.TrueNode;
                if (next is not null && next.Op == Opcode.Branch && next.TrueNode is null)
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
