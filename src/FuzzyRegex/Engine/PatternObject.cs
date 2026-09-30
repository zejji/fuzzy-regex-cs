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
    /// kept, and the body is never tried there again in the same attempt. That turns
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
    /// constructs (<see cref="PatternObject.KeepInSubmatch"/>). A partial match does not use it
    /// either (<c>MatchState.KeepsFailureMemo</c>). Where it does apply, a conditional's restore
    /// leaves the repeat's guards in place rather than putting back the saved ones (D35,
    /// <c>Matcher.PushRepeats</c>).
    /// </para>
    /// <para>
    /// Four conditions have a witness in <c>FailureMemoTests</c>, an answer that changes when the
    /// condition is deleted: the maximum, backreferences, group-exists conditionals and a
    /// <c>\K</c> inside a construct. The rest
    /// have none. Differential grids of 7.6 million rows found no answer that changes when any one
    /// of them is deleted (2026-09-26), and for most there is a reason. Guards are reset at every
    /// start position, so a verb that ends the attempt leaves nothing behind for a later path to
    /// misread. An atomic group, a lookaround and a conditional's test throw away the body's
    /// backtrack entries once the body reaches its end, so a failure recorded inside one of them
    /// only ever means "cannot reach the end of the construct from here", a fact about the
    /// position; and a group call clears the guard lists on the way in and restores them on the
    /// way out. A repeat inside a bounded
    /// repeat has no active guards at all (<c>AddRepeatGuards</c> does not walk a bounded body). A
    /// partial match returns at the first path that reaches the end of the text, POSIX matching
    /// replaces its best match only with a longer one, and the guards are not consulted under
    /// fuzzy matching. They stay as exclusions anyway: each is a place where the rest of the match
    /// can read more than the position, and none of those arguments has been proved in general.
    /// <c>docs/plan/2026-09-26-backtrack-memoisation-design.md</c> gives the full reasoning.
    /// </para>
    /// </remarks>
    internal bool FailureMemo;
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
    /// Oracle-only: a fuzzy section a <c>(*PRUNE)</c> or <c>(*SKIP)</c> cut through stays the open
    /// one, as upstream leaves <c>fuzzy_node</c> (D44, ledger entry 61). Set only by
    /// <c>OracleComparer</c>'s ablation; never by the library. See
    /// <c>MatchState.PushSubAttemptFuzzyState</c>.
    /// </summary>
    internal bool KeepSectionOpenAfterAVerb;

    /// <summary>
    /// Oracle-only: the running error total and cost keep the errors of a fuzzy section an atomic
    /// group, lookaround, condition or verb threw away, as upstream's <c>total_errors</c> does (D45,
    /// ledger entry 32). Set only by <c>OracleComparer</c>'s ablation; never by the library. See
    /// <c>MatchState.PushSubAttemptFuzzyState</c>.
    /// </summary>
    internal bool KeepDiscardedTotals;

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
        for (int i = 0; i < self.NodeList.Count; i++)
        {
            self.NodeList[i].Index = i;

            // NOT UPSTREAM'S (S60): note a '(*SKIP)' while we are walking the list anyway, so the
            // prefilter knows not to move the first attempt. See HasSkipVerb.
            if (self.NodeList[i].Op == Opcode.Skip)
            {
                self.HasSkipVerb = true;
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

        self.NarrowExactDeletions = !noNarrowing && !self.HasSkipVerb;
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

        return self;
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

            List<uint> values = node.Values;
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
        node.Op is Opcode.String or Opcode.StringIgn or Opcode.StringRev or Opcode.StringIgnRev ? node.Values.Count : 1;

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
