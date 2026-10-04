using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11, build chunk 1: the w-mark list a partial call's second pass keeps, and the checks A7 and A8
/// make of it (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, sections 4.2 and 4.5). No
/// engine arm makes a mark yet; these pin the list's own rules before the chunks that do.
/// </summary>
public sealed class PartialWMarksTests
{
    [Test]
    public void A_mark_made_below_the_top_keeps_the_list_in_order()
    {
        using MatchState state = PartialState();
        state.Bstack.PushBlock(new byte[10]);

        state.AddWMark(8, WMarkKind.Touch);
        state.AddWMark(3, WMarkKind.VerbTaint);
        state.AddWMark(3, WMarkKind.CutTaint);
        state.AddWMark(9, WMarkKind.Touch);

        state
            .WMarks.Should()
            .Equal(
                new WMark(3, WMarkKind.VerbTaint),
                new WMark(3, WMarkKind.CutTaint),
                new WMark(8, WMarkKind.Touch),
                new WMark(9, WMarkKind.Touch)
            );
        state.WMarksAreInOrder().Should().BeTrue();
    }

    [Test]
    public void Dropping_from_a_position_removes_the_marks_at_and_above_it()
    {
        using MatchState state = PartialState();
        state.Bstack.PushBlock(new byte[10]);
        state.AddWMark(2, WMarkKind.CutTaint);
        state.AddWMark(5, WMarkKind.Touch);
        state.AddWMark(7, WMarkKind.Touch);

        state.DropWMarksFrom(5);

        state.WMarks.Should().Equal(new WMark(2, WMarkKind.CutTaint));
    }

    [Test]
    public void A_truncation_to_a_marked_position_would_discard_a_live_mark()
    {
        using MatchState state = PartialState();
        state.Bstack.PushBlock(new byte[10]);
        state.AddWMark(6, WMarkKind.Touch);

        // A7: a truncation to 7 keeps the entry at 6; one to 6 would pop it and leave its mark.
        (state.NoWMarkAtOrAbove(7), state.NoWMarkAtOrAbove(6))
            .Should()
            .Be((true, false));
    }

    [Test]
    public void A_mark_whose_entry_has_been_popped_breaks_the_order()
    {
        using MatchState state = PartialState();
        state.Bstack.PushBlock(new byte[10]);
        state.AddWMark(6, WMarkKind.Touch);

        // A8: the mark must go with its entry. A pop that leaves it behind is what A8 catches.
        state.Bstack.DropBlock(4);

        state.WMarksAreInOrder().Should().BeFalse();
    }

    [Test]
    public void A_second_pass_event_is_counted_for_the_call_in_a_debug_build()
    {
        using MatchState state = PartialState();

        state.NotePartialEvent(PartialEvent.CutBlockedAtomic);
        state.NotePartialEvent(PartialEvent.CutBlockedAtomic);

#if DEBUG
        const long expected = 2;
#else
        const long expected = 0;
#endif
        (state.PartialEventCount(PartialEvent.CutBlockedAtomic), state.PartialEventCount(PartialEvent.Dedup))
            .Should()
            .Be((expected, 0L));
    }

    private static MatchState PartialState()
    {
        var regex = new FuzzyRegex("a");
        MatchState state = MatchState.Create(
            regex.PatternObject,
            "a".AsMemory(),
            0,
            1,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        state.InitMatch();
        return state;
    }
}
