using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Where a node goes next, and the shortcuts the matcher uses to get there. Port of
/// <c>RE_NextNode</c> (<c>upstream/src/_regex.c</c> lines 283-288).
/// </summary>
/// <remarks>
/// A class rather than a struct because upstream embeds it in <c>RE_Node</c> and mutates it through
/// a pointer (<c>set_test_node(&amp;node-&gt;next_1)</c>, <c>:23814</c>); one instance per slot,
/// allocated with the node, says the same thing without passing <c>ref</c> through the optimiser.
/// </remarks>
internal sealed class NextNode
{
    /// <summary>The node itself.</summary>
    internal Node? Node;

    /// <summary>
    /// The node the matcher may test first, looked up past the bookkeeping-only nodes.
    /// <see cref="Optimiser"/> fills this in.
    /// </summary>
    internal Node? Test;

    /// <summary>Where to continue from once <see cref="Test"/> has matched.</summary>
    internal Node? MatchNext;

    /// <summary>How far <see cref="Test"/> moves the text position.</summary>
    internal long MatchStep;
}

/// <summary>
/// One node of the compiled graph. Port of <c>RE_Node</c> (<c>upstream/src/_regex.c</c> lines
/// 290-310).
/// </summary>
/// <remarks>
/// <para>
/// Upstream's <c>next_2</c>/<c>true_node</c> pair and its <c>bad_character_offset</c>/
/// <c>good_suffix_offset</c> pair share one union, discriminated by
/// <see cref="NodeStatus.String"/>. They are separate fields here. That is not a restructuring: the
/// search offsets are allocated lazily at match time (<see cref="NodeStatus.FastInit"/>), so
/// throughout compilation the union reads as all-zero whichever arm the code picks - which is
/// exactly what <c>skip_one_way_branches</c> relies on when it reads <c>nonstring.next_2</c> of a
/// string node (<c>:23162</c>).
/// </para>
/// <para>
/// The search offsets themselves are not ported: nothing before Phase 7's prefilters writes them.
/// </para>
/// </remarks>
internal sealed class Node
{
    /// <summary>Upstream <c>next_1</c>: the node that follows this one.</summary>
    internal readonly NextNode Next1 = new();

    /// <summary>
    /// Upstream <c>nonstring.next_2</c>: the second exit of a branch, a repeat's tail, or a set's
    /// out-of-line members.
    /// </summary>
    internal readonly NextNode Next2 = new();

    /// <summary>Upstream <c>nonstring.true_node</c>: used by a <c>CONDITIONAL</c> node.</summary>
    internal Node? TrueNode;

    /// <summary>Upstream <c>step</c>: how far matching this node moves the text position.</summary>
    internal long Step;

    /// <summary>
    /// Upstream <c>values</c> and <c>value_count</c>. A list because <c>add_index</c>
    /// (<c>:23482</c>) appends to it after the node is built.
    /// </summary>
    internal readonly List<uint> Values;

    /// <summary>Upstream <c>status</c>: the <see cref="NodeStatus"/> bits.</summary>
    internal uint Status;

    /// <summary>Upstream <c>op</c>.</summary>
    internal Opcode Op;

    /// <summary>
    /// Upstream <c>match</c>: whether the node matches or rejects what it describes - a negated
    /// set, a negative lookaround.
    /// </summary>
    internal bool Match;

    /// <summary>Creates a node with <paramref name="valueCount"/> zeroed values.</summary>
    /// <param name="valueCount">How many values the opcode carries.</param>
    internal Node(int valueCount)
    {
        Values = [.. new uint[valueCount]];
    }
}

/// <summary>
/// The flags a code word carries into <c>create_node</c>: upstream's "node bitflags"
/// (<c>upstream/src/_regex.c</c> lines 124-129). They are shifted into
/// <see cref="Node.Status"/> by <see cref="NodeStatus.Shift"/>.
/// </summary>
internal static class NodeFlags
{
    /// <summary>Upstream <c>RE_POSITIVE_OP</c>.</summary>
    internal const uint Positive = 0x1;

    /// <summary>Upstream <c>RE_ZEROWIDTH_OP</c>.</summary>
    internal const uint ZeroWidth = 0x2;

    /// <summary>Upstream <c>RE_FUZZY_OP</c>.</summary>
    internal const uint Fuzzy = 0x4;

    /// <summary>Upstream <c>RE_REVERSE_OP</c>.</summary>
    internal const uint Reverse = 0x8;

    /// <summary>Upstream <c>RE_REQUIRED_OP</c>.</summary>
    internal const uint Required = 0x10;
}

/// <summary>
/// The bits of <see cref="Node.Status"/>, and of <c>RE_RepeatInfo.Status</c>. Port of the
/// <c>RE_STATUS_*</c> defines (<c>upstream/src/_regex.c</c> lines 131-174).
/// </summary>
/// <remarks>
/// The first six are ordered, not independent: <c>max_status_2</c> (<c>:746</c>) picks the larger
/// of two, so <c>Ref</c> beats <c>Limited</c> beats <c>Repeat</c> beats <c>Neither</c>. That is why
/// <c>add_repeat_guards</c> can compare them with <c>&gt;=</c>.
/// </remarks>
internal static class NodeStatus
{
    /// <summary>Upstream <c>RE_STATUS_BODY</c>: guard the start of a repeat's body.</summary>
    internal const uint Body = 0x1;

    /// <summary>Upstream <c>RE_STATUS_TAIL</c>: guard a repeat's tail.</summary>
    internal const uint Tail = 0x2;

    /// <summary>Upstream <c>RE_STATUS_NEITHER</c>.</summary>
    internal const uint Neither = 0x0;

    /// <summary>Upstream <c>RE_STATUS_REPEAT</c>.</summary>
    internal const uint Repeat = 0x4;

    /// <summary>Upstream <c>RE_STATUS_LIMITED</c>.</summary>
    internal const uint Limited = 0x8;

    /// <summary>Upstream <c>RE_STATUS_REF</c>.</summary>
    internal const uint Ref = 0x10;

    /// <summary>Upstream <c>RE_STATUS_VISITED_AG</c>: seen by <c>add_repeat_guards</c>.</summary>
    internal const uint VisitedAg = 0x20;

    /// <summary>
    /// Upstream <c>RE_STATUS_VISITED_REP</c>: seen by
    /// <c>record_subpattern_repeats_and_fuzzy_sections</c>.
    /// </summary>
    internal const uint VisitedRep = 0x40;

    /// <summary>
    /// Upstream <c>RE_STATUS_FAST_INIT</c>: the string node's search tables have been built. Set at
    /// match time; the compiler only ever reads it as clear.
    /// </summary>
    internal const uint FastInit = 0x80;

    /// <summary>Upstream <c>RE_STATUS_USED</c>: the node is reachable.</summary>
    internal const uint Used = 0x100;

    /// <summary>Upstream <c>RE_STATUS_STRING</c>: the node is a string node.</summary>
    internal const uint String = 0x200;

    /// <summary>Upstream <c>RE_STATUS_INNER</c>: a repeat inside another repeat.</summary>
    internal const uint Inner = 0x400;

    /// <summary>Upstream <c>RE_STATUS_SHIFT</c>: where <see cref="NodeFlags"/> land in the status.</summary>
    internal const int Shift = 11;

    /// <summary>Upstream <c>RE_STATUS_FUZZY</c>.</summary>
    internal const uint Fuzzy = NodeFlags.Fuzzy << Shift;

    /// <summary>Upstream <c>RE_STATUS_REVERSE</c>.</summary>
    internal const uint Reverse = NodeFlags.Reverse << Shift;

    /// <summary>Upstream <c>RE_STATUS_REQUIRED</c>.</summary>
    internal const uint Required = NodeFlags.Required << Shift;

    /// <summary>Upstream <c>RE_STATUS_HAS_GROUPS</c>.</summary>
    internal const uint HasGroups = 0x10000;

    /// <summary>Upstream <c>RE_STATUS_HAS_REPEATS</c>.</summary>
    internal const uint HasRepeats = 0x20000;

    /// <summary>Upstream <c>RE_ENCODING_SHIFT</c> (<c>upstream/src/_regex.c</c> line 164).</summary>
    /// <remarks>
    /// These are the same two bits as <see cref="HasGroups"/> and <see cref="HasRepeats"/>: the
    /// parser's encoding value (0, 1 or 2) is shifted by <c>ENCODING_OP_SHIFT</c> (5) into the code
    /// word's flags, and <c>create_node</c> shifts the whole flags word by <see cref="Shift"/> (11),
    /// which lands it on bits 16 and 17. Nothing sets both meanings on one node - the encoding is
    /// only ever carried by <c>PROPERTY</c> and <c>RANGE</c>-family nodes, and the two
    /// <c>HAS_</c> bits only by group, repeat and lookaround nodes.
    /// </remarks>
    internal const int EncodingShift = 16;

    /// <summary>Upstream <c>ASCII_ENCODING</c> (line 165).</summary>
    internal const uint AsciiEncoding = 1;

    /// <summary>Upstream <c>UNICODE_ENCODING</c> (line 166).</summary>
    internal const uint UnicodeEncoding = 2;

    /// <summary>Upstream <c>ENCODING_KIND</c> (line 167).</summary>
    /// <param name="node">The node.</param>
    /// <returns>0 for "whatever the pattern uses", otherwise <see cref="AsciiEncoding"/> or <see cref="UnicodeEncoding"/>.</returns>
    internal static uint EncodingKind(Node node) => (node.Status >> EncodingShift) & 0x3;

    /// <summary>Upstream <c>RE_STATUS_ALL_ATOMIC</c>.</summary>
    internal const uint AllAtomic = 0x40000;

    /// <summary>
    /// Upstream <c>max_status_2</c> (<c>upstream/src/_regex.c</c> line 746): the larger of two
    /// status values.
    /// </summary>
    /// <param name="x">One status.</param>
    /// <param name="y">The other.</param>
    /// <returns>Whichever is larger.</returns>
    internal static uint Max2(uint x, uint y) => x >= y ? x : y;

    /// <summary>Upstream <c>max_status_3</c> (line 751).</summary>
    /// <param name="x">One status.</param>
    /// <param name="y">Another.</param>
    /// <param name="z">Another.</param>
    /// <returns>The largest.</returns>
    internal static uint Max3(uint x, uint y, uint z) => Max2(x, Max2(y, z));

    /// <summary>Upstream <c>max_status_4</c> (line 757).</summary>
    /// <param name="w">One status.</param>
    /// <param name="x">Another.</param>
    /// <param name="y">Another.</param>
    /// <param name="z">Another.</param>
    /// <returns>The largest.</returns>
    internal static uint Max4(uint w, uint x, uint y, uint z) => Max2(Max2(w, x), Max2(y, z));
}

/// <summary>
/// Where each fuzzy constraint sits in a <c>FUZZY</c> node's values. Port of the
/// <c>RE_FUZZY_VAL_*</c> defines (<c>upstream/src/_regex.c</c> lines 176-200).
/// </summary>
/// <remarks>
/// Value 0 is the fuzzy section's index, so the constraints start at 1. The four error types are
/// ordered <c>SUB</c>, <c>INS</c>, <c>DEL</c>, <c>ERR</c> within each block of four, which is not
/// the order <c>build_FUZZY</c> reads them out of the code list in - see the assignments at
/// <c>:24220-24233</c>.
/// </remarks>
internal static class FuzzyValue
{
    /// <summary>Upstream <c>RE_FUZZY_SUB</c>.</summary>
    internal const int Sub = 0;

    /// <summary>Upstream <c>RE_FUZZY_INS</c>.</summary>
    internal const int Ins = 1;

    /// <summary>Upstream <c>RE_FUZZY_DEL</c>.</summary>
    internal const int Del = 2;

    /// <summary>Upstream <c>RE_FUZZY_ERR</c>.</summary>
    internal const int Err = 3;

    /// <summary>Upstream <c>RE_FUZZY_COUNT</c>: how many error types are counted separately.</summary>
    internal const int Count = 3;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MIN_BASE</c>.</summary>
    internal const int MinBase = 1;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MIN_SUB</c>.</summary>
    internal const int MinSub = MinBase + Sub;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MIN_INS</c>.</summary>
    internal const int MinIns = MinBase + Ins;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MIN_DEL</c>.</summary>
    internal const int MinDel = MinBase + Del;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MIN_ERR</c>.</summary>
    internal const int MinErr = MinBase + Err;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_BASE</c>.</summary>
    internal const int MaxBase = 5;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_SUB</c>.</summary>
    internal const int MaxSub = MaxBase + Sub;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_INS</c>.</summary>
    internal const int MaxIns = MaxBase + Ins;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_DEL</c>.</summary>
    internal const int MaxDel = MaxBase + Del;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_ERR</c>.</summary>
    internal const int MaxErr = MaxBase + Err;

    /// <summary>Upstream <c>RE_FUZZY_VAL_COST_BASE</c>.</summary>
    internal const int CostBase = 9;

    /// <summary>Upstream <c>RE_FUZZY_VAL_SUB_COST</c>.</summary>
    internal const int SubCost = CostBase + Sub;

    /// <summary>Upstream <c>RE_FUZZY_VAL_INS_COST</c>.</summary>
    internal const int InsCost = CostBase + Ins;

    /// <summary>Upstream <c>RE_FUZZY_VAL_DEL_COST</c>.</summary>
    internal const int DelCost = CostBase + Del;

    /// <summary>Upstream <c>RE_FUZZY_VAL_MAX_COST</c>.</summary>
    internal const int MaxCost = CostBase + Err;
}

/// <summary>
/// Questions asked of a node that the compiler, the optimiser and the matcher all ask. Upstream
/// declares them next to the matcher rather than the compiler
/// (<c>upstream/src/_regex.c</c> lines 3433 and 3476); they are here because the compiler is what
/// needs them first.
/// </summary>
internal static class NodeQueries
{
    /// <summary>
    /// Upstream <c>node_matches_one_character</c> (line 3433): does this node consume exactly one
    /// character?
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> if it matches one and only one character.</returns>
    internal static bool MatchesOneCharacter(Node node) =>
        node.Op switch
        {
            Opcode.Any
            or Opcode.AnyAll
            or Opcode.AnyAllRev
            or Opcode.AnyRev
            or Opcode.AnyU
            or Opcode.AnyURev
            or Opcode.Character
            or Opcode.CharacterIgn
            or Opcode.CharacterIgnRev
            or Opcode.CharacterRev
            or Opcode.Property
            or Opcode.PropertyIgn
            or Opcode.PropertyIgnRev
            or Opcode.PropertyRev
            or Opcode.Range
            or Opcode.RangeIgn
            or Opcode.RangeIgnRev
            or Opcode.RangeRev
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
            or Opcode.SetUnionRev => true,
            _ => false,
        };

    /// <summary>
    /// Upstream <c>locate_test_start</c> (line 3476): the node the matcher should test first when
    /// it starts a search.
    /// </summary>
    /// <param name="node">The pattern's start node.</param>
    /// <returns>The node to test.</returns>
    internal static Node LocateTestStart(Node node)
    {
        while (true)
        {
            switch (node.Op)
            {
                case Opcode.Boundary:
                {
                    Node next = node.Next1.Node!;
                    return next.Op switch
                    {
                        Opcode.String
                        or Opcode.StringFld
                        or Opcode.StringFldRev
                        or Opcode.StringIgn
                        or Opcode.StringIgnRev
                        or Opcode.StringRev => next,
                        _ => node,
                    };
                }
                case Opcode.CallRef:
                case Opcode.EndGroup:
                case Opcode.StartGroup:
                    node = node.Next1.Node!;
                    break;
                case Opcode.GreedyRepeat:
                case Opcode.LazyRepeat:
                    if (node.Values[1] == 0)
                    {
                        return node;
                    }

                    node = node.Next1.Node!;
                    break;
                case Opcode.GreedyRepeatOne:
                case Opcode.LazyRepeatOne:
                    return node.Values[1] == 0 ? node : node.Next2.Node!;
                case Opcode.Lookaround:
                    node = node.Next2.Node!;
                    break;
                default:
                    if (node.Step == 0 && MatchesOneCharacter(node))
                    {
                        switch (node.Next1.Node!.Op)
                        {
                            case Opcode.EndOfString:
                            case Opcode.StartOfString:
                                return node.Next1.Node;
                        }
                    }

                    return node;
            }
        }
    }
}
