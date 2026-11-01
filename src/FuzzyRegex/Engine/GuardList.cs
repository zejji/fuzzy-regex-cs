using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// One inclusive range of positions guarded against further matching. Port of <c>RE_GuardSpan</c>
/// (<c>upstream/src/_regex.c</c> lines 312-317).
/// </summary>
/// <param name="Low">The lowest guarded position.</param>
/// <param name="High">The highest guarded position, inclusive.</param>
/// <param name="Protect">
/// Whether the span blocks matching or merely records that the position was reached.
/// <see cref="GuardList.IsGuarded"/> answers <see langword="false"/> inside an unprotected span, and
/// a position only ever extends a neighbouring span whose <c>Protect</c> matches, so the two kinds
/// never merge. <c>END_GREEDY_REPEAT</c> is the one caller that passes <see langword="false"/>: it
/// records that the body succeeded at that position without stopping anything from matching there.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GuardSpan(int Low, int High, bool Protect);

/// <summary>
/// The positions one repeat has already failed at. Port of <c>RE_GuardList</c>
/// (<c>upstream/src/_regex.c</c> lines 319-326) and the five functions over it: <c>insert_guard_span</c>
/// (<c>:9296</c>), <c>delete_guard_span</c> (<c>:9328</c>), <c>is_guarded</c> (<c>:9340</c>),
/// <c>guard</c> (<c>:9378</c>) and <c>guard_range</c> (<c>:9464</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is what stops a pathological pattern re-entering the same repeat at the same position for
/// ever. The spans are kept sorted and non-overlapping so a lookup is a binary search, and an
/// adjacent position extends a span rather than adding one, which is why the list stays short even
/// when every position in a long subject has been tried.
/// </para>
/// <para>
/// Upstream's <c>capacity</c> is <see cref="_spans"/>'s <c>Length</c>, and its
/// <c>safe_realloc</c> failure path has nothing to port on a garbage-collected heap: the list can
/// hold at most one span per position and every caller's positions come from the subject, so it is
/// bounded by the subject's length rather than by an allocation limit.
/// </para>
/// <para>
/// <b>Upstream's <c>last_text_pos</c> and <c>last_low</c> are not ported</b>, because neither is
/// read anywhere in <c>_regex.c</c>. <c>grep -n "last_low\|last_text_pos" upstream/src/_regex.c</c>
/// (2026-08-31) gives the two declarations at <c>:324-325</c> and eight sites, every one of which
/// assigns and none of which reads: <c>:2721</c>, <c>:3365</c>, <c>:9347</c>, <c>:9385</c>,
/// <c>:9471</c>, <c>:11802</c> and <c>:13429-13431</c>. Porting a write-only field would put a line
/// in five methods to record something nothing asks about.
/// </para>
/// </remarks>
internal sealed class GuardList
{
    private GuardSpan[] _spans = [];

    /// <summary>Upstream <c>count</c>: how many of <see cref="_spans"/> are live.</summary>
    internal int Count;

    /// <summary>
    /// NOT UPSTREAM (D35), counted in Debug only: how many times the list has been guarded, reset
    /// or popped, so <c>Matcher.CheckKeptGuards</c> can tell that nothing touched it in between.
    /// </summary>
    internal long Edits;

    /// <summary>Upstream <c>reset_guard_list</c> (<c>upstream/src/_regex.c</c> line 3363).</summary>
    internal void Reset()
    {
        NoteEdit();
        Count = 0;
    }

    /// <summary>Counts one edit in <see cref="Edits"/>, in Debug only.</summary>
    [Conditional("DEBUG")]
    private void NoteEdit() => Edits++;

    /// <summary>
    /// NOT UPSTREAM (D35), for a Debug assertion: whether every live span blocks matching, that is,
    /// the list records failures and no success mark.
    /// </summary>
    /// <returns><see langword="true"/> if no live span is unprotected.</returns>
    internal bool HoldsFailuresOnly()
    {
        for (int i = 0; i < Count; i++)
        {
            if (!_spans[i].Protect)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// NOT UPSTREAM (D85, D103): <see cref="PopFrom"/> for a keyed tail list, which a restore may
    /// rebuild in a spare list that never held this save, so the array grows first if it must.
    /// </summary>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool RestoreFrom(ByteStack stack)
    {
        if (!stack.TopSize(out long count))
        {
            return false;
        }

        if (count > _spans.Length)
        {
            _spans = new GuardSpan[Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)count))];
        }

        return PopFrom(stack);
    }

    /// <summary>
    /// NOT UPSTREAM (D103), Debug only: whether the save <see cref="PushTo"/> left just below
    /// <paramref name="top"/> holds exactly the live spans; moves <paramref name="top"/> below it.
    /// </summary>
    /// <param name="stack">The stack holding the save.</param>
    /// <param name="top">The depth the save ends at; on return, the depth it starts at.</param>
    /// <returns><see langword="true"/> if the save equals the list.</returns>
    internal bool MatchesSaved(ByteStack stack, ref int top)
    {
        if (!stack.ReadSizeBelow(top, out long count) || count != Count)
        {
            return false;
        }

        top -= sizeof(long);
        GuardSpan span = default;
        for (int i = Count - 1; i >= 0; i--)
        {
            if (!stack.ReadBlockBelow(top, MemoryMarshal.AsBytes(new Span<GuardSpan>(ref span))) || span != _spans[i])
            {
                return false;
            }

            top -= Unsafe.SizeOf<GuardSpan>();
        }

        return true;
    }

    /// <summary>Upstream <c>push_guard_data</c> (line 2541): the live spans, then how many.</summary>
    /// <param name="stack">The stack to push onto.</param>
    internal void PushTo(ByteStack stack)
    {
        stack.PushBlock(MemoryMarshal.AsBytes(_spans.AsSpan(0, Count)));
        stack.PushSize(Count);
    }

    /// <summary>Upstream <c>pop_guard_data</c> (line 2712).</summary>
    /// <remarks>
    /// The array is never too small to pop back into: the same list pushed this block earlier in the
    /// same match, and the array only ever grows. Upstream relies on the same invariant.
    /// <c>last_text_pos = -1</c> is not ported, for the reason given on this type.
    /// </remarks>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFrom(ByteStack stack)
    {
        NoteEdit();
        if (!stack.PopSize(out long count))
        {
            return false;
        }

        Count = (int)count;
        return stack.PopBlock(MemoryMarshal.AsBytes(_spans.AsSpan(0, Count)));
    }

    /// <summary>
    /// NOT UPSTREAM (D121): where <see cref="PopFromKeepingSuccessMarks"/> holds the success marks
    /// while the save is popped, kept so a restore allocates only when it holds more than before.
    /// </summary>
    private GuardSpan[] _successMarks = [];

    /// <summary>
    /// NOT UPSTREAM (D121): where <see cref="PopFromKeepingSuccessMarks"/> builds the merged list,
    /// kept for the same reason.
    /// </summary>
    private GuardSpan[] _merged = [];

    /// <summary>
    /// NOT UPSTREAM (D121): adds <paramref name="span"/> after the last of
    /// <paramref name="spans"/>, joining it to that one when they touch and both block or both do
    /// not, as <see cref="Guard"/> joins them.
    /// </summary>
    /// <param name="spans">The list being built, in order.</param>
    /// <param name="count">How many of <paramref name="spans"/> are live; one more on return unless joined.</param>
    /// <param name="span">The span, which starts after the last live one ends.</param>
    private static void Append(GuardSpan[] spans, ref int count, GuardSpan span)
    {
        Debug.Assert(count == 0 || spans[count - 1].High < span.Low, "D121: the merge appends in order");
        if (count > 0 && spans[count - 1].Protect == span.Protect && spans[count - 1].High + 1 == span.Low)
        {
            spans[count - 1] = spans[count - 1] with { High = span.High };
            return;
        }

        spans[count++] = span;
    }

    /// <summary>
    /// NOT UPSTREAM (D121): <see cref="PopFrom"/>, then the success marks the list holds now put back
    /// wherever the save has nothing, so a restore never forgets that a body reached its end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body guard blocks a position only when the body never got to its end from it:
    /// <c>END_GREEDY_REPEAT</c> and <c>END_LAZY_REPEAT</c> mark the position the body started at with
    /// an unprotected span, and the failure written when backtracking reaches the body's entry there
    /// finds the mark and leaves it (<see cref="Guard"/> keeps the first record at a position). A
    /// conditional saves the lists before its test and puts them back when backtracking reaches it,
    /// after its branch and the rest of the match have run, and upstream puts them back whole
    /// (<c>upstream/src/_regex.c</c> pop_repeats, from the CONDITIONAL and END_CONDITIONAL backtrack
    /// arms), so a mark made since goes. The failure then written at that position is true only of
    /// the path that wrote it: the rest of the match failed under its group values. A later path
    /// with other values is refused, as in <c>(a?)(?(?=c)c|[ab])*\1</c> over 'ac', (0, 1) for
    /// (0, 2).
    /// </para>
    /// <para>
    /// Putting the marks back is sound whatever path made them, because an unprotected span never
    /// blocks (<see cref="IsGuarded"/>, <see cref="TryGetSpan"/>'s callers read
    /// <see cref="GuardSpan.Protect"/>) and a mark only fills a gap the save leaves: the save's
    /// failures all stay, and the most the marks can do is let a path through. The failures written since still go, as upstream's restore intends: those inside the
    /// test were about a subpattern that was only being asked about.
    /// </para>
    /// </remarks>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFromKeepingSuccessMarks(ByteStack stack)
    {
        int marks = 0;
        for (int i = 0; i < Count; i++)
        {
            if (!_spans[i].Protect)
            {
                if (marks == _successMarks.Length)
                {
                    Array.Resize(ref _successMarks, Math.Max(16, marks * 2));
                }

                _successMarks[marks++] = _spans[i];
            }
        }

        if (!PopFrom(stack))
        {
            return false;
        }

        if (marks == 0)
        {
            return true;
        }

#if DEBUG
        long protectedBefore = ProtectedPositions();
#endif

        // One merge of two sorted lists, so a restore costs O(spans + marks) like the pop itself;
        // a GuardRange per mark would shift the array for each one, quadratic where marks and
        // failures alternate. The save's spans go through as they are, and each mark only fills
        // the gaps between them. At most every saved span and every gap a mark crosses: 2n + m.
        int saved = Count;
        int need = (2 * saved) + marks;
        if (_merged.Length < need)
        {
            _merged = new GuardSpan[Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)need))];
        }

        GuardSpan[] spans = _spans;
        GuardSpan[] merged = _merged;
        int count = 0;
        int next = 0;
        for (int i = 0; i < marks; i++)
        {
            int loPos = _successMarks[i].Low;
            int hiPos = _successMarks[i].High;
            while (loPos <= hiPos)
            {
                while (next < saved && spans[next].High < loPos)
                {
                    Append(merged, ref count, spans[next++]);
                }

                if (next < saved && spans[next].Low <= loPos)
                {
                    // The save has a record here, which stays: the mark resumes after it. It is a
                    // mark too: the list now grew from the save by Guard, which never overwrites a
                    // record, and by pops of later saves, which held the same record.
                    Debug.Assert(!spans[next].Protect, "D121: a success mark never sits on a failure the save holds");
                    loPos = spans[next].High + 1;
                    continue;
                }

                int end = next < saved ? Math.Min(hiPos, spans[next].Low - 1) : hiPos;
                Append(merged, ref count, new GuardSpan(loPos, end, false));
                loPos = end + 1;
            }
        }

        while (next < saved)
        {
            Append(merged, ref count, spans[next++]);
        }

        // Copied back rather than swapped: PopFrom relies on _spans never shrinking.
        if (count > _spans.Length)
        {
            _spans = new GuardSpan[(int)BitOperations.RoundUpToPowerOf2((uint)count)];
        }

        Array.Copy(merged, _spans, count);
        Count = count;

#if DEBUG
        Debug.Assert(
            ProtectedPositions() == protectedBefore,
            "D121: putting the success marks back leaves every failure the save holds, and adds none"
        );
#endif
        return true;
    }

#if DEBUG
    /// <summary>NOT UPSTREAM (D121), Debug only: how many positions the live spans protect.</summary>
    /// <returns>The count.</returns>
    private long ProtectedPositions()
    {
        long total = 0;
        for (int i = 0; i < Count; i++)
        {
            if (_spans[i].Protect)
            {
                total += (long)_spans[i].High - _spans[i].Low + 1;
            }
        }

        return total;
    }
#endif

    /// <summary>Upstream <c>insert_guard_span</c> (line 9296): makes room at <paramref name="index"/>.</summary>
    /// <param name="index">Where the new span goes.</param>
    private void InsertSpan(int index)
    {
        if (Count >= _spans.Length)
        {
            int newCapacity = _spans.Length * 2;

            if (newCapacity == 0)
            {
                newCapacity = 16;
            }

            Array.Resize(ref _spans, newCapacity);
        }

        int n = Count - index;
        if (n > 0)
        {
            Array.Copy(_spans, index, _spans, index + 1, n);
        }

        ++Count;
    }

    /// <summary>Upstream <c>delete_guard_span</c> (line 9328).</summary>
    /// <param name="index">The span to remove.</param>
    private void DeleteSpan(int index)
    {
        int n = Count - index - 1;
        if (n > 0)
        {
            Array.Copy(_spans, index + 1, _spans, index, n);
        }

        --Count;
    }

    /// <summary>Upstream <c>is_guarded</c> (line 9340).</summary>
    /// <param name="textPos">The position to ask about.</param>
    /// <returns><see langword="true"/> if a protecting span covers it.</returns>
    internal bool IsGuarded(int textPos)
    {
        GuardSpan[] spans = _spans;
        int count = Count;

        if (count == 0 || textPos < spans[0].Low || textPos > spans[count - 1].High)
        {
            return false;
        }

        int below = -1;
        int above = count;

        while (above - below > 1)
        {
            int mid = (below + above) / 2;
            GuardSpan span = spans[mid];

            if (textPos < span.Low)
            {
                above = mid;
            }
            else if (textPos > span.High)
            {
                below = mid;
            }
            else
            {
                return span.Protect;
            }
        }

        return false;
    }

    /// <summary>
    /// The span holding <paramref name="textPos"/>, protecting or not. <b>Not upstream</b> (C5,
    /// G2): <see cref="IsGuarded"/>'s search, returning the span, so that a caller walking back
    /// over guarded positions can pass the span in one move. Spans merge only when exactly
    /// adjacent and of the same kind (<see cref="Guard"/>), so the span may end where the next
    /// one begins.
    /// </summary>
    /// <param name="textPos">The position to ask about.</param>
    /// <param name="span">The span, when one holds the position.</param>
    /// <returns><see langword="true"/> if a span holds the position.</returns>
    internal bool TryGetSpan(int textPos, out GuardSpan span)
    {
        GuardSpan[] spans = _spans;
        int count = Count;
        span = default;

        if (count == 0 || textPos < spans[0].Low || textPos > spans[count - 1].High)
        {
            return false;
        }

        int below = -1;
        int above = count;

        while (above - below > 1)
        {
            int mid = (below + above) / 2;
            GuardSpan candidate = spans[mid];

            if (textPos < candidate.Low)
            {
                above = mid;
            }
            else if (textPos > candidate.High)
            {
                below = mid;
            }
            else
            {
                span = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Upstream <c>guard</c> (line 9378): guards one position.</summary>
    /// <param name="textPos">The position to guard.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    internal void Guard(int textPos, bool protect)
    {
        NoteEdit();
        GuardSpan[] spans = _spans;
        int count = Count;
        int below;
        int above;

        if (count > 0 && textPos > spans[count - 1].High)
        {
            below = count - 1;
            above = count;
        }
        else if (count > 0 && textPos < spans[0].Low)
        {
            below = -1;
            above = 0;
        }
        else
        {
            below = -1;
            above = count;

            while (above - below > 1)
            {
                int mid = (below + above) / 2;
                GuardSpan span = spans[mid];

                if (textPos < span.Low)
                {
                    above = mid;
                }
                else if (textPos > span.High)
                {
                    below = mid;
                }
                else
                {
                    return;
                }
            }
        }

        // Add the position to the guard list.
        if (below >= 0 && textPos - spans[below].High == 1 && spans[below].Protect == protect)
        {
            if (above < count && spans[above].Low - textPos == 1 && spans[above].Protect == protect)
            {
                // The new position joins 2 spans.
                spans[below] = spans[below] with
                {
                    High = spans[above].High,
                };
                DeleteSpan(above);
            }
            else
            {
                // The new position is just above the 'below' span.
                spans[below] = spans[below] with
                {
                    High = textPos,
                };
            }
        }
        else if (above < count && spans[above].Low - textPos == 1 && spans[above].Protect == protect)
        {
            // The new position is just below the 'above' span.
            spans[above] = spans[above] with
            {
                Low = textPos,
            };
        }
        else
        {
            // Insert a new span.
            InsertSpan(above);
            _spans[above] = new GuardSpan(textPos, textPos, protect);
        }
    }

    /// <summary>
    /// Upstream <c>guard_range</c> (line 9464): guards as much of
    /// <paramref name="loPos"/>..<paramref name="hiPos"/> as it can in one pass.
    /// </summary>
    /// <remarks>
    /// It stops at the first span already in the list, which is why <c>guard_repeat_range</c> calls
    /// it in a loop: the return value is where the next pass has to start from.
    /// </remarks>
    /// <param name="loPos">The lowest position to guard.</param>
    /// <param name="hiPos">The highest, inclusive.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    /// <returns>One past the highest position now guarded.</returns>
    internal int GuardRange(int loPos, int hiPos, bool protect)
    {
        NoteEdit();
        GuardSpan[] spans = _spans;
        int count = Count;

        int below = -1;
        int above = count;

        while (above - below > 1)
        {
            int mid = (below + above) / 2;
            GuardSpan span = spans[mid];

            if (loPos < span.Low)
            {
                above = mid;
            }
            else if (loPos > span.High)
            {
                below = mid;
            }
            else
            {
                return span.High + 1;
            }
        }

        // Add the range to the guard list.
        if (below >= 0 && loPos - spans[below].High == 1 && spans[below].Protect == protect)
        {
            if (above < count && spans[above].Low - hiPos <= 1 && spans[above].Protect == protect)
            {
                // The new range joins the spans.
                spans[below] = spans[below] with
                {
                    High = spans[above].High,
                };
                DeleteSpan(above);
                spans = _spans;
            }
            else
            {
                if (above < count)
                {
                    hiPos = Math.Min(hiPos, spans[above].Low - 1);
                }

                spans[below] = spans[below] with { High = hiPos };
            }

            loPos = spans[below].High + 1;
        }
        else if (above < count && spans[above].Low - hiPos <= 1 && spans[above].Protect == protect)
        {
            spans[above] = spans[above] with { Low = loPos };
            loPos = spans[above].High + 1;
        }
        else
        {
            // Insert a new span.
            InsertSpan(above);
            spans = _spans;

            if (above < count)
            {
                hiPos = Math.Min(hiPos, spans[above].Low - 1);
            }

            spans[above] = new GuardSpan(loPos, hiPos, protect);
            loPos = spans[above].High + 1;
        }

        return loPos;
    }
}

/// <summary>
/// What one repeat is doing in this match. Port of <c>RE_RepeatData</c>
/// (<c>upstream/src/_regex.c</c> lines 336-343).
/// </summary>
/// <remarks>
/// A class rather than a struct for the same reason <see cref="GroupData"/> is one: the matcher
/// reaches into it through <c>rp_data</c> and mutates it in place all over
/// <see cref="Matcher.BasicMatch"/>, and a struct would need <c>ref</c> on every one of those sites
/// to say what a reference type says for free.
/// </remarks>
internal sealed class RepeatData
{
    /// <summary>Upstream <c>body_guard_list</c>: where the body has already failed.</summary>
    internal readonly GuardList BodyGuardList = new();

    /// <summary>Upstream <c>tail_guard_list</c>: where the tail has already failed.</summary>
    internal readonly GuardList TailGuardList = new();

    /// <summary>
    /// NOT UPSTREAM (D85): the tail guards, one list per key, for a repeat whose tail reads group
    /// state (<see cref="RepeatInfo.TailKey"/>); <see langword="null"/> otherwise, when
    /// <see cref="TailGuardList"/> is used as upstream uses it.
    /// </summary>
    internal KeyedTailGuards? KeyedTail;

    /// <summary>Upstream <c>count</c>: how many times the body has matched.</summary>
    internal long Count;

    /// <summary>Upstream <c>start</c>: where the current iteration of the body started.</summary>
    internal int Start;

    /// <summary>Upstream <c>capture_change</c>: the state's counter when this iteration started.</summary>
    internal long CaptureChange;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule): how many fuzzy edits the path had made
    /// (<c>MatchState.FuzzyChanges</c>) when this iteration started, so the end of the iteration
    /// can tell whether it spent any. The list is exact along the path, because every backtrack over
    /// an edit unrecords it, where upstream's <c>capture_change</c> also counts edits since undone.
    /// The high 32 bits hold how many verbs had been crossed (<c>Matcher.IterationStart</c>).
    /// </summary>
    internal long ChangesAtStart;

    /// <summary>
    /// NOT UPSTREAM (empty-iteration rule): the repeat memo, the states that iterations of the
    /// current run of this repeat have already led to. See <c>Matcher.RepeatMemoHit</c>.
    /// </summary>
    /// <remarks>
    /// A run is one entry into the repeat, with its fixed continuation. The memo is emptied on
    /// entry and whenever an outer backtrack restores the repeat's saved state, since those entries
    /// belong to a different run; emptying is always safe, it only drops pruning.
    /// </remarks>
    internal HashSet<RepeatMemoKey>? Memo;

    /// <summary>Empties <see cref="Memo"/>, dropping a large one rather than clearing it.</summary>
    internal void ClearMemo()
    {
        if (Memo is not { Count: > 0 })
        {
            return;
        }

        // Clear costs the table's capacity, so a memo that grew large once would tax every later
        // entry into the repeat; a fresh one is allocated on demand instead.
        if (Memo.Count > 4096)
        {
            Memo = null;
        }
        else
        {
            Memo.Clear();
        }
    }

    /// <summary>
    /// NOT UPSTREAM (D17): which run of this repeat is under way, one entry into it with its fixed
    /// continuation, so that the states one run records are never read by another. Numbered from
    /// <c>MatchState.NextRunId</c>, and only when the pattern tests a group. See
    /// <c>Matcher.RevisitsEmptyIterationState</c>.
    /// </summary>
    internal long RunId;
}

/// <summary>
/// NOT UPSTREAM (D17): the state an empty iteration of a repeat led to, which is everything the rest
/// of the match can depend on within one run of the repeat. See
/// <c>Matcher.RevisitsEmptyIterationState</c>.
/// </summary>
/// <remarks>
/// The values sit in an array because the number of tested groups is the pattern's. A lookup
/// wraps the state's scratch array (<c>MatchState.EmptyIterationScratch</c>), so it allocates
/// nothing; only a new state is stored, with a <see cref="Copy"/> of its values.
/// </remarks>
internal readonly struct EmptyIterationState : IEquatable<EmptyIterationState>
{
    private readonly long _runId;
    private readonly long[] _values;
    private readonly int _hash;

    /// <summary>Wraps <paramref name="values"/> without copying them.</summary>
    /// <param name="runId">The run of the repeat.</param>
    /// <param name="values">The position, the count and the rest of the state, in a fixed order.</param>
    internal EmptyIterationState(long runId, long[] values)
    {
        _runId = runId;
        _values = values;

        var hash = new HashCode();
        hash.Add(runId);
        foreach (long value in values)
        {
            hash.Add(value);
        }

        _hash = hash.ToHashCode();
    }

    /// <summary>The same state over its own copy of the values, for storing.</summary>
    /// <returns>The copy.</returns>
    internal EmptyIterationState Copy() => new(_runId, [.. _values]);

    /// <inheritdoc/>
    public bool Equals(EmptyIterationState other) =>
        _hash == other._hash && _runId == other._runId && _values.AsSpan().SequenceEqual(other._values);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EmptyIterationState other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;
}

/// <summary>
/// NOT UPSTREAM (empty-iteration rule): the state after an iteration of a fuzzy repeat, which is
/// everything the rest of the match can depend on within one run of the repeat. See
/// <c>Matcher.RepeatMemoHit</c>.
/// </summary>
/// <param name="TextPos">The text position.</param>
/// <param name="Count">The repeat count, clipped to the minimum when the repeat has no maximum.</param>
/// <param name="Sub">The open section's substitutions.</param>
/// <param name="Ins">The open section's insertions.</param>
/// <param name="Del">The open section's deletions.</param>
/// <param name="Group1">The span of the first tested group, packed, or 0.</param>
/// <param name="Group2">The span of the second tested group, packed, or 0.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RepeatMemoKey(
    int TextPos,
    long Count,
    long Sub,
    long Ins,
    long Del,
    long Group1,
    long Group2
);
