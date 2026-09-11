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

    /// <summary>Upstream <c>reset_guard_list</c> (<c>upstream/src/_regex.c</c> line 3363).</summary>
    internal void Reset() => Count = 0;

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
        if (!stack.PopSize(out long count))
        {
            return false;
        }

        Count = (int)count;
        return stack.PopBlock(MemoryMarshal.AsBytes(_spans.AsSpan(0, Count)));
    }

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

    /// <summary>Upstream <c>guard</c> (line 9378): guards one position.</summary>
    /// <param name="textPos">The position to guard.</param>
    /// <param name="protect">Whether the span blocks matching or merely records.</param>
    internal void Guard(int textPos, bool protect)
    {
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

    /// <summary>Upstream <c>count</c>: how many times the body has matched.</summary>
    internal long Count;

    /// <summary>Upstream <c>start</c>: where the current iteration of the body started.</summary>
    internal int Start;

    /// <summary>Upstream <c>capture_change</c>: the state's counter when this iteration started.</summary>
    internal long CaptureChange;
}
