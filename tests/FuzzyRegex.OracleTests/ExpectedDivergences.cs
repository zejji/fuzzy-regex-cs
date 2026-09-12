namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// The divergences a wave is allowed to contain, each named, reasoned and pinned by a permanent test.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this is for.</b> Some generators produce a handful of rows in every wave where this port
/// and upstream genuinely differ and the difference has been judged - upstream's answer comes from a
/// start-position prefilter this port defers to Phase 7, or from a defect in upstream. Before S33 a
/// whole generator was kept off the default list for that handful, which is a hundred per cent of
/// its coverage traded for a few per cent of its rows (<c>tools/run-oracle.ps1</c>'s Generator note
/// has the history). Classifying the known families instead put <c>partial-sliced</c> into the
/// default run.
/// </para>
/// <para>
/// <b>It did not put <c>verbs</c> there, and that is worth knowing before adding an entry.</b>
/// Classifying the judged families is necessary and was not sufficient: the S33 blind review ran the
/// generators at seeds no earlier slice had used and found a fifth, unjudged <c>(*SKIP)</c> family
/// plus two more elsewhere. An entry here accounts for a divergence somebody has already judged; it
/// is not a way to make a wave green.
/// </para>
/// <para>
/// <b>What it is NOT.</b> It is not <c>xfail_strict</c>, and pretending otherwise would be the
/// Chromium <c>TestExpectations</c> failure mode the ROADMAP's Phase 6 entry warns about. A wave's
/// rows are generated from a random seed, so there is no stable row identity to pin, and a predicate
/// over the row and upstream's answer alone - the two halves that do not change when this port does -
/// is nowhere near tight enough to demand a divergence: "a search, asked with <c>partial</c>, where
/// upstream answered a partial" describes hundreds of rows a wave agrees on. So each predicate here
/// includes what THIS PORT answers, which makes it narrow and makes it silent the day the port's
/// answer changes.
/// </para>
/// <para>
/// <b>What makes it strict anyway.</b> Each entry carries <see cref="ExpectedDivergence.Example"/>,
/// the minimised row the family was found on, recorded by <c>tools/record-oracle.py --rows</c>.
/// <c>OracleWaveTests.Every_expected_divergence_still_diverges</c> runs those through this port on
/// every oracle run and fails if one has stopped diverging. That is the staleness alarm, and it does
/// not depend on a seed. The gap tests named in <see cref="ExpectedDivergence.PinnedBy"/> are the
/// second, in the ordinary suite, where they also fail the ratchet.
/// </para>
/// <para>
/// <b>What it masks, said out loud.</b> A real engine defect that happened to take the exact shape of
/// an entry would be classified rather than reported. That is the price; the report prints every
/// classified row's id and count rather than dropping them, and the predicates are as narrow as the
/// evidence allows - which is not the same for all three. <c>search-start-partial</c> carries a
/// recorded discriminator, <c>searchOnlyPartial</c>, and a control proved it necessary: without it,
/// reverting S33's own fix produced zero reported divergences over 2,000 rows at three seeds.
/// <c>reverse-fullmatch-narrowed-slice</c> is narrow by construction - four conditions on the row
/// and two exact answer shapes. <c>search-start-skip-slice</c> has none and is the one to tighten
/// first; its own entry says how.
/// </para>
/// </remarks>
internal static class ExpectedDivergences
{
    /// <summary>Upstream's <c>REVERSE</c> flag bit, which is <c>regex.R</c>.</summary>
    private const int _reverse = 0x400;

    /// <summary>The operations that scan for more than one match.</summary>
    private static readonly string[] _scans = ["finditer", "finditer-overlapped", "split", "sub", "subf"];

    private static readonly ExpectedDivergence[] _entries =
    [
        new(
            Id: "search-start-partial",
            Reason: "Upstream's `search_start` prefilter (upstream/src/_regex.c:8385) gives every "
                + "scanner a partial-match arm of its own; the slow path this port runs has none, and "
                + "neither does upstream's. Upstream's own `match` and `fullmatch` answer None to the "
                + "same row, so the two doors disagree at one position. Port right - "
                + "docs/plan/2026-09-12-divergence-research.md, judged against PCRE2 10.47 and "
                + "upstream issue 589. Phase 7 owns the prefilter and must not import this answer.",
            PinnedBy: "PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_"
                + "subject_finds_no_partial_here",
            Example: """
            {"generator": "partial", "pattern": "(?r)\\b$", "flags": 0, "namedLists": {}, "subject": "", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
            """,
            Applies: static (row, ours) =>
                row.Partial
                && string.Equals(row.Operation, "search", StringComparison.Ordinal)
                && row.Expected is MatchOutcome { Partial: true }
                // The discriminator, and the reason this entry is safe. `search_start` is consulted
                // on a search and nowhere else, so the family's signature is upstream's own `match`
                // DENYING the partial its `search` reported at the same position - recorded per row
                // by tools/record-oracle.py. Without this the predicate also swallowed a genuine
                // engine defect: control S33-A, which reverts this slice's own fix, reported ZERO
                // divergences over 2,000 rows at three seeds until the field was added.
                && row.SearchOnlyPartial
                && ours is NoMatchOutcome
        ),
        new(
            Id: "reverse-fullmatch-narrowed-slice",
            Reason: "Upstream bug. `try_match`'s RE_OP_SUCCESS arm (:7829) bounds a reversed "
                + "fullmatch by `text_start`, where `basic_match`'s own SUCCESS opcode (:15167) and "
                + "the search loop (:11880) both bound it by `slice_start`. Only a GENERAL repeat "
                + "consults `try_match` for its tail, so the symptom needs `(?r)`, a fullmatch, "
                + "pos > 0 and a repeat whose body is not a single character. Two symptoms: a match "
                + "lost outright, and a complete zero-width match reported as a partial. Measured "
                + "2026-09-12, .scratch/up-rev-fullmatch.py and .scratch/row756.py.",
            PinnedBy: "ReverseMatchingTests.A_reverse_fullmatch_of_a_repeat_over_a_narrowed_slice_" + "succeeds_here",
            Example: """
            {"generator": "partial-sliced", "pattern": "(?r)(ab)+", "flags": 0, "namedLists": {}, "subject": "xabz", "operation": "fullmatch", "pos": 1, "endpos": 3, "codepointSlice": [1, 3], "codepointSpan": null, "outcome": {"kind": "nomatch"}}
            """,
            Applies: static (row, ours) =>
                string.Equals(row.Operation, "fullmatch", StringComparison.Ordinal)
                && row.Pos > 0
                && IsReversed(row)
                && (
                    // Upstream lost the match outright.
                    (row.Expected is NoMatchOutcome && ours is MatchOutcome)
                    // Or it kept the span and called a complete match a partial one.
                    || (
                        row.Expected is MatchOutcome { Partial: true } expected
                        && ours is MatchOutcome { Partial: false } got
                        && string.Equals(
                            (expected with { Partial = false }).Describe(),
                            got.Describe(),
                            StringComparison.Ordinal
                        )
                    )
                )
        ),
        new(
            Id: "search-start-skip-slice",
            Reason: "Upstream's `search_start_*_rev` scanners bound themselves with "
                + "`text_end`/`text_start` where the `try_match_*` predicates `basic_match` consults "
                + "bound themselves with `slice_end`/`slice_start`. Nothing but a `(*SKIP)` moves the "
                + "slice inside an attempt, so the two agree everywhere else; once one does, "
                + "upstream's fast path walks past a start position its own slow path accepts. Port "
                + "right on every `(*SKIP)` case PCRE2 10.47 can be asked - "
                + "docs/plan/2026-09-12-divergence-research.md. Phase 7 owns the prefilter.",
            PinnedBy: "BacktrackingVerbTests.Skip_under_reverse_tries_a_start_position_upstreams_"
                + "search_start_skips",
            Example: """
            {"generator": "verbs", "pattern": "(?r)(?:a*(*SKIP)b|[^a-f])$", "flags": 8, "namedLists": {}, "subject": "\nb", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}}
            """,
            // THE BROADEST OF THE THREE, and the one to tighten first. Unlike
            // `search-start-partial` above there is no recorded discriminator here, so this
            // classifies any reversed `(*SKIP)` scan whose two answers differ - which is a real
            // engine defect of that shape as well as the judged family. The discriminator that
            // would work is the analogue of `searchOnlyPartial`: record, per row, the span
            // upstream's own anchored `match` produces at each position, and classify only when
            // every match the port found that upstream's scan did not is one upstream's own
            // matcher accepts at that position. Designed 2026-09-12, not built - it belongs with
            // Phase 6's oracle hardening. Until then the alarm for this family is S29's four
            // controls plus the example row below.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                && _scans.Contains(row.Operation, StringComparer.Ordinal)
                // Both sides produced an answer of the same kind: one side crashing is never this.
                && row.Expected.GetType() == ours.GetType()
        ),
    ];

    /// <summary>Every entry, so a test can hold each one's example to account.</summary>
    public static IReadOnlyList<ExpectedDivergence> All => _entries;

    /// <summary>Which entry, if any, accounts for a divergence.</summary>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns>The entry, or <see langword="null"/> if the divergence is unaccounted for.</returns>
    public static ExpectedDivergence? For(OracleRow row, IOracleOutcome ours)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(ours);

        return Array.Find(_entries, entry => entry.Applies(row, ours));
    }

    /// <summary>
    /// Whether the row's pattern runs right to left, by either of the two ways it can say so.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> if it is reversed.</returns>
    private static bool IsReversed(OracleRow row) =>
        (row.Flags & _reverse) != 0 || row.Pattern.Contains("(?r", StringComparison.Ordinal);
}

/// <summary>One judged, pinned family of divergence.</summary>
/// <param name="Id">The short name the report prints.</param>
/// <param name="Reason">Why the two engines differ, and which one is right.</param>
/// <param name="PinnedBy">The permanent test in the ordinary suite that holds this port's answer.</param>
/// <param name="Example">
/// The minimised row, as one line of recorder output. Run against this port on every oracle run: if
/// it stops diverging, the entry is stale and the run fails.
/// </param>
/// <param name="Applies">
/// Whether a divergence belongs to this family. Takes this port's answer as well as the row, because
/// no predicate over the row and upstream's answer alone is narrow enough to be useful - see the
/// class remarks.
/// </param>
internal sealed record ExpectedDivergence(
    string Id,
    string Reason,
    string PinnedBy,
    string Example,
    Func<OracleRow, IOracleOutcome, bool> Applies
);
