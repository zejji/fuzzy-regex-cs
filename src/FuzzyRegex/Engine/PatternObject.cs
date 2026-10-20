using System.Buffers;
using System.Diagnostics;
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

    /// <summary>
    /// NOT UPSTREAM'S: a backreference reads this group's text, as opposed to only a conditional
    /// reading whether it is set. The call guard keys such a group on its text and any other
    /// <see cref="Referenced"/> group on one bit (<c>CallRead</c>).
    /// </summary>
    internal bool TextRead;

    /// <summary>
    /// NOT UPSTREAM'S: the group is captured inside a repeat body, where a repeat's empty-iteration
    /// test reads whether the capture changed its span, so the call guard keys it on its span
    /// (<c>CallRead.Span</c>).
    /// </summary>
    internal bool CapturedInRepeat;

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

    /// <summary>
    /// Whether this repeat's body guard is a failure memo. <b>Not upstream.</b> When set,
    /// <c>END_GREEDY_REPEAT</c> and <c>END_LAZY_REPEAT</c> do not mark the position where the body
    /// matched, so the failure recorded when the engine later backtracks past that position is
    /// kept, and the body is never tried there again in the same attempt (in the same search, where
    /// <see cref="PatternObject.KeepsFailureMemosAcrossAttempts"/> holds). That turns
    /// <c>(?:a|a)+c</c> from exponential to linear.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's success mark exists because a failure is not always a fact about the position
    /// alone: <c>(?:(a)|a)+(?(1)c|b)</c> over <c>aab</c> fails from position 1 on the path that set
    /// group 1 and matches from it on the path that did not. The memo is sound only when nothing
    /// after the body can see how the engine got there. The compiler sets this flag for a repeat
    /// with no maximum and a minimum of at most 1, that is not inside another repeat, an atomic
    /// group, a possessive repeat, a lookaround, a conditional's lookaround test, a called group or
    /// a fuzzy section. <c>Optimiser.KeepFailureMemosSound</c> then clears it for every repeat if
    /// the pattern has anything that reads more than the position: a backreference, a group-exists
    /// conditional, a group call, <c>(*PRUNE)</c> or <c>(*SKIP)</c>, fuzzy matching or POSIX
    /// matching; or anything a failing path leaves behind, which is a <c>\K</c> inside one of those
    /// constructs (<see cref="PatternObject.KeepInSubmatch"/>). A partial call's second pass uses
    /// it as well, since D11 build chunk 9 (<c>MatchState.KeepsFailureMemo</c> gives the argument).
    /// Where it does apply, a conditional's restore leaves the repeat's guards in place rather than
    /// putting back the saved ones (D35, <c>Matcher.PushRepeats</c>).
    /// </para>
    /// <para>
    /// Four conditions have a witness in <c>FailureMemoTests</c>, an answer that changes when the
    /// condition is deleted: the maximum, backreferences, group-exists conditionals and a
    /// <c>\K</c> inside a construct. The rest
    /// have none. Differential grids of 7.6 million rows found no answer that changes when any one
    /// of them is deleted (2026-09-26), and for most there is a reason. A pattern with a verb that
    /// ends the attempt has no memo at all, so nothing it leaves behind is kept for a later start
    /// (<see cref="PatternObject.KeepsFailureMemosAcrossAttempts"/>). An atomic group, a lookaround and a conditional's test throw away the body's
    /// backtrack entries once the body reaches its end, so a failure recorded inside one of them
    /// only ever means "cannot reach the end of the construct from here", a fact about the
    /// position; and a group call clears the guard lists on the way in and restores them on the
    /// way out. A repeat inside a bounded
    /// repeat has no active guards at all (<c>AddRepeatGuards</c> does not walk a bounded body).
    /// POSIX matching replaces its best match only with a longer one, and the guards are not consulted under
    /// fuzzy matching. They stay as exclusions anyway: each is a place where the rest of the match
    /// can read more than the position, and none of those arguments has been proved in general.
    /// <c>docs/plan/2026-09-26-backtrack-memoisation-design.md</c> gives the full reasoning.
    /// </para>
    /// </remarks>
    internal bool FailureMemo;

    /// <summary>
    /// NOT UPSTREAM (D90): whether the repeat is inside a lookaround, a condition's test, an atomic
    /// group or a possessive repeat, whose end a partial call's second pass can block
    /// (<c>MatchState.BlockedEndMark</c>). Set by <c>NodeCompiler</c>.
    /// </summary>
    internal bool InsideACutBody;

    /// <summary>
    /// Where the copy of this repeat's first iteration starts, for a failure-memo repeat whose
    /// minimum was 1: the placeholder node <c>NodeCompiler.BuildRepeat</c> built the copy from, so
    /// a one-way branch that <c>Optimiser.FindFirstIterationAtStart</c> looks through. Otherwise
    /// <see langword="null"/>. <b>Not upstream</b> (C5, S2d); see
    /// <see cref="PatternObject.FirstIterationAtStart"/>.
    /// </summary>
    internal Node? FirstIterationStart;

    /// <summary>
    /// NOT UPSTREAM (D85): what this repeat's tail guard is keyed on, or <see langword="null"/>
    /// when the rest of the match reads no group state and the position alone is the key. Set by
    /// <see cref="TailGuardLiveness.FindTailKeys"/>.
    /// </summary>
    internal TailGuardKeySpec? TailKey;

    /// <summary>
    /// NOT UPSTREAM (D104): what this repeat's records written after a blocked end are keyed on
    /// besides the instance, for a repeat <see cref="InsideACutBody"/>; see
    /// <c>MatchState.CutBodyKeyOf</c>. Set by <see cref="TailGuardLiveness.FindTailKeys"/>.
    /// </summary>
    internal TailGuardKeySpec? CutBodyKey;

    /// <summary>
    /// NOT UPSTREAM (D104): the loops enclosing this repeat, for a repeat
    /// <see cref="InsideACutBody"/>: their count, start and progress are in the key of its records
    /// written after a blocked end. Set by <see cref="TailGuardLiveness.FindTailKeys"/>.
    /// </summary>
    internal int[] EnclosingLoops = [];

    /// <summary>
    /// NOT UPSTREAM (D104): for each of <see cref="EnclosingLoops"/>, the count past which the key
    /// stops telling counts apart: the minimum of a loop with no maximum, else no cap.
    /// </summary>
    internal long[] EnclosingLoopCaps = [];

    /// <summary>
    /// NOT UPSTREAM (D104): the repeat has no maximum and a minimum of at most 1, so its own count
    /// cannot change what its body leads to, and a body record written after a blocked end may
    /// leave the count out of its key. A plain body record relies on the success mark instead
    /// (<c>GuardRepeat(..., protect: false)</c> at the iteration's end), which a record kept under
    /// its instance never gets. Set by <see cref="TailGuardLiveness.FindTailKeys"/>.
    /// </summary>
    internal bool CountFreeBody;

    /// <summary>
    /// NOT UPSTREAM (D85): the body holds a repeat whose tail guard is keyed, so this repeat's body
    /// guard is off. A body guard records that the body never got to its end from a position; a
    /// keyed tail guard inside can stop the body under one value of a group the rest of the match
    /// reads, so the record would block the body under another value
    /// (<c>(c*).?(?:(?:a|d)*ac{1,3})*\1d</c> over <c>cadaacd</c>). Set by
    /// <see cref="TailGuardLiveness.FindTailKeys"/>.
    /// </summary>
    internal bool BodyHoldsKeyedTail;

    /// <summary>
    /// NOT UPSTREAM (D85), and never read by this library: <see cref="Status"/> lost its body guard
    /// in <c>Optimiser.DropBodyGuardsThatReadAGroup</c>, where upstream would have kept it. The
    /// oracle reads it to put upstream's guards back on a pattern it compiled for one call, to show
    /// that D85 is the whole of a divergence.
    /// </summary>
    internal bool BodyGuardDroppedForD85;
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

    /// <summary>
    /// NOT UPSTREAM'S: whether the pattern holds a group call. Only then does the matcher track the
    /// text an attempt has reached (<see cref="MatchState.ReachedLow"/>), so a pattern without
    /// calls pays nothing for the call guard on its backtracking path.
    /// </summary>
    internal bool HasGroupCalls;

    /// <summary>Upstream <c>repeat_info</c>.</summary>
    internal readonly List<RepeatInfo> RepeatInfoList = [];

    /// <summary>
    /// Upstream <c>min_width</c>: the shortest string the pattern can match, ignoring fuzziness.
    /// </summary>
    internal long MinWidth;

    /// <summary>
    /// NOT UPSTREAM, and never read by this library: <see cref="MinWidth"/> as upstream computes it,
    /// with the called copies after the pattern's SUCCESS counted too (D49). The oracle's
    /// <c>RunWithTheUpstreamCallFeatures</c> puts it back.
    /// </summary>
    internal long UpstreamMinWidth;

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
    /// NOT UPSTREAM'S: every UTF-16 code unit that could hold the first character (in reading order)
    /// of <see cref="ReqString"/> for <c>Engine.RequiredStringScreen</c>, which searches for these
    /// with a vectorised scan before checking a window. Null when the screen does not apply.
    /// </summary>
    internal System.Buffers.SearchValues<char>? ReqScreenUnits;

    /// <summary>
    /// Whether the compiled graph holds a <c>(*SKIP)</c>. <b>This port's own field</b> (S60):
    /// upstream has no equivalent because upstream does not need one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The required-string prefilter moves the FIRST attempt forward, to the position the required
    /// string implies. Skipping a position that cannot match is answer-transparent only while
    /// attempts are independent of each other, and <c>(*SKIP)</c> is the one thing in this engine
    /// that makes them dependent: when backtracking reaches it, it sets
    /// <see cref="MatchState.SliceStart"/> to where it was reached (<c>Matcher</c>'s
    /// <see cref="Opcode.Skip"/> arms; upstream does so when it runs, <c>:14544</c>), so the attempt that
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
    /// NOT UPSTREAM'S, D111: whether the pattern holds something that commits to what it has read:
    /// an atomic group, which a possessive repeat, <c>\R</c> and <c>\X</c> compile to. Only such a
    /// pattern can match differently when text past a span is cut off, so only it needs a BESTMATCH
    /// or ENHANCEMATCH refinement to keep the caller's slice (<see cref="MatchState.RefineStart"/>);
    /// every other pattern keeps upstream's narrowed slice, which matches the same and reads no
    /// further than the span. A <c>(*PRUNE)</c> or <c>(*SKIP)</c> commits too, but D109 refuses a
    /// ranked pattern that holds one and can make errors, and only a match with errors is refined.
    /// </summary>
    internal bool RefinesInAWindow;

    /// <summary>
    /// NOT UPSTREAM'S, D115: whether the pattern holds a <c>\G</c>, whose answer depends on where a
    /// search began, so a failed search from one position says nothing about a search from a later
    /// one. See <c>Matcher.BestWalkKnownToFail</c>.
    /// </summary>
    internal bool ReadsTheSearchAnchor;

    /// <summary>
    /// NOT UPSTREAM'S, D115: whether the pattern holds a lookaround, or a conditional's lookaround
    /// test, that reads against the scan's direction: a lookbehind in a forward pattern, a
    /// lookahead in a reversed one. Its body can reach back from an attempt to where a search
    /// began, so a failed search from one position says nothing about a search from a later one.
    /// See <c>Matcher.BestWalkKnownToFail</c>.
    /// </summary>
    internal bool LooksAgainstTheScan;

    /// <summary>
    /// NOT UPSTREAM'S, D11: whether the pattern holds a <c>(*PRUNE)</c> or <c>(*SKIP)</c>, so that a
    /// run-out in a lookaround body may have skipped one (design section 4.2, the run-out taint).
    /// </summary>
    internal bool HasBacktrackingVerb;

    /// <summary>
    /// NOT UPSTREAM'S, D11: whether an attempt can choose a next start other than the ordinary
    /// bump-along's: a <c>(*SKIP)</c> moves the slice, and a <c>\K</c> whose backtracking entry a cut
    /// discards leaves the match position where it was set. A second-pass attempt that follows a
    /// touched one then counts as touched (<c>Matcher.BasicMatch</c>, at the attempt's start).
    /// </summary>
    internal bool AnAttemptCanMoveTheNextStart;

    /// <summary>
    /// NOT UPSTREAM'S, D11 chunk 8: whether a fuzzy section lies inside a lookaround body or a
    /// condition's test, in a pattern without group calls, so that a run-out there may leave the
    /// section open (<c>Matcher.CloseTheBodysSection</c>). Set with <see cref="Node.EnclosingSection"/>.
    /// </summary>
    internal bool HasFuzzyInABody;

    /// <summary>
    /// NOT UPSTREAM'S, D11: whether the pattern reads a group's capture while it matches, through a
    /// backreference or a group condition. A run-out in a lookaround body leaves the body's later
    /// captures unknown, which build chunk 8 models; until then such a pattern keeps the run-out
    /// partial (<c>Matcher.RunOutInALookaroundBody</c>).
    /// </summary>
    internal bool ReadsCapturesWhileMatching;

    /// <summary>
    /// NOT UPSTREAM (finding F-A): whether <c>Matcher.ExactDeletionMayMatch</c> may leave out the
    /// "delete it instead" choice of an item that matched exactly when that choice cannot lead to a
    /// match the search has not already ruled out. Set when the pattern is compiled.
    /// </summary>
    /// <remarks>
    /// The argument behind it (see <c>Matcher.ExactDeletionMayMatch</c>) exchanges one deletion for
    /// another, which can remove an error, so it does not hold where a section restricts which
    /// characters an error may touch (a <c>FUZZY</c> node with a test, which is asked at the
    /// deletion's position). And it shows only that the left-out choice holds no match, where a
    /// <c>(*SKIP)</c> or <c>(*PRUNE)</c> reached inside it would still change what the search does
    /// next. Either turns it off. A minimum error count does not; the matcher narrows there only
    /// once every minimum is met (<c>Matcher.AllMinimumsMet</c>).
    /// </remarks>
    internal bool NarrowExactDeletions;

    /// <summary>
    /// NOT UPSTREAM (D93 performance): whether <c>Matcher.ExactDeletionMayMatch</c> may count the
    /// items a match reads after a group call returns as part of an item's run
    /// (<c>Matcher.DeletionsPastReturns</c>). Needs the narrowing and exactly one fuzzy section, so
    /// a walk that crosses no <c>END_FUZZY</c> stays in the section instance it started in.
    /// </summary>
    internal bool NarrowsPastReturns;

    /// <summary>
    /// NOT UPSTREAM (finding F-A): the most items an exact match's "delete it instead" choice can
    /// have left to delete and still be offered; <see cref="long.MaxValue"/> where there is no such
    /// ceiling. Set when the pattern is compiled; read first by <c>Matcher.ExactDeletionMayMatch</c>,
    /// so the common case, a character with more of its string left than the budget has deletions,
    /// costs one comparison.
    /// </summary>
    /// <remarks>
    /// With the narrowing on and no minimum error count, <c>ExactDeletionMayMatch</c> refuses a
    /// choice when the items left exceed <c>DeletionRoom</c>, and that room is at most the current
    /// section's deletion limit, its error limit and its cost limit over a deletion's cost, since
    /// counts are never negative. So the largest of those over every section is a ceiling no room
    /// can pass. Elsewhere <c>ExactDeletionMayMatch</c> does not compare the count with the room,
    /// and the ceiling is off.
    /// </remarks>
    internal long ExactDeletionCeiling = long.MaxValue;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule, finding F-A): whether some fuzzy section has a minimum
    /// error count. Only then can an empty iteration that spent errors be admitted for a section
    /// minimum (<c>Matcher.RaisesUnmetMinimum</c>), only then can an exact item's deletion be
    /// needed for one (<c>Matcher.AllMinimumsMet</c>), and only then does the matcher keep
    /// <c>MatchState.SectionFrame</c>. Set when the pattern is compiled.
    /// </summary>
    internal bool HasFuzzyMinimum;

    /// <summary>
    /// NOT UPSTREAM (D94 performance): every one-character test inside a fuzzy section, when a
    /// substitution can only be taken where one of them rejects the subject character; null when
    /// the pattern holds an item this does not model (a fuzzy string, '.' or assertion, a
    /// reference, a reverse item, a lookaround or a call), and then nothing is pruned. Read by
    /// <c>Matcher.ASubstitutionMayFollow</c>. Set when the pattern is compiled, for a pattern with a
    /// fuzzy minimum only.
    /// </summary>
    internal Node[]? SubstitutionTests;

    /// <summary>
    /// NOT UPSTREAM (D94 performance): null when the failed-state memo
    /// (<c>Matcher.StateKnownToFail</c>) is off for this pattern; otherwise, per repeat, the count
    /// above which its count no longer matters (its minimum when it has no maximum), or
    /// <see cref="long.MaxValue"/> to keep it all. Set when a pattern with a fuzzy minimum is compiled.
    /// </summary>
    internal long[]? RepeatStateMemoCaps;

    /// <summary>
    /// NOT UPSTREAM (D81): whether one fuzzy section can be open inside another: the pattern has two
    /// or more, or one and a group call, which can enter it again from inside itself. Only then can
    /// a run-out find an enclosing section's limits broken (<c>Matcher.AnOpenSectionIsOverItsLimits</c>).
    /// Set when the pattern is compiled.
    /// </summary>
    internal bool NestsFuzzySections;

    /// <summary>
    /// Whether the matcher keeps <c>MatchState.SectionFrame</c>, the link from each open fuzzy
    /// section to the one around it: for a minimum (<see cref="HasFuzzyMinimum"/>) or for the
    /// enclosing limits a run-out checks (<see cref="NestsFuzzySections"/>). A pattern with neither
    /// pushes no link. Set when the pattern is compiled.
    /// </summary>
    internal bool KeepsSectionLinks;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule): whether the repeat memo runs (<c>Matcher.RepeatMemoHit</c>):
    /// for a fuzzy pattern whose tested groups, the groups a backreference or conditional reads,
    /// fit in <see cref="MemoGroups"/>. Set when the pattern is compiled.
    /// </summary>
    internal bool UseRepeatMemo;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule): the tested groups' numbers, at most two, whose spans are
    /// part of a repeat memo key.
    /// </summary>
    internal int[] MemoGroups = [];

    /// <summary>
    /// NOT UPSTREAM'S: the groups the call guard keys a call on (<c>CallRead</c>), as indexes into
    /// <c>MatchState.Groups</c>, each with what the key holds of it; empty when the
    /// pattern has no group call. Set when the pattern is compiled.
    /// </summary>
    internal (int Index, CallRead Read)[] CallReadGroups = [];

    /// <summary>
    /// NOT UPSTREAM'S: whether a capture group is matched against the pattern's direction, inside a
    /// lookbehind of a forward pattern or a lookahead of a reverse one, where its span can lie
    /// behind the position it is captured from. <c>Matcher.CouldRefuseInside</c> reads it. Written
    /// by <c>NodeCompiler</c>.
    /// </summary>
    internal bool CapturesAgainstDirection;

    /// <summary>What the call guard keys a read group on; see <c>CallRead</c>.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="info">The group, which a conditional or backreference reads.</param>
    /// <returns>Its span where the capture-change counter can see the span, else its text or one bit.</returns>
    private static CallRead CallReadOf(PatternObject pattern, GroupInfo info)
    {
        if (pattern.IsFuzzy || info.CapturedInRepeat)
        {
            return CallRead.Span;
        }

        return info.TextRead ? CallRead.Text : CallRead.SetOrUnset;
    }

    /// <summary>
    /// NOT UPSTREAM (the failed-call memo): whether <c>GROUP_CALL</c> may fail a call at once
    /// because an earlier call with the same entry key ran out of choices without returning
    /// (<c>Matcher.FailedCallKey</c>). Set when the pattern is compiled, for a pattern with a group
    /// call and none of the constructs that let a failed call leave something behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A call that fails is undone completely by backtracking, except where a construct succeeds
    /// and throws away the undo entries of its body. A lookaround, an atomic group, a possessive
    /// repeat or a conditional's test does that, so a capture list keeps an entry from a path that
    /// later failed, and skipping the failed call would leave that entry out:
    /// <c>(?r)(a)(?:b(?:(?R)|)(?R)?(?:(?!.(?R)(?R))(?:a.))*?.){2&lt;=e&lt;=3}</c> over <c>aa</c>
    /// changed group 1's capture list with the memo. A fuzzy section that ends in there does the
    /// same to the whole-match error total, which <c>END_FUZZY</c> restores only from the entry the
    /// construct threw away: <c>(?e)((?=(?:a){e&lt;=1}))(((?&gt;a)){1}()?(.?(?1)|(?0))){d&lt;=1}((?0)(a))?</c>
    /// over <c>xa</c> answered (0, 1) with two errors with the memo and (1, 1) with one without. So one
    /// of those constructs whose body holds a call, a fuzzy section or a <c>\K</c> turns the memo
    /// off (<see cref="WritesInDiscardingConstruct"/>, <see cref="KeepInSubmatch"/>). That also
    /// covers a call inside a lookbehind, which runs the other way and so can meet open calls the
    /// key does not hold. A capture group in there does not: the stray entry appeared only when a
    /// call wrote the capture, because a lookaround saved the captures only for a body holding a
    /// capture group, so <c>(a)(?:(?!.(?1))|.)+?b</c> over <c>aaab</c> gave group 1 [0,1][2,1] where
    /// <c>(a)(?:(?!.(a))|.)+?b</c> gives [0,1] (upstream 2026.9.10 still does). D10 made a call count
    /// as a group (<c>NodeCompiler.BuildGroupCall</c>), and the capture-list witness above now
    /// answers the same with the memo forced on. A call in there still turns the memo off: the group
    /// it calls may hold a fuzzy section, which leaves the error total as above, and in a lookbehind
    /// it meets open calls the key does not hold. Keeping the memo on for a capture group there is
    /// what keeps <c>(?:(?=(a*))|a)(?R)|\1x</c> polynomial.
    /// </para>
    /// <para>
    /// <c>(*PRUNE)</c> and <c>(*SKIP)</c> keep it off, and so does POSIX matching, without a
    /// witness, as <see cref="RepeatInfo.FailureMemo"/> does. A partial match does not use it either
    /// (<c>MatchState.InitMatch</c>): a path that reaches the end of the text records a partial
    /// result even if it then fails. See <c>docs/plan/2026-09-27-recursion-failure-memo-design.md</c>.
    /// </para>
    /// </remarks>
    internal bool UseCallMemo;

    /// <summary>
    /// NOT UPSTREAM (the failed-call memo): how many <c>GROUP_CALL</c> nodes the pattern has. A pass
    /// makes (slice length + 1) x this many calls before the memo starts to build keys, so ordinary
    /// recursion, which makes about one call per character, pays one counter per call and nothing
    /// more.
    /// </summary>
    internal int GroupCallSites;

    /// <summary>
    /// NOT UPSTREAM (the failed-call memo): whether a group call or a fuzzy section
    /// sits inside an atomic group, a possessive repeat, a lookaround or a conditional's lookaround
    /// test. Written by <c>NodeCompiler</c>, read where <see cref="UseCallMemo"/> is set.
    /// </summary>
    internal bool WritesInDiscardingConstruct;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether the failed-call memo is off whatever
    /// <see cref="UseCallMemo"/> says. Tests and <c>tools/probes/recursion-failure-memo/memo-grid.cs</c>
    /// set it on a pattern compiled for one call, to compare every answer with the memo on and off.
    /// </summary>
    internal bool SkipCallMemo;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether the failed-call memo builds keys from
    /// the first call of a pass rather than after (slice length + 1) x
    /// <see cref="GroupCallSites"/> calls. Tests and the memo grid set it, since the small subjects
    /// they use never reach that count and would otherwise never exercise the memo.
    /// </summary>
    internal bool EagerCallMemo;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether the failed-call memo's key leaves out
    /// the position, a deliberately wrong key, so a call that failed at one position fails at
    /// every other. The matrix's check C5 sets it to prove the check can see a broken call memo
    /// (<c>tools/matrix/controls.py</c>); <c>FailedCallMemoTests</c> pins its witness.
    /// </summary>
    internal bool CallMemoKeyOmitsPosition;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library (D93): whether the call guard compares a
    /// re-entry's section counts with an open call's even when the re-entry has opened a new fuzzy
    /// section, so such a re-entry runs where the library refuses it (DECISIONS, 2026-10-17). The
    /// oracle and the matrix set it to show that the refusal is the whole of a difference from the
    /// written-out form or from upstream (<c>OracleComparer.RunWithTheReentryInANewSection</c>).
    /// </summary>
    internal bool CallGuardComparesAcrossSections;

    /// <summary>
    /// Whether a <c>\K</c> sits inside an atomic group, a possessive repeat, a lookaround, a
    /// conditional's lookaround test or a called group. <b>This port's own field</b>, written by
    /// <c>NodeCompiler.BuildBoundary</c> and read by <c>Optimiser.KeepFailureMemosSound</c>, which
    /// withdraws every failure memo when it is set.
    /// </summary>
    /// <remarks>
    /// <c>\K</c> moves the reported start and pushes an entry that moves it back when the path
    /// fails. In one of those constructs the entry is thrown away as soon as the construct
    /// succeeds, so the moved start outlives the path that moved it, and a failing path has a
    /// lasting effect that a memo would skip: <c>(?:(?=\K)b|)+.c</c> over <c>aabc</c> is (3, 4)
    /// upstream and was (2, 4) with the memo. It is the only such effect. The other entries a
    /// succeeding construct throws away restore captures, the capture-change counter and fuzzy
    /// counts, which the construct saved on entry and puts back itself when the engine
    /// backtracks past it, or belong to repeats, group calls and fuzzy sections, which the memo
    /// excludes already.
    /// </remarks>
    internal bool KeepInSubmatch;

    /// <summary>
    /// NOT UPSTREAM (D104): some positive lookaround of the pattern has a
    /// <see cref="Node.ContinuationKeyRepeat"/>, so a partial call's second pass may record its
    /// continuations (<c>MatchState.ContinuationKeyOf</c>). Set by <see cref="SetLookaroundBodies"/>.
    /// </summary>
    internal bool KeysContinuations;

    /// <summary>
    /// Whether a search keeps the failure memos (<see cref="RepeatInfo.FailureMemo"/>) from one
    /// start position to the next. <b>This port's own field</b>, written by
    /// <c>Optimiser.KeepFailureMemosSound</c>: set when some repeat has a failure memo and the
    /// pattern has no <c>\G</c>. A partial call resets the memos at every start all the same
    /// (<c>Matcher.BasicMatch</c>, <c>FAILURE</c> arm).
    /// </summary>
    /// <remarks>
    /// A failure memo says that entering the repeat's body at a position leads to no match.
    /// Nothing the rest of the match reads changes between the attempts of one pass: captures are
    /// read only by the constructs that switch the memo off, the text and the slice are the same,
    /// and a match ending at the search anchor (rejected under <c>MustAdvance</c>) can only start
    /// there, before every later attempt. So a failure recorded in one attempt holds in the next.
    /// <c>\G</c> is excluded only because no workload needs it yet: it reads a position fixed for
    /// the pass, so it is a fact about the position too. A partial call is excluded because the
    /// touch flag that decides what <c>SUCCESS</c> does is set per attempt.
    /// <c>docs/plan/2026-10-11-memo-option-b.md</c> gives the argument row by row.
    /// </remarks>
    internal bool KeepsFailureMemosAcrossAttempts;

    /// <summary>
    /// The index of the failure-memo repeat whose copied first iteration is where every attempt
    /// starts, or -1. <b>This port's own field</b> (C5, S2d), written by
    /// <c>Optimiser.FindFirstIterationAtStart</c> only when
    /// <see cref="KeepsFailureMemosAcrossAttempts"/> holds.
    /// </summary>
    /// <remarks>
    /// <c>(a+)+c</c> compiles as <c>(a+)(a+)*c</c>: the compiler copies the first iteration out of
    /// the repeat R (<c>NodeCompiler.BuildRepeat</c>), so an attempt at s enters the copy at s, and
    /// the copy is not a memo repeat. But the copy at s can match only if R's body at s can: what
    /// follows the copy is R with a count of 0, no maximum and a minimum of 0, which offers an
    /// iteration and then the tail, exactly what <c>END_GREEDY_REPEAT</c> offers after R's body,
    /// plus, after an empty copy, R's body at s once more, which fails too. So once R's body is
    /// guarded at s, the attempt at s cannot match, and <c>Matcher.BasicMatch</c> fails it before
    /// it starts. The guard was written in an earlier attempt of the same search, and holds there
    /// for the reasons <see cref="KeepsFailureMemosAcrossAttempts"/> gives; a partial call does not
    /// keep the guards and does not skip. That makes the later attempts of <c>(a+)+c</c> one
    /// lookup each, and <c>(x+x+)+y</c> quadratic rather than cubic.
    /// <c>docs/plan/2026-10-13-linear-nested-repeats-design.md</c>, option S2d.
    /// </remarks>
    internal int FirstIterationAtStart = -1;

    /// <summary>
    /// The private numbers of the groups a backreference or a conditional tests
    /// (<see cref="GroupInfo.Referenced"/>), in order. <b>This port's own field</b> (D17), written by
    /// <c>Optimiser.CollectTestedGroups</c>; when it is empty the matcher neither numbers repeat runs
    /// nor records empty-iteration states. See <c>Matcher.RevisitsEmptyIterationState</c>.
    /// </summary>
    internal int[] TestedGroups = [];

    /// <summary>
    /// The largest finite limit any fuzzy section in the pattern sets, on one kind of error, on all
    /// errors or on cost, or -1 when none sets one. <b>This port's own field</b> (D17), written by
    /// <c>Optimiser.FindFuzzyLimit</c> and read by <c>Matcher.ErrorCountCap</c>.
    /// </summary>
    internal long LargestFuzzyLimit = -1;

    /// <summary>
    /// The start-position prefilter for a pattern that is one fuzzy ASCII literal, or
    /// <see langword="null"/>. <b>This port's own field</b> (S60b item 10); see
    /// <see cref="Engine.FuzzyLiteralFilter"/>.
    /// </summary>
    internal FuzzyLiteralFilter? FuzzyLiteralFilter;

    /// <summary>
    /// The single-character repeat a search attempt starts with, or <see langword="null"/>: the
    /// one whose run <c>Matcher.BasicMatch</c>'s <c>FAILURE</c> arm skips before the next attempt.
    /// <b>Wider than upstream's</b> (S60b item 17); see <see cref="FindLeadingRepeat"/>.
    /// </summary>
    internal Node? LeadingRepeat;

    /// <summary>
    /// The literals a search attempt can start with when the pattern opens with an alternation
    /// whose every branch opens with one, or <see langword="null"/>. <b>This port's own field</b>
    /// (S60b item 14); see <see cref="FindLeadingStrings"/>.
    /// </summary>
    internal SearchValues<string>? LeadingStrings;

    /// <summary>
    /// The code units a search attempt can start on when the pattern opens with an alternation
    /// whose every branch opens with a literal and at least one of those ignores case, or
    /// <see langword="null"/>. <b>This port's own field</b> (2026-10-11, the fold-aware screen);
    /// see <see cref="FindLeadingStrings"/>.
    /// </summary>
    internal SearchValues<char>? LeadingUnits;

    /// <summary>Upstream <c>is_fuzzy</c>.</summary>
    internal bool IsFuzzy;

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): the code offsets of the <c>NEXT</c> words that
    /// <c>Branch.OptionalPassEndWord</c> marks (<see cref="CompiledPattern.OptionalPassEnds"/>). Read
    /// by <c>NodeCompiler.BuildBranch</c>; <see langword="null"/> when there are none, as there
    /// are in most patterns.
    /// </summary>
    internal HashSet<int>? OptionalPassEnds { get; private init; }

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): the 2-way branch of each alternative that has an
    /// alternative written empty after it, with the <c>END_OPTIONAL_PASS</c> node that ends its
    /// pass, as <c>NodeCompiler.BuildBranch</c> makes them; <see langword="null"/> when there are
    /// none. Compile turns it into <see cref="OptionalPassEndOf"/> once the nodes are numbered.
    /// </summary>
    internal List<(Node Branch, Node PassEnd)>? OptionalPassBranches;

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): by <see cref="Node.Index"/>, the
    /// <c>END_OPTIONAL_PASS</c> node that ends the pass a 2-way branch opens, or null for a node
    /// that opens none; <see langword="null"/> for a pattern with no such branch. Taking the branch
    /// records where the pass began in the node's slot, and that node reads it back
    /// (<c>Matcher.OptionalPassAdmitted</c>).
    /// </summary>
    /// <remarks>
    /// A table on the pattern rather than a field on every node: only a fuzzy pattern with an
    /// alternative written empty has one, and a field would cost every node of every pattern.
    /// </remarks>
    internal Node?[]? OptionalPassEndOf;

    /// <summary>
    /// NOT UPSTREAM (finding F-A): by <see cref="Node.Index"/>, the first node after the fuzzy run
    /// through that node that is not an <c>END_FUZZY</c>, <c>START_GROUP</c> or <c>END_GROUP</c>,
    /// which read no text; null for a node in no run or where the chain ends. Set by
    /// <see cref="SetFuzzyRunLengths"/>, and <see langword="null"/> for a pattern with no run. Read
    /// by <c>Matcher.ExactDeletionMayMatch</c> for a node whose <see cref="Node.FuzzyRunLength"/>
    /// is not 0. On the pattern for the reason <see cref="OptionalPassEndOf"/> is.
    /// </summary>
    internal Node?[]? FuzzyRunExits;

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): how many alternations have an
    /// <c>END_OPTIONAL_PASS</c>, and so a slot in <see cref="MatchState.OptionalPasses"/>. Counted
    /// by <c>NodeCompiler.BuildBranch</c>.
    /// </summary>
    internal int OptionalPassCount;

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
    /// <see cref="Optimiser"/> fills this in, for BESTMATCH fuzzy patterns only since D61: every other
    /// search follows rule H' (<see cref="LiftsAnchorBan"/>).
    /// </para>
    /// </remarks>
    internal IReadOnlyList<Node>? AnchorGuards;

    /// <summary>
    /// Whether rule H' can change a search's answer, so that <c>Matcher.DoMatch2LiftingTheAnchorBan</c>
    /// runs the attempt at the anchor without the ban. <b>This port's own field (known defect D61).</b>
    /// Filled by <c>Optimiser.FindAnchorBanLift</c>; cleared by <c>OracleComparer.RunWithoutTheAnchorPin</c>,
    /// the oracle's ablation, and by nothing in the library.
    /// </summary>
    internal bool LiftsAnchorBan;

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
    /// Oracle-only: a fuzzy <c>STRING_FLD</c> item edits a subject character that expands under full
    /// case folding one folded character at a time, as upstream does, and never whole (D7). Set only
    /// by <c>OracleComparer</c>'s ablation; never by the library. See <c>Matcher.FoldWholeSub</c>.
    /// </summary>
    internal bool SkipWholeFoldedCharEdits;

    /// <summary>
    /// Oracle-only: a <c>(*SKIP)</c> moves the slice the moment it runs, as upstream's
    /// <c>RE_OP_SKIP</c> does (<c>:14551-14555</c>), instead of when backtracking reaches it (ledger
    /// entry 45). Set only by <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool SkipMovesTheSliceWhenItRuns;

    /// <summary>
    /// Oracle-only: a <c>(*PRUNE)</c> or <c>(*SKIP)</c> fails only the innermost atomic group,
    /// possessive repeat or lookaround it is in, as upstream's <c>top_bstack</c> makes it
    /// (<c>_regex.c:2811</c>), instead of unwinding through the unfinished atomic groups and positive
    /// lookarounds to the innermost negative assertion, conditional test or attempt (ledger entry
    /// 47). Set only by <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool VerbsAreConfinedToTheInnermostGroup;

    /// <summary>
    /// Oracle-only: a <c>\K</c> inside a lookaround or a condition's test outlives the construct's
    /// cut, as upstream's <c>RE_OP_END_LOOKAROUND</c> (<c>_regex.c:12918</c>) lets it, instead of
    /// being undone when matching backtracks past the construct (D88). Set only by
    /// <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool KeepOutlivesALookaround;

    /// <summary>
    /// Oracle-only: a <c>\K</c> inside an atomic group or possessive repeat outlives the group's
    /// cut, as upstream's <c>RE_OP_END_ATOMIC</c> lets it, instead of being undone when matching
    /// backtracks past the group (D88). Set only by <c>OracleComparer</c>'s ablation; never by the
    /// library.
    /// </summary>
    internal bool KeepOutlivesAnAtomicGroup;

    /// <summary>
    /// Oracle-only: a failed attempt skips past the run of a lazy leading repeat even when the
    /// pattern holds a <c>(*PRUNE)</c> or <c>(*SKIP)</c>, as upstream does (<c>_regex.c:15705-15723</c>),
    /// instead of bumping along (D77). Set only by <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool SkipsALazyRunDespiteAVerb;

    /// <summary>
    /// Oracle-only: a failed attempt steps on from the reported start a <c>\K</c> left ahead of it,
    /// as upstream steps on from <c>match_pos</c> (<c>_regex.c:15695</c>), instead of from where the
    /// attempt began (D92). Set only by <c>OracleComparer</c>'s ablations; never by the library.
    /// </summary>
    internal bool StepsOnFromTheKeptStart;

    /// <summary>
    /// Oracle-only: a BESTMATCH or ENHANCEMATCH scan step bans an empty match at every candidate run's
    /// start after an empty match, as upstream's <c>do_best_fuzzy_match</c> and
    /// <c>do_enhanced_fuzzy_match</c> do, instead of only at the resume position (D78). Set only by
    /// <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool BansAnEmptyMatchAtEveryCandidate;

    /// <summary>
    /// Oracle-only: <c>\G</c> holds at each BESTMATCH or ENHANCEMATCH candidate run's start, as
    /// upstream's does (it reads <c>search_anchor</c>, which every run's <c>init_match</c> moves),
    /// instead of only where the matching operation began (D79). Set only by <c>OracleComparer</c>'s
    /// ablation; never by the library.
    /// </summary>
    internal bool AnchorsBackslashGAtEveryCandidate;

    /// <summary>
    /// Oracle-only: a fuzzy section a <c>(*PRUNE)</c> or <c>(*SKIP)</c> cut through stays the open
    /// one, as upstream leaves <c>fuzzy_node</c> (D44, ledger entry 61). Set only by
    /// <c>OracleComparer</c>'s ablation; never by the library. See
    /// <c>MatchState.PushSubAttemptFuzzyState</c>.
    /// </summary>
    internal bool KeepSectionOpenAfterAVerb;

    /// <summary>
    /// Oracle-only: a match may reach SUCCESS with a group call and its fuzzy section still open,
    /// and with more changes listed than counted, as upstream's does when a call to the whole
    /// pattern jumps to its start with no return (D37, ledger entry 55). It
    /// changes no answer; it only lets <c>Matcher.AssertMatchIsClosed</c> accept the ablation that
    /// reproduces that on purpose. Set only by <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool MatchMayEndInsideTheWholePatternCall;

    /// <summary>
    /// Oracle-only: the running error total and cost keep the errors of a fuzzy section an atomic
    /// group, lookaround, condition or verb threw away, as upstream's <c>total_errors</c> does (D45,
    /// ledger entry 32). Set only by <c>OracleComparer</c>'s ablation; never by the library. See
    /// <c>MatchState.PushSubAttemptFuzzyState</c>.
    /// </summary>
    internal bool KeepDiscardedTotals;

    /// <summary>
    /// Oracle-only: the budget BESTMATCH and ENHANCEMATCH tighten between runs also bounds every
    /// lookaround's body, a conditional's test and an atomic group's body, as upstream's
    /// <c>max_errors</c> does (D102 and D109, ledger entry 32's addenda of 2026-10-17 and -19). Set only by <c>OracleComparer</c>'s
    /// ablation; never by the library. See <c>MatchState.BoundlessBodies</c>.
    /// </summary>
    internal bool BoundsReachDecidingBodies;

    /// <summary>
    /// Oracle-only: a conditional whose negative test fails, because the test's body matched, puts
    /// back the captures, repeats and fuzzy counts saved before the test and then takes the no
    /// branch, as upstream's <c>END_CONDITIONAL</c> does (<c>upstream/src/_regex.c</c>:12423-12432;
    /// D54, ledger entry 65). Set only by <c>OracleComparer</c>'s ablation; never by the library.
    /// </summary>
    internal bool DropNegativeTestCaptures;

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

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether END_FUZZY's backtrack arm counts the
    /// section's errors twice when it decides whether a trailing insertion still fits the budget,
    /// which is upstream's rule. The oracle sets it on a pattern it compiled for one call, to show
    /// that the S46 fix is part of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream tests <c>total_errors(state-&gt;fuzzy_counts) + total_errors(inner_counts) &lt;
    /// state-&gt;max_errors</c> (<c>upstream/src/_regex.c</c>:15516), but the forward arm has already
    /// added the inner counts into <c>fuzzy_counts</c>. The budget is only finite under BESTMATCH,
    /// so <c>(?b)(?:){e&lt;=3}</c> finds nothing in <c>znz</c> where the flagless engine finds three
    /// insertions. Ledger entry 12; see <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool DoubleCountTrailingInsertions;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether the WORD flag's default word boundary
    /// uses upstream's rules rather than UAX #29's. The oracle sets it on a pattern it compiled for
    /// one call, to show that the ledger entry 40 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream applies WB4 only to the character on the left, returns "no break" when a run of
    /// Extend, Format or ZWJ reaches the start of the text, keeps an odd run of regional indicators
    /// with whatever follows, and adds a WB5a joining an apostrophe to a vowel. See
    /// <c>Matcher.AtUpstreamDefaultBoundary</c> and <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool UpstreamDefaultBoundary;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a partial call's second pass runs as it
    /// did before D11, as upstream's does: the word, line and string tests read against the held
    /// text (M1 off), no attempt from inside the text to come, so that a search the second pass
    /// finds nothing for answers failure, and a fuzzy insertion at the edge that fails rather than
    /// running out (RC5 off). The oracle sets it on a pattern it compiled for one call, to show that
    /// D11 is the whole of a divergence. See <c>Matcher.TryMatchZeroWidth</c>,
    /// <c>Matcher.MatchFromInsideTheTextToCome</c> and the insertion arm of
    /// <c>Matcher.NextFuzzyMatchItem</c>.
    /// </summary>
    internal bool SecondPassAsBeforeD11;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a repeat's tail test that runs out of
    /// text answers the run-out at once, as upstream's does, rather than letting the tail be entered
    /// (D67). The oracle sets it on a pattern it compiled for one call, to show that D67 is the whole
    /// of a divergence. See <c>Matcher.TailMayMatch</c>.
    /// </summary>
    internal bool TailRunOutAsBeforeD67;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether this pattern was compiled with
    /// <see cref="Parsing.Info.UpstreamReverseGrapheme"/>, so that a reversed <c>\X</c> also keeps
    /// upstream's answer at the slice start (D108). See <c>Matcher.ReversedGraphemeCutBySliceStart</c>.
    /// </summary>
    internal bool UpstreamReverseGrapheme;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a run-out answers without checking the
    /// open fuzzy sections' upper limits, as upstream's does (D81). The oracle sets it on a pattern
    /// it compiled for an ablation, since upstream has none of this port's fixes. See
    /// <c>Matcher.AnOpenSectionIsOverItsLimits</c>.
    /// </summary>
    internal bool RunOutAsBeforeD81;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether an item that matched exactly is never
    /// offered as a deletion, which is upstream's rule. The oracle sets it on a pattern it compiled
    /// for one call, to show that the ledger entry 42 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream tries errors on a fuzzy item only when it fails to match
    /// (<c>upstream/src/_regex.c</c>:10185-10258), so <c>(?:a){d&lt;=1}a</c> finds nothing in
    /// <c>a</c>. See <c>Matcher.ExactDeletionMayMatch</c> and <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool SkipExactDeletionRetry;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a lookaround that fails in a fuzzy
    /// section is never passed by an insertion, which is upstream's rule. The oracle sets it on a
    /// pattern it compiled for one call, to show that the ledger entry 50 fix is the whole of a
    /// divergence.
    /// </summary>
    /// <remarks>
    /// Upstream fuzzes a failing <c>\b</c> or <c>$</c> (<c>upstream/src/_regex.c</c>:12060-12075) but
    /// not a failing lookaround (:12918-13000, :17115-17168), so <c>(?:b(?=c)){i&lt;=1}</c> finds
    /// nothing in <c>bxc</c>. See <c>Matcher.InsertBeforeAFailedLookaround</c>.
    /// </remarks>
    internal bool SkipLookaroundInsertion;

    /// <summary>
    /// NOT UPSTREAM, and never read by this library: the lookarounds that save and restore the
    /// captures only because their body calls a group (D10), or <see langword="null"/> when there
    /// are none. Written by <c>NodeCompiler.BuildLookaround</c>. The oracle clears their
    /// <see cref="NodeStatus.HasGroups"/> flag on a pattern it compiled for one call, to show that
    /// the D10 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream saves the captures around a lookaround only when its body holds a capture group
    /// (<c>upstream/src/_regex.c</c>:13772), so a call's capture outlives a body that is thrown away:
    /// <c>(a)(?:(?!.(?1))|.)+?b</c> over <c>aaab</c> leaves group 1 with [0,1][2,1]. See
    /// <c>NodeCompiler.BuildGroupCall</c>.
    /// </remarks>
    internal List<Node>? LookaroundsSavingOnlyForCalls;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a group call's captures stay on the
    /// group's capture list when the call returns, which is upstream's rule. The oracle sets it on a
    /// pattern it compiled for one call, to show that the D51 fix (ledger entry 63) is the whole of a
    /// divergence.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>GROUP_RETURN</c> restores only each group's <c>current</c>
    /// (<c>push_groups</c>, <c>upstream/src/_regex.c</c>:2490, pushed at :13405 and popped at :13516), so
    /// <c>(?(DEFINE)(?&lt;c&gt;a))(?&amp;c)b</c> over 'ab' leaves c unset with the capture 'a'
    /// listed. See <c>Matcher.ReturnToCallerCaptures</c>.
    /// </remarks>
    internal bool KeepCallCaptures;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a partial result restores the groups
    /// by walking every open call's frame, innermost first, instead of from the outermost frame
    /// alone. Both give the same groups, since each frame saved its caller's; the oracle sets it on
    /// a pattern it compiled for one call, as a second computation the D62 key compares against, so
    /// a restore from the wrong frame is not accounted for as the divergence (blind review,
    /// 2026-09-30). See <c>Matcher.BasicMatch</c>.
    /// </summary>
    internal bool PartialRestoresFrameByFrame;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a fuzzy section that reaches its end
    /// below its minimum error count fails at once, without trying the trailing insertions that
    /// could meet it, which is upstream's order. The oracle sets it on a pattern it compiled for one
    /// call, to show that the ledger entry 51 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream's forward <c>END_FUZZY</c> checks the minimums (<c>upstream/src/_regex.c</c>:12461)
    /// before it pushes the frame that offers trailing insertions (:12500-12511), so
    /// <c>(?:a){1&lt;=e&lt;=2}b</c> finds nothing in 'aab'. See <c>Matcher.InsertionsCanMeetMinimum</c>.
    /// </remarks>
    internal bool CheckMinimumBeforeTrailingInsertions;

    /// <summary>
    /// NOT UPSTREAM, and never set by this library: whether a repeat's end reads progress as
    /// upstream does, counting any fuzzy edit, even one since undone, and stopping at the end of the
    /// slice, instead of the "needed" rule and the repeat memo. The oracle sets it on a pattern it
    /// compiled for one call, to show that the ledger entry 44 fix is the whole of a divergence.
    /// </summary>
    /// <remarks>
    /// Upstream's rule is <c>upstream/src/_regex.c</c>:12550-12557; under it some patterns loop
    /// until the backtracking stack is exhausted, which the oracle's own deadline bounds. See
    /// <c>Matcher.EmptyIterationAdmitted</c> and <c>docs/DIVERGENCES.md</c>.
    /// </remarks>
    internal bool UpstreamEmptyIterations;

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
            OptionalPassEnds = compiled.OptionalPassEnds.Count == 0 ? null : [.. compiled.OptionalPassEnds],
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

            if (self.ReqString is Node screened)
            {
                self.ReqScreenUnits = RequiredStringScreen.FirstUnits(screened);
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
        bool noNarrowing = false;
        bool hasPrune = false;
        int sections = 0;
        for (int i = 0; i < self.NodeList.Count; i++)
        {
            self.NodeList[i].Index = i;
            if (self.NodeList[i].Op == Opcode.Fuzzy)
            {
                ++sections;
            }

            // NOT UPSTREAM'S (S60): note a '(*SKIP)' while we are walking the list anyway, so the
            // prefilter knows not to move the first attempt. See HasSkipVerb.
            if (self.NodeList[i].Op == Opcode.Skip)
            {
                self.HasSkipVerb = true;
            }

            // NOT UPSTREAM'S, D111: see RefinesInAWindow.
            if (self.NodeList[i].Op == Opcode.Atomic)
            {
                self.RefinesInAWindow = true;
            }

            // NOT UPSTREAM'S, D115: see ReadsTheSearchAnchor.
            if (self.NodeList[i].Op == Opcode.SearchAnchor)
            {
                self.ReadsTheSearchAnchor = true;
            }

            // NOT UPSTREAM'S, D11: see HasBacktrackingVerb and ReadsCapturesWhileMatching.
            if (self.NodeList[i].Op is Opcode.Skip or Opcode.Prune)
            {
                self.HasBacktrackingVerb = true;
            }

            if (self.NodeList[i].Op is Opcode.Skip or Opcode.Keep)
            {
                self.AnAttemptCanMoveTheNextStart = true;
            }

            if (
                self.NodeList[i].Op
                is Opcode.GroupExists
                    or Opcode.RefGroup
                    or Opcode.RefGroupFld
                    or Opcode.RefGroupFldRev
                    or Opcode.RefGroupIgn
                    or Opcode.RefGroupIgnRev
                    or Opcode.RefGroupRev
            )
            {
                self.ReadsCapturesWhileMatching = true;
            }

            // NOT UPSTREAM (finding F-A): see NarrowExactDeletions.
            Node node = self.NodeList[i];
            if (
                node.Op == Opcode.Fuzzy
                && (
                    node.Values[FuzzyValue.MinSub] > 0
                    || node.Values[FuzzyValue.MinIns] > 0
                    || node.Values[FuzzyValue.MinDel] > 0
                    || node.Values[FuzzyValue.MinErr] > 0
                )
            )
            {
                self.HasFuzzyMinimum = true;
            }

            if (node.Op == Opcode.Prune || (node.Op == Opcode.Fuzzy && node.Next2.Node is not null))
            {
                noNarrowing = true;
            }

            // NOT UPSTREAM (the failed-call memo): see UseCallMemo.
            hasPrune |= node.Op == Opcode.Prune;
            if (node.Op == Opcode.GroupCall)
            {
                ++self.GroupCallSites;
            }
        }

        // NOT UPSTREAM (D81): see NestsFuzzySections and KeepsSectionLinks.
        self.NestsFuzzySections = sections > 1 || (sections == 1 && self.HasGroupCalls);
        self.KeepsSectionLinks = self.HasFuzzyMinimum || self.NestsFuzzySections;

        // NOT UPSTREAM (D94 performance): see SubstitutionTests.
        if (self.HasFuzzyMinimum)
        {
            self.SubstitutionTests = Matcher.CollectSubstitutionTests(self.NodeList);
            self.RepeatStateMemoCaps = Matcher.MemoCapsFor(self, sections);
        }

        // NOT UPSTREAM (D61): after the loop above, which settles HasFuzzyMinimum.
        Optimiser.FindAnchorBanLift(self);

        self.NarrowExactDeletions = !noNarrowing && !self.HasSkipVerb;
        self.NarrowsPastReturns = self.NarrowExactDeletions && sections == 1 && self.HasGroupCalls;
        if (self.NarrowExactDeletions && !self.HasFuzzyMinimum)
        {
            self.ExactDeletionCeiling = DeletionCeiling(self);
        }

        // NOT UPSTREAM (the failed-call memo): see UseCallMemo.
        self.UseCallMemo =
            self.GroupCallSites > 0
            && !self.WritesInDiscardingConstruct
            && !self.KeepInSubmatch
            && !hasPrune
            && !self.HasSkipVerb
            && (self.Flags & RegexFlags.Posix) == 0;

        // NOT UPSTREAM (empty-iteration rule): the groups a repeat memo key must hold. A key has
        // room for two spans; with more tested groups the memo is off, which only costs pruning.
        // SHORTCUT: two tested groups is the ceiling; a pattern with more would need a key that
        // holds a variable number of spans.
        var tested = new List<int>();
        for (int g = 1; g <= self.GroupInfoList.Count; g++)
        {
            if (self.GroupInfoList[g - 1].Referenced)
            {
                tested.Add(g);
            }
        }

        self.UseRepeatMemo = self.IsFuzzy && tested.Count <= 2;
        self.MemoGroups = [.. tested];
        if (self.HasGroupCalls)
        {
            self.CallReadGroups = [.. tested.Select(g => (g - 1, CallReadOf(self, self.GroupInfoList[g - 1])))];
        }

        // NOT UPSTREAM (finding F-A): the fuzzy runs Matcher.ExactDeletionMayMatch reads, once the
        // nodes are numbered, since the walk marks nodes by Node.Index. A run is of fuzzy items, so
        // an exact pattern has none.
        if (self.IsFuzzy)
        {
            SetFuzzyRunLengths(self);
        }

        // NOT UPSTREAM'S, D11 chunk 8: the bodies a run-out inside a lookaround or condition test
        // reads, once the nodes are numbered.
        SetLookaroundBodies(self);

        // NOT UPSTREAM (ledger entry 44's addendum): see OptionalPassEndOf.
        if (self.OptionalPassBranches is { } optionalPassBranches)
        {
            self.OptionalPassEndOf = new Node?[self.NodeList.Count];
            foreach ((Node branch, Node passEnd) in optionalPassBranches)
            {
                if (branch.Index < self.NodeList.Count && ReferenceEquals(self.NodeList[branch.Index], branch))
                {
                    self.OptionalPassEndOf[branch.Index] = passEnd;
                }
            }

            self.OptionalPassBranches = null;
        }

        // NOT UPSTREAM'S (S60b item 10): the prefilter for a pattern that is one fuzzy literal.
        self.FuzzyLiteralFilter = FuzzyLiteralFilter.TryCreate(self);

        self.LeadingRepeat = FindLeadingRepeat(self);

        (self.LeadingStrings, self.LeadingUnits) = FindLeadingStrings(self);

        // NOT UPSTREAM'S (S60b item 9): index the long runs of branches by first character.
        BranchChain.Build(self.NodeList);

        // NOT UPSTREAM'S (S62): last, so every node and every set member is final. See
        // Node.AsciiAnswers.
        foreach (Node node in self.NodeList)
        {
            Matcher.FillAsciiAnswers(node);
        }

        return self;
    }

    /// <summary>
    /// Finds <see cref="LeadingStrings"/> or <see cref="LeadingUnits"/>. Not upstream code: upstream
    /// has no fast search for a pattern that opens with a branch, so it tries every position (S60b
    /// item 14; for case-insensitive branches, 2026-10-11).
    /// sync-divergence: the search loop jumps to the earliest of these literals, or of these code
    /// units, before each attempt.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A match of <c>cat|dog</c> starts where <c>cat</c> or <c>dog</c> does, so no attempt anywhere
    /// else can succeed, and the engine still chooses the branch. The walk follows each branch to
    /// its first node, looking through capture groups, which record where they open and do not read
    /// text, nested branches, and the zero-width first-set check the compiler puts in front of a
    /// branch, case-sensitive or not, which tests the character an attempt starts on and moves
    /// nothing. Since C6 it also looks through a word boundary, a start-of-line anchor or a
    /// lookaround in front, so <c>\b(?:cat|dog)\b</c> searches for <c>cat</c> and <c>dog</c>: the
    /// test reads the text around the position and moves nothing, so a match still starts on a
    /// literal. Every path must reach a forward literal. Anything else, such as an empty branch, a
    /// class, <c>\G</c>, a string anchor or an end anchor, gives <see langword="null"/>.
    /// </para>
    /// <para>
    /// When every literal is case-sensitive the result is the literals themselves, since a search
    /// for whole strings skips more than one for first characters. When one ignores case, which
    /// under full case folding can let <c>ﬅ</c> start <c>st</c> or <c>ß</c> start <c>ss</c>, the
    /// result is the first UTF-16 unit of every character that could start any of them, by the
    /// rule <see cref="RequiredStringScreen.FirstCodepoints"/> applies to a required string. An
    /// astral character contributes only its high surrogate, so a hit is never inside a pair; a
    /// lone high surrogate in the subject is then a harmless false hit. A LOCALE node never gets
    /// here: construction rejects case-insensitive LOCALE matching.
    /// </para>
    /// <para>
    /// A literal holding a surrogate code point on its own is refused. A search for a lone low
    /// surrogate could stop in the middle of a pair, a position the engine never starts at.
    /// </para>
    /// <para>
    /// Not fuzzy, where an error can delete the literal, and not reversed. Partial matches and
    /// <c>(*SKIP)</c> are refused at the call site in <c>Matcher.BasicMatch</c>.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The compiled pattern.</param>
    /// <returns>The literals or the code units, at most one of them set.</returns>
    private static (SearchValues<string>? Strings, SearchValues<char>? Units) FindLeadingStrings(PatternObject pattern)
    {
        if (pattern.IsFuzzy || (pattern.Flags & RegexFlags.Reverse) != 0)
        {
            return (null, null);
        }

        var heads = new List<Node>();
        int branches = 0;

        if (!Collect(pattern.StartNode) || branches == 0)
        {
            return (null, null);
        }

        if (heads.TrueForAll(static head => head.Op is Opcode.String or Opcode.Character))
        {
            return (
                SearchValues.Create(
                    [
                        .. heads.Select(static head =>
                            string.Concat(head.Values.Select(static c => char.ConvertFromUtf32((int)c)))
                        ),
                    ],
                    StringComparison.Ordinal
                ),
                null
            );
        }

        var units = new HashSet<char>();
        foreach (Node head in heads)
        {
            foreach (uint codepoint in RequiredStringScreen.FirstCodepoints(head))
            {
                units.Add(char.ConvertFromUtf32((int)codepoint)[0]);
            }
        }

        return (null, SearchValues.Create([.. units]));

        bool Collect(Node? node)
        {
            while (true)
            {
                switch (node?.Op)
                {
                    case Opcode.Branch when node.Next2.Node is not null:
                        // The count bounds the recursion, which only a branch nested first in a
                        // branch takes.
                        if (++branches > _maxLeadingBranches || !Collect(node.Next1.Node))
                        {
                            return false;
                        }

                        node = node.Next2.Node;
                        continue;
                    case Opcode.Branch:
                    case Opcode.StartGroup:
                    // The zero-width first-set check the compiler puts in front of a branch
                    // (ParseFunctions.CheckFirstset) reads nothing; it can only refuse a position.
                    // Its ignore-case forms too (D84): which characters such a check admits,
                    // under any encoding, does not matter, since the heads alone decide the
                    // screen and the check still runs at every attempt. In front of literal heads
                    // the ignore-case check is only ever one character or a union of characters
                    // (CheckFirstset never merges characters into a range), so only those two
                    // forms are looked through.
                    case Opcode.Character
                    or Opcode.CharacterIgn
                    or Opcode.Range
                    or Opcode.SetDiff
                    or Opcode.SetInter
                    or Opcode.SetSymDiff
                    or Opcode.SetUnion
                    or Opcode.SetUnionIgn when node.Step == 0:
                    // C6: a word boundary or start-of-line anchor tests the position the attempt
                    // stands on and goes on from that same position (BasicMatchCore's zero-width
                    // arm calls TryMatchZeroWidth at TextPos, then takes Next1), so a match
                    // starting there still opens with one of the heads. The screen only moves the
                    // attempt to the next head; the engine still runs the test there. Not
                    // StartOfString, which anchors the search so that no screen is consulted, the
                    // end anchors, after which only a newline can follow and the compiler factors
                    // it out in front of the branch, or SearchAnchor (\G), which holds at one
                    // position.
                    case Opcode.Boundary
                    or Opcode.DefaultBoundary
                    or Opcode.StartOfWord
                    or Opcode.EndOfWord
                    or Opcode.DefaultStartOfWord
                    or Opcode.DefaultEndOfWord
                    or Opcode.StartOfLine
                    or Opcode.StartOfLineU:
                        node = node.Next1.Node;
                        continue;
                    // C6: a lookaround in front runs its body and goes on from the position it
                    // started at (AdvanceLookaround saves TextPos; EndLookaround and a failed
                    // negative body both lead to Next2, the node after it, NodeCompiler.
                    // BuildLookaround), so a match still opens with one of the heads. Refused when
                    // a body could move the next attempt's start: a \K there whose entry the body's
                    // end discards leaves the bump-along at the position it set
                    // (AnAttemptCanMoveTheNextStart), so skipping that attempt would change which
                    // start comes next. Also refused with a verb or a group call, which this
                    // change has not proved.
                    case Opcode.Lookaround
                        when !pattern.AnAttemptCanMoveTheNextStart
                            && !pattern.HasBacktrackingVerb
                            && !pattern.HasGroupCalls:
                        Debug.Assert(
                            ReferenceEquals(node.LookaroundEnd?.Next1.Node, node.Next2.Node),
                            "C6: a lookaround's body ends where its continuation starts"
                        );
                        node = node.Next2.Node;
                        continue;
                    case Opcode.Character when node.Match && node.Step != 0:
                    case Opcode.CharacterIgn when node.Match && node.Step != 0 && node.Encoding != CaseEncoding.Locale:
                    case Opcode.String:
                    case Opcode.StringIgn or Opcode.StringFld when node.Encoding != CaseEncoding.Locale:
                        if (Array.Exists(node.Values, static c => c is >= 0xD800 and <= 0xDFFF))
                        {
                            return false;
                        }

                        heads.Add(node);
                        return true;
                    default:
                        return false;
                }
            }
        }
    }

    /// <summary>The most branches <see cref="FindLeadingStrings"/> walks before giving up.</summary>
    private const int _maxLeadingBranches = 1000;

    /// <summary>
    /// Finds <see cref="LeadingRepeat"/>. Upstream takes the start node when it is a
    /// <c>GREEDY_REPEAT_ONE</c> or <c>LAZY_REPEAT_ONE</c> (<c>upstream/src/_regex.c</c>:15719).
    /// sync-divergence: this port also looks through the capture groups that open in front of a
    /// greedy one, so <c>(\d+)x</c> skips the way <c>\d+x</c> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The skip is sound for a bare repeat because an attempt that starts inside the run the
    /// failed attempt's repeat matched can only reach end positions that attempt already tried,
    /// and runs the rest of the pattern from each of them the same way. A capture group round the
    /// repeat changes only what the group holds, so the argument still holds unless something
    /// reads the group's text. So the look-through needs all of these:
    /// </para>
    /// <list type="bullet">
    /// <item>The repeat is greedy. The argument needs the failed attempt to have tried every end
    /// position in the run, and a lazy repeat that meets <c>(*SKIP)</c> or <c>(*PRUNE)</c>, or a
    /// partial match that runs out, stops short: regex 2026.9.10 finds <c>(?i)(a*?)(*SKIP)b</c>
    /// over <c>aab</c> at (2, 3), and a skip would miss it. Upstream skips for a bare lazy repeat
    /// all the same, and this keeps that.</item>
    /// <item>No group is referenced: no backreference and no <c>(?(n)...)</c>, whose answer
    /// depends on what the group holds or on where it started.</item>
    /// <item>No group call. A call re-enters a group body, and keeping it out keeps the argument
    /// above short.</item>
    /// <item>Not fuzzy. This is the slice's own guard (<c>MaxErrors == 0</c>), kept as caution: a
    /// differential sweep with it removed changed no answer (S60b sittings notes).</item>
    /// <item>Not reversed. A reverse group opens with <c>END_GROUP</c>, so there is nothing to look
    /// through, and saying so keeps this forward-only.</item>
    /// </list>
    /// <para>
    /// regex 2026.9.10 is quadratic on <c>(?s)(.*)a\d</c> over a run of <c>a</c> (1,239 ms at
    /// 8,000 characters, 2026-09-23) and linear without the group. Pinned by
    /// <c>OptimiserTrapsTests.A_leading_repeat_inside_a_capture_group_is_skipped_like_a_bare_one</c>.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The compiled pattern.</param>
    /// <returns>The leading repeat, or <see langword="null"/>.</returns>
    private static Node? FindLeadingRepeat(PatternObject pattern)
    {
        Node node = pattern.StartNode!;
        if (node.Op is Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne)
        {
            return node;
        }

        bool mayLookThroughGroups =
            !pattern.IsFuzzy
            && (pattern.Flags & RegexFlags.Reverse) == 0
            && pattern.CallRefInfoList.Count == 0
            && !pattern.GroupInfoList.Exists(static group => group.Referenced);

        while (mayLookThroughGroups && node.Op == Opcode.StartGroup)
        {
            node = node.Next1.Node!;
        }

        return node.Op == Opcode.GreedyRepeatOne ? node : null;
    }

    /// <summary>
    /// The largest number of deletions any fuzzy section of <paramref name="pattern"/> permits: its
    /// deletion limit, error limit and cost limit over a deletion's cost, whichever is least.
    /// </summary>
    /// <param name="pattern">A compiled pattern.</param>
    /// <returns>The ceiling for <see cref="ExactDeletionCeiling"/>; 0 with no fuzzy section.</returns>
    private static long DeletionCeiling(PatternObject pattern)
    {
        long ceiling = 0;
        foreach (Node node in pattern.NodeList)
        {
            if (node.Op != Opcode.Fuzzy)
            {
                continue;
            }

            uint[] values = node.Values;
            long cap = Math.Min(values[FuzzyValue.MaxBase + FuzzyValue.Del], values[FuzzyValue.MaxErr]);
            long unitCost = values[FuzzyValue.CostBase + FuzzyValue.Del];
            if (unitCost > 0)
            {
                cap = Math.Min(cap, values[FuzzyValue.MaxCost] / unitCost);
            }

            ceiling = Math.Max(ceiling, cap);
        }

        return ceiling;
    }

    /// <summary>NOT UPSTREAM'S, D11 chunk 8: see <see cref="Node.BodyInfo"/>.</summary>
    /// <remarks>
    /// A walk from each test's first body node, over every exit, stopped at the test's end node. A
    /// group call's target is not followed: the walk goes on at its return node, and a run-out
    /// inside a call the body opened is taken at the call node, or answered at once where the body
    /// feeds a decision (<c>Matcher.RunOutInALookaroundBody</c>).
    /// </remarks>
    /// <param name="pattern">The pattern, with its nodes numbered.</param>
    private static void SetLookaroundBodies(PatternObject pattern)
    {
        foreach (Node test in pattern.NodeList)
        {
            if (test.Op is not (Opcode.Lookaround or Opcode.Conditional) || test.LookaroundEnd is null)
            {
                continue;
            }

            var nodes = new HashSet<int>();
            var groups = new List<(int Private, int Public)>();
            var todo = new Stack<Node>();
            bool hasVerbOrCall = false;
            todo.Push(test.Next1.Node!);
            while (todo.Count > 0)
            {
                Node n = todo.Pop();
                if (ReferenceEquals(n, test.LookaroundEnd) || !nodes.Add(n.Index))
                {
                    continue;
                }

                hasVerbOrCall |= n.Op is Opcode.Prune or Opcode.Skip or Opcode.GroupCall;

                if (n.Op is Opcode.StartGroup or Opcode.EndGroup && n.Values[2] != 0)
                {
                    (int, int) g = ((int)n.Values[0], (int)n.Values[1]);
                    if (!groups.Contains(g))
                    {
                        groups.Add(g);
                    }
                }

                foreach (Node? next in (ReadOnlySpan<Node?>)[n.Next1.Node, n.Next2.Node, n.TrueNode])
                {
                    if (next is not null)
                    {
                        todo.Push(next);
                    }
                }
            }

            int[] sorted = [.. nodes];
            Array.Sort(sorted);
            test.BodyInfo = new Node.Body(sorted, [.. groups], hasVerbOrCall);

            // NOT UPSTREAM (D104): see Node.ContinuationKeyRepeat. The repeats' keys are set by
            // then (TailGuardLiveness.FindTailKeys, in the optimiser).
            if (test.Op == Opcode.Lookaround && test.Match)
            {
                foreach (int i in sorted)
                {
                    Node n = pattern.NodeList[i];
                    if (
                        n.Op
                            is Opcode.GreedyRepeat
                                or Opcode.LazyRepeat
                                or Opcode.GreedyRepeatOne
                                or Opcode.LazyRepeatOne
                        && pattern.RepeatInfoAt((int)n.Values[0]).CutBodyKey is { HasUnknownInside: false }
                    )
                    {
                        test.ContinuationKeyRepeat = (int)n.Values[0];
                        pattern.KeysContinuations = true;
                        break;
                    }
                }
            }
        }

        // A call is stepped over (D97): its return restores every capture, so it adds no group.
        if (pattern.ReadsCapturesWhileMatching)
        {
            SetRunOutGroups(pattern);
        }

        if (pattern.IsFuzzy && !pattern.HasGroupCalls)
        {
            SetEnclosingSections(pattern);
        }
    }

    /// <summary>NOT UPSTREAM'S, D11 chunk 8: see <see cref="Node.EnclosingSection"/>.</summary>
    /// <remarks>
    /// A walk from the start node that carries the section open at each node: FUZZY opens its own
    /// for its body, and END_FUZZY goes back to the one FUZZY was reached in. Sections nest, so each
    /// node is reached in one section whichever path reaches it; a repeat's end leads back to its
    /// body in the same section.
    /// </remarks>
    /// <param name="pattern">The pattern, with its bodies set.</param>
    private static void SetEnclosingSections(PatternObject pattern)
    {
        var fuzzyInABody = false;
        foreach (Node test in pattern.NodeList)
        {
            if (test.BodyInfo is { } body)
            {
                foreach (int i in body.Nodes)
                {
                    fuzzyInABody |= pattern.NodeList[i].Op == Opcode.Fuzzy;
                }
            }
        }

        if (!fuzzyInABody || pattern.StartNode is not { } start)
        {
            return;
        }

        pattern.HasFuzzyInABody = true;
        var outerOf = new Dictionary<int, Node?>();
        var seen = new HashSet<int>();
        var todo = new Stack<(Node Node, Node? Section)>();
        todo.Push((start, null));
        while (todo.Count > 0)
        {
            (Node n, Node? section) = todo.Pop();
            if (!seen.Add(n.Index))
            {
                continue;
            }

            Node? after = section;
            if (n.Op is Opcode.Lookaround or Opcode.Conditional)
            {
                n.EnclosingSection = section;
            }
            else if (n.Op == Opcode.Fuzzy)
            {
                outerOf[n.Index] = section;
                after = n;
            }
            else if (n.Op == Opcode.EndFuzzy && section is not null)
            {
                after = outerOf.GetValueOrDefault(section.Index);
            }

            foreach (Node? next in (ReadOnlySpan<Node?>)[n.Next1.Node, n.Next2.Node, n.TrueNode])
            {
                if (next is not null)
                {
                    todo.Push((next, after));
                }
            }
        }
    }

    /// <summary>NOT UPSTREAM'S, D11 chunk 8: see <see cref="Node.RunOutGroups"/>.</summary>
    /// <remarks>
    /// Per body, the groups each node reaches are its own and those of the nodes after it, which a
    /// few passes over the body settle (a repeat's end leads back to its body, so the graph has
    /// cycles). The bodies are taken largest first, so a node inside a nested body keeps the
    /// innermost body's set, which is the one a run-out at it reads (assert A10).
    /// </remarks>
    /// <param name="pattern">The pattern, with its bodies set.</param>
    private static void SetRunOutGroups(PatternObject pattern)
    {
        var byIndex = new Dictionary<int, Node>();
        foreach (Node n in pattern.NodeList)
        {
            byIndex[n.Index] = n;
        }

        IEnumerable<Node> tests = pattern
            .NodeList.Where(static t => t.BodyInfo is not null)
            .OrderByDescending(static t => t.BodyInfo!.Nodes.Length);
        foreach (Node test in tests)
        {
            int[] body = test.BodyInfo!.Nodes;
            var reach = new Dictionary<int, HashSet<(int Private, int Public)>>();
            foreach (int i in body)
            {
                Node n = byIndex[i];
                reach[i] =
                    n.Op is Opcode.StartGroup or Opcode.EndGroup && n.Values[2] != 0
                        ? [((int)n.Values[0], (int)n.Values[1])]
                        : [];
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int k = body.Length - 1; k >= 0; k--)
                {
                    Node n = byIndex[body[k]];
                    foreach (Node? next in (ReadOnlySpan<Node?>)[n.Next1.Node, n.Next2.Node, n.TrueNode])
                    {
                        if (next is not null && reach.TryGetValue(next.Index, out var after))
                        {
                            int before = reach[body[k]].Count;
                            reach[body[k]].UnionWith(after);
                            changed |= reach[body[k]].Count != before;
                        }
                    }
                }
            }

            foreach (int i in body)
            {
                byIndex[i].RunOutGroups = [.. reach[i]];
            }
        }
    }

    /// <summary>
    /// Sets <see cref="Node.FuzzyRunLength"/> and <see cref="FuzzyRunExits"/> for every node of a
    /// compiled pattern.
    /// </summary>
    /// <remarks>
    /// Each node is visited once: a run is a chain by <c>next_1</c>, so the length at a node is its
    /// own width plus the length at the node after it, which is set first. A run cannot loop back
    /// on itself, because a repeat's body reaches its repeat's end node, which ends the run; the
    /// visited marks stop the walk even if it did.
    /// </remarks>
    /// <param name="pattern">The compiled pattern, with its nodes numbered.</param>
    private static void SetFuzzyRunLengths(PatternObject pattern)
    {
        var visited = new bool[pattern.NodeList.Count];
        var chain = new List<Node>();

        foreach (Node start in pattern.NodeList)
        {
            // Walk forward to the end of the run, then set the lengths on the way back.
            chain.Clear();
            Node? node = start;
            while (
                node is not null
                && node.Index < visited.Length
                && ReferenceEquals(pattern.NodeList[node.Index], node)
                && !visited[node.Index]
                && IsFuzzyRunItem(node)
            )
            {
                visited[node.Index] = true;
                chain.Add(node);
                node = node.Next1.Node;
            }

            if (chain.Count == 0)
            {
                continue;
            }

            int length = 0;
            Node? exit = SkipTextlessNodes(pattern, node);
            if (node is not null && IsFuzzyRunItem(node))
            {
                // A node outside the list, or one in this chain, has no exit recorded yet.
                length = node.FuzzyRunLength;
                exit =
                    node.Index < visited.Length && ReferenceEquals(pattern.NodeList[node.Index], node)
                        ? pattern.FuzzyRunExits?[node.Index]
                        : null;
            }

            pattern.FuzzyRunExits ??= new Node?[pattern.NodeList.Count];
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                length += RunItemWidth(chain[i]);
                chain[i].FuzzyRunLength = length;
                pattern.FuzzyRunExits[chain[i].Index] = exit;
            }
        }
    }

    /// <summary>
    /// Whether a node is a fuzzy item that consumes exactly one subject character per pattern
    /// character when it matches: a one-character item, or a case-sensitive or simply case-folded
    /// string, either way round.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> if the node can be part of a fuzzy run.</returns>
    private static bool IsFuzzyRunItem(Node node) =>
        (node.Status & NodeStatus.Fuzzy) != 0
        && (
            node.Op is Opcode.String or Opcode.StringIgn or Opcode.StringRev or Opcode.StringIgnRev
            || NodeQueries.MatchesOneCharacter(node)
        );

    /// <summary>How many characters a fuzzy run item consumes when it matches exactly.</summary>
    /// <param name="node">A node <see cref="IsFuzzyRunItem"/> accepts.</param>
    /// <returns>The string's length, or 1.</returns>
    private static int RunItemWidth(Node node) =>
        node.Op is Opcode.String or Opcode.StringIgn or Opcode.StringRev or Opcode.StringIgnRev
            ? node.Values.Length
            : 1;

    /// <summary>
    /// The first node from <paramref name="node"/> on that is not an <c>END_FUZZY</c>, or a
    /// <c>START_GROUP</c> or <c>END_GROUP</c> of a group nothing tests: nodes that always succeed,
    /// read no text and leave nothing the rest of the match reads.
    /// </summary>
    /// <remarks>
    /// The boundary of a group a backreference or conditional tests (<see cref="GroupInfo.Referenced"/>)
    /// is where the walk stops. <c>Matcher.ExactDeletionMayMatch</c> uses the node found here to ask
    /// whether the character an exact item read can be read after its run, which rests on
    /// exchanging a trailing insertion at a section's end for the item's own match; across a tested
    /// group's boundary that exchange moves the group's span and a later backreference can then
    /// fail. <c>(?:(a)){e&lt;=2}b\1</c> over 'ab' matches only by deleting the fuzzy 'a', so group 1
    /// is empty, and inserting the 'a' after the group (found by the blind review of af59be7,
    /// 2026-09-26). A group nothing tests changes only what a match reports, and the exchanged
    /// match, which would be reported, was found first.
    /// </remarks>
    /// <param name="pattern">The pattern, for which groups are tested.</param>
    /// <param name="node">The node, or <see langword="null"/>.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    internal static Node? SkipTextlessNodes(PatternObject pattern, Node? node)
    {
        // A bound only against a malformed cycle; a real chain of these is short.
        for (int i = 0; i < 64 && node is not null; i++)
        {
            bool textless =
                node.Op == Opcode.EndFuzzy
                || (
                    node.Op is Opcode.StartGroup or Opcode.EndGroup
                    && !pattern.GroupInfoAt((int)node.Values[0]).Referenced
                );
            if (!textless)
            {
                break;
            }

            node = node.Next1.Node;
        }

        return node;
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
