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

    /// <summary>
    /// At a positive condition test's frame: its body succeeded unblocked, so it holds for every
    /// continuation, with the captures and fuzzy counts of an earlier success whose yes-branch has
    /// already failed. The CONDITIONAL backtracking arm then fails the conditional, as backtracking
    /// past a test that cut does, rather than taking the no-branch (D38).
    /// </summary>
    Held,
}

/// <summary>NOT UPSTREAM'S, D11: one w-mark, live while its position is on the backtracking stack.</summary>
/// <param name="Position">The index of the backtracking-stack entry the mark belongs to.</param>
/// <param name="Kind">What the mark records.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WMark(int Position, WMarkKind Kind);

/// <summary>
/// NOT UPSTREAM'S, D11 M2: one success of a positive lookaround that the second pass continued, the
/// dedup's key (design section 4.2): what follows the lookaround depends on its start, which the
/// frame fixes, and on these.
/// </summary>
/// <param name="Frame">The backtracking-stack position of the lookaround's frame.</param>
/// <param name="Captures">Each group's current capture, or (-1, -1) when it has none.</param>
/// <param name="FuzzyCounts">The fuzzy counts, which count against the enclosing budget (row F1).</param>
/// <param name="FuzzyChanges">Under <c>(?b)</c> and <c>(?e)</c>, the fuzzy changes, which they rank
/// on; otherwise empty, since only the counts reach what follows.</param>
internal sealed record LookaroundSuccess(
    int Frame,
    GroupSpan[] Captures,
    long[] FuzzyCounts,
    FuzzyChange[] FuzzyChanges
)
{
    /// <summary>Whether another success of the same frame leads to the same continuation.</summary>
    /// <param name="other">The other success.</param>
    /// <returns><see langword="true"/> if the keys are equal.</returns>
    internal bool SameContinuation(LookaroundSuccess other) =>
        Frame == other.Frame
        && Captures.AsSpan().SequenceEqual(other.Captures)
        && FuzzyCounts.AsSpan().SequenceEqual(other.FuzzyCounts)
        && FuzzyChanges.AsSpan().SequenceEqual(other.FuzzyChanges);
}

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

    /// <summary>Amendment 2: a blocked positive end moved the saved-stack bytes it parked.</summary>
    BlockedEndMovedSstack,

    /// <summary>Amendment 2: the moved bytes were longer than the parked state (a run-out's residue).</summary>
    BlockedEndMovedMore,

    /// <summary>Amendment 2: a <c>REOPEN</c> put the moved bytes back on the saved stack.</summary>
    ReopenRestoredSstack,

    /// <summary>Amendment 2, repair 1: a <c>REOPEN</c> moved its frame's depth after its call's frame moved.</summary>
    ReopenRebased,

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

    /// <summary>M2: a run-out closed the fuzzy section its body left open.</summary>
    RunOutClosedSection,

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

    /// <summary>
    /// M3: the required-string locator admitted a start because the string runs into the text to
    /// come. It is the only prefilter of the second pass: the start-test prefilter is withheld there.
    /// </summary>
    PrefilterCandidate,

    /// <summary>
    /// M3: a search found no start in the held text, and its attempt from inside the text to come
    /// answered the edge partial.
    /// </summary>
    PrefilterNoCandidatePartial,

    /// <summary>M4: the fork budget fired.</summary>
    BudgetFired,

    /// <summary>RC5: a fuzzy insertion ran out of text.</summary>
    InsertionRunOut,

    /// <summary>RC5 and D41: END_FUZZY's trailing insertion ran out of text at the edge.</summary>
    TrailingInsertionRunOut,

    /// <summary>Chunk 9, amendment 1: a repeat guard held in the second pass.</summary>
    SecondPassGuardHeld,

    /// <summary>
    /// D38: a condition's test held for every continuation after its yes-branch failed (a
    /// <see cref="WMarkKind.Held"/> mark), so the conditional failed instead of taking its no-branch.
    /// </summary>
    HeldFailedTheCondition,

    /// <summary>
    /// D64 review, finding 1: the attempt from inside the text to come ran with the scan's
    /// must-advance flag lifted.
    /// </summary>
    MustAdvanceLifted,

    /// <summary>
    /// A zero-width character test (a first set or a branch's precheck) reached the edge and let
    /// the path through to the item that reads the same character.
    /// </summary>
    ZeroWidthTestDeferred,

    /// <summary>
    /// A lazy one-character repeat that backtracked to the edge kept an entry to take a character
    /// of the text to come.
    /// </summary>
    LazyRepeatPastTheEdge,

    /// <summary>A greedy repeat's body ran out at its end and stayed a run-out.</summary>
    GreedyBodyRunOutKept,

    /// <summary>A run-out outside any lookaround body answered the partial (the rest is presumed).</summary>
    RunOutAnswered,

    /// <summary>
    /// A choice point whose test ran out kept its other alternatives on the backtracking stack
    /// before the run-out (the repeat run-out rule).
    /// </summary>
    RunOutKeptAlternatives,

    /// <summary>
    /// The run-out that answered the partial was a lazy one-character repeat's try past the edge
    /// (<see cref="LazyRepeatPastTheEdge"/>'s entry): limit C2 by that mechanism.
    /// </summary>
    RunOutAnsweredByLazyPastTheEdge,

    /// <summary>
    /// The run-out that answered the partial was a greedy repeat's body run-out at its end
    /// (<see cref="GreedyBodyRunOutKept"/>): limit C2 by that mechanism.
    /// </summary>
    RunOutAnsweredByGreedyBodyKept,
}
