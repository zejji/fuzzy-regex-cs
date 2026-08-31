using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// What the compiler learns about one capture group. Port of <c>RE_GroupInfo</c>
/// (<c>upstream/src/_regex.c</c> lines 352-357).
/// </summary>
internal sealed class GroupInfo
{
    /// <summary>Upstream <c>end_index</c>: the order in which the group closed.</summary>
    internal long EndIndex;

    /// <summary>Upstream <c>node</c>: the group's start node.</summary>
    internal Node? Node;

    /// <summary>Upstream <c>referenced</c>: something refers back to this group.</summary>
    internal bool Referenced;

    /// <summary>Upstream <c>has_name</c>: the group is named.</summary>
    internal bool HasName;
}

/// <summary>
/// What the compiler learns about one group call. Port of <c>RE_CallRefInfo</c>
/// (<c>upstream/src/_regex.c</c> lines 360-364).
/// </summary>
internal sealed class CallRefInfo
{
    /// <summary>Upstream <c>node</c>: the <c>CALL_REF</c> node the call enters.</summary>
    internal Node? Node;

    /// <summary>Upstream <c>defined</c>.</summary>
    internal bool Defined;

    /// <summary>Upstream <c>used</c>.</summary>
    internal bool Used;
}

/// <summary>
/// What the compiler learns about one repeat. Port of <c>RE_RepeatInfo</c>
/// (<c>upstream/src/_regex.c</c> lines 367-369).
/// </summary>
internal sealed class RepeatInfo
{
    /// <summary>
    /// Upstream <c>status</c>: which of <see cref="NodeStatus.Body"/> and
    /// <see cref="NodeStatus.Tail"/> need position guards, plus <see cref="NodeStatus.Inner"/>.
    /// </summary>
    internal uint Status;
}

/// <summary>
/// A pattern compiled all the way to a node graph. Port of <c>PatternObject</c>
/// (<c>upstream/src/_regex.c</c> lines 539-592), built by <see cref="Compile"/>, which is the port
/// of <c>re_compile</c> (<c>:25863-26121</c>).
/// </summary>
/// <remarks>
/// <para>
/// Only the members the compiler fills in are here. Upstream's match-time caches
/// (<c>groups_storage</c>, <c>repeats_storage</c>, <c>stack_storage</c>) are pools for
/// <c>PyMem_Alloc</c> and have no counterpart on a garbage-collected heap;
/// <c>packed_code_list</c> is for pickling and <c>weakreflist</c> is CPython plumbing.
/// <c>docs/PORTMAP.md</c> records each one.
/// </para>
/// <para>
/// The encoding upstream picks here (<c>:26013-26028</c>) is picked on demand instead - see
/// <see cref="Encoding"/>.
/// </para>
/// </remarks>
internal sealed class PatternObject
{
    /// <summary>Upstream <c>flags</c>: the resolved flags the pattern compiled under.</summary>
    internal int Flags;

    /// <summary>Upstream <c>start_node</c>.</summary>
    internal Node? StartNode;

    /// <summary>Upstream <c>start_test</c>: where a search starts testing.</summary>
    internal Node? StartTest;

    /// <summary>Upstream <c>true_group_count</c>: including the private groups a rebuild needs.</summary>
    internal int TrueGroupCount;

    /// <summary>Upstream <c>public_group_count</c>: the groups the pattern text declares.</summary>
    internal int PublicGroupCount;

    /// <summary>
    /// Upstream <c>visible_capture_count</c>: the groups not hidden inside <c>(?(DEFINE)...)</c>.
    /// </summary>
    internal int VisibleCaptureCount;

    /// <summary>Upstream <c>repeat_count</c>.</summary>
    internal int RepeatCount;

    /// <summary>Upstream <c>group_end_index</c>: how many groups have closed.</summary>
    internal long GroupEndIndex;

    /// <summary>Upstream <c>groupindex</c>: capture group name to group number.</summary>
    internal IReadOnlyDictionary<string, int> GroupIndex = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Upstream <c>named_lists</c>.</summary>
    internal IReadOnlyDictionary<string, IReadOnlySet<string>> NamedLists = new Dictionary<
        string,
        IReadOnlySet<string>
    >(StringComparer.Ordinal);

    /// <summary>Upstream <c>named_list_indexes</c>: the lists in the order the bytecode indexes them.</summary>
    internal IReadOnlyList<IReadOnlySet<string>> NamedListIndexes = [];

    /// <summary>Upstream <c>node_list</c> and <c>node_count</c>.</summary>
    internal readonly List<Node> NodeList = [];

    /// <summary>Upstream <c>group_info</c>, indexed by group number minus one.</summary>
    internal readonly List<GroupInfo> GroupInfoList = [];

    /// <summary>Upstream <c>call_ref_info</c> and <c>call_ref_info_count</c>.</summary>
    internal readonly List<CallRefInfo> CallRefInfoList = [];

    /// <summary>Upstream <c>repeat_info</c>.</summary>
    internal readonly List<RepeatInfo> RepeatInfoList = [];

    /// <summary>
    /// Upstream <c>min_width</c>: the shortest string the pattern can match, ignoring fuzziness.
    /// </summary>
    internal long MinWidth;

    /// <summary>Upstream <c>fuzzy_count</c>: how many fuzzy sections the pattern has.</summary>
    internal int FuzzyCount;

    /// <summary>Upstream <c>req_offset</c>.</summary>
    internal long ReqOffset;

    /// <summary>
    /// Upstream <c>req_flags</c>. Kept exactly as the parser reported it: <see cref="Compile"/>
    /// masks off <c>FULLCASE</c> in a local when it chooses the opcode for
    /// <see cref="ReqString"/> and upstream does not write that masked value back
    /// (<c>:25997</c> stores it, <c>:26052</c> masks the local).
    /// </summary>
    internal int ReqFlags;

    /// <summary>Upstream <c>required_chars</c>.</summary>
    internal IReadOnlyList<int> RequiredChars = [];

    /// <summary>
    /// Upstream <c>req_string</c>: a free-standing string node for the required substring, if the
    /// parser found one and its case flags name an opcode. Nothing consults it until Phase 7 ports
    /// the prefilters.
    /// </summary>
    internal Node? ReqString;

    /// <summary>Upstream <c>is_fuzzy</c>.</summary>
    internal bool IsFuzzy;

    /// <summary>Upstream <c>do_search_start</c>.</summary>
    internal bool DoSearchStart;

    /// <summary>
    /// Upstream <c>pattern_call_ref</c>: the call reference for the whole pattern, or -1.
    /// </summary>
    internal long PatternCallRef;

    /// <summary>
    /// Upstream's <c>encoding</c> (<c>:26013-26028</c>), chosen on first use rather than during
    /// compilation.
    /// </summary>
    /// <remarks>
    /// Deferring it is deliberate. <see cref="Encodings.Select"/> throws for <c>LOCALE</c>, whose
    /// casing depends on the C locale and is not ported; doing that here would refuse to build a
    /// node graph for a <c>(?L)</c> pattern that upstream builds one for happily. Upstream's own
    /// guess-from-the-pattern-class branch has nothing to port, since this port has no bytes
    /// patterns.
    /// </remarks>
    internal CaseEncoding Encoding => Encodings.Select(Flags);

    /// <summary>
    /// Compiles a code list to a node graph. Port of <c>re_compile</c>
    /// (<c>upstream/src/_regex.c</c> lines 25863-26121), minus the <c>PyArg_ParseTuple</c> half:
    /// the arguments arrive as a <see cref="CompiledPattern"/> instead.
    /// </summary>
    /// <param name="compiled">What the parser produced.</param>
    /// <returns>The compiled pattern.</returns>
    /// <exception cref="NotSupportedException">
    /// The code list does not describe a graph this compiler can build. Upstream raises
    /// <c>RuntimeError: invalid RE code</c>, not its own <c>error</c>, so this is not a
    /// <see cref="FuzzyRegexParseException"/> - see DECISIONS 2026-08-31.
    /// </exception>
    internal static PatternObject Compile(CompiledPattern compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);

        // NOT PORTED: unpack_code_list / pack_code_list, which exist for pickling.
        uint[] code = [.. compiled.Code];

        // Get the required characters.
        IReadOnlyList<int> reqChars = GetRequiredChars(compiled.ReqChars);

        var self = new PatternObject
        {
            Flags = compiled.Flags,
            PublicGroupCount = compiled.GroupCount,
            GroupIndex = compiled.GroupIndex,
            NamedLists = compiled.NamedLists,
            NamedListIndexes = compiled.NamedListIndexes,
            ReqOffset = compiled.ReqOffset,
            RequiredChars = reqChars,
            ReqFlags = compiled.ReqFlags,
        };

        // Compile the regular expression code to nodes.
        if (!NodeCompiler.CompileToNodes(code, self))
        {
            throw new NotSupportedException("invalid RE code");
        }

        // Make a node for the required string, if there's one.
        if (reqChars.Count > 0)
        {
            // Remove the FULLCASE flag if it's not a Unicode pattern or not ignoring case. The
            // masked value stays local, exactly as upstream leaves it.
            int reqFlags = compiled.ReqFlags;
            if ((self.Flags & RegexFlags.Unicode) == 0 || (self.Flags & RegexFlags.IgnoreCase) == 0)
            {
                reqFlags &= ~RegexFlags.FullCase;
            }

            if ((self.Flags & RegexFlags.Reverse) != 0)
            {
                self.ReqString = reqFlags switch
                {
                    0 => NodeCompiler.MakeStringNode(self, Opcode.StringRev, reqChars),
                    RegexFlags.IgnoreCase | RegexFlags.FullCase => NodeCompiler.MakeStringNode(
                        self,
                        Opcode.StringFldRev,
                        reqChars
                    ),
                    RegexFlags.IgnoreCase => NodeCompiler.MakeStringNode(self, Opcode.StringIgnRev, reqChars),
                    _ => null,
                };
            }
            else
            {
                self.ReqString = reqFlags switch
                {
                    0 => NodeCompiler.MakeStringNode(self, Opcode.String, reqChars),
                    RegexFlags.IgnoreCase | RegexFlags.FullCase => NodeCompiler.MakeStringNode(
                        self,
                        Opcode.StringFld,
                        reqChars
                    ),
                    RegexFlags.IgnoreCase => NodeCompiler.MakeStringNode(self, Opcode.StringIgn, reqChars),
                    _ => null,
                };
            }
        }

        // NOT PORTED: scan_locale_chars, which reads the C locale.

        return self;
    }

    /// <summary>
    /// Returns the <see cref="GroupInfo"/> for a group number, growing the list to reach it.
    /// </summary>
    /// <remarks>
    /// Upstream reallocates <c>group_info</c> in blocks of sixteen and then indexes it with
    /// whatever number it is given, so an entry past the last <c>ensure_group</c> call reads as the
    /// zeroed remainder of the block. Growing on demand says the same thing without the block size,
    /// and without depending on the block size being large enough - which
    /// <c>mark_named_groups</c>, looping to <c>public_group_count</c>, quietly does.
    /// </remarks>
    /// <param name="group">The group number, one-based.</param>
    /// <returns>The entry for that group.</returns>
    internal GroupInfo GroupInfoAt(int group)
    {
        while (GroupInfoList.Count < group)
        {
            GroupInfoList.Add(new GroupInfo());
        }

        return GroupInfoList[group - 1];
    }

    /// <summary>
    /// Returns the <see cref="CallRefInfo"/> for a call reference, growing the list to reach it.
    /// Upstream <c>ensure_call_ref</c> (<c>upstream/src/_regex.c</c> line 23996), whose
    /// <c>call_ref_info_count</c> is this list's <c>Count</c>.
    /// </summary>
    /// <param name="callRef">The call reference, zero-based.</param>
    /// <returns>The entry for that call reference.</returns>
    internal CallRefInfo CallRefInfoAt(int callRef)
    {
        while (CallRefInfoList.Count <= callRef)
        {
            CallRefInfoList.Add(new CallRefInfo());
        }

        return CallRefInfoList[callRef];
    }

    /// <summary>
    /// Returns the <see cref="RepeatInfo"/> for a repeat, growing the list to reach it. Upstream
    /// grows <c>repeat_info</c> inside <c>record_repeat</c> (line 24067) and inside
    /// <c>add_repeat_guards</c> reads it without growing, because by then every repeat has been
    /// recorded.
    /// </summary>
    /// <param name="index">The repeat's index.</param>
    /// <returns>The entry for that repeat.</returns>
    internal RepeatInfo RepeatInfoAt(int index)
    {
        while (RepeatInfoList.Count <= index)
        {
            RepeatInfoList.Add(new RepeatInfo());
        }

        return RepeatInfoList[index];
    }

    /// <summary>
    /// Upstream <c>get_required_chars</c> (<c>upstream/src/_regex.c</c> lines 25756-25799).
    /// </summary>
    /// <remarks>
    /// Upstream's job is to turn a Python tuple into an array of code words, and it treats any
    /// failure - a non-integer, a value too wide for an <c>RE_CODE</c> - as "there are no required
    /// characters". Ours arrive already as codepoints, so only the empty case is left.
    /// </remarks>
    /// <param name="reqChars">The required characters the parser found.</param>
    /// <returns>The same characters, or an empty list when there are none.</returns>
    private static IReadOnlyList<int> GetRequiredChars(IReadOnlyList<int> reqChars) =>
        reqChars.Count < 1 ? [] : reqChars;
}
