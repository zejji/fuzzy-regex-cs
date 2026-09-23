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
/// The encoding upstream picks here (<c>:26013-26028</c>) is exposed through <see cref="Encoding"/>.
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

    /// <summary>
    /// The compile budget: the most nodes <see cref="NodeList"/> may hold. <b>This port's own
    /// field</b> (S56b), enforced in <c>NodeCompiler.CreateNode</c>; upstream has no limit.
    /// </summary>
    internal int MaxNodes = FuzzyRegex.DefaultMaxCompiledNodes;

    /// <summary>
    /// The pattern the caller wrote. <b>This port's own field</b> (S56b), carried only so the
    /// budget's <see cref="FuzzyRegexParseException"/> can report the pattern that overran, as
    /// every other parse failure does.
    /// </summary>
    internal string PatternText = "";

    /// <summary>
    /// Whether the compiler encountered an opcode that consults its encoding's casing functions.
    /// <b>This port's own field:</b> it identifies the unsupported part of <c>LOCALE</c> exactly.
    /// </summary>
    internal bool RequiresCaseEncoding;

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

    /// <summary>
    /// Whether any fuzzy section prices the three error kinds differently. <b>This port's own field:
    /// upstream has no equivalent, because nothing upstream ranks by cost.</b>
    /// </summary>
    /// <remarks>
    /// Read by <c>Matcher.DoBestFuzzyMatch</c>, which walks the slice a second time to honour the
    /// owner's cost ranking (DECISIONS 2026-09-12). Where every error costs the same, a section's
    /// cost is a fixed multiple of its error count, so the cost ranking and upstream's error-count
    /// ranking put matches in the same order and that extra walk cannot change the answer. Set in
    /// <c>NodeCompiler.BuildFuzzy</c>, where the three costs are read off the bytecode.
    /// </remarks>
    internal bool HasWeightedFuzzyCosts;

    /// <summary>
    /// The pattern's first <c>FUZZY</c> node, which when <see cref="FuzzyCount"/> is 1 is its only
    /// one. <b>This port's own field</b>, for the same consumer as <see cref="HasWeightedFuzzyCosts"/>.
    /// </summary>
    /// <remarks>
    /// <c>Matcher.DoBestFuzzyMatch</c>'s cost walk has to price a finished match's errors, and by
    /// then the section has been popped and <c>MatchState.FuzzyNode</c> is null - which is why
    /// <c>MatchState.TotalCost</c> exists at all. But that field is a snapshot taken at
    /// <c>END_FUZZY</c> and a match can succeed on a path whose last <c>END_FUZZY</c> belongs to a
    /// branch that was backtracked out of, so it can be STALE where <c>MatchState.FuzzyCounts</c> -
    /// the counts <c>FuzzyRegex</c> reports to the caller - is live. Holding the node lets the walk
    /// price the live counts instead.
    /// </remarks>
    internal Node? SingleFuzzyNode;

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
    /// parser found one and its case flags name an opcode. Read by
    /// <c>Matcher.LocateRequiredString</c>.
    /// </summary>
    internal Node? ReqString;

    /// <summary>
    /// <see cref="ReqString"/>'s values as UTF-16 text, so the prefilter can hand them to
    /// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>. <b>This port's
    /// own field</b> (S60): upstream searches with Boyer-Moore tables it builds lazily ON the node,
    /// which needs a lock; this is computed once at compile time and never written again, which is
    /// what S52b's immutability contract asks for.
    ///
    /// sync-divergence: upstream's needle lives on the node as tables <c>build_fast_tables</c>
    /// (<c>upstream/src/_regex.c:6298</c>) fills in on first use / ours is a string built in
    /// <see cref="Compile"/> beside the node it belongs to / the compiled pattern stays write-once.
    /// Re-aligning: a sync slice that sees upstream change what the required string CONTAINS
    /// changes the values fed to this field; a change to how upstream SEARCHES with it needs
    /// nothing here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> when there is no required string, when its opcode is not one the
    /// locator searches, or when it holds an <b>unpaired surrogate</b>. That last condition is what
    /// makes the vectorised search answer identically to the character-at-a-time one: with no
    /// unpaired surrogate in the needle, its UTF-16 encoding occurs exactly where its codepoint
    /// sequence does, and a match can never start inside a surrogate pair because the needle's
    /// first code unit is never a low surrogate. A pattern that does hold one takes
    /// <c>Matcher.SimpleStringSearch</c> instead, which compares whole codepoints.
    /// </para>
    /// </remarks>
    internal string? ReqStringText;

    /// <summary>
    /// Whether the compiled graph holds a <c>(*SKIP)</c>. <b>This port's own field</b> (S60):
    /// upstream has no equivalent because upstream does not need one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The required-string prefilter moves the FIRST attempt forward, to the position the required
    /// string implies. Skipping a position that cannot match is answer-transparent only while
    /// attempts are independent of each other, and <c>(*SKIP)</c> is the one thing in this engine
    /// that makes them dependent: it sets <see cref="MatchState.SliceStart"/> to where it was
    /// reached (<c>Matcher</c>'s <see cref="Opcode.Skip"/> arm, <c>:14544</c>), so the attempt that
    /// runs decides where the next one starts. Begin at a later position and the chain of skips is
    /// a different chain.
    /// </para>
    /// <para>
    /// Upstream jumps anyway, and answers wrongly for it. On <c>(?:..(*SKIP)x|q)x</c> over
    /// <c>"ab cd xx"</c> its <c>req_offset=3</c> puts the first attempt at 3; the verb steps 3 to 5,
    /// and position 4 - the one that matches - is never tried. PCRE2 10.47 answers <c>(4, 8)</c>
    /// with its start optimiser both on and off. ROADMAP's owner rule (2026-09-12) is that Phase 7
    /// ports upstream's prefilters without importing their answers, so this flag turns the jump off
    /// rather than reproducing the bug. Pinned by
    /// <c>Gaps/Engine/BacktrackingVerbTests.Skip_past_a_required_string_tries_a_start_position_upstreams_prefilter_skips</c>,
    /// which goes red the moment the jump is made unconditional.
    /// </para>
    /// <para>
    /// Only the jump is withheld. REFUSING the whole subject when the required string is absent
    /// stays on, because a string every match must contain is missing whatever order the attempts
    /// run in, and no <c>(*SKIP)</c> can conjure a match without it.
    /// </para>
    /// </remarks>
    internal bool HasSkipVerb;

    /// <summary>
    /// The start-position prefilter for a pattern that is one fuzzy ASCII literal, or
    /// <see langword="null"/>. <b>This port's own field</b> (S60b item 10); see
    /// <see cref="Engine.FuzzyLiteralFilter"/>.
    /// </summary>
    internal FuzzyLiteralFilter? FuzzyLiteralFilter;

    /// <summary>Upstream <c>is_fuzzy</c>.</summary>
    internal bool IsFuzzy;

    /// <summary>
    /// The zero-width position assertions the pattern must pass before it can match anything, or
    /// <see langword="null"/> where it has none. <b>This port's own field: upstream has no
    /// equivalent.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read by <c>Matcher.AnchorIsPinned</c>, which fixes upstream issues 563 and 564
    /// (<c>docs/DIVERGENCES.md</c>). Upstream forbids a fuzzy section from opening with an inserted
    /// character at the search anchor, on the reasoning in its own comment at
    /// <c>upstream/src/_regex.c</c>:10213 - "it's better just to start searching one character
    /// later". That reasoning holds only while starting one character later can still find the same
    /// match. An assertion that holds at the anchor and fails one character on takes that away, and
    /// upstream then loses a match it finds at every other position.
    /// </para>
    /// <para>
    /// Only the assertions the pattern passes <em>before anything else happens</em> are collected,
    /// so every one of them is passed on every path to every item in the pattern. An assertion
    /// reachable only down one branch - <c>(?:\bq|)</c>, <c>(?:\bq)*</c>, the body of a negative
    /// lookaround - is not here, which is what stops the rule firing on a path the engine abandons.
    /// <see cref="Optimiser"/> fills this in, for fuzzy patterns only.
    /// </para>
    /// </remarks>
    internal IReadOnlyList<Node>? AnchorGuards;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a full-case-folded string or group
    /// reference treats a subject folding it loaded but never used as half-matched, which is
    /// upstream's rule. The oracle sets it on a pattern it compiled for one call, to show that the
    /// S83 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// When a fuzzy deletion finishes a <c>STRING_FLD</c> or <c>REF_GROUP_FLD</c> item, the next
    /// subject character's folding may already be loaded with none of it compared. Upstream tests
    /// only <c>folded_pos &lt; folded_len</c> (<c>upstream/src/_regex.c</c>:14856, :14874, :14154
    /// and their reversed mirrors), so it charges an extra edit for that character or backtracks:
    /// <c>(?fi)(?:fi){d&lt;=1}</c> finds nothing in <c>fe</c>. This port charges only a folding that
    /// is part-used; see <c>Matcher.FoldingIsPartUsed</c> and <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool ChargeUntouchedFoldings;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a fuzzy full-case-folded group reference
    /// whose group runs out part way through a subject character's folding backtracks, which is
    /// upstream's rule. The oracle sets it on a pattern it compiled for one call, to show that the
    /// S84 leftovers fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Under full case folding ß folds to ss, so a group holding s matches only half of it. The
    /// literal <c>STRING_FLD</c> arm charges the other half as an edit (<c>upstream/src/_regex.c</c>:14855);
    /// <c>REF_GROUP_FLD</c> (:14154) and its reversed mirror (:14255) backtrack, so
    /// <c>(s)(?:\1){e&lt;=1}</c> finds nothing in <c>sß</c>. This port runs the literal arm's loop
    /// in both; see <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool SkipGroupFoldLeftovers;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a fuzzy deletion in the leftovers loop of
    /// a full-case-folded string deletes nothing, which is upstream's rule, rather than taking back
    /// the last comparison into the part-used folding. The oracle sets it on a pattern it compiled
    /// for one call, to show that the S85 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Under full case folding ß folds to ss, so <c>sss</c> over <c>ßß</c> stops half-way through
    /// the second ß. Upstream's deletion there (<c>upstream/src/_regex.c</c>:10590, reached from
    /// :14856) charges an edit and leaves the folding as it was, so with only deletions allowed
    /// <c>(?i)(?:sss){d&lt;=1}</c> finds nothing at the first ß, and with free deletions it never
    /// stops. For a group reference the flag restores S84's refusal instead, since upstream's
    /// <c>REF_GROUP_FLD</c> has no leftovers loop at all. See <c>Matcher.TakeBackFoldedComparison</c>
    /// and <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool SkipLeftoverTakeBack;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a retried fuzzy edit on a full-case-folded
    /// group reference re-enters the comparison without first stepping past a folding the edit
    /// finished, which is upstream's rule. The oracle sets it on a pattern it compiled for one call,
    /// to show that the S84 retry fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// After a first fuzzy try, <c>REF_GROUP_FLD</c>'s loop body moves to the next subject or group
    /// character when the edit used up that side's folding. A retry re-enters at the top of the arm
    /// and skips those steps, so it compares the finished character again:
    /// <c>(?i)(ab)(?:\1){e&lt;=1}</c> finds nothing in <c>abxab</c> under V1, where inserting the x
    /// matches. The literal <c>STRING_FLD</c> arm takes the step (<c>upstream/src/_regex.c</c>:14801);
    /// see <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool SkipRetriedFoldSteps;

    /// <summary>Upstream <c>do_search_start</c>.</summary>
    internal bool DoSearchStart;

    /// <summary>
    /// Upstream <c>pattern_call_ref</c>: the call reference for the whole pattern, or -1.
    /// </summary>
    internal long PatternCallRef;

    /// <summary>
    /// Upstream's <c>encoding</c> (<c>:26013-26028</c>).
    /// </summary>
    /// <remarks>
    /// Upstream's guess-from-the-pattern-class branch has nothing to port, since this port has no
    /// bytes patterns. <c>LOCALE</c> can be selected for non-casing operations; <see cref="Compile"/>
    /// rejects only a graph that contains an operation whose casing would need the C locale.
    /// </remarks>
    internal CaseEncoding Encoding => Encodings.Select(Flags);

    /// <summary>
    /// Compiles a code list to a node graph. Port of <c>re_compile</c>
    /// (<c>upstream/src/_regex.c</c> lines 25863-26121), minus the <c>PyArg_ParseTuple</c> half:
    /// the arguments arrive as a <see cref="CompiledPattern"/> instead.
    /// </summary>
    /// <param name="compiled">What the parser produced.</param>
    /// <param name="patternText">
    /// The pattern the caller wrote, carried only so the compile budget's refusal can report it.
    /// </param>
    /// <param name="maxNodes">
    /// The compile budget: the most nodes <see cref="NodeList"/> may hold. S56b; upstream has no
    /// equivalent and allocates until the process dies.
    /// </param>
    /// <returns>The compiled pattern.</returns>
    /// <exception cref="NotSupportedException">
    /// The code list does not describe a graph this compiler can build. Upstream raises
    /// <c>RuntimeError: invalid RE code</c>, not its own <c>error</c>, so this is not a
    /// <see cref="FuzzyRegexParseException"/> - see DECISIONS 2026-08-31.
    /// </exception>
    /// <exception cref="FuzzyRegexParseException">
    /// The graph would need more than <paramref name="maxNodes"/> nodes.
    /// </exception>
    internal static PatternObject Compile(
        CompiledPattern compiled,
        string patternText,
        int maxNodes = FuzzyRegex.DefaultMaxCompiledNodes
    )
    {
        ArgumentNullException.ThrowIfNull(compiled);

        // NOT PORTED: unpack_code_list / pack_code_list, which exist for pickling.
        uint[] code = [.. compiled.Code];

        // Get the required characters.
        IReadOnlyList<int> reqChars = GetRequiredChars(compiled.ReqChars);

        var self = new PatternObject
        {
            PatternText = patternText,
            MaxNodes = maxNodes,
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

            // NOT UPSTREAM'S (S60): the needle the vectorised prefilter searches with, built once
            // here rather than lazily on the node as 'build_fast_tables' does. Only for the one
            // opcode 'Matcher.LocateRequiredString' searches, and only when no value is an unpaired
            // surrogate - see the field's remarks for why that condition is what makes the
            // vectorised search and the character-at-a-time one answer the same.
            if (self.ReqString?.Op == Opcode.String && !reqChars.Any(static c => c is >= 0xD800 and <= 0xDFFF))
            {
                self.ReqStringText = string.Concat(reqChars.Select(static c => char.ConvertFromUtf32(c)));
            }
        }

        if (self.Encoding == CaseEncoding.Locale && self.RequiresCaseEncoding)
        {
            Encodings.EnsureCaseSupported(self.Encoding);
        }

        // NOT PORTED: scan_locale_chars, which reads the C locale.

        // Number the nodes, so the matcher can put a node reference on a byte stack where upstream
        // puts a pointer (Node.Index). Last, because the optimiser has by now removed the
        // unreachable nodes from the list and the required-string node has been added to it.
        for (int i = 0; i < self.NodeList.Count; i++)
        {
            self.NodeList[i].Index = i;

            // NOT UPSTREAM'S (S60): note a '(*SKIP)' while we are walking the list anyway, so the
            // prefilter knows not to move the first attempt. See HasSkipVerb.
            if (self.NodeList[i].Op == Opcode.Skip)
            {
                self.HasSkipVerb = true;
            }
        }

        // NOT UPSTREAM'S (S60b item 10): the prefilter for a pattern that is one fuzzy literal.
        self.FuzzyLiteralFilter = FuzzyLiteralFilter.TryCreate(self);

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
