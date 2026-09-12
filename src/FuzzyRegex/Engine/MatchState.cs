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

    /// <summary>Upstream <c>text</c> and <c>string</c>, which are one object here.</summary>
    internal readonly string Text;

    /// <summary>Upstream <c>text_length</c>.</summary>
    internal readonly int TextLength;

    /// <summary>Upstream <c>slice_start</c>: where the searched slice starts.</summary>
    internal int SliceStart;

    /// <summary>Upstream <c>slice_end</c>.</summary>
    internal int SliceEnd;

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
    internal GroupData[] Groups = [];

    /// <summary>
    /// Upstream <c>repeats</c>: one entry per repeat in the pattern, indexed by the repeat index
    /// that a <c>GREEDY_REPEAT</c>-family node carries in <c>values[0]</c>.
    /// </summary>
    internal RepeatData[] Repeats = [];

    /// <summary>Upstream <c>sstack</c>: the structure stack.</summary>
    internal readonly ByteStack Sstack = new();

    /// <summary>Upstream <c>bstack</c>: the backtracking stack.</summary>
    internal readonly ByteStack Bstack = new();

    /// <summary>Upstream <c>pstack</c>: the pruning stack.</summary>
    internal readonly ByteStack Pstack = new();

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

    /// <summary>Upstream <c>min_width</c>, a codepoint count.</summary>
    internal long MinWidth;

    /// <summary>Upstream <c>encoding</c>, reduced to which table it is (see <see cref="Encodings"/>).</summary>
    internal CaseEncoding Encoding;

    /// <summary>Upstream <c>partial_side</c>.</summary>
    internal int PartialSide;

    /// <summary>Upstream <c>max_errors</c>.</summary>
    internal long MaxErrors;

    /// <summary>Upstream <c>total_errors</c>.</summary>
    internal long TotalErrors;

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
    internal readonly bool OneUnitPerCharacter;

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

    private MatchState(PatternObject pattern, string text)
    {
        Pattern = pattern;
        Text = text;
        TextLength = text.Length;
        OneUnitPerCharacter = text.AsSpan().IndexOfAnyInRange('\uD800', '\uDBFF') < 0;
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
    /// <param name="timeout">The timeout in <see cref="Stopwatch"/> ticks, or <see cref="NoTimeout"/>.</param>
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
        long timeout
    )
    {
        // The capture groups (state_init_2, upstream/src/_regex.c:18327). Upstream caches the block
        // on the pattern as 'groups_storage' and reuses it; on a garbage-collected heap that cache
        // has nothing to port. 'true_group_count' rather than 'public_group_count', because a
        // branch-reset group's private number is larger than its public one and START_GROUP indexes
        // by the private one.
        //
        // NOT PORTED: the fuzzy-guard allocation, whose contents belong to Phase 5, and the
        // group-call-guard allocation, which S30 settled as never to be ported - upstream's
        // 'group_call_guard_list' is written in five places and read in none, so it guards nothing.
        // docs/PORTMAP.md's "deliberately not ported" table has the grep and the date.
        var groups = new GroupData[pattern.TrueGroupCount];
        for (int g = 0; g < groups.Length; g++)
        {
            groups[g] = new GroupData();
        }

        // The repeats (state_init_2, upstream/src/_regex.c:18493-18505). Like the groups, upstream
        // caches the block on the pattern as 'repeats_storage' and reuses it; there is nothing to
        // port in that cache on a garbage-collected heap, and nothing in 'dealloc_repeats'
        // (:18630) either, which is three calls to free.
        var repeats = new RepeatData[pattern.RepeatCount];
        for (int r = 0; r < repeats.Length; r++)
        {
            repeats[r] = new RepeatData();
        }

        var state = new MatchState(pattern, text)
        {
            VisibleCaptures = visibleCaptures,
            MatchAll = matchAll,
            ReqPos = -1,
            IsFuzzy = pattern.IsFuzzy,
            Groups = groups,
            Repeats = repeats,
        };

        // Adjust boundaries.
        start = ClampIndex(start, text.Length);
        end = ClampIndex(end, text.Length);

        if (end < start)
        {
            end = start;
        }

        state.Overlapped = overlapped;
        state.MinWidth = pattern.MinWidth;
        state.Encoding = pattern.Encoding;

        // Open start and closed end bounds, like in re module.
        state.TextStart = 0;
        state.TextEnd = end;

        state.SliceStart = start;
        state.SliceEnd = end;

        state.Reverse = (pattern.Flags & RegexFlags.Reverse) != 0;

        if (partial)
        {
            state.PartialSide = state.Reverse ? PartialLeft : PartialRight;
        }
        else
        {
            state.PartialSide = PartialNone;
        }

        state.TextPos = state.Reverse ? state.SliceEnd : state.SliceStart;

        // Point to the final newline and line separator if it's at the end of the string, otherwise
        // just -1.
        state.FinalNewline = -1;
        state.FinalLineSep = -1;
        int finalPos = state.PrevPos(state.TextEnd);
        if (finalPos >= 0)
        {
            uint ch = state.CharAt(finalPos);
            if (ch == 0x0A)
            {
                // The string ends with LF.
                state.FinalNewline = finalPos;
                state.FinalLineSep = finalPos;

                // Does the string end with CR/LF?
                finalPos = state.PrevPos(finalPos);
                if (finalPos >= 0 && state.CharAt(finalPos) == 0x0D)
                {
                    state.FinalLineSep = finalPos;
                }
            }
            else if (Encodings.IsLineSep(state.Encoding, ch))
            {
                // The string doesn't end with LF, but it could be another kind of line separator.
                state.FinalLineSep = finalPos;
            }
        }

        // If the 'new' behaviour is enabled then split correctly on zero-width matches.
        state.Version0 = (pattern.Flags & RegexFlags.Version1) == 0;
        state.MustAdvance = false;

        state.Timeout = timeout;
        state.StartTime = timeout == NoTimeout ? 0 : Stopwatch.GetTimestamp();

        // NOT PORTED: search_positions, which only search_start reads (Phase 7).

        return state;
    }

    /// <summary>
    /// Upstream <c>state_fini</c> (<c>upstream/src/_regex.c</c> line 18662), reduced to returning
    /// the stacks' rented buffers.
    /// </summary>
    public void Dispose()
    {
        Sstack.Dispose();
        Bstack.Dispose();
        Pstack.Dispose();
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
        // NOT PORTED: the fuzzy counts, node and change list (Phase 5).

        TotalErrors = 0;
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
    /// The fuzzy-section and group-call halves (<c>:3392-3400</c>) are not ported, for two different
    /// reasons. The fuzzy half waits for Phase 5, and until then a pattern needing it throws at its
    /// own opcode before anything could have written a guard. The group-call half is **never** to be
    /// ported: S30 landed group calls without it, because upstream's
    /// <c>group_call_guard_list</c> is written in five places and read in none - see the allocation
    /// in <see cref="Create"/> and the "deliberately not ported" table in <c>docs/PORTMAP.md</c>,
    /// which carries the grep and the date. Corrected at the Phase 4 close (S36); the line read
    /// "no group-call guards until Phase 4" until then.
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
