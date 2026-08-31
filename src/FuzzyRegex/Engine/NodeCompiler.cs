using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// The state one call to a builder threads through its children. Port of <c>RE_CompileArgs</c>
/// (<c>upstream/src/_regex.c</c> lines 643-661).
/// </summary>
/// <remarks>
/// A mutable struct on purpose. Upstream writes <c>subargs = *args;</c> to fork the state for a
/// subpattern and then copies chosen members back by hand; a C# struct assignment does exactly
/// that, where a class would silently share the fork with its parent.
/// </remarks>
internal struct CompileArgs
{
    /// <summary>The code list being read.</summary>
    internal uint[] CodeList;

    /// <summary>Upstream <c>code</c>, as an index into <see cref="CodeList"/>.</summary>
    internal int Code;

    /// <summary>Upstream <c>end_code</c>, as an index into <see cref="CodeList"/>.</summary>
    internal int EndCode;

    /// <summary>Upstream <c>pattern</c>. Shared, not forked: this is the pointer C copies.</summary>
    internal PatternObject Pattern;

    /// <summary>Upstream <c>min_width</c>.</summary>
    internal long MinWidth;

    /// <summary>Upstream <c>start</c>.</summary>
    internal Node? Start;

    /// <summary>Upstream <c>end</c>.</summary>
    internal Node? End;

    /// <summary>Upstream <c>repeat_depth</c>.</summary>
    internal int RepeatDepth;

    /// <summary>Upstream <c>visible_capture_count</c>.</summary>
    internal int VisibleCaptureCount;

    /// <summary>Upstream <c>forward</c>.</summary>
    internal bool Forward;

    /// <summary>Upstream <c>visible_captures</c>.</summary>
    internal bool VisibleCaptures;

    /// <summary>Upstream <c>has_captures</c>.</summary>
    internal bool HasCaptures;

    /// <summary>Upstream <c>is_fuzzy</c>.</summary>
    internal bool IsFuzzy;

    /// <summary>Upstream <c>within_fuzzy</c>.</summary>
    internal bool WithinFuzzy;

    /// <summary>Upstream <c>has_groups</c>.</summary>
    internal bool HasGroups;

    /// <summary>Upstream <c>has_repeats</c>.</summary>
    internal bool HasRepeats;

    /// <summary>Upstream <c>in_define</c>.</summary>
    internal bool InDefine;

    /// <summary>Upstream <c>all_atomic</c>.</summary>
    internal bool AllAtomic;

    /// <summary>The opcode at the read position.</summary>
    internal readonly Opcode Op => (Opcode)CodeList[Code];

    /// <summary>The code word <paramref name="offset"/> words past the read position.</summary>
    /// <param name="offset">How far past the read position to look.</param>
    /// <returns>The code word.</returns>
    internal readonly uint At(int offset) => CodeList[Code + offset];
}

/// <summary>
/// Turns a code list into a node graph: the builders and their bookkeeping, from
/// <c>create_node</c> (<c>upstream/src/_regex.c</c> line 23864) to <c>compile_to_nodes</c>
/// (<c>:25701</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every builder upstream has is here, including the ones for constructs no later phase can match
/// yet: the graph has to hold whatever the parser emits, and only <i>matching</i> an opcode is
/// later work.
/// </para>
/// <para>
/// <b>Two upstream shapes are ported as written rather than corrected.</b> First, several builders
/// check <c>code + n &gt; end_code</c> and then read <c>code[n]</c>, which is one word too far; with
/// a well-formed code list the last word is <c>SUCCESS</c>, which no builder over-reads, so the
/// path is unreachable. Second, <c>build_sequence</c>'s <c>STRING</c> case is written
/// <c>if (!build_STRING(...)) return FALSE;</c> (<c>:25679</c>) where every sibling compares
/// against <c>RE_ERROR_SUCCESS</c>; since <c>build_STRING</c> never returns zero the guard is dead,
/// and a truncated <c>STRING</c> would spin rather than be rejected. Neither is reachable from
/// <see cref="PatternCompiler"/>, which is the only caller, and both are the kind of difference a
/// future upstream sync has to be able to see.
/// </para>
/// </remarks>
internal static class NodeCompiler
{
    /// <summary>Upstream <c>RE_ERROR_SUCCESS</c>.</summary>
    private const int _success = 1;

    /// <summary>Upstream <c>RE_ERROR_FAILURE</c>.</summary>
    private const int _failure = 0;

    /// <summary>Upstream <c>RE_ERROR_ILLEGAL</c>.</summary>
    private const int _illegal = -1;

    /// <summary>
    /// Upstream <c>RE_MAX_FOLDED</c> (<c>upstream/src/_regex_unicode.h</c> line 20): the most
    /// characters one character can fold to.
    /// </summary>
    private const int _maxFolded = 3;

    // NOT PORTED: RE_ERROR_MEMORY. Upstream returns it wherever an allocation could fail; in .NET
    // `new` throws instead, so every `if (!node) return RE_ERROR_MEMORY` has nothing to guard.

    /// <summary>
    /// Compiles a code list to nodes and optimises the result. Port of <c>compile_to_nodes</c>
    /// (<c>upstream/src/_regex.c</c> lines 25701-25749).
    /// </summary>
    /// <param name="code">The flattened code list.</param>
    /// <param name="pattern">The pattern being built. Filled in as compilation discovers things.</param>
    /// <returns><see langword="false"/> if the code list is not a graph this can build.</returns>
    internal static bool CompileToNodes(uint[] code, PatternObject pattern)
    {
        var args = new CompileArgs
        {
            CodeList = code,
            Code = 0,
            EndCode = code.Length,
            Pattern = pattern,
            Forward = (pattern.Flags & RegexFlags.Reverse) == 0,
            VisibleCaptures = false,
            HasCaptures = false,
            RepeatDepth = 0,
            IsFuzzy = false,
            WithinFuzzy = false,
            VisibleCaptureCount = 0,
            InDefine = false,
        };

        // Upstream calls set_error(RE_ERROR_ILLEGAL) here and returns FALSE; a status of
        // RE_ERROR_FAILURE returns FALSE with no error set at all, which CPython turns into a
        // SystemError. Both are "this code list is not buildable", and PatternObject.Compile says
        // so the same way for each.
        if (BuildSequence(ref args) != _success)
        {
            return false;
        }

        pattern.MinWidth = args.MinWidth;
        pattern.IsFuzzy = args.IsFuzzy;
        pattern.DoSearchStart = true;
        pattern.StartNode = args.Start;
        pattern.VisibleCaptureCount = args.VisibleCaptureCount;

        // Optimise the pattern.
        Optimiser.OptimisePattern(pattern);

        pattern.StartTest = NodeQueries.LocateTestStart(pattern.StartNode!);

        // Get the call_ref for the entire pattern, if any.
        pattern.PatternCallRef = pattern.StartNode!.Op == Opcode.CallRef ? (long)pattern.StartNode.Values[0] : -1L;

        return true;
    }

    /// <summary>
    /// Upstream <c>make_STRING_node</c> (<c>upstream/src/_regex.c</c> lines 25802-25821): a string
    /// node that is not part of the graph.
    /// </summary>
    /// <param name="pattern">The pattern to record the node against.</param>
    /// <param name="op">The string opcode.</param>
    /// <param name="chars">The characters.</param>
    /// <returns>The node.</returns>
    internal static Node MakeStringNode(PatternObject pattern, Opcode op, IReadOnlyList<int> chars)
    {
        long step = GetStep(op);

        Node node = CreateNode(pattern, op, 0, step * chars.Count, chars.Count);
        node.Status |= NodeStatus.String;

        for (int i = 0; i < chars.Count; i++)
        {
            node.Values[i] = (uint)chars[i];
        }

        return node;
    }

    /// <summary>
    /// Upstream <c>create_node</c> (<c>upstream/src/_regex.c</c> lines 23864-23915).
    /// </summary>
    /// <param name="pattern">The pattern to record the node against.</param>
    /// <param name="op">The opcode.</param>
    /// <param name="flags">The <see cref="NodeFlags"/> the code word carried.</param>
    /// <param name="step">How far the node moves the text position.</param>
    /// <param name="valueCount">How many values the node carries.</param>
    /// <returns>The new node.</returns>
    private static Node CreateNode(PatternObject pattern, Opcode op, uint flags, long step, int valueCount)
    {
        var node = new Node(valueCount)
        {
            Op = op,
            Match = (flags & NodeFlags.Positive) != 0,
            Status = flags << NodeStatus.Shift,
            Step = step,
        };

        // Record the new node.
        pattern.NodeList.Add(node);

        return node;
    }

    /// <summary>
    /// Upstream <c>add_node</c> (<c>upstream/src/_regex.c</c> lines 23918-23923): the second call
    /// for a node fills its second exit.
    /// </summary>
    /// <param name="node1">The node to attach to.</param>
    /// <param name="node2">The node to attach.</param>
    private static void AddNode(Node node1, Node node2)
    {
        if (node1.Next1.Node is null)
        {
            node1.Next1.Node = node2;
        }
        else
        {
            node1.Next2.Node = node2;
        }
    }

    /// <summary>Upstream <c>ensure_group</c> (line 23926).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="group">The group number.</param>
    private static void EnsureGroup(PatternObject pattern, int group)
    {
        if (group <= pattern.TrueGroupCount)
        {
            // We already have an entry for the group.
            return;
        }

        _ = pattern.GroupInfoAt(group);
        pattern.TrueGroupCount = group;
    }

    /// <summary>Upstream <c>record_ref_group</c> (line 23963).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="group">The group number.</param>
    private static void RecordRefGroup(PatternObject pattern, int group)
    {
        EnsureGroup(pattern, group);
        pattern.GroupInfoAt(group).Referenced = true;
    }

    /// <summary>Upstream <c>record_group</c> (line 23973).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="group">The group number.</param>
    /// <param name="node">The group's start node.</param>
    private static void RecordGroup(PatternObject pattern, int group, Node node)
    {
        EnsureGroup(pattern, group);

        if (group >= 1)
        {
            GroupInfo info = pattern.GroupInfoAt(group);
            info.EndIndex = pattern.TrueGroupCount;
            info.Node = node;
        }
    }

    /// <summary>Upstream <c>record_group_end</c> (line 23990).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="group">The group number.</param>
    private static void RecordGroupEnd(PatternObject pattern, int group)
    {
        if (group >= 1)
        {
            pattern.GroupInfoAt(group).EndIndex = ++pattern.GroupEndIndex;
        }
    }

    /// <summary>Upstream <c>record_call_ref_defined</c> (line 24033).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="callRef">The call reference.</param>
    /// <param name="node">The <c>CALL_REF</c> node.</param>
    private static void RecordCallRefDefined(PatternObject pattern, int callRef, Node node)
    {
        CallRefInfo info = pattern.CallRefInfoAt(callRef);
        info.Defined = true;
        info.Node = node;
    }

    /// <summary>Upstream <c>record_call_ref_used</c> (line 24045).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="callRef">The call reference.</param>
    private static void RecordCallRefUsed(PatternObject pattern, int callRef) =>
        pattern.CallRefInfoAt(callRef).Used = true;

    /// <summary>Upstream <c>sequence_matches_one</c> (line 24056).</summary>
    /// <param name="node">The first node of the sequence.</param>
    /// <returns><see langword="true"/> if the sequence matches one and only one character.</returns>
    private static bool SequenceMatchesOne(Node node)
    {
        while (node.Op == Opcode.Branch && node.Next2.Node is null)
        {
            node = node.Next1.Node!;
        }

        return node.Next1.Node is null
            && (node.Status & NodeStatus.Fuzzy) == 0
            && NodeQueries.MatchesOneCharacter(node);
    }

    /// <summary>Upstream <c>record_repeat</c> (line 24067).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="index">The repeat's index.</param>
    /// <param name="repeatDepth">How deeply the repeat is nested.</param>
    private static void RecordRepeat(PatternObject pattern, int index, int repeatDepth)
    {
        RepeatInfo info = pattern.RepeatInfoAt(index);

        if (index >= pattern.RepeatCount)
        {
            pattern.RepeatCount = index + 1;
        }

        if (repeatDepth > 0)
        {
            info.Status |= NodeStatus.Inner;
        }
    }

    /// <summary>Upstream <c>get_step</c> (line 24104): which way an opcode moves, and by how much.</summary>
    /// <param name="op">The opcode.</param>
    /// <returns>1 forwards, -1 backwards, 0 for a zero-width opcode.</returns>
    private static long GetStep(Opcode op) =>
        op switch
        {
            Opcode.Any
            or Opcode.AnyAll
            or Opcode.AnyU
            or Opcode.Character
            or Opcode.CharacterIgn
            or Opcode.Property
            or Opcode.PropertyIgn
            or Opcode.Range
            or Opcode.RangeIgn
            or Opcode.SetDiff
            or Opcode.SetDiffIgn
            or Opcode.SetInter
            or Opcode.SetInterIgn
            or Opcode.SetSymDiff
            or Opcode.SetSymDiffIgn
            or Opcode.SetUnion
            or Opcode.SetUnionIgn
            or Opcode.String
            or Opcode.StringFld
            or Opcode.StringIgn => 1,
            Opcode.AnyAllRev
            or Opcode.AnyRev
            or Opcode.AnyURev
            or Opcode.CharacterIgnRev
            or Opcode.CharacterRev
            or Opcode.PropertyIgnRev
            or Opcode.PropertyRev
            or Opcode.RangeIgnRev
            or Opcode.RangeRev
            or Opcode.SetDiffIgnRev
            or Opcode.SetDiffRev
            or Opcode.SetInterIgnRev
            or Opcode.SetInterRev
            or Opcode.SetSymDiffIgnRev
            or Opcode.SetSymDiffRev
            or Opcode.SetUnionIgnRev
            or Opcode.SetUnionRev
            or Opcode.StringFldRev
            or Opcode.StringIgnRev
            or Opcode.StringRev => -1,
            _ => 0,
        };

    /// <summary>
    /// Upstream <c>possible_unfolded_length</c> (<c>upstream/src/_regex.c</c> line 6908): full case
    /// folding lets one character in the text stand for up to three in the pattern.
    /// </summary>
    /// <param name="length">The folded length.</param>
    /// <returns>The shortest unfolded length that could produce it.</returns>
    private static long PossibleUnfoldedLength(long length)
    {
        if (length == 0)
        {
            return 0;
        }

        return length < _maxFolded ? 1 : length / _maxFolded;
    }

    /// <summary>Upstream <c>build_ANY</c> (line 24157).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildAny(ref CompileArgs args)
    {
        // codes: opcode, flags.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        Opcode op = args.Op;
        uint flags = args.At(1);

        long step = GetStep(op);

        // Create the node.
        Node node = CreateNode(args.Pattern, op, flags, step, 0);

        node.Match = true;
        args.Code += 2;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        ++args.MinWidth;

        return _success;
    }

    /// <summary>Upstream <c>build_FUZZY</c> (line 24190).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildFuzzy(ref CompileArgs args)
    {
        // codes: FUZZY, flags, constraints, ..., end
        // or:    FUZZY_EXT, flags, constraints, ... next ..., end.
        bool isExt = args.Op == Opcode.FuzzyExt;
        if (args.Code + (isExt ? 15 : 13) > args.EndCode)
        {
            return _illegal;
        }

        uint flags = args.At(1);

        // Create nodes for the start and end of the fuzzy sequence.
        Node startNode = CreateNode(args.Pattern, Opcode.Fuzzy, flags, 0, 13);
        Node endNode = CreateNode(args.Pattern, Opcode.EndFuzzy, flags, 0, 0);

        var index = (uint)args.Pattern.FuzzyCount++;
        startNode.Values[0] = index;

        // The constraints consist of 4 pairs of limits and the cost equation.
        startNode.Values[FuzzyValue.MinDel] = args.At(2);
        startNode.Values[FuzzyValue.MinIns] = args.At(4);
        startNode.Values[FuzzyValue.MinSub] = args.At(6);
        startNode.Values[FuzzyValue.MinErr] = args.At(8);

        startNode.Values[FuzzyValue.MaxDel] = args.At(3);
        startNode.Values[FuzzyValue.MaxIns] = args.At(5);
        startNode.Values[FuzzyValue.MaxSub] = args.At(7);
        startNode.Values[FuzzyValue.MaxErr] = args.At(9);

        startNode.Values[FuzzyValue.DelCost] = args.At(10);
        startNode.Values[FuzzyValue.InsCost] = args.At(11);
        startNode.Values[FuzzyValue.SubCost] = args.At(12);
        startNode.Values[FuzzyValue.MaxCost] = args.At(13);

        args.Code += 14;

        CompileArgs subargs;
        Node? testNode;
        int status;

        if (isExt)
        {
            subargs = args;
            status = BuildCharsetEquiv(ref subargs);
            if (status != _success)
            {
                return status;
            }

            args.Code = subargs.Code;
            if (args.Code >= args.EndCode || args.Op != Opcode.Next)
            {
                return _illegal;
            }

            ++args.Code;
            testNode = subargs.Start;
        }
        else
        {
            testNode = null;
        }

        subargs = args;
        subargs.WithinFuzzy = true;

        // Compile the sequence and check that we've reached the end of the subpattern.
        status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.MinWidth += subargs.MinWidth;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy = true;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        ++args.Code;

        // Append the fuzzy sequence.
        AddNode(args.End!, startNode);
        AddNode(startNode, subargs.Start!);
        if (testNode is not null)
        {
            AddNode(startNode, testNode);

            // The END_FUZZY node needs access to the charset, if any, in case of fuzzy insertion
            // at the end.
            endNode.Next2.Node = startNode;
        }

        AddNode(subargs.End!, endNode);
        args.End = endNode;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_ATOMIC</c> (line 24291).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildAtomic(ref CompileArgs args)
    {
        // codes: opcode, ..., end.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        Node atomicNode = CreateNode(args.Pattern, Opcode.Atomic, 0, 0, 0);

        ++args.Code;

        // Compile the sequence and check that we've reached the end of it.
        CompileArgs subargs = args;

        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.MinWidth += subargs.MinWidth;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        ++args.Code;

        // Check the subpattern.
        if (subargs.HasGroups)
        {
            atomicNode.Status |= NodeStatus.HasGroups;
        }

        if (subargs.HasRepeats)
        {
            atomicNode.Status |= NodeStatus.HasRepeats;
        }

        // Create the node to terminate the subpattern.
        Node endNode = CreateNode(subargs.Pattern, Opcode.EndAtomic, 0, 0, 0);

        // Append the new sequence.
        AddNode(args.End!, atomicNode);
        AddNode(atomicNode, subargs.Start!);
        AddNode(subargs.End!, endNode);
        args.End = endNode;

        return _success;
    }

    /// <summary>Upstream <c>build_BOUNDARY</c> (line 24349).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildBoundary(ref CompileArgs args)
    {
        // codes: opcode, flags.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        Opcode op = args.Op;
        uint flags = args.At(1);

        args.Code += 2;

        // Create the node.
        Node node = CreateNode(args.Pattern, op, flags, 0, 0);

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        return _success;
    }

    /// <summary>Upstream <c>build_BRANCH</c> (line 24376).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildBranch(ref CompileArgs args)
    {
        // codes: opcode, ..., next, ..., end.
        if (args.Code + 2 > args.EndCode)
        {
            return _illegal;
        }

        // Create nodes for the start and end of the branch sequence.
        Node branchNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);
        Node joinNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);

        // Append the node.
        AddNode(args.End!, branchNode);
        args.End = joinNode;

        long minWidth = long.MaxValue;

        CompileArgs subargs = args;

        // A branch in the regular expression is compiled into a series of 2-way branches.
        do
        {
            // Skip over the 'BRANCH' or 'NEXT' opcode.
            ++subargs.Code;

            // Compile the sequence until the next 'BRANCH' or 'NEXT' opcode.
            int status = BuildSequence(ref subargs);
            if (status != _success)
            {
                return status;
            }

            minWidth = Math.Min(minWidth, subargs.MinWidth);

            args.HasCaptures |= subargs.HasCaptures;
            args.IsFuzzy |= subargs.IsFuzzy;
            args.HasGroups |= subargs.HasGroups;
            args.HasRepeats |= subargs.HasRepeats;

            // Append the sequence.
            AddNode(branchNode, subargs.Start!);
            AddNode(subargs.End!, joinNode);

            // Create a start node for the next sequence and append it.
            Node nextBranchNode = CreateNode(subargs.Pattern, Opcode.Branch, 0, 0, 0);

            AddNode(branchNode, nextBranchNode);
            branchNode = nextBranchNode;
        } while (subargs.Code < subargs.EndCode && subargs.Op == Opcode.Next);

        // We should have reached the end of the branch.
        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        ++args.Code;
        args.MinWidth += minWidth;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_CALL_REF</c> (line 24450).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildCallRef(ref CompileArgs args)
    {
        // codes: opcode, call_ref.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        uint callRef = args.At(1);

        args.Code += 2;

        // Create nodes for the start and end of the subpattern.
        Node startNode = CreateNode(args.Pattern, Opcode.CallRef, 0, 0, 1);
        Node endNode = CreateNode(args.Pattern, Opcode.GroupReturn, 0, 0, 0);

        startNode.Values[0] = callRef;

        // Compile the sequence and check that we've reached the end of the subpattern.
        CompileArgs subargs = args;
        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.MinWidth += subargs.MinWidth;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        ++args.Code;

        // Record that we defined a call_ref.
        RecordCallRefDefined(args.Pattern, (int)callRef, startNode);

        // Append the node.
        AddNode(args.End!, startNode);
        AddNode(startNode, subargs.Start!);
        AddNode(subargs.End!, endNode);
        args.End = endNode;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_CHARACTER_or_PROPERTY</c> (line 24509).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildCharacterOrProperty(ref CompileArgs args)
    {
        // codes: opcode, flags, value.
        if (args.Code + 2 > args.EndCode)
        {
            return _illegal;
        }

        Opcode op = args.Op;
        uint flags = args.At(1);

        long step = GetStep(op);

        if ((flags & NodeFlags.ZeroWidth) != 0)
        {
            step = 0;
        }

        // Create the node.
        Node node = CreateNode(args.Pattern, op, flags, step, 1);

        node.Values[0] = args.At(2);

        args.Code += 3;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        if (step != 0)
        {
            ++args.MinWidth;
        }

        return _success;
    }

    /// <summary>Upstream <c>build_CONDITIONAL</c> (line 24547).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildConditional(ref CompileArgs args)
    {
        // codes: opcode, flags, forward, ..., next, ..., next, ..., end.
        if (args.Code + 4 > args.EndCode)
        {
            return _illegal;
        }

        uint flags = args.At(1);
        bool forward = args.At(2) != 0;

        // Create a node for the lookaround.
        Node testNode = CreateNode(args.Pattern, Opcode.Conditional, flags, 0, 0);

        args.Code += 3;

        AddNode(args.End!, testNode);

        // Compile the lookaround test and check that we've reached the end of the subpattern.
        CompileArgs subargs = args;
        subargs.Forward = forward;
        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.Next)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        ++args.Code;

        // Check the lookaround subpattern.
        if (subargs.HasGroups)
        {
            testNode.Status |= NodeStatus.HasGroups;
        }

        if (subargs.HasRepeats)
        {
            testNode.Status |= NodeStatus.HasRepeats;
        }

        // Create the node to terminate the test.
        Node endTestNode = CreateNode(args.Pattern, Opcode.EndConditional, 0, 0, 0);

        // test node -> test -> end test node
        AddNode(testNode, subargs.Start!);
        AddNode(subargs.End!, endTestNode);

        // Compile the true branch.
        subargs = args;
        status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        // Check the true branch.
        args.Code = subargs.Code;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        long minWidth = subargs.MinWidth;

        // Create the terminating node.
        Node endNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);

        // end test node -> true branch -> end node
        AddNode(endTestNode, subargs.Start!);
        AddNode(subargs.End!, endNode);
        testNode.TrueNode = subargs.Start;

        if (args.Op == Opcode.Next)
        {
            // There's a false branch.
            ++args.Code;

            // Compile the false branch.
            subargs.Code = args.Code;
            status = BuildSequence(ref subargs);
            if (status != _success)
            {
                return status;
            }

            // Check the false branch.
            args.Code = subargs.Code;
            args.HasCaptures |= subargs.HasCaptures;
            args.IsFuzzy |= subargs.IsFuzzy;
            args.HasGroups |= subargs.HasGroups;
            args.HasRepeats |= subargs.HasRepeats;
            args.VisibleCaptureCount = subargs.VisibleCaptureCount;

            minWidth = Math.Min(minWidth, subargs.MinWidth);

            // test node -> false branch -> end node
            AddNode(testNode, subargs.Start!);
            AddNode(subargs.End!, endNode);
        }
        else
        {
            minWidth = 0;

            // test node -> end node
            AddNode(testNode, endNode);
        }

        if (args.Op != Opcode.End)
        {
            return _illegal;
        }

        args.MinWidth += minWidth;

        ++args.Code;

        args.End = endNode;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_GROUP</c> (line 24680).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildGroup(ref CompileArgs args)
    {
        // codes: opcode, forward, private_group, public_group, ..., end.
        if (args.Code + 3 > args.EndCode)
        {
            return _illegal;
        }

        bool forward = args.At(1) != 0;
        uint privateGroup = args.At(2);
        uint publicGroup = args.At(3);

        args.Code += 4;

        // Create nodes for the start and end of the capture group.
        Node startNode = CreateNode(args.Pattern, forward ? Opcode.StartGroup : Opcode.EndGroup, 0, 0, 3);
        Node endNode = CreateNode(args.Pattern, forward ? Opcode.EndGroup : Opcode.StartGroup, 0, 0, 3);

        startNode.Values[0] = privateGroup;
        endNode.Values[0] = privateGroup;
        startNode.Values[1] = publicGroup;
        endNode.Values[1] = publicGroup;

        // Signal that the capture should be saved when it's complete.
        startNode.Values[2] = 0;
        endNode.Values[2] = 1;

        // Record that we have a new capture group.
        RecordGroup(args.Pattern, (int)privateGroup, startNode);

        // Compile the sequence and check that we've reached the end of the capture group.
        CompileArgs subargs = args;
        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        args.MinWidth += subargs.MinWidth;
        // Upstream ORs the two bitwise (:24733); neither side has an effect, so `||` says the same
        // thing and keeps S2178 quiet.
        args.HasCaptures |= subargs.HasCaptures || subargs.VisibleCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups = true;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        if (!args.InDefine)
        {
            ++args.VisibleCaptureCount;
        }

        ++args.Code;

        // Record that the capture group has closed.
        RecordGroupEnd(args.Pattern, (int)privateGroup);

        // Append the capture group.
        AddNode(args.End!, startNode);
        AddNode(startNode, subargs.Start!);
        AddNode(subargs.End!, endNode);
        args.End = endNode;

        return _success;
    }

    /// <summary>Upstream <c>build_GROUP_CALL</c> (line 24757).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildGroupCall(ref CompileArgs args)
    {
        // codes: opcode, call_ref.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        uint callRef = args.At(1);

        // Create the node.
        Node node = CreateNode(args.Pattern, Opcode.GroupCall, 0, 0, 1);

        node.Values[0] = callRef;

        node.Status |= NodeStatus.HasGroups;
        node.Status |= NodeStatus.HasRepeats;

        args.Code += 2;

        // Record that we used a call_ref.
        RecordCallRefUsed(args.Pattern, (int)callRef);

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_GROUP_EXISTS</c> (line 24792).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildGroupExists(ref CompileArgs args)
    {
        // codes: opcode, ..., next, ..., end.
        if (args.Code + 2 > args.EndCode)
        {
            return _illegal;
        }

        uint group = args.At(1);

        args.Code += 2;

        // Record that we have a reference to a group. If group is 0, then we have a DEFINE and not
        // a true group.
        if (group > 0)
        {
            RecordRefGroup(args.Pattern, (int)group);
        }

        // Create nodes for the start and end of the structure.
        Node startNode = CreateNode(args.Pattern, Opcode.GroupExists, 0, 0, 1);
        Node endNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);

        startNode.Values[0] = group;

        CompileArgs subargs = args;
        subargs.InDefine = true;
        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        args.Code = subargs.Code;
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        long minWidth = subargs.MinWidth;

        // Append the start node.
        AddNode(args.End!, startNode);
        AddNode(startNode, subargs.Start!);

        if (args.Op == Opcode.Next)
        {
            ++args.Code;

            Node trueBranchEnd = subargs.End!;

            subargs.Code = args.Code;

            status = BuildSequence(ref subargs);
            if (status != _success)
            {
                return status;
            }

            args.Code = subargs.Code;
            args.HasCaptures |= subargs.HasCaptures;
            args.IsFuzzy |= subargs.IsFuzzy;
            args.VisibleCaptureCount = subargs.VisibleCaptureCount;

            if (group == 0)
            {
                // Join the 2 branches end-to-end and bypass it. The sequence itself will never be
                // matched as a whole, so it doesn't matter.
                minWidth = 0;

                AddNode(startNode, endNode);
                AddNode(trueBranchEnd, subargs.Start!);
            }
            else
            {
                args.HasGroups |= subargs.HasGroups;
                args.HasRepeats |= subargs.HasRepeats;
                args.VisibleCaptureCount = subargs.VisibleCaptureCount;

                minWidth = Math.Min(minWidth, subargs.MinWidth);

                AddNode(startNode, subargs.Start!);
                AddNode(trueBranchEnd, endNode);
            }

            AddNode(subargs.End!, endNode);
        }
        else
        {
            AddNode(startNode, endNode);
            AddNode(subargs.End!, endNode);

            minWidth = 0;
        }

        args.MinWidth += minWidth;

        if (args.Op != Opcode.End)
        {
            return _illegal;
        }

        ++args.Code;

        args.End = endNode;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_LOOKAROUND</c> (line 24900).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildLookaround(ref CompileArgs args)
    {
        // codes: opcode, flags, forward, ..., end.
        if (args.Code + 3 > args.EndCode)
        {
            return _illegal;
        }

        uint flags = args.At(1);
        bool forward = args.At(2) != 0;

        // Create a node for the lookaround.
        Node lookaroundNode = CreateNode(args.Pattern, Opcode.Lookaround, flags, 0, 0);

        args.Code += 3;

        // Compile the sequence and check that we've reached the end of the subpattern.
        CompileArgs subargs = args;
        subargs.Forward = forward;
        int status = BuildSequence(ref subargs);
        if (status != _success)
        {
            return status;
        }

        if (subargs.Op != Opcode.End)
        {
            return _illegal;
        }

        args.Code = subargs.Code;
        ++args.Code;

        // Check the subpattern.
        args.HasCaptures |= subargs.HasCaptures;
        args.IsFuzzy |= subargs.IsFuzzy;
        args.HasGroups |= subargs.HasGroups;
        args.HasRepeats |= subargs.HasRepeats;
        args.VisibleCaptureCount = subargs.VisibleCaptureCount;

        if (subargs.HasGroups)
        {
            lookaroundNode.Status |= NodeStatus.HasGroups;
        }

        if (subargs.HasRepeats)
        {
            lookaroundNode.Status |= NodeStatus.HasRepeats;
        }

        // Create the node to terminate the subpattern.
        Node endNode = CreateNode(args.Pattern, Opcode.EndLookaround, 0, 0, 0);

        // Make a continuation node.
        Node nextNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);

        // Append the new sequence.
        AddNode(args.End!, lookaroundNode);
        AddNode(lookaroundNode, subargs.Start!);
        AddNode(lookaroundNode, nextNode);
        AddNode(subargs.End!, endNode);
        AddNode(endNode, nextNode);

        args.End = nextNode;
        args.AllAtomic = false;

        return _success;
    }

    /// <summary>Upstream <c>build_RANGE</c> (line 24976).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildRange(ref CompileArgs args)
    {
        // codes: opcode, flags, lower, upper.
        if (args.Code + 3 > args.EndCode)
        {
            return _illegal;
        }

        Opcode op = args.Op;
        uint flags = args.At(1);

        long step = GetStep(op);

        if ((flags & NodeFlags.ZeroWidth) != 0)
        {
            step = 0;
        }

        // Create the node.
        Node node = CreateNode(args.Pattern, op, flags, step, 2);

        node.Values[0] = args.At(2);
        node.Values[1] = args.At(3);

        args.Code += 4;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        if (step != 0)
        {
            ++args.MinWidth;
        }

        return _success;
    }

    /// <summary>Upstream <c>build_REF_GROUP</c> (line 25015).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildRefGroup(ref CompileArgs args)
    {
        // codes: opcode, flags, group.
        if (args.Code + 2 > args.EndCode)
        {
            return _illegal;
        }

        uint flags = args.At(1);
        uint group = args.At(2);
        Node node = CreateNode(args.Pattern, args.Op, flags, 0, 1);

        node.Values[0] = group;

        args.Code += 3;

        // Record that we have a reference to a group.
        RecordRefGroup(args.Pattern, (int)group);

        // Append the reference.
        AddNode(args.End!, node);
        args.End = node;

        return _success;
    }

    /// <summary>Upstream <c>build_REPEAT</c> (line 25046).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildRepeat(ref CompileArgs args)
    {
        // codes: opcode, min_count, max_count, ..., end.
        if (args.Code + 3 > args.EndCode)
        {
            return _illegal;
        }

        bool greedy = args.Op == Opcode.GreedyRepeat;
        uint minCount = args.At(1);
        uint maxCount = args.At(2);
        if (args.At(1) > args.At(2))
        {
            return _illegal;
        }

        args.Code += 3;

        int status;

        if (minCount == 1 && maxCount == 1)
        {
            // Singly-repeated sequence.
            CompileArgs subargs = args;
            status = BuildSequence(ref subargs);
            if (status != _success)
            {
                return status;
            }

            if (subargs.Op != Opcode.End)
            {
                return _illegal;
            }

            args.Code = subargs.Code;
            args.MinWidth += subargs.MinWidth;
            args.HasCaptures |= subargs.HasCaptures;
            args.IsFuzzy |= subargs.IsFuzzy;
            args.HasGroups |= subargs.HasGroups;
            args.HasRepeats |= subargs.HasRepeats;
            args.VisibleCaptureCount = subargs.VisibleCaptureCount;

            ++args.Code;

            // Append the sequence.
            AddNode(args.End!, subargs.Start!);
            args.End = subargs.End;
        }
        else
        {
            // Extract the minimum number of repeats out of a repeat if it contains a repeat.
            var subargs = default(CompileArgs);

            if (minCount > 0)
            {
                uint doneCount;

                for (doneCount = 0; doneCount < minCount; doneCount++)
                {
                    subargs = args;
                    subargs.VisibleCaptures = true;

                    status = BuildSequence(ref subargs);
                    if (status != _success)
                    {
                        return status;
                    }

                    if (subargs.Op != Opcode.End)
                    {
                        return _illegal;
                    }

                    args.VisibleCaptureCount = subargs.VisibleCaptureCount;

                    AddNode(args.End!, subargs.Start!);
                    args.End = subargs.End;
                }

                args.MinWidth += minCount * subargs.MinWidth;
                args.HasCaptures |= subargs.HasCaptures;
                args.IsFuzzy |= subargs.IsFuzzy;
                args.HasGroups |= subargs.HasGroups;
                args.HasRepeats |= subargs.HasRepeats;

                minCount -= doneCount;
                if (~maxCount != 0)
                {
                    maxCount -= doneCount;
                }
            }

            // We've extracted the minimum number of repeats. Are there any left?
            //
            // The `min_count > 0` here is dead: the loop above left min_count at zero whenever it
            // ran, and this branch is only reachable when it did. Upstream :25129, ported as
            // written - a repeat with max_count now zero still builds a repeat node below, which
            // is what upstream does and what the corpus rows depend on.
            if (minCount > 0 && maxCount == 0)
            {
                // All done.
                args.Code = subargs.Code;
                ++args.Code;
            }
            else
            {
                // More to do.
                int index = args.Pattern.RepeatCount;

                // Create the nodes for the repeat.
                Node repeatNode = CreateNode(
                    args.Pattern,
                    greedy ? Opcode.GreedyRepeat : Opcode.LazyRepeat,
                    0,
                    args.Forward ? 1 : -1,
                    4
                );
                RecordRepeat(args.Pattern, index, args.RepeatDepth);

                repeatNode.Values[0] = (uint)index;
                repeatNode.Values[1] = minCount;
                repeatNode.Values[2] = maxCount;
                repeatNode.Values[3] = args.Forward ? 1u : 0u;

                if (args.WithinFuzzy)
                {
                    args.Pattern.RepeatInfoAt(index).Status |= NodeStatus.Body;
                }

                // Compile the 'body' and check that we've reached the end of it.
                subargs = args;
                subargs.VisibleCaptures = true;
                ++subargs.RepeatDepth;
                status = BuildSequence(ref subargs);
                if (status != _success)
                {
                    return status;
                }

                if (subargs.Op != Opcode.End)
                {
                    return _illegal;
                }

                args.Code = subargs.Code;
                args.MinWidth += minCount * subargs.MinWidth;
                args.HasCaptures |= subargs.HasCaptures;
                args.IsFuzzy |= subargs.IsFuzzy;
                args.HasGroups |= subargs.HasGroups;
                args.HasRepeats = true;
                args.VisibleCaptureCount = subargs.VisibleCaptureCount;

                ++args.Code;

                // Is it a repeat of something which will match a single character?
                //
                // If it's in a fuzzy section then it won't be optimised as a single-character
                // repeat.
                if (SequenceMatchesOne(subargs.Start!))
                {
                    repeatNode.Op = greedy ? Opcode.GreedyRepeatOne : Opcode.LazyRepeatOne;

                    if (args.AllAtomic && args.Code < args.EndCode && args.Op == Opcode.End && !args.WithinFuzzy)
                    {
                        repeatNode.Status |= NodeStatus.AllAtomic;
                    }

                    // Append the new sequence.
                    AddNode(args.End!, repeatNode);
                    repeatNode.Next2.Node = subargs.Start;
                    args.End = repeatNode;
                }
                else
                {
                    Node endRepeatNode = CreateNode(
                        args.Pattern,
                        greedy ? Opcode.EndGreedyRepeat : Opcode.EndLazyRepeat,
                        0,
                        args.Forward ? 1 : -1,
                        4
                    );

                    endRepeatNode.Values[0] = repeatNode.Values[0];
                    endRepeatNode.Values[1] = repeatNode.Values[1];
                    endRepeatNode.Values[2] = repeatNode.Values[2];
                    endRepeatNode.Values[3] = args.Forward ? 1u : 0u;

                    Node endNode = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);

                    if (args.AllAtomic && args.Code < args.EndCode && args.Op == Opcode.End && !args.WithinFuzzy)
                    {
                        endRepeatNode.Status |= NodeStatus.AllAtomic;
                    }

                    // Append the new sequence.
                    AddNode(args.End!, repeatNode);
                    AddNode(repeatNode, subargs.Start!);
                    AddNode(repeatNode, endNode);
                    AddNode(subargs.End!, endRepeatNode);
                    AddNode(endRepeatNode, subargs.Start!);
                    AddNode(endRepeatNode, endNode);
                    args.End = endNode;
                }
            }
        }

        if (!(args.AllAtomic && args.Code < args.EndCode && args.Op != Opcode.End && !args.WithinFuzzy))
        {
            args.AllAtomic = false;
        }

        return _success;
    }

    /// <summary>Upstream <c>build_STRING</c> (line 25235).</summary>
    /// <param name="args">The compile state.</param>
    /// <param name="isCharset">Whether the string is a set's members rather than a literal.</param>
    /// <returns>A status code.</returns>
    private static int BuildString(ref CompileArgs args, bool isCharset)
    {
        // codes: opcode, flags, length, ....
        uint flags = args.At(1);
        uint length = args.At(2);
        if (args.Code + 3 + length > args.EndCode)
        {
            return _illegal;
        }

        Opcode op = args.Op;

        long step = GetStep(op);

        // Create the node.
        Node node = CreateNode(args.Pattern, op, flags, step * length, (int)length);
        if (!isCharset)
        {
            node.Status |= NodeStatus.String;
        }

        for (int i = 0; i < length; i++)
        {
            node.Values[i] = args.At(3 + i);
        }

        args.Code += (int)(3 + length);

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        // Because of full case-folding, one character in the text could match multiple characters
        // in the pattern.
        args.MinWidth += op is Opcode.StringFld or Opcode.StringFldRev ? PossibleUnfoldedLength(length) : length;

        return _success;
    }

    /// <summary>Upstream <c>build_SET</c> (line 25282).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildSet(ref CompileArgs args)
    {
        // codes: opcode, flags, ..., end.
        Opcode op = args.Op;
        uint flags = args.At(1);

        long step = GetStep(op);

        if ((flags & NodeFlags.ZeroWidth) != 0)
        {
            step = 0;
        }

        Node node = CreateNode(args.Pattern, op, flags, step, 0);

        args.Code += 2;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        long minWidth = args.MinWidth;

        // Compile the character set.
        do
        {
            int status;
            switch (args.Op)
            {
                case Opcode.AnyAll:
                    status = BuildAny(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Character:
                case Opcode.Property:
                    status = BuildCharacterOrProperty(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Range:
                    status = BuildRange(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.SetDiff:
                case Opcode.SetInter:
                case Opcode.SetSymDiff:
                case Opcode.SetUnion:
                    status = BuildSet(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.String:
                    // A set of characters.
                    status = BuildString(ref args, isCharset: true);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                default:
                    // Illegal opcode for a character set.
                    return _illegal;
            }
        } while (args.Code < args.EndCode && args.Op != Opcode.End);

        // Check that we've reached the end correctly. (The last opcode should be 'END'.)
        if (args.Code >= args.EndCode || args.Op != Opcode.End)
        {
            return _illegal;
        }

        ++args.Code;

        // At this point the set's members are in the main sequence. They need to be moved
        // out-of-line.
        node.Next2.Node = node.Next1.Node;
        node.Next1.Node = null;
        args.End = node;

        args.MinWidth = minWidth;

        if (step != 0)
        {
            ++args.MinWidth;
        }

        return _success;
    }

    /// <summary>Upstream <c>build_SUCCESS</c> (line 25374).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildSuccess(ref CompileArgs args)
    {
        // codes: opcode.

        // Create the node.
        Node node = CreateNode(args.Pattern, args.Op, 0, 0, 0);

        ++args.Code;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        return _success;
    }

    /// <summary>Upstream <c>build_zerowidth</c> (line 25393).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildZeroWidth(ref CompileArgs args)
    {
        // codes: opcode, flags.
        if (args.Code + 1 > args.EndCode)
        {
            return _illegal;
        }

        uint flags = args.At(1);

        // Create the node.
        Node node = CreateNode(args.Pattern, args.Op, flags, 0, 0);

        args.Code += 2;

        // Append the node.
        AddNode(args.End!, node);
        args.End = node;

        return _success;
    }

    /// <summary>Upstream <c>build_charset_equiv</c> (line 25418).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildCharsetEquiv(ref CompileArgs args)
    {
        // Guarantee that there's something to attach to.
        args.Start = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);
        args.End = args.Start;

        // The following code groups opcodes by format, not function.
        int status;
        switch (args.Op)
        {
            case Opcode.Any:
            case Opcode.AnyAll:
            case Opcode.AnyU:
            case Opcode.AnyRev:
            case Opcode.AnyAllRev:
            case Opcode.AnyURev:
                // A simple opcode with no trailing codewords and width of 1.
                status = BuildAny(ref args);
                if (status != _success)
                {
                    return status;
                }

                break;
            case Opcode.Character:
            case Opcode.Property:
            case Opcode.CharacterIgn:
            case Opcode.PropertyIgn:
            case Opcode.CharacterRev:
            case Opcode.PropertyRev:
            case Opcode.CharacterIgnRev:
            case Opcode.PropertyIgnRev:
                // A character literal or a property.
                status = BuildCharacterOrProperty(ref args);
                if (status != _success)
                {
                    return status;
                }

                break;
            case Opcode.Range:
            case Opcode.RangeIgn:
            case Opcode.RangeIgnRev:
            case Opcode.RangeRev:
                // A range.
                status = BuildRange(ref args);
                if (status != _success)
                {
                    return status;
                }

                break;
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
                // A set.
                status = BuildSet(ref args);
                if (status != _success)
                {
                    return status;
                }

                break;
            default:
                // We've found an opcode which we don't recognise.
                return _illegal;
        }

        return _success;
    }

    /// <summary>Upstream <c>build_sequence</c> (line 25490).</summary>
    /// <param name="args">The compile state.</param>
    /// <returns>A status code.</returns>
    private static int BuildSequence(ref CompileArgs args)
    {
        // Guarantee that there's something to attach to.
        args.Start = CreateNode(args.Pattern, Opcode.Branch, 0, 0, 0);
        args.End = args.Start;

        args.MinWidth = 0;
        args.HasCaptures = false;
        args.IsFuzzy = false;
        args.HasGroups = false;
        args.HasRepeats = false;
        args.AllAtomic = true;

        // The sequence should end with an opcode we don't understand. If it doesn't then the code
        // is illegal.
        while (args.Code < args.EndCode)
        {
            int status;

            // The following code groups opcodes by format, not function.
            switch (args.Op)
            {
                case Opcode.Any:
                case Opcode.AnyAll:
                case Opcode.AnyAllRev:
                case Opcode.AnyRev:
                case Opcode.AnyU:
                case Opcode.AnyURev:
                    // A simple opcode with no trailing codewords and width of 1.
                    status = BuildAny(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Atomic:
                    // An atomic sequence.
                    status = BuildAtomic(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Boundary:
                case Opcode.DefaultBoundary:
                case Opcode.DefaultEndOfWord:
                case Opcode.DefaultStartOfWord:
                case Opcode.EndOfWord:
                case Opcode.GraphemeBoundary:
                case Opcode.Keep:
                case Opcode.Skip:
                case Opcode.StartOfWord:
                    // A word or grapheme boundary.
                    status = BuildBoundary(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Branch:
                    // A 2-way branch.
                    status = BuildBranch(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.CallRef:
                    // A group call ref.
                    status = BuildCallRef(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Character:
                case Opcode.CharacterIgn:
                case Opcode.CharacterIgnRev:
                case Opcode.CharacterRev:
                case Opcode.Property:
                case Opcode.PropertyIgn:
                case Opcode.PropertyIgnRev:
                case Opcode.PropertyRev:
                    // A character literal or a property.
                    status = BuildCharacterOrProperty(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Conditional:
                    // A lookaround conditional.
                    status = BuildConditional(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.EndOfLine:
                case Opcode.EndOfLineU:
                case Opcode.EndOfString:
                case Opcode.EndOfStringLine:
                case Opcode.EndOfStringLineU:
                case Opcode.SearchAnchor:
                case Opcode.StartOfLine:
                case Opcode.StartOfLineU:
                case Opcode.StartOfString:
                    // A simple opcode with no trailing codewords and width of 0.
                    status = BuildZeroWidth(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Failure:
                case Opcode.Prune:
                case Opcode.Success:
                    status = BuildSuccess(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Fuzzy:
                case Opcode.FuzzyExt:
                    // A fuzzy sequence.
                    status = BuildFuzzy(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.GreedyRepeat:
                case Opcode.LazyRepeat:
                    // A repeated sequence.
                    status = BuildRepeat(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Group:
                    // A capture group.
                    status = BuildGroup(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.GroupCall:
                    // A group call.
                    status = BuildGroupCall(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.GroupExists:
                    // A conditional sequence.
                    status = BuildGroupExists(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Lookaround:
                    // A lookaround.
                    status = BuildLookaround(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.Range:
                case Opcode.RangeIgn:
                case Opcode.RangeIgnRev:
                case Opcode.RangeRev:
                    // A range.
                    status = BuildRange(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.RefGroup:
                case Opcode.RefGroupFld:
                case Opcode.RefGroupFldRev:
                case Opcode.RefGroupIgn:
                case Opcode.RefGroupIgnRev:
                case Opcode.RefGroupRev:
                    // A reference to a group.
                    status = BuildRefGroup(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
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
                    // A set.
                    status = BuildSet(ref args);
                    if (status != _success)
                    {
                        return status;
                    }

                    break;
                case Opcode.String:
                case Opcode.StringFld:
                case Opcode.StringFldRev:
                case Opcode.StringIgn:
                case Opcode.StringIgnRev:
                case Opcode.StringRev:
                    // A string literal.
                    //
                    // Upstream writes `if (!build_STRING(args, FALSE)) return FALSE;` (:25679).
                    // build_STRING returns SUCCESS or ILLEGAL and never zero, so this guard never
                    // fires - unlike every sibling case above, which compares against SUCCESS. See
                    // the class remarks.
                    if (BuildString(ref args, isCharset: false) == _failure)
                    {
                        return _failure;
                    }

                    break;
                default:
                    // We've found an opcode which we don't recognise. We'll leave it for the
                    // caller.
                    return _success;
            }
        }

        // If we're here then we should be at the end of the code, otherwise we have an error.
        return args.Code == args.EndCode ? _success : _failure;
    }
}
