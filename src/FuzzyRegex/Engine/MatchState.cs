using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// One stretch of the subject a group captured. Port of <c>RE_GroupSpan</c>
/// (<c>upstream/src/_regex.c</c> lines 277-280).
/// </summary>
/// <remarks>
/// A readonly struct because upstream copies it by value everywhere: onto the capture list, into a
/// <c>RE_GroupStateData</c> and out again. Positions are UTF-16 code unit indices, like every other
/// position in the engine, and the <c>(start, end)</c> pair keeps upstream's names - the
/// <c>(Index, Length)</c> the public API reports is converted in <c>Capture</c>'s accessors and
/// nowhere else (DECISIONS 2026-08-31).
/// </remarks>
/// <param name="Start">Where the capture starts.</param>
/// <param name="End">One past where it ends.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GroupSpan(int Start, int End);

/// <summary>
/// One error a fuzzy match used. Port of <c>RE_FuzzyChange</c>
/// (<c>upstream/src/_regex.c</c> lines 392-395).
/// </summary>
/// <remarks>
/// <see cref="Pos"/> is a UTF-16 code unit index, like every other position in the engine; upstream's
/// is a codepoint index. For a substitution or an insertion it is a real position in the subject; for
/// a deletion it is where the missing character would have gone, which <c>match_fuzzy_changes</c>
/// (<c>:20555-20558</c>) then shifts by one per earlier deletion, so the reported value can be past the end
/// of the match.
/// </remarks>
/// <param name="Type">Which error: <see cref="FuzzyValue.Sub"/>, <c>Ins</c> or <c>Del</c>.</param>
/// <param name="Pos">Where it was used.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FuzzyChange(byte Type, int Pos);

/// <summary>
/// NOT UPSTREAM'S: what of a group that a conditional or backreference reads the call guard
/// compares, to tell whether a call would repeat an open one (<see cref="MatchState.ActiveCalls"/>).
/// </summary>
/// <remarks>
/// <para>
/// The guard compares exactly what a reader can see (the capture-dependent recursion design,
/// <c>docs/plan/2026-09-28-capture-dependent-recursion-design.md</c> section 4d). A conditional
/// reads only whether its group is set. A backreference compares the group's text with the text
/// ahead, so two captures with equal text behave the same for every backreference, wherever they
/// sit. Comparing the spans instead walked a new call chain for every position at which an empty
/// group had been set, 2^n of them on <c>(?:()|a)(?R)|(?(1)x)</c> over <c>a...ay</c>. Exact text is
/// finer than a case-insensitive or fuzzy comparison needs, so it can keep apart two states that
/// could have been merged, but never merges two that differ.
/// </para>
/// <para>
/// One more thing reads a group's span: the capture-change counter, which a capture bumps only
/// when the new span differs from the current one, and which a repeat's empty-iteration test and
/// an optional pass read. So a group captured inside a repeat body, and every read group of a
/// fuzzy pattern, is compared by span (<see cref="Span"/>).
/// <c>(?:(?:(?:(?(g)|z)b)?.|(?:a(?(g)b)|a(?:(?&lt;g&gt;)|a)*)))|(?&lt;g&gt;)(?R)(?:(?R)|\1)|(?:(?P=g)(?R)|(?R))</c>
/// fullmatch over <c>aax</c> gave group 1 two captures more than upstream's <c>[0,0][1,0]</c> when
/// that group was compared by text (the design grid, 2026-09-29).
/// </para>
/// </remarks>
internal enum CallRead : byte
{
    /// <summary>Only whether it is set: only conditionals read it.</summary>
    SetOrUnset,

    /// <summary>Its text: a backreference reads it.</summary>
    Text,

    /// <summary>Its span: it is captured inside a repeat body, or the pattern is fuzzy.</summary>
    Span,
}

/// <summary>
/// NOT UPSTREAM'S: numbers the distinct texts that groups a backreference reads have captured, so
/// the failed-call memo can key a group on its text with one number (<c>Matcher.ReadValue</c>).
/// Two spans get the same id exactly when their texts are equal, compared ordinally.
/// </summary>
/// <remarks>
/// The ids hold for one subject: <see cref="MatchState.Init"/> calls <see cref="Reset"/>, and the
/// failed-call memo, the one set of keys that holds an id, is emptied there too. SHORTCUT: the table
/// only grows while one subject is matched. It gains an entry only for a new text seen while the
/// memo builds keys, so it is no larger than the calls made; a cap with a fallback to keying on
/// spans would bound it, if a workload ever shows it matters.
/// </remarks>
internal sealed class CaptureTextIds : IEqualityComparer<(int Start, int Length)>
{
    private readonly Dictionary<(int Start, int Length), long> _ids;
    private ReadOnlyMemory<char> _text;

    /// <summary>Initializes a new instance of the <see cref="CaptureTextIds"/> class.</summary>
    internal CaptureTextIds()
    {
        _ids = new Dictionary<(int Start, int Length), long>(this);
    }

    /// <summary>How many distinct texts have an id.</summary>
    internal int Count => _ids.Count;

    /// <summary>Forgets every id and starts numbering the texts of <paramref name="text"/>.</summary>
    /// <param name="text">The subject.</param>
    internal void Reset(ReadOnlyMemory<char> text)
    {
        _ids.Clear();
        _text = text;
    }

    /// <summary>The id of the text at <paramref name="start"/>, numbering it if it is new.</summary>
    /// <param name="start">Where the text starts in the subject.</param>
    /// <param name="length">Its length.</param>
    /// <returns>The id, at least 1, so it is never the value of an unset group (-1) or of a set group only conditionals read (0).</returns>
    internal long IdOf(int start, int length)
    {
        ref long id = ref CollectionsMarshal.GetValueRefOrAddDefault(_ids, (start, length), out bool exists);
        if (!exists)
        {
            id = _ids.Count;
        }

        return id;
    }

    /// <inheritdoc/>
    public bool Equals((int Start, int Length) x, (int Start, int Length) y) =>
        x.Length == y.Length && _text.Span.Slice(x.Start, x.Length).SequenceEqual(_text.Span.Slice(y.Start, y.Length));

    /// <inheritdoc/>
    public int GetHashCode((int Start, int Length) obj) =>
        string.GetHashCode(_text.Span.Slice(obj.Start, obj.Length), StringComparison.Ordinal);
}

/// <summary>
/// Everything one capture group has captured during this match. Port of <c>RE_GroupData</c>
/// (<c>upstream/src/_regex.c</c> lines 329-334).
/// </summary>
/// <remarks>
/// Upstream keeps <b>every</b> capture a group made, not just the last, which is the headline
/// mrab-regex feature the built-in <c>Regex</c> only offers inside a repeated construct. So
/// <see cref="Captures"/> is the list and <see cref="Current"/> indexes the one that
/// <c>Match.Groups[n]</c> reports, or is <c>-1</c> when the group did not take part in the match.
/// Upstream's <c>capacity</c> is <see cref="Captures"/>'s <c>Length</c>.
/// </remarks>
internal sealed class GroupData
{
    /// <summary>Upstream <c>captures</c>: the spans, oldest first, of which <see cref="Count"/> are live.</summary>
    internal GroupSpan[] Captures = [];

    /// <summary>Upstream <c>count</c>: how many of <see cref="Captures"/> this match has written.</summary>
    internal int Count;

    /// <summary>
    /// Upstream <c>current</c>: which capture <c>Match.Groups[n]</c> reports, or <c>-1</c> for a
    /// group that did not take part in the match.
    /// </summary>
    internal int Current = -1;

    /// <summary>
    /// The half of <c>copy_groups</c> (<c>upstream/src/_regex.c</c> line 20621) that is not
    /// arithmetic on one allocated block: <see cref="Match"/> outlives the state, so it takes a copy
    /// of the spans rather than a pointer into the state's.
    /// </summary>
    /// <returns>A copy holding exactly the live spans.</returns>
    internal GroupData Copy() =>
        new()
        {
            Captures = Captures.AsSpan(0, Count).ToArray(),
            Count = Count,
            Current = Current,
        };

    /// <summary>
    /// Upstream <c>copy_groups</c> (<c>upstream/src/_regex.c</c> line 20621), less its single-block
    /// allocation arithmetic, which a garbage-collected heap does not need.
    /// </summary>
    /// <param name="groups">The state's groups.</param>
    /// <param name="groupCount">How many to copy - upstream's <c>public_group_count</c>.</param>
    /// <returns>The copies.</returns>
    internal static GroupData[] CopyGroups(GroupData[] groups, int groupCount)
    {
        if (groupCount == 0)
        {
            // A pattern without groups gives every match the one shared empty array: 24 B a match.
            return [];
        }

        var copies = new GroupData[groupCount];
        for (int g = 0; g < groupCount; g++)
        {
            copies[g] = groups[g].Copy();
        }

        return copies;
    }

    /// <summary>Where a group's spans start in a packed array, relative to its header.</summary>
    internal const int PackedOffset = 0;

    /// <summary>How many spans the group has, relative to its header.</summary>
    internal const int PackedCount = 1;

    /// <summary>Which span is current, or <c>-1</c>, relative to its header.</summary>
    internal const int PackedCurrent = 2;

    /// <summary>How many numbers each group's header takes.</summary>
    private const int _packedHeaderSize = 3;

    /// <summary>Where group <paramref name="number"/>'s header sits in a packed array.</summary>
    /// <param name="number">The public group number, one-based.</param>
    /// <returns>The index of its <see cref="PackedOffset"/>.</returns>
    internal static int PackedHeader(int number) => (number - 1) * _packedHeaderSize;

    /// <summary>
    /// What <see cref="Match"/> keeps of the groups: <see cref="CopyGroups"/> packed into one
    /// array, which is the shape of upstream's own copy: <c>copy_groups</c>
    /// (<c>upstream/src/_regex.c</c> line 20641) allocates "the storage for the group info in a
    /// single block", the group headers first and every span after them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The array opens with a header of three numbers per group, at <see cref="PackedHeader"/>:
    /// where the group's spans start in this same array, how many there are, and which one is
    /// current. The spans follow as start and end pairs, oldest first, group by group.
    /// </para>
    /// <para>
    /// One array rather than a <see cref="GroupData"/> and a span array per group, because a
    /// match pays for these whether or not anyone reads a group: three groups with one capture each
    /// cost 240 bytes as seven objects and 88 as this one (C2, the <c>ParseLogLines</c> row).
    /// </para>
    /// </remarks>
    /// <param name="groups">The state's groups.</param>
    /// <param name="groupCount">How many to pack - upstream's <c>public_group_count</c>.</param>
    /// <returns>The packed groups, or the shared empty array for a pattern without groups.</returns>
    internal static int[] PackSpans(GroupData[] groups, int groupCount)
    {
        if (groupCount == 0)
        {
            return [];
        }

        int length = groupCount * _packedHeaderSize;
        for (int g = 0; g < groupCount; g++)
        {
            length += 2 * groups[g].Count;
        }

        int[] packed = new int[length];
        int at = groupCount * _packedHeaderSize;
        for (int g = 0; g < groupCount; g++)
        {
            GroupData group = groups[g];

            // The reader indexes the current span inside this group's own pairs, so an index past
            // them would read the next group's span instead of failing as the old copy's array did.
            Debug.Assert(group.Current < group.Count, "a group's current capture is one of its live captures");

            int header = PackedHeader(g + 1);
            packed[header + PackedOffset] = at;
            packed[header + PackedCount] = group.Count;
            packed[header + PackedCurrent] = group.Current;

            foreach (GroupSpan span in group.Captures.AsSpan(0, group.Count))
            {
                packed[at++] = span.Start;
                packed[at++] = span.End;
            }
        }

        return packed;
    }
}

/// <summary>
/// The state one matching operation runs in. Port of <c>RE_State</c>
/// (<c>upstream/src/_regex.c</c> lines 459-529), built by <see cref="Create"/>, which is the port of
/// <c>state_init</c> (<c>:18598</c>) and <c>state_init_2</c> (<c>:18275</c>).
/// </summary>
/// <remarks>
/// <para>
/// A class with internal fields rather than a struct, exactly as S15 ported <c>RE_Node</c>: every
/// helper below mutates it, and a struct would put <c>ref</c> on every signature to say what a
/// reference type says for free. Nothing here is public, so CA1051, S1104 and MA0008 - the rules the
/// <c>.editorconfig</c> note was written for - do not fire (measured, S16). See DECISIONS.
/// </para>
/// <para>
/// Not ported: the <c>view</c>/<c>charsize</c>/<c>is_unicode</c>/<c>should_release</c> buffer fields
/// and the <c>char_at</c>/<c>set_char_at</c>/<c>point_to</c> function pointers they select
/// (<c>get_string</c>, <c>:18217</c>), because this port matches <see cref="string"/> only; the GIL
/// and lock machinery (<c>acquire_state_lock</c>, <c>:20847</c>), because the state is per call and
/// the pattern is immutable; and <c>check_compatible</c> (<c>:18573</c>), which exists to reject a
/// <c>str</c> pattern against a <c>bytes</c> subject.
/// </para>
/// <para>
/// <b>Positions are UTF-16 code units</b> where upstream's are codepoints, so a codepoint step is
/// one or two of them - see <see cref="CharAt"/> and <see cref="NextPos"/>. Internal spans are
/// <c>(start, end)</c>, keeping upstream's names; the <c>(Index, Length)</c> the public API reports
/// is converted in <c>Match</c>/<c>Group</c>'s accessors and nowhere else (DECISIONS 2026-08-31).
/// </para>
/// </remarks>
internal sealed class MatchState : IDisposable
{
    /// <summary>Upstream <c>RE_PARTIAL_NONE</c> (<c>upstream/src/_regex.c</c> line 93).</summary>
    internal const int PartialNone = -1;

    /// <summary>Upstream <c>RE_PARTIAL_LEFT</c> (line 94).</summary>
    internal const int PartialLeft = 0;

    /// <summary>Upstream <c>RE_PARTIAL_RIGHT</c> (line 95).</summary>
    internal const int PartialRight = 1;

    /// <summary>Upstream <c>RE_NO_TIMEOUT</c> (line 85).</summary>
    internal const long NoTimeout = -1;

    /// <summary>Upstream <c>pattern</c>.</summary>
    internal readonly PatternObject Pattern;

    /// <summary>
    /// The cache that built this state and takes it back on <see cref="Dispose"/>, or <see langword="null"/>
    /// for a state <see cref="Create"/> built. Has no counterpart upstream, whose <c>state_fini</c>
    /// always hands its storage back to the pattern.
    /// </summary>
    internal readonly MatchStateCache? Cache;

    /// <summary>
    /// How many times this state has been disposed. A <see cref="ValueMatchEnumerator"/> notes it
    /// when it rents the state and disposes only while it is unchanged, because a copy of that
    /// struct shares the state and a second hand-back would give it to two callers at once. No
    /// counterpart upstream.
    /// </summary>
    internal int Lease;

    /// <summary>
    /// Upstream <c>text</c> and <c>string</c>, which are one object here. A memory rather than a
    /// <see cref="string"/> so that the <see cref="ReadOnlyMemory{T}"/> overloads of
    /// <c>IsMatch</c> and <c>Count</c> match a caller's buffer without copying it (S61, option (a)
    /// of <c>docs/plan/2026-09-19-span-threading-decision.md</c>).
    /// </summary>
    internal ReadOnlyMemory<char> Text;

    /// <summary>
    /// NOT UPSTREAM'S: the ids of the texts the failed-call memo has keyed a backreferenced group on.
    /// See <see cref="CaptureTextIds"/>.
    /// </summary>
    internal readonly CaptureTextIds CaptureTexts = new();

    /// <summary>Upstream <c>text_length</c>.</summary>
    internal int TextLength;

    /// <summary>Upstream <c>slice_start</c>: where the searched slice starts.</summary>
    internal int SliceStart;

    /// <summary>Upstream <c>slice_end</c>.</summary>
    internal int SliceEnd;

    /// <summary>
    /// The slice as <see cref="Create"/> set it, which <see cref="Matcher.DoMatch"/> puts back at
    /// the start of every match. Has no counterpart upstream.
    /// </summary>
    /// <remarks>
    /// Added by S40a. <c>(*SKIP)</c> moves <see cref="SliceStart"/>/<see cref="SliceEnd"/>
    /// mid-attempt and upstream restores them nowhere, so one scanner state carries the moved slice
    /// from one match into the next - upstream's own bug, ledger entry 5, whose proposed fix is this
    /// reset. The reasoning and the measurements are at the reset itself; this pair is only the
    /// remembered value.
    /// </remarks>
    internal int InitialSliceStart;

    /// <inheritdoc cref="InitialSliceStart" />
    internal int InitialSliceEnd;

    /// <summary>
    /// Upstream <c>text_start</c>. Always 0: upstream documents the bounds as an open start and a
    /// closed end, so <c>pos</c> moves <see cref="SliceStart"/> but not this, which is why
    /// <c>^</c> does not match at <c>pos</c>.
    /// </summary>
    internal int TextStart;

    /// <summary>Upstream <c>text_end</c>, which <c>endpos</c> does move.</summary>
    internal int TextEnd;

    /// <summary>Upstream <c>search_anchor</c>: where this matching operation was asked to start.</summary>
    internal int SearchAnchor;

    /// <summary>
    /// NOT UPSTREAM'S, D79: where this matching operation began, which is what <c>\G</c> reads. It is
    /// upstream's <c>search_anchor</c> as the operation's FIRST <c>init_match</c> sets it
    /// (<c>_regex.c:3410</c>); the BESTMATCH and ENHANCEMATCH re-runs move <see cref="SearchAnchor"/>
    /// to each candidate's start, and the insertion rule keeps reading that.
    /// </summary>
    internal int OperationStart;

    /// <summary>Upstream <c>match_pos</c>: where the match being attempted starts.</summary>
    internal int MatchPos;

    /// <summary>Upstream <c>text_pos</c>: where matching has got to.</summary>
    internal int TextPos;

    /// <summary>Upstream <c>final_newline</c>: the index of a newline ending the string, or -1.</summary>
    internal int FinalNewline;

    /// <summary>Upstream <c>final_line_sep</c>.</summary>
    internal int FinalLineSep;

    /// <summary>
    /// Upstream <c>groups</c>: one entry per group, indexed by group number minus one, and
    /// <c>true_group_count</c> long so a private group number reaches its own entry.
    /// </summary>
    internal readonly GroupData[] Groups;

    /// <summary>
    /// Upstream <c>repeats</c>: one entry per repeat in the pattern, indexed by the repeat index
    /// that a <c>GREEDY_REPEAT</c>-family node carries in <c>values[0]</c>.
    /// </summary>
    internal readonly RepeatData[] Repeats;

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): one slot per alternation with an alternative
    /// written empty after one that is not, indexed by the value its <c>END_OPTIONAL_PASS</c> nodes
    /// carry: where the pass through the alternative now being matched began. Written by
    /// <c>Matcher.OpenOptionalPass</c>, read by <c>Matcher.OptionalPassAdmitted</c>.
    /// </summary>
    internal readonly OptionalPassStart[] OptionalPasses;

    /// <summary>Upstream <c>sstack</c>: the structure stack.</summary>
    internal readonly ByteStack Sstack;

    /// <summary>Upstream <c>bstack</c>: the backtracking stack.</summary>
    internal readonly ByteStack Bstack;

    /// <summary>Upstream <c>pstack</c>: the pruning stack.</summary>
    internal readonly ByteStack Pstack;

    /// <summary>
    /// NOT UPSTREAM'S, D11: the w-marks of a partial call's second pass, sorted by position, each
    /// live while its entry is on <see cref="Bstack"/> (design section 4.2).
    /// </summary>
    /// <remarks>
    /// Empty outside the second pass (A6), and at the start of every attempt (A5). The list is made
    /// with the state and has no storage until the first mark, so a call without partial allocates
    /// nothing for it; a reused state keeps the storage, as the stacks keep theirs. Every truncation
    /// of <see cref="Bstack"/> other than a pop goes through <see cref="SetBstackCount"/>, which
    /// asserts that no mark is left above the new count (A7).
    /// </remarks>
    internal readonly List<WMark> WMarks = [];

    /// <summary>
    /// NOT UPSTREAM'S, D11 M2, the dedup: the successes of each open positive lookaround that a
    /// partial call's second pass continued after a blocked end, by the frame's backtracking-stack
    /// position (design section 4.2). A second success with the same captures, fuzzy counts and
    /// fuzzy changes leads where the first did, so it is not continued again. An entry goes when
    /// its frame goes (A12).
    /// </summary>
    internal readonly List<LookaroundSuccess> LookaroundSuccesses = [];

    /// <summary>
    /// NOT UPSTREAM'S, D11: whether the attempt running is the one that starts inside the text to
    /// come, with both sides of its start unknown (<c>Matcher.MatchFromInsideTheTextToCome</c>).
    /// </summary>
    internal bool InsideW;

    /// <summary>
    /// NOT UPSTREAM'S, D73: in the attempt that starts inside the text to come, whether no held text
    /// lies beyond the edge, so that the edge is the end of the string. Set by
    /// <c>Matcher.MatchFromInsideTheTextToCome</c> before it sets <see cref="InsideW"/>, read only
    /// while that is set, and cleared with it when the state is reset.
    /// </summary>
    internal bool NothingHeldBeyondTheEdge;

    /// <summary>
    /// NOT UPSTREAM'S, D11: whether the attempt running in a partial call's second pass has read
    /// the text to come: an undetermined test, a decided one that differs from the first pass, or
    /// any test of the attempt from inside that text. Cleared at each attempt's start. It is the
    /// per-attempt flag of the design's assert A1; M3 turns a success into a partial only when it
    /// is set, because any other success is one the first pass could have found.
    /// </summary>
    /// <remarks>
    /// A touch mark cannot say this: a cut discards it with the path it marks, and a decided read
    /// leaves none. A run-out needs no flag, because it answers the partial itself.
    /// </remarks>
    internal bool AttemptTouchedTheTextToCome;

    /// <summary>
    /// NOT UPSTREAM'S, D11 M4 (design section 4.4): the forks of the current attempt of a partial
    /// call's second pass, a fork being a blocked construct that keeps more than one outcome.
    /// Cleared at each attempt's start; always 0 in a first pass (assert A6).
    /// </summary>
    internal long Forks;

    /// <summary>
    /// NOT UPSTREAM'S, D11 M4: set when <see cref="Forks"/> passes the budget. The matcher reads it
    /// at its next cancellation check (which the firing brings forward) and answers the partial at
    /// the attempt's start (<see cref="ForkAttemptStart"/>).
    /// </summary>
    internal bool ForkBudgetSpent;

    /// <summary>NOT UPSTREAM'S, D11 M4: where the current attempt started.</summary>
    internal int ForkAttemptStart;

    /// <summary>No verb whose scope is the attempt has been skipped (<see cref="SkippedAttemptVerb"/>).</summary>
    internal const long NoSkippedVerb = long.MinValue;

    /// <summary>Verbs of two kinds or positions were skipped (<see cref="SkippedAttemptVerb"/>).</summary>
    internal const long SkippedVerbsDiffer = long.MaxValue;

    /// <summary>
    /// NOT UPSTREAM'S, D11 M2: which verb whose scope is the attempt the second pass skipped in this
    /// attempt: a <c>(*SKIP)</c>'s position, -1 for a <c>(*PRUNE)</c>, or one of the two constants.
    /// A later verb of the attempt whose path is not blocked may act only when it is the same verb
    /// (<c>Matcher.ActingEndsTheScopeAsTheSkippedVerbDid</c>): then the attempt ends as the skipped
    /// one would have ended it.
    /// </summary>
    internal long SkippedAttemptVerb = NoSkippedVerb;

#if DEBUG
    /// <summary>
    /// NOT UPSTREAM'S, D11 M4, Debug only: set by <c>Matcher.ForkBudgetPartial</c>, so that
    /// <c>Matcher.BasicMatch</c> can assert that a partial returned with <see cref="ForkBudgetSpent"/>
    /// set is the budget's own (rule 10; chunk 9 fix round's re-review, finding 3).
    /// </summary>
    internal bool ForkBudgetAnswered;
#endif

    /// <summary>
    /// NOT UPSTREAM'S, D11 chunk 8: whether a run-out has given this call an unknown capture, so
    /// the backreference and group-condition arms must ask (<c>Matcher.MarkTheBodysCapturesUnknown</c>).
    /// A call without partial never sets it and pays one test per such arm.
    /// </summary>
    internal bool HasUnknownCaptures;

    /// <summary>
    /// Whether consumption past the right edge of the text is a run-out (a partial) rather than a
    /// failure: forward partial calls, and the attempt from inside the text to come either way.
    /// </summary>
    internal bool RunsOutRight
    {
        // AggressiveInlining (cold-arm round 3): BasicMatchCore calls this from its repeat arms, and the JIT
        // turned every one of those inlines down as "unprofitable" (runtime JitTracing events, 2026-10-14):
        // its inline policy scales profitability by (1 - locals / JitMaxLocalsToTrack), and that method
        // has hundreds of locals. They inline anyway once that limit is raised; the body is short.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => PartialSide == PartialRight || InsideW;
    }

    /// <summary>The same as <see cref="RunsOutRight"/>, for the left edge.</summary>
    internal bool RunsOutLeft => PartialSide == PartialLeft || InsideW;

    /// <summary>
    /// NOT UPSTREAM'S, D11: how many times each <see cref="PartialEvent"/> happened in this call,
    /// which the port grid reads per row to show every mechanism fired (VERIFICATION rule 11).
    /// </summary>
    /// <remarks>
    /// Null outside Debug builds. It is on the state rather than in a static because the library
    /// holds no writable static (<c>ThreadSafetyTests</c>). <see cref="Init"/> zeroes it for each
    /// call.
    /// </remarks>
#if DEBUG
    internal readonly long[]? PartialEventCounts = new long[Enum.GetValues<PartialEvent>().Length];
#else
    internal readonly long[]? PartialEventCounts;
#endif

    /// <summary>
    /// NOT UPSTREAM'S, D11: the mechanism that produced the run-out the matcher is about to take,
    /// so that a run-out which answers the partial can be attributed to it (the grid's C2 label,
    /// review S2 of 2026-10-04). Cleared by every run-out. Written only by Debug builds: its two
    /// methods are conditional.
    /// </summary>
    private PartialEvent? _pendingRunOutCause;

    /// <summary>
    /// NOT UPSTREAM'S, D11: how many dedup entries (<see cref="LookaroundSuccesses"/>) the dedup's
    /// look-ups and drops have examined in this call, so that a test can show the work grows with
    /// the entries made, not with their product (row X1). Counted in every build, so the test's
    /// bound is live in the Release suite CI runs (review L1, 2026-10-04); the dedup runs only in a
    /// partial call's second pass, so no other call pays for the count.
    /// </summary>
    internal long DedupEntriesExamined;

    /// <summary>
    /// NOT UPSTREAM'S: the group calls that are open right now, one entry per call: the key
    /// <c>(call index &lt;&lt; 32) | text position</c>, and the <see cref="ReachedWidth"/> when the
    /// call was made. In a pattern where a conditional or backreference reads a group, two open
    /// calls can share an entry, so <see cref="ActiveCallCounts"/> is used in its place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ledger entry 14: the guard against a recursion that can never finish. A call that
    /// re-enters a group at the text position where a call of that group is already open, when the
    /// attempt has reached no text since that call opened AND with every group a conditional or
    /// backreference reads showing what it showed then (<see cref="CallRead"/>: set or not for a
    /// conditional, the text for a backreference), can only repeat the open call's work one level
    /// deeper, so that path recurses for ever and is failed. The captures matter
    /// because a group set between the two calls can change what a conditional or backreference
    /// inside does (<c>GroupCallTests.A_call_that_reaches_nothing_new_but_sees_a_new_capture_is_let_through</c>;
    /// PCRE2 refuses those shapes, upstream answers them). If the attempt HAS reached
    /// further, the inner call is let through: that is left recursion, <c>G -&gt; '' | G 'a'</c>,
    /// where the outer call's first try failed further on and the inner call is how 'aa' gets
    /// matched. The rule is PCRE2's (<c>OP_RECURSE</c> in <c>pcre2_match.c</c> 10.47, lines
    /// 5686-5709: same group, same subject pointer and the same <c>last_used_ptr</c>). PCRE2 then
    /// fails the whole match with <c>PCRE2_ERROR_RECURSELOOP</c>; this port fails only the path.
    /// Upstream has no guard at all and allocates until <c>MemoryError</c>.
    /// </para>
    /// <para>
    /// Why it terminates: the width only grows during an attempt and never passes the text length,
    /// each read group shows one of at most <c>(n + 1)^2 + 1</c> values (n the text length: a text
    /// or unset; a group only conditionals read has two), and the set holds each entry once, so at
    /// most <c>(n + 1) * ((n + 1)^2 + 1)^r</c> calls of one group can be open at one position, r the
    /// number of groups read. Groups nothing reads are left out
    /// of the key, which matters: keying on them let <c>(?:()|()|...|())(?R)|\1x</c> open a call
    /// for every order of setting the empty groups, about k! paths. When no group is read at all
    /// the bound is <c>n + 1</c>.
    /// </para>
    /// <para>
    /// A set rather than a counter where no group is read, because an entry is only ever added when
    /// it is absent - that is what the guard tests. Where groups are read, a count per key
    /// (<see cref="ActiveCallCounts"/>), so a call whose key and reach no open call shares is let
    /// through without looking at any capture; only when one does are the open calls with that key
    /// and reach compared with the read groups now (<see cref="OpenCallSpans"/>). Either way the test
    /// costs O(1) on a recursion ten thousand deep. <see cref="OpenCalls"/> is the same information
    /// as a stack, and is what keeps them in step.
    /// </para>
    /// </remarks>
    internal readonly HashSet<(long Key, int Reach)> ActiveCalls = [];

    /// <summary>
    /// NOT UPSTREAM'S: <see cref="ActiveCalls"/> for a pattern with a group a conditional or
    /// backreference reads: how many calls are open for each key and reach. Only keys with an open
    /// call are present.
    /// </summary>
    internal readonly Dictionary<(long Key, int Reach), int> ActiveCallCounts = [];

    /// <summary>
    /// NOT UPSTREAM'S: the lowest and highest text positions this attempt has reached, read where a
    /// match path fails and where a group call is made. Only the guard in
    /// <see cref="ActiveCalls"/> reads them, and the failed-call memo keys them
    /// (<c>Matcher.FailedCallKey</c>) because the guard does.
    /// </summary>
    /// <remarks>
    /// Both ends, so the measure works the same in a reversed pattern, where the text reached grows
    /// to the left. <see cref="ReachedLow"/> starts above <see cref="ReachedHigh"/>, meaning nothing
    /// reached yet; the first group call of the attempt records its own position before it reads
    /// the width.
    /// </remarks>
    internal int ReachedLow = int.MaxValue;

    /// <inheritdoc cref="ReachedLow"/>
    internal int ReachedHigh = int.MinValue;

    /// <summary>How much text this attempt has reached; see <see cref="ReachedLow"/>.</summary>
    internal int ReachedWidth => ReachedHigh - ReachedLow;

    /// <summary>
    /// NOT UPSTREAM'S: the same open calls as <see cref="ActiveCalls"/>, innermost last, each with
    /// the <see cref="Sstack"/> depth its frame ends at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The depth is what makes the guard safe, and it was a blind review that proved it has to be
    /// here</b> (S47). Pushing and popping the key at <c>GROUP_CALL</c> and <c>GROUP_RETURN</c> is
    /// not enough, because a call's frames can be thrown away without either arm ever running: a
    /// <c>(*PRUNE)</c> or <c>(*SKIP)</c> truncates the backtracking stack and leaves the saved stack
    /// alone, so the <c>GROUP_CALL</c> entry is gone while the call is still open, and an enclosing
    /// atomic group, lookaround or conditional then restores <see cref="ByteStack.Count"/> on the
    /// saved stack and discards the orphan. The key would stay in the set for the rest of the
    /// attempt and refuse the next legitimate call of that group at that position - a match upstream
    /// finds, lost, and not a shape upstream blows up on. So every site that restores the saved
    /// stack's count calls <c>Matcher.CloseFramesAbove</c>, which drops every entry whose frame that
    /// restore has just discarded.
    /// </para>
    /// <para>
    /// <see cref="Matcher"/>'s <c>start_match</c> clears both, which covers a whole attempt being
    /// abandoned.
    /// </para>
    /// <para>
    /// <c>MemoKey</c> is the call's entry key for the failed-call memo (<see cref="FailedCalls"/>),
    /// or <see langword="null"/> when the call is not to be recorded: the memo is off or not yet
    /// switched on, the memo is full, or the call has returned at least once. <c>GROUP_CALL</c>'s
    /// backtrack arm records a key it finds here; a call that returned is re-opened by
    /// <c>GROUP_RETURN</c>'s backtrack arm with none, so it is never recorded.
    /// </para>
    /// </remarks>
    internal readonly List<(long Key, int Reach, int SstackDepth, long[]? MemoKey)> OpenCalls = [];

    /// <summary>
    /// NOT UPSTREAM'S: for each entry of <see cref="OpenCalls"/>, in the same order, the packed span
    /// (<c>Matcher.PackedSpan</c>, -1 when unset) that each group of
    /// <see cref="PatternObject.CallReadGroups"/> held when the call was made. The guard compares
    /// them (<see cref="CallRead"/>) and the failed-call memo keys them; empty when no group is read.
    /// </summary>
    internal readonly List<long> OpenCallSpans = [];

    /// <summary>
    /// NOT UPSTREAM'S (the failed-call memo): the entry keys of the calls in this pass that ran out of
    /// choices without ever returning, or <see langword="null"/> before the first is recorded. See
    /// <c>Matcher.FailedCallKey</c> for what a key holds and why a call with a recorded key can be
    /// failed at once.
    /// </summary>
    /// <remarks>
    /// One set per pass: <see cref="InitMatch"/> empties it. A pass is one <c>basic_match</c>, with
    /// one <see cref="SearchAnchor"/>, one search flag and one error limit, all read by what a call
    /// can do (the rule against an insertion where a search began, and the fuzzy limits) but none
    /// in the key. A set kept across the best-match walk's passes answered
    /// <c>(?b)(?1)(?(DEFINE)((?:ab){e&lt;=1}))</c> over <c>zb</c> with no match, where the right
    /// answer is (0, 2) with a substitution.
    /// </remarks>
    internal HashSet<long[]>? FailedCalls;

    /// <summary>
    /// NOT UPSTREAM'S (the failed-call memo): the calls made so far in this pass. The memo builds
    /// keys only once this passes <see cref="CallMemoThreshold"/>.
    /// </summary>
    internal long CallsThisPass;

    /// <summary>
    /// NOT UPSTREAM'S (the failed-call memo): how many calls a pass makes before the memo switches
    /// on, set by <see cref="InitMatch"/>; <see cref="long.MaxValue"/> where it never does.
    /// </summary>
    /// <remarks>
    /// The switch is lazy, as Perl's super-linear cache is: a pass that makes more calls than
    /// (slice length + 1) x the pattern's call sites is doing more than one call per position
    /// per site, which ordinary recursion does not. Below that the memo costs one counter per call.
    /// A call made before the switch has no key and is never recorded, which is only lost pruning.
    /// </remarks>
    internal long CallMemoThreshold = long.MaxValue;

    /// <summary>
    /// Where <c>Matcher.FailedCallKey</c> builds a key, kept so a call that is failed at once
    /// allocates nothing.
    /// </summary>
    internal readonly List<long> CallMemoKey = [];

    /// <summary>
    /// Where <c>Matcher.FailedCallKey</c> collects the open calls its key holds, as indexes into
    /// <see cref="OpenCalls"/>, kept so building a key allocates nothing.
    /// </summary>
    internal readonly List<int> CallMemoAncestors = [];

    /// <summary>
    /// NOT UPSTREAM'S (the failed-call memo): how many calls the memo has failed at once since the
    /// state was last initialised. Nothing in the engine reads it; the memo's tests and grid do, to
    /// tell a row the memo decided from one it never touched.
    /// </summary>
    internal long CallMemoHits;

    /// <summary>Upstream <c>best_match_pos</c>: where the best POSIX match so far starts.</summary>
    internal int BestMatchPos;

    /// <summary>Upstream <c>best_text_pos</c>: where the best POSIX match so far ends.</summary>
    internal int BestTextPos;

    /// <summary>
    /// Upstream <c>best_match_groups</c>: the groups of the best POSIX match so far, or
    /// <see langword="null"/> before one has been saved.
    /// </summary>
    /// <remarks>
    /// Only ever read when <see cref="FoundMatch"/> is true, which is what upstream relies on too -
    /// it leaves the array allocated for the life of the state and clears the flag in
    /// <see cref="InitMatch"/> rather than the storage.
    /// </remarks>
    internal GroupData[]? BestMatchGroups;

    /// <summary>
    /// Upstream <c>best_fuzzy_counts</c> (<c>:11501</c>): the errors the best POSIX match so far
    /// spent, saved beside its groups so that restoring the match restores what it cost.
    /// </summary>
    internal readonly long[] BestFuzzyCounts = new long[FuzzyValue.Count];

    /// <summary>
    /// The changes the best POSIX match so far made. <b>Upstream has no counterpart and that is the
    /// point of it</b> - see <c>Matcher.SaveBestMatch</c>.
    /// </summary>
    internal readonly List<FuzzyChange> BestFuzzyChanges = [];

    /// <summary>
    /// The running <see cref="TotalErrors"/> and <see cref="TotalCost"/> of the best POSIX match so
    /// far. <b>Upstream has no counterpart to either, and the first of them is an inherited bug
    /// rather than a missing feature</b> - see <c>Matcher.RestoreBestMatch</c>.
    /// </summary>
    internal long BestTotalErrors;

    /// <inheritdoc cref="BestTotalErrors"/>
    internal long BestTotalCost;

    /// <summary>Upstream <c>min_width</c>, a codepoint count.</summary>
    internal long MinWidth;

    /// <summary>
    /// Whether <c>Matcher.SearchStart</c> is still worth calling.
    /// </summary>
    /// <remarks>
    /// Upstream keeps this on the pattern (<c>do_search_start</c>, <c>_regex.c:588</c>) and clears it
    /// there, so a pattern the prefilter cannot help stops paying for the call for the rest of the
    /// program. This port cannot: a compiled pattern is frozen after <c>Compile</c> and shared across
    /// threads without a lock, and <c>ThreadSafetyTests</c> holds it to that. So the flag is seeded
    /// from <see cref="PatternObject.DoSearchStart"/> here and cleared on the state instead, and the
    /// saving lasts one matching operation rather than forever. The cost of the difference is one
    /// wasted <c>SearchStart</c> call per operation on a pattern it cannot help.
    /// </remarks>
    internal bool DoSearchStart;

    /// <summary>Upstream <c>encoding</c>, reduced to which table it is (see <see cref="Encodings"/>).</summary>
    internal CaseEncoding Encoding;

    /// <summary>Upstream <c>partial_side</c>.</summary>
    internal int PartialSide;

    /// <summary>Upstream <c>max_errors</c>.</summary>
    internal long MaxErrors;

    /// <summary>
    /// The maximum permitted fuzzy COST for one run of <c>basic_match</c>, the way
    /// <see cref="MaxErrors"/> is the maximum permitted error count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is upstream's own field, from before the rework</b>: <c>state-&gt;max_cost</c>,
    /// <c>/* The maximum permitted fuzzy cost. */</c>, at line 579 of <c>_regex.c</c> in release
    /// 2015.09.28, where <c>any_error_permitted</c> and <c>this_error_permitted</c> ended in
    /// <c>state-&gt;total_cost &lt;= state-&gt;max_cost</c> and <c>state-&gt;total_cost +
    /// values[RE_FUZZY_VAL_COST_BASE + fuzzy_type] &lt;= state-&gt;max_cost</c> (<c>:9923</c>,
    /// <c>:9936</c>). The 2015.11.5 issue-165 hang fix replaced it with <c>max_errors</c> throughout,
    /// and that is why upstream cannot honour its own cost equations when it ranks - open issue 470.
    /// The owner's decision is that this port ranks by cost (DECISIONS 2026-09-12), so the bound
    /// comes back.
    /// </para>
    /// <para>
    /// <b>It is additive, not a replacement.</b> <see cref="MaxErrors"/> stays exactly as upstream
    /// leaves it and every mode but <c>BESTMATCH</c> leaves this at <see cref="long.MaxValue"/>, so
    /// the three predicates gain a conjunct that is true by construction and nothing outside
    /// <c>Matcher.DoBestFuzzyMatch</c> changes behaviour at all. With unit costs the two bounds are
    /// the same bound: <c>total_cost + 1 &lt;= max_cost</c> is <c>error_count &lt; max_errors</c>.
    /// </para>
    /// <para>
    /// It goes negative, which upstream's <c>size_t</c> could not: the first pass sets it to one
    /// below the cheapest run so far, and a cost equation may price an error kind at zero. Tests read
    /// <c>&lt;= 0</c> rather than <c>== 0</c> for that reason.
    /// </para>
    /// </remarks>
    internal long MaxCost;

    /// <summary>Upstream <c>total_errors</c>.</summary>
    internal long TotalErrors;

    /// <summary>
    /// What the errors in <see cref="TotalErrors"/> cost under the fuzzy section that used them.
    /// <b>This port's own field: upstream has no running total of the cost.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream computes <c>total_cost</c> (<c>upstream/src/_regex.c</c> line 9649) on demand inside
    /// the per-error constraint checks, which always have a section in hand. The ranking modes need
    /// it once the match is over, when the section has been popped and there is nothing left to ask -
    /// so it is recorded here, at exactly the two places <see cref="TotalErrors"/> is recorded
    /// (<c>:12484</c> and <c>:15561</c>, both <c>END_FUZZY</c>).
    /// </para>
    /// <para>
    /// <b>With nested sections this conflates their cost equations, deliberately.</b> Once
    /// <c>END_FUZZY</c> has merged an inner section's counts into the outer ones the two are not
    /// separable again, and upstream's own <c>any_error_permitted</c> then applies the outer
    /// section's equation to the merged counts for the rest of the match. This field follows the same
    /// rule the other way round - the merged counts under the section just closed - so a pattern with
    /// one fuzzy section, which is every case where cost ranking differs from error ranking, is
    /// exact.
    /// </para>
    /// </remarks>
    internal long TotalCost;

    /// <summary>Upstream <c>fewest_errors</c>.</summary>
    internal long FewestErrors;

    /// <summary>Upstream <c>capture_change</c>.</summary>
    internal long CaptureChange;

    /// <summary>
    /// NOT UPSTREAM (ledger entry 44's addendum): for each <c>(*PRUNE)</c> or <c>(*SKIP)</c> this
    /// attempt has crossed, in order, the depth of <see cref="Pstack"/> when it was crossed: the
    /// pruning mark it cuts to, or for one in an unfinished atomic group or positive lookaround the
    /// group it will unwind past (ledger entry 47). A pass or iteration records how many there were
    /// when it began; <c>Matcher.VerbCutPast</c> reads the ones crossed since.
    /// </summary>
    internal readonly List<int> VerbMarks = [];

    /// <summary>How many verbs this attempt has crossed: the length of <see cref="VerbMarks"/>.</summary>
    internal int VerbsCrossed => VerbMarks.Count;

    /// <summary>
    /// NOT UPSTREAM (ledger 33): what a fuzzy edit adds to <see cref="CaptureChange"/>, where
    /// upstream adds 1. A referenced group's span change still adds 1, so the low 32 bits count
    /// group changes alone and come back with every save and restore upstream already makes -
    /// a group call's return included. Only ever compared for equality, as upstream compares it.
    /// A match cannot make 2^32 group changes: each pushes a backtrack entry, and the stack stops
    /// at 1 GB.
    /// </summary>
    internal const long FuzzyEditChange = 1L << 32;

    /// <summary>The group-change half of a <see cref="CaptureChange"/> value.</summary>
    /// <param name="captureChange">A <see cref="CaptureChange"/> value.</param>
    /// <returns>Its low 32 bits.</returns>
    internal static long GroupChanges(long captureChange) => captureChange & (FuzzyEditChange - 1);

    /// <summary>
    /// NOT UPSTREAM (D17): the last run number handed to a repeat (<see cref="RepeatData.RunId"/>).
    /// It only counts up, so a number is never reused within one call.
    /// </summary>
    internal long NextRunId;

    /// <summary>
    /// NOT UPSTREAM (D17): the states that empty iterations have led to in this attempt, keyed by
    /// run. Emptied by <see cref="ResetGuards"/>; <see langword="null"/> until a pattern that tests
    /// a group makes such an iteration. See <c>Matcher.RevisitsEmptyIterationState</c>.
    /// </summary>
    internal HashSet<EmptyIterationState>? EmptyIterationStates;

    /// <summary>
    /// NOT UPSTREAM (D17): the key <c>Matcher.RevisitsEmptyIterationState</c> fills for each lookup,
    /// so that only a new state allocates. <see langword="null"/> exactly when the pattern tests no
    /// group. Every lookup overwrites all of it before reading; <see cref="Init"/> clears it anyway,
    /// so that a reused state starts exactly as a new one does.
    /// </summary>
    internal readonly long[]? EmptyIterationScratch;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule): where on <see cref="Sstack"/> the frame of the fuzzy
    /// section now open ends, or -1 when no section entered in this attempt is open. Kept only for a
    /// pattern with a minimum error count (<c>PatternObject.HasFuzzyMinimum</c>), which is what
    /// needs to know whether an enclosing section's minimum is met
    /// (<c>Matcher.RaisesUnmetMinimum</c>, <c>Matcher.AllMinimumsMet</c>).
    /// </summary>
    /// <remarks>
    /// FUZZY pushes the enclosing section's counts and node onto <see cref="Sstack"/>, and for such
    /// a pattern this value beneath them, the frame of the section it was entered from. Each
    /// section's link therefore lives in its own frame, restored with it on every path the engine
    /// takes, and a recursive entry into a section gets a frame of its own. The value itself is set
    /// by FUZZY, END_FUZZY and their backtrack arms, and cleared at the start of each attempt, where
    /// upstream leaves <see cref="FuzzyNode"/> as a verb may have left it. A verb that drops
    /// backtracking entries leaves the structure stack alone, but an enclosing atomic group,
    /// lookaround or conditional then cuts the stack back past a section whose FUZZY entry is gone,
    /// so each such cut also steps this value out to a frame still on the stack
    /// (<c>Matcher.CloseFramesAbove</c>).
    /// </remarks>
    internal int SectionFrame = -1;

    /// <summary>
    /// The bytes a section frame spans for a pattern that keeps <see cref="SectionFrame"/>: the link,
    /// three counts, the change count and the outer node.
    /// </summary>
    internal const int SectionFrameSize = 6 * sizeof(long);

    /// <summary>Upstream <c>req_pos</c>: where the required string matched, or -1.</summary>
    internal int ReqPos;

    /// <summary>Upstream <c>req_end</c>.</summary>
    internal int ReqEnd;

    /// <summary>
    /// NOT UPSTREAM (D14): what the pattern's <see cref="FuzzyLiteralFilter"/> has learned about
    /// this scan's subject, kept across the scan's steps; <see langword="null"/> when the pattern
    /// has no filter.
    /// </summary>
    internal readonly FuzzyLiteralFilter.ScanMemory? FilterMemory;

    /// <summary>
    /// NOT UPSTREAM: the slice start <see cref="RequiredStringScreen"/> last scanned, part of its
    /// cache key.
    /// </summary>
    internal int ScreenSliceStart;

    /// <summary>
    /// NOT UPSTREAM: the slice end <see cref="RequiredStringScreen"/> last scanned, part of its cache
    /// key; -1 when nothing is cached.
    /// </summary>
    internal int ScreenSliceEnd = -1;

    /// <summary>
    /// NOT UPSTREAM: the text position <see cref="RequiredStringScreen"/>'s last scan began from.
    /// </summary>
    internal int ScreenFrom;

    /// <summary>
    /// NOT UPSTREAM: what <see cref="RequiredStringScreen"/>'s last scan found - where the required
    /// string could start (forward) or end (reverse) - or -1 for nowhere.
    /// </summary>
    internal int ScreenFound;

    /// <summary>
    /// NOT UPSTREAM: whether <see cref="ScreenFound"/> is the farthest possible occurrence rather
    /// than the nearest, so that nothing lies beyond it.
    /// </summary>
    internal bool ScreenFoundFarthest;

    /// <summary>
    /// NOT UPSTREAM: the stretch of attempt positions <see cref="RequiredStringScreen"/> has already
    /// cleared for a caller that does not jump, so that <c>Matcher.BasicMatch</c> can skip the call
    /// for each of them. Empty (low above high) when there is none.
    /// </summary>
    internal int ScreenClearedLow = 1;

    /// <inheritdoc cref="ScreenClearedLow"/>
    internal int ScreenClearedHigh;

    /// <summary>
    /// NOT UPSTREAM: whether <see cref="RequiredStringScreen"/> has already cleared an attempt at
    /// <see cref="TextPos"/> against the current slice, so the locator need not be asked.
    /// </summary>
    /// <returns><see langword="true"/> if the attempt may go ahead without asking.</returns>
    internal bool ScreenSettles() =>
        TextPos >= ScreenClearedLow
        && TextPos <= ScreenClearedHigh
        && ScreenSliceStart == SliceStart
        && ScreenSliceEnd == SliceEnd;

    /// <summary>Upstream <c>lastindex</c>.</summary>
    internal int LastIndex;

    /// <summary>Upstream <c>lastgroup</c>.</summary>
    internal int LastGroup;

    /// <summary>
    /// Upstream <c>timeout</c>, in <see cref="Stopwatch"/> ticks rather than clock ticks, or
    /// <see cref="NoTimeout"/>. Upstream's <c>decode_timeout</c> (<c>:21056</c>) maps a negative
    /// number to "no timeout", which is what <c>FuzzyRegex.InfiniteMatchTimeout</c> does here.
    /// </summary>
    internal long Timeout;

    /// <summary>
    /// The caller's cancellation token, polled at the same site as the clock. Upstream has no
    /// counterpart field: its equivalent is the interpreter's own signal state, which
    /// <c>safe_check_cancel</c> reads through <c>PyErr_CheckSignals</c> (<c>:2253</c>) rather than
    /// carrying on the state. See <see cref="MatchLimits"/>.
    /// </summary>
    internal CancellationToken Cancellation;

    /// <summary>Upstream <c>start_time</c>.</summary>
    internal long StartTime;

    /// <summary>
    /// NOT UPSTREAM'S, a test seam: the clock check at which <see cref="CheckTimedOut"/> reports the
    /// budget spent whatever the clock says, or 0 for none. See <see cref="FuzzyRegex.ExpiresOnCheck"/>.
    /// </summary>
    internal int ExpiresOnCheck;

    /// <summary>
    /// NOT UPSTREAM'S: clock checks made so far in this operation, counted only while
    /// <see cref="ExpiresOnCheck"/> is set.
    /// </summary>
    internal int ClockChecks;

    /// <summary>Upstream <c>iterations</c>: how long since the last cancellation check.</summary>
    internal ushort Iterations;

    /// <summary>Upstream <c>overlapped</c>.</summary>
    internal bool Overlapped;

    /// <summary>Upstream <c>reverse</c>.</summary>
    internal bool Reverse;

    /// <summary>Upstream <c>visible_captures</c>.</summary>
    internal bool VisibleCaptures;

    /// <summary>
    /// NOT UPSTREAM'S, stage C4: whether the group arms record nothing for this call, because the
    /// caller reads no group and nothing in the pattern reads one while matching. Cleared by
    /// <see cref="Init"/>, set by <see cref="SkipCapturesIfUnread"/>. Not
    /// <see cref="VisibleCaptures"/>, which is about capture lists: <c>Split</c> passes
    /// <see langword="false"/> there and still reads every group's span.
    /// </summary>
    internal bool SkipsCaptures;

    /// <summary>
    /// For a caller that reads no group span or capture list (<c>IsMatch</c>, <c>Count</c>, a span
    /// enumeration, a replacement without a group reference): records no captures at all when the
    /// pattern reads none while matching. See <c>docs/plan/2026-10-11-capture-free-matching.md</c>,
    /// amendment 1.
    /// </summary>
    /// <remarks>
    /// A pattern reads a group while matching exactly when one is <see cref="GroupInfo.Referenced"/>
    /// - by a backreference or a group condition (<c>NodeCompiler.RecordRefGroup</c>) - and those
    /// are the <see cref="PatternObject.TestedGroups"/>. Every other reader of group state keys on
    /// them: the capture-change counter, the D17 empty-iteration states and the group-call memo
    /// (<see cref="PatternObject.CallReadGroups"/>).
    /// </remarks>
    internal void SkipCapturesIfUnread()
    {
        Debug.Assert(
            PartialSide == PartialNone,
            "C4: no capture-free caller is partial, so D11's unknown captures never meet the skip"
        );
        SkipsCaptures = Pattern.TestedGroups.Length == 0;
    }

    /// <summary>Upstream <c>version_0</c>.</summary>
    internal bool Version0;

    /// <summary>Upstream <c>must_advance</c>.</summary>
    internal bool MustAdvance;

    /// <summary>Upstream <c>too_few_errors</c>.</summary>
    internal bool TooFewErrors;

    /// <summary>Upstream <c>match_all</c>: this is a <c>fullmatch</c>.</summary>
    internal bool MatchAll;

    /// <summary>Upstream <c>found_match</c>: a POSIX match has been found.</summary>
    internal bool FoundMatch;

    /// <summary>Upstream <c>is_fuzzy</c>.</summary>
    internal bool IsFuzzy;

    /// <summary>
    /// NOT UPSTREAM (D61): the storage rule H' keeps the banned search's match in while it tries the
    /// attempt at the anchor (<c>Matcher.AnchorBanSnapshot</c>), reused across the searches of one
    /// call. Dropped at reset.
    /// </summary>
    internal Matcher.AnchorBanSnapshot? AnchorBanScratch;

    /// <summary>
    /// Upstream <c>fuzzy_counts</c>: how many substitutions, insertions and deletions the fuzzy
    /// section now being matched has used, indexed by <see cref="FuzzyValue.Sub"/>,
    /// <see cref="FuzzyValue.Ins"/> and <see cref="FuzzyValue.Del"/>.
    /// </summary>
    /// <remarks>
    /// The counts of an <b>enclosing</b> section are not in here: <c>FUZZY</c> pushes them onto
    /// <see cref="Sstack"/> and zeroes these, and <c>END_FUZZY</c> adds the two together again.
    /// </remarks>
    internal readonly long[] FuzzyCounts = new long[FuzzyValue.Count];

    /// <summary>
    /// Upstream <c>fuzzy_node</c>: the <c>FUZZY</c> node whose constraints
    /// <see cref="FuzzyCounts"/> is being tested against, or <see langword="null"/> outside any
    /// fuzzy section.
    /// </summary>
    internal Node? FuzzyNode;

    /// <summary>
    /// NOT UPSTREAM (D22): whether the last retried <c>REF_GROUP_FLD</c> edit was one of ledger
    /// entry 52's whole-character kinds, which the arm must step past even when
    /// <see cref="PatternObject.SkipRetriedFoldSteps"/> restores upstream's re-entry.
    /// </summary>
    /// <remarks>
    /// That switch reproduces upstream's re-entry for upstream's own edits, which compare the used-up
    /// character again. A whole-character edit always uses up the subject character and upstream has
    /// none, so skipping its step would only undo it, and the oracle's
    /// <c>full-fold-backreference-retry</c> entry would claim the rows the edit explains. Set on
    /// every successful retry, which is the one way into the arm's re-entry.
    /// </remarks>
    internal bool RetriedAWholeFoldEdit;

    /// <summary>
    /// Upstream <c>fuzzy_changes</c> (<c>RE_FuzzyChangesList</c>, <c>:397</c>): every error used so
    /// far, in the order it was used.
    /// </summary>
    /// <remarks>
    /// A <see cref="List{T}"/> rather than a hand-grown array: upstream's <c>capacity</c>/<c>count</c>
    /// pair and its doubling from 64 (<c>record_fuzzy</c>, <c>:9775</c>) are what a <c>List</c> is,
    /// and <c>unrecord_fuzzy</c> (<c>:9801</c>) is nothing but <c>--count</c>.
    /// </remarks>
    internal readonly List<FuzzyChange> FuzzyChanges = [];

    /// <summary>
    /// How far past a query <see cref="NoHighSurrogateIn"/> searches when it has to search at all,
    /// so that a run of small queries moving along the subject costs one vectorised search per this
    /// many units rather than one per query.
    /// </summary>
    /// <remarks>
    /// 256 units is 512 bytes, a few nanoseconds of vectorised search, and it is also the most a
    /// call examines beyond what its queries reach. Larger only makes a short match on a long
    /// subject pay for text it never asks about.
    /// </remarks>
    private const int _scanChunk = 256;

    /// <summary>The first unit of the stretch known to hold no high surrogate: see <see cref="NoHighSurrogateIn"/>.</summary>
    private int _cleanStart;

    /// <summary>One past the last unit of that stretch.</summary>
    private int _cleanEnd;

    /// <summary>Whether a high surrogate was found at an edge of the stretch, which then never grows again.</summary>
    private bool _highSurrogateFound;

    /// <summary>
    /// What this state has learnt about the subject, for <see cref="Match.NextMatch"/> to hand to the
    /// next state of a walk.
    /// </summary>
    internal SubjectScan Scan => new(_cleanStart, _cleanEnd, _highSurrogateFound);

    /// <summary>
    /// Whether the code units <c>[low, high)</c> of <see cref="Text"/> hold no high surrogate, which
    /// is when one character is exactly one code unit over them - what upstream gets for free by
    /// indexing the subject by codepoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not upstream's. <see cref="NextPos"/> steps two units only from a high surrogate, so a
    /// stretch with none in it converts between a character count and a position by arithmetic;
    /// <see cref="Matcher.StepBy"/> and <see cref="Matcher.CountBetween"/> ask this before they fall
    /// back on the <see cref="CharacterIndex"/>. A lone high surrogate answers <see langword="false"/>
    /// although it steps one unit: conservative, and it costs only the index a pair would have cost.
    /// </para>
    /// <para>
    /// <b>It looks only where it is asked, and never twice (D83).</b> The state keeps one stretch
    /// known to be clean and widens it to cover each query that reaches past it, by
    /// <see cref="_scanChunk"/> at least, with one vectorised search of the units it adds. So a call
    /// examines the hull of what its queries touch and little more, and a walk that hands the
    /// stretch on (<see cref="SubjectScan"/>) examines each unit about once in all. Before D83 every
    /// state searched the whole subject when it was set up: a <c>Match</c> that found a word at
    /// position 0 of a megabyte paid for the megabyte, 30 of its 36 microseconds.
    /// </para>
    /// <para>
    /// Once a widening meets a high surrogate the stretch stops there and never grows again: every
    /// query outside it is answered <see langword="false"/> and goes to the index. Growing past the
    /// surrogate would need a second stretch, and a subject with a surrogate pair in reach pays for
    /// the index either way.
    /// </para>
    /// </remarks>
    /// <param name="low">The first unit.</param>
    /// <param name="high">One past the last unit.</param>
    /// <returns>Whether none of those units is a high surrogate.</returns>
    internal bool NoHighSurrogateIn(int low, int high)
    {
        if (low >= high || (low >= _cleanStart && high <= _cleanEnd))
        {
            return true;
        }

        return !_highSurrogateFound && Widen(low, high);
    }

    /// <summary>
    /// <see cref="NoHighSurrogateIn"/>'s slow path: widens the clean stretch to cover
    /// <c>[low, high)</c>, stopping at the first high surrogate in the way.
    /// </summary>
    /// <param name="low">The first unit of the query.</param>
    /// <param name="high">One past its last unit.</param>
    /// <returns>Whether the widened stretch covers the query.</returns>
    private bool Widen(int low, int high)
    {
        // The ushort form of the search allocates nothing; the char form allocated 96 B on every
        // call, even after tier-up, in Debug and Release (measured 2026-09-22, .NET 10).
        // AllocationTests pins a warm IsMatch's 0.
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(Text.Span);

        if (_cleanStart >= _cleanEnd)
        {
            // Nothing known yet: start from the query, not from either end of the subject.
            _cleanStart = low;
            _cleanEnd = low;
        }

        if (low < _cleanStart)
        {
            // Downwards. A gap between the query and the stretch is searched too, so that the
            // stretch stays one piece; it is text the search has already passed over.
            int from = Math.Max(0, Math.Min(low, _cleanStart - _scanChunk));
            int found = units[from.._cleanStart].LastIndexOfAnyInRange((ushort)0xD800, (ushort)0xDBFF);
            WorkCounter.ScannedSubject(_cleanStart - from);
            if (found >= 0)
            {
                _highSurrogateFound = true;
                _cleanStart = from + found + 1;
                return false;
            }

            _cleanStart = from;
        }

        if (high > _cleanEnd)
        {
            // Upwards, and the chunk stops at TextEnd: no match reads past it, so a surrogate there
            // must not send the queries to the index.
            int to = Math.Min(units.Length, Math.Max(high, Math.Min(TextEnd, _cleanEnd + _scanChunk)));
            int found = units[_cleanEnd..to].IndexOfAnyInRange((ushort)0xD800, (ushort)0xDBFF);
            WorkCounter.ScannedSubject(to - _cleanEnd);
            if (found >= 0)
            {
                _highSurrogateFound = true;
                _cleanEnd += found;
            }
            else
            {
                _cleanEnd = to;
            }
        }

        return high <= _cleanEnd;
    }

    private CharacterIndex? _characterIndex;

    /// <summary>
    /// The sampled position table that converts between a character count and a position over a
    /// stretch <see cref="NoHighSurrogateIn"/> cannot vouch for, building it on first use.
    /// </summary>
    /// <remarks>
    /// A method rather than a property because the first call walks the whole subject. Built lazily
    /// rather than in <see cref="Create"/> because most patterns never ask for the conversion at
    /// all: only the single-character repeat opcodes do. A subject that holds a surrogate pair but
    /// is matched against a pattern with no such repeat pays nothing.
    /// </remarks>
    /// <returns>The index.</returns>
    internal CharacterIndex GetCharacterIndex() => _characterIndex ??= new CharacterIndex(this);

    /// <summary>
    /// The allocating half of <c>state_init_2</c> (<c>upstream/src/_regex.c</c> line 18275): the
    /// storage whose size depends on the pattern and not on the subject, which is what
    /// <see cref="MatchStateCache"/> keeps from one call to the next.
    /// </summary>
    /// <param name="pattern">The compiled pattern.</param>
    /// <param name="pool">Where the stacks rent their buffers; see <see cref="Create"/>.</param>
    /// <param name="cache">The cache <see cref="Dispose"/> hands the state back to, if any.</param>
    private MatchState(PatternObject pattern, ArrayPool<byte>? pool, MatchStateCache? cache = null)
    {
        Cache = cache;
        Sstack = new ByteStack(pool);
        Bstack = new ByteStack(pool);
        Pstack = new ByteStack(pool);
        Pattern = pattern;

        // The capture groups (state_init_2, upstream/src/_regex.c:18327). Upstream caches the block
        // on the pattern as 'groups_storage' and reuses it; MatchStateCache is that cache here.
        // 'true_group_count' rather than 'public_group_count', because a branch-reset group's
        // private number is larger than its public one and START_GROUP indexes by the private one.
        //
        // NOT PORTED, and both for the same reason: the fuzzy-guard allocation (:18515) and the
        // group-call-guard allocation. Upstream's 'fuzzy_guards' is written in four places -
        // allocated, memset, reset in 'reset_guards' (:3394) and freed - and read in NONE, exactly
        // like 'group_call_guard_list', which S30 settled the same way. `grep -n fuzzy_guards
        // upstream/src/_regex.c` on 2026-09-13 gives :508, :3394, :3395, :18310, :18515, :18517,
        // :18519, :18565, :18568, :18646, :18724, :18725 - a declaration, two resets, and
        // allocation and deallocation. Nothing consults a fuzzy guard to decide anything, so there
        // is no behaviour to port. docs/PORTMAP.md's "deliberately not ported" table has both.
        Groups = new GroupData[pattern.TrueGroupCount];
        for (int g = 0; g < Groups.Length; g++)
        {
            Groups[g] = new GroupData();
        }

        // The repeats (state_init_2, upstream/src/_regex.c:18493-18505), which upstream caches on
        // the pattern as 'repeats_storage' the same way. Nothing in 'dealloc_repeats' (:18630) is
        // ported: it is three calls to free.
        Repeats = new RepeatData[pattern.RepeatCount];
        for (int r = 0; r < Repeats.Length; r++)
        {
            Repeats[r] = new RepeatData();

            // NOT UPSTREAM (D85): see KeyedTailGuardList.
            if (pattern.RepeatInfoAt(r).TailKey is not null)
            {
                Repeats[r].KeyedTail = new KeyedTailGuards();
            }
        }

        _tailKeyScratch = new long[MaxTailKeyLength(pattern)];
        _tailTextSpans = new long[_tailKeyScratch.Length == 0 ? 0 : pattern.TrueGroupCount];
        _tailTextValues = new long[_tailKeyScratch.Length == 0 ? 0 : pattern.TrueGroupCount];

        EmptyIterationScratch =
            pattern.TestedGroups.Length > 0 ? new long[2 + FuzzyValue.Count + (2 * pattern.TestedGroups.Length)] : null;
        OptionalPasses = pattern.OptionalPassCount == 0 ? [] : new OptionalPassStart[pattern.OptionalPassCount];
        FilterMemory = pattern.FuzzyLiteralFilter?.NewScanMemory();
    }

    /// <summary>
    /// One step out along the chain of open fuzzy sections: the section the one whose frame ends at
    /// <paramref name="frame"/> was entered from, and that section's counts at the time.
    /// </summary>
    /// <remarks>
    /// Safe by construction: a link is followed only if it points below the frame it was read from,
    /// so a walk ends after at most one step per frame on the stack, whatever the stack holds.
    /// </remarks>
    /// <param name="frame">The end of the current frame; on success, the end of the outer one's.</param>
    /// <param name="outerCounts">Receives the enclosing section's counts at entry.</param>
    /// <returns>The enclosing FUZZY node, or <see langword="null"/> when there is none.</returns>
    internal Node? TryOuterSection(ref int frame, Span<long> outerCounts)
    {
        if (frame < SectionFrameSize || frame > Sstack.Count)
        {
            return null;
        }

        long link = Sstack.SizeAt(frame - SectionFrameSize);
        if (link < 0 || link > frame - SectionFrameSize)
        {
            return null;
        }

        int counts = frame - (5 * sizeof(long));
        outerCounts[FuzzyValue.Sub] = Sstack.SizeAt(counts + (FuzzyValue.Sub * sizeof(long)));
        outerCounts[FuzzyValue.Ins] = Sstack.SizeAt(counts + (FuzzyValue.Ins * sizeof(long)));
        outerCounts[FuzzyValue.Del] = Sstack.SizeAt(counts + (FuzzyValue.Del * sizeof(long)));
        Node? outer = Sstack.NodeAt(Pattern, frame - sizeof(long));
        frame = (int)link;
        return outer;
    }

    /// <summary>
    /// Port of <c>state_init</c> (<c>upstream/src/_regex.c</c> line 18598) and the half of
    /// <c>state_init_2</c> (line 18275) that is not allocation.
    /// </summary>
    /// <param name="pattern">The compiled pattern.</param>
    /// <param name="text">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether a partial match is wanted.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="matchAll">Whether the match must cover the whole slice.</param>
    /// <param name="limits">The time budget and cancellation token bounding this operation.</param>
    /// <param name="pool">
    /// Where the stacks rent their buffers, or <see langword="null"/> for
    /// <see cref="ArrayPool{T}.Shared"/>. Only <c>PoolDisciplineTests</c> passes anything else; see
    /// <see cref="ByteStack"/>.
    /// </param>
    /// <param name="scan">
    /// What an earlier state of the same walk learnt about the subject's surrogates, or the default
    /// for nothing. <see cref="Match.NextMatch"/> passes it on so that a walk does not search the
    /// same units again at every step.
    /// </param>
    /// <param name="cache">
    /// The cache the state goes back to when it is disposed; only <see cref="MatchStateCache.Rent"/>
    /// passes one.
    /// </param>
    /// <returns>The state, ready to match.</returns>
    internal static MatchState Create(
        PatternObject pattern,
        ReadOnlyMemory<char> text,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        bool matchAll,
        MatchLimits limits,
        ArrayPool<byte>? pool = null,
        SubjectScan scan = default,
        MatchStateCache? cache = null
    )
    {
        var state = new MatchState(pattern, pool, cache);
        state.Init(text, start, end, overlapped, partial, visibleCaptures, matchAll, limits, scan);
        return state;
    }

    /// <summary>
    /// Port of <c>state_init</c> (<c>upstream/src/_regex.c</c> line 18598) and the half of
    /// <c>state_init_2</c> (line 18275) that is not allocation, run on a new state by
    /// <see cref="Create"/> and on a reused one by <see cref="MatchStateCache.Rent"/>.
    /// </summary>
    /// <remarks>
    /// <b>Every field is assigned here, including the ones a new object already holds at their
    /// default</b>, because a reused state holds whatever its last match left. The two paths must
    /// give the same state, and <c>MatchStateCacheTests</c> compares them field by field over a wave
    /// of patterns, so a field added to this class without a line here fails that test. The buffers
    /// are the exception, and deliberately: a group's capture array, a guard list's spans and the
    /// two change lists keep their capacity and lose only their count, exactly as upstream's
    /// <c>clear_groups</c> (<c>:3369</c>) and <c>reset_guards</c> (<c>:3383</c>) keep theirs.
    /// <para>
    /// sync-divergence: upstream builds a new <c>RE_State</c> each call and reuses only three
    /// buffers; ours reuses the whole state, so this method resets what upstream never has to.
    /// Re-aligning: a field upstream adds to <c>RE_State</c> gets a line here.
    /// </para>
    /// </remarks>
    /// <param name="text">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether a partial match is wanted.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="matchAll">Whether the match must cover the whole slice.</param>
    /// <param name="limits">The time budget and cancellation token bounding this operation.</param>
    /// <param name="scan">As for <see cref="Create"/>.</param>
    internal void Init(
        ReadOnlyMemory<char> text,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        bool matchAll,
        MatchLimits limits,
        SubjectScan scan
    )
    {
        WorkCounter.StateInitialised();

        PatternObject pattern = Pattern;

        Text = text;
        TextLength = text.Length;
        CaptureTexts.Reset(text);
        Array.Clear(_tailTextValues); // the ids above start again (D85)

        // Nothing is searched here: NoHighSurrogateIn looks only as far as the match asks (D83).
        (_cleanStart, _cleanEnd, _highSurrogateFound) = scan;
        _characterIndex = null;

        // What a new state holds by default, and a reused one must be given back. A call that
        // succeeded leaves its stack, and so its marks, where they were (D11).
        WMarks.Clear();
        LookaroundSuccesses.Clear();
        InsideW = false;
        NothingHeldBeyondTheEdge = false;
        AttemptTouchedTheTextToCome = false;
        HasUnknownCaptures = false;
#if DEBUG
        Array.Clear(PartialEventCounts!);
#endif
        _pendingRunOutCause = null;
        DedupEntriesExamined = 0;
        Sstack.Reset();
        SetBstackCount(0);
        Pstack.Reset();
        ClearGroups();
        foreach (RepeatData repeat in Repeats)
        {
            repeat.BodyGuardList.Reset();
            repeat.TailGuardList.Reset();
            repeat.KeyedTail?.Clear();
            repeat.Count = 0;
            repeat.Start = 0;
            repeat.CaptureChange = 0;
            repeat.ChangesAtStart = 0;
            repeat.ClearMemo();
            repeat.RunId = 0;
        }

        NextRunId = 0;
        EmptyIterationStates?.Clear();
        if (EmptyIterationScratch is not null)
        {
            Array.Clear(EmptyIterationScratch);
        }

        KeyedTailWrites = 0;
        Array.Clear(_tailKeyScratch);
        Array.Clear(_tailTextSpans);

        // Every pass writes its slot before its end reads it, so this is for reuse alone.
        Array.Clear(OptionalPasses);

        ActiveCalls.Clear();
        ActiveCallCounts.Clear();
        OpenCalls.Clear();
        OpenCallSpans.Clear();
        FailedCalls = null;
        CallsThisPass = 0;
        CallMemoThreshold = long.MaxValue;
        CallMemoKey.Clear();
        CallMemoAncestors.Clear();
        VerbMarks.Clear();
        CallMemoHits = 0;
        ReachedLow = int.MaxValue;
        ReachedHigh = int.MinValue;
        SearchAnchor = 0;
        OperationStart = 0;
        MatchPos = 0;
        BestMatchPos = 0;
        BestTextPos = 0;
        BestMatchGroups = null;
        Array.Clear(BestFuzzyCounts);
        BestFuzzyChanges.Clear();
        BestTotalErrors = 0;
        BestTotalCost = 0;
        MaxErrors = 0;
        MaxCost = 0;
        TotalErrors = 0;
        TotalCost = 0;
        FewestErrors = 0;
        CaptureChange = 0;
        SectionFrame = -1;
        ReqEnd = 0;
        LastIndex = 0;
        LastGroup = 0;
        Iterations = 0;
        ForkBudgetSpent = false;
        Forks = 0;
        ForkAttemptStart = 0;
        SkippedAttemptVerb = NoSkippedVerb;
#if DEBUG
        ForkBudgetAnswered = false;
#endif
        TooFewErrors = false;
        FoundMatch = false;
        Array.Clear(FuzzyCounts);
        FuzzyNode = null;
        RetriedAWholeFoldEdit = false;
        FuzzyChanges.Clear();

        VisibleCaptures = visibleCaptures;
        SkipsCaptures = false;
        MatchAll = matchAll;
        ReqPos = -1;
        ScreenSliceStart = 0;
        ScreenSliceEnd = -1;
        ScreenFrom = 0;
        ScreenFound = 0;
        ScreenFoundFarthest = false;
        ScreenClearedLow = 1;
        ScreenClearedHigh = 0;
        FilterMemory?.Reset();
        IsFuzzy = pattern.IsFuzzy;
        AnchorBanScratch = null;

        // Adjust boundaries.
        start = ClampIndex(start, text.Length);
        end = ClampIndex(end, text.Length);

        if (end < start)
        {
            end = start;
        }

        Overlapped = overlapped;
        DoSearchStart = pattern.DoSearchStart;
        MinWidth = pattern.MinWidth;
        Encoding = pattern.Encoding;

        // Open start and closed end bounds, like in re module.
        TextStart = 0;
        TextEnd = end;

        SliceStart = start;
        SliceEnd = end;
        InitialSliceStart = start;
        InitialSliceEnd = end;

        Reverse = (pattern.Flags & RegexFlags.Reverse) != 0;

        if (partial)
        {
            PartialSide = Reverse ? PartialLeft : PartialRight;
        }
        else
        {
            PartialSide = PartialNone;
        }

        TextPos = Reverse ? SliceEnd : SliceStart;

        // Point to the final newline and line separator if it's at the end of the string, otherwise
        // just -1.
        FinalNewline = -1;
        FinalLineSep = -1;
        int finalPos = PrevPos(TextEnd);
        if (finalPos >= 0)
        {
            uint ch = CharAt(finalPos);
            if (ch == 0x0A)
            {
                // The string ends with LF.
                FinalNewline = finalPos;
                FinalLineSep = finalPos;

                // Does the string end with CR/LF?
                finalPos = PrevPos(finalPos);
                if (finalPos >= 0 && CharAt(finalPos) == 0x0D)
                {
                    FinalLineSep = finalPos;
                }
            }
            else if (Encodings.IsLineSep(Encoding, ch))
            {
                // The string doesn't end with LF, but it could be another kind of line separator.
                FinalLineSep = finalPos;
            }
        }

        // If the 'new' behaviour is enabled then split correctly on zero-width matches.
        Version0 = (pattern.Flags & RegexFlags.Version1) == 0;
        MustAdvance = false;

        Timeout = limits.TimeoutTicks;
        StartTime = limits.TimeoutTicks == NoTimeout ? 0 : Stopwatch.GetTimestamp();
        Cancellation = limits.Cancellation;
        ExpiresOnCheck = limits.ExpiresOnCheck;
        ClockChecks = 0;

        // NOT PORTED: search_positions, which only search_start reads (Phase 7).
    }

    /// <summary>
    /// Upstream <c>state_fini</c> (<c>upstream/src/_regex.c</c> line 18662), reduced to returning
    /// the stacks' rented buffers. A state <see cref="MatchStateCache.Rent"/> gave out goes back to
    /// that cache instead, as upstream's <c>state_fini</c> hands its storage back to the pattern, so
    /// a caller disposes a rented state exactly as it disposes a built one, and exactly once.
    /// </summary>
    public void Dispose()
    {
        Lease++;

        if (Cache is { } cache)
        {
            cache.Return(this);
        }
        else
        {
            ReturnBuffers();
        }
    }

    /// <summary>Upstream's limit on the stack storage a pattern keeps between calls, 64 KB.</summary>
    private const int _cachedStackLimit = 0x10000;

    internal void ReturnBuffers()
    {
        // The state stays usable: a stack that has given its buffer back rents another on its
        // next push, which is what lets MatchStateCache keep it.
        Sstack.Dispose();
        Bstack.Dispose();
        Pstack.Dispose();
    }

    /// <summary>
    /// Lets go of everything this state holds that belongs to the call rather than the pattern, so
    /// that a state kept by <see cref="MatchStateCache"/> does not keep the caller's subject or
    /// cancellation token alive after the call has returned.
    /// </summary>
    internal void Release()
    {
        // The stacks keep their buffers, as upstream's state_fini keeps the stack's storage on the
        // pattern up to 64 KB (upstream/src/_regex.c :18684-18700). Giving them back to
        // ArrayPool<byte>.Shared after every call made a warm call allocate whenever another thread
        // had taken them from the pool's shared stacks in between: 280 B at a time in the parallel
        // native-AOT run, and deterministically in AllocationTests (2026-09-25).
        WMarks.Clear();
        LookaroundSuccesses.Clear();
        InsideW = false;
        NothingHeldBeyondTheEdge = false;
        AttemptTouchedTheTextToCome = false;
        HasUnknownCaptures = false;
        Sstack.KeepUpTo(_cachedStackLimit);
        Bstack.KeepUpTo(_cachedStackLimit);
        Pstack.KeepUpTo(_cachedStackLimit);

        // NOT UPSTREAM (D17). The states belong to this call, so a kept state must not hold a
        // large set for the next one, as the stacks above give back what they grew past their limit.
        // The capacity, not the count: ResetGuards empties the set at each start position and
        // leaves it the size it grew to.
        if (EmptyIterationStates is { } states)
        {
            if (states.Capacity > 1024)
            {
                EmptyIterationStates = null;
            }
            else
            {
                states.Clear();
            }
        }

        Text = default;
        CaptureTexts.Reset(default);
        Array.Clear(_tailTextValues);
        _characterIndex = null;
        BestMatchGroups = null;
        Cancellation = default;

        // The failed-call memo belongs to one pass, so a state waiting in the cache holds none.
        FailedCalls = null;
    }

    /// <summary>
    /// Upstream's <c>char_at</c> function pointer (<c>upstream/src/_regex.c</c> line 18408), which
    /// reads one whole codepoint. A surrogate pair is decoded here, so a non-BMP character is one
    /// character to every opcode just as it is one to upstream's Python <c>str</c>. An unpaired
    /// surrogate reads as itself, which is what a Python <c>str</c> holding one does.
    /// </summary>
    /// <remarks>
    /// A pair is never read across either edge of the caller's slice: not past <see cref="TextEnd"/>,
    /// and not across <see cref="InitialSliceStart"/> when the caller's <c>beginning</c> falls
    /// between the two halves. See <see cref="PrevPos"/> for why.
    /// </remarks>
    /// <param name="pos">The position, a UTF-16 code unit index.</param>
    /// <returns>The codepoint there.</returns>
    internal uint CharAt(int pos)
    {
        ReadOnlySpan<char> text = Text.Span;
        char first = text[pos];
        if (
            char.IsHighSurrogate(first)
            && pos + 1 < TextEnd
            && char.IsLowSurrogate(text[pos + 1])
            && pos + 1 != InitialSliceStart
        )
        {
            return (uint)char.ConvertToUtf32(first, text[pos + 1]);
        }

        return first;
    }

    /// <summary>
    /// One codepoint forward from <paramref name="pos"/>: upstream's <c>++text_pos</c> and
    /// <c>text_pos += node-&gt;step</c> when the step is positive.
    /// </summary>
    /// <param name="pos">The position.</param>
    /// <returns>The next position.</returns>
    internal int NextPos(int pos)
    {
        WorkCounter.CharacterWalked();

        ReadOnlySpan<char> text = Text.Span;
        return
            pos + 1 < TextEnd
            && char.IsHighSurrogate(text[pos])
            && char.IsLowSurrogate(text[pos + 1])
            && pos + 1 != InitialSliceStart
            ? pos + 2
            : pos + 1;
    }

    /// <summary>
    /// One codepoint back from <paramref name="pos"/>: upstream's <c>--text_pos</c> and
    /// <c>char_at(text_pos - 1)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asymmetric with <see cref="NextPos"/> and <see cref="CharAt"/> on purpose, and the asymmetry
    /// is a decision rather than an oversight (S26, DECISIONS 2026-09-01). Those two refuse to pair
    /// a high and a low surrogate when the low one is at or past <see cref="TextEnd"/>; this one
    /// pairs regardless of any bound. So above <c>TextEnd</c> the two disagree about where a
    /// character starts.
    /// </para>
    /// <para>
    /// Left that way because no position above <c>TextEnd</c> is reachable from the engine - S25
    /// surfaced the difference by sweeping positions directly, not through a match - and because the
    /// bound this would have to test sits in the inner loop of every reverse step and every
    /// <see cref="CharBefore"/>. What the *public* surface does when <c>beginning</c>/<c>length</c>
    /// cuts through a surrogate pair is a separate question, and it is settled and pinned: the slice
    /// ends on a lone high surrogate, which matches as one character exactly as it does in a Python
    /// <c>str</c> holding one. See
    /// <c>MatchSpineTests.A_length_that_cuts_a_surrogate_pair_leaves_a_lone_surrogate_that_matches_as_one_character</c>.
    /// </para>
    /// <para>
    /// The same rule at the other edge is enforced here, in <see cref="NextPos"/> and in
    /// <see cref="CharAt"/>: when the caller's <c>beginning</c> (<see cref="InitialSliceStart"/>)
    /// falls between the two halves of a pair, the cut leaves two lone surrogates, and no step or
    /// read joins them. Without it the two directions disagreed about where characters start
    /// inside the slice: forwards the slice opens with a lone low surrogate, backwards
    /// <c>PrevPos</c> stepped from one past it over the whole pair to one BELOW the slice start.
    /// A greedy repeat retreating to its limit then walked past that limit, which it tests for
    /// equality, and never stopped: <c>(?:.)*x</c> fully matched against "\U00010428xc" at (1, 3)
    /// hung. A Python <c>str</c> holding the two halves as separate codepoints is exactly the
    /// text a cut pair denotes, and a codepoint <c>pos</c> can fall between them, so upstream
    /// answers the rule: see <c>SliceSplitsSurrogatePairTests</c>. It is the initial slice start
    /// and not <see cref="SliceStart"/>, which a lookaround or a verb moves mid-match: the cut is
    /// a property of the caller's text, and the characters must not change shape during a match.
    /// </para>
    /// </remarks>
    /// <param name="pos">The position.</param>
    /// <returns>The previous position, which may be -1 when <paramref name="pos"/> is 0.</returns>
    internal int PrevPos(int pos)
    {
        WorkCounter.CharacterWalked();

        ReadOnlySpan<char> text = Text.Span;
        return
            pos >= 2
            && char.IsLowSurrogate(text[pos - 1])
            && char.IsHighSurrogate(text[pos - 2])
            && pos - 1 != InitialSliceStart
            ? pos - 2
            : pos - 1;
    }

    /// <summary>Upstream's <c>char_at(state-&gt;text, text_pos - 1)</c>, over whole codepoints.</summary>
    /// <param name="pos">The position to look back from, which must be greater than 0.</param>
    /// <returns>The codepoint before it.</returns>
    internal uint CharBefore(int pos) => CharAt(PrevPos(pos));

    /// <summary>
    /// Starts the time budget again from now. Not upstream's: its one scanner state times the whole
    /// walk, and only this port's lazy walks time each step (see <c>Iteration.Enumerate</c>).
    /// </summary>
    internal void RestartClock()
    {
        if (Timeout != NoTimeout)
        {
            StartTime = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>
    /// NOT UPSTREAM'S, D11: sets <see cref="Bstack"/>'s count, which is how every truncation of the
    /// backtracking stack is made (upstream assigns <c>bstack.count</c> directly).
    /// </summary>
    /// <remarks>
    /// The one check is A7: no live w-mark lies at or above the new count, so the caller must have
    /// dropped the marks it discards. The design's one exception, an unblocked construct's own-frame
    /// cut taint, which moves to the entry its cut pushes, needs nothing here: the frame lies below
    /// the count its cut sets, and the cut drops the taint before it pops the frame and adds it
    /// again at the end entry (<c>Matcher.DropOwnCutTaint</c>, build chunk 4). The check is Debug
    /// only, so a Release build pays nothing here.
    /// </remarks>
    /// <param name="count">The new count, at most the current one.</param>
    internal void SetBstackCount(int count)
    {
        Debug.Assert(count <= Bstack.Count, "a truncation never grows the backtracking stack");
        Debug.Assert(NoWMarkAtOrAbove(count), "D11 A7: a truncation discards no live w-mark");
        Bstack.Count = count;
    }

    /// <summary>NOT UPSTREAM'S, D11: counts one second-pass event. Compiled away outside Debug builds.</summary>
    /// <param name="partialEvent">The event.</param>
    [Conditional("DEBUG")]
    internal void NotePartialEvent(PartialEvent partialEvent) => PartialEventCounts![(int)partialEvent]++;

    /// <summary>
    /// NOT UPSTREAM'S, D11: names the mechanism whose run-out the matcher takes next (Debug only).
    /// </summary>
    /// <param name="cause">The mechanism's event.</param>
    [Conditional("DEBUG")]
    internal void NoteRunOutCause(PartialEvent cause) => _pendingRunOutCause = cause;

    /// <summary>
    /// NOT UPSTREAM'S, D11: a run-out was taken; when it answers the partial, the call records
    /// <see cref="PartialEvent.RunOutAnswered"/> and, if a mechanism produced it,
    /// <see cref="PartialEvent.RunOutAnsweredByLazyPastTheEdge"/> or
    /// <see cref="PartialEvent.RunOutAnsweredByGreedyBodyKept"/> (Debug only). The cause is cleared
    /// either way, so the answer's label belongs to the run-out that produced it.
    /// </summary>
    /// <param name="answered">Whether the run-out answers the partial.</param>
    [Conditional("DEBUG")]
    internal void NoteRunOutTaken(bool answered)
    {
        if (answered)
        {
            NotePartialEvent(PartialEvent.RunOutAnswered);
            if (_pendingRunOutCause == PartialEvent.LazyRepeatPastTheEdge)
            {
                NotePartialEvent(PartialEvent.RunOutAnsweredByLazyPastTheEdge);
            }
            else if (_pendingRunOutCause == PartialEvent.GreedyBodyRunOutKept)
            {
                NotePartialEvent(PartialEvent.RunOutAnsweredByGreedyBodyKept);
            }
        }

        _pendingRunOutCause = null;
    }

    /// <summary>NOT UPSTREAM'S, D11: how many times an event has happened in this call.</summary>
    /// <param name="partialEvent">The event.</param>
    /// <returns>The count; always 0 outside Debug builds.</returns>
    internal long PartialEventCount(PartialEvent partialEvent) => PartialEventCounts?[(int)partialEvent] ?? 0;

    /// <summary>NOT UPSTREAM'S, D11: records a w-mark (design section 4.2).</summary>
    /// <param name="position">The index of the backtracking-stack entry the mark belongs to.</param>
    /// <param name="kind">What the mark records.</param>
    internal void AddWMark(int position, WMarkKind kind)
    {
        Debug.Assert(PartialSide != PartialNone, "D11 A6: only a partial call's second pass marks");

        // Marks are nearly always made at the top of the stack; a verb taint goes to its scope's
        // frame, lower down, and sits after any mark already at that position.
        int index = WMarks.Count;
        while (index > 0 && WMarks[index - 1].Position > position)
        {
            index--;
        }

        WMarks.Insert(index, new WMark(position, kind));
        Debug.Assert(WMarksAreInOrder(), "D11 A8: w-marks sorted, each below the backtracking stack's count");
    }

    /// <summary>NOT UPSTREAM'S, D11: drops every w-mark at or above a position.</summary>
    /// <param name="position">The lowest position dropped.</param>
    internal void DropWMarksFrom(int position)
    {
        int keep = WMarks.Count;
        while (keep > 0 && WMarks[keep - 1].Position >= position)
        {
            keep--;
        }

        WMarks.RemoveRange(keep, WMarks.Count - keep);
        Debug.Assert(WMarksAreInOrder(), "D11 A8: w-marks sorted, each below the backtracking stack's count");
    }

    /// <summary>
    /// NOT UPSTREAM'S, D11 M2: records a continued success for the dedup, keeping
    /// <see cref="LookaroundSuccesses"/> in frame order.
    /// </summary>
    /// <remarks>
    /// Entries are nearly always made in frame order, because a frame is newer than every frame
    /// below it; the exception is a blocked outer lookaround that records its success after a
    /// nested one's, whose frame is higher (review of chunk 7, round 2), and that one is inserted
    /// below the nested entries. The order is what keeps the dedup linear: row X1 leaves one entry
    /// per iteration live, and scanning all of them at every look-up and drop made its second pass
    /// quadratic (2.5 s at n = 1000, 2026-10-03).
    /// </remarks>
    /// <param name="success">The success.</param>
    internal void AddLookaroundSuccess(LookaroundSuccess success)
    {
        int at = LookaroundSuccesses.Count;
        while (at > 0 && LookaroundSuccesses[at - 1].Frame > success.Frame)
        {
            DedupEntriesExamined++;
            at--;
        }

        LookaroundSuccesses.Insert(at, success);
        Debug.Assert(
            (at == 0 || LookaroundSuccesses[at - 1].Frame <= success.Frame)
                && (at == LookaroundSuccesses.Count - 1 || LookaroundSuccesses[at + 1].Frame > success.Frame),
            "D11 A12: the dedup entries are in frame order"
        );
    }

    /// <summary>
    /// NOT UPSTREAM'S, D11 M2: whether a success with the same continuation was already continued
    /// from the same frame (the dedup). Reads only the entries at or above the frame, from the end.
    /// </summary>
    /// <param name="success">The success.</param>
    /// <returns><see langword="true"/> if an entry of the same frame leads where it does.</returns>
    internal bool HasSameContinuation(LookaroundSuccess success)
    {
        for (int i = LookaroundSuccesses.Count - 1; i >= 0 && LookaroundSuccesses[i].Frame >= success.Frame; i--)
        {
            DedupEntriesExamined++;
            if (success.SameContinuation(LookaroundSuccesses[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>NOT UPSTREAM'S, D11 A12: drops the dedup entries of the frames at or above a position.</summary>
    /// <remarks>
    /// The entries are in frame order (<see cref="AddLookaroundSuccess"/>), so the ones dropped are
    /// a run at the end. They are made only by a blocked positive end in a partial call's second
    /// pass (<c>Matcher.EndALookaroundInTheSecondPass</c>), each for a frame still on the
    /// backtracking stack, and the attempt's FAILURE arm and <see cref="InitMatch"/> clear them.
    /// Every caller asks <c>LookaroundSuccesses.Count != 0</c> first, so a call without partial
    /// pays that test alone.
    /// </remarks>
    /// <param name="position">The lowest frame position dropped.</param>
    internal void DropLookaroundSuccessesFrom(int position)
    {
        int keep = LookaroundSuccesses.Count;
        while (keep > 0 && LookaroundSuccesses[keep - 1].Frame >= position)
        {
            DedupEntriesExamined++;
            keep--;
        }

        LookaroundSuccesses.RemoveRange(keep, LookaroundSuccesses.Count - keep);
        Debug.Assert(
            keep == 0 || LookaroundSuccesses[keep - 1].Frame < position,
            "D11 A12: no dedup entry outlives its frame"
        );
    }

    /// <summary>A7's test: whether every live w-mark lies below <paramref name="count"/>.</summary>
    /// <param name="count">A backtracking-stack count.</param>
    /// <returns><see langword="true"/> if no mark would be left above that count.</returns>
    internal bool NoWMarkAtOrAbove(int count) => WMarks.Count == 0 || WMarks[^1].Position < count;

    /// <summary>A8's test: the w-marks are sorted by position and each lies below the stack's count.</summary>
    /// <returns><see langword="true"/> if they are.</returns>
    internal bool WMarksAreInOrder()
    {
        for (int i = 0; i < WMarks.Count; i++)
        {
            if (WMarks[i].Position < 0 || (i > 0 && WMarks[i - 1].Position > WMarks[i].Position))
            {
                return false;
            }
        }

        return NoWMarkAtOrAbove(Bstack.Count);
    }

    /// <summary>
    /// Upstream <c>init_match</c> (<c>upstream/src/_regex.c</c> line 3404).
    /// </summary>
    internal void InitMatch()
    {
        // Reset the stacks. A pass that succeeded leaves its marks with its stack (D11).
        WMarks.Clear();
        LookaroundSuccesses.Clear();
        Sstack.Reset();
        SetBstackCount(0);
        Pstack.Reset();

        SearchAnchor = TextPos;
        MatchPos = TextPos;

        // Clear the groups.
        ClearGroups();

        // Reset the guards.
        ResetGuards();

        // Clear the counts and cost for matching.
        if (IsFuzzy)
        {
            Array.Clear(FuzzyCounts);
            FuzzyNode = null;
            RetriedAWholeFoldEdit = false;
            FuzzyChanges.Clear();
        }

        TotalErrors = 0;
        TotalCost = 0;
        FoundMatch = false;
        CaptureChange = 0;
        Iterations = 0;
        ForkBudgetSpent = false;
        Forks = 0;
        ForkAttemptStart = 0;
        SkippedAttemptVerb = NoSkippedVerb;

        // NOT UPSTREAM'S (the failed-call memo): a fresh set for the pass. A pattern that cannot use
        // the memo never leaves the threshold or the set other than Reset leaves them, so it pays
        // one test here. See FailedCalls and CallMemoThreshold.
        CallsThisPass = 0;
        if (Pattern.UseCallMemo)
        {
            InitCallMemo();
        }
    }

    /// <summary>
    /// The failed-call memo's part of <see cref="InitMatch"/>, for a pattern that can use it: empties
    /// <see cref="FailedCalls"/> and sets <see cref="CallMemoThreshold"/> for the pass.
    /// </summary>
    /// <remarks>
    /// Clear costs the table's capacity, so a set that grew large once would tax every later pass;
    /// a fresh one is allocated on demand instead. The same rule as <c>RepeatData.ClearMemo</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitCallMemo()
    {
        if (Pattern.SkipCallMemo || PartialSide != PartialNone)
        {
            CallMemoThreshold = long.MaxValue;
        }
        else
        {
            CallMemoThreshold = Pattern.EagerCallMemo ? 0 : ((long)SliceEnd - SliceStart + 1) * Pattern.GroupCallSites;
        }

        if (FailedCalls is not { Count: > 0 })
        {
            return;
        }

        if (FailedCalls.Count > 4096)
        {
            FailedCalls = null;
        }
        else
        {
            FailedCalls.Clear();
        }
    }

    /// <summary>
    /// Moves the scan on from the match now in the state, so that the next <c>do_match</c> looks in
    /// the right place. Upstream spells this identically in three loops -
    /// <c>scanner_search_or_match</c> (<c>upstream/src/_regex.c</c> line 20903),
    /// <c>pattern_findall</c> (<c>:22470</c>) and, without the overlapped half it can never take,
    /// <c>pattern_subx</c> (<c>:22047</c>) and <c>pattern_split</c> (<c>:22326</c>) - so it is one
    /// method here and every caller uses it.
    /// </summary>
    /// <remarks>
    /// The overlapped branch steps one <b>codepoint</b> from where the match started, not one code
    /// unit and not from where it ended, because upstream indexes the subject by codepoint. A step
    /// off either end of the slice is left to show as a <see cref="TextPos"/> outside
    /// <see cref="SliceStart"/>..<see cref="SliceEnd"/>, which <c>do_match</c> tests on the next
    /// turn (<c>upstream/src/_regex.c</c> line 18128), so the matcher catches it rather than this.
    /// </remarks>
    internal void AdvancePastMatch()
    {
        if (Overlapped)
        {
            // Advance one character.
            TextPos = Reverse ? PrevPos(MatchPos) : NextPos(MatchPos);
            MustAdvance = false;
        }
        else
        {
            // Don't allow 2 contiguous zero-width matches.
            MustAdvance = TextPos == MatchPos;
        }
    }

    /// <summary>
    /// Upstream <c>push_fuzzy_counts</c> (<c>upstream/src/_regex.c</c> line 2480), which pushes
    /// nothing at all for a pattern that is not fuzzy - so the matching pop must be skipped too, and
    /// every caller of both is a pair.
    /// </summary>
    /// <remarks>
    /// <b>The length of <see cref="FuzzyChanges"/> goes on the stack beside the counts, which
    /// upstream does not do (S48b).</b> Upstream saves and restores the counts as a BLOCK and unwinds
    /// the changes one item at a time (<c>record_fuzzy</c>/<c>unrecord_fuzzy</c>,
    /// <c>:9768</c>/<c>:9801</c>), so any construct that throws a sub-attempt's backtracking away
    /// wholesale - an atomic group, a lookaround, a conditional - puts the counts back and leaves the
    /// sub-attempt's changes standing. That is ledger entry 11's defect class, and holding the two in
    /// step needs the pop to know where the list stood at the push.
    /// </remarks>
    /// <param name="stack">The stack to push onto.</param>
    /// <param name="fuzzyCounts">The counts to push.</param>
    internal void PushFuzzyCounts(ByteStack stack, ReadOnlySpan<long> fuzzyCounts)
    {
        if (!IsFuzzy)
        {
            return;
        }

        stack.PushBlock(MemoryMarshal.AsBytes(fuzzyCounts));
        stack.PushSize(FuzzyChanges.Count);
    }

    /// <summary>
    /// What <c>ATOMIC</c>, <c>CONDITIONAL</c> and <c>LOOKAROUND</c> save so that their eight
    /// <b>restoring</b> arms can put the fuzzy state back as if the body had never run: upstream
    /// <c>push_fuzzy_counts</c> (<c>upstream/src/_regex.c</c> line 2480), the change-list length
    /// beside it (see <see cref="PushFuzzyCounts"/>), the open section and the running totals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The open section is this port's (D44), and upstream does not save it.</b> The body always
    /// ends in the section it started in, except where a <c>(*PRUNE)</c> or <c>(*SKIP)</c> cuts the
    /// backtracking straight to the construct: that throws away the <c>FUZZY</c> entry of a section
    /// opened inside the body, whose backtrack arm is what would have put <see cref="FuzzyNode"/>
    /// back. The inner section then stayed the open one and its limits governed the rest of the
    /// enclosing section: <c>(?:(?!(?:a(*PRUNE)b){d&lt;=0})cd){e&lt;=2}</c> over 'ad' moved to (1, 2),
    /// in upstream too, where the same pattern without the verb matches at (0, 2) with one
    /// substitution.
    /// </para>
    /// <para>
    /// <b>The running totals are this port's too (D45).</b> <see cref="TotalErrors"/> and
    /// <see cref="TotalCost"/> are written by every <c>END_FUZZY</c> the body passes, so a body
    /// thrown away with a closed section in it left its errors in the totals while the counts went
    /// back: <c>(?:(?=(?:q){e&lt;=1})z)?a</c> over 'a' reached SUCCESS with no errors counted and a
    /// total of one. Upstream never restores <c>total_errors</c> at all (ledger entry 32).
    /// </para>
    /// </remarks>
    /// <param name="stack">The stack to push onto.</param>
    internal void PushSubAttemptFuzzyState(ByteStack stack)
    {
        if (!IsFuzzy)
        {
            return;
        }

        stack.PushSize(TotalErrors);
        stack.PushSize(TotalCost);
        stack.PushNode(FuzzyNode);
        PushFuzzyCounts(stack, FuzzyCounts);
    }

    /// <summary>
    /// Upstream <c>pop_fuzzy_counts</c> (line 2652) at the eight <b>restoring</b> sites: the counts,
    /// the change list, the open section and the running totals all go back to what they were at the matching
    /// <see cref="PushSubAttemptFuzzyState"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The truncation is this port's own and is the fix for ledger entry 11's mechanisms C and D. Use
    /// <see cref="PopFuzzyCountsMerging"/> at the THREE sites whose semantics are "merge" instead:
    /// both <c>END_FUZZY</c> arms, where the inner section's changes are part of the answer and must
    /// survive the outer counts being taken off the stack, and the <c>FUZZY</c> backtrack arm, where
    /// every item in the section has already unwound its own change so there is nothing left to
    /// truncate.
    /// </para>
    /// <para>
    /// <b>The eight restoring sites are the constructs that abandon a sub-attempt without
    /// backtracking through it</b> - <c>ATOMIC</c>/<c>END_ATOMIC</c>,
    /// <c>CONDITIONAL</c>/<c>END_CONDITIONAL</c> and <c>LOOKAROUND</c>/<c>END_LOOKAROUND</c>.
    /// </para>
    /// </remarks>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopSubAttemptFuzzyState(ByteStack stack)
    {
        if (!IsFuzzy)
        {
            return true;
        }

        if (
            !PopFuzzyCountsMerging(stack, FuzzyCounts, out long changeCount)
            || !stack.PopNode(Pattern, out Node? fuzzyNode)
            || !stack.PopSize(out long totalCost)
            || !stack.PopSize(out long totalErrors)
        )
        {
            return false;
        }

        if (!Pattern.KeepSectionOpenAfterAVerb)
        {
            FuzzyNode = fuzzyNode;
        }

        if (!Pattern.KeepDiscardedTotals)
        {
            TotalErrors = totalErrors;
            TotalCost = totalCost;
        }

        TruncateFuzzyChanges(changeCount);

        return true;
    }

    /// <summary>
    /// Upstream <c>pop_fuzzy_counts</c> (line 2652), <b>merging</b>: the counts go back and the change
    /// list is left exactly as it stands.
    /// </summary>
    /// <param name="stack">The stack to pop from.</param>
    /// <param name="fuzzyCounts">Receives the counts, and is left alone for a non-fuzzy pattern.</param>
    /// <param name="changeCount">Receives the change-list length the push recorded.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFuzzyCountsMerging(ByteStack stack, Span<long> fuzzyCounts, out long changeCount)
    {
        changeCount = 0;

        return !IsFuzzy || (stack.PopSize(out changeCount) && stack.PopBlock(MemoryMarshal.AsBytes(fuzzyCounts)));
    }

    /// <summary>
    /// Drops every change recorded since the list stood at <paramref name="changeCount"/> entries.
    /// </summary>
    /// <remarks>
    /// Never grows the list: a restore whose sub-attempt unwound BELOW the push point has nothing to
    /// put back, and quietly inventing entries would turn a contradiction into a wrong answer.
    /// </remarks>
    /// <param name="changeCount">The length the list stood at.</param>
    internal void TruncateFuzzyChanges(long changeCount)
    {
        if (FuzzyChanges.Count > changeCount)
        {
            FuzzyChanges.RemoveRange((int)changeCount, FuzzyChanges.Count - (int)changeCount);
        }
    }

    /// <summary>Upstream <c>record_fuzzy</c> (line 9768), less its hand-grown array growth.</summary>
    /// <param name="fuzzyType">Which error was used.</param>
    /// <param name="textPos">Where it was used.</param>
    internal void RecordFuzzy(int fuzzyType, int textPos) =>
        FuzzyChanges.Add(new FuzzyChange((byte)fuzzyType, textPos));

    /// <summary>Upstream <c>unrecord_fuzzy</c> (line 9801), which is <c>--count</c>.</summary>
    /// <remarks>
    /// Upstream decrements an unsigned <c>count</c>, so unrecording one change more than was ever
    /// recorded wraps it to an enormous number and the engine then walks off the end of the list.
    /// This throws instead, which is the same bug made visible rather than a second, quieter one.
    /// </remarks>
    internal void UnrecordFuzzy() => FuzzyChanges.RemoveAt(FuzzyChanges.Count - 1);

    /// <summary>Upstream <c>clear_groups</c> (<c>upstream/src/_regex.c</c> line 3369).</summary>
    /// <remarks>
    /// The capture arrays are kept and only the counts go to zero, exactly as upstream does: a
    /// retried match at the next start position reuses the storage it has already grown.
    /// </remarks>
    internal void ClearGroups()
    {
        foreach (GroupData group in Groups)
        {
            group.Count = 0;
            group.Current = -1;
        }
    }

    /// <summary>Upstream <c>reset_guards</c> (<c>upstream/src/_regex.c</c> line 3383).</summary>
    /// <remarks>
    /// The fuzzy-section and group-call halves (<c>:3392-3400</c>) are <b>never</b> to be ported, and
    /// now for the same reason. S30 landed group calls without the group-call half because upstream's
    /// <c>group_call_guard_list</c> is written in five places and read in none; S38 found
    /// <c>fuzzy_guards</c> to be exactly that shape too - allocated, cleared, reset here, freed, and
    /// never consulted. Both greps and their dates are in <see cref="Create"/> and in the
    /// "deliberately not ported" table in <c>docs/PORTMAP.md</c>. The fuzzy line read "waits for
    /// Phase 5" until S38 measured it.
    /// </remarks>
    /// <param name="keepFailureMemos">
    /// Whether to leave the body guard lists of failure-memo repeats as they are. <b>Not
    /// upstream</b>; see <see cref="PatternObject.KeepsFailureMemosAcrossAttempts"/>.
    /// </param>
    internal void ResetGuards(bool keepFailureMemos = false)
    {
        // Reset the guards for the repeats.
        for (int i = 0; i < Repeats.Length; i++)
        {
            RepeatData repeat = Repeats[i];

            if (!keepFailureMemos || !Pattern.RepeatInfoAt(i).FailureMemo)
            {
                repeat.BodyGuardList.Reset();
            }

            repeat.TailGuardList.Reset();
            repeat.KeyedTail?.Reset(newAttempt: true);
        }

        KeyedTailWrites = 0;

        // NOT UPSTREAM (D17). The states belong to runs of this attempt; a new start position
        // begins new runs, so they could never be read again.
        EmptyIterationStates?.Clear();
    }

    /// <summary>
    /// Which of a repeat's two guard lists a guard type names, or <see langword="null"/> when no
    /// guard of that type is active for that repeat. Upstream spells the same three lines out in
    /// each of <c>guard_repeat</c> (<c>upstream/src/_regex.c</c> line 9446),
    /// <c>guard_repeat_range</c> (<c>:9534</c>) and <c>is_repeat_guarded</c> (<c>:9559</c>).
    /// </summary>
    /// <remarks>
    /// The three differ in one respect that is <b>not</b> folded in here: <c>is_repeat_guarded</c>
    /// tests <c>guard_type == RE_STATUS_BODY</c> where the other two test
    /// <c>guard_type &amp; RE_STATUS_BODY</c>. Every call site passes exactly one of the two bits, so
    /// the two spellings agree, and <c>&amp;</c> is the one kept.
    /// </remarks>
    /// <param name="index">The repeat index.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <returns>The list, or <see langword="null"/> if this repeat needs no guard of that type.</returns>
    /// <param name="textPos">The position asked about or guarded (D85: a keyed tail's key reads it).</param>
    /// <param name="write">Whether the caller writes to the list rather than reads it (D85).</param>
    private GuardList? ActiveGuardList(int index, uint guardType, int textPos, bool write)
    {
        // Is a guard active here?
        if ((Pattern.RepeatInfoAt(index).Status & guardType) == 0)
        {
            return null;
        }

        // Which guard list?
        RepeatData repeat = Repeats[index];
        if ((guardType & NodeStatus.Body) != 0)
        {
            // NOT UPSTREAM (D85): see RepeatInfo.BodyHoldsKeyedTail.
            return Pattern.RepeatInfoAt(index).BodyHoldsKeyedTail ? null : repeat.BodyGuardList;
        }

        // NOT UPSTREAM (D85): a repeat inside a group whose parked start the key cannot find.
        if (Pattern.RepeatInfoAt(index).TailKey is { HasUnknownInside: true })
        {
            return null;
        }

        // NOT UPSTREAM (D85): a repeat whose tail reads group state keeps one list per key.
        return repeat.KeyedTail is null ? repeat.TailGuardList : KeyedTailGuardList(index, textPos, write);
    }

    /// <summary>
    /// NOT UPSTREAM (D85): how many keyed tail guard writes one attempt may make before further
    /// ones are skipped, which bounds the memory the keyed lists take (each write adds at most one
    /// span). Skipping a write only loses pruning. The same figure as the failure memo's cap.
    /// </summary>
    internal const long KeyedTailWriteCap = 1 << 20;

    /// <summary>NOT UPSTREAM (D85): keyed tail guard writes made in this attempt; see <see cref="KeyedTailWriteCap"/>.</summary>
    internal long KeyedTailWrites;

    /// <summary>
    /// NOT UPSTREAM (D85): where <see cref="TailGuardKeyOf"/> builds a key, so a lookup allocates
    /// nothing; as long as the longest key, and zero past the end of a shorter one.
    /// </summary>
    private readonly long[] _tailKeyScratch;

    /// <summary>The longest tail key any repeat of <paramref name="pattern"/> needs.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The length.</returns>
    private static int MaxTailKeyLength(PatternObject pattern)
    {
        int longest = 0;
        foreach (RepeatInfo info in pattern.RepeatInfoList)
        {
            if (info.TailKey is TailGuardKeySpec spec)
            {
                longest = Math.Max(longest, spec.KeyLength);
            }
        }

        return longest;
    }

    /// <summary>
    /// NOT UPSTREAM (D85): the tail guard list of repeat <paramref name="index"/> for the group
    /// state the rest of the match can read now, or <see langword="null"/> for a read under a key
    /// with no list yet (or a write past the cap).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tail guard at p records that the rest of the match, entered at p, failed. Upstream keys it
    /// on p alone and switches it off where <c>add_repeat_guards</c> sees the rest read a group,
    /// but that walk stops at the enclosing repeat's end and skips bounded bodies, so it leaves the
    /// guard on where a group is read (ledger entry 84). The rest of the match is a deterministic
    /// function of p and of the state it reads; the key is that state, as
    /// <see cref="TailGuardLiveness"/> narrows it: for each live group, what the rest can see of it,
    /// and for each enclosing repeat whose body captures a read group, whether its iteration has
    /// already made progress.
    /// </para>
    /// <para>
    /// The write is sound because a tail guard is written only when backtracking has undone the
    /// whole tail back to the repeat (the TailStart entry, and the one-character repeat's arm), so
    /// every capture and every repeat field the key reads is back to its value at the tail's entry:
    /// they are restored from the backtrack stack (<c>PushGroupStateData</c>, <c>PushRepeatData</c>)
    /// before that entry is popped.
    /// </para>
    /// </remarks>
    /// <param name="index">The repeat index.</param>
    /// <param name="textPos">The position the tail is entered at.</param>
    /// <param name="write">Whether the caller guards rather than asks.</param>
    /// <returns>The list.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private GuardList? KeyedTailGuardList(int index, int textPos, bool write)
    {
        KeyedTailGuards store = Repeats[index].KeyedTail!;

        // A fuzzy match never reads a repeat guard (IsRepeatGuarded), so its writes are skipped.
        if (IsFuzzy || (!write && store.Count == 0))
        {
            return null;
        }

        long[] key = TailGuardKeyOf(Pattern.RepeatInfoAt(index).TailKey!, textPos);
        return write ? store.ForWrite(key, mayAdd: KeyedTailWrites++ < KeyedTailWriteCap) : store.Find(key);
    }

    /// <summary>
    /// NOT UPSTREAM (D85): the last span whose text <see cref="CachedTextValue"/> looked up for each
    /// group, packed, and the value it got (0 for none: no text value is 0), so a long capture's
    /// text is hashed once rather than at every guard check.
    /// </summary>
    private readonly long[] _tailTextSpans;

    /// <summary>NOT UPSTREAM (D85): see <see cref="_tailTextSpans"/>.</summary>
    private readonly long[] _tailTextValues;

    /// <summary>NOT UPSTREAM (D85): <c>Matcher.TextValue</c> of a group's span, cached per group.</summary>
    /// <param name="group">The group, as an index into <see cref="Groups"/>.</param>
    /// <param name="span">Its current span.</param>
    /// <returns>The value.</returns>
    private long CachedTextValue(int group, GroupSpan span)
    {
        long packed = ((long)span.Start << 32) | (uint)span.End;
        if (_tailTextValues[group] != 0 && _tailTextSpans[group] == packed)
        {
            return _tailTextValues[group];
        }

        long value = Matcher.TextValue(this, span.Start, span.End - span.Start);
        _tailTextSpans[group] = packed;
        _tailTextValues[group] = value;
        return value;
    }

    /// <summary>NOT UPSTREAM (D85): the key of a tail entered at <paramref name="textPos"/> now.</summary>
    /// <remarks>
    /// Per group: -1 when unset; else its exact span (made negative) when a new capture in the
    /// tail could equal the current span, which decides whether the capture bumps the counter
    /// (the repeat inside the group's brackets, a capture against the direction, or a span that
    /// starts at or after p, or in reverse ends at or before p); else its text for a
    /// backreferenced group (<c>Matcher.TextValue</c>), else 0. Per enclosing progress repeat: 1
    /// when its iteration has already advanced or changed a capture, else 0.
    /// </remarks>
    /// <param name="spec">The repeat's key spec.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>The key, in <see cref="_tailKeyScratch"/>.</returns>
    private long[] TailGuardKeyOf(TailGuardKeySpec spec, int textPos)
    {
        long[] key = _tailKeyScratch;
        Array.Clear(
            key,
            spec.Groups.Length + spec.ProgressRepeats.Length,
            key.Length - spec.Groups.Length - spec.ProgressRepeats.Length
        );
        // The parked start of each group the repeat is inside: the tail's capture of it runs
        // from there, so the text it captures depends on it. `.?(a*)(?:b\1)?c` over aabaac
        // reaches the tail of a* at 2 with g1 open at 1 and then open at 0, and only the second
        // matches. Outside fuzzy sections and group calls the save stack holds exactly the
        // parked starts of the groups open around the repeat, innermost on top.
        int extra = spec.Groups.Length + spec.ProgressRepeats.Length;
        for (int i = 0; i < spec.InsideDepths.Length; i++)
        {
            int depth = spec.InsideDepths[i];
            if (depth >= 0)
            {
                int offset = Sstack.Count - (sizeof(long) * (depth + 1));
                key[extra + i] = offset >= 0 ? Sstack.SizeAt(offset) : long.MinValue;
            }
        }

        for (int i = 0; i < spec.Groups.Length; i++)
        {
            GroupData group = Groups[spec.Groups[i]];
            byte mode = spec.Modes[i];
            if (group.Current < 0)
            {
                key[i] = -1;
                continue;
            }

            GroupSpan span = group.Captures[group.Current];
            bool mayMeet =
                spec.ExactSpans
                || (mode & TailGuardLiveness.Inside) != 0
                || (
                    (mode & TailGuardLiveness.Span) != 0 && (spec.Forward ? span.Start >= textPos : span.End <= textPos)
                );
            if (mayMeet)
            {
                key[i] = long.MinValue | ((long)span.Start << 32) | (uint)span.End;
            }
            else
            {
                key[i] = (mode & TailGuardLiveness.Text) != 0 ? CachedTextValue(spec.Groups[i], span) : 0;
            }
        }

        for (int j = 0; j < spec.ProgressRepeats.Length; j++)
        {
            RepeatData around = Repeats[spec.ProgressRepeats[j]];
            key[spec.Groups.Length + j] = around.Start != textPos || around.CaptureChange != CaptureChange ? 1 : 0;
        }

        return key;
    }

    /// <summary>
    /// NOT UPSTREAM (D85): the part of a guarded span around <paramref name="textPos"/>
    /// over which the current state's key is the same as at <paramref name="textPos"/>. The key moves
    /// only where a span-keyed group's span starts (forward) or ends (reverse), and at an enclosing
    /// progress repeat's iteration start while its counter has not moved.
    /// </summary>
    /// <param name="spec">The repeat's key spec.</param>
    /// <param name="textPos">The position asked about.</param>
    /// <param name="span">The guarded span holding it.</param>
    /// <returns>The clipped span.</returns>
    private GuardSpan ClipTailSpan(TailGuardKeySpec spec, int textPos, GuardSpan span)
    {
        int low = span.Low;
        int high = span.High;
        if (!spec.ExactSpans)
        {
            for (int i = 0; i < spec.Groups.Length; i++)
            {
                byte mode = spec.Modes[i];
                GroupData group = Groups[spec.Groups[i]];
                if (group.Current < 0 || (mode & TailGuardLiveness.Span) == 0 || (mode & TailGuardLiveness.Inside) != 0)
                {
                    continue;
                }

                GroupSpan current = group.Captures[group.Current];
                if (spec.Forward)
                {
                    // Exact for y <= Start, text above.
                    if (low <= current.Start && current.Start < textPos)
                    {
                        low = current.Start + 1;
                    }

                    if (textPos <= current.Start && current.Start < high)
                    {
                        high = current.Start;
                    }
                }
                else
                {
                    // Exact for y >= End, text below.
                    if (low < current.End && current.End <= textPos)
                    {
                        low = current.End;
                    }

                    if (textPos < current.End && current.End <= high)
                    {
                        high = current.End - 1;
                    }
                }
            }
        }

        foreach (int r in spec.ProgressRepeats)
        {
            RepeatData around = Repeats[r];
            if (around.CaptureChange != CaptureChange)
            {
                continue;
            }

            int start = around.Start;
            if (start == textPos)
            {
                low = high = textPos;
            }
            else if (low <= start && start < textPos)
            {
                low = start + 1;
            }
            else if (textPos < start && start <= high)
            {
                high = start - 1;
            }
        }

        return new GuardSpan(low, high, span.Protect);
    }

    /// <summary>Upstream <c>guard_repeat</c> (<c>upstream/src/_regex.c</c> line 9446).</summary>
    /// <param name="index">The repeat index.</param>
    /// <param name="textPos">The position to guard.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    // AggressiveInlining (cold-arm round 3): BasicMatchCore calls this from its repeat arms, and the JIT
    // turned every one of those inlines down as "unprofitable" (runtime JitTracing events, 2026-10-14):
    // its inline policy scales profitability by (1 - locals / JitMaxLocalsToTrack), and that method
    // has hundreds of locals. They inline anyway once that limit is raised; the body is short.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GuardRepeat(int index, int textPos, uint guardType, bool protect) =>
        ActiveGuardList(index, guardType, textPos, write: true)?.Guard(textPos, protect);

    /// <summary>Upstream <c>guard_repeat_range</c> (line 9534).</summary>
    /// <param name="index">The repeat index.</param>
    /// <param name="loPos">The lowest position to guard.</param>
    /// <param name="hiPos">The highest, inclusive.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    internal void GuardRepeatRange(int index, int loPos, int hiPos, uint guardType, bool protect)
    {
        GuardList? guardList = ActiveGuardList(index, guardType, loPos, write: true);

        if (guardList is null)
        {
            return;
        }

        while (loPos <= hiPos)
        {
            loPos = guardList.GuardRange(loPos, hiPos, protect);
        }
    }

    /// <summary>
    /// Whether the body guard of repeat <paramref name="index"/> is kept as a failure memo on this
    /// call, so the position where the body matched is not marked. <b>Not upstream</b>; see
    /// <see cref="RepeatInfo.FailureMemo"/>.
    /// </summary>
    /// <remarks>
    /// A partial call's second pass keeps the memo too (D11 build chunk 9, design amendment 1). The
    /// exclusion came from the memo's design ("a partial result is neither success nor failure")
    /// with no witness, and it made the second pass exponential wherever the memo had made the
    /// first pass linear: `(?:a|a)+c` over 24 'a's and "bc" took 2.1 s. The memo needs one fact,
    /// that entering the body of the repeat at a position has one outcome whatever path arrived
    /// there, and nothing a second-pass path carries beyond the position breaks it for a memo
    /// repeat: a w-mark blocks only constructs older than itself, and a memo repeat's continuation
    /// holds no cut, lookaround or verb older than the repeat; the touch flag decides only what
    /// <c>Success</c> does, the same for every arrival; a run-out outside a body ends the attempt,
    /// and a memo repeat is never inside a body; unknown captures matter only to backreferences and
    /// group conditions, which switch the memo off. A 42,240-row differential grid gave identical
    /// answers with and without the exclusion (amendment 1).
    /// </remarks>
    /// <param name="index">The repeat index.</param>
    /// <returns><see langword="true"/> if a matched body leaves no mark.</returns>
    // AggressiveInlining (cold-arm round 3): BasicMatchCore calls this from its repeat arms, and the JIT
    // turned every one of those inlines down as "unprofitable" (runtime JitTracing events, 2026-10-14):
    // its inline policy scales profitability by (1 - locals / JitMaxLocalsToTrack), and that method
    // has hundreds of locals. They inline anyway once that limit is raised; the body is short.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool KeepsFailureMemo(int index) => Pattern.RepeatInfoAt(index).FailureMemo;

    /// <summary>Upstream <c>is_repeat_guarded</c> (line 9559).</summary>
    /// <remarks>
    /// The guards are switched off outright while fuzzy matching, which is upstream's own
    /// <c>|| state-&gt;is_fuzzy</c>: a fuzzy match may reach the same position again with a
    /// different error budget, so "already failed here" is not a sound conclusion.
    /// </remarks>
    /// <param name="index">The repeat index.</param>
    /// <param name="textPos">The position to ask about.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <returns><see langword="true"/> if that position is guarded for that repeat.</returns>
    // AggressiveInlining (cold-arm round 3): BasicMatchCore calls this from its repeat arms, and the JIT
    // turned every one of those inlines down as "unprofitable" (runtime JitTracing events, 2026-10-14):
    // its inline policy scales profitability by (1 - locals / JitMaxLocalsToTrack), and that method
    // has hundreds of locals. They inline anyway once that limit is raised; the body is short.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsRepeatGuarded(int index, int textPos, uint guardType)
    {
        if (IsFuzzy)
        {
            return false;
        }

        WorkCounter.GuardChecked();
        bool guarded = ActiveGuardList(index, guardType, textPos, write: false)?.IsGuarded(textPos) ?? false;
        NoteSecondPassGuard(guarded);

        return guarded;
    }

    /// <summary>
    /// <see cref="IsRepeatGuarded(int, int, uint)"/>, also giving the span that guards the
    /// position, so a caller can pass over the whole span at once. <b>Not upstream</b> (C5, G2):
    /// same answer and the same side effects, one lookup counted.
    /// </summary>
    /// <param name="index">The repeat index.</param>
    /// <param name="textPos">The position to ask about.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <param name="span">The protecting span holding <paramref name="textPos"/> when guarded.</param>
    /// <returns><see langword="true"/> if that position is guarded for that repeat.</returns>
    internal bool IsRepeatGuarded(int index, int textPos, uint guardType, out GuardSpan span)
    {
        span = default;
        if (IsFuzzy)
        {
            return false;
        }

        WorkCounter.GuardChecked();
        bool guarded =
            ActiveGuardList(index, guardType, textPos, write: false) is GuardList list
            && list.TryGetSpan(textPos, out span)
            && span.Protect;
        NoteSecondPassGuard(guarded);

        // NOT UPSTREAM (D85): a keyed tail's key can depend on the position as well (whether a span
        // starts at or after it, whether an enclosing iteration started there), so the rest of a
        // span written under this key need not be guarded under the key the caller would get at
        // those positions. The span is cut where the key would change (ClipTailSpan).
        if (
            guarded
            && (guardType & NodeStatus.Tail) != 0
            && Pattern.RepeatInfoAt(index).TailKey is { DependsOnPosition: true } clipSpec
        )
        {
            span = ClipTailSpan(clipSpec, textPos, span);
        }

        return guarded;
    }

    /// <summary>
    /// NOT UPSTREAM'S, D11 build chunk 9 (rule 11): counts a guard that held in a partial call's
    /// second pass, which the failure memo there makes possible for memo repeats.
    /// </summary>
    /// <param name="guarded">Whether the guard held.</param>
    [Conditional("DEBUG")]
    private void NoteSecondPassGuard(bool guarded)
    {
        if (guarded && PartialSide != PartialNone)
        {
            NotePartialEvent(PartialEvent.SecondPassGuardHeld);
        }
    }

    /// <summary>
    /// Upstream <c>save_capture</c> (<c>upstream/src/_regex.c</c> line 9249).
    /// </summary>
    /// <remarks>
    /// <b>Indexed by the public group number, not the private one</b>, which is upstream's own
    /// asymmetry and is kept: <c>START_GROUP</c> and <c>END_GROUP</c> read and write
    /// <c>group-&gt;current</c> through <c>private_index</c> and then call this, which appends
    /// through <c>public_index</c>. The two are the same number for every group except a
    /// branch-reset one, so changing it here would silently alter branch-reset behaviour that
    /// Phase 4 has yet to test.
    /// </remarks>
    /// <param name="privateIndex">Upstream's <c>private_index</c>, which it also does not use.</param>
    /// <param name="publicIndex">The public group number, one-based.</param>
    /// <param name="span">The span to append.</param>
    internal void SaveCapture(int privateIndex, int publicIndex, GroupSpan span)
    {
        _ = privateIndex;
        WorkCounter.CaptureSaved();

        // Capture group indexes are 1-based (excluding group 0, which is the entire matched string).
        GroupData group = Groups[publicIndex - 1];

        if (group.Count >= group.Captures.Length)
        {
            int newCapacity = group.Captures.Length * 2;

            if (newCapacity == 0)
            {
                newCapacity = 16;
            }

            Array.Resize(ref group.Captures, newCapacity);
        }

        group.Captures[group.Count++] = span;
    }

    /// <summary>Upstream <c>unsave_capture</c> (<c>upstream/src/_regex.c</c> line 9282).</summary>
    /// <param name="privateIndex">Upstream's <c>private_index</c>, which it also does not use.</param>
    /// <param name="publicIndex">The public group number, one-based.</param>
    internal void UnsaveCapture(int privateIndex, int publicIndex)
    {
        _ = privateIndex;

        GroupData group = Groups[publicIndex - 1];

        if (group.Count > 0)
        {
            --group.Count;
        }
    }

    /// <summary>
    /// Upstream <c>check_timed_out</c> (<c>upstream/src/_regex.c</c> line 2253), which upstream
    /// calls from <c>safe_check_cancel</c> alongside <c>PyErr_CheckSignals</c> - the second has no
    /// counterpart here, so this is the whole of the cancellation check.
    /// </summary>
    /// <returns><see langword="true"/> if the operation has run out of time.</returns>
    internal bool CheckTimedOut()
    {
        if (Timeout == NoTimeout)
        {
            // No timeout.
            return false;
        }

        // NOT UPSTREAM'S: the test seam. Read only here, after the no-budget test, so a call without
        // a budget never reaches it, and behind the polling gates, which admit one turn in 256 of
        // the matching loop plus each match's first attempt.
        if (ExpiresOnCheck != 0 && ++ClockChecks >= ExpiresOnCheck)
        {
            return true;
        }

        // Hasn't timed out yet.
        return Stopwatch.GetTimestamp() - StartTime >= Timeout;
    }

    /// <summary>
    /// Upstream's boundary adjustment, which <c>state_init_2</c> (<c>:18376</c>) and
    /// <c>get_limits</c> (<c>:21627</c>) spell out identically: a negative index counts back from
    /// the end, and the result is clamped to the string.
    /// </summary>
    /// <param name="value">The index.</param>
    /// <param name="length">The subject's length.</param>
    /// <returns>The clamped index.</returns>
    internal static int ClampIndex(int value, int length)
    {
        if (value < 0)
        {
            value += length;
        }

        if (value < 0)
        {
            return 0;
        }

        return value > length ? length : value;
    }
}

/// <summary>
/// NOT UPSTREAM (ledger entry 44's addendum): where a pass through an alternative with an alternative
/// written empty after it began (<see cref="MatchState.OptionalPasses"/>).
/// </summary>
/// <param name="TextPos">The text position the pass began at.</param>
/// <param name="Changes">How many fuzzy changes had been made when it began.</param>
/// <param name="CaptureChange">The state's capture change counter when it began.</param>
/// <param name="Verbs">
/// <see cref="MatchState.VerbsCrossed"/> when it began, so its end can tell which verbs it crossed.
/// </param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OptionalPassStart(int TextPos, int Changes, long CaptureChange, int Verbs);
