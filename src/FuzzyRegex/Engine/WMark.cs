using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM'S, D11: what a w-mark records about the text to come. A partial call's second
/// pass matches against the held text followed by an unknown continuation <c>w</c>, and a mark
/// says that the path on the backtracking stack above it depends on <c>w</c>
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.2).
/// </summary>
internal enum WMarkKind : byte
{
    /// <summary>The path read <c>w</c>: an undetermined test at the edge passed, or a run-out.</summary>
    Touch,

    /// <summary>
    /// At a construct's frame: its cut was blocked, or its body succeeded only for some <c>w</c>,
    /// so the alternatives inside it are reached only for some continuations.
    /// </summary>
    CutTaint,

    /// <summary>
    /// At a verb scope's frame: a <c>(*PRUNE)</c> or <c>(*SKIP)</c> that unwinds to this scope was
    /// skipped.
    /// </summary>
    VerbTaint,
}

/// <summary>NOT UPSTREAM'S, D11: one w-mark, live while its position is on the backtracking stack.</summary>
/// <param name="Position">The index of the backtracking-stack entry the mark belongs to.</param>
/// <param name="Kind">What the mark records.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WMark(int Position, WMarkKind Kind);

/// <summary>
/// NOT UPSTREAM'S, D11: the second-pass events the port grid counts per row (design 8.1, item 2),
/// through <see cref="MatchState.NotePartialEvent"/>.
/// </summary>
internal enum PartialEvent
{
    /// <summary>M1: a test at the edge that some continuations pass and others fail.</summary>
    UndeterminedRead,

    /// <summary>M1: a test at the edge decided for every continuation, differently from the first pass.</summary>
    DecidedReadDiffering,

    /// <summary>M2: a touch mark was pushed.</summary>
    TouchPushed,

    /// <summary>M2: an atomic group, possessive repeat or <c>\X</c> could not cut.</summary>
    CutBlockedAtomic,

    /// <summary>M2: a lookaround could not cut.</summary>
    CutBlockedLookaround,

    /// <summary>M2: a condition's test could not cut.</summary>
    CutBlockedCondition,

    /// <summary>M2: a negative lookaround or negative test passed undetermined.</summary>
    NegativeUndetermined,

    /// <summary>M2: a condition kept both branches.</summary>
    ConditionBothWays,

    /// <summary>M2: a <c>(*PRUNE)</c> or <c>(*SKIP)</c> reached by backtracking did nothing.</summary>
    VerbSkipped,

    /// <summary>M2: a consuming item inside a lookaround body or condition test ran out of text.</summary>
    RunOutInBody,

    /// <summary>M2: a run-out tainted the constructs open inside its body.</summary>
    RunOutTaintSet,

    /// <summary>M2: a group inside a body was given the unknown capture.</summary>
    UnknownCaptureSet,

    /// <summary>M2: a lookaround success was not continued again, as a repeat of an earlier one.</summary>
    Dedup,

    /// <summary>M2: a lookaround or condition body's outcome was replayed from the memo.</summary>
    MemoReplay,

    /// <summary>M2: a repeat guard was not recorded because a taint lives.</summary>
    GuardRefused,

    /// <summary>M3: a second-pass success was reported as a partial.</summary>
    SuccessTurnedPartial,

    /// <summary>M3: a second-pass success under <c>fullmatch</c> backtracked instead.</summary>
    FullmatchBacktrack,

    /// <summary>M3: the prefilter admitted a start through an undetermined test.</summary>
    PrefilterCandidate,

    /// <summary>M3: the prefilter found no start and the search answered the edge partial.</summary>
    PrefilterNoCandidatePartial,

    /// <summary>M4: the fork budget fired.</summary>
    BudgetFired,

    /// <summary>RC5: a fuzzy insertion ran out of text.</summary>
    InsertionRunOut,
}
