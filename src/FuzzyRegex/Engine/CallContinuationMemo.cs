using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM (D99, the continuation memo): the per-state part. See
/// <see cref="CallContinuationMemo"/>.
/// </summary>
internal sealed class CallContinuations
{
    /// <summary>The keys of the calls backtracked past in this attempt, compared by content.</summary>
    internal HashSet<long[]> Failed = new(new FailedCallKeyComparer());

    /// <summary>
    /// The snapshots of <see cref="Levels"/>, one after another: for each open call, the caller's
    /// state when the call was made, which is what its return gives back: the capture counter and
    /// the repeats and optional passes around the call site (<see cref="CallContinuationTables.Around"/>),
    /// four values each; nothing where nothing is around the site.
    /// </summary>
    internal readonly List<long> Snapshots = [];

    /// <summary>
    /// For each entry of <see cref="MatchState.OpenCalls"/>, in the same order: where its snapshot
    /// starts in <see cref="Snapshots"/> and how long it is, the open section frame when the call
    /// was made, and the id <see cref="Links"/> gave the continuation its return leads to, or -1
    /// until a key first needs it.
    /// </summary>
    internal readonly List<(int SnapshotAt, int SnapshotLength, int SectionFrame, int Link, bool Late)> Levels = [];

    /// <summary>
    /// How many entries of <see cref="Levels"/> hold an iteration or pass start after the position
    /// their call was made at (<see cref="CallContinuationMemo.LateStart"/>); while any does, no
    /// key is built.
    /// </summary>
    internal int LateLevels;

    /// <summary>
    /// The continuations seen in this attempt, interned: each is its parent's id, the called group,
    /// the return node and the id in <see cref="Tails"/> of the rest of what its level holds
    /// (<see cref="CallContinuationMemo.LinkOf"/>), so two ids are equal exactly when the
    /// continuations are. Id 0 is the empty continuation. A struct key, so interning a level
    /// allocates nothing once its tail has been seen: a deep recursion makes a new link at every
    /// level, nearly always with the same tail.
    /// </summary>
    internal Dictionary<(int Parent, int Callee, int ReturnNode, int Tail), int> Links = [];

    /// <summary>The tails of <see cref="Links"/>, interned by content.</summary>
    internal Dictionary<long[], int> Tails = new(new FailedCallKeyComparer());

    /// <summary>A link being built.</summary>
    internal readonly List<long> Link = [];

    /// <summary>
    /// One entry per <c>GROUP_CALL</c> entry on the backtracking stack, innermost last: where it lies,
    /// the key to record when backtracking passes it (null when it is not to be recorded), and
    /// whether the audit flagged it.
    /// </summary>
    internal readonly List<(int BstackAt, int KeyAt, int KeyLength, bool Flagged)> Entries = [];

    /// <summary>
    /// The keys of <see cref="Entries"/>, one after another (<c>KeyAt</c>, <c>KeyLength</c>; a
    /// length of -1 for an entry not to be recorded), so opening a call allocates nothing.
    /// </summary>
    internal readonly List<long> EntryKeys = [];

    /// <summary>The key being built.</summary>
    internal readonly List<long> Key = [];

    /// <summary>The live snapshot while a key is built.</summary>
    internal readonly List<long> Live = [];

    /// <summary>The key for the call about to be opened, from the lookup to the push.</summary>
    internal bool PendingKey;

    /// <summary>Whether the audit flagged the call about to be opened.</summary>
    internal bool PendingFlagged;

    /// <summary>How many flagged entries <see cref="Entries"/> holds.</summary>
    internal int Flagged;

    /// <summary>How many keys the memo has built in this pass (<see cref="CallContinuationMemo.Pays"/>).</summary>
    internal long KeysThisPass;

    /// <summary>How many of <see cref="KeysThisPass"/> hit.</summary>
    internal long HitsThisPass;

    /// <summary>
    /// Forgets everything, for a new attempt or a reused state. Clear costs the set's capacity, so a
    /// set that grew large is dropped instead, as <c>MatchState.InitCallMemo</c> drops the
    /// failed-call memo's.
    /// </summary>
    internal void Clear()
    {
        Snapshots.Clear();
        Levels.Clear();
        LateLevels = 0;
        Entries.Clear();
        EntryKeys.Clear();
        Flagged = 0;
        PendingKey = false;
        PendingFlagged = false;
        if (Failed.Count > 4096)
        {
            Failed = new HashSet<long[]>(new FailedCallKeyComparer());
        }
        else
        {
            Failed.Clear();
        }

        if (Links.Count > 4096)
        {
            Links = [];
        }
        else
        {
            Links.Clear();
        }

        if (Tails.Count > 4096)
        {
            Tails = new Dictionary<long[], int>(new FailedCallKeyComparer());
        }
        else
        {
            Tails.Clear();
        }
    }
}

/// <summary>
/// NOT UPSTREAM (D99, the continuation memo): what it reads of the pattern. See
/// <see cref="CallContinuationMemo"/>.
/// </summary>
internal sealed class CallContinuationTables
{
    /// <summary>
    /// Per repeat, the count above which its count reads the same: its minimum where its maximum is
    /// unbounded, as <see cref="Matcher.MemoCapsFor"/> caps it; otherwise no cap.
    /// </summary>
    internal readonly long[] Caps;

    /// <summary>
    /// Per node, by <see cref="Node.Index"/>: for a call site's return node, the repeats (by index)
    /// and the optional passes (by slot plus the repeat count) whose body holds the call site, the
    /// only ones the code after the return can read before it enters them afresh. Null for any
    /// other node, and for a return node with none around it.
    /// </summary>
    internal readonly int[]?[] Around;

    private CallContinuationTables(long[] caps, int[]?[] around)
    {
        Caps = caps;
        Around = around;
    }

    /// <summary>
    /// The tables, or null where the memo is off for the pattern: see the remarks of
    /// <see cref="CallContinuationMemo"/>.
    /// </summary>
    /// <param name="pattern">The pattern, with its nodes numbered and its optional passes' ends known.</param>
    /// <param name="testsAGroup">Whether a backreference or condition tests a group.</param>
    /// <returns>The tables, or null.</returns>
    internal static CallContinuationTables? For(PatternObject pattern, bool testsAGroup)
    {
        if (!pattern.UseCallMemo || !pattern.IsFuzzy || testsAGroup || (pattern.Flags & RegexFlags.Reverse) != 0)
        {
            return null;
        }

        long[] caps = new long[pattern.RepeatCount];
        Array.Fill(caps, long.MaxValue);
        var around = new List<int>?[pattern.NodeList.Count];
        foreach (Node node in pattern.NodeList)
        {
            if (node.Op is Opcode.GreedyRepeat or Opcode.LazyRepeat)
            {
                int index = (int)node.Values[0];
                if (node.Values[2] == uint.MaxValue)
                {
                    caps[index] = node.Values[1];
                }

                MarkCallSites(
                    node.Next1.Node,
                    n => n.Op is Opcode.EndGreedyRepeat or Opcode.EndLazyRepeat && (int)n.Values[0] == index,
                    index,
                    around
                );
            }
            else if (pattern.OptionalPassEndOf?[node.Index] is { } passEnd)
            {
                MarkCallSites(
                    node,
                    n => ReferenceEquals(n, passEnd),
                    pattern.RepeatCount + (int)passEnd.Values[0],
                    around
                );
            }
        }

        return new CallContinuationTables(caps, [.. around.Select(static list => list?.ToArray())]);
    }

    /// <summary>
    /// Adds <paramref name="item"/> to the list of the return node of every call site reachable from
    /// <paramref name="first"/> before a node that <paramref name="end"/> accepts.
    /// </summary>
    /// <param name="first">The first node of the body.</param>
    /// <param name="end">Accepts the node that ends the body.</param>
    /// <param name="item">The repeat or pass.</param>
    /// <param name="around">The lists, by node index.</param>
    private static void MarkCallSites(Node? first, Func<Node, bool> end, int item, List<int>?[] around)
    {
        var seen = new HashSet<int>();
        var todo = new Stack<Node>();
        if (first is not null)
        {
            todo.Push(first);
        }

        while (todo.Count > 0)
        {
            Node node = todo.Pop();
            if (end(node) || !seen.Add(node.Index))
            {
                continue;
            }

            if (node.Op == Opcode.GroupCall && node.Next1.Node is { } returnNode)
            {
                List<int> list = around[returnNode.Index] ??= [];
                if (!list.Contains(item))
                {
                    list.Add(item);
                }
            }

            foreach (Node? next in (ReadOnlySpan<Node?>)[node.Next1.Node, node.Next2.Node, node.TrueNode])
            {
                if (next is not null)
                {
                    todo.Push(next);
                }
            }
        }
    }
}

/// <summary>
/// NOT UPSTREAM (D99, the continuation memo). A group call fails at once when an earlier call in
/// this attempt, with the same entry key and an equivalent continuation, was backtracked past: every
/// choice inside it and in everything that ran after it returned was tried without a match.
/// </summary>
/// <remarks>
/// <para>
/// The failed-call memo (<c>Matcher.FailedCallKey</c>) records only calls that never returned. In a
/// fuzzy recursion almost every call returns and the failure comes later, in the caller's
/// continuation: <c>(?:(?P&lt;g&gt;a(?&amp;g)b|c)){d&lt;=6}</c> over 'a' * 12 + 'c' made 76,446 Debug
/// steps on main 56fcabfb, nearly all of them calls whose stacks of pending returns were the same.
/// The design, the measurements, the proof obligations and the blind review are in
/// <c>docs/plan/2026-11-17-d99-continuation-keyed-call-memo.md</c>.
/// </para>
/// <para>
/// The key is the failed-call key (which settles the called body up to its return) followed by
/// what the code after each pending return can read. After a call returns, its frame gives back
/// the caller's groups, repeats, optional passes and capture counter as they were at the call
/// (D51, D68: what the frame does not save, the body cannot change), so each level is described by
/// a snapshot taken when its call was made. What the key adds, and why each part is needed:
/// </para>
/// <list type="bullet">
/// <item>The return node and the called group of each level, the call's own first: the code that
/// runs after each return (blind review F2, <c>CallContinuationMemoTests.The_key_holds_where_the_call_returns_to</c>).</item>
/// <item>Every open fuzzy section frame's saved counts and node, unclamped: <c>END_FUZZY</c> charges
/// later errors to the outer sections' exact counts, where the failed-call key clamps their rooms to
/// the inner section's (F1, <c>The_key_holds_the_exact_counts_of_the_sections_outside_the_call</c>).</item>
/// <item>For each level, and for the live state, the repeats and optional passes whose body holds
/// that level's call site (F5; <see cref="CallContinuationTables.Around"/>): each repeat's count,
/// capped as <see cref="Matcher.MemoCapsFor"/> caps it, and for an iteration or pass that began at this
/// position, its capture-change difference and the fuzzy changes since, by kind. The
/// empty-iteration readers (<c>FuzzyIterationStands</c>, <c>EmptyIterationAdmitted</c>,
/// <c>OptionalPassAdmitted</c>) read those only while the position is where the iteration or pass
/// began, and positions only move forward here, so a start before this position is never read
/// again. A repeat that does not hold the site is entered afresh before it is read.</item>
/// <item>For each pending call made at this position with the reach as it is now, its section and
/// the counts the re-entry guard compares (F3). A call made earlier, or with a smaller reach, can
/// never equal a later call's guard key, since the reach only grows within an attempt.</item>
/// <item>Whether the last change is an insertion just behind the position
/// (<c>Matcher.DeletionRepeatsAnEarlierAlternative</c>).</item>
/// </list>
/// <para>
/// Off where the failed-call memo is off, for a pattern that is not fuzzy (repeat guards are read
/// there, and a call restores only the repeats it saves), that tests a group (no read group then
/// exists, so no parked group start or read value needs a key, and D17's empty-iteration states,
/// which are never undone on backtracking, are never read), or that matches in reverse. The set is
/// emptied at every attempt (F4): within one attempt the reach a second call starts with equals the
/// first's and only grows, so the first call's search grew nothing, and skipping the second's
/// changes no later guard decision.
/// </para>
/// </remarks>
internal static class CallContinuationMemo
{
    /// <summary>The most keys <see cref="CallContinuations.Failed"/> records in one attempt.</summary>
    /// <remarks>
    /// SHORTCUT: past 2^20 keys the set stops recording and only answers from what it holds, as the
    /// failed-call memo does: correct, and slower only on an attempt that fails that many calls.
    /// </remarks>
    private const int _cap = 1 << 20;

    /// <summary>Whether the memo is on for the pass about to start, and readies its state if so.</summary>
    /// <param name="state">The match state, with the failed-call threshold set for the pass.</param>
    /// <returns><see langword="true"/> if it is on.</returns>
    internal static bool StartPass(MatchState state)
    {
        if (
            state.Pattern.ContinuationTables is null
            || state.Pattern.SkipContinuationMemo
            || state.CallMemoThreshold == long.MaxValue
        )
        {
            return false;
        }

        CallContinuations memo = state.Continuations ??= new CallContinuations();
        memo.KeysThisPass = 0;
        memo.HitsThisPass = 0;
        StartAttempt(state);
        return true;
    }

    /// <summary>Empties the memo and the open-call records for a new attempt (F4).</summary>
    /// <param name="state">The match state.</param>
    internal static void StartAttempt(MatchState state) => state.Continuations!.Clear();

    /// <summary>
    /// Builds the key of the call about to be made, from the failed-call key just built, and reports
    /// whether a call with that key was backtracked past in this attempt. In audit mode
    /// (<see cref="PatternObject.AuditContinuationMemo"/>) a hit is flagged, not taken.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The <c>GROUP_CALL</c> node.</param>
    /// <param name="keyed">Whether the failed-call memo built its key for this call.</param>
    /// <returns><see langword="true"/> to fail the call now.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool AlreadyFailed(MatchState state, Node node, bool keyed)
    {
        CallContinuations memo = state.Continuations!;
        memo.PendingKey = false;
        memo.PendingFlagged = false;
        if (!keyed || memo.Links.Count >= _cap || memo.LateLevels > 0 || !Pays(memo))
        {
            return false;
        }

        memo.KeysThisPass++;

        ReadOnlySpan<long> key = BuildKey(state, node);
        if (key.IsEmpty)
        {
            return false;
        }

        WorkCounter.CallContinuationKeyed();
        if (memo.Failed.Count > 0 && memo.Failed.GetAlternateLookup<ReadOnlySpan<long>>().Contains(key))
        {
            WorkCounter.CallContinuationHit();
            memo.HitsThisPass++;
            if (!state.Pattern.AuditContinuationMemo)
            {
                ForgetReturnlessCalls(state);
                return true;
            }

            // Kept for the counterexample report; recording it again changes nothing.
            memo.PendingKey = true;
            memo.PendingFlagged = true;
        }
        else
        {
            memo.PendingKey = true;
        }

        return false;
    }

    /// <summary>
    /// Whether the memo still earns its keep in this pass: after <see cref="_trialKeys"/> keys, it
    /// stops building them unless at least one in <see cref="_trialRatio"/> hit.
    /// </summary>
    /// <remarks>
    /// A key costs about what the failed-call key does, so a deep recursion whose continuations are
    /// all different paid it at every call for nothing: <c>(?P&lt;g&gt;a(?&amp;g)?c)(?:x){s&lt;=1}</c>
    /// over 'a' * 1000 made about 500,000 keys and no hit, 15-20% slower than without the memo. Where
    /// the memo helps, most keys hit (the D99 witness 3,665 of 3,792; D93's call rows 85%).
    /// Stopping is safe: a call with no key is never failed early nor recorded, which only loses
    /// pruning. SHORTCUT: one trial per pass; a pattern whose hits come only after the trial keeps
    /// paying nothing and gains nothing. The upgrade is a trial per window of keys.
    /// </remarks>
    /// <param name="memo">The memo.</param>
    /// <returns><see langword="true"/> to keep building keys.</returns>
    internal static bool Pays(CallContinuations memo) =>
        memo.KeysThisPass < _trialKeys || memo.HitsThisPass * _trialRatio >= memo.KeysThisPass;

    /// <summary>How many keys a pass builds before <see cref="Pays"/> judges the memo.</summary>
    private const long _trialKeys = 1 << 14;

    /// <summary>The fewest hits per key, as one in this many, for the memo to go on.</summary>
    private const long _trialRatio = 64;

    /// <summary>
    /// A hit skips the call's continuation, which runs through every pending return, so an open
    /// call may now never return on this path although it could. The failed-call memo records a call
    /// backtracked past without returning as one that cannot return
    /// (<c>Matcher.RecordFailedCall</c>), so none of the open calls may be recorded there any more:
    /// a later call with the same entry key but another continuation would be failed although its
    /// return leads to a match. <c>(?:(?P&lt;g&gt;[ab](?&amp;g)a+|ba)(?&amp;g)){1&lt;=d&lt;=2}</c> over
    /// 'aabbaa' gave group 1 (0, 1) for (0, 2) before this
    /// (<c>CallContinuationMemoTests.A_hit_keeps_the_open_calls_out_of_the_failed_call_memo</c>).
    /// </summary>
    /// <param name="state">The match state.</param>
    private static void ForgetReturnlessCalls(MatchState state)
    {
        List<(long Key, int Reach, int SstackDepth, long[]? MemoKey, int FrameBase, int BstackAt)> open =
            state.OpenCalls;
        for (int i = 0; i < open.Count; i++)
        {
            if (open[i].MemoKey is not null)
            {
                open[i] = open[i] with { MemoKey = null };
            }
        }
    }

    /// <summary>
    /// A call has just been opened (its <c>GROUP_CALL</c> entry pushed at
    /// <paramref name="bstackAt"/>): records its entry and the caller's snapshot.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="bstackAt">Where its <c>GROUP_CALL</c> entry lies.</param>
    /// <param name="returnNode">Where it returns to.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Opened(MatchState state, int bstackAt, Node returnNode)
    {
        CallContinuations memo = state.Continuations!;
        // The key AlreadyFailed has just built is still in Key: nothing between them builds one.
        int keyAt = memo.EntryKeys.Count;
        int keyLength = -1;
        if (memo.PendingKey)
        {
            keyLength = memo.Key.Count;
            memo.EntryKeys.AddRange(memo.Key);
        }

        memo.Entries.Add((bstackAt, keyAt, keyLength, memo.PendingFlagged));
        if (memo.PendingFlagged)
        {
            memo.Flagged++;
        }

        memo.PendingKey = false;
        memo.PendingFlagged = false;
        int sectionFrame = state.Pattern.NestsFuzzySections ? state.OpenCallSections![^1].Frame : -1;
        Debug.Assert(sectionFrame == state.SectionFrame, "D99: the guard's section is the open one");
        PushSnapshot(state, returnNode, state.TextPos, sectionFrame);
    }

    /// <summary>
    /// <c>GROUP_RETURN</c>'s backtrack arm is about to re-open a call: the state is again what the
    /// return gave back, which is what it was when the call was made, so the snapshot is taken again.
    /// </summary>
    /// <remarks>
    /// The position and the open section are not the call's here but where the return left them,
    /// so they come from the call's own record (found by the audit, 2026-11-18).
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="returnNode">Where the call returns to.</param>
    /// <param name="callPos">Where the call was made.</param>
    /// <param name="sectionFrame">The section frame it was made in (<see cref="CallSection.Frame"/>).</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Reopening(MatchState state, Node returnNode, int callPos, int sectionFrame) =>
        PushSnapshot(state, returnNode, callPos, sectionFrame);

    /// <summary>An open call has closed, by its return or by backtracking: drops its snapshot.</summary>
    /// <param name="state">The match state.</param>
    internal static void Closed(MatchState state)
    {
        CallContinuations memo = state.Continuations!;
        Debug.Assert(memo.Levels.Count == state.OpenCalls.Count, "D99: one level per open call");
        CollectionsMarshal.SetCount(memo.Snapshots, memo.Levels[^1].SnapshotAt);
        if (memo.Levels[^1].Late)
        {
            memo.LateLevels--;
        }

        memo.Levels.RemoveAt(memo.Levels.Count - 1);
    }

    /// <summary>
    /// <c>GROUP_CALL</c>'s backtrack arm: every choice inside the call and after its returns has been
    /// tried, so its key is recorded.
    /// </summary>
    /// <param name="state">The match state, with the entry popped.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void BacktrackedPast(MatchState state)
    {
        CallContinuations memo = state.Continuations!;
        (int bstackAt, int keyAt, int keyLength, bool flagged) = memo.Entries[^1];
        memo.Entries.RemoveAt(memo.Entries.Count - 1);

        // The proof's "the arm ran": nothing but this arm takes a GROUP_CALL entry off while the memo
        // is on (no cut can reach a call: PatternObject.UseCallMemo), so the entries pair up.
        Debug.Assert(bstackAt == state.Bstack.Count, "D99: the GROUP_CALL entry popped is the one recorded");
        if (flagged)
        {
            memo.Flagged--;
        }

        // A call that never returned is recorded by the failed-call memo, whose shorter key, a part
        // of this one, fails it first (Matcher.CallAlreadyFailed runs before AlreadyFailed), and
        // which is emptied less often. So only a call that returned, or whose failed-call key a hit
        // cleared (ForgetReturnlessCalls), or one that memo had no room for, is recorded here.
        if (keyLength >= 0 && state.OpenCalls[^1].MemoKey is null && memo.Failed.Count < _cap)
        {
            _ = memo.Failed.Add(CollectionsMarshal.AsSpan(memo.EntryKeys).Slice(keyAt, keyLength).ToArray());
        }

        CollectionsMarshal.SetCount(memo.EntryKeys, keyAt);
    }

    /// <summary>
    /// Debug only: a match is about to succeed. In audit mode, a flagged call still open on the
    /// backtracking stack is one the memo would have failed, so its success is a counterexample.
    /// </summary>
    /// <param name="state">The match state.</param>
    [Conditional("DEBUG")]
    internal static void CheckAudit(MatchState state)
    {
        if (state.ContinuationMemoOn && state.Continuations!.Flagged > 0)
        {
            WorkCounter.CallContinuationCounterexample(
                $"{state.Pattern.PatternText} | {state.Text} | {state.Continuations.Entries.Find(static e => e.Flagged).KeyLength}"
            );
        }
    }

    /// <summary>
    /// The caller state a return gives back that the code after it can read: the capture counter,
    /// then for each repeat around the site its count, iteration start, capture counter and change
    /// count, and for each optional pass its start.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="items">The repeats and passes around the site (<see cref="CallContinuationTables.Around"/>).</param>
    /// <param name="into">Receives 1 + 4 values per item.</param>
    private static void Snapshot(MatchState state, int[] items, Span<long> into)
    {
        into[0] = state.CaptureChange;
        int at = 1;
        foreach (int item in items)
        {
            if (item < state.Repeats.Length)
            {
                RepeatData repeat = state.Repeats[item];
                into[at++] = repeat.Count;
                into[at++] = repeat.Start;
                into[at++] = repeat.CaptureChange;
                into[at++] = repeat.ChangesAtStart;
            }
            else
            {
                OptionalPassStart pass = state.OptionalPasses[item - state.Repeats.Length];
                into[at++] = pass.TextPos;
                into[at++] = pass.Changes;
                into[at++] = pass.CaptureChange;
                into[at++] = pass.Verbs;
            }
        }
    }

    /// <summary>
    /// Whether a snapshot holds an iteration or pass start after the position the call was made at. A slot can hold
    /// a start that this activation did not write: <c>(?:(?:(?&amp;g)c?|){2,3}$(?P&lt;g&gt;a(?&amp;g)?)){e&lt;=1}</c>
    /// over 'abyb' made a call at 1 with its optional pass's slot at 4 (found by the counts grid,
    /// 2026-11-18). <see cref="LinkOf"/> keys a level as if every start were before the position
    /// the key is built at, so while such a level is open the memo builds no key, which only loses
    /// pruning; it happened on 3 of 96,768 grid rows.
    /// </summary>
    /// <param name="state">The match state, at the call.</param>
    /// <param name="items">The repeats and passes around the site.</param>
    /// <param name="snapshot">Their values (<see cref="Snapshot"/>).</param>
    /// <param name="callPos">Where the call was made.</param>
    /// <returns><see langword="true"/> if one began after the call's position.</returns>
    internal static bool LateStart(MatchState state, int[] items, ReadOnlySpan<long> snapshot, int callPos)
    {
        int at = 1;
        foreach (int item in items)
        {
            if (snapshot[item < state.Repeats.Length ? at + 1 : at] > callPos)
            {
                return true;
            }

            at += 4;
        }

        return false;
    }

    /// <summary>
    /// Records the level of a call returning to <paramref name="returnNode"/>, made in the state
    /// now: its snapshot and the open section frame.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="returnNode">Where the call returns to.</param>
    /// <param name="callPos">Where the call was made.</param>
    /// <param name="sectionFrame">The section frame it was made in.</param>
    private static void PushSnapshot(MatchState state, Node returnNode, int callPos, int sectionFrame)
    {
        CallContinuations memo = state.Continuations!;
        List<long> snapshots = memo.Snapshots;
        int at = snapshots.Count;
        int length = 0;
        bool late = false;
        if (state.Pattern.ContinuationTables!.Around[returnNode.Index] is { } items)
        {
            length = 1 + (4 * items.Length);
            CollectionsMarshal.SetCount(snapshots, at + length);
            Span<long> snapshot = CollectionsMarshal.AsSpan(snapshots).Slice(at, length);
            Snapshot(state, items, snapshot);
            late = LateStart(state, items, snapshot, callPos);
        }

        if (late)
        {
            memo.LateLevels++;
        }

        memo.Levels.Add((at, length, sectionFrame, -1, late));
    }

    /// <summary>The key: see the remarks of <see cref="CallContinuationMemo"/>.</summary>
    /// <param name="state">The match state, with <see cref="MatchState.CallMemoKey"/> just built.</param>
    /// <param name="node">The <c>GROUP_CALL</c> node.</param>
    /// <returns>The key, valid until the next call; empty where none can be built (<see cref="FrameIndex"/>).</returns>
    private static ReadOnlySpan<long> BuildKey(MatchState state, Node node)
    {
        CallContinuations memo = state.Continuations!;
        CallContinuationTables tables = state.Pattern.ContinuationTables!;
        List<long> key = memo.Key;
        key.Clear();
        List<long> failedCallKey = state.CallMemoKey!;
        // Exact encoding: every segment whose length varies is preceded by its length (the
        // failed-call key, the section lists, the run of levels made here), and every other one
        // has a length fixed by a value already written before it (a return node fixes the repeats
        // and passes around its site, AddStart's first value fixes its own length, the pattern
        // fixes the section part of a guard entry), so a key decodes one way and equal keys mean
        // equal states.
        key.Add(failedCallKey.Count);
        key.AddRange(failedCallKey);

        int p = state.TextPos;
        List<FuzzyChange> changes = state.FuzzyChanges;
        Node callReturn = node.Next1.Node!;
        key.Add(callReturn.Index);
        key.Add(changes.Count > 0 && changes[^1].Type == FuzzyValue.Ins && changes[^1].Pos == p - 1 ? 1 : 0);

        // The live state is what the call's own return gives back.
        if (tables.Around[callReturn.Index] is { } liveItems)
        {
            List<long> live = memo.Live;
            CollectionsMarshal.SetCount(live, 1 + (4 * liveItems.Length));
            Snapshot(state, liveItems, CollectionsMarshal.AsSpan(live));
            AddLevel(state, key, liveItems, CollectionsMarshal.AsSpan(live));
        }

        // The live level's section frames, those above the innermost call's frame.
        List<(long Key, int Reach, int SstackDepth, long[]? MemoKey, int FrameBase, int BstackAt)> open =
            state.OpenCalls;
        AddSections(state, key, state.SectionFrame, open.Count > 0 ? open[^1].SstackDepth : 0);

        // The pending calls made at this position, innermost first, in full: what the code after
        // their returns reads depends on where matching now stands. Positions only grow along the
        // stack of open calls (a call is made where its caller has got to, and the memo is off in a
        // reverse pattern and wherever a call could sit in a lookbehind), so they are a run at the
        // top. The re-entry guard can refuse a later call against one of them, so for those whose
        // reach is the reach now, which the guard compares (Matcher.TryOpenCall), the section and
        // counts each was made with (blind review F3).
        int reach = state.ReachedWidth;
        bool sections = state.Pattern.NestsFuzzySections;
        ReadOnlySpan<long> snapshots = CollectionsMarshal.AsSpan(memo.Snapshots);
        int i = open.Count - 1;
        int runAt = key.Count;
        key.Add(0);
        for (; i >= 0 && (int)(uint)open[i].Key == p; i--)
        {
            (long callKey, int callReach, int depth, _, _, _) = open[i];
            (int snapshotAt, int snapshotLength, int sectionFrame, _, _) = memo.Levels[i];
            Node returnNode = state.Sstack.NodeAt(state.Pattern, depth - sizeof(long))!;
            key.Add(callKey >> 32);
            key.Add(returnNode.Index);
            if (callReach == reach)
            {
                key.Add(1);
                if (sections)
                {
                    CallSection section = state.OpenCallSections![i];
                    Debug.Assert(section.Frame == sectionFrame, "D99: the level's section is the guard's");
                    long frameIndex = FrameIndex(state, section.Frame);
                    if (frameIndex == -2)
                    {
                        return [];
                    }

                    key.Add(frameIndex);
                    key.Add(section.Sub);
                    key.Add(section.Ins);
                    key.Add(section.Del);
                }
            }
            else
            {
                key.Add(0);
            }

            if (tables.Around[returnNode.Index] is { } items)
            {
                AddLevel(state, key, items, snapshots.Slice(snapshotAt, snapshotLength));
            }

            AddSections(state, key, sectionFrame, i > 0 ? open[i - 1].SstackDepth : 0);
        }

        key[runAt] = open.Count - 1 - i;

        // The calls made before this position: their continuations, interned.
        Debug.Assert(i < 0 || (int)(uint)open[i].Key < p, "D99: positions only grow along the open calls");
        key.Add(i >= 0 ? LinkOf(state, i) : 0);
        return CollectionsMarshal.AsSpan(key);
    }

    /// <summary>
    /// The interned continuation that open call <paramref name="call"/>'s return leads to, for a
    /// call made before the position now. Interns the levels below it that have no id yet, so each
    /// open call is interned once while it stays open, and a key costs the same at any depth.
    /// </summary>
    /// <remarks>
    /// A link is its parent's id, the called group, the return node, the counts of the repeats
    /// around the site (capped as the live ones are), and the section frames the caller had opened
    /// above its own frame. It leaves out where an iteration or pass began and what changed since:
    /// every such start is at or before the call's position, which is before the position now, and
    /// <see cref="AddStart"/> keys a start before the position as a plain 0.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="call">An index into <see cref="MatchState.OpenCalls"/>.</param>
    /// <returns>The id, never 0.</returns>
    internal static int LinkOf(MatchState state, int call)
    {
        CallContinuations memo = state.Continuations!;
        List<(int SnapshotAt, int SnapshotLength, int SectionFrame, int Link, bool Late)> levels = memo.Levels;
        int from = call;
        while (from >= 0 && levels[from].Link < 0)
        {
            from--;
        }

        CallContinuationTables tables = state.Pattern.ContinuationTables!;
        List<long> link = memo.Link;
        for (int i = from + 1; i <= call; i++)
        {
            (long callKey, _, int depth, _, _, _) = state.OpenCalls[i];
            (int snapshotAt, int snapshotLength, int sectionFrame, _, _) = levels[i];
            Debug.Assert(
                i == 0 || (int)(uint)state.OpenCalls[i - 1].Key <= (int)(uint)callKey,
                "D99: positions only grow along the open calls"
            );
            Node returnNode = state.Sstack.NodeAt(state.Pattern, depth - sizeof(long))!;
            link.Clear();
            if (tables.Around[returnNode.Index] is { } items)
            {
                AddStaticLevel(
                    state,
                    link,
                    items,
                    CollectionsMarshal.AsSpan(memo.Snapshots).Slice(snapshotAt, snapshotLength),
                    (int)(uint)callKey
                );
            }

            AddSections(state, link, sectionFrame, i > 0 ? state.OpenCalls[i - 1].SstackDepth : 0);
            ReadOnlySpan<long> tail = CollectionsMarshal.AsSpan(link);
            if (!memo.Tails.GetAlternateLookup<ReadOnlySpan<long>>().TryGetValue(tail, out int tailId))
            {
                tailId = memo.Tails.Count;
                memo.Tails.Add(tail.ToArray(), tailId);
            }

            ref int id = ref CollectionsMarshal.GetValueRefOrAddDefault(
                memo.Links,
                (i > 0 ? levels[i - 1].Link : 0, (int)(callKey >> 32), returnNode.Index, tailId),
                out bool exists
            );
            if (!exists)
            {
                id = memo.Links.Count;
            }

            levels[i] = levels[i] with { Link = id };
            WorkCounter.CallContinuationLevelInterned();
        }

        return levels[call].Link;
    }

    /// <summary>
    /// <see cref="AddLevel"/> for a level whose call was made before the position now, where every
    /// start is before it: the repeats' capped counts alone.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="link">The link.</param>
    /// <param name="items">The repeats and passes (<see cref="CallContinuationTables.Around"/>).</param>
    /// <param name="snapshot">Their values (<see cref="Snapshot"/>).</param>
    /// <param name="callPos">Where the call was made.</param>
    private static void AddStaticLevel(
        MatchState state,
        List<long> link,
        int[] items,
        ReadOnlySpan<long> snapshot,
        int callPos
    )
    {
        long[] caps = state.Pattern.ContinuationTables!.Caps;
        int at = 1;
        foreach (int item in items)
        {
            bool repeat = item < state.Repeats.Length;
            Debug.Assert(
                snapshot[repeat ? at + 1 : at] <= callPos,
                "D99: no key is built while a level holds a late start (LateStart)"
            );
            link.Add(repeat ? Math.Min(snapshot[at], caps[item]) : 0);
            at += 4;
        }
    }

    /// <summary>
    /// The section frames from <paramref name="frame"/> down to, not including, those at or below
    /// <paramref name="floor"/>: each one's saved counts and node, then how many. The link and the
    /// change count are left out: END_FUZZY discards the change count, and the link is the next
    /// frame listed.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="key">The key or link.</param>
    /// <param name="frame">The innermost frame's end, or -1.</param>
    /// <param name="floor">Where the level's part of the saved stack begins.</param>
    private static void AddSections(MatchState state, List<long> key, int frame, int floor)
    {
        ByteStack sstack = state.Sstack;
        int countAt = key.Count;
        key.Add(0);
        int count = 0;
        while (frame > floor && frame >= MatchState.SectionFrameSize)
        {
            key.Add(sstack.SizeAt(frame - (5 * sizeof(long))));
            key.Add(sstack.SizeAt(frame - (4 * sizeof(long))));
            key.Add(sstack.SizeAt(frame - (3 * sizeof(long))));
            key.Add(sstack.NodeAt(state.Pattern, frame - sizeof(long))?.Index ?? -1);
            long link = sstack.SizeAt(frame - MatchState.SectionFrameSize);
            Debug.Assert(link < frame, "D99: a section's link points to a lower frame");
            frame = (int)link;
            count++;
        }

        key[countAt] = count;
    }

    /// <summary>
    /// How many open section frames lie above <paramref name="frame"/>, or -1 outside a section.
    /// </summary>
    /// <remarks>
    /// Asked only for a pending call made at this position; the walk follows the links of the
    /// open frames from the open section down.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="frame">An open section frame's end.</param>
    /// <returns>The count; -2 if the frame is no longer open.</returns>
    private static long FrameIndex(MatchState state, int frame)
    {
        if (frame < 0)
        {
            return -1;
        }

        int at = state.SectionFrame;
        long index = 0;
        while (at > frame)
        {
            at = (int)state.Sstack.SizeAt(at - MatchState.SectionFrameSize);
            index++;
        }

        // Not on the chain of open frames reached from the open section. Reached, though no
        // mechanism has been named: (?:[ab](?R)?a+|a+){i<=1,d<=2} over 'ababbbcbaaab' gives 4 such
        // keys (2026-11-18), with or without the eager switch; mapping it to "no section" changed
        // no answer on any grid. No key can say exactly what the guard would compare against such
        // a frame, so the caller builds none, which only loses pruning.
        return at == frame ? index : -2;
    }

    /// <summary>
    /// One level's repeats and optional passes around its call site, as the code after its return
    /// reads them from this position on.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="key">The key.</param>
    /// <param name="items">The repeats and passes (<see cref="CallContinuationTables.Around"/>).</param>
    /// <param name="snapshot">Their values (<see cref="Snapshot"/>).</param>
    private static void AddLevel(MatchState state, List<long> key, int[] items, ReadOnlySpan<long> snapshot)
    {
        long[] caps = state.Pattern.ContinuationTables!.Caps;
        long captureChange = snapshot[0];
        int at = 1;
        foreach (int item in items)
        {
            if (item < state.Repeats.Length)
            {
                key.Add(Math.Min(snapshot[at], caps[item]));
                AddStart(
                    state,
                    key,
                    (int)snapshot[at + 1],
                    captureChange - snapshot[at + 2],
                    (int)snapshot[at + 3],
                    snapshot[at + 3] >> 32
                );
            }
            else
            {
                AddStart(
                    state,
                    key,
                    (int)snapshot[at],
                    captureChange - snapshot[at + 2],
                    (int)snapshot[at + 1],
                    snapshot[at + 3]
                );
            }

            at += 4;
        }
    }

    /// <summary>
    /// Where an iteration or pass began: 0 if before the position, never read again; otherwise
    /// what the readers compare, relative to the position and the changes made so far.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="key">The key.</param>
    /// <param name="start">Where it began.</param>
    /// <param name="captureDifference">The capture counter at the return less its own.</param>
    /// <param name="changesAtStart">How many fuzzy changes had been made when it began.</param>
    /// <param name="verbs">How many verbs had been crossed when it began.</param>
    private static void AddStart(
        MatchState state,
        List<long> key,
        int start,
        long captureDifference,
        int changesAtStart,
        long verbs
    )
    {
        int p = state.TextPos;
        if (start < p)
        {
            key.Add(0);
            return;
        }

        List<FuzzyChange> changes = state.FuzzyChanges;
        key.Add(start == p ? 1 : 2 + (long)start);
        key.Add(MatchState.GroupChanges(captureDifference));
        key.Add(changes.Count - changesAtStart);
        key.Add(verbs);
        long substitutions = 0;
        long insertions = 0;
        for (int i = Math.Max(0, changesAtStart); i < changes.Count; i++)
        {
            switch (changes[i].Type)
            {
                case FuzzyValue.Sub:
                    substitutions++;
                    break;
                case FuzzyValue.Ins:
                    insertions++;
                    break;
            }
        }

        key.Add(substitutions);
        key.Add(insertions);
        key.Add(changes.Count - Math.Max(0, changesAtStart) - substitutions - insertions);
    }
}
