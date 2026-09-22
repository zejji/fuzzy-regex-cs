using System.Buffers;
using System.Diagnostics;
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
        var copies = new GroupData[groupCount];
        for (int g = 0; g < groupCount; g++)
        {
            copies[g] = groups[g].Copy();
        }

        return copies;
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

    /// <summary>Upstream <c>text</c> and <c>string</c>, which are one object here.</summary>
    internal string Text = string.Empty;

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

    /// <summary>Upstream <c>sstack</c>: the structure stack.</summary>
    internal readonly ByteStack Sstack;

    /// <summary>Upstream <c>bstack</c>: the backtracking stack.</summary>
    internal readonly ByteStack Bstack;

    /// <summary>Upstream <c>pstack</c>: the pruning stack.</summary>
    internal readonly ByteStack Pstack;

    /// <summary>
    /// NOT UPSTREAM'S: the group calls that are open right now, one key per call, as
    /// <c>(call index &lt;&lt; 32) | text position</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ledger entry 14's guard, and PCRE2's: a call that re-enters a group at a text position where
    /// a call of that same group is already open cannot consume anything before it arrives back
    /// where it started, so that path recurses for ever. PCRE2 answers the whole match with
    /// <c>PCRE2_ERROR_RECURSELOOP</c>, "nested recursion at the same subject position"; upstream has
    /// no guard at all and allocates until <c>MemoryError</c>.
    /// </para>
    /// <para>
    /// A set rather than a counter, because a key is only ever added when it is absent - that is
    /// what the guard tests. <see cref="OpenCalls"/> is the same information as a stack, and is what
    /// keeps the two in step; this is only here so the test itself costs O(1) on a recursion ten
    /// thousand deep.
    /// </para>
    /// </remarks>
    internal readonly HashSet<long> ActiveCalls = [];

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
    /// stack's count calls <c>Matcher.CloseCallsAbove</c>, which drops every entry whose frame that
    /// restore has just discarded.
    /// </para>
    /// <para>
    /// <see cref="Matcher"/>'s <c>start_match</c> clears both, which covers a whole attempt being
    /// abandoned.
    /// </para>
    /// </remarks>
    internal readonly List<(long Key, int SstackDepth)> OpenCalls = [];

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

    /// <summary>
    /// PCRE2's <c>hitend</c>: a word or grapheme boundary was decided at the truncation point of a
    /// partial match, so the answer it gave rests on text the caller has said is missing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This has no upstream counterpart. Upstream resolves such a boundary against the truncated
    /// text and is done with it, which is upstream issue 589 and ledger entry 21: 'True' is denied
    /// as a partial of <c>(?!(True|False)\b)(.*)</c> even though 'Truest' is a complete match, so a
    /// prefix whose completion exists is reported as impossible. S57d takes PCRE2's model instead,
    /// which pcre2partial(3) states in one line - under the soft option "the partial match is
    /// remembered, but matching continues as normal", and "if no complete match can be found,
    /// PCRE2_ERROR_PARTIAL is returned instead of PCRE2_ERROR_NOMATCH".
    /// </para>
    /// <para>
    /// <b>Setting this flag is all a predicate does; it never ends the match.</b> That is the whole
    /// difference from S50's reverted attempt, which returned a partial status from the predicate
    /// itself and so truncated a capture group that backtracking had not finished growing -
    /// <c>search(r'(\.+?)\1\b', '..', partial=True)</c> came back with group 1 at (0, 1) where
    /// upstream gives (0, 2). Only <see cref="Matcher.DoMatch"/> reads the flag, and only once the
    /// whole match has failed.
    /// </para>
    /// </remarks>
    internal bool HitEnd;

    /// <summary>
    /// Where the attempt that first set <see cref="HitEnd"/> began, which is the start of the span a
    /// hitend-derived partial reports.
    /// </summary>
    /// <remarks>
    /// Written once per match and not overwritten, so that the leftmost end-reaching attempt is the
    /// one reported. Measured on PCRE2 10.47, 2026-09-21,
    /// <c>tools/probes/pcre2-hitend-partial-span.py</c> section B: <c>a*c\B</c> over <c>'ac'</c>
    /// reaches the end from start 0 and again from start 1, and the answer is PARTIAL (0,2).
    /// Section A is the same question from the other side - <c>cd\B</c> over <c>'xabcd'</c> is
    /// PARTIAL (3,5), so the start is the attempt's and not the subject's.
    /// </remarks>
    internal int HitEndMatchPos;

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

    /// <summary>Upstream <c>req_pos</c>: where the required string matched, or -1.</summary>
    internal int ReqPos;

    /// <summary>Upstream <c>req_end</c>.</summary>
    internal int ReqEnd;

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

    /// <summary>Upstream <c>iterations</c>: how long since the last cancellation check.</summary>
    internal ushort Iterations;

    /// <summary>Upstream <c>overlapped</c>.</summary>
    internal bool Overlapped;

    /// <summary>Upstream <c>reverse</c>.</summary>
    internal bool Reverse;

    /// <summary>Upstream <c>visible_captures</c>.</summary>
    internal bool VisibleCaptures;

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
    /// Whether one character of <see cref="Text"/> is exactly one UTF-16 code unit, so that a
    /// character count and a code-unit offset are the same number - which is what upstream gets for
    /// free by indexing the subject by codepoint.
    /// </summary>
    /// <remarks>
    /// Not an upstream field. <see cref="NextPos"/> steps two units only across a well-formed
    /// surrogate pair, and a pair needs a high surrogate, so "the subject holds no high surrogate"
    /// is sufficient - and it is conservative, because a lone high surrogate turns the flag off and
    /// costs nothing but the walk that would have happened anyway. The scan is one vectorised pass
    /// over the subject, done once per matching operation, and it buys back the per-position walks
    /// in the repeat opcodes' backtrack arms that made a lazy scan quadratic.
    /// </remarks>
    internal bool OneUnitPerCharacter;

    private CharacterIndex? _characterIndex;

    /// <summary>
    /// The sampled position table that converts between a character count and a position when
    /// <see cref="OneUnitPerCharacter"/> is <see langword="false"/>, building it on first use.
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
        }
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
    /// <param name="oneUnitPerCharacter">
    /// <see cref="OneUnitPerCharacter"/> for this subject when an earlier state already worked it
    /// out, or <see langword="null"/> to scan the subject for it. <see cref="Match.NextMatch"/>
    /// passes it on so that a walk does not rescan the whole subject at every step.
    /// </param>
    /// <param name="cache">
    /// The cache the state goes back to when it is disposed; only <see cref="MatchStateCache.Rent"/>
    /// passes one.
    /// </param>
    /// <returns>The state, ready to match.</returns>
    internal static MatchState Create(
        PatternObject pattern,
        string text,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        bool matchAll,
        MatchLimits limits,
        ArrayPool<byte>? pool = null,
        bool? oneUnitPerCharacter = null,
        MatchStateCache? cache = null
    )
    {
        var state = new MatchState(pattern, pool, cache);
        state.Init(text, start, end, overlapped, partial, visibleCaptures, matchAll, limits, oneUnitPerCharacter);
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
    /// </remarks>
    /// <param name="text">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether a partial match is wanted.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="matchAll">Whether the match must cover the whole slice.</param>
    /// <param name="limits">The time budget and cancellation token bounding this operation.</param>
    /// <param name="oneUnitPerCharacter">As for <see cref="Create"/>.</param>
    internal void Init(
        string text,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        bool matchAll,
        MatchLimits limits,
        bool? oneUnitPerCharacter
    )
    {
        PatternObject pattern = Pattern;

        Text = text;
        TextLength = text.Length;
        // The ushort form of the same search allocates nothing; the char form allocated 96 B on
        // every call, even after tier-up, in Debug and Release (measured 2026-09-22, .NET 10).
        // That 96 B was the whole of a warm IsMatch's allocation; AllocationTests pins the 0.
        OneUnitPerCharacter =
            oneUnitPerCharacter
            ?? MemoryMarshal.Cast<char, ushort>(text.AsSpan()).IndexOfAnyInRange((ushort)0xD800, (ushort)0xDBFF) < 0;
        _characterIndex = null;

        // What a new state holds by default, and a reused one must be given back.
        Sstack.Reset();
        Bstack.Reset();
        Pstack.Reset();
        ClearGroups();
        foreach (RepeatData repeat in Repeats)
        {
            repeat.BodyGuardList.Reset();
            repeat.TailGuardList.Reset();
            repeat.Count = 0;
            repeat.Start = 0;
            repeat.CaptureChange = 0;
        }

        ActiveCalls.Clear();
        OpenCalls.Clear();
        SearchAnchor = 0;
        MatchPos = 0;
        BestMatchPos = 0;
        BestTextPos = 0;
        BestMatchGroups = null;
        Array.Clear(BestFuzzyCounts);
        BestFuzzyChanges.Clear();
        BestTotalErrors = 0;
        BestTotalCost = 0;
        HitEnd = false;
        HitEndMatchPos = 0;
        MaxErrors = 0;
        MaxCost = 0;
        TotalErrors = 0;
        TotalCost = 0;
        FewestErrors = 0;
        CaptureChange = 0;
        ReqEnd = 0;
        LastIndex = 0;
        LastGroup = 0;
        Iterations = 0;
        TooFewErrors = false;
        FoundMatch = false;
        Array.Clear(FuzzyCounts);
        FuzzyNode = null;
        FuzzyChanges.Clear();

        VisibleCaptures = visibleCaptures;
        MatchAll = matchAll;
        ReqPos = -1;
        IsFuzzy = pattern.IsFuzzy;

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
        if (Cache is { } cache)
        {
            cache.Return(this);
        }
        else
        {
            ReturnBuffers();
        }
    }

    private void ReturnBuffers()
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
        ReturnBuffers();
        Text = string.Empty;
        _characterIndex = null;
        BestMatchGroups = null;
        Cancellation = default;
    }

    /// <summary>
    /// Upstream's <c>char_at</c> function pointer (<c>upstream/src/_regex.c</c> line 18408), which
    /// reads one whole codepoint. A surrogate pair is decoded here, so a non-BMP character is one
    /// character to every opcode just as it is one to upstream's Python <c>str</c>. An unpaired
    /// surrogate reads as itself, which is what a Python <c>str</c> holding one does.
    /// </summary>
    /// <param name="pos">The position, a UTF-16 code unit index.</param>
    /// <returns>The codepoint there.</returns>
    internal uint CharAt(int pos)
    {
        char first = Text[pos];
        if (char.IsHighSurrogate(first) && pos + 1 < TextEnd && char.IsLowSurrogate(Text[pos + 1]))
        {
            return (uint)char.ConvertToUtf32(first, Text[pos + 1]);
        }

        return first;
    }

    /// <summary>
    /// One codepoint forward from <paramref name="pos"/>: upstream's <c>++text_pos</c> and
    /// <c>text_pos += node-&gt;step</c> when the step is positive.
    /// </summary>
    /// <param name="pos">The position.</param>
    /// <returns>The next position.</returns>
    internal int NextPos(int pos) =>
        pos + 1 < TextEnd && char.IsHighSurrogate(Text[pos]) && char.IsLowSurrogate(Text[pos + 1]) ? pos + 2 : pos + 1;

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
    /// </remarks>
    /// <param name="pos">The position.</param>
    /// <returns>The previous position, which may be -1 when <paramref name="pos"/> is 0.</returns>
    internal int PrevPos(int pos) =>
        pos >= 2 && char.IsLowSurrogate(Text[pos - 1]) && char.IsHighSurrogate(Text[pos - 2]) ? pos - 2 : pos - 1;

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
    /// Upstream <c>init_match</c> (<c>upstream/src/_regex.c</c> line 3404).
    /// </summary>
    internal void InitMatch()
    {
        // Reset the stacks.
        Sstack.Reset();
        Bstack.Reset();
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
            FuzzyChanges.Clear();
        }

        TotalErrors = 0;
        TotalCost = 0;
        FoundMatch = false;
        CaptureChange = 0;
        Iterations = 0;
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
    /// Upstream <c>pop_fuzzy_counts</c> (line 2652), <b>restoring</b>: the counts and the change list
    /// both go back to what they were at the matching push.
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
    /// <c>CONDITIONAL</c>/<c>END_CONDITIONAL</c> and <c>LOOKAROUND</c>/<c>END_LOOKAROUND</c>. A rough
    /// rule is that a pop into a scratch span is a merge and a pop into <see cref="FuzzyCounts"/> is
    /// a restore, <b>and the <c>FUZZY</c> backtrack arm is the exception that breaks it</b>: it
    /// merges into <see cref="FuzzyCounts"/>. Read the site, not the buffer.
    /// </para>
    /// </remarks>
    /// <param name="stack">The stack to pop from.</param>
    /// <param name="fuzzyCounts">Receives the counts, and is left alone for a non-fuzzy pattern.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFuzzyCounts(ByteStack stack, Span<long> fuzzyCounts)
    {
        if (!PopFuzzyCountsMerging(stack, fuzzyCounts, out long changeCount))
        {
            return false;
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
    internal void ResetGuards()
    {
        // Reset the guards for the repeats.
        foreach (RepeatData repeat in Repeats)
        {
            repeat.BodyGuardList.Reset();
            repeat.TailGuardList.Reset();
        }
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
    private GuardList? ActiveGuardList(int index, uint guardType)
    {
        // Is a guard active here?
        if ((Pattern.RepeatInfoAt(index).Status & guardType) == 0)
        {
            return null;
        }

        // Which guard list?
        RepeatData repeat = Repeats[index];
        return (guardType & NodeStatus.Body) != 0 ? repeat.BodyGuardList : repeat.TailGuardList;
    }

    /// <summary>Upstream <c>guard_repeat</c> (<c>upstream/src/_regex.c</c> line 9446).</summary>
    /// <param name="index">The repeat index.</param>
    /// <param name="textPos">The position to guard.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    internal void GuardRepeat(int index, int textPos, uint guardType, bool protect) =>
        ActiveGuardList(index, guardType)?.Guard(textPos, protect);

    /// <summary>Upstream <c>guard_repeat_range</c> (line 9534).</summary>
    /// <param name="index">The repeat index.</param>
    /// <param name="loPos">The lowest position to guard.</param>
    /// <param name="hiPos">The highest, inclusive.</param>
    /// <param name="guardType"><see cref="NodeStatus.Body"/> or <see cref="NodeStatus.Tail"/>.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    internal void GuardRepeatRange(int index, int loPos, int hiPos, uint guardType, bool protect)
    {
        GuardList? guardList = ActiveGuardList(index, guardType);

        if (guardList is null)
        {
            return;
        }

        while (loPos <= hiPos)
        {
            loPos = guardList.GuardRange(loPos, hiPos, protect);
        }
    }

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
    internal bool IsRepeatGuarded(int index, int textPos, uint guardType)
    {
        if (IsFuzzy)
        {
            return false;
        }

        return ActiveGuardList(index, guardType)?.IsGuarded(textPos) ?? false;
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
