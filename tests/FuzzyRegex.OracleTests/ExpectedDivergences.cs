using System.Globalization;

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
/// is not a way to make a wave green. S34 judged those three, added their entries, and only then put
/// <c>verbs</c> on the default list - in that order, which is the order the rule requires.
/// </para>
/// <para>
/// <b>One entry has been REMOVED because the port stopped diverging, and that is the list working.</b>
/// <c>search-start-skip-slice</c> classified every reversed <c>(*SKIP)</c> scan whose answer differed,
/// on S29's verdict that upstream's <c>search_start_END_OF_LINE_rev</c> disagreeing with its own
/// <c>try_match_END_OF_LINE</c> made this port right. An independent verification reversed that on
/// 2026-09-12 - <c>$</c> under MULTILINE is simply false one character before a <c>b</c>, whichever
/// bound the code reads - and S35 fixed the port. The staleness alarm below is what said so: the
/// entry's own example row stopped diverging and reddened the run. Its whole family went with it,
/// rows 519 and 863 included; <c>verbs</c> at 600 rows is now 600 agree, 0 expected, 0 diverge at
/// all three default seeds. The entry was deleted rather than kept "in case", because an entry that
/// does not fire is the rot this list exists to avoid.
/// </para>
/// <para>
/// <b>One entry is for a bug upstream has since fixed, and it is here on purpose.</b>
/// <c>reverse-group-call-direction</c> is issue 614, fixed by commit <c>9398a6d</c>, released in
/// 2026.8.30, and verified fixed on 2026-09-12 against 2026.9.10 (the span is now (3, 6), this
/// port's answer). The oracle records against the pinned release, which is older, so it still
/// diverges here. <c>overlapped-skip-stale-slice</c> was thought to be one consequence of the
/// issue 613 fix (<c>b77694a</c>); it is NOT - re-run against 2026.9.10 the same day, its rows
/// reproduce unchanged, so it stays an open upstream defect (ledger entry 5).
/// </para>
/// <para>
/// <b>THE SYNC MUST RE-RECORD EVERY <see cref="ExpectedDivergence.Example"/>, and that is the step
/// the strictness depends on.</b> An example is recorder output frozen at the version it was taken
/// against, so the staleness alarm below compares this port against upstream-as-it-was: it fires
/// when THIS PORT's answer changes, and it cannot see upstream's answer change. Re-running
/// <c>python tools/record-oracle.py --rows</c> over each example at the new version, and pasting the
/// result back, is what turns an upstream fix into a red run; skipping it leaves entries that quietly
/// stop applying, which is the Chromium <c>TestExpectations</c> rot this list exists to avoid. Two
/// entries are already known to be waiting for exactly that, so the Phase 6 sync has a test of the
/// procedure built in.
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
/// evidence allows - which is not the same for all of them. <c>search-start-partial</c> carries a
/// recorded discriminator, <c>searchOnlyPartial</c>, and a control proved it necessary: without it,
/// reverting S33's own fix produced zero reported divergences over 2,000 rows at three seeds.
/// <c>reverse-fullmatch-narrowed-slice</c> is narrow by construction - four conditions on the row
/// and two exact answer shapes. <c>overlapped-skip-stale-slice</c> carries S34's
/// <c>anchoredScan</c>, upstream's own answer to the same scan taken one match at a time;
/// <c>bounded-lazy-repeat-partial</c> is keyed on individually judged rows and on this port's answer
/// to each, because no predicate for it survives the same test. With
/// <c>search-start-skip-slice</c> gone, no entry left here is wide.
/// </para>
/// </remarks>
internal static class ExpectedDivergences
{
    /// <summary>Upstream's <c>REVERSE</c> flag bit, which is <c>regex.R</c>.</summary>
    private const int _reverse = 0x400;

    /// <summary>
    /// The rows of the bounded-lazy-repeat partial family, recorded by
    /// <c>tools/record-oracle.py --rows</c>. This entry is keyed on the ROWS rather than on a
    /// predicate, which is why they live here as recorder output - see its own
    /// <see cref="ExpectedDivergence.Reason"/> for why no predicate was written.
    /// </summary>
    private const string _boundedLazyRows = """
        {"generator": "partial", "pattern": "^([A-Z]??)__$", "flags": 0, "namedLists": {}, "subject": "__aA ", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial-sliced", "pattern": "(?r)A(.??)", "flags": 0, "namedLists": {}, "subject": "_\ufb03", "operation": "search", "partial": true, "pos": 0, "endpos": 2, "codepointSlice": [0, 2], "oracle": "prefilter-free", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's answer to each row of <see cref="_boundedLazyRows"/>, in the same order, as the
    /// report renders it. Judged one row at a time and held here so the entry is exact on both
    /// sides: an engine change that alters what this port says about a listed row un-classifies it
    /// rather than keeping it hidden.
    /// </summary>
    /// <remarks>
    /// Row 1 was drawn by <c>partial</c> at seed 314159, row 2 by <c>partial-sliced</c> at the same
    /// seed. Row 2 needs its <c>prefilter-free</c> recording to diverge at all: plain upstream
    /// answers (0, 1) to it, exactly as this port does, and only with
    /// <c>locate_required_string</c> switched off does upstream stretch the partial to (0, 2).
    /// Making the quantifier greedy removes the divergence from both rows, which is what says the
    /// lazy repeat is the cause (tools/probes/upstream-partial-prefilter-free.py, 2026-09-12).
    /// </remarks>
    private static readonly string[] _boundedLazyOurs = ["no match", "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial"];

    /// <summary>
    /// Every row of <see cref="_boundedLazyRows"/> by its question - pattern, flags, subject,
    /// operation, partial and slice - mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _boundedLazy = OracleWave
        .ParseRows(_boundedLazyRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _boundedLazyOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

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
            {"generator": "partial-sliced", "pattern": "(?r)(ab)+", "flags": 0, "namedLists": {}, "subject": "xabz", "operation": "fullmatch", "pos": 1, "endpos": 3, "codepointSlice": [1, 3], "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "nomatch"}}
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
            Id: "overlapped-skip-stale-slice",
            Reason: "Upstream bug, and the one S33's review found by running `verbs` at a seed no "
                + "earlier slice had used. A `(*SKIP)` moves `slice_start`/`slice_end` mid-attempt "
                + "(upstream/src/_regex.c:14553) and nothing puts them back: `init_match` (:3404), "
                + "`do_match` (:18121) and `scanner_search_or_match` (:20874) all leave them alone, "
                + "so a scanner carries the slice from one match into the next - and an overlapped "
                + "scan then resumes BELOW it, at `match_pos + 1` (:20903). Three facts place the "
                + "defect upstream. Its own `match` and `search` at every start position agree with "
                + "this port; its scanner returns a match NARROWER THAN THE PATTERN'S MINIMUM WIDTH "
                + "- `regex.compile(r'(?:[^\\d](*SKIP)){2}').finditer('abcde', overlapped=True)` "
                + "yields (2, 3), one character for a pattern that needs two, where its own "
                + "`match('abcde', 2)` is (2, 4); and its answer is not stable, a `gc.collect()` or "
                + "an `open()` between iterations changing it (tools/probes/upstream-overlapped-skip-instability.py, 2026-09-12). "
                + "Upstream has since patched one consequence of exactly this carry-over - commit "
                + "b77694a, issue 613, the `pos == limit` scan stop in GREEDY_REPEAT_ONE's "
                + "backtrack - in the 2026.8.30 release, which is past the pin this oracle records "
                + "against. Whether that patch covers this row is untested here; the Phase 6 sync "
                + "is what answers it.",
            PinnedBy: "BacktrackingVerbTests.An_overlapped_scan_of_a_skip_inside_a_bounded_repeat_"
                + "matches_what_upstreams_own_matcher_accepts",
            Example: """
            {"generator": "verbs", "pattern": "(?:[^\\d](*SKIP)){2}", "flags": 0, "namedLists": {}, "subject": "abcde", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 2]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 2, "captures": [[1, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 3]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [2, 3]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 5]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 2]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 2, "captures": [[1, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 3]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 2, "captures": [[2, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [2, 4]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 5]}]}
            """,
            // The discriminator, and the reason this entry is narrow where `search-start-skip-slice`
            // above is not: `anchoredScan` is upstream's OWN answer to the same scan, asked one
            // match at a time from a fresh state, so the entry demands that upstream contradicts
            // itself AND that this port agrees with upstream's single-shot door, rendering for
            // rendering - every match, every group, every capture. A defect in this port's scan
            // makes the second condition false and is reported.
            //
            // The comparison is over the whole rendering rather than the spans because a carried
            // slice shows in a CAPTURE as readily as in a whole-match span: at seed 314159
            // upstream's overlapped scan of `((?:\p{L}(*SKIP))+)` reports group 1 as (0, 4) for a
            // match spanning (1, 4) - a capture outside its own match, in a pattern with no
            // lookaround to justify one - where its own walk gives (1, 4), which is this port's
            // answer.
            Applies: static (row, ours) =>
                row.AnchoredScan is { } anchored
                && !string.Equals(row.Expected.Describe(), anchored.Describe(), StringComparison.Ordinal)
                && string.Equals(ours.Describe(), anchored.Describe(), StringComparison.Ordinal)
        ),
        new(
            Id: "overlapped-skip-stale-slice-reversed",
            Reason: "Upstream bug, and the same carry-over as `overlapped-skip-stale-slice` seen "
                + "from the reversed side, where `(*SKIP)` moves `slice_end` rather than "
                + "`slice_start` (upstream/src/_regex.c:14545). Nothing puts it back between the "
                + "matches of one scan, so an overlapped scan's next attempt starts later than it "
                + "should, and every span it reports moves right. These two rows were classified by "
                + "`search-start-skip-slice` until S35 deleted that entry; they are what is left "
                + "once the `$`-reads-the-slice defect that entry was really about is fixed, and "
                + "they are a different mechanism - no prefilter is involved.\n"
                + "THREE FACTS, all measured 2026-09-12 against regex 2026.7.19 on "
                + "`(?r)(?:\\p{L}+(*SKIP)\\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\\w\\s]))` over "
                + "'AAAA00'. One: upstream's own single-shot door gives THIS PORT's answer - "
                + "`regex.compile(pat).search('AAAA00', 0, 5)` is (0, 5) with g1 at (4, 5), where "
                + "its overlapped scan reports g1 at (5, 6). Two: (5, 6) lies OUTSIDE the match "
                + "(0, 5) it belongs to, in a pattern with no lookaround to justify one. Three: the "
                + "scan is not merely wrong but memory-unsafe - printing each match as it arrives "
                + "SEGFAULTS the interpreter (exit 139), which is the same instability the forward "
                + "entry records as a `gc.collect()` changing the answer. All three are re-runnable: "
                + "`python tools/probes/upstream-reversed-overlapped-skip.py [--crash]`.",
            PinnedBy: "BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_keeps_every_"
                + "span_where_upstreams_own_single_shot_door_puts_it",
            Example: """
            {"generator": "verbs", "pattern": "(?r)(?:\\p{L}+(*SKIP)\\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\\w\\s]))", "flags": 0, "namedLists": {}, "subject": "AAAA00", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 5]}]}}
            """,
            // Narrow, and shaped by the mechanism rather than by the symptom. A reversed scan
            // visits END positions, so the two engines must agree on every match's end - they are
            // answering at the same places - and a slice_end carried over from the last match can
            // only ever make an attempt start LATER, never earlier. So every span upstream reports
            // must be at or to the right of this port's, and at least one strictly to the right.
            // An ordinary engine defect here moves a span the other way, shortens a match at its
            // end, or changes how many matches there are, and each of those is reported.
            //
            // The hole, said out loud: a port defect that wrongly extended a span LEFTWARDS while
            // keeping its end and the match count would satisfy this. What bounds it is that the
            // reversed walk cannot be recorded to check against - tools/record-oracle.py refuses an
            // `anchoredScan` for a reversed row, because stepping one means moving `endpos`, which
            // truncates the subject and changes what every end-of-subject assertion means. Making
            // that walk conditional on the pattern having no such assertion is the way to close
            // this, and it is Phase 6 oracle hardening.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                && string.Equals(row.Operation, "finditer-overlapped", StringComparison.Ordinal)
                && row.Expected is MatchesOutcome theirScan
                && ours is MatchesOutcome ourScan
                && EveryStaleSliceHasOnlyMovedSpansRight(theirScan, ourScan)
        ),
        new(
            Id: "reverse-group-call-direction",
            Reason: "Upstream bug, and one upstream has already fixed: `build_GROUP()` did not "
                + "propagate the match direction into a called group, so a group called from a "
                + "lookaround whose direction differs from the pattern's ran its body the wrong way "
                + "round. That is issue 614, fixed on 2026-08-30 by commit 9398a6d - one line, "
                + "`subargs.forward = forward;` - and released in 2026.8.30, past the pin this "
                + "oracle records against. The symptom is self-evident rather than a judgement "
                + "call: upstream records a capture whose END PRECEDES ITS START, which it then "
                + "renders as the empty string. `regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b')"
                + ".search('abbaa').spans('g')` is [(2, 1), (0, 2)]; writing the called body out "
                + "instead of calling it - `(?r)(?<g>[ab]+)(?=([ab]+))b` - gives upstream's own "
                + "(2, 5) for it, which is this port's answer. A lookahead inside `(?r)` runs "
                + "forward, which upstream agrees with wherever the body is not a variable repeat: "
                + "`(?r)(?<g>[ab]{2})(?=(?&g))b` records (2, 4). Measured 2026-09-12, "
                + "tools/probes/upstream-reversed-group-call.py. The same defect also surfaces as a capture of length "
                + "ZERO rather than of negative length - seed 31 draws "
                + "`(?r)\\b(?<g>[ab]+)(?=(?&g))b`, where upstream's call records an empty capture "
                + "for `[ab]+`, which needs at least one character - so the entry covers both.",
            PinnedBy: "GroupCallTests.A_group_called_from_a_lookahead_under_reverse_matches_forwards_here",
            Example: """
            {"generator": "recursion", "pattern": "(?r)(?<g>[ab]+)(?=(?&g))b", "flags": 0, "namedLists": {}, "subject": "abbaa", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[2, -1], [0, 2]]}], "lastIndex": 1, "lastGroup": "g", "partial": false}}
            """,
            // Narrow on three counts: the pattern must be reversed AND contain a group call, which
            // is the defect's precondition and not a symptom; the two answers must agree capture
            // for capture everywhere else; and the only captures allowed to differ are ones where
            // upstream recorded nothing - a negative length, or an empty span - and this port
            // recorded text. An ordinary engine defect that shortens or moves a capture is
            // reported, because its upstream side is a real span.
            //
            // The hole, said out loud: a called group whose body CAN match empty would make an
            // empty capture upstream's honest answer, so a port defect that wrongly extended that
            // capture would be classified. Closing it needs the body's minimum width, which the
            // row does not carry.
            Applies: static (row, ours) =>
                IsReversed(row)
                && HasGroupCall(row.Pattern)
                && OnlyDifferenceIsACaptureUpstreamLeftEmpty(row.Expected, ours)
        ),
        new(
            Id: "bounded-lazy-repeat-partial",
            Reason: "Port right, judged in docs/plan/2026-09-12-divergence-research.md and pinned "
                + "with its evidence in PartialMatchingTests: a bounded LAZY repeat that has "
                + "reached its maximum loses its partial here and keeps one upstream, and PCRE2 "
                + "10.47 answers no match to the family's minimal case "
                + "(tools/probes/pcre2-partial-and-skip.py). `^([A-Z]??)__$` over '__aA ' is the "
                + "same shape - making the quantifier greedy, `^([A-Z]?)__$`, removes the partial "
                + "from upstream too (tools/probes/upstream-bounded-lazy-partial.py, 2026-09-12) - and no complete match "
                + "can exist, because '__' would have to be followed by the end of the subject and "
                + "'aA ' already follows it.\n"
                + "KEYED ON ROWS, NOT ON A PREDICATE, deliberately. S33 looked for a discriminator "
                + "and found none that does not also swallow a genuine missed partial: every "
                + "predicate over 'upstream answered a partial and this port did not' describes the "
                + "real defects too. So this entry lists the rows a probe has individually judged, "
                + "and widening it means judging another row and adding it here - not loosening a "
                + "condition. The permanent test is what fixing it would turn red.",
            PinnedBy: "PartialMatchingTests.A_bounded_lazy_repeat_that_reaches_its_maximum_loses_" + "its_partial_here",
            Example: _boundedLazyRows,
            Applies: static (row, ours) =>
                _boundedLazy.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
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

    /// <summary>
    /// The question a row asks, as one string: everything that identifies it across waves, and
    /// nothing either engine answered.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The key.</returns>
    private static string Question(OracleRow row) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{row.Pattern}\0{row.Flags}\0{row.Subject}\0{row.Operation}\0{row.Partial}\0{row.Pos}\0{row.EndPos}"
        );

    /// <summary>
    /// Whether two reversed overlapped scans differ only in the way a carried <c>slice_end</c> can
    /// make them: the same matches, ending in the same places, with the same <c>lastindex</c>,
    /// <c>lastgroup</c> and capture counts, and every one of upstream's spans at or to the right of
    /// this port's, at least one strictly to the right.
    /// </summary>
    /// <param name="upstream">Upstream's scan.</param>
    /// <param name="ours">This port's scan.</param>
    /// <returns><see langword="true"/> if that is the whole of the difference.</returns>
    private static bool EveryStaleSliceHasOnlyMovedSpansRight(MatchesOutcome upstream, MatchesOutcome ours)
    {
        if (upstream.Matches.Count != ours.Matches.Count)
        {
            return false;
        }

        bool moved = false;

        for (int m = 0; m < upstream.Matches.Count; m++)
        {
            MatchOutcome theirs = upstream.Matches[m];
            MatchOutcome mine = ours.Matches[m];

            if (
                theirs.Groups.Count != mine.Groups.Count
                || theirs.Partial != mine.Partial
                // Not derivable from the groups, so compared here or not at all - S35's blind review
                // built a row that differed only in these and was classified.
                || theirs.LastIndex != mine.LastIndex
                || !string.Equals(theirs.LastGroup, mine.LastGroup, StringComparison.Ordinal)
                // A reversed scan visits end positions, so the two are answering at the same place
                // only while the ends agree.
                || theirs.Groups[0].Index + theirs.Groups[0].Length != mine.Groups[0].Index + mine.Groups[0].Length
            )
            {
                return false;
            }

            for (int g = 0; g < theirs.Groups.Count; g++)
            {
                OracleGroup theirGroup = theirs.Groups[g];
                OracleGroup ourGroup = mine.Groups[g];

                if (
                    theirGroup.Success != ourGroup.Success
                    // A capture LOST or GAINED is never a moved slice: the carry-over changes where
                    // an attempt starts, not how many times a group took part.
                    || theirGroup.Captures.Count != ourGroup.Captures.Count
                    // Nor does it change how LONG an inner span is - it shifts the whole thing
                    // right. Only group 0 may change length, because its end is pinned above and
                    // its start is what moved. A capture one character too long is a real defect
                    // and must be reported; S35's blind review built the row that proved it.
                    || (g > 0 && theirGroup.Length != ourGroup.Length)
                )
                {
                    return false;
                }

                if (theirGroup.Success && theirGroup.Index < ourGroup.Index)
                {
                    return false;
                }

                for (int c = 0; c < theirGroup.Captures.Count; c++)
                {
                    if (
                        theirGroup.Captures[c].Index < ourGroup.Captures[c].Index
                        || (g > 0 && theirGroup.Captures[c].Length != ourGroup.Captures[c].Length)
                    )
                    {
                        return false;
                    }

                    moved |= theirGroup.Captures[c] != ourGroup.Captures[c];
                }

                moved |= theirGroup.Index != ourGroup.Index;
            }
        }

        return moved;
    }

    /// <summary>Whether the pattern calls a group by name, which is the defect's precondition.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> if it contains a call.</returns>
    /// <remarks>
    /// The three spellings the <c>recursion</c> generator draws. A numeric call would go
    /// unrecognised and its row would be reported rather than classified, which is the safe way
    /// round for a list whose failure mode is hiding a defect.
    /// </remarks>
    private static bool HasGroupCall(string pattern) =>
        pattern.Contains("(?&", StringComparison.Ordinal)
        || pattern.Contains("(?P>", StringComparison.Ordinal)
        || pattern.Contains(@"\g<", StringComparison.Ordinal);

    /// <summary>
    /// Whether the two matches say the same thing everywhere except at captures upstream recorded
    /// as nothing - a negative length or an empty span - and this port recorded text for. At least
    /// one such capture must exist, or the two answers did not differ in this family's way at all.
    /// </summary>
    /// <param name="upstream">Upstream's answer, one match or a whole scan.</param>
    /// <param name="ours">This port's answer, of the same kind.</param>
    /// <returns><see langword="true"/> if those captures are the whole of the difference.</returns>
    /// <remarks>
    /// A scan as well as a single match, because the generators draw the calling pattern into
    /// <c>finditer</c> rows too, and the defect is per match wherever it appears.
    /// </remarks>
    private static bool OnlyDifferenceIsACaptureUpstreamLeftEmpty(IOracleOutcome upstream, IOracleOutcome ours)
    {
        if (upstream is MatchesOutcome theirScan && ours is MatchesOutcome ourScan)
        {
            return theirScan.Matches.Count == ourScan.Matches.Count
                && !theirScan
                    .Matches.Where(
                        (match, i) =>
                            !string.Equals(match.Describe(), ourScan.Matches[i].Describe(), StringComparison.Ordinal)
                            && !OnlyDifferenceIsACaptureUpstreamLeftEmpty(match, ourScan.Matches[i])
                    )
                    .Any()
                && !string.Equals(theirScan.Describe(), ourScan.Describe(), StringComparison.Ordinal);
        }

        return upstream is MatchOutcome theirs
            && ours is MatchOutcome mine
            && OnlyDifferenceIsACaptureUpstreamLeftEmpty(theirs, mine);
    }

    /// <summary>One match's half of <see cref="OnlyDifferenceIsACaptureUpstreamLeftEmpty(IOracleOutcome, IOracleOutcome)"/>.</summary>
    /// <param name="upstream">Upstream's match.</param>
    /// <param name="ours">This port's match.</param>
    /// <returns><see langword="true"/> if those captures are the whole of the difference.</returns>
    private static bool OnlyDifferenceIsACaptureUpstreamLeftEmpty(MatchOutcome upstream, MatchOutcome ours)
    {
        if (
            upstream.Groups.Count != ours.Groups.Count
            || upstream.LastIndex != ours.LastIndex
            || !string.Equals(upstream.LastGroup, ours.LastGroup, StringComparison.Ordinal)
            || upstream.Partial != ours.Partial
        )
        {
            return false;
        }

        bool replaced = false;

        for (int g = 0; g < upstream.Groups.Count; g++)
        {
            OracleGroup theirs = upstream.Groups[g];
            OracleGroup mine = ours.Groups[g];

            if (
                theirs.Success != mine.Success
                || theirs.Index != mine.Index
                || theirs.Length != mine.Length
                || theirs.Captures.Count != mine.Captures.Count
            )
            {
                return false;
            }

            for (int c = 0; c < theirs.Captures.Count; c++)
            {
                if (theirs.Captures[c] == mine.Captures[c])
                {
                    continue;
                }

                if (theirs.Captures[c].Length > 0 || mine.Captures[c].Length <= 0)
                {
                    return false;
                }

                replaced = true;
            }
        }

        return replaced;
    }
}

/// <summary>One judged, pinned family of divergence.</summary>
/// <param name="Id">The short name the report prints.</param>
/// <param name="Reason">Why the two engines differ, and which one is right.</param>
/// <param name="PinnedBy">The permanent test in the ordinary suite that holds this port's answer.</param>
/// <param name="Example">
/// The minimised row, as one line of recorder output - or every listed row, one per line, for an
/// entry keyed on rows rather than on a predicate. Run against this port on every oracle run: if one
/// stops diverging, the entry is stale and the run fails. Re-record these at every upstream sync;
/// frozen recorder output cannot see upstream's answer change.
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
