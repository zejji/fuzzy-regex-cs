using System.Diagnostics.CodeAnalysis;
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
/// <b>And the same thing happened again at 2000 rows, which is why the entry count keeps moving.</b>
/// S34's wider wave left three <c>verbs</c> rows unjudged at seeds 7 and 4242; S35 fixed one half of
/// what they showed and handed the rest to S36, which judged all three as one family and added
/// <c>overlapped-skip-extra-match-reversed</c>. The default wave is 300 rows per generator and
/// reaches none of them, so a row count is a seed by another name: widen both before believing a
/// green run.
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
/// <b>A SECOND ENTRY HAS BEEN REMOVED, and this one is the sync working rather than a fix of
/// ours.</b> <c>group-call-direction</c> - issue 614, and <c>reverse-group-call-direction</c>
/// before S36 renamed it - was here on purpose while the pin was older than the fix. S44 moved
/// the pin to 2026.9.10, past commit <c>9398a6d</c>, and the re-record the paragraph below
/// demands is exactly what caught it: both of the entry's example rows stopped diverging.
/// Upstream now answers <c>(2, 3)</c> where it recorded <c>(2, -1)</c>, and <c>(0, 1)</c> where
/// it recorded <c>(2, -1)</c> - this port's answers, unchanged since S30 and S36. The two gap
/// tests it named are KEPT and now say upstream agrees; a test that pins the right answer is
/// worth having whoever else agrees with it.
/// </para>
/// <para>
/// <c>overlapped-skip-stale-slice</c> was thought to be one consequence of the issue 613 fix
/// (<c>b77694a</c>); it is NOT - re-run against 2026.9.10 on 2026-09-12, and again by S44 with
/// the clamps ported here, its rows reproduce unchanged, so it stays an open upstream defect
/// (ledger entry 5).
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
    /// The one row of <c>enhancematch-ranks-by-cost</c>, hand-built and recorded by
    /// <c>python tools/record-oracle.py --rows</c> on 2026-09-13. No wave has drawn this family -
    /// see the entry's own reason for why, and why the row is the whole of its strictness.
    /// </summary>
    private const string _costRankedRow =
        """{"generator": "fuzzy", "pattern": "(?e)(?:x|xyq){1i+9s+9d<=20}", "flags": 0, "namedLists": {}, "subject": "xyz", "operation": "fullmatch", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [], "deletions": []}}}""";

    /// <summary>
    /// The one row of <c>bestmatch-ranks-by-cost</c>, the same shape as the row above under the other
    /// flag, hand-built and recorded by <c>python tools/record-oracle.py --rows</c> on 2026-09-13.
    /// It is a <c>fullmatch</c> on purpose: the span is then fixed by the operation, so the whole of
    /// the difference between the two engines is which errors they spent, which is what this entry's
    /// predicate can judge. No wave draws this family - see the entry's own reason.
    /// <para>
    /// The cheaper answer is two SUBSTITUTIONS rather than the insertions the <c>(?e)</c> row above
    /// uses, and that is not a stylistic choice. Steering <c>BESTMATCH</c> onto a candidate whose
    /// fit needs two TRAILING insertions walked into an inherited bug that lost the match outright -
    /// <c>regex.fullmatch(r'(?b)(?:x){e&lt;=3}', 'xyz')</c> is <c>None</c> where the same pattern
    /// without <c>(?b)</c> answers <c>(0, 2, 0)</c> - so an insertion row would have pinned an
    /// unrelated defect instead of this entry's family.
    /// <b>S46 fixed that defect and this row is left as it is anyway</b>: upstream still answers
    /// <c>None</c> to it, so an insertion row would now diverge for the OTHER entry's reason and
    /// this one would stop being about cost ranking. Ledger entry 12;
    /// <c>bestmatch-loses-a-candidate</c> below;
    /// <c>Gaps.Engine.FuzzyBestMatchTests.Bestmatch_keeps_a_match_that_needs_two_trailing_insertions</c>.
    /// </para>
    /// </summary>
    private const string _bestCostRankedRow =
        """{"generator": "fuzzy", "pattern": "(?b)(?:ab|xyc){9i+1s+9d<=20}", "flags": 0, "namedLists": {}, "subject": "abc", "operation": "fullmatch", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2], "deletions": []}}}""";

    /// <summary>
    /// The eleven rows of <c>bestmatch-loses-a-candidate</c>, recorded by
    /// <c>python tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-candidate-rows.jsonl</c> -
    /// rows 1 to 9 on 2026-09-14, rows 10 and 11 on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// <b>These rows are the entry's KEY as well as its staleness alarm since S47b</b>, so a row is
    /// here only because someone ran the probe over it and judged it. Row 1 is ledger entry 12's own
    /// minimised reproduction - upstream loses the match outright.
    /// Row 2 is the same shape as wave row 121859 (seed 4242), an insertion-only budget. Row 3 is
    /// wave row 120771 (seed 4242), where BOTH engines match the same span at the same error count
    /// and the mix differs - upstream spends two substitutions under the flag and one substitution
    /// plus one insertion without it. Row 4 is wave row 125716 (seed 4242), a <c>sub</c>, whose
    /// outcome is a string and a count rather than a match: the flag costs upstream one of the two
    /// replacements it makes without it.
    /// <para>
    /// <b>Rows 5 to 9 are the five the narrowing itself turned red</b>, and they are here because
    /// the owner's 2026-09-14 ruling says a pin widens by judging another row with the probe and
    /// adding it. All five are the 6000-row <c>fuzzy</c> wave: 3179, 3683, 4251 and 5275 at seed
    /// 4242 and 1774 at seed 7. Rows 5, 7 and 8 are the commonest shape, upstream refusing a match
    /// outright over one or two insertions its own flagless engine spends. <b>The other two are
    /// shapes the first four did not have.</b> Row 6 is a <c>partial=True</c> row where upstream
    /// does not lose the match but DOWNGRADES it - a full match with one substitution and one
    /// insertion becomes a partial with two substitutions, because the insertion is what the guard
    /// refuses. Row 9 is a <c>finditer</c> that loses one match of three, which is the first row of
    /// this family whose divergence is inside a scan rather than at its only answer.
    /// </para>
    /// <para>
    /// Every one carries a recorded <c>bestmatchFreeOutcome</c>, which is the second half of the
    /// entry's <see cref="ExpectedDivergence.Applies"/>, so the staleness alarm re-tests the
    /// discriminator and not only the divergence - and on all of them this port's answer is
    /// upstream's own flagless answer exactly, groups, counts and change positions included.
    /// </para>
    /// <para>
    /// <b>Rows 10 and 11 are S52's, and they are the trade this entry's Reason names being paid
    /// rather than anything new about the mechanism.</b> Widening the generators to every Unicode
    /// plane (2026-09-15) drew two fresh questions of the same shape, and the owner's 2026-09-14
    /// ruling says such a row reds the wave until someone judges it with the probe and adds it. Both
    /// were judged that way, <c>.scratch</c>-free, and both are exact:
    /// <list type="bullet">
    /// <item>
    /// Row 10, seed 7 row 41035 of the 2000-row <c>fuzzy</c> wave:
    /// <c>(?b)(?fi)(?:(?:[𝔘😀][ab]){e&lt;=1}){s&lt;=1,i&lt;=1,d&lt;=1}</c> over <c>𝔘S🏻</c>. Upstream
    /// refuses the match outright; with the <c>(?b)</c> deleted it answers (0, 5) with one
    /// substitution at 2 and one insertion at 3, which is this port's answer to the code unit.
    /// <b>The first row of this family whose subject and pattern are both astral</b>, which is why
    /// it had never been drawn - before S52 the <c>fuzzy</c> generator put nothing above U+FFFF in a
    /// pattern at all.
    /// </item>
    /// <item>
    /// Row 11, seed 20260915 row 24462 of the same wave, from <c>interactions</c>: a <c>finditer</c>
    /// over a subject carrying an emoji modifier, two ZERO WIDTH JOINERs and a Deseret capital.
    /// Upstream finds NO match under the flag and one at (4, 5) without it, groups, counts and the
    /// substitution at 5 all matching this port. <b>The first row of the family drawn by a composed
    /// generator rather than by <c>fuzzy</c>.</b>
    /// </item>
    /// </list>
    /// Neither is a new mechanism and neither needed a new argument: the flagless control is the
    /// entry's own discriminator and it answers on both.
    /// </para>
    /// <para>
    /// <b>Rows 12 and 13 are S52 sitting 8's, and they are what the paragraph above predicted</b> -
    /// the 6000-row three-seed gate drawing two more questions of the same shape, one per new seed
    /// it reached. Seed 7 row 76930, an <c>interactions</c> <c>match</c>:
    /// <c>(?b)(?e)(?:[[:alpha:]][[a-f]~~[d-k]]){e&lt;=2}\b</c> over <c>bab_.bB</c>. Seed 4242 row
    /// 122115, a <c>fuzzy</c> <c>fullmatch</c> whose pattern and subject are both astral. Upstream
    /// refuses both outright; with the <c>(?b)</c> deleted it answers (0, 4) with insertions at 2
    /// and 3, and (0, 5) in codepoints with an insertion at 4 and a deletion at 3 - this port's
    /// answers to the code unit. <b>Deleting the <c>(?e)</c> instead leaves both None</b>, so it is
    /// <c>(?b)</c> that loses them and not the pair, which is the ablation this family's own
    /// discriminator does not distinguish and which was measured here rather than assumed. Measured
    /// 2026-09-15 on regex 2026.9.10 as rows 12 and 13 of
    /// <c>python tools/probes/gate-divergence-doors.py --rows
    /// tools/probes/bestmatch-loses-a-candidate-rows.jsonl</c> - the row form of that probe rather
    /// than the seed form, because a judged row is no longer in a gate report for the seed form to
    /// read.
    /// </para>
    /// </remarks>
    private const string _bestmatchLostCandidateRows = """
        {"generator": "fuzzy", "pattern": "(?b)(?:x){e<=3}", "flags": 0, "namedLists": {}, "subject": "xyz", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [], "insertions": [1, 2], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?:abx\\sx){i<=2}", "flags": 0, "namedLists": {}, "subject": "abx bxx", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [], "insertions": [4, 6], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?e)(?:x0bb+?[^a-f]b+?){e<=2}", "flags": 0, "namedLists": {}, "subject": "x0fbbbxba", "operation": "fullmatch", "codepointSpan": [0, 9], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [2, 8], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [null], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [8], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?i)(?:x\\A){e<=3}", "flags": 0, "namedLists": {}, "subject": "aX", "operation": "sub", "template": "<>", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "<>aX", "count": 1}, "bestmatchFreeOutcome": {"kind": "sub", "text": "<><>X", "count": 2}}
        {"generator": "fuzzy", "pattern": "(?b)(?:[^a-f]a0\\Bx\\p{L}){e<=3:[abx]}", "flags": 0, "namedLists": {}, "subject": "zaQa", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 2], "fuzzyChanges": {"substitutions": [], "insertions": [3], "deletions": [2, 3]}}}
        {"generator": "fuzzy", "pattern": "(?b)(?:[ab]*?[^a-f]){e<=2}", "flags": 0, "namedLists": {}, "subject": "0aya", "operation": "fullmatch", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [0, 2], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [null], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [3], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?:[a-f][a-f][^a-f]){1<=e<=2}", "flags": 0, "namedLists": {}, "subject": "badyf", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2, 4], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?:\\s[ab]+){i<=2}", "flags": 0, "namedLists": {}, "subject": "a bax", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [], "insertions": [0, 4], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?:\\W(?:b\\B){d<=1:\\d}){e}", "flags": 0, "namedLists": {}, "subject": ".ba", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 2]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 0, "captures": [[2, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 2], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [2, 3]}, "codepointSpan": [2, 2]}]}, "leakFreeFuzzy": [null, null], "bestmatchFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 2]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 0, "captures": [[2, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 2], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [2, 3]}, "codepointSpan": [2, 2]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 2], "fuzzyChanges": {"substitutions": [], "insertions": [2], "deletions": [2, 3]}, "codepointSpan": [2, 3]}]}}
        {"generator": "fuzzy", "pattern": "(?b)(?fi)(?:(?:[\ud83d\ude00\ud835\udd18][ab]){e<=1}){s<=1,i<=1,d<=1}", "flags": 0, "namedLists": {}, "subject": "\ud835\udd18S\ud83c\udffb", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [3], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)(?:(?P<g1>\\S)\ud83c\udffb){s<=1,i<=1,d<=1}(?:\\p{Ll}(*SKIP)[\\p{L}\\p{N}]|[a-f])", "flags": 16386, "namedLists": {}, "subject": "a\ud83c\udffba\u200d\u200da\ud801\udc00", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}, "bestmatchFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 4, "length": 5, "captures": [[4, 5]]}, {"number": 1, "success": true, "index": 4, "length": 1, "captures": [[4, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [5], "insertions": [], "deletions": []}, "codepointSpan": [3, 7]}]}}
        {"generator": "interactions", "pattern": "(?b)(?e)(?:[[:alpha:]][[a-f]~~[d-k]]){e<=2}\\b", "flags": 264, "namedLists": {}, "subject": "bab_.bB", "operation": "match", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2, 3], "deletions": []}}}
        {"generator": "fuzzy", "pattern": "(?b)(?e)(?:😀𝟮(?:😀bx){1i+1d+1s<=1:[a-s]}){s<=1,i<=1,d<=1}", "flags": 0, "namedLists": {}, "subject": "😀𝟮😀xa", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 8, "captures": [[0, 8]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 1], "fuzzyChanges": {"substitutions": [], "insertions": [7], "deletions": [6]}}}
        """;

    /// <summary>
    /// The two rows of <c>fuzzy-changes-leaked-from-an-abandoned-attempt</c>, one per arm, recorded
    /// by <c>python tools/record-oracle.py --rows</c> on 2026-09-14.
    /// </summary>
    /// <remarks>
    /// Row 1 is ledger entry 11's own minimised reproduction, and it is the STRONG arm: upstream
    /// answers the anchored question - <c>leakFreeFuzzy</c> is one deletion at 3, which is this
    /// port's answer, where the row itself records one substitution at 0 against counts of
    /// <c>(0, 0, 1)</c>. Row 2 is row 77766 of the seed-4242 6000-row gate as the wave drew it, and
    /// it is the WEAK arm: a fuzzy section inside a lookahead before a <c>\K</c>, where all four
    /// <c>leakFreeFuzzy</c> entries are null because <c>endpos</c> cuts the lookahead off and the
    /// reported start is not where the attempt began. Upstream reports match k's substitution at
    /// position k+1 there - the previous attempt's, one per step down the reversed scan.
    /// </remarks>
    private const string _leakedChangeRows = """
        {"generator": "rows", "pattern": "(?:[ab][bc](*PRUNE)[wx]){e<=2}", "flags": 0, "namedLists": {}, "subject": "qab", "operation": "search", "codepointSpan": [1, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 1, "length": 2, "captures": [[1, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [0], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [3]}}]}
        {"generator": "rows", "pattern": "(?e)(?r)(?=(?:\\p{Nd}[a-f]){s<=1,i<=1,d<=1:.})\\K", "flags": 10, "namedLists": {}, "subject": "aaaa", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 0, "captures": [[3, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [3]}, "codepointSpan": [3, 3]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 0, "captures": [[2, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [3], "insertions": [], "deletions": []}, "codepointSpan": [2, 2]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 0, "captures": [[1, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [], "deletions": []}, "codepointSpan": [1, 1]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}, "codepointSpan": [0, 0]}]}, "leakFreeFuzzy": [null, null, null, null]}
        """;

    /// <summary>
    /// The two rows of <c>fuzzy-counts-of-a-partial-are-the-innermost-sections</c>, one per shape,
    /// recorded by <c>python tools/record-oracle.py --rows</c> on 2026-09-14.
    /// </summary>
    /// <remarks>
    /// Row 1 is row 120049 of the seed-7 6000-row gate: upstream reports <c>(0, 3, 0)</c> with
    /// insertions at 2, 5 and 7 where this port reports <c>(0, 5, 0)</c> with those three and two
    /// more, which is the truncation the entry is named for. Row 2 is row 120002 of the same gate and
    /// is the commoner shape - the innermost section had spent nothing, so upstream reports no fuzzy
    /// half at all and the prefix it must be is the empty one.
    /// </remarks>
    private const string _innermostPartialCountRows = """
        {"generator": "rows", "pattern": "(?b)(ab)(?:[ab]*?(?:\\Zoba[^a-f](?:\\1)){e<=3}){i<=2}", "flags": 0, "namedLists": {}, "subject": "abxbaobaya", "operation": "search", "partial": true, "codepointSpan": [0, 10], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 10, "captures": [[0, 10]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": true, "fuzzyCounts": [0, 3, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2, 5, 7], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 3, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2, 5, 7], "deletions": []}}], "searchOnlyPartial": false, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 10, "captures": [[0, 10]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": true, "fuzzyCounts": [0, 3, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2, 5, 7], "deletions": []}}}
        {"generator": "rows", "pattern": "(?i)(?:[ab]*?(?:\\d[^a]){2i+1d+1s<=2:[a-cx-z]}){i<=2}", "flags": 0, "namedLists": {}, "subject": " Axb", "operation": "match", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        """;

    /// <summary>
    /// The rows of the bounded-lazy-repeat partial family, recorded by
    /// <c>tools/record-oracle.py --rows</c>. This entry is keyed on the ROWS rather than on a
    /// predicate, which is why they live here as recorder output - see its own
    /// <see cref="ExpectedDivergence.Reason"/> for why no predicate was written.
    /// </summary>
    private const string _boundedLazyRows = """
        {"generator": "partial", "pattern": "^([A-Z]??)__$", "flags": 0, "namedLists": {}, "subject": "__aA ", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial-sliced", "pattern": "(?r)A(.??)", "flags": 0, "namedLists": {}, "subject": "_\ufb03", "operation": "search", "partial": true, "pos": 0, "endpos": 2, "codepointSlice": [0, 2], "oracle": "prefilter-free", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "^\\K([^a-f]??)(?P<g2>[\\ ])(?:(?(2)(?<!(?&g2))\\S|.))*$", "flags": 266, "namedLists": {}, "subject": " \r", "operation": "search", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// The seven rows of <c>overlapped-skip-extra-match-reversed</c>, as
    /// <c>tools/record-oracle.py --rows</c> wrote them. All seven are listed rather than one,
    /// because the entry has four tells, the third row is the one whose ground truth was
    /// suspected of depending on call order - a claim the staleness alarm should keep re-testing -
    /// and the fourth is the one that pays for the negative-lookaround clause in
    /// <see cref="CarriesACaptureOutsideItself"/>.
    /// </summary>
    /// <remarks>
    /// Row 1 is row 1567 of the seed-4242 wave, minimised from an eight-codepoint astral subject to
    /// 'bxA'; rows 2 and 3 are rows 1863 (seed 4242) and 1439 (seed 7) as the wave drew them, because
    /// a shorter pattern loses the second <c>(*SKIP)</c> that makes the carry-over observable. Row 4
    /// is row 5543 of a 6000-row seed-7 <c>interactions</c> wave, added by S37 and also as the wave
    /// drew it: every cut tried removed the extra match rather than the noise, which is the same
    /// reason rows 2 and 3 are here whole. Row 5 is row 116766 of the 6000-row seed-20260913 gate,
    /// added by S40a, and it is the row that widened the entry past <c>finditer-overlapped</c>.
    /// <para>
    /// <b>Rows 6 and 7 are S40d's, and each is the ONLY row of its tell</b> - row 117071 of the
    /// seed-4242 gate for the walk, row 116388 of the seed-20260913 one for the substitution. Both
    /// are as the wave drew them and both had to be RE-RECORDED to be listed at all, because each
    /// carries a field the recorder did not write when the wave first drew it:
    /// <c>anchoredScan</c> on row 6, which S40d stopped refusing for a reversed pattern that reads
    /// nothing at the end of the subject, and <c>subMatches</c> on row 7, which S40d added. A row
    /// listed here without its field would be reported rather than classified, and the staleness
    /// alarm would go red on the entry's own example.
    /// </para>
    /// </remarks>
    private const string _reversedExtraMatchRows = """
        {"generator": "verbs", "pattern": "(?r)(?:.{2}(*SKIP)A|x)$", "flags": 8, "namedLists": {}, "subject": "bxA", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 3]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}}
        {"generator": "verbs", "pattern": "(?r)([^a]{2,4}(*SKIP)[a\\d])((?:[^\\d]++(*SKIP)\\s|\\ ))", "flags": 0, "namedLists": {}, "subject": "b0 0\n A", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 6]}]}
        {"generator": "verbs", "pattern": "(?r)(?:[a\\d]*(*SKIP)\\D|\\p{Nd})(?:[\\p{L}\\p{N}]{1,3}(*SKIP)\\S|.)((?>\\s+(*PRUNE)A))", "flags": 0, "namedLists": {}, "subject": "İİAAA AS", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 4]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 7]}]}
        {"generator": "interactions", "pattern": "(?r)(?:\\D{1,1}(*SKIP)[\\p{ASCII}&&\\p{L}]|[[a-f]~~[d-k]])(?P<g1>.*)??(?P<g2>[A])(?:(?(2)(?<!(?&g2))\\p{Nd}))\\b", "flags": 264, "namedLists": {}, "subject": "A\r\nAAA", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 3, "captures": [[3, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [3, 6]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [3, 5]}]}}
        {"generator": "verbs", "pattern": "(?r)[[:digit:]]*(*SKIP)a$", "flags": 16394, "namedLists": {}, "subject": "\r\nAAa\r\naaa", "operation": "finditer", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 9, "length": 1, "captures": [[9, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [9, 10]}, {"groups": [{"number": 0, "success": true, "index": 8, "length": 1, "captures": [[8, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [8, 9]}, {"groups": [{"number": 0, "success": true, "index": 7, "length": 1, "captures": [[7, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [7, 8]}]}}
        {"generator": "verbs", "pattern": "(?r)\\w{1,3}?(*SKIP).(?:\\p{L}(*SKIP)){2,3}", "flags": 8, "namedLists": {}, "subject": "_ ___𐐀𐐀𐐀", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 8, "captures": [[3, 8]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 8]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 6, "captures": [[3, 6]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 7]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 4, "captures": [[3, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 6]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 8, "captures": [[3, 8]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 8]}]}
        {"generator": "verbs", "pattern": "(?r)(?:\\d*?(*SKIP)𝔘|a)$", "flags": 10, "namedLists": {}, "subject": "aa𝔘𝔘", "operation": "sub", "template": "<\\t", "count": 0, "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "sub", "text": "a<\t<\t<\t", "count": 3}, "subMatches": [{"groups": [{"number": 0, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 4]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 2, "captures": [[2, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [2, 3]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}
        """;

    /// <summary>
    /// <c>search-start-partial</c>'s FIRST arm - upstream reports a partial where this port reports
    /// nothing at all - minimised by hand to a reversed boundary at the end of an empty subject.
    /// </summary>
    private const string _searchStartEmptyReverseRow =
        """{"generator": "partial", "pattern": "(?r)\\b$", "flags": 0, "namedLists": {}, "subject": "", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}""";

    /// <summary>
    /// The seven rows of <c>search-start-partial</c>'s second symptom - upstream's prefilter reports
    /// a partial covering the whole searched region, and this port reports its OWN partial somewhere
    /// else - as <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-12 and 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Row 1 is the family minimised by hand; rows 2, 3 and 4 are rows 3497 (seed 4242), 2689 and
    /// 4313 (seed 20260912) of a 6000-row <c>interactions</c> wave. <b>Two of the three come from
    /// one seed and seed 7 draws none</b>, so the family is evidenced at two seeds of the three, not
    /// at three - the S37 blind review's second pass corrected "one per seed" here.
    /// <para>
    /// <b>Row 5 is S43's, row 77889 of the seed-7 gate, and it is the row that finally gives the
    /// family a seed-7 draw</b> - so the arm is now evidenced at all three default seeds. It is the
    /// first of them to carry a FUZZY section, which is what S43's widening of <c>interactions</c>
    /// put into the generator, and its judgement is the arm's cleanest: upstream's
    /// <c>search(partial=True)</c> is (0, 2), the whole searched region, and its own
    /// <c>match</c> answers None at every start. The pattern is reversed, so sweeping
    /// <c>endpos</c> instead - which is where a reversed match anchors - upstream's own matcher
    /// gives (0, 1) partial, this port's answer; and deleting the <c>(*SKIP)</c> or making it
    /// <c>(*PRUNE)</c>, which moves no bound, gives upstream's own search (0, 1) too. Measured
    /// 2026-09-13 on regex 2026.7.19.
    /// </para>
    /// <para>
    /// <b>Rows 6 and 7 are S52 sitting 7's, from the LONG-subject generators, and they are the only
    /// two on which every control returns this port's answer EXACTLY - span and partial flag
    /// both.</b> Upstream's <c>search(partial=True)</c> covers the whole searched region on each,
    /// its own <c>match</c> over that span is None, the anchor sweep on the bound that actually
    /// moves (endpos on the reversed row 6, pos on the forward row 7) gives this port's answer, and
    /// so do the verb-free and <c>(*PRUNE)</c> spellings.
    /// </para>
    /// <para>
    /// The other rows each lose one control, which is why the arm lists judged rows rather than
    /// predicating on a rule. On rows 1, 2 and 4 the VERB-FREE spelling answers a COMPLETE match -
    /// (2, 3), (0, 2) and (2, 3) in codepoints - rather than this port's partial, so it agrees about
    /// the verb without agreeing about the answer. On row 3 it is the SWEEP that never lands: it
    /// gives (0, 1) partial and (0, 2) and (0, 3) complete, and never this port's zero-width partial
    /// at (0, 0), so that row rests on the verb evidence alone. Row 5's sweep DOES give its judged
    /// answer at endpos 1, as the S43 paragraph above says. Re-measured 2026-09-15 on regex
    /// 2026.9.10; an earlier draft of this paragraph said rows 3 and 5 both rested on the verb
    /// evidence and the blind review killed it with the run.
    /// </para>
    /// <para>
    /// <b>The long generators found them but their length is not what makes them.</b> They were
    /// drawn on subjects of 3,363 and 18,759 characters and both delta-debug to THREE codepoints
    /// with the whole signature intact, every character astral or a line break. So what the
    /// <c>partial-long</c> wrapper contributed is its astral alphabet over these pattern shapes,
    /// not its length, and the short <c>partial</c> generator could in principle have drawn either.
    /// Measured 2026-09-15 on regex 2026.9.10,
    /// <c>tools/probes/upstream-search-start-whole-region-partial.py</c>.
    /// </para>
    /// <para>
    /// <b>Rows 8 to 13 are S52 sitting 8's, and they are the first this arm has been fed by the
    /// 6000-ROW GATE</b> - seed 7 rows 98092, 99757 and 100168, seed 4242 row 105103, and seed
    /// 20260915 rows 99718 and 100141 of <c>pwsh -File tools/run-oracle.ps1 -Count 6000</c>. The
    /// same three seeds at 300 rows a generator are green, so it is the row count that reached
    /// them. On five of the six all four controls converge on this port's answer as they do on rows
    /// 6 and 7; row 12 loses the verb-free one, which answers a COMPLETE match at (0, 2) instead.
    /// <b>They are also the first rows of this arm drawn by the <c>partial</c> and
    /// <c>partial-sliced</c> generators rather than by <c>interactions</c></b>, and row 11's slice
    /// is what makes "the whole searched region" (0, 4) rather than the whole subject - so the arm
    /// now covers a question carrying a pos/endpos. Measured 2026-09-15 on regex 2026.9.10,
    /// <c>tools/probes/gate-divergence-doors.py</c>.
    /// </para>
    /// </remarks>
    private const string _searchStartElsewhereRows = """
        {"generator": "interactions", "pattern": "(?:\\w{2,}(*SKIP)\\w|\\w)\\B", "flags": 0, "namedLists": {}, "subject": "a.Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\b(?:.+(*SKIP)[^\\d]|\\p{Lu})([^[\\p{L}--[a-z]]])+(?(?=\\W)[\\w--[0-9]])", "flags": 16650, "namedLists": {}, "subject": "ﬃ\nﬃaa", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "(?r)[a](\\D)*(?:[a-f](*SKIP)[^a-f]|[[a-f]~~[d-k]])\\b", "flags": 16642, "namedLists": {}, "subject": "AA𝔘𐐀", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\m(?:[\\p{L}\\p{N}]{2,}(*SKIP)\\p{ASCII}|\\w)\\B", "flags": 264, "namedLists": {}, "subject": "a😀Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "(?r)^(?(?<![^\\p{L}])\\p{L}|[[:alpha:]])(?:\\.(\\S)){e<=2,i<=1}(?:[\\w--[0-9]](*SKIP)[^\\d]|[abz])", "flags": 256, "namedLists": {}, "subject": "\r.", "operation": "search", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "partial-long", "pattern": "(?r)(?:[a-f](*PRUNE)\\d|[[:digit:]])(?(?<![[:digit:]])[abz])(?:\\p{Nd}(*SKIP)\\s|\\p{L})", "flags": 0, "namedLists": {}, "subject": "𝔘😀\n", "operation": "search", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "partial-long", "pattern": "(?:[\\p{L}\\p{N}](*SKIP)\\p{Nd}|\\p{Ll})(\\S)*?(?P<g2>\\S?)(?:(?(2)(?=(?P>g2))\\p{Nd}|.))", "flags": 8, "namedLists": {}, "subject": "𐐀🏻𐐀", "operation": "search", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "partial", "pattern": "(?r)^\\B(?:[\\p{L}\\p{N}]+?(*SKIP)\\w|[a\\d])$", "flags": 8, "namedLists": {}, "subject": "a𝔘A", "operation": "search", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "\\b(?:\\s*(*SKIP)[A-Z]|\\p{Lu})([abz])", "flags": 264, "namedLists": {}, "subject": "𐐀𝟮a𐐀‍", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 8, "captures": [[0, 8]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 8, "length": 0, "captures": [[8, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "\\b(\\p{Ll}+)*(?:\\D(*SKIP)\\s|[A-Z])(?P<g2>\\p{L}*?)(?:(?(2)(?!(?P>g2))\\s)){2,2}", "flags": 0, "namedLists": {}, "subject": "AA", "operation": "search", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 0, "captures": [[2, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial-sliced", "pattern": "\\b(?:[^\\p{L}](*SKIP)[^\\p{L}]|\\D)𐐀(\\p{Nd}+)", "flags": 8, "namedLists": {}, "subject": "𐐀\r\n𐐀AAA", "operation": "search", "partial": true, "pos": 0, "endpos": 6, "codepointSlice": [0, 4], "oracle": "prefilter-free", "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 4, "captures": [[2, 4]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)([^\\d])*(?:[A-Z](*SKIP)\\S|[[:alpha:]])$", "flags": 264, "namedLists": {}, "subject": "ßß", "operation": "search", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)\\b(?P<g1>[\\ İ]{0,0})\\1(?:[a-f](*SKIP)[^\\d]|[\\w--[0-9]])", "flags": 256, "namedLists": {}, "subject": "İ  ", "operation": "search", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_searchStartElsewhereRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Rows 1, 2 and 4 are the answer upstream's own <c>match(pos, partial=True)</c> gives at the
    /// LEFTMOST position that matches at all. <b>Row 3 is not, and that is why this arm lists
    /// rows.</b> It is reversed, so <c>match</c> anchors at the end; sweeping <c>endpos</c> instead,
    /// upstream answers (0, 1) partial and (0, 2) and (0, 3) complete, and never this port's
    /// zero-width partial at (0, 0). What judges it is the verb: delete the <c>(*SKIP)</c>, or make
    /// it <c>(*PRUNE)</c>, and upstream's own search answers (0, 0) partial - this port's answer.
    /// <para>
    /// <b>Rows 1 and 4 were re-judged by S40b</b>, which restored the slice bounds before the partial
    /// retry in <c>Matcher.DoMatch</c>. They read (4, 0) and (5, 0) before: the positions the slice a
    /// <c>(*SKIP)</c> had moved left reachable, each of them PAST an earlier position at which this
    /// port's own matcher answered a partial. They now read (2, 2) and (3, 2), and both are
    /// upstream's own anchored answer - codepoint (2, 4) on each row, which is UTF-16 (3, 5) on row
    /// 4's astral subject. Measured 2026-09-13,
    /// <c>tools/probes/upstream-partial-retry-slice-restore.py</c> and the <c>--rows</c> replay.
    /// Rows 2 and 3 did not move, which is what says the restore reached only the carried slice.
    /// </para>
    /// </remarks>
    private static readonly string[] _searchStartElsewhereOurs =
    [
        "match 0:(2,2)[(2,2)] last=-1/- partial",
        "match 0:(2,3)[(2,3)] 1:unset last=-1/- partial",
        "match 0:(0,0)[(0,0)] 1:unset last=-1/- partial",
        "match 0:(3,2)[(3,2)] last=-1/- partial",
        // Row 5, S43's. Upstream's own match at endpos 1, which is the first anchor a reversed
        // search tries, and the answer its own search gives once the verb is gone.
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
        // Rows 6 and 7, S52 sitting 7's, and the two cleanest in the arm: on BOTH of them all four
        // controls converge on this port's answer, where rows 3 and 5 had only the verb evidence.
        // Row 6 is reversed, so its anchor sweep varies endpos and lands on (0, 1) codepoints - the
        // astral U+1D518, UTF-16 (0, 2). Row 7 is forward, swept on pos, and lands on codepoint
        // (2, 3) - the trailing U+10400, UTF-16 (4, 2).
        "match 0:(0,2)[(0,2)] last=-1/- partial",
        "match 0:(4,2)[(4,2)] 1:unset 2:unset last=-1/- partial",
        // Rows 8 to 13, S52 sitting 8's, and the first time this arm has been fed by the 6000-ROW
        // gate rather than by a 2000-row wave: seed 7 rows 98092, 99757 and 100168, seed 4242 row
        // 105103, and seed 20260915 rows 99718 and 100141. Every one has `searchOnlyPartial` true -
        // upstream's own anchored `match` over the span its `search` reported answers None - and on
        // every one the `(*PRUNE)` spelling answers exactly this port's answer, which is what
        // `pruneOutcome` on each row above carries. Measured by
        // tools/probes/gate-divergence-doors.py, 2026-09-15 on regex 2026.9.10.
        "match 0:(0,0)[(0,0)] last=-1/- partial",
        "match 0:(8,0)[(8,0)] 1:unset last=-1/- partial",
        "match 0:(2,0)[(2,0)] 1:unset 2:unset last=-1/- partial",
        // Row 11 is `partial-sliced`, so it is the first row in this arm whose question carries a
        // pos/endpos at all, and the slice is what makes "the whole searched region" (0, 4) rather
        // than the whole subject.
        "match 0:(2,4)[(2,4)] 1:unset last=-1/- partial",
        // Row 12 is the one of the six that loses a control, the way rows 1, 2 and 4 do: deleting
        // the `(*SKIP)` gives a COMPLETE match at (0, 2) rather than this port's zero-width partial,
        // so there it is `(*PRUNE)` and the anchor sweep that name this port's answer. On the other
        // five all four controls converge, as they do on rows 6 and 7.
        "match 0:(0,0)[(0,0)] 1:unset last=-1/- partial",
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
    ];

    /// <summary>
    /// Every row of <see cref="_searchStartElsewhereRows"/> by its question, mapped to this port's
    /// judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _searchStartElsewhere = OracleWave
        .ParseRows(_searchStartElsewhereRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _searchStartElsewhereOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The eight rows of <c>turkic-default-folding-without-spans</c>, recorded by
    /// <c>python tools/record-oracle.py --rows tools/probes/turkic-without-spans-rows.jsonl</c> on
    /// 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Rows 1 to 3 are the minimal form of each span-less shape - a <c>sub</c>, a <c>split</c> and a
    /// <c>subf</c> whose template blows up only because upstream found a match. Rows 4 and 5 are the
    /// two real wave rows that made the shape matter: seed 7 row 29165 (<c>conditionals</c>) and seed
    /// 4242 row 24416 (<c>interactions</c>) of the three-seed 2000-row wave of commit 58977bb, which
    /// was red at all three seeds before this entry existed. <b>Row 6 is seed 4242 row 24256 of the
    /// next wave, commit 407c0cb</b>, added by S52's second sitting: a <c>split</c> whose
    /// <c>[A-Z]{1}?</c> reaches a dotless small i, so the parts upstream hands back are four where
    /// this port hands back one, and the two engines answer identically the moment the U+0131 is
    /// swapped for a letter whose DEFAULT fold reaches <c>A-Z</c>. It is row 29165's
    /// range-spanning-<c>I</c> control in a second operation, which is why it needs no new argument.
    /// <para>
    /// <b>Rows 7 and 8 are seed 7 row 118133 (<c>verbs</c>, <c>subf</c>) and seed 20260915 row 75528
    /// (<c>interactions</c>, <c>split</c>) of the 6000-row three-seed gate</b>, added by S52's ninth
    /// sitting. Row 7 is row 29165's control again in a third operation and with the range written
    /// <c>[A-Z]{1,3}</c>: <c>[A-Z]</c> and <c>[A-Y]</c> both reach the dotless small i and
    /// <c>[A-H]</c> and <c>[J-Z]</c> find nothing at all, which is this port's answer. Row 8 is a new
    /// symptom rather than a new argument - upstream EATS the U+0130, so its first match is
    /// <c>(0, 1)</c> where every swap for a letter with no <c>T</c> row gives a ZERO-WIDTH first
    /// match and hands the letter back as a part. <b>Fold LENGTH is measured not to be the variable
    /// there</b>: U+00DF, U+FB00 and U+01F0 all fold to two characters and <c>h</c> to one, and all
    /// four agree with this port.
    /// </para>
    /// <para>
    /// Every one carries the recorder's <c>scanMatches</c>, which is upstream's own <c>finditer</c>
    /// over the row and the only thing on any of these rows that has a span at all; the entry's
    /// <see cref="ExpectedDivergence.Applies"/> reads it, so the staleness alarm re-tests the
    /// evidence and not only the divergence.
    /// </para>
    /// </remarks>
    private const string _turkicWithoutSpansRows = """
        {"generator": "rows", "pattern": "(?i)I", "flags": 0, "namedLists": {}, "subject": "\u0131", "operation": "sub", "template": "X", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "X", "count": 1}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}]}
        {"generator": "rows", "pattern": "(?i)(I)", "flags": 0, "namedLists": {}, "subject": "a\u0131b", "operation": "split", "count": 0, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["a", "\u0131", "b"]}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}, {"number": 1, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}
        {"generator": "rows", "pattern": "(?i)I", "flags": 0, "namedLists": {}, "subject": "\u0131", "operation": "subf", "template": "{1}", "count": 0, "codepointSpan": null, "outcome": {"kind": "error", "exception": "IndexError", "message": "Replacement index 1 out of range for positional args tuple", "whileMatching": true}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}]}
        {"generator": "conditionals", "pattern": "(?r)(?(?!s)[A-Z]{2}|(S))$", "flags": 16394, "namedLists": {}, "subject": "s\rS\u0131", "operation": "subf", "template": "{0[2]}", "count": 3, "codepointSpan": null, "outcome": {"kind": "error", "exception": "IndexError", "message": "list index out of range", "whileMatching": true}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 2, "length": 2, "captures": [[2, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [2, 4]}]}
        {"generator": "interactions", "pattern": "^(?:\ufb01\u0130){e<=2:\\S}([^a-f]+)$", "flags": 16386, "namedLists": {}, "subject": "\u0130\u0130\ufb01 \ufb01\ufb00", "operation": "split", "count": 3, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["", " \ufb01\ufb00", ""]}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 3, "length": 3, "captures": [[3, 3]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [2], "deletions": []}, "codepointSpan": [0, 6]}]}
        {"generator": "interactions", "pattern": "\\b(?P<g1>[^a-f])*?(?P<g2>[A-Z]{1}?)", "flags": 16386, "namedLists": {}, "subject": "\u00df\u00df\u0131\u0131", "operation": "split", "count": 0, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["", "\u00df", "\u0131", "\u0131"]}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 1, "length": 1, "captures": [[0, 1], [1, 1]]}, {"number": 2, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [0, 3]}]}
        {"generator": "verbs", "pattern": "[A-Z]{1,3}(?<![a\\d](*SKIP))[\\w\\s]\\d*+(?=(*PRUNE))[^a][^a]*(?!(*SKIP)\u0131)\\S", "flags": 10, "namedLists": {}, "subject": "A\u0130\ufb00\u0131\r\nS", "operation": "subf", "template": "{0}{0[-2]}", "count": 0, "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "error", "exception": "IndexError", "message": "list index out of range", "whileMatching": true}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 4, "captures": [[3, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 7]}]}
        {"generator": "interactions", "pattern": "(?p)(?P<g1>\\d)??\\L<w1>{e<=2:\\s}", "flags": 266, "namedLists": {"w1": ["a", "\ufb01"]}, "subject": "\u0130\ufb01\r\n\u0131", "operation": "split", "count": 0, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["", null, "", null, "", null, "\u0131", null, ""]}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "codepointSpan": [0, 1]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 3, "captures": [[1, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 2, 0], "codepointSpan": [1, 4]}, {"groups": [{"number": 0, "success": true, "index": 4, "length": 0, "captures": [[4, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "codepointSpan": [4, 4]}, {"groups": [{"number": 0, "success": true, "index": 5, "length": 0, "captures": [[5, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "codepointSpan": [5, 5]}], "posixFreeOutcome": {"kind": "split", "parts": ["", null, "", null, "", null, "", null, "", null, "", null, "", null, "\u0131", null, ""]}}
        """;

    /// <summary>
    /// The rows of <c>turkic-default-folding-without-spans</c>, so a test can assert that each is
    /// classified by THAT entry rather than by its predicate-keyed sibling.
    /// </summary>
    internal static string SpanlessTurkicRows => _turkicWithoutSpansRows;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_turkicWithoutSpansRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// <b>Keying on the question ALONE was a defect, found by S52's second blind pass and reproduced
    /// before it was fixed.</b> Without this array the entry read nothing from the port's answer at
    /// all, so on these five questions ANY answer - a fabricated total failure included - was tallied
    /// EXPECTED, which is the widest possible pin on exactly the rows a pin is meant to be narrow on.
    /// Every other row-keyed entry in this file already demanded the judged answer;
    /// <c>_partialRetryReversedOurs</c> is the pattern this now follows.
    /// </remarks>
    /// <remarks>
    /// <b>Written with DOUBLED BACKSLASHES rather than as a verbatim string, and that is not a
    /// style choice.</b> <see cref="OracleWave.Printable"/> renders a non-ASCII character as the
    /// six-character text <c>ı</c>, so these strings must hold a literal backslash; a verbatim
    /// <c>@"...ı..."</c> written into this file was silently normalised into the CHARACTER
    /// U+0131 by an editing tool, and the entry then matched nothing at all while every clause of it
    /// looked right. Found while writing this array.
    /// </remarks>
    private static readonly string[] _turkicWithoutSpansOurs =
    [
        "sub 0 '\\u0131'",
        "split 1 'a\\u0131b'",
        "sub 0 '\\u0131'",
        "sub 0 's\\u000dS\\u0131'",
        "split 3 '' '\\ufb01 \\ufb01\\ufb00' ''",
        "split 1 '\\u00df\\u00df\\u0131\\u0131'",
        "sub 0 'A\\u0130\\ufb00\\u0131\\u000d\\u000aS'",
        "split 9 '' <null> '\\u0130' <null> '' <null> '\\u0131' <null> ''",
    ];

    /// <summary>
    /// <see cref="_turkicWithoutSpansRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _turkicWithoutSpans = OracleWave
        .ParseRows(_turkicWithoutSpansRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _turkicWithoutSpansOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The one row of <c>turkic-default-folding-read-by-a-lookaround</c>, seed 20260915 row 88716
    /// (<c>conditionals</c>, <c>sub</c>) of the 6000-row three-seed gate, as
    /// <c>tools/record-oracle.py --rows</c> wrote it on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// The subject is four dotless small i followed by two <c>a</c>, and BOTH of upstream's matches
    /// land on the <c>a</c> at the end - <c>(5, 6)</c> and <c>(4, 5)</c>. <b>No span on either side
    /// covers a Turkic letter</b>, so the sibling entry's <c>scanMatches</c> condition refuses this
    /// row and the span test the first Turkic entry uses cannot reach it either. What reads the
    /// letter is the set union inside the NEGATIVE LOOKBEHIND <c>(?&lt;!(?:a|\p{ASCII})+)</c>,
    /// which is not part of any match by construction - see the entry's <c>Reason</c> for the
    /// one-line isolation and for the construct a first draft named wrongly.
    /// </remarks>
    private const string _turkicLookaroundRows = """
        {"generator": "conditionals", "pattern": "(?r)(?(?<!(?:a|\\p{ASCII})+)\\d{3}|)(?:(?(?<=ı[\\w\\s])a\\w|(?P<g1>ı))ı|a)", "flags": 16386, "namedLists": {}, "subject": "ııııaa", "operation": "sub", "template": "\\1\\1", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "ıııı", "count": 2}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [5, 6]}, {"groups": [{"number": 0, "success": true, "index": 4, "length": 1, "captures": [[4, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [4, 5]}]}
        """;

    /// <summary>
    /// This port's judged answer to <see cref="_turkicLookaroundRows"/>, as the report renders it.
    /// </summary>
    /// <remarks>
    /// One replacement where upstream makes two, and the surviving <c>a</c> is the whole of the
    /// difference in the text. Written with doubled backslashes for the reason
    /// <see cref="_turkicWithoutSpansOurs"/> records.
    /// </remarks>
    private static readonly string[] _turkicLookaroundOurs = ["sub 1 '\\u0131\\u0131\\u0131\\u0131a'"];

    /// <summary>
    /// <see cref="_turkicLookaroundRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _turkicLookaround = OracleWave
        .ParseRows(_turkicLookaroundRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _turkicLookaroundOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The two rows of <c>turkic-default-folding-from-the-pattern-side</c>, rows 25482
    /// (<c>interactions</c>) and 34508 (<c>partial-sliced</c>) of the seed-20260915 2000-row wave of
    /// commit <c>407c0cb</c>, as <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Neither subject holds a Turkic letter the divergence's spans cover - 25482's holds none at
    /// all - so the span test both entries above use cannot reach them. On 25482 the U+0130 is the
    /// first letter of a <c>\L&lt;w1&gt;</c> word; on 34508 it is the pattern's own leading literal.
    /// <para>
    /// Row 34508's recorded <c>searchOnlyPartial</c> is <see langword="false"/>, which is the field
    /// that keeps <c>search-start-partial</c> off it: upstream's own <c>match</c> reports the same
    /// zero-width partial its <c>search</c> did, so this is upstream's SLOW path and not the
    /// prefilter, and the row belongs here rather than there.
    /// </para>
    /// </remarks>
    private const string _turkicPatternSideRows = """
        {"generator": "interactions", "pattern": "(?(?=\\D)[\\p{L}||\\p{N}])\\L<w1>{e<=2}\\K", "flags": 16642, "namedLists": {"w1": ["İı", "ﬀ"]}, "subject": "ﬀ\r ﬀ", "operation": "match", "partial": true, "codepointSpan": [3, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 3, "length": 0, "captures": [[3, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [1, 2], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 2], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [3, 4]}}]}
        {"generator": "partial-sliced", "pattern": "^İ\\K\\b", "flags": 16650, "namedLists": {}, "subject": "sﬁﬀıİ", "operation": "search", "partial": true, "pos": 5, "endpos": 5, "codepointSlice": [5, 5], "oracle": "prefilter-free", "codepointSpan": [5, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 5, "length": 0, "captures": [[5, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_turkicPatternSideRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    private static readonly string[] _turkicPatternSideOurs =
    [
        "match 0:(2,0)[(2,0)] last=-1/- fuzzy=(1,0,0)[s:1][i:][d:]",
        "no match",
    ];

    /// <summary>
    /// <see cref="_turkicPatternSideRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _turkicPatternSide = OracleWave
        .ParseRows(_turkicPatternSideRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _turkicPatternSideOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The five rows of <c>partial-retry-reversed-slice</c>, each copied out of
    /// <c>TestResults/oracle/wave-&lt;seed&gt;.jsonl</c> rather than retyped - rows 1 to 4 on
    /// 2026-09-13, row 5 on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Rows 101560 (seed 7), 96397 and 99556 (seed 20260913) of the gate, all on the
    /// <c>partial</c> generator. <b>A ROW INDEX MEANS NOTHING WITHOUT THE COMMAND THAT DREW IT</b>,
    /// because it counts across every generator in the run: these are
    /// <c>pwsh -File tools/run-oracle.ps1 -Count 6000</c>, the full default generator list, which is
    /// 126,000 rows a seed. The S40b blind review looked for them in a 12,000-row
    /// <c>-Generator partial,verbs</c> wave, where the same three rows sit at 5560, 397 and 3556, and
    /// reported the provenance as wrong; it is the wave that differs, not the rows. Nothing depends
    /// on the index - the entry is keyed on the row's own question - so this is a pointer for a
    /// reader, and it needs the command to be one.
    /// <para>
    /// <b>Two of the three come from one seed and seed 4242 draws none</b>, so the family is
    /// evidenced at two seeds of the three - stated the way the S37 blind review's second pass
    /// required of <see cref="_searchStartElsewhereRows"/>. All three are the same shape: reversed,
    /// <c>partial</c>, a <c>(*SKIP)</c>, and upstream answering the zero-width partial at (0, 0) that
    /// its own matcher beats at a higher <c>endpos</c>.
    /// </para>
    /// <para>
    /// <b>Row 4 is S43's, row 7329 of a 6000-row seed-99991 <c>fuzzy,interactions</c> wave, and it is
    /// the first of this family to carry a FUZZY section - but it is ALSO the first on which the
    /// entry's "upstream's own matcher answers what this port does" line is FALSE, so it is said
    /// here rather than left to be assumed.</b> Upstream's search answers the zero-width (0, 0)
    /// partial as the other three do, and its own anchored match gives (0, 1) at <c>endpos</c> 1 and
    /// (0, 2) at 2 - but NOTHING at 3, where this port answers (0, 3). The moved bound reaches
    /// upstream's anchored door too on this row, so the anchored sweep cannot judge it.
    /// <b>What judges it is the <c>(*PRUNE)</c> control</b>, which is this entry's other argument and
    /// the one that does not depend on the anchored sweep: spelling the verb <c>(*PRUNE)</c> - the
    /// same backtracking pruning, no bound moved - gives upstream (0, 3) partial with one
    /// substitution at 3, which is this port's answer to the code unit. Deleting the verb gives
    /// upstream (0, 3) COMPLETE with the same substitution. Measured 2026-09-13 on regex 2026.7.19.
    /// </para>
    /// <para>
    /// <b>Row 5 is S52's, row 33858 of the seed-20260915 2000-row default wave, and its SYMPTOM IS
    /// THE OTHER WAY UP - which is said here rather than left for a reader to trip over.</b> Rows 1
    /// to 4 are upstream answering a SHORTER partial than this port; here upstream answers a LONGER
    /// one, the whole subject:
    /// <code>
    /// (?r)(?:[A-Z](*SKIP).|\d)([^\p{L}])\B   over '00a .', partial search
    ///   as drawn, (*SKIP)     upstream (0, 5) partial, group 1 (1, 2)
    ///   verb -&gt; (*PRUNE)      upstream (0, 2) partial, group 1 (1, 2)   &lt;- this port's answer
    ///   verb deleted          upstream (0, 2) COMPLETE, group 1 (1, 2)
    /// </code>
    /// A moved <c>slice_end</c> can lengthen an answer as readily as shorten one - the second pass
    /// searches a region the first pass's verb redrew, and which way the answer moves depends on
    /// where the anchors fall in it - so the mechanism is unchanged and only the direction of the
    /// consequence differs. <b>Like row 4, the anchored sweep does not judge this row</b>: upstream's
    /// own <c>match(0, endpos, partial=True)</c> is None at every endpos from 5 down to 1 and (0, 0)
    /// at 0, so it names neither answer. The <c>(*PRUNE)</c> control is the whole of the evidence,
    /// and it is decisive: <c>(*PRUNE)</c> prunes identically and moves no bound, and it gives this
    /// port's span, its group and its partial flag exactly. Measured 2026-09-15 on regex 2026.9.10,
    /// <c>tools/probes/upstream-partial-retry-reversed-longer.py</c>.
    /// </para>
    /// </remarks>
    private const string _partialRetryReversedRows = """
        {"generator": "partial", "pattern": "(?r)\\b(?:[^a-f](*SKIP)[\\p{L}\\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})", "flags": 8, "namedLists": {}, "subject": "a\n", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(?r)\\M(?:[^[\\p{L}--[a-z]]](*SKIP)[a-f]|[A-Z])", "flags": 16642, "namedLists": {}, "subject": "A𐐨", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(?r)(?:[^a-f](*SKIP)\\S|[\\p{L}\\p{N}])(?(?<![\\w--[0-9]])[\\p{L}\\p{N}]|[\\w--[0-9]])", "flags": 256, "namedLists": {}, "subject": "𐐀_  ", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "interactions", "pattern": "(?r)\\b(?:\\D(*SKIP)[\\p{L}\\p{N}]|[^a])(?:\\p{Ll}{0,2}?İ(?:\\p{Nd}){2i+1d+1s<=2}){1i+2d+1s<=3}", "flags": 0, "namedLists": {}, "subject": "ıİﬀ", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(?r)(?:[A-Z](*SKIP).|\\d)([^\\p{L}])\\B", "flags": 0, "namedLists": {}, "subject": "00a .", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "partial", "pattern": "(?r)^(?P<g1>[\\p{ASCII}&&\\p{L}])(?:(?(1)(?<=(?P>g1))\\p{Ll}))*?\\m(?:[[a-z]--[aei]](*SKIP)\\p{Ll}|[^\\p{L}])", "flags": 266, "namedLists": {}, "subject": "𐐀𐐀__\na\r", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)^(?P<g1>\\p{Nd}{0,}?){3,4}a(?:\\s(*SKIP)[A-Z]|[[:digit:]])", "flags": 2, "namedLists": {}, "subject": "aﬃ", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)^[^\\p{L}](?:[[:digit:]](*SKIP)\\w|[\\w\\s])[a-f]*", "flags": 16386, "namedLists": {}, "subject": "𐐀\r\n", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)(\\w)(?:[abz]+(*SKIP)\\p{Lu}|\\p{Ll})", "flags": 264, "namedLists": {}, "subject": "A\r\n", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "(?r)[[:alpha:]](?:[[:alpha:]]{1,1}?(*SKIP)[^a]|\\S)", "flags": 16642, "namedLists": {}, "subject": "_\n0B A.a", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "partial-sliced", "pattern": "(?r)(?P<g1>[\\.B]{1,3})\\1\\.(?:[a]{0,1}(*SKIP)[A-Z]|\\p{Nd})", "flags": 2, "namedLists": {}, "subject": "B. ", "operation": "search", "partial": true, "pos": 0, "endpos": 3, "codepointSlice": [0, 3], "oracle": "prefilter-free", "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_partialRetryReversedRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Every one is upstream's OWN <c>match(0, endpos, partial=True)</c> answer at the highest
    /// <c>endpos</c> that matches, which is the first anchor a reversed search tries and therefore
    /// the answer it owes. In codepoints upstream gives (0, 1), (0, 1) and (0, 2); rows 2 and 3 have
    /// astral subjects, so those are UTF-16 (0, 1) and (0, 3) here. Measured 2026-09-13,
    /// <c>tools/probes/upstream-partial-retry-slice-restore.py</c>.
    /// </remarks>
    private static readonly string[] _partialRetryReversedOurs =
    [
        "match 0:(0,1)[(0,1)] 1:(1,0)[(1,0)] last=1/g1 partial",
        "match 0:(0,1)[(0,1)] last=-1/- partial",
        "match 0:(0,3)[(0,3)] last=-1/- partial",
        // Row 4. NOT upstream's anchored answer - see the remark above - but upstream's own answer
        // to the same pattern with the verb spelled (*PRUNE), substitution and all.
        "match 0:(0,3)[(0,3)] last=-1/- partial fuzzy=(1,0,0)[s:3][i:][d:]",
        // Row 5, S52's, and upstream's (*PRUNE) answer again - group and all.
        "match 0:(0,2)[(0,2)] 1:(1,1)[(1,1)] last=1/- partial",
        // Rows 6 to 11, S52 sitting 8's, from the 6000-row three-seed gate: seed 7 rows 98857,
        // 99490 and 100234, and seed 20260915 rows 98719, 99223 and 105625. Every one is this
        // entry's own printed shape, line for line - upstream's `search` answers the zero-width
        // partial at (0, 0), the LAST anchor a reversed search would try, while its own
        // `match(0, 1, partial=True)` answers this port's (0, 1) and so do the `(*PRUNE)` and
        // verb-free spellings. On row 10 the verb-free spelling instead answers a COMPLETE match at
        // (5, 7), so that row rests on `(*PRUNE)` and the anchor sweep; the other five keep all
        // four. Row 11 is `partial-sliced`, the first row here whose question carries a pos/endpos.
        // ROWS 6 AND 8 are the two with astral subjects - '𐐀𐐀__\na\r' and '𐐀\r\n' - so this port's
        // (0, 1) in codepoints is UTF-16 (0, 2) on those two and (0, 1) on the other four, whose
        // subjects are 'aﬃ', 'A\r\n', '_\n0B A.a' and 'B. '. Measured by
        // tools/probes/gate-divergence-doors.py, 2026-09-15 on regex 2026.9.10.
        "match 0:(0,2)[(0,2)] 1:unset last=-1/- partial",
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
        "match 0:(0,2)[(0,2)] last=-1/- partial",
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
        "match 0:(0,1)[(0,1)] last=-1/- partial",
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
    ];

    /// <summary>
    /// <see cref="_partialRetryReversedRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _partialRetryReversed = OracleWave
        .ParseRows(_partialRetryReversedRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _partialRetryReversedOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The three rows of <c>partial-retry-carried-slice-forward</c> - the entry above's mechanism in
    /// a pattern that runs LEFT TO RIGHT - copied out of the wave files rather than retyped: row 1
    /// from <c>TestResults/oracle/wave-99991.jsonl</c> on 2026-09-13, rows 2 and 3 from a
    /// <c>--rows</c> re-record on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 1 is row 6897 of <c>pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator
    /// fuzzy,interactions -Seeds 99991</c>. It was the only row any wave had drawn until S52's third
    /// sitting, because the forward half of this mechanism needs a <c>(*SKIP)</c> whose moved
    /// <c>slice_start</c> changes which ALTERNATIVE the partial pass can still enter, and that is a
    /// narrower accident than the reversed half, which only needs the bound to hide an anchor.
    /// </para>
    /// <para>
    /// Rows 2 and 3 are seed 7 rows 24018 and 24737 of the three-seed 2000-row <c>interactions</c>
    /// wave of commit 407c0cb, and they widen the SYMPTOM rather than the predicate: on both,
    /// upstream's partial call answers a match that is not partial at all, and its own non-partial
    /// call to the same compiled pattern over the same subject answers <c>None</c>. Row 2 is a
    /// <c>match</c>, which is one attempt, so <c>(*SKIP)</c> has no next attempt to move the start of
    /// and must prune exactly what <c>(*PRUNE)</c> prunes; row 3 is a <c>search</c>.
    /// </para>
    /// </remarks>
    private const string _partialRetryForwardRows = """
        {"generator": "interactions", "pattern": "\\b(?:(?:\\ _(\\W)){e<=1}(*SKIP)[A-Z]|[^a])(?:.?(?:(\\w+?)){i<=1:.}){e<=2,s<=1:[^a-z]}(?:(?:[abz]([abz])){2i+1d+1s<=2}(*PRUNE)[\\w\\s]|\\W)", "flags": 8, "namedLists": {}, "subject": "😀ß_ ", "operation": "search", "partial": true, "codepointSpan": [1, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 3, "captures": [[2, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 3, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [4], "deletions": []}}, "searchOnlyPartial": false}
        {"generator": "interactions", "pattern": "\\L<w1>{e<=2}(?:\\D(*SKIP)\\S|\\p{Lu})", "flags": 0, "namedLists": {"w1": ["sı", "İ", "ﬁ", "ﬁı"]}, "subject": "ßß", "operation": "match", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 2], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0, 1]}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 2], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0, 1]}}], "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?:(?:a[\\p{L}\\p{N}]?(?:(.+?)){e<=2:\\s}){1i+2d+1s<=3}(*SKIP)\\W|\\w)(\\p{Ll}{3,3}?)+\\K", "flags": 8, "namedLists": {}, "subject": "😀😀aa𐐨𐐨 ", "operation": "search", "partial": true, "codepointSpan": [5, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 8, "length": 0, "captures": [[8, 0]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 2, "success": true, "index": 4, "length": 4, "captures": [[4, 4]]}], "lastIndex": 2, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0]}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 0], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": []}}], "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 5, "length": 6, "captures": [[5, 6]]}, {"number": 1, "success": true, "index": 8, "length": 2, "captures": [[8, 2]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": null, "partial": true}}
        {"generator": "partial", "pattern": "^A([^a-f]*)(?:\\D(*SKIP)\\p{ASCII}|\\s)", "flags": 16394, "namedLists": {}, "subject": "𐐀𐐀\nAA", "operation": "search", "partial": true, "codepointSpan": [5, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 7, "length": 0, "captures": [[7, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}, {"number": 1, "success": true, "index": 6, "length": 0, "captures": [[6, 0]]}], "lastIndex": 1, "lastGroup": null, "partial": true}}
        {"generator": "partial-sliced", "pattern": "(\\D)\\1(?:[\\p{L}\\p{N}]*?(*SKIP)\\s|[a])", "flags": 258, "namedLists": {}, "subject": "𐐀\r\n😀𐐀ßß😀 𐐀", "operation": "search", "partial": true, "pos": 2, "endpos": 10, "codepointSlice": [1, 7], "oracle": "prefilter-free", "codepointSpan": [7, 7], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 10, "length": 0, "captures": [[10, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 8, "length": 2, "captures": [[8, 2]]}, {"number": 1, "success": true, "index": 8, "length": 1, "captures": [[8, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": true}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_partialRetryForwardRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Every one is upstream's own answer to the same row with the <c>(*SKIP)</c> spelled
    /// <c>(*PRUNE)</c>, which prunes the same backtracking and moves no bound. On row 1 deleting the
    /// verb gives the same answer again; the substitution at 1 and the capture at (3, 4) are in
    /// codepoints, which is UTF-16 (4, 1) on that astral subject. Measured 2026-09-13 and
    /// 2026-09-15, the two probes named in the entry.
    /// </remarks>
    private static readonly string[] _partialRetryForwardOurs =
    [
        "match 0:(2,3)[(2,3)] 1:(4,1)[(4,1)] 2:unset 3:unset last=1/- partial fuzzy=(1,0,0)[s:2][i:][d:]",
        // Row 2, seed 7 row 24018, and upstream's own `(*PRUNE)` answer - two substitutions where
        // its `(*SKIP)` answer spends two deletions and drops the partial flag.
        "match 0:(0,2)[(0,2)] last=-1/- partial fuzzy=(2,0,0)[s:0,1][i:][d:]",
        // Row 3, seed 7 row 24737, and upstream's own `(*PRUNE)` answer again, group and all.
        "match 0:(5,6)[(5,6)] 1:(8,2)[(8,2)] 2:unset last=1/- partial",
        // Rows 4 and 5, S52 sitting 8's, from the 6000-row three-seed gate: seed 7 row 99850 and
        // seed 20260915 row 105880. They WIDEN THE SYMPTOM AGAIN, and the entry's own "the two
        // engines agree on the SPAN" no longer covers the whole family: on both of these upstream
        // answers a ZERO-WIDTH partial at the far end of what it searched - codepoints (5, 5) and
        // (7, 7) - where its own `(*PRUNE)` and verb-free spellings answer the wider partial (3, 5)
        // and (5, 7) that this port answers, group and all. So the moved `slice_start` costs a
        // START here rather than an alternative, which is the reversed entry's symptom appearing on
        // a forward pattern. Row 5 is `partial-sliced`, so its searched region is the slice (1, 7)
        // and not the subject. Measured by tools/probes/gate-divergence-doors.py, 2026-09-15 on
        // regex 2026.9.10.
        "match 0:(5,2)[(5,2)] 1:(6,0)[(6,0)] last=1/- partial",
        "match 0:(8,2)[(8,2)] 1:(8,1)[(8,1)] last=1/- partial",
    ];

    /// <summary>
    /// <see cref="_partialRetryForwardRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _partialRetryForward = OracleWave
        .ParseRows(_partialRetryForwardRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _partialRetryForwardOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The three rows of <c>end-of-line-reads-a-skip-moved-slice</c>, as <c>tools/record-oracle.py
    /// --rows</c> wrote them on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seed 20260915 rows 24224 (<c>interactions</c>, a reversed <c>split</c>) and 38101
    /// (<c>verbs</c>, a reversed <c>subf</c>) of the three-seed 2000-row wave of commit 407c0cb. Both
    /// are operations no <c>(*SKIP)</c> entry here had reached before: a <c>split</c> renders as a
    /// list of parts with no span in it at all, and row 38101's recorded outcome is an EXCEPTION,
    /// because upstream's template holds <c>{0[-1]}</c> and a match object has no group -1 - so
    /// upstream raises exactly when it finds a match and answers the subject unchanged when it does
    /// not.
    /// </para>
    /// <para>
    /// The third is seed 7 row 74413 of the 6000-row gate, added by S52 sitting 11 (<c>interactions</c>,
    /// a reversed <c>subf</c>, MULTILINE, taken verbatim out of <c>wave-7.jsonl</c>). It is the same
    /// shape as row 38101 and needed no new argument: upstream replaces once over a span that ENDS AT
    /// codepoint 3, where its own <c>$</c> is true only at 4 and 5, and spelling <c>$</c> out as
    /// <c>(?:(?=\n)|(?!\n|.))</c> - as well as <c>(*PRUNE)</c> and the verb deleted - answers the
    /// <c>sub 0</c> this port answers. The <c>(?w)</c> control cannot run on it: 3 IS a <c>(?w)$</c>
    /// position, so this is a SECOND row where the phantom end is one the twin would create anyway.
    /// </para>
    /// </remarks>
    private const string _endOfLineReadsMovedSliceRows = """
        {"generator": "interactions", "pattern": "(?r)(?:\\s*?(*SKIP)\\W|[^a])(\\S{1,})$", "flags": 8, "namedLists": {}, "subject": "ﬀﬀ\r\nﬀﬀss\rS", "operation": "split", "count": 0, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["", "S", "", "ﬀﬀss", "ﬀﬀ\r"]}, "pruneOutcome": {"kind": "split", "parts": ["", "S", "ﬀﬀ\r\nﬀﬀss"]}}
        {"generator": "verbs", "pattern": "(?r)(\\D+(*PRUNE)[^\\p{L}])(?:[^a-f](*PRUNE)){1,3}?((?>\\p{Lu}{1,3}?(*SKIP)\\D))$", "flags": 10, "namedLists": {}, "subject": "a\r\na𝔘𝔘𐐨\r𐐨𝔘", "operation": "subf", "template": "-{0[0]}{0[-1]}{0[-2]}", "count": 0, "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "error", "exception": "IndexError", "message": "list index out of range", "whileMatching": true}, "pruneOutcome": {"kind": "sub", "text": "a\r\na𝔘𝔘𐐨\r𐐨𝔘", "count": 0}}
        {"generator": "interactions", "pattern": "(?r)^(?P<g1>\\D)(?:[^\\d](*SKIP)\\p{ASCII}|\\W)$", "flags": 8, "namedLists": {}, "subject": "𐐨𐐨a\r\n", "operation": "subf", "template": "-}}{g1}", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "-}𐐨\r\n", "count": 1}, "subMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 3]}], "pruneOutcome": {"kind": "sub", "text": "𐐨𐐨a\r\n", "count": 0}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_endOfLineReadsMovedSliceRows"/>, in the
    /// same order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Every one is upstream's own <c>pruneOutcome</c> - its answer to the same row with every
    /// <c>(*SKIP)</c> spelled <c>(*PRUNE)</c> - and every one is also upstream's answer when the
    /// trailing <c>$</c> is spelled out as what <c>$</c> is defined to be. Measured 2026-09-15 -
    /// rows 1 and 2 by <c>tools/probes/upstream-skip-carried-slice-doors.py</c>, which holds those
    /// two and not the third, and row 3 by
    /// <c>tools/probes/upstream-gate-drawn-skip-rows.py</c>.
    /// </remarks>
    private static readonly string[] _endOfLineReadsMovedSliceOurs =
    [
        "split 3 '' 'S' '\\ufb00\\ufb00\\u000d\\u000a\\ufb00\\ufb00ss'",
        "sub 0 'a\\u000d\\u000aa\\ud835\\udd18\\ud835\\udd18\\ud801\\udc28\\u000d\\ud801\\udc28\\ud835\\udd18'",
        "sub 0 '\\ud801\\udc28\\ud801\\udc28a\\u000d\\u000a'",
    ];

    /// <summary>
    /// <see cref="_endOfLineReadsMovedSliceRows"/> by its question, mapped to this port's judged
    /// answer.
    /// </summary>
    private static readonly Dictionary<string, string> _endOfLineReadsMovedSlice = OracleWave
        .ParseRows(_endOfLineReadsMovedSliceRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _endOfLineReadsMovedSliceOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The two rows of <c>skip-carried-slice-on-a-scan-with-no-walk</c>, as
    /// <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Seed 7 rows 25854 (<c>interactions</c>, a forward <c>finditer</c>) and 38151 (<c>verbs</c>, a
    /// reversed <c>finditer-overlapped</c>) of the three-seed 2000-row wave of commit 407c0cb. Both
    /// are the carried-slice defect the four <c>overlapped-skip-*</c> entries above judge, and
    /// neither can be judged BY one of them, because neither row carries an <c>anchoredScan</c> - see
    /// the entry for the two separate reasons the recorder refuses it.
    /// </remarks>
    private const string _skipCarriedSliceNoWalkRows = """
        {"generator": "interactions", "pattern": "(?b)(?:(?:\\W{2,}[^\\d]*?){1<=e<=2}(*SKIP)\\D|\\w)(\\p{Lu}{2,3}){0,0}", "flags": 2, "namedLists": {}, "subject": "b\r\nabA\n_", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}]}, "bestmatchFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [0, 3], "insertions": [], "deletions": []}, "codepointSpan": [0, 5]}]}, "pruneOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 1, "captures": [[3, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [3, 4]}, {"groups": [{"number": 0, "success": true, "index": 4, "length": 1, "captures": [[4, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [4, 5]}]}}
        {"generator": "verbs", "pattern": "(?r)\\p{ASCII}{1,3}(?![a](*SKIP))s(?:[^\\p{L}]*+(*SKIP)\\W|s)[^a]*(?<=\\W(*PRUNE))[A-Z]", "flags": 16386, "namedLists": {}, "subject": "aas\rs\r\ns", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 8, "captures": [[0, 8]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 8]}]}, "pruneOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 8, "captures": [[0, 8]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 8]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}]}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_skipCarriedSliceNoWalkRows"/>, in the
    /// same order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Both are upstream's own <c>pruneOutcome</c>, and both are upstream's own scan taken one match
    /// at a time from a fresh state. Measured 2026-09-15,
    /// <c>tools/probes/upstream-skip-carried-slice-doors.py</c>.
    /// </remarks>
    private static readonly string[] _skipCarriedSliceNoWalkOurs =
    [
        "matches 3 | match 0:(0,1)[(0,1)] 1:unset last=-1/- || match 0:(3,1)[(3,1)] 1:unset last=-1/-"
            + " || match 0:(4,1)[(4,1)] 1:unset last=-1/-",
        "matches 2 | match 0:(0,8)[(0,8)] last=-1/- || match 0:(0,5)[(0,5)] last=-1/-",
    ];

    /// <summary>
    /// <see cref="_skipCarriedSliceNoWalkRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _skipCarriedSliceNoWalk = OracleWave
        .ParseRows(_skipCarriedSliceNoWalkRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _skipCarriedSliceNoWalkOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The five rows of <c>group-call-loses-the-match</c>, as <c>tools/record-oracle.py --rows</c>
    /// wrote them, and all five because between them they are the OUTCOME SHAPES the defect appears
    /// in - an empty scan, a scan one match short, a substitution that replaced nothing, and a
    /// substitution this port answered with an exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rows 4182 (seed 7), 4407 (seed 20260912), and 5087 and 5773 (seed 4242) of a 6000-row
    /// <c>interactions</c> wave, each as the wave drew it, and row 93133 (seed 20260913) of a 6000-row
    /// <c>recursion</c> one, added by S40a. The last arrived the way this list is meant to work: the
    /// shape was deliberately left out of the predicate as one no row had shown, a later seed drew
    /// one, the run went red rather than quietly classifying it, and it was judged by the same probe
    /// as the others. Minimisation was tried and is recorded in the entry's own
    /// <see cref="ExpectedDivergence.Reason"/> as having failed: every shrink that kept upstream
    /// contradicting ITSELF lost the divergence, because this port reproduces upstream's answer on
    /// the short forms.
    /// </para>
    /// <para>
    /// <b>S40c REMOVED THE SPLIT ROW, row 1624 of seed 99991, and its reason is worth keeping.</b> It
    /// was <c>(?P&lt;g1&gt;[𐐀A]{2,2})(?:(?(1)(?&lt;!(?P&gt;g1))[^\d]|[a-f]))?([abz]{0,2})$</c> over
    /// '𐐀𐐀A', and it stopped diverging when S40c made <c>do_exact_match</c>'s width early-out count
    /// characters rather than UTF-16 code units. That row's divergence was never this family: the
    /// subject is three characters, the call inside the lookbehind pushes <c>min_width</c> to four,
    /// and upstream refuses the match on arithmetic before matching at all. This port had been
    /// reading the two astral characters as four code units, so it skipped the early-out and found a
    /// match upstream never looked for.
    /// </para>
    /// <para>
    /// The family itself is untouched, and the same row proves it once the width is out of the way:
    /// pad the subject to four characters or more and upstream STILL finds nothing while this port
    /// finds the match, at every length tried up to seven. So only the example row goes, and with it
    /// the <c>split</c> outcome shape - which the predicate still classifies, and which the next wave
    /// to draw one will re-supply. Measured 2026-09-13 on regex 2026.7.19.
    /// </para>
    /// </remarks>
    private const string _groupCallLostMatchRows = """
        {"generator": "interactions", "pattern": "(?P<g1>\\S)(?:(?(1)(?<!(?P>g1))[[:alpha:]]))??([a]{0,0})?\\2\\b", "flags": 0, "namedLists": {}, "subject": "aa𐐨𐐨A", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}}
        {"generator": "interactions", "pattern": "\\b(?(?![\\w\\s])[[:digit:]])(\\w)(?P<g2>[^\\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*", "flags": 16650, "namedLists": {}, "subject": "İİ\nİİﬁﬁ ", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 3, "captures": [[1, 3], [1, 3]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [0, 4]}]}}
        {"generator": "interactions", "pattern": "(?r)\\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\\S)){3}(\\p{Nd}+?)?", "flags": 10, "namedLists": {}, "subject": "AA..0", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}}
        {"generator": "interactions", "pattern": "(?r)^([[:alpha:]]+)(?P<g2>[😀])(?:(?(2)(?=(?P>g2))[^\\p{L}]))+?", "flags": 10, "namedLists": {}, "subject": "a😀\r", "operation": "subf", "template": "{g2}{{", "count": 2, "codepointSpan": null, "outcome": {"kind": "sub", "text": "a😀\r", "count": 0}}
        {"generator": "recursion", "pattern": "(?r)\\b(?<g>[ab]+)(?=(?&g))", "flags": 0, "namedLists": {}, "subject": "ba)((a)((a", "operation": "subf", "template": "{{{0[-1]}ab{1[2]}", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "ba)((a)((a", "count": 0}}
        """;

    /// <summary>
    /// The rows of <c>group-call-loses-the-match</c> that the entry's PREDICATE does not reach, and
    /// which are therefore judged one at a time and keyed on this port's answer - the discipline
    /// <c>bounded-lazy-repeat-partial</c> uses, and for the same reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two rows: row 74396 of a 6000-row <c>interactions</c> wave at seed 20260913, added by
    /// S40c, and row 75821 of the seed-7 one, added by S43. It is the family exactly - a call
    /// reached from a lookahead under <c>(?r)</c>, and a
    /// <c>*</c> repeat holding it, so zero iterations is always available and the piece cannot remove
    /// a match - and upstream contradicts itself on it in the family's usual way. Measured 2026-09-13
    /// on regex 2026.7.19 with <c>finditer</c>, which says more than the <c>sub</c> the wave drew:
    /// </para>
    /// <para>
    ///   <c>(?r)\b\m(?P&lt;g1&gt;[𝔘a])(?:(?(1)(?=(?P&gt;g1))\p{L}))*[a]*</c> over
    ///   '𝔘𝔘\r\raa𐐨𐐨' gives upstream (4, 6) ALONE; delete the <c>(?:...)*</c> piece, or write the
    ///   call out as the class it calls, and upstream gives (4, 6) AND (0, 1) - which is this port's
    ///   answer. The substitution count the row records, 1 against this port's 2, is that missing
    ///   match seen through the template.
    /// </para>
    /// <para>
    /// WHY NOT WIDEN THE PREDICATE. Its substitution arm requires upstream to have replaced NOTHING,
    /// because "upstream's output is the untouched subject" is checkable from the row and "upstream
    /// replaced fewer times than we did" is not - a sub renders as one string, so there is no way to
    /// ask whether upstream's replacements are a prefix of ours. Loosening it to
    /// <c>theirs.Count &lt; mine.Count</c> would classify any port defect that substitutes once too
    /// often in a pattern of this shape. So the row is judged instead, and widening means judging
    /// another and adding it here.
    /// </para>
    /// <para>
    /// <b>Row 2 is S43's, row 75821 of the seed-7 gate, and it is the same shape under the OTHER
    /// substitution operation</b> - a <c>subf</c> where upstream replaced once and this port twice,
    /// so the predicate's "upstream replaced NOTHING" arm cannot reach it either. It is
    /// <c>(?r)(?P&lt;g1&gt;\p{ASCII})(?:(?(1)(?=(?P&gt;g1))\d))*\b\b</c> over '\nﬀ\rﬀİİ', and
    /// upstream contradicts itself on it three separate ways. With the piece present its own
    /// <c>finditer</c> gives (2, 3) ALONE; delete the zero-width <c>(?:...)*</c>, or write the call
    /// out as the class it calls, or delete the lookahead the call sits in, and upstream gives
    /// (2, 3) AND (0, 1) - this port's answer. A <c>*</c> repeat can always take zero iterations, so
    /// the piece cannot remove a match, and the call rather than the conditional is the cause,
    /// because spelling the call out as its body restores the second match. Measured 2026-09-13 on
    /// regex 2026.7.19.
    /// </para>
    /// <para>
    /// <b>Row 3 is S43's, row 10201 of a 6000-row seed-99991 <c>fuzzy,interactions</c> wave, and it
    /// carries the STRONGEST reproduction this family has.</b> The other rows need a wave-sized
    /// pattern to show the defect; this one minimises to
    /// <c>(?P&lt;g1&gt;[[:alpha:]])(?:(?(1)(?&lt;=(?&amp;g1))[^\p{L}]|[A-Z]))([^\d]*)</c>, and
    /// upstream's PLAIN <c>search</c> - no partial asked for at all - answers None on 'a ', 'aa ',
    /// 'aaa ', 'aaaa ', 'aaaaa ' and 'aaaaaa ', while the same pattern with the lookbehind written
    /// out as the class it calls answers (0, 2), (1, 3), (2, 4), (3, 5), (4, 6) and (5, 7). Six
    /// subject lengths, six lost matches, no exceptions. The lookbehind consumes nothing, so it
    /// cannot remove a match, which is this family's own argument in its cleanest form. Measured
    /// 2026-09-13 on regex 2026.7.19.
    /// </para>
    /// <para>
    /// It is here rather than in the predicate for the predicate's own stated reason: upstream did
    /// not lose the match outright on the row the WAVE drew, which asked with <c>partial=True</c> -
    /// it degraded the complete match to a PARTIAL and left the trailing group unset, an outcome
    /// shape <c>UpstreamFoundStrictlyLess</c> does not express. Widening it to reach a partial would
    /// classify every real defect that reports a complete match where a partial is owed.
    /// </para>
    /// </remarks>
    private const string _groupCallLostMatchJudgedRows = """
        {"generator": "interactions", "pattern": "(?r)\\b\\m(?P<g1>[𝔘a])(?:(?(1)(?=(?P>g1))\\p{L}))*[a]*", "flags": 256, "namedLists": {}, "subject": "𝔘𝔘\r\raa𐐨𐐨", "operation": "sub", "template": "\\1\\1\\1", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "𝔘𝔘\r\raaa𐐨𐐨", "count": 1}}
        {"generator": "interactions", "pattern": "(?r)(?P<g1>\\p{ASCII})(?:(?(1)(?=(?P>g1))\\d))*\\b\\b", "flags": 264, "namedLists": {}, "subject": "\nﬀ\rﬀİİ", "operation": "subf", "template": "{}{}", "count": 2, "codepointSpan": null, "outcome": {"kind": "sub", "text": "\nﬀ\r\rﬀİİ", "count": 1}}
        {"generator": "interactions", "pattern": "(?p)(?P<g1>[[:alpha:]])(?:(?(1)(?<=(?&g1))[^\\p{L}]|[A-Z]))([^\\d]*)", "flags": 16386, "namedLists": {}, "subject": "ﬁaıﬁ ", "operation": "search", "partial": true, "codepointSpan": [3, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}, {"number": 1, "success": true, "index": 3, "length": 1, "captures": [[3, 1], [3, 1]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": "g1", "partial": true}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_groupCallLostMatchJudgedRows"/>, in the
    /// same order, as the report renders it.
    /// </summary>
    private static readonly string[] _groupCallLostMatchJudgedOurs =
    [
        "sub 2 '\\ud835\\udd18\\ud835\\udd18\\ud835\\udd18\\ud835\\udd18\\u000d\\u000daaa\\ud801\\udc28\\ud801\\udc28'",
        "sub 2 '\\u000a\\u000a\\ufb00\\u000d\\u000d\\ufb00\\u0130\\u0130'",
        // The COMPLETE match upstream degrades to a partial, trailing group and all - and the exact
        // answer upstream's own inline copy of the lookbehind gives.
        "match 0:(3,2)[(3,2)] 1:(3,1)[(3,1),(3,1)] 2:(5,0)[(5,0)] last=1/g1",
    ];

    /// <summary>
    /// <see cref="_groupCallLostMatchJudgedRows"/> by its question, mapped to this port's answer.
    /// </summary>
    private static readonly Dictionary<string, string> _groupCallLostMatchJudged = OracleWave
        .ParseRows(_groupCallLostMatchJudgedRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _groupCallLostMatchJudgedOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The seven rows of <c>bestmatch-loses-a-partial</c>: five wave draws, the minimised forward
    /// shape and, since S47c, the minimised REVERSED one. Re-recorded whole on 2026-09-14 by
    /// <c>python tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-partial-rows.jsonl</c>,
    /// so this constant is the recorder's own output and the block reproduces.
    /// </summary>
    /// <remarks>
    /// Rows 74938 and 77937 (seed 7), 76251 and 76681 (seed 4242) and 76593 (seed 20260913) of
    /// <c>pwsh -File tools/run-oracle.ps1 -Count 6000</c>, the full default generator list at its
    /// three default seeds, which is 126,000 rows a seed. <b>All three seeds draw one, which is the
    /// first family to manage that</b>; they are the first divergences any wave has produced from
    /// composing fuzzy with the Phase 3 and 4 constructs, which is what S43 widened
    /// <c>interactions</c> to do.
    /// <para>
    /// All five carry the same four things - <c>(?b)</c>, a fuzzy section, a <c>(*SKIP)</c> and
    /// <c>partial=True</c> - and all five are recorded <c>nomatch</c>. <b>Two are not
    /// <c>search</c> rows at all</b>: 74938 is a <c>match</c> and 76251 a <c>fullmatch</c>, so the
    /// defect is not confined to the search loop and a predicate keyed on the operation would be
    /// wrong as well as wide.
    /// </para>
    /// <para>
    /// <b>Row 7 is S47c's widening, and it is the only widening that slice made.</b> The mechanism
    /// it traced has two arms, because <c>RE_OP_SKIP</c> moves <c>slice_start</c> going forwards
    /// (<c>_regex.c:14555</c>) and <c>slice_end</c> going backwards (<c>:14553</c>). Row 7,
    /// <c>(?b)(?r)(?:ab){e&lt;=1}(?:\S(*SKIP)\w|\W)</c> over <c>".ab"</c>, is the MINIMISED witness
    /// of the reversed arm - the reversed twin of row 6, and there for the reason row 6 is there.
    /// It is not the only row that reaches that arm: the two <c>(?r)</c> wave rows, 1 and 2, fire
    /// <c>:14553</c> too, which the blind review measured after a first draft of this comment
    /// claimed rows 1-6 were forward-only. What rows 1 and 2 are not is small - each is several
    /// hundred characters of generated pattern. Upstream answers nothing to row 7; a build with
    /// either candidate fix answers this port's <c>(0, 3)</c> partial with <c>fuzzy=(1,0,0)</c> at
    /// position 1. Which arm each row fires is printed by
    /// <c>python tools/probes/upstream-bestmatch-lost-candidate.py --trace</c> and the answers by
    /// <c>--fix</c>.
    /// </para>
    /// <para>
    /// <b>Seed 20260914 row 76345 is explained by the same mechanism and is still NOT pinned here</b>,
    /// because its recorded question is not on disk: the wave file that held it has been overwritten
    /// and a single-generator re-run at its seed does not redraw it, so recovering it needs the full
    /// 21-generator 6000-row gate at seed 20260914. What was measured rather than assumed is that
    /// the mechanism reaches it -
    /// <c>(?b)(?e)\b(?:\p{Ll}(*SKIP)[^\d]|\W)(?=(?:(\p{ASCII}+)([^\d]*)a){e&lt;=2,s&lt;=1})</c> over
    /// <c>"aaa"</c>, the row's own <c>search</c> from position 0, is None on stock and the
    /// <c>(0, 3)</c> partial this port answers on both fixed builds
    /// (<c>... --fix</c>). <b>It is not None from every door</b>, and a draft that said so was
    /// wrong: <c>search</c> from 1, 2 or 3 answers the empty partial at (3, 3) even on stock. It
    /// will red a wave that draws it again, which is the owner's 2026-09-14 ruling working as
    /// intended.
    /// </para>
    /// </remarks>
    private const string _bestmatchLostPartialRows = """
        {"generator": "interactions", "pattern": "(?b)(?r)(?:[^\\d]+(*SKIP)\\p{L}|[^\\d])(?:(?:(\\p{Nd}{1})(?:(?P<g2>\\p{ASCII})){e<=2,i<=1}){s<=1,i<=1,d<=1}(*SKIP)\\p{L}|\\w)\\D", "flags": 16386, "namedLists": {}, "subject": " \ud801\udc28 ", "operation": "match", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": true}}
        {"generator": "interactions", "pattern": "(?b)(?r)^(\\d)(?:([^\\d]{3,4}?)a(?:[[:alpha:]]{2,3}?){e<=2,s<=1:[A-Za-z_]}){s<=1,i<=1,d<=1}(?:\\S*?(*SKIP)\\w|[^\\d])", "flags": 10, "namedLists": {}, "subject": "a\n\ud801\udc00\r\na\ud835\udd18", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "interactions", "pattern": "(?b)(?e)^(?:(?:AA){s<=1,i<=1,d<=1}(*SKIP)\\p{ASCII}|\\s)(?:(?:A([[a-z]--[aei]])(?:(\\D*)){e<=2,s<=1}){i<=1}(*SKIP)\\p{ASCII}|[A-Z])", "flags": 256, "namedLists": {}, "subject": "A", "operation": "fullmatch", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "interactions", "pattern": "(?b)(?e)(?:a\\w){s<=1,i<=1,d<=1}(?:\\S(*SKIP)[\\p{L}\\p{N}]|\\W)", "flags": 8, "namedLists": {}, "subject": "\r\na\ud835\udd18\n", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)\ufb01(?:(?:(.)\ufb01\ufb01){s<=1:[^a-z]}(*SKIP)[A-Z]|\\p{ASCII})(?P<g2>[[:digit:]])?", "flags": 16394, "namedLists": {}, "subject": "\ufb01\ufb01\ufb01\ufb01\u00df\u00df\n ", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 8, "length": 0, "captures": [[8, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "fuzzy", "pattern": "(?b)(?:ab){e<=1}(?:\\S(*SKIP)\\w|\\W)", "flags": 0, "namedLists": {}, "subject": "ab.", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        {"generator": "fuzzy", "pattern": "(?b)(?r)(?:ab){e<=1}(?:\\S(*SKIP)\\w|\\W)", "flags": 0, "namedLists": {}, "subject": ".ab", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_bestmatchLostPartialRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Every SPAN is the one UPSTREAM ITSELF gives to the same row once the leading <c>(?b)</c> is
    /// deleted and nothing else is changed: in codepoints (0, 3), (0, 7), (0, 1), (4, 5) and (8, 8),
    /// which is what these UTF-16 renderings are on the three astral subjects.
    /// <para>
    /// <b>On four of the five that flagless answer is this port's answer in FULL - groups, counts
    /// and all - and on row 77937 (the second) only the span agrees.</b> Upstream flagless spends no
    /// errors and captures nothing there, where this port answers <c>fuzzy=(1,1,1)</c> with group 2
    /// set. The blind review found this because the probe compared spans while the prose claimed
    /// whole answers; it now prints groups and counts.
    /// </para>
    /// <para>
    /// <b>S47c retires what that used to mean.</b> The flagless answer was only ever a stand-in for
    /// "what upstream would say if the defect were not there", and now there is a build without the
    /// defect to ask directly. On a <c>/Od</c> build of the pinned source carrying either candidate
    /// fix, upstream's answer UNDER <c>(?b)</c> is this port's answer IN FULL on all five wave rows,
    /// <b>77937 included</b>: codepoints (0, 7) with <c>fuzzy=(1,1,1)</c> and group 2 at (0, 3),
    /// which is the (0, 9) / (0, 4) rendering below. So 77937 no longer rests on the weak form alone
    /// because its whole answer agrees - with a repaired upstream rather than with a flagless one.
    /// </para>
    /// <para>
    /// Measured row by row rather than described -
    /// <c>python tools/probes/upstream-bestmatch-loses-a-partial.py</c>, whose second section
    /// replays all five whole, each asked its own operation, and whose third sweeps both anchored
    /// doors; and <c>python tools/probes/upstream-bestmatch-lost-candidate.py --fix</c>, which
    /// builds stock and fixed upstreams and prints all five from each.
    /// </para>
    /// </remarks>
    private static readonly string[] _bestmatchLostPartialOurs =
    [
        "match 0:(0,4)[(0,4)] 1:unset 2:(0,1)[(0,1)] last=2/g2 partial",
        "match 0:(0,9)[(0,9)] 1:unset 2:(0,4)[(0,4)] last=2/- partial fuzzy=(1,1,1)[s:6][i:5][d:4]",
        "match 0:(0,1)[(0,1)] 1:unset 2:unset last=-1/- partial",
        // The substitution position moved from 0 to 5 in S47, and the strictness alarm is what
        // caught it. `start_match` now clears the change list beside the counts (ledger entry 11,
        // mechanism A), so this row no longer reports the leftover of an attempt at position 0 that
        // a `(*SKIP)` abandoned; 5 is inside the (5, 1) span this row matches and 0 was not.
        "match 0:(5,1)[(5,1)] last=-1/- partial fuzzy=(1,0,0)[s:5][i:][d:]",
        "match 0:(8,0)[(8,0)] 1:unset 2:unset last=-1/- partial",
        // S47b. Row 6 is not a wave row at all: it is ledger entry 13's MINIMISED shape, the four
        // conditions with nothing else around them, recorded on 2026-09-14 by
        // `python tools/record-oracle.py --rows`. Held here because the five wave rows are each
        // several hundred characters of generated pattern, so if one of them changes nobody can see
        // from the list what the family actually IS - and because the minimised shape was, until
        // this slice, evidence that existed only in prose and in a probe.
        // And this port's answer to it IS upstream's own flagless answer, span for span: delete the
        // `(?b)` and upstream returns the (0, 3) partial it refused with the flag present.
        "match 0:(0,3)[(0,3)] last=-1/- partial",
        // S47c. Row 7 is row 6 turned round: `RE_OP_SKIP` writes `slice_start` going forwards
        // (`_regex.c:14555`) and `slice_end` going backwards (`:14553`), and this is the MINIMISED
        // witness of the reversed arm. Wave rows 1 and 2 reach that arm as well - measured, after a
        // first draft claimed they did not - but neither is small. Upstream answers nothing; both
        // candidate fixes answer this (0, 3) partial with the substitution at 1, which is also what
        // upstream answers with the `(?b)` deleted.
        "match 0:(0,3)[(0,3)] last=-1/- partial fuzzy=(1,0,0)[s:1][i:][d:]",
    ];

    /// <summary>
    /// The question each row of <see cref="_bestmatchLostCandidateRows"/> asks, which is what
    /// <c>bestmatch-loses-a-candidate</c> is keyed on.
    /// </summary>
    /// <remarks>
    /// S47b, from the independent audit of S44-S46. Keyed on the rows because nothing about the two
    /// compared ANSWERS distinguishes this family from a defect in the feature it is about: a port
    /// that ignored <c>(?b)</c> outright lands on upstream's flagless answer on every row, which is
    /// the one thing the flagless discriminator cannot see, and the entry's own Reason said so
    /// before the audit read it. The flagless test is kept as well - it is what makes an entry go
    /// stale when upstream changes its mind - but it no longer decides on its own.
    /// </remarks>
    private static readonly HashSet<string> _bestmatchLostCandidate = OracleWave
        .ParseRows(_bestmatchLostCandidateRows)
        .Select(Question)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The one row of <c>reversed-skip-invents-a-match</c>, row 74889 (<c>interactions</c>) of the
    /// seed-20260915 6000-row wave, as <c>tools/record-oracle.py</c> wrote it on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Kept for its <c>pruneOutcome</c>, which is the control the entry is keyed on: upstream's own
    /// answer to the same question with every <c>(*SKIP)</c> spelled <c>(*PRUNE)</c>.
    /// </remarks>
    private const string _reversedSkipInventedMatchRows = """
        {"generator": "interactions", "pattern": "(?r)^(?:[^a]+(*SKIP)[^a-f]|\\p{Lu})(?P<g1>\\D)(?:(?(1)(?=(?P>g1))\\w))*$", "flags": 10, "namedLists": {}, "subject": "\ud83d\ude00\ufb03 _\ud801\udc00a\ufb03\ud801\udc00", "operation": "search", "partial": true, "codepointSpan": [0, 6], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 8, "captures": [[0, 8]]}, {"number": 1, "success": true, "index": 7, "length": 1, "captures": [[7, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false}, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}}
        """;

    /// <summary>
    /// The question <see cref="_reversedSkipInventedMatchRows"/> asks, which is half of what
    /// <c>reversed-skip-invents-a-match</c> is keyed on; the other half is the row's own recorded
    /// <c>pruneOutcome</c>.
    /// </summary>
    private static readonly HashSet<string> _reversedSkipInventedMatch = OracleWave
        .ParseRows(_reversedSkipInventedMatchRows)
        .Select(Question)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// <see cref="_bestmatchLostPartialRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _bestmatchLostPartial = OracleWave
        .ParseRows(_bestmatchLostPartialRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _bestmatchLostPartialOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

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
    /// <para>
    /// Row 3 is row 98191 of a 6000-row <c>partial</c> wave at seed 20260913, added by S40c. It
    /// arrived carrying the slice's own family - a group call inside an opposite-direction lookaround
    /// - and does not belong to it: upstream answers the SAME (0, 2) partial with the call written
    /// out as its body, with the piece holding it deleted, and with the <c>\K</c> removed, so none of
    /// those is the cause. It minimises to <c>^([^a-f]??)([\ ])$</c> over ' \r', which is row 1's
    /// shape with a different class, and it passes this family's own discriminator: spelling the
    /// bounded repeat GREEDY removes the partial from upstream too. Measured 2026-09-13 on regex
    /// 2026.7.19.
    /// </para>
    /// </remarks>
    private static readonly string[] _boundedLazyOurs =
    [
        "no match",
        "match 0:(0,1)[(0,1)] 1:unset last=-1/- partial",
        "no match",
    ];

    /// <summary>
    /// Every row of <see cref="_boundedLazyRows"/> by its question - pattern, flags, subject,
    /// operation, partial and slice - mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _boundedLazy = OracleWave
        .ParseRows(_boundedLazyRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _boundedLazyOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The two rows of <c>skip-blocks-a-repeat-retreat-partial</c>, as
    /// <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-13 against the NEW pin.
    /// </summary>
    /// <remarks>
    /// Row 1 is row 98050 of <c>pwsh -File tools/run-oracle.ps1 -Count 6000</c> at seed 20260913,
    /// the full default generator list, as the wave drew it - the single row in 378,000 whose
    /// answer the sync changed. Row 2 is it minimised by hand to three ASCII characters and no
    /// flags, which is the form a reader can check without running anything: the retreat
    /// <c>(a+)</c> has to make for <c>\1</c> to match is the whole of the difference.
    /// <para>
    /// <b>Both rows are recorded against 2026.9.10 and only 2026.9.10.</b> The row the wave drew
    /// carried upstream's OLD answer until this slice, and it is upstream's answer that moved -
    /// proven rather than assumed, by replaying the identical row against the engine at S43's
    /// HEAD, where this port gives the same (0, 5) it gives now.
    /// </para>
    /// </remarks>
    private const string _skipBlocksRetreatRows = """
        {"generator": "partial", "pattern": "\\b([İ]+)\\1(?:.{3}?(*SKIP)[^[\\p{L}--[a-z]]]|\\S)", "flags": 266, "namedLists": {}, "subject": "İİSsS", "operation": "search", "partial": true, "codepointSpan": [5, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 5, "length": 0, "captures": [[5, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(a+)\\1x(*SKIP)b", "flags": 0, "namedLists": {}, "subject": "aax", "operation": "search", "partial": true, "codepointSpan": [3, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 3, "length": 0, "captures": [[3, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_skipBlocksRetreatRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Each is upstream 2026.7.19's own answer to the same row, and PCRE2 10.47's answer to the
    /// minimised one. Not upstream's anchored sweep, which this family does not need: the
    /// contradiction is inside 2026.9.10 itself, between the partial it refuses and the complete
    /// match it accepts on the same pattern one character later. See the entry's own reason.
    /// </remarks>
    private static readonly string[] _skipBlocksRetreatOurs =
    [
        "match 0:(0,5)[(0,5)] 1:(0,1)[(0,1)] last=1/- partial",
        "match 0:(0,3)[(0,3)] 1:(0,1)[(0,1)] last=1/- partial",
    ];

    /// <summary>
    /// <see cref="_skipBlocksRetreatRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _skipBlocksRetreat = OracleWave
        .ParseRows(_skipBlocksRetreatRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _skipBlocksRetreatOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The seven rows of <c>bestmatch-walk-truncated-by-a-skip</c>: a <c>(*SKIP)</c> ends upstream's
    /// own <c>(?b)</c> walk on its first successful candidate, so a better match further along the
    /// subject is never attempted.
    /// </summary>
    /// <remarks>
    /// The first five were not drawn by a wave. The mechanism was found by reading
    /// <c>do_best_fuzzy_match</c>'s loop guard (<c>upstream/src/_regex.c:17625</c>) while S48
    /// inventoried ledger entry 5, and then hunted for over a small alphabet - 11,340 shapes, 1,861
    /// of which answer differently with <c>(*SKIP)</c> than with <c>(*PRUNE)</c>. Row 1 is the
    /// minimisation, four ASCII characters and no flags; rows 2 to 5 are the first larger shapes the
    /// hunt drew, kept because each one spends a different error kind.
    /// <para>
    /// Rows 6 and 7 ARE drawn, by S52's 6000-row three-seed gate and judged by its eleventh sitting -
    /// seed 4242 rows 76778 and 77119, taken verbatim out of <c>wave-4242.jsonl</c> and so carrying
    /// the recorder's own <c>bestmatchFreeOutcome</c> and <c>pruneOutcome</c> beside the drawn
    /// answer. That the hunt's authored shapes and a generator drawing 126,000 rows a seed land on
    /// one mechanism is the first evidence this family is reachable by a GENERATOR rather than only
    /// by someone already looking for it.
    /// </para>
    /// <para>
    /// Re-recordable in full:
    /// <c>python tools/record-oracle.py --rows tools/probes/bestmatch-walk-truncated-rows.jsonl</c>,
    /// or straight through the runner as
    /// <c>pwsh -File tools/run-oracle.ps1 -Rows &lt;that file&gt;</c>. Its first seven rows are this
    /// entry's seven in this order, and it carries an EIGHTH
    /// which is deliberately not in this entry: <c>(?b)(?:\w(*SKIP)a|a){e&lt;=1}</c> over
    /// <c>'a b c'</c>, on which both engines answer <c>(0, 2)</c> with one substitution at 1. It was
    /// put there when rows 1 to 5 were the whole entry and all five were PERFECT matches, because a
    /// wave holding no fuzzy match with an error in it fails
    /// <c>OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts</c>'s
    /// non-degeneracy guard - so without it the artifact could not be replayed through the runner at
    /// all. Row 7's judged answer carries an error of its own now, so that is no longer the only
    /// thing keeping the eighth row here; it doubles as the family's agreeing control, which is
    /// reason enough. The whole file replays <c>agree 1, expected 7, diverge 0 of 8</c>
    /// (S52 sitting 13, 2026-09-15).
    /// </para>
    /// </remarks>
    private const string _bestmatchWalkTruncatedRows = """
        {"generator": "interactions", "pattern": "(?b)(?:a(*SKIP)b){e<=1}", "flags": 0, "namedLists": {}, "subject": "axab", "operation": "search", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)(?:b(*SKIP)a){e<=1}", "flags": 0, "namedLists": {}, "subject": "bxba", "operation": "search", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)(?:b(*SKIP)ab){e<=1}", "flags": 0, "namedLists": {}, "subject": "bbbab", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)(?:\\w(*SKIP)ab){e<=1}", "flags": 0, "namedLists": {}, "subject": "abcabc", "operation": "search", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [1]}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [1]}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [1]}}}
        {"generator": "interactions", "pattern": "(?b)(?:\\w(*SKIP)ab){e<=2}", "flags": 0, "namedLists": {}, "subject": "qqxyab", "operation": "search", "codepointSpan": [2, 6], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 4, "captures": [[2, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [3], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [3], "deletions": []}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [1, 2], "insertions": [], "deletions": []}}}
        {"generator": "interactions", "pattern": "(?b)(?r)(?:[a-f]\\D*){e<=1}(?:\\S*(*SKIP)\\p{Lu}|[^a-f])(?(?=[a-f])[[:digit:]])\\b", "flags": 8, "namedLists": {}, "subject": "😀aa_\r\n_A", "operation": "match", "partial": true, "codepointSpan": [0, 8], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0]}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0]}}], "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [0]}}, "pruneOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 7, "captures": [[2, 7]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "interactions", "pattern": "(?b)(?e)^(?:\\p{Ll}\\p{ASCII}(?:A){e<=2:\\w}){e<=1}(?:[^\\d]?(*SKIP)[^a]|[abz])$", "flags": 16394, "namedLists": {}, "subject": "00AA ", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}, "anchoredScan": [], "bestmatchFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [], "deletions": []}, "codepointSpan": [0, 5]}]}, "pruneOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [], "deletions": []}, "codepointSpan": [0, 5]}]}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_bestmatchWalkTruncatedRows"/>, in the
    /// same order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first five are PERFECT matches - no errors at all, which is why none of them carries a
    /// <c>fuzzy=</c> field - and on every one it is the answer the same compiled pattern's own
    /// anchored <c>match</c> gives at that position, on upstream as well as here. It is also what
    /// upstream answers with the verb replaced by <c>(*PRUNE)</c> and with the verb deleted.
    /// </para>
    /// <para>
    /// The two S52 sitting 11 added are upstream's own <c>pruneOutcome</c> and nothing else, and
    /// row 7 is the first in this entry whose judged answer CARRIES an error. What the family
    /// promises is the fewest errors among the matches that exist, not zero of them: on row 7
    /// upstream finds no match at all where its own <c>(*PRUNE)</c>, verb-free and
    /// <c>(?b)</c>-free spellings all find the one substitution this port answers.
    /// </para>
    /// </remarks>
    private static readonly string[] _bestmatchWalkTruncatedOurs =
    [
        "match 0:(2,2)[(2,2)] last=-1/-",
        "match 0:(2,2)[(2,2)] last=-1/-",
        "match 0:(2,3)[(2,3)] last=-1/-",
        "match 0:(2,3)[(2,3)] last=-1/-",
        "match 0:(3,3)[(3,3)] last=-1/-",
        "match 0:(2,7)[(2,7)] last=-1/-",
        "matches 1 | match 0:(0,5)[(0,5)] last=-1/- fuzzy=(1,0,0)[s:0][i:][d:]",
    ];

    /// <summary>
    /// <see cref="_bestmatchWalkTruncatedRows"/> by its question, mapped to this port's judged
    /// answer.
    /// </summary>
    private static readonly Dictionary<string, string> _bestmatchWalkTruncated = OracleWave
        .ParseRows(_bestmatchWalkTruncatedRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _bestmatchWalkTruncatedOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The five rows of <c>posix-fuzzy-contradicts-its-own-flagless-answer</c>, as
    /// <c>tools/record-oracle.py --rows</c> wrote them, and all five because between them they are
    /// the shapes the defect appears in - an error spent on a span that needs fewer, a match charged
    /// an error its own POSIX-free engine fits with none, a substitution whose replacement text
    /// moves, and a substitution COUNT that moves while the text does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rows 76983 (seed 7) and 76101 (seed 20260914) are the plain shape: both engines report the
    /// same answer except for what it cost, and upstream's own <c>posixFreeOutcome</c> is this port's
    /// answer exactly. <b>Row 73895 (seed 7) is the shape that needs the anchored question</b>: the
    /// whole SCAN moves when POSIX goes - upstream's POSIX-free scan starts its first match at
    /// codepoint 4 rather than 0 - so the recorded <c>posixFreeOutcome</c> is NOT this port's answer
    /// and cannot be the key. What judges that row is upstream's own POSIX-free <c>fullmatch</c> over
    /// the span it reported UNDER POSIX, which answers with NO errors where the POSIX scan charged
    /// one; that is upstream contradicting itself at identical flags, and it is this port's answer to
    /// the code unit. Measured 2026-09-14,
    /// <c>tools/probes/upstream-posix-and-atomic-free-answers.py</c>.
    /// </para>
    /// <para>
    /// <b>S52 added rows 24430 and 24916, both seed 7 of the three-seed 2000-row wave of commit
    /// 407c0cb (2026-09-15), and the second is a symptom this entry had not seen.</b> Row 24430 is
    /// row 76983's mechanism reached through a <c>\L&lt;name&gt;</c> list and with POSIX set as a
    /// FLAG rather than written <c>(?p)</c>: the scan's two matches agree on both spans and on the
    /// second match's cost, and upstream charges the FIRST span one deletion where its own POSIX-free
    /// engine spends none over the identical span. <b>Row 24916 does not move a cost or a span at
    /// all - POSIX invents a MATCH.</b> <c>subn</c> with <c>count=2</c> replaces TWICE under POSIX
    /// and once without it, on a template that expands to nothing either way, so the two answers
    /// carry the same text and only the count says what happened. POSIX chooses leftmost-longest
    /// among the matches the ordinary engine can make; it cannot produce one the flagless engine
    /// cannot. Both measured 2026-09-15 by the same probe, which carries both rows.
    /// </para>
    /// </remarks>
    private const string _posixOvercostRows = """
        {"generator": "interactions", "pattern": "(?b)(?e)(?r)(?p)(?:(?P<g1>\\D+?)([\\p{L}||\\p{N}]*)\\w){e<=2,s<=1}(?P<g3>[A])(?:(?(3)(?!(?P>g3))[\\w--[0-9]]))*?", "flags": 266, "namedLists": {}, "subject": "\nAA😀😀aa ", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}, {"number": 1, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 2, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}, {"number": 3, "success": true, "index": 8, "length": 1, "captures": [[8, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "fuzzyCounts": [1, 0, 0], "codepointSpan": [0, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 0, "captures": [[1, 0]]}, {"number": 3, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "codepointSpan": [0, 3]}]}, "bestmatchFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 9, "captures": [[0, 9]]}, {"number": 1, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 2, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}, {"number": 3, "success": true, "index": 8, "length": 1, "captures": [[8, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "fuzzyCounts": [1, 0, 0], "codepointSpan": [0, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 0, "captures": [[1, 0]]}, {"number": 3, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "codepointSpan": [0, 3]}]}, "posixFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 5, "length": 4, "captures": [[5, 4]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}, {"number": 2, "success": true, "index": 7, "length": 0, "captures": [[7, 0]]}, {"number": 3, "success": true, "index": 8, "length": 1, "captures": [[8, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "codepointSpan": [4, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 0, "captures": [[1, 0]]}, {"number": 3, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 3, "lastGroup": "g3", "partial": false, "codepointSpan": [0, 3]}]}}
        {"generator": "interactions", "pattern": "(?e)(?r)(?p)(?:[^\\d][a\\d]\\p{L}){s<=1,i<=1,d<=1}(\\p{Lu})+\\b", "flags": 8, "namedLists": {}, "subject": " ﬀS", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 1], "codepointSpan": [0, 3]}]}, "posixFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [1]}, "codepointSpan": [0, 3]}]}}
        {"generator": "interactions", "pattern": "(?b)(?e)(?r)(?:\\p{Ll}+.([a]+)){s<=1:\\W}(?:[a](?P<g2>[\\w\\s]*)){e<=1}$", "flags": 65536, "namedLists": {}, "subject": "ıı\rAAﬁ\r\n", "operation": "subf", "template": "{g2}{g2}{1}", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "AAﬁ\rAAﬁ\r\r\n", "count": 1}, "bestmatchFreeOutcome": {"kind": "sub", "text": "AAﬁ\r\nAAﬁ\r\n\r", "count": 1}, "posixFreeOutcome": {"kind": "sub", "text": "AAﬁ\r\nAAﬁ\r\n\r", "count": 1}}
        {"generator": "interactions", "pattern": "(?e)(?r)\\b(?P<g1>[[:alpha:]]+?)\\L<w1>{s<=1,i<=1,d<=1:\\d}(?<!(?:[a\\d](?P<g2>[\\p{L}\\p{N}]+?)😀){e<=2,i<=1})", "flags": 65544, "namedLists": {"w1": ["😀A", "😀𝔘", "😀𝔘A"]}, "subject": "a😀𐐨😀𝔘\r\nAa", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 6, "captures": [[3, 6]]}, {"number": 1, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "fuzzyCounts": [0, 0, 1], "codepointSpan": [2, 5]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "fuzzyCounts": [0, 0, 1], "codepointSpan": [0, 2]}]}, "posixFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 6, "captures": [[3, 6]]}, {"number": 1, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [2, 5]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [3]}, "codepointSpan": [0, 2]}]}}
        {"generator": "interactions", "pattern": "(?b)(?:([a]*)[a]*){s<=1}\\g<1>\\K$", "flags": 81930, "namedLists": {}, "subject": "\rA\n", "operation": "sub", "template": "\\1", "count": 2, "codepointSpan": null, "outcome": {"kind": "sub", "text": "\rA\n", "count": 2}, "bestmatchFreeOutcome": {"kind": "sub", "text": "\rA\n", "count": 2}, "posixFreeOutcome": {"kind": "sub", "text": "\rA\n", "count": 1}}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_posixOvercostRows"/>, in the same order,
    /// as the report renders it.
    /// </summary>
    /// <remarks>
    /// Rows 2, 3, 4 and 5 are upstream's own recorded <c>posixFreeOutcome</c> - rows 3 and 5 to the
    /// character, rows 2 and 4 to the counts, because a POSIX row carries no change positions on
    /// either side (ledger entry 9) and the comparison drops them from both. Row 1 is upstream's own
    /// POSIX-free <c>fullmatch</c> over the span upstream reported under POSIX, which is a different
    /// question from the recorded scan and the reason this entry lists rows rather than keying on the
    /// discriminator alone.
    /// </remarks>
    private static readonly string[] _posixOvercostOurs =
    [
        "matches 2 | match 0:(0,9)[(0,9)] 1:(0,7)[(0,7)] 2:(7,0)[(7,0)] 3:(8,1)[(8,1)] last=3/g3 "
            + "|| match 0:(0,3)[(0,3)] 1:(0,1)[(0,1)] 2:(1,0)[(1,0)] 3:(2,1)[(2,1)] last=3/g3",
        "matches 1 | match 0:(0,3)[(0,3)] 1:(2,1)[(2,1)] last=1/- fuzzy=(0,0,1)[changes unavailable upstream]",
        // Deliberately NOT a verbatim string: `OracleWave.Printable` renders the ligature and the two
        // line endings as the six-character escapes themselves, so each backslash has to survive into
        // the literal rather than being read as one.
        "sub 1 'AA\\ufb01\\u000d\\u000aAA\\ufb01\\u000d\\u000a\\u000d'",
        "matches 2 | match 0:(3,6)[(3,6)] 1:(3,2)[(3,2)] 2:unset last=1/g1 "
            + "|| match 0:(0,3)[(0,3)] 1:(0,1)[(0,1)] 2:unset last=1/g1 "
            + "fuzzy=(0,0,1)[changes unavailable upstream]",
        "sub 1 '\\u000dA\\u000a'",
    ];

    /// <summary>
    /// <see cref="_posixOvercostRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _posixOvercost = OracleWave
        .ParseRows(_posixOvercostRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _posixOvercostOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The two rows of <c>atomic-group-leaks-a-change-position</c> - row 74033 of the seed-20260914
    /// 6000-row gate and row 74510 of the seed-7 one - as <c>tools/record-oracle.py --rows</c> wrote
    /// them on 2026-09-14 and 2026-09-15.
    /// </summary>
    /// <remarks>
    /// Two rows, because two is all any wave has drawn: the shape needs an atomic group whose body
    /// carries a fuzzy section that spends an error and then abandons a sub-attempt, which is a
    /// narrower accident than the earlier-attempt leak <c>leakFreeFuzzy</c> already covers. Note each
    /// row's own <c>leakFreeFuzzy</c>, which agrees with upstream's drawn answer and NOT with this
    /// port's: that is the entry's reason for existing said in one field, because the anchored
    /// question removes an EARLIER attempt's leak and an atomic group's is inside one attempt.
    /// <para>
    /// Row 74510 is the STRONGER of the two and was added by S52 sitting 9. On row 74033 upstream's
    /// drawn change list is internally consistent - counts <c>(2,1,1)</c> against two substitutions,
    /// one insertion and one deletion - and only the deletion's POSITION differs, which is the shape
    /// the entry's own <c>Reason</c> says is indistinguishable from this port miscomputing a
    /// position. On row 74510 upstream's list is of the wrong KIND for upstream's own counts: it
    /// counts <c>(1,2,0)</c> - one substitution and two insertions - and then lists TWO substitutions
    /// and ONE insertion. That is upstream contradicting its own documentation on one answer, before
    /// any comparison with this port is made.
    /// </para>
    /// </remarks>
    private const string _atomicLeakedChangeRows = """
        {"generator": "interactions", "pattern": "^(?:\\p{Ll}\\w??[a-f]){1i+2d+1s<=3}(?>(?:\\p{Ll}(?:\\p{L}){s<=1,i<=1,d<=1}){d<=1})$", "flags": 0, "namedLists": {}, "subject": "AA𝔘𝔘", "operation": "search", "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 1, 1], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [2], "deletions": [2]}}, "leakFreeFuzzy": [{"fuzzyCounts": [2, 1, 1], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [2], "deletions": [2]}}], "atomicFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 1, 1], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [2], "deletions": [4]}}}
        {"generator": "interactions", "pattern": "^(?:(\\p{Lu})([\\w\\s])\\W){1i+2d+1s<=3}(?>(?:(\\s?)(?:([^\\d])){s<=1,i<=1,d<=1:\\w}){2i+1d+1s<=2})([a\\d]{0,})$", "flags": 16394, "namedLists": {}, "subject": "ﬃﬃ ﬃﬃßß", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}, {"number": 3, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}, {"number": 4, "success": true, "index": 6, "length": 1, "captures": [[6, 1]]}, {"number": 5, "success": true, "index": 7, "length": 0, "captures": [[7, 0]]}], "lastIndex": 5, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 2, 0], "fuzzyChanges": {"substitutions": [3, 4], "insertions": [3], "deletions": []}, "codepointSpan": [0, 7]}]}, "leakFreeFuzzy": [{"fuzzyCounts": [1, 2, 0], "fuzzyChanges": {"substitutions": [3, 4], "insertions": [3], "deletions": []}}], "atomicFreeOutcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}, {"number": 3, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}, {"number": 4, "success": true, "index": 6, "length": 1, "captures": [[6, 1]]}, {"number": 5, "success": true, "index": 7, "length": 0, "captures": [[7, 0]]}], "lastIndex": 5, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 2, 0], "fuzzyChanges": {"substitutions": [5], "insertions": [3, 4], "deletions": []}, "codepointSpan": [0, 7]}]}}
        """;

    /// <summary>
    /// The one row of <c>reversed-lookahead-change-at-the-match-start</c>, row 73463 of the seed-7
    /// 6000-row gate, as <c>tools/record-oracle.py --rows</c> wrote it on 2026-09-14.
    /// </summary>
    /// <remarks>
    /// One row, because one is all any wave has drawn, and KEYED ON THE ROW rather than on a
    /// recorded discriminator because the control that judges it cannot be recorded: deleting the
    /// <c>(?r)</c> makes upstream answer None to THIS row, so there is no forward answer to write
    /// down. The contradiction was established on a minimised reproducer instead - see the entry's
    /// own <c>Reason</c> - and the gap test carries that reproducer.
    /// <para>
    /// Note the row's own <c>leakFreeFuzzy</c>, which is a THIRD answer again: substitutions at 5
    /// and 6 where the row records 1 and 0 and this port answers 6 and 7. Anchoring at the reported
    /// span cuts the lookahead off - it has to read past <c>endpos</c> - so that question is not the
    /// same question, and the recorder's own docstring names this as one of the three shapes where
    /// it cannot be asked.
    /// </para>
    /// </remarks>
    private const string _reversedLookaheadChangeRows = """
        {"generator": "interactions", "pattern": "(?r)A(?=(?:[a\\d]([^a-f]*)){1i+2d+1s<=3})(?:(?:([A-Z]{1,})([^\\p{L}]*)\\D+){e<=2,s<=1}(*PRUNE)[a]|[abz])", "flags": 16394, "namedLists": {}, "subject": "\r\n\r\nAAAA_", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 4, "length": 4, "captures": [[4, 4]]}, {"number": 1, "success": true, "index": 6, "length": 3, "captures": [[6, 3]]}, {"number": 2, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}, {"number": 3, "success": true, "index": 6, "length": 0, "captures": [[6, 0]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [1, 0], "insertions": [], "deletions": []}, "codepointSpan": [4, 8]}]}, "leakFreeFuzzy": [{"fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [5, 6], "insertions": [], "deletions": []}}]}
        """;

    /// <summary>
    /// This port's judged answer to <see cref="_reversedLookaheadChangeRows"/>, as the report
    /// renders it.
    /// </summary>
    /// <remarks>
    /// Both substitutions sit one code unit to the RIGHT of upstream's, which is the distance from
    /// the match start to the position the lookahead tests - one leading <c>A</c>. The minimised
    /// reproducer in the entry's <c>Reason</c> shows the same shift of one, and shows upstream's own
    /// FORWARD matching of that pattern answering this port's positions.
    /// </remarks>
    private static readonly string[] _reversedLookaheadChangeOurs =
    [
        "matches 1 | match 0:(4,4)[(4,4)] 1:(6,3)[(6,3)] 2:(5,1)[(5,1)] 3:(6,0)[(6,0)] last=1/- "
            + "fuzzy=(2,0,0)[s:6,7][i:][d:]",
    ];

    /// <summary>
    /// <see cref="_reversedLookaheadChangeRows"/> by its question, mapped to this port's judged
    /// answer.
    /// </summary>
    private static readonly Dictionary<string, string> _reversedLookaheadChange = OracleWave
        .ParseRows(_reversedLookaheadChangeRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _reversedLookaheadChangeOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary><see cref="_atomicLeakedChangeRows"/> by its question.</summary>
    private static readonly HashSet<string> _atomicLeakedChange = OracleWave
        .ParseRows(_atomicLeakedChangeRows)
        .Select(Question)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The one row of <c>fuzzy-changes-of-the-wrong-kind-for-their-own-counts</c>, row 74345 of the
    /// seed-7 6000-row gate, as <c>tools/record-oracle.py --rows</c> wrote it on 2026-09-15.
    /// </summary>
    /// <remarks>
    /// One row, because one is all any wave has drawn. Note the row's own <c>leakFreeFuzzy</c>,
    /// which reproduces upstream's drawn answer CHARACTER FOR CHARACTER: the leak is inside one
    /// attempt, so S47's anchored question cannot see it, exactly as on the atomic-group and
    /// reversed-lookahead rows above.
    /// </remarks>
    private const string _wrongKindChangeRows = """
        {"generator": "interactions", "pattern": "(?r)(?!(?:(?P<g1>\\p{Nd}{0,2})ß){s<=1,i<=1,d<=1:\\s})(?:𐐀𐐀){e<=2}\\b", "flags": 264, "namedLists": {}, "subject": "ßß𐐀\n", "operation": "search", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [], "deletions": [1]}}, "leakFreeFuzzy": [{"fuzzyCounts": [0, 2, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [], "deletions": [1]}}], "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's judged answer to <see cref="_wrongKindChangeRows"/>, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Two INSERTIONS, which is the only kind the counts both engines agree on can take. Upstream
    /// reports the same counts and then lists a substitution and a deletion.
    /// </remarks>
    private static readonly string[] _wrongKindChangeOurs =
    [
        "match 0:(0,4)[(0,4)] 1:unset last=-1/- partial fuzzy=(0,2,0)[s:][i:2,1][d:]",
    ];

    /// <summary>
    /// <see cref="_wrongKindChangeRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _wrongKindChange = OracleWave
        .ParseRows(_wrongKindChangeRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _wrongKindChangeOurs[i]))
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
                + "upstream issue 589. Phase 7 owns the prefilter and must not import this answer.\n"
                + "WIDENED BY S37 TO THE SECOND SYMPTOM: this port answers its OWN partial somewhere "
                + "else rather than nothing at all. Three rows of a 6000-row `interactions` wave show "
                + "it - one at seed 4242 and two at 20260912, none at seed 7 - and all three carry a "
                + "`(*SKIP)`, which is the piece S36's widening put "
                + "into the generator. `(?:\\w{2,}(*SKIP)\\w|\\w)\\B` over 'a.Aa' is the minimised "
                + "shape: upstream's `search(partial=True)` is (0, 4) partial - the WHOLE subject, "
                + "from the search start to the end of the text - and its own "
                + "`match('a.Aa', 0, 4, partial=True)` over that very span is None. This port answers "
                + "the partial at (2, 4), which is upstream's own `match(pos=2, partial=True)` "
                + "answer and the leftmost position at which anything matches. Delete the verb and "
                + "upstream's search gives a COMPLETE match at (2, 3); make it `(*PRUNE)`, which "
                + "moves no bound, and it gives this port's partial. Measured 2026-09-12, "
                + "tools/probes/upstream-search-start-whole-region-partial.py.\n"
                + "NARROWED BY S40b, WHICH TOOK HALF OF THIS FAMILY BACK. Until S40b this port "
                + "answered (4, 4) on the minimised row and the arm's rows 1 and 4 read (4, 0) and "
                + "(5, 0). That was not the prefilter: it was this port's OWN defect, a `(*SKIP)` in "
                + "the non-partial pass of a partial search leaving `slice_start` moved for the "
                + "partial pass, so the retry skipped every start position below it. S40b restores "
                + "both slice bounds with `text_pos` in `Matcher.DoMatch`, and both rows moved to "
                + "upstream's own anchored answer. WHAT IS LEFT HERE IS THE PREFILTER ALONE - "
                + "upstream reporting a span its own `match` denies - and a row whose only "
                + "difference is WHERE this port started is now a defect to fix, not a row to "
                + "classify. Measured 2026-09-13, tools/probes/upstream-partial-retry-slice-restore.py.\n"
                + "THE NEW ARM IS KEYED ON ROWS, exactly as `bounded-lazy-repeat-partial` below is, "
                + "and the first draft of it was not - which the S37 blind review killed with a "
                + "reproduction. That draft asked only that upstream's partial span the whole "
                + "searched region, which is the prefilter's fingerprint (`search_start`'s partial "
                + "arms set `new_position->text_pos` to the end of the text or of the slice, :8471 "
                + "and :8487, while the match start stays where the search began) - and put NO "
                + "condition on this port's side beyond 'a partial, rendered differently'. A "
                + "one-character defect in the port's own partial start was then classified instead "
                + "of reported. No predicate over this port's answer is narrow enough here, so the "
                + "arm lists the rows a probe has individually judged with the answer this port is "
                + "judged to be right about, and widening it means judging another row.",
            PinnedBy: "PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_"
                + "subject_finds_no_partial_here and .A_skip_alternation_partial_starts_at_the_"
                + "leftmost_position_that_matches",
            // The first arm's own row, then the second arm's judged rows VERBATIM rather than
            // retyped. They were a copy of `_searchStartElsewhereRows` until S43 added a fifth row
            // and had to edit the same JSON in two places to keep the alarm honest - which is the
            // drift this file spends three paragraphs warning about, reproduced inside one entry.
            Example: _searchStartEmptyReverseRow + "\n" + _searchStartElsewhereRows,
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
                && (
                    ours is NoMatchOutcome
                    // S37's arm: this port answered its OWN partial somewhere else, on a row a probe
                    // has judged one at a time. Keyed on the row, because no predicate over this
                    // port's answer is narrow enough - see the Reason above.
                    || (
                        _searchStartElsewhere.TryGetValue(Question(row), out string? judgedPartial)
                        && string.Equals(ours.Describe(), judgedPartial, StringComparison.Ordinal)
                    )
                )
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
            //
            // FORWARD ONLY since S40d, and the guard is new rather than the scope: the recorder
            // refused a reversed walk outright until then, so nothing reversed could reach this
            // predicate. It can now, and a reversed carried slice is `slice_end` rather than
            // `slice_start` and has its own two entries below - so the direction is checked here to
            // keep an id naming the mechanism it actually is.
            Applies: static (row, ours) =>
                !IsReversed(row)
                && row.AnchoredScan is { } anchored
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
                + "`python tools/probes/upstream-reversed-overlapped-skip.py [--crash]`.\n"
                + "A FOURTH FACT, and the one S52 added the walk tell for (seed 4242 row 119927 of "
                + "the 6000-row gate): the carried `slice_end` can move a CAPTURE's END while every "
                + "whole-match span stays put, which the moved-spans-right test cannot see because "
                + "it requires an inner group to keep its length. "
                + "`(?r)^(?:[^a]*?(*SKIP)\\w|\\u200d)(?P<g1>\\S*(*SKIP)A)` over 'aa\\u200d\\u200dAAa', "
                + "overlapped and prefilter-free, has upstream reporting (0, 6) with g1 at (1, 6) "
                + "and then (0, 5) with g1 at (1, 6) AGAIN - a capture reaching past the end of its "
                + "own match, in a pattern with no lookaround and no `\\K` to put one there, which "
                + "is the same self-evident symptom the forward entry records. Upstream's OWN "
                + "stepwise walk gives (0, 6) g1=(1, 6) and then (0, 5) g1=(1, 5), which is this "
                + "port's answer rendering for rendering, and its `(*PRUNE)` spelling and its "
                + "verb-free spelling both give that answer too. The row carries that walk as its "
                + "recorded `anchoredScan`, so the tell is a fact of the row rather than a probe's "
                + "claim about it.",
            PinnedBy: "BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_keeps_every_"
                + "span_where_upstreams_own_single_shot_door_puts_it",
            Example: """
            {"generator": "verbs", "pattern": "(?r)(?:\\p{L}+(*SKIP)\\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\\w\\s]))", "flags": 0, "namedLists": {}, "subject": "AAAA00", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 5]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 4, "length": 1, "captures": [[4, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 5]}]}
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
            // keeping its end and the match count would satisfy the moved-spans-right test. It used
            // to be unbounded, because the reversed walk could not be recorded to check against -
            // tools/record-oracle.py refused an `anchoredScan` for a reversed row, since stepping
            // one means moving `endpos`, which truncates the subject and changes what every
            // end-of-subject assertion means.
            //
            // CLOSED for the rows that carry a walk, and this is the Phase 6 oracle hardening the
            // paragraph used to ask for: S40d made the recorder's refusal read the PATTERN rather
            // than the direction, so a reversed row with no end-sensitive item now carries
            // upstream's own stepwise answer, and S52 put it to work as the second arm of the
            // predicate below. A row that has the walk is judged by it and the hole does not reach
            // it; a row that does not is judged by the moved-spans-right test alone and the
            // paragraph above still applies to it.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                && string.Equals(row.Operation, "finditer-overlapped", StringComparison.Ordinal)
                && row.Expected is MatchesOutcome theirScan
                && ours is MatchesOutcome ourScan
                && (
                    EveryStaleSliceHasOnlyMovedSpansRight(theirScan, ourScan)
                    // THE WALK TELL, added by S52 sitting 10 for seed 4242 row 119927 of the
                    // 6000-row gate. It is the discriminator `overlapped-skip-stale-slice` uses
                    // forward, which S40d brought within reach of a reversed row by making the
                    // recorder's refusal read the pattern rather than the direction - and it is
                    // the one this entry's own "hole, said out loud" paragraph above names as the
                    // way to close that hole. The count is required to be EQUAL here because a
                    // scan upstream ran too long or too short is the two entries below, not this
                    // one, and an id has to keep naming the mechanism it is.
                    || (
                        theirScan.Matches.Count == ourScan.Matches.Count
                        && row.AnchoredScan is { } walked
                        && !string.Equals(theirScan.Describe(), walked.Describe(), StringComparison.Ordinal)
                        && string.Equals(ourScan.Describe(), walked.Describe(), StringComparison.Ordinal)
                    )
                )
        ),
        new(
            Id: "overlapped-skip-extra-match-reversed",
            Reason: "Upstream bug, and the same carried slice as the two entries above seen as EXTRA "
                + "MATCHES rather than as moved spans: under `(?r)` a `(*SKIP)` moves `slice_end` "
                + "(upstream/src/_regex.c:14545), nothing puts it back between the matches of one "
                + "scan, and upstream's next attempt then succeeds in a view of the subject that "
                + "ends where the verb left the bound. This port's scan is a PREFIX of upstream's, "
                + "and every match upstream reports beyond it refutes itself on the row alone, in "
                + "one of two ways.\n"
                + "ONE, the assertion tell: the pattern ends in `$` and upstream's extra match ends "
                + "at a position that is not the end of a line in the real subject. "
                + "`regex.finditer(r'(?r)(?:.{2}(*SKIP)A|x)$', 'bxA', regex.M, overlapped=True)` "
                + "gives (0, 3) then (1, 2), and the second needs `$` to hold at index 2, where the "
                + "subject has an 'A'. Removing the verb, or making it `(*PRUNE)`, leaves upstream "
                + "with (0, 3) alone - so the extra match exists only because `$` read the moved "
                + "bound, which is precisely the defect S35 fixed on this side.\n"
                + "TWO, the capture tell: upstream's extra match carries a capture OUTSIDE its own "
                + "span, in a pattern with no lookaround that could put one there - the same "
                + "self-evident symptom `overlapped-skip-stale-slice` records. "
                + "`(?r)([^a]{2,4}(*SKIP)[a\\d])((?:[^\\d]++(*SKIP)\\s|\\ ))` over 'b0 0\\n A' has "
                + "upstream reporting (0, 5) with group 2 at (4, 6), and its own `search` and "
                + "`match` over (0, 5) are both None.\n"
                + "Judged in S36 on rows 1567 and 1863 (seed 4242) and 1439 (seed 7) of a 2000-row "
                + "`verbs` wave. Row 1439 was suspected of depending on CALL ORDER, because the wave "
                + "recorded three matches where a run on the row alone gave one; it does not - "
                + "`verbs` is recorded prefilter-free (tools/record-oracle.py) and the isolated run "
                + "was not. Recompiled prefilter-free it gives the same three matches every time, so "
                + "the recorder needs no per-row isolation. Ledger entry 5.\n"
                + "WIDENED BY S40a FROM OVERLAPPED TO EITHER SCAN, and the reason is this port rather "
                + "than a new upstream shape: S40a stopped the scanner carrying a moved slice, so a "
                + "PLAIN reversed `finditer` now ends where upstream keeps going too. It had agreed "
                + "before by reproducing the bug. Seed 20260913 row 116766, `verbs`, prefilter-free: "
                + "`regex.finditer(r'(?r)[[:digit:]]*(*SKIP)a$', '\\r\\nAAa\\r\\naaa', regex.M)` gives "
                + "(9, 10), (8, 9) and (7, 8), and upstream's own `(?m)$` holds at 1, 6 and 10 alone - "
                + "so the second and third matches need `$` where the subject has an 'a'. Making the "
                + "verb `(*PRUNE)`, or deleting it, leaves (9, 10) by itself, which is this port's "
                + "answer. Measured 2026-09-13, tools/probes/upstream-reversed-skip-scan-shapes.py.\n"
                + "THE LAST TWO ROWS OF THE FAMILY WERE CLASSIFIED BY S40d, and neither by widening a "
                + "tell - each got the fact it was missing.\n"
                + "THREE, the walk tell, for an overlapped row whose pattern has no `$` and no groups "
                + "at all (seed 4242 row 117071): `(?r)\\w{1,3}?(*SKIP).(?:\\p{L}(*SKIP)){2,3}` over "
                + "'_ ___\\U00010400\\U00010400\\U00010400', where upstream's scan reports codepoint "
                + "(3, 8), (3, 7) and (3, 6). Neither tell above exists on it, so what refutes it is "
                + "upstream's OWN reversed search over the truncated subject - `search(S, 0, 7)` and "
                + "`search(S, 0, 6)` are both None for matches it reports as ending at 7 and at 6 - "
                + "and that is legitimate HERE because the pattern holds no end-sensitive item for "
                + "the truncation to change the meaning of. The recorder refused every reversed "
                + "`anchoredScan` until S40d for exactly that reason; the refusal now reads the "
                + "pattern rather than the direction, so this row carries its own refutation, one "
                + "match against upstream's three, and the discriminator is the same whole-rendering "
                + "equality `overlapped-skip-stale-slice` uses.\n"
                + "FOUR, the `$` tell again, read on a `sub` row through the spans upstream replaced "
                + "at (seed 20260913 row 116388): `(?r)(?:\\d*?(*SKIP)\\U0001D518|a)$` over "
                + "'aa\\U0001D518\\U0001D518', where upstream replaces 3 times and this port once. A "
                + "sub outcome is a string and a count, so the row carried no position for a tell to "
                + "read; the recorder now records where a `(*SKIP)` substitution replaced, upstream's "
                + "three spans are codepoint (3, 4), (2, 3) and (1, 2), and the two beyond this "
                + "port's need `$` at 3 and at 2, where the subject has a character. Making the verb "
                + "`(*PRUNE)` leaves the (3, 4) replacement alone, which is this port's answer.\n"
                + "Both are measured in that probe.",
            PinnedBy: "BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_stops_where_"
                + "upstreams_own_extra_matches_refute_themselves",
            Example: _reversedExtraMatchRows,
            // Narrow on the two tells, and on the prefix: a port defect that shortens a match, moves
            // a span or changes a capture makes the prefix comparison fail and is reported. What is
            // left is a scan this port ended where upstream kept going.
            //
            // The hole, said out loud: a port defect that ended a reversed overlapped scan one match
            // early would be classified IF the match it dropped happened to carry a capture outside
            // itself or to end where a trailing `$` is false. The second is out of reach -
            // `TryMatchEndOfLine` reads TextEnd since S35 - and the first is out of reach only
            // because `CarriesACaptureOutsideItself` excludes the three constructs that can put a
            // capture there: a lookaround, and `\K`, which the S36 blind review found missing from
            // the list. Read that method's remarks before trusting this paragraph again.
            // CLOSED, for the rows that carry a walk, by S40d's third tell below: upstream's own
            // answer to each extra match is the reversed `anchoredScan` the recorder refused until
            // then. It still does refuse it where the pattern reads the end of the subject - moving
            // `endpos` truncates it and changes what `$` means, which is the very thing this entry
            // is about - so the hole survives on exactly the rows whose FIRST tell is the `$` one.
            //
            // EITHER SCAN SHAPE since S40a, and the two tells are what carries the widening: the
            // operation was never the thing doing the discriminating - the prefix comparison and the
            // per-extra-match refutation are - and a plain reversed `finditer` carries the same
            // moved slice as an overlapped one. Neither tell was loosened to let it in.
            //
            // A SUBSTITUTION ARM since S40d, resting on the recorder's `subMatches`, and the hole in
            // it said out loud: it compares COUNTS and not text, because a sub renders as one string
            // and this port's own replacement spans are not in the row. A port defect that replaced
            // the right number of times at the right places with a wrong expansion would be
            // classified here. What bounds it is that such a defect is in `Substitution` rather than
            // in the scan, so it would red every sub row of the `substitution` generator rather than
            // the handful this entry's four other conditions let through.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                && (
                    (
                        row.Operation is "finditer" or "finditer-overlapped"
                        && row.Expected is MatchesOutcome theirScan
                        && ours is MatchesOutcome ourScan
                        && (
                            OnlyExtraMatchesTheRowItselfRefutes(row, theirScan, ourScan)
                            || UpstreamsOwnWalkIsThisPortsScan(row, theirScan, ourScan)
                        )
                    )
                    || (
                        row.Operation is "sub" or "subf"
                        && row.Expected is SubOutcome theirSub
                        && ours is SubOutcome ourSub
                        && OnlyExtraReplacementsTheRowItselfRefutes(row, theirSub, ourSub)
                    )
                )
        ),
        new(
            Id: "overlapped-skip-missing-match-reversed",
            Reason: "Upstream bug, the same carried slice as the three entries above, and the THIRD "
                + "and last symptom it takes: under `(?r)` a `(*SKIP)` moves `slice_end` "
                + "(upstream/src/_regex.c:14545), nothing puts it back between the matches of one "
                + "scan, and here the bound the verb left is too SHORT rather than too long - so "
                + "upstream's next attempt runs in a view of the subject that cannot hold the match, "
                + "and its scan ends one match early. Where "
                + "`overlapped-skip-extra-match-reversed` has upstream reporting matches this port "
                + "does not, this has upstream LOSING one this port finds. The three entries are "
                + "disjoint by count: spans that moved with the counts equal, upstream longer, "
                + "upstream shorter.\n"
                + "FOUND AT A SEED NO SLICE HAD USED, which is VERIFICATION rule 7a doing its job "
                + "one level up: S40d drew seed 20260914 to re-run a control and the `verbs` "
                + "generator reddened on a row the three gate seeds never drew. It is not caused by "
                + "anything S40d changed - the engine is untouched by that slice, and the row's own "
                + "outcome is recorded before any second question is asked.\n"
                + "WHAT JUDGES IT IS THE WALK, and nothing softer. Upstream's own reversed search "
                + "asked one match at a time from a fresh state gives BOTH matches - "
                + "`search(S, 0, 4)` is (0, 4) and `search(S, 0, 3)` is (0, 2), which is this port's "
                + "answer - while its stateful scan reports (0, 4) alone. Making both verbs "
                + "`(*PRUNE)`, which moves no bound, or deleting them, gives upstream both matches "
                + "too. The walk is legitimate on this row because the pattern holds no `$`, no "
                + "`\\Z`, no word boundary and no lookahead, so truncating the subject changes the "
                + "meaning of nothing in it - the refusal in tools/record-oracle.py's "
                + "`_reads_the_end_of_the_subject` is what decides that. Measured 2026-09-13 on "
                + "regex 2026.7.19, `python tools/probes/upstream-reversed-skip-scan-shapes.py`. "
                + "Ledger entry 5.",
            PinnedBy: "BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_keeps_the_match_"
                + "upstreams_own_stepwise_door_still_finds",
            Example: """
            {"generator": "verbs", "pattern": "(?r)\\p{Lu}*(*SKIP)B(?P<g1>(?:\\D{2,4}(*SKIP)a|.))", "flags": 0, "namedLists": {}, "subject": "B_\ra", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 1, "length": 3, "captures": [[1, 3]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 4]}]}, "anchoredScan": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 1, "length": 3, "captures": [[1, 3]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 4]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 2]}]}
            """,
            // Keyed on the walk alone, which is the same discriminator `overlapped-skip-stale-slice`
            // uses on the forward side: upstream's stateful scanner must contradict upstream's own
            // stepwise matcher, and this port must agree with the matcher rendering for rendering -
            // every match, every group, every capture. A defect in this port's scan makes the second
            // condition false and is reported.
            //
            // The count comparison is only what keeps the three reversed entries from overlapping.
            // It is NOT the tell: a row where upstream is one short but the walk agrees with
            // upstream rather than with this port is a row this port got wrong, and it is reported.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                // Overlapped alone, because that is the only shape the recorder writes a walk for -
                // a non-overlapped step needs `must_advance` and no Python call carries it. The
                // sibling above lists both operations because its `$` and capture tells read the row
                // rather than a walk, and a plain reversed `finditer` carries the same moved slice.
                && row.Operation is "finditer-overlapped"
                && row.Expected is MatchesOutcome theirShortScan
                && ours is MatchesOutcome ourLongerScan
                && row.AnchoredScan is { } walked
                && theirShortScan.Matches.Count < ourLongerScan.Matches.Count
                && !string.Equals(theirShortScan.Describe(), walked.Describe(), StringComparison.Ordinal)
                && string.Equals(ourLongerScan.Describe(), walked.Describe(), StringComparison.Ordinal)
        ),
        new(
            Id: "end-of-line-reads-a-skip-moved-slice",
            Reason: "Upstream bug, the SAME `$` tell `overlapped-skip-extra-match-reversed` reads, "
                + "and a separate entry because on these two rows the tell is the WHOLE argument "
                + "rather than one of two, the operations are ones no entry here had reached, and on "
                + "row 38101 nothing crosses between matches at all. S52's third sitting, "
                + "2026-09-15.\n"
                + "WHAT UPSTREAM'S OWN SOURCE SAYS, and it is the cleanest statement of this defect "
                + "anywhere in this file. Upstream has eight predicates that ask whether a position "
                + "is at an edge of the text, and SEVEN of them read a TEXT bound - including `$`'s "
                + "own Unicode twin, so upstream's `$` disagrees with ITSELF in one file:\n"
                + "  try_match_START_OF_LINE        :7360  text_pos <= state->text_start\n"
                + "  try_match_START_OF_LINE_U      :7367  -> {ascii,unicode}_at_line_start,\n"
                + "                                        :902 / :1945, text_pos <= text_start\n"
                + "  try_match_START_OF_STRING      :7373  text_pos <= state->text_start\n"
                + "  try_match_END_OF_STRING        :7123  text_pos >= state->text_end\n"
                + "  try_match_END_OF_STRING_LINE   :7129  text_pos >= state->text_end\n"
                + "  try_match_END_OF_STRING_LINE_U :7136  text_pos >= state->text_end\n"
                + "  try_match_END_OF_LINE_U        :7117  -> {ascii,unicode}_at_line_end,\n"
                + "                                        :922 / :1966, text_pos >= text_end\n"
                + "  try_match_END_OF_LINE          :7110  text_pos >= state->SLICE_END  <- odd one\n"
                + "(`try_match_START_OF_WORD` and `try_match_END_OF_WORD` are word edges, not text "
                + "edges, and are not in the count.) `RE_OP_SKIP` under `(?r)` writes exactly the "
                + "field the odd one reads - `state->slice_end = state->text_pos` (:14553). So a "
                + "`(*SKIP)` in a reversed pattern makes `$` TRUE at the position the verb ran at, "
                + "wherever the subject actually ends. S35 made every assertion in this port read the "
                + "text bound; the DECISIONS entry of 2026-09-11 recorded the same `slice_end` "
                + "against `text_end` split between upstream's fast and slow paths without noticing "
                + "that every sibling predicate agrees with `text_end` and only `$` does not.\n"
                + "EVERY LINE NUMBER ABOVE IS AGAINST THE 2026.9.10 PIN and was re-read out of the "
                + "file. Older text in this repo cites :14545 and :14551 for the two `RE_OP_SKIP` "
                + "writes and :20903 for the scanner's step; today those are a TRACE call, a blank "
                + "line and a comment terminator. Reconciling them repo-wide is a maintenance job.\n"
                + "AND WHAT THE TWO ROWS SAY, each on its own, measured 2026-09-15 on regex 2026.9.10 "
                + "with tools/probes/upstream-skip-carried-slice-doors.py:\n"
                + "  row 24224  (?r)(?:\\s*?(*SKIP)\\W|[^a])(\\S{1,})$ over "
                + "'\\ufb00\\ufb00\\r\\n\\ufb00\\ufb00ss\\rS', MULTILINE, split\n"
                + "    upstream's own `$` is true at 3 and 10 ALONE, asked one anchored position at a "
                + "time\n"
                + "    upstream's split takes TWO separators, (8,10) and (3,8) - the second ENDS AT 8\n"
                + "    `$` written out as (?:(?=\\n)|(?!\\n|.))  one separator, this port's answer\n"
                + "    (*SKIP) -> (*PRUNE), or deleted          one separator, this port's answer\n"
                + "  row 38101  the same shape on a reversed `subf`, IGNORECASE|MULTILINE\n"
                + "    upstream's own `$` is true at 2 and 10 alone\n"
                + "    upstream finds one match, (0,5) - it ENDS AT 5\n"
                + "    `$` written out                          no match, this port's answer\n"
                + "    (*SKIP) -> (*PRUNE), or deleted          no match, this port's answer\n"
                + "    `$` written as (?w)$, the TWIN           no match, this port's answer\n"
                + "THE `(?w)` CONTROL IS THE SHARPEST ONE AND IT RUNS ON ONE ROW ONLY, and the reason "
                + "is narrower than it first looks. `(?w)` compiles `$` to `END_OF_LINE_U` "
                + "(regex/_regex_core.py:506-510), the twin that reads `text_end` - but it is NOT a "
                + "clean swap of one bound for another, because it also changes WHICH POSITIONS ARE "
                + "LINE ENDS, on both rows and in both directions:\n"
                + "  row 24224   `$` true at [3, 10]   `(?w)$` true at [2, 8, 10]   phantom end 8\n"
                + "  row 38101   `$` true at [2, 10]   `(?w)$` true at [1, 7, 10]   phantom end 5\n"
                + "So 'it moves the line ends' does not separate the two rows - it is true of both. "
                + "What separates them is whether it moves THE PHANTOM POSITION. On row 24224 the "
                + "phantom end 8 becomes a genuine `(?w)` line end, so a `(?w)` run that stops "
                + "reporting the separator cannot tell a bound that stopped being read from a line "
                + "end that started existing; on row 38101 the phantom end 5 is not a line end under "
                + "either spelling, so a `(?w)` run that answers None says the bound was the only "
                + "thing holding the match up. The probe derives that condition per row rather than "
                + "listing it, and prints both position lists either way.\n"
                + "WHY THE STEPWISE WALK IS NOT THE CONTROL HERE, said out loud because it looks like "
                + "one and points the wrong way. `search(subject, 0, 8)` on row 24224 gives (3, 8) on "
                + "EVERY line including the verb-free one, and `search(subject, 0, 5)` on row 38101 "
                + "gives (0, 5) on every line too - because passing an `endpos` sets `slice_end` to "
                + "it legitimately, which is the very bound this defect leaves stale. A walk that "
                + "moves `endpos` reproduces the bug instead of testing it. That is the refusal "
                + "`_reads_the_end_of_the_subject` already encodes in tools/record-oracle.py, and it "
                + "is why neither row carries an `anchoredScan` to key on.\n"
                + "THE TWO ROWS ARE DIFFERENT DISTANCES, which is the other reason they are here "
                + "rather than in the entry above. On row 24224 the stale bound crosses BETWEEN the "
                + "matches of one scan: upstream's own single `search` over the whole subject gives "
                + "(8, 10), the same as its `(*PRUNE)` line, and only the second separator diverges. "
                + "On row 38101 nothing crosses - a SINGLE `search(subject, 0, 10)` gives (0, 5) as "
                + "drawn and None with the verb spelled `(*PRUNE)`, so the bound a FAILED attempt "
                + "moved is read by a later attempt inside one call. The fix upstream needs is the "
                + "same either way, and it is not `init_match`: `$` should read `text_end`.\n"
                + "ROW 38101's RECORDED OUTCOME IS AN EXCEPTION, and that is incidental. Its template "
                + "holds `{0[-1]}`, which a match object answers with IndexError, so upstream raises "
                + "precisely when it finds a match and returns the subject unchanged when it does "
                + "not. The divergence is whether a match exists; the traceback is what upstream does "
                + "about one.\n"
                + "KEYED ON THE TWO ROWS AND ON THIS PORT'S ANSWER TO EACH, the owner's 2026-09-14 "
                + "ruling for this file. A predicate over 'the pattern ends in `$` and upstream's "
                + "extra span ends where `$` is false' already exists one entry up and is not "
                + "loosened here; what these rows lack is a SPAN for it to read - a split renders as "
                + "parts and an error renders as a traceback - and inventing one from the parts would "
                + "be a new inference rather than a recorded fact.\n"
                + "S52 SITTING 11 ADDED A THIRD ROW, seed 7 row 74413 of the 6000-row gate, and it "
                + "needed one line rather than an argument because it is row 38101's shape on a "
                + "second draw: a reversed `subf` whose replaced span ENDS AT 3 where upstream's own "
                + "`$` is true only at 4 and 5, with `$` spelled out, `(*PRUNE)` and the verb deleted "
                + "all answering the `sub 0` this port answers. It is the SECOND row on which the "
                + "`(?w)` control cannot run - `(?w)$` is true at [3, 5] here, so the phantom end 3 "
                + "is a line end the twin would create anyway, exactly the condition stated above "
                + "for row 24224. Measured 2026-09-15 on regex 2026.9.10, and re-runnable from the "
                + "committed tree with no gate run: "
                + "`python tools/probes/upstream-gate-drawn-skip-rows.py`.",
            PinnedBy: "BacktrackingVerbTests.A_reversed_split_of_a_skip_does_not_end_a_separator_where_"
                + "the_line_does_not_end and .A_reversed_substitution_of_a_skip_replaces_nothing_"
                + "where_the_line_does_not_end",
            Example: _endOfLineReadsMovedSliceRows,
            Applies: static (row, ours) =>
                _endOfLineReadsMovedSlice.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "skip-carried-slice-on-a-scan-with-no-walk",
            Reason: "Upstream bug, the same carried slice as the four `overlapped-skip-*` entries "
                + "above, on two rows that none of them can judge because the recorder writes no "
                + "`anchoredScan` for either - and for two DIFFERENT reasons, each already argued in "
                + "tools/record-oracle.py rather than invented here. S52's third sitting, "
                + "2026-09-15.\n"
                + "ROW 25854 IS A FORWARD, NON-OVERLAPPED `finditer`, and a non-overlapped walk needs "
                + "`must_advance`, which no Python call carries: after a zero-width match at p the "
                + "scanner re-attempts AT p with the flag set (upstream/src/_regex.c:20932), which "
                + "`search(subject, p)` cannot express and `search(subject, p + 1)` skips past. So "
                + "`_anchored_scan` is recorded for overlapped rows only, and this row has none.\n"
                + "ROW 38151 IS REVERSED AND OVERLAPPED, and would carry a walk but for its "
                + "lookahead: `_reads_the_end_of_the_subject` refuses `(?=` and `(?!` along with `$` "
                + "and the boundary escapes, because a reversed walk moves `endpos` and every one of "
                + "them changes meaning on a truncated subject. The refusal is deliberately crude and "
                + "costs a classification here, which is the direction that cannot hide a defect.\n"
                + "WHAT JUDGES THEM, measured 2026-09-15 on regex 2026.9.10 with "
                + "tools/probes/upstream-skip-carried-slice-doors.py, which computes the walk by hand "
                + "for exactly these two rows and says in its docstring why each is legitimate there:\n"
                + "  row 25854  (?b)(?:(?:\\W{2,}[^\\d]*?){1<=e<=2}(*SKIP)\\D|\\w)(\\p{Lu}{2,3}){0,0}\n"
                + "             over 'b\\r\\nabA\\n_', IGNORECASE, forward finditer\n"
                + "    upstream's scan            1 match,  (0,1)\n"
                + "    upstream's stepwise scan   3 matches, (0,1) (3,4) (4,5)  <- this port\n"
                + "    (*SKIP) -> (*PRUNE)        3 matches, the same three     <- this port\n"
                + "    verb deleted               5 matches, so both verbs really do prune two\n"
                + "  row 38151  (?r)\\p{ASCII}{1,3}(?![a](*SKIP))s(?:[^\\p{L}]*+(*SKIP)\\W|s)[^a]*\n"
                + "             (?<=\\W(*PRUNE))[A-Z] over 'aas\\rs\\r\\ns', reversed overlapped\n"
                + "    upstream's scan            1 match,  (0,8)\n"
                + "    upstream's stepwise scan   2 matches, (0,8) (0,5)        <- this port\n"
                + "    (*SKIP) -> (*PRUNE)        2 matches, the same two       <- this port\n"
                + "    verb deleted               2 matches, the same two\n"
                + "The walk is sound on row 25854 because no match in it is zero-width, and "
                + "`must_advance` is set only when one is - `state->must_advance = state->text_pos == "
                + "state->match_pos` (:20932) - so `search(subject, m.end())` IS the scanner's own "
                + "step on this row. It is sound on row 38151 because the walk takes upstream's own "
                + "reversed overlapped step, `state->text_pos = state->match_pos + step` with a step "
                + "of -1 under `(?r)` (:20927-20928), and the lookahead the refusal fires on sits at "
                + "a position both questions read identically. Neither argument generalises, which "
                + "is why neither was written into the recorder. (Older text in this repo cites "
                + ":20903 for that step; against the 2026.9.10 pin that line is a comment "
                + "terminator.)\n"
                + "KEYED ON THE TWO ROWS AND ON THIS PORT'S ANSWER TO EACH. The recorder's two "
                + "refusals are correct and stay: widening `_anchored_scan` to a forward "
                + "non-overlapped row would write a walk that is wrong the moment a row draws a "
                + "zero-width match, and S34's blind review built that row and watched the list "
                + "absorb an engine mutation because of it. Widening this entry means judging another "
                + "row with the probe and adding it.",
            PinnedBy: "BacktrackingVerbTests.A_forward_scan_of_a_skip_keeps_the_matches_upstreams_own_"
                + "stepwise_door_still_finds and .A_reversed_overlapped_scan_behind_a_lookahead_keeps_"
                + "its_second_match",
            Example: _skipCarriedSliceNoWalkRows,
            Applies: static (row, ours) =>
                _skipCarriedSliceNoWalk.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "reversed-skip-invents-a-match",
            Reason: "Upstream bug, the same carried `slice_end` as the `overlapped-skip-*` entries "
                + "above and a symptom none of them records: not a span that moved, not a match too "
                + "many in a scan, but a SINGLE `search` finding a complete match where the same "
                + "search without the verb finds nothing at all. Seed 20260915 row 74889 of S52's "
                + "6000-row gate, judged in sitting 10, 2026-09-15, regex 2026.9.10.\n"
                + "THE ROW. `(?r)^(?:[^a]+(*SKIP)[^a-f]|\\p{Lu})(?P<g1>\\D)(?:(?(1)(?=(?P>g1))\\w))*$` "
                + "searched with `partial=True` over '\\U0001F600\\uFB03 _\\U00010400a\\uFB03\\U00010400' "
                + "(IGNORECASE and MULTILINE). Upstream answers a COMPLETE match at codepoints (0, 6) "
                + "with g1 at (5, 6); this port answers the zero-width partial at (0, 0).\n"
                + "THE CONTRADICTION NEEDS NO READING, and it is the sharpest this file has. A verb "
                + "whose entire job is to REMOVE backtracking positions cannot create a match that "
                + "does not exist without it - pruning only ever takes candidates away. Upstream's "
                + "own three spellings of the same question, all measured on the same call:\n"
                + "  `(*SKIP)`, partial=True    complete (0, 6), g1 (5, 6)   <- the drawn answer\n"
                + "  `(*PRUNE)`, partial=True   zero-width partial (0, 0)    <- THIS PORT's answer\n"
                + "  verb deleted, partial=True zero-width partial (0, 0)    <- THIS PORT's answer\n"
                + "`(*PRUNE)` is the same opcode body but for the two lines `(*SKIP)` has first, which "
                + "are the ones that move the slice (upstream/src/_regex.c:14553 reversed, :14555 "
                + "forward), so it prunes identically and moves no bound. The complete match exists in "
                + "upstream when and only when a bound was moved.\n"
                + "AND IT IS NOT THE PARTIAL MACHINERY, which is what keeps this row out of "
                + "`partial-retry-reversed-slice` and `search-start-partial`. Asked with `partial=True` "
                + "DROPPED, upstream still answers the complete match and this port still answers no "
                + "match - while the verb-free non-partial spelling is None on BOTH sides. So the "
                + "divergence is in the ordinary reversed search and the partial is only how this port "
                + "renders having found nothing.\n"
                + "THE MECHANISM, and where it has been seen before: under `(?r)` a `(*SKIP)` moves "
                + "`slice_end` mid-attempt and nothing puts it back, so a LATER ANCHOR of the same "
                + "`search` runs in a view of the subject that ends where a FAILED earlier attempt "
                + "left the bound. That is the carry-over the four `overlapped-skip-*` entries record "
                + "between the matches of a scan, happening inside one call instead - which "
                + "`end-of-line-reads-a-skip-moved-slice` already records on its row 38101 (of the 2000-row wave "
                + "it was judged from, and held as the second row of `_endOfLineReadsMovedSliceRows` "
                + "above) - a `subf` whose "
                + "recorded outcome is an IndexError, to which that entry puts a single "
                + "`search(subject, 0, 10)` as a DOOR and gets a match as drawn and None with the "
                + "verb spelled `(*PRUNE)`.\n"
                + "KEYED ON THE ROW AND ON THE ROW'S OWN RECORDED `pruneOutcome`, not on a judged "
                + "answer string. The row set is the owner's 2026-09-14 ruling for this file; the "
                + "`pruneOutcome` half is the live control, so if upstream ever makes `(*PRUNE)` "
                + "answer something else, or this port stops agreeing with it, the row stops being "
                + "classified and is reported. Widening this entry means judging another row with the "
                + "probe and adding it. Re-runnable: "
                + "tools/probes/upstream-reversed-skip-invents-a-match.py.",
            PinnedBy: "BacktrackingVerbTests.A_reversed_search_of_a_skip_finds_nothing_where_pruning_"
                + "alone_finds_nothing",
            Example: _reversedSkipInventedMatchRows,
            // Narrow twice over. The row set fixes WHICH question this can answer, and the
            // `pruneOutcome` equality makes the classification depend on a fact the RUN recorded
            // rather than on a string judged once: this port has to land on upstream's own
            // pruning-equivalent answer, group for group and partial flag included, or the row is
            // reported. A port defect that merely lost the match would have to lose it into exactly
            // upstream's `(*PRUNE)` rendering to be hidden here.
            Applies: static (row, ours) =>
                _reversedSkipInventedMatch.Contains(Question(row))
                && row.PruneOutcome is { } pruned
                && !string.Equals(row.Expected.Describe(), pruned.Describe(), StringComparison.Ordinal)
                && string.Equals(ours.Describe(), pruned.Describe(), StringComparison.Ordinal)
        ),
        new(
            Id: "group-call-loses-the-match",
            Reason: "Upstream bug, NOT fixed by issue 614 and still present in 2026.9.10 (the wave "
                + "rows replayed against .venvs/regex-2026.9.10 on 2026-09-12 and 2026-09-13, "
                + "identical every time). "
                + "The same precondition as `group-call-direction` above - a group reached by a call "
                + "from a lookaround running the other way round from the pattern - and a different "
                + "symptom: upstream does not record a bad capture, it LOSES the match. This is the "
                + "family S30 found and pinned as a KNOWN DIVERGENCE; S37 judged five more rows of it "
                + "and gave it an entry so the composed wave can be green at 6000 rows.\n"
                + "THE JUDGEMENT NEEDS NO SECOND ENGINE, because upstream contradicts itself on each "
                + "row: DELETING THE PIECE THAT HOLDS THE CALL GIVES UPSTREAM A MATCH IT REFUSED WITH "
                + "THE PIECE PRESENT, and in every one of these rows that piece can match zero-width, "
                + "so it cannot remove a match. Three of the five say so outright - a `??`, a `?` and "
                + "a `*` can always take zero iterations - and the other two say it through "
                + "upstream's own answer to the call-free copy, whose match is no WIDER than the "
                + "pattern without the piece, which is what proves the conditional inside it matched "
                + "empty. Re-runnable: tools/probes/upstream-group-call-loses-matches.py.\n"
                + "  \\b(?(?![\\w\\s])[[:digit:]])(\\w)(?P<g2>[^\\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*\n"
                + "  over 'İİ\\nİİﬁﬁ ' overlapped: upstream (0, 4) alone; drop the trailing `(?:...)*` "
                + "and upstream gives (0, 4) AND (3, 7), which is this port's answer.\n"
                + "MINIMISATION FAILED AND THE FAILURE IS THE FINDING. Shrinking each row while "
                + "upstream kept contradicting itself produced tiny patterns - "
                + "`(?P<g1>a)((?<!(?&g1)))*` over 'a', `(?r)(?P<g1>[A])((?(?=(?&g1))S))` over 'A' - "
                + "on which THIS PORT ANSWERS WHAT UPSTREAM ANSWERS. So the divergence is not 'a call "
                + "through an opposite-direction lookaround' on its own; something about the wave's "
                + "longer shapes is also needed, and what that is remains unknown. The rows are "
                + "therefore listed whole. Phase 6's open-issue sweep owns the report, and the ledger "
                + "entry has to carry this caveat with it.",
            PinnedBy: "GroupCallTests.A_group_called_from_a_lookbehind_with_anything_after_it_matches_"
                + "here_and_not_upstream and .A_zero_width_piece_holding_a_group_call_cannot_remove_a_"
                + "match_here",
            // The judged rows are Example rows TOO, and the blind review is what caught their being
            // left out. A predicate cannot notice a port that has stopped diverging - the note below
            // says so - but neither can a judged row that nothing replays: a stale
            // _groupCallLostMatchJudgedOurs would have silently stopped classifying its row instead
            // of reddening the run, which is the exact failure this list exists to prevent. Both
            // sibling row-keyed entries make their judged rows the Example for this reason.
            Example: _groupCallLostMatchRows + "\n" + _groupCallLostMatchJudgedRows,
            // Narrow on three counts. The pattern must call a group AND hold a lookaround running
            // the other way from itself, which is the defect's precondition rather than a symptom.
            // Upstream must have found STRICTLY LESS than this port. And everything upstream did
            // find must render identically here - every span, every capture, every `lastindex` -
            // so a port defect that shortens a match, moves a span or drops a capture is reported.
            //
            // The hole, said out loud, and it is a real one: a port defect that INVENTS a match in a
            // pattern of this shape would be classified rather than reported. Closing it needs
            // upstream's own answer to the same question with the call resolved, and there is no
            // such answer to record - inlining the called body is not semantics-preserving, which
            // this slice measured rather than assumed: on the minimised rows above the inlined copy
            // differs from the call copy for BOTH engines.
            //
            // The control that does bite is S37-A, and on the OTHER alarm. It stops the parser
            // recompiling a called group for the caller's direction, which is upstream's own defect,
            // and the port then agrees with upstream where it used to diverge: `expected` collapses
            // to 0 and `Every_expected_divergence_still_diverges` fails outright with
            // "group-call-direction: an entry must account for its own example row 1". A predicate
            // cannot notice a port that has stopped diverging; the example rows can, and do.
            Applies: static (row, ours) =>
                (
                    HasGroupCall(row.Pattern)
                    && CallsThroughAnOppositeDirectionLookaround(row)
                    && UpstreamFoundStrictlyLess(row, ours)
                )
                // ...plus the rows the predicate cannot reach, judged one at a time. See
                // _groupCallLostMatchJudgedRows for why widening the predicate instead is the worse
                // trade.
                || (
                    _groupCallLostMatchJudged.TryGetValue(Question(row), out string? judged)
                    && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
                )
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
        new(
            Id: "partial-retry-reversed-slice",
            Reason: "Port right, and NEW IN S40b - this row diverges BECAUSE of that slice's fix, "
                + "which is the honest way to say it. A `partial` search runs two passes over one "
                + "match attempt (upstream do_match, upstream/src/_regex.c:18160): a non-partial one, "
                + "then a partial one from the same `text_pos`. Upstream restores `text_pos` and "
                + "nothing else, so a `(*SKIP)` that moved `slice_end` under `(?r)` (:14551) leaves "
                + "it moved for the second pass, whose search retry then skips anchors outside it. "
                + "S40b restores both slice bounds here; upstream still does not.\n"
                + "WHAT JUDGES IT IS UPSTREAM CONTRADICTING ITSELF, not a preference. A reversed "
                + "search is anchored by its END and tries the highest endpos first, so the answer it "
                + "owes is the first anchor that matches. On all three rows upstream's search answers "
                + "the zero-width partial at (0, 0) - the LAST anchor it would try - while its own "
                + "match at a higher endpos answers what this port does. Measured 2026-09-13 on "
                + "regex 2026.7.19, tools/probes/upstream-partial-retry-slice-restore.py, row 1 of "
                + "three and the other two identical in shape:\n"
                + "  search(partial=True)                   (0, 0) partial   <- upstream\n"
                + "  match(endpos=1, partial=True)          (0, 1) partial   <- upstream's own matcher\n"
                + "  verb deleted,     search(partial=True) (0, 1) partial\n"
                + "  verb -> (*PRUNE), search(partial=True) (0, 1) partial\n"
                + "`(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, so "
                + "the last line is what makes the bound move the cause rather than the pattern's "
                + "meaning - the same argument `search-start-partial` above rests on. This port "
                + "answers upstream's own matcher's answer on every row.\n"
                + "KEYED ON ROWS, like `bounded-lazy-repeat-partial` and `search-start-partial`'s "
                + "second arm above, and for the same reason: any predicate over 'upstream answered a "
                + "shorter partial on a reversed verb row' also describes the defects this port might "
                + "still have there, and this entry's own family is one this port had until S40b. "
                + "Widening means judging another row with the probe and adding it, not loosening a "
                + "condition. Phase 7 must not import upstream's answer here along with the prefilter.",
            PinnedBy: "PartialMatchingTests.A_reversed_skip_does_not_move_the_slice_end_the_partial_" + "pass_searches",
            Example: _partialRetryReversedRows,
            Applies: static (row, ours) =>
                _partialRetryReversed.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "partial-retry-carried-slice-forward",
            Reason: "Port right, and the entry above's mechanism in a pattern that runs LEFT TO "
                + "RIGHT. Split by direction rather than widened, which is the convention this file "
                + "already uses for `overlapped-skip-stale-slice` and its `-reversed` twin: the "
                + "bound a `(*SKIP)` moves is `slice_start` forwards and `slice_end` under `(?r)` "
                + "(upstream/src/_regex.c:14555 forwards, :14553 reversed), the symptoms differ "
                + "accordingly, and one entry "
                + "spanning both would have to state each half separately anyway.\n"
                + "THE SAME TWO PASSES. A `partial` search runs a non-partial pass and then a "
                + "partial one from the same `text_pos` (upstream do_match, the save at :18159). "
                + "Upstream "
                + "restores `text_pos` and nothing else, so a bound the verb moved in the first pass "
                + "is still moved in the second. S40b restores both bounds here; upstream does not.\n"
                + "WHAT THE MOVED BOUND COSTS IS DIFFERENT THIS WAY ROUND, and it is worth saying "
                + "because it is why the reversed entry's own argument does not transfer. The two "
                + "engines agree on the SPAN on ROW 1 - both answer a partial at codepoints (1, 4) - "
                + "and "
                + "differ in which ALTERNATIVE the partial pass could still enter, and therefore in "
                + "which error was spent and which group captured. Upstream takes the `[^a]` branch "
                + "and charges an insertion at 3, with every group unset. This port takes the branch "
                + "the `(*SKIP)` sits in, charges a substitution at 1, and captures (3, 4).\n"
                + "THAT IS NOT THE WHOLE FAMILY ANY MORE, and the sentence above said it was until "
                + "S52's eighth sitting added rows 4 and 5 from the 6000-row gate. On those two the "
                + "spans DIFFER: upstream answers a zero-width partial at the far end of what it "
                + "searched, codepoints (5, 5) and (7, 7), where its own `(*PRUNE)` and verb-free "
                + "spellings answer the wider (3, 5) and (5, 7) that this port answers, group and "
                + "all. So forwards the moved `slice_start` can cost a START as well as an "
                + "alternative - which is the reversed entry's symptom on a left-to-right pattern, "
                + "and the one thing the split-by-direction convention did not predict. The "
                + "mechanism and the control are unchanged; only the claim about what the symptom "
                + "looks like was too narrow.\n"
                + "WHAT JUDGES IT IS THE `(*PRUNE)` CONTROL, the argument `search-start-partial` and "
                + "`partial-retry-reversed-slice` both rest on, and here it is the whole of the "
                + "evidence rather than a corroboration:\n"
                + "  as the wave drew it        (1,4)P insertion at 3, no captures  <- upstream\n"
                + "  first verb -> (*PRUNE)     (1,4)P substitution at 1, (3,4)     <- this port's\n"
                + "  first verb deleted         (1,4)P substitution at 1, (3,4)     <- this port's\n"
                + "  second verb (*PRUNE) gone  (1,4)P insertion at 3, no captures  <- unchanged\n"
                + "`(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, so "
                + "the bound move is the cause rather than the pattern's meaning; and the last line "
                + "says the OTHER verb in the pattern is not involved, which is a control this "
                + "family has not had before. Measured 2026-09-13 on regex 2026.7.19, "
                + "tools/probes/upstream-skip-carried-slice-forward.py.\n"
                + "TWO MORE ROWS, AND A TELL THIS FAMILY DID NOT HAVE, added by S52's third sitting "
                + "on 2026-09-15 from the three-seed 2000-row wave of commit 407c0cb. On seed 7 rows "
                + "24018 and 24737 the second pass does not merely enter a different alternative - it "
                + "returns a match that IS NOT PARTIAL, and upstream's own non-partial call to the "
                + "same compiled pattern over the same subject returns None:\n"
                + "  row 24018  \\L<w1>{e<=2}(?:\\D(*SKIP)\\S|\\p{Lu}) over '\\xdf\\xdf'\n"
                + "    match(partial=True)  (0,2) NOT partial, two deletions   <- upstream\n"
                + "    match()              None                               <- upstream\n"
                + "    (*PRUNE), partial    (0,2) PARTIAL, two substitutions   <- this port\n"
                + "    verb deleted, either (0,2) NOT partial, two deletions\n"
                + "  row 24737  the same shape on a `search`\n"
                + "    search(partial=True) (5,5) NOT partial, one deletion    <- upstream\n"
                + "    search()             None                               <- upstream\n"
                + "    (*PRUNE), partial    (3,7) PARTIAL                      <- this port\n"
                + "`partial=True` is documented to ALSO allow a partial match (upstream/README.rst); "
                + "it cannot conjure a complete one the same engine denies without it, so upstream "
                + "refutes itself on each row in two calls, with no model of a scan needed. What the "
                + "rows add beyond that is the pruning: with no partial asked for, the `(*SKIP)` and "
                + "the `(*PRUNE)` line AGREE (both None) and only the verb-free line matches, so both "
                + "verbs do prune the match in one pass and the `(*SKIP)` line gets it back in the "
                + "other - and what it gets back is the VERB-FREE answer, character for character. "
                + "ROW 24018 CLOSES THE SCAN QUESTION for this family: a `match` is a single attempt, "
                + "so there is no next attempt for `(*SKIP)` to move the start of, and the two verbs "
                + "MUST agree within it. Measured 2026-09-15 on regex 2026.9.10, "
                + "tools/probes/upstream-skip-carried-slice-doors.py.\n"
                + "KEYED ON ITS ROWS, like every sibling named above. No predicate over 'the two "
                + "engines agree on the span and disagree on which error they spent' is safe here - "
                + "that is also what a genuine fuzzy-path defect looks like, and this port has "
                + "shipped one in Phase 5 already (S43's own SaveBestMatch fix, which reported the "
                + "errors a POSIX fuzzy match spent as zero). Nor is the new tell safe as a "
                + "predicate: 'upstream answered a non-partial match where its own non-partial call "
                + "answers None' is also exactly what `bestmatch-loses-a-partial` looks like from the "
                + "other side. Widening means judging another row with the probe and adding it. "
                + "Phase 7 must not import upstream's answer here.",
            PinnedBy: "PartialMatchingTests.A_forward_skip_does_not_move_the_slice_start_the_partial_"
                + "pass_searches, .A_partial_match_of_a_skip_is_not_the_verb_free_answer, "
                + ".A_partial_search_of_a_skip_is_not_the_verb_free_answer and "
                + ".A_forward_skip_does_not_cost_the_partial_its_start",
            Example: _partialRetryForwardRows,
            Applies: static (row, ours) =>
                _partialRetryForward.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "skip-blocks-a-repeat-retreat-partial",
            Reason: "Port right, and this is a REGRESSION UPSTREAM INTRODUCED between the old pin "
                + "and the new one - the only entry here that upstream did not have when S43 closed. "
                + "Found by S44's own sync gate: one row of the 378,000 in the three-seed 6000-row "
                + "wave changed answer when the pin moved from 2026.8.12 to 2026.9.10, row 98050 of "
                + "seed 20260913.\n"
                + "THE CAUSE IS ISSUE 613'S OWN FIX. Commit b77694a clamps the GREEDY_REPEAT_ONE "
                + "retreat limit down to the current position (upstream/src/_regex.c:15863 and "
                + ":15869), which stops the runaway retreat that made `.?x(?>a(*SKIP)z)` hang - and "
                + "also stops the ONE legitimate retreat step some matches need, once a `(*SKIP)` to "
                + "the right of the repeat has moved `slice_start` above it. This port ports both "
                + "clamps and does NOT lose the match, because S40b restores the slice bounds before "
                + "the partial pass and upstream does not; the two changes meet here.\n"
                + "MINIMISED TO THREE ASCII CHARACTERS AND NO FLAGS, which is the strongest "
                + "reproduction any entry in this file has:\n"
                + "  regex.compile(r'(a+)\\1x(*SKIP)b').search('aax', partial=True)\n"
                + "  2026.7.19  (0, 3) partial, group 1 (0, 1)      <- this port's answer\n"
                + "  2026.9.10  (3, 3) partial, group 1 unset\n"
                + "The match upstream lost is plainly reachable: `(a+)` takes 'aa', `\\1` cannot "
                + "match 'aa' at 2, the repeat retreats to 'a', `\\1` matches 'a' at 1, 'x' matches "
                + "at 2, and 'b' runs off the end of the subject - which is what a partial match is.\n"
                + "FOUR THINGS JUDGE IT, and the third is on its own decisive:\n"
                + "  1. `(*PRUNE)` in place of `(*SKIP)` - the same backtracking pruned, no bound "
                + "moved - gives (0, 3) on BOTH releases, so the bound move is the cause rather than "
                + "the pattern's meaning. Same argument as `search-start-partial` and both "
                + "`partial-retry-*` entries above.\n"
                + "  2. The verb deleted gives (0, 3) on both.\n"
                + "  3. UPSTREAM 2026.9.10 CONTRADICTS ITSELF. The same pattern over 'aaxb', where "
                + "the match COMPLETES, answers (0, 4) with group 1 at (0, 1) - which needs the "
                + "identical `a+` retreat it just refused. An engine that can take the retreat to "
                + "finish a match cannot consistently refuse it to report a partial one.\n"
                + "  4. PCRE2 10.47, run rather than read: PARTIAL (0, 3) on the minimised row, and "
                + "(0, 3) on both controls. A second engine agrees with this port and with upstream's "
                + "own previous release.\n"
                + "All measured 2026-09-13, tools/probes/upstream-skip-blocks-a-repeat-retreat.py "
                + "(and `--pcre2` for the fourth). Ledger entry 15; NOT filed, per the owner's rule.\n"
                + "KEYED ON ITS ROWS, like `partial-retry-carried-slice-forward` above and for the "
                + "same reason: 'upstream answered the zero-width partial at the end and this port "
                + "answered a real one earlier' is also what a genuine partial-search defect in this "
                + "port looks like, and this port has shipped one (S40b's). Two rows, because the "
                + "wave's row is what the alarm must keep testing and the minimised row is what a "
                + "reader can check by hand. Widening means judging another row with the probe.",
            PinnedBy: "PartialMatchingTests.A_skip_does_not_block_the_repeat_retreat_a_partial_needs",
            Example: _skipBlocksRetreatRows,
            Applies: static (row, ours) =>
                _skipBlocksRetreat.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "bestmatch-walk-truncated-by-a-skip",
            Reason: "UPSTREAM IS WRONG AND THIS PORT DIVERGES ON PURPOSE - ledger entry 5's SIXTH "
                + "door, fixed here by S48 because the port shared it. Every door above this one "
                + "carries the stale slice from one MATCH to the next, or between the two passes of "
                + "one match; this one carries it between the CANDIDATES of a single `(?b)` match.\n"
                + "THE MECHANISM, READ OFF THE SOURCE. `do_best_fuzzy_match` walks `start_pos` across "
                + "the slice, one `basic_match` per candidate, and its loop guard holds `start_pos` "
                + "between `state->slice_start` and `state->slice_end` (upstream/src/_regex.c:17625). "
                + "`RE_OP_SKIP` assigns `slice_start` mid-attempt (:14555, or `slice_end` under `(?r)`, "
                + ":14553) and nothing puts it back - `init_match` (:3404), which this walk calls once "
                + "per candidate, resets the stacks, the groups and the guards but not the slice. "
                + "`start_pos` is then set to `state->match_pos`, the START of the match the candidate "
                + "just found, so a verb that consumed anything leaves `slice_start` ABOVE it and the "
                + "guard is false on the next turn. THE WALK ENDS ON ITS FIRST SUCCESSFUL CANDIDATE.\n"
                + "WHAT JUDGES IT IS THE VERB'S OWN DEFINITION, not a preference between rankings. "
                + "`(*SKIP)` sets a skip point, and what the skip point forbids is a later attempt "
                + "BELOW it (PCRE2 pcre2pattern, 'Verbs that act after backtracking'). On row 1 the "
                + "skip point is 1 and the candidate the walk never reaches starts at 2, which the "
                + "verb permits outright. `(?b)` then promises the fewest errors among the matches "
                + "that exist, and the match it loses is PERFECT.\n"
                + "AND UPSTREAM CONTRADICTS ITSELF ON THE SAME COMPILED PATTERN, which is the form "
                + "`bestmatch-loses-a-partial` and both `partial-retry-*` entries rest on. Row 1, "
                + "measured 2026-09-14 on regex 2026.9.10, "
                + "tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py:\n"
                + "  (?b)(?:a(*SKIP)b){e<=1} over 'axab'\n"
                + "    search .................  (0, 2) one substitution   <- upstream\n"
                + "    ...the same object....... match(2)  (2, 4) NO errors\n"
                + "    (*PRUNE) in its place ..  search    (2, 4) NO errors\n"
                + "    the verb deleted .......  search    (2, 4) NO errors\n"
                + "`(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, so "
                + "the third line is what makes the moved bound the cause rather than the pattern's "
                + "meaning - the same control `search-start-partial` and `partial-retry-carried-slice-"
                + "forward` rest on. NO SECOND ENGINE IS AVAILABLE and none is needed: PCRE2 has no "
                + "fuzzy matching at all (tools/probes/pcre2-has-no-fuzzy-matching.py), and a search "
                + "that reports a worse match than its own anchored door finds is wrong on `(?b)`'s "
                + "own definition.\n"
                + "WHAT THE FIX IS, AND WHERE IT IS NOT. Matcher.DoBestFuzzyMatch restores the "
                + "caller's slice before each candidate, in the walk and in the second pass. It is "
                + "NOT hoisted into `InitMatch`, where ledger entry 5's note puts upstream's version "
                + "of the fix: `DoEnhancedFuzzyMatch` and this function's own widened-slice fallback "
                + "narrow the slice DELIBERATELY and then call it, and a reset there would throw "
                + "their narrowing away. The restore is once per candidate, so a verb still moves the "
                + "slice for the rest of the attempt it fired in - pinned separately by "
                + "`Bestmatch_still_lets_a_skip_prune_a_candidates_own_alternatives`, on a row where "
                + "the pruning decides the answer and the moved bound does not, and which is "
                + "upstream's own answer rather than a divergence.\n"
                + "KEYED ON ITS ROWS, like every sibling above, and here the reason is sharper than "
                + "usual: 'this port answered a better fuzzy match than upstream' is exactly what a "
                + "ranking defect in this port's own cost walk would also look like, and this port "
                + "has shipped two of those in Phase 5 (S42's two hangs). Widening means judging "
                + "another row with the probe and adding it, not loosening a condition. Phase 7 must "
                + "not import upstream's answer here along with the prefilter.\n"
                + "S52 SITTING 11 ADDED ROWS 6 AND 7, the first drawn rows of this family rather "
                + "than authored ones, and what classifies each is one line - the `(?b)`-free pair. "
                + "Measured 2026-09-15 on regex 2026.9.10, the wave rows themselves, prefilter on as "
                + "`interactions` is recorded, and re-runnable from the committed tree with no gate "
                + "run: `python tools/probes/upstream-gate-drawn-skip-rows.py`, which reads the four "
                + "drawn rows out of `tools/probes/gate-drawn-skip-rows.jsonl` beside it:\n"
                + "  row 6  seed 4242 row 76778, a REVERSED anchored `match`, `(?b)` + `(*SKIP)`\n"
                + "    (?b) + (*SKIP)   (0, 8) one deletion     <- upstream, and (?b) improved NOTHING\n"
                + "    (?b) + (*PRUNE)  (1, 8) NO errors        <- ours\n"
                + "    no (?b), either verb  (0, 8) one deletion\n"
                + "  row 7  seed 4242 row 77119, a forward overlapped `finditer`, `(?b)(?e)` + `(*SKIP)`\n"
                + "    (?b)(?e) + (*SKIP)   NO MATCH AT ALL     <- upstream\n"
                + "    (?b)(?e) + (*PRUNE)  (0, 5) one substitution   <- ours\n"
                + "    verb deleted, and (?e) + (*SKIP) with (?b) gone, both find it too\n"
                + "THE `(?b)`-FREE PAIR IS WHAT MAKES THE VERB'S PRUNING INNOCENT on both. Without "
                + "`(?b)` the two verbs answer IDENTICALLY - so the pruning `(*SKIP)` does is not "
                + "what moves either row - and with `(?b)` they do not. A verb that only matters "
                + "when a `(?b)` walk is running is a verb acting on the walk, which is this entry. "
                + "Row 6 is the sharpest statement of the family in the file: `(?b)`'s whole job is "
                + "to improve on the non-best answer, and with the verb present it improves by "
                + "nothing at all.\n"
                + "A THIRD ROW WAS RULED OUT BY THIS ENTRY'S OWN DOOR and is deliberately not here: "
                + "seed 7 row 76160. Its `(*PRUNE)` spelling does not terminate - 5 seconds at "
                + "sitting 8, 300 at sitting 11 and 300 again at sitting 13 - and "
                + "the anchored door that stands in for it points the WRONG WAY - upstream's own "
                + "`match(4, 7)` on the drawn object costs (1, 0, 1) where this port answers (4, 7) "
                + "with no errors, so the candidate the walk is supposed to have missed is worse, "
                + "not better. It is left on the gate unjudged rather than filed on a resemblance.",
            PinnedBy: "FuzzyBestMatchTests.Bestmatch_looks_past_the_candidate_whose_own_skip_moved_" + "the_slice",
            Example: _bestmatchWalkTruncatedRows,
            Applies: static (row, ours) =>
                _bestmatchWalkTruncated.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "enhancematch-ranks-by-cost",
            Reason: "PORT DELIBERATELY DIFFERENT, by owner decision rather than by a verdict about "
                + "who is right about an edge case (DECISIONS 2026-09-12). `ENHANCEMATCH` re-runs a "
                + "fuzzy match inside its own span with a tighter budget until the fit stops "
                + "improving, and upstream ranks those runs by error COUNT alone - `better = "
                + "state->total_errors < fewest_errors`, upstream/src/_regex.c:17930, and it never "
                + "consults `total_cost` (:9649). That is upstream's open issue 470. This port ranks "
                + "by cost, ties by fewer errors, then earliest, through Matcher.IsBetterFuzzyMatch. "
                + "`BESTMATCH` reaches the same rule by a different route and has its own entry "
                + "below, because its budget rather than its tie-break is what had to change.\n"
                + "WITH UNIT COSTS THE TWO RULES AGREE, which is why no ported test changes and why "
                + "this predicate insists on a cost equation whose coefficients are not all equal: "
                + "cost is then a multiple of the error count and the orderings cannot differ. The "
                + "example row is `(?e)(?:x|xyq){1i+9s+9d<=20}` over 'xyz'. The first run takes the "
                + "'x' branch with two insertions - two errors costing 1 each - and the second, held "
                + "to one error, takes 'xyq' with one substitution costing 9. Upstream keeps the "
                + "substitution (1 < 2); this port keeps the insertions (2 < 9). Measured on regex "
                + "2026.7.19, tools/probes/upstream-enhancematch.py.\n"
                + "NARROW ON THE DIFFERENCE, not just on the flag. The two answers must agree on "
                + "everything except which errors were spent, and this port's must be CHEAPER while "
                + "using AT LEAST AS MANY errors. That direction is forced: upstream keeps the last "
                + "run of the improvement chain, which is the one with fewest errors, and this port "
                + "walks the same chain and keeps the cheapest run on it.\n"
                + "IT DOES NOT COVER THE WHOLE FAMILY, AND THAT IS DELIBERATE. A cheaper match can "
                + "also be a match of a DIFFERENT SPAN, and then the `sub`, `split` and `finditer` "
                + "counts that follow from it differ too. Those rows are REPORTED rather than "
                + "classified, because 'this port answered a different span' is also exactly what a "
                + "real engine defect looks like, and no predicate separates the two from the row "
                + "alone. Measured on a 2500-row wave of `(?e)` alternation bodies with non-unit cost "
                + "equations (S41's blind review, seed 777): 43 rows of the family, 12 of them "
                + "same-span and classified here, 31 span-different and reported, and NONE where this "
                + "port's answer is dearer than upstream's or uses fewer errors. Widening this entry "
                + "to swallow the other 31 would buy a green wave with the one property that makes "
                + "the list worth keeping.\n"
                + "NO COMMITTED GENERATOR DRAWS IT, AND S42 MADE THAT DELIBERATE RATHER THAN "
                + "ACCIDENTAL. It used to be accidental: the fuzzy generator builds a section by "
                + "concatenating atoms and never by alternation, so the improvement loop rarely had "
                + "two candidates of different shape to choose between, and 435 rows carrying both "
                + "`(?e)` and a non-unit cost equation across three 2000-row seeds produced none. "
                + "Adding `(?b)` to the generator ended that - 4 rows of 2000 diverged at each of "
                + "seeds 7, 4242 and 20260913, all twelve a weighted equation under a ranking flag, "
                + "all twelve this port answering more cheaply, and eight of the twelve "
                + "span-different and so unclassifiable. `record-oracle.py`'s `_has_weighted_cost` "
                + "now refuses to put `(?e)` or `(?b)` on a row whose equation prices the three "
                + "error kinds differently, because an oracle cannot judge a comparison the two "
                + "engines are DEFINED to answer differently. The example row is what keeps this "
                + "entry honest, exactly as Every_expected_divergence_still_diverges intends, and "
                + "tools/probes/enhancematch-cost-rows.py is where the family is drawn on purpose.",
            PinnedBy: "FuzzyEnhanceMatchTests.Cost_ranking_keeps_the_cheaper_match_where_upstream_"
                + "takes_the_one_with_fewer_errors",
            Example: _costRankedRow,
            Applies: static (row, ours) => IsCostRankedDivergence(row, ours, "(?e)")
        ),
        new(
            Id: "bestmatch-ranks-by-cost",
            Reason: "PORT DELIBERATELY DIFFERENT, the same owner decision as "
                + "`enhancematch-ranks-by-cost` above (DECISIONS 2026-09-12) reaching `BESTMATCH` in "
                + "S42's second sitting. The two entries are separate because the CHANGE is not the "
                + "same change. `ENHANCEMATCH` walks a chain of runs and only its tie-break moved. "
                + "`BESTMATCH` searches the slice with a BUDGET, `state->max_errors = fewest_errors "
                + "- 1` (upstream/src/_regex.c:17675), and an error-count budget cannot reach a "
                + "cheaper match that spends the same number of errors - which is the whole of issue "
                + "470. So the budget moved: Matcher.DoBestFuzzyMatch walks the slice twice, once "
                + "bounded by COST to find the cheapest match there is, and then upstream's own walk "
                + "with the cost pinned at that answer, which makes upstream's fewest-errors rule the "
                + "owner's tie-break. The cost budget is release 2015.09.28's `state->max_cost = "
                + "state->total_cost - 1`, which the 2015.11.5 issue-165 hang fix removed.\n"
                + "WITH UNIT COSTS THE TWO RULES AGREE, so this predicate insists on a cost equation "
                + "whose coefficients are not all equal, exactly as the entry above does. The example "
                + "row is `(?b)(?:ab|xyc){9i+1s+9d<=20}` fullmatched against 'abc': upstream takes "
                + "'ab' with one insertion costing 9 and this port takes 'xyc' with two "
                + "substitutions costing 1 each. Measured on regex 2026.7.19, 2026-09-13.\n"
                + "NARROW ON THE DIFFERENCE, on the same terms as the entry above: the two answers "
                + "must agree on everything but which errors were spent, and this port's must be "
                + "cheaper while spending at least as many. A `BESTMATCH` divergence at a DIFFERENT "
                + "span is reported rather than classified, for the reason set out above - it is also "
                + "what a real engine defect looks like - and `_has_weighted_cost` keeps waves off "
                + "the family rather than the list being widened to swallow it.\n"
                + "ONE PORTED TEST ASSERTS THIS PORT'S ANSWER BECAUSE OF THIS ENTRY, which is the "
                + "thing to know before trusting S42's first sitting on it. `test_fuzzy#44`, "
                + "`(?b)(foobar){i<=1,d<=2,s<=3,2d+1s<4}` over FuzzyTestData.Scattered, answers "
                + "(34, 39) upstream and (26, 33) here, and upstream's own engine proves the cheaper "
                + "match is real: tighten the equation to `2d+1s<3` and upstream finds exactly "
                + "(26, 33). See the comment on the test.",
            PinnedBy: "FuzzyBestMatchTests.Bestmatch_answers_the_cheaper_match_where_upstream_"
                + "answers_the_earlier_one",
            Example: _bestCostRankedRow,
            Applies: static (row, ours) => IsCostRankedDivergence(row, ours, "(?b)")
        ),
        new(
            Id: "bestmatch-loses-a-partial",
            Reason: "Upstream bug, ledger entry 13, found by S43's composed `interactions` wave at "
                + "the Phase 5 close - the first three-seed 6000-row run to draw fuzzy beside the "
                + "Phase 3 and 4 constructs. `(?b)` makes upstream lose a fuzzy PARTIAL that the "
                + "same pattern without the flag still finds.\n"
                + "THE JUDGEMENT NEEDS NO SECOND ENGINE, and it rests on two arguments of "
                + "different strength which a report must keep apart. THE SCOPE OF THE STRONG ONE "
                + "HAS BEEN STATED WRONG THREE TIMES - claimed for all five with no evidence, cut "
                + "to two by a blind review that swept `pos` alone, over-corrected back to five - "
                + "and the measurement says FOUR. Hence the scope beside each.\n"
                + "  STRONG, on FOUR of the five: the SAME compiled pattern answers None from "
                + "`search(partial=True)` and a partial from its own ANCHORED door. No reading of "
                + "any ranking rule lets a search miss what its own anchored match finds. 76681 and "
                + "76593 answer by `pos`; 74938 and 77937 are `(?r)` and answer by `endpos`, which "
                + "is where a reversed pattern anchors - 74938 at endpos 1, 77937 at endpos 2, 4 "
                + "and 5.\n"
                + "  THE FIFTH IS 76251 AND IT DOES NOT COUNT. It is forward, and its only anchored "
                + "answer is the degenerate empty slice at endpos 0. Truncating a FORWARD subject "
                + "with `endpos` changes what a trailing `$`, `\\Z` or lookahead means, so an "
                + "endpos hit argues only for a pattern that reads nothing at the end - it is the "
                + "natural door for a `(?r)` row and a weak one for a forward row. 76251 therefore "
                + "rests on the weak form alone, and the draft that said 'all five' was counting "
                + "the empty slice its own caveat disqualifies.\n"
                + "  WEAK, on all five and on the minimised shape: deleting `(?b)` gives upstream a "
                + "match it refused with the flag present - codepoints (0, 3), (0, 7), (0, 1), "
                + "(4, 5) and (8, 8). `BESTMATCH` is documented as choosing the BEST match rather "
                + "than the first; it is not a filter that removes matches, so a flag that turns a "
                + "match into no match is upstream contradicting its own documentation.\n"
                + "  THOSE FLAGLESS ANSWERS ARE THIS PORT'S ANSWER IN FULL ON FOUR OF THE FIVE, AND "
                + "ON 77937 ONLY THE SPAN AGREES. Upstream flagless spends no errors and captures "
                + "nothing there; this port answers the same span with `fuzzy=(1,1,1)` and group 2 "
                + "set. So 77937 rests on 'upstream refused a match it finds without the flag', and "
                + "NOT on 'upstream's flagless answer is ours'. The first draft claimed the latter "
                + "for all five because its probe compared `m.span()` alone while its prose claimed "
                + "the whole answer; the probe now prints the groups and the counts.\n"
                + "  S47c RETIRES THAT CAVEAT, by asking a better question. The flagless answer was "
                + "a stand-in for 'what upstream would say without the defect', and there is now a "
                + "build without the defect: with either candidate fix below, upstream's answer "
                + "UNDER `(?b)` is this port's answer IN FULL on all five, 77937 included. So the "
                + "yardstick was wrong for that row, not the port.\n"
                + "Minimised, AND SINCE S47b AN EXAMPLE ROW OF THIS ENTRY (row 6) rather than only a "
                + "sentence and a probe, so the staleness alarm runs it on every oracle run: "
                + "`(?b)(?:ab){e<=1}(?:\\S(*SKIP)\\w|\\W)` over 'ab.' - upstream's "
                + "search is None and its own `match('ab.', 2, partial=True)` is (2, 3) partial. "
                + "Four conditions, each necessary on that shape: `(?b)` (`(?e)` in its place keeps "
                + "the match, so it is `do_best_fuzzy_match` and not fuzzy ranking at large), a "
                + "fuzzy section, a `(*SKIP)` (the same pattern without the verb keeps its match "
                + "under `(?b)`), and `partial=True`. Measured 2026-09-13 on regex 2026.7.19, "
                + "tools/probes/upstream-bestmatch-loses-a-partial.py, whose second section replays "
                + "all five wave rows whole, each asked its own operation. SINCE S47c THERE IS A ROW "
                + "7 AS WELL, the minimised shape REVERSED - `(?b)(?r)(?:ab){e<=1}(?:\\S(*SKIP)\\w|"
                + "\\W)` over '.ab' - because the mechanism has a `slice_end` arm (`:14553`) that "
                + "rows 1-6 never touched. The whole block is re-recorded output: `python "
                + "tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-partial-rows.jsonl`.\n"
                + "FAULTING MECHANISM, ESTABLISHED TO THE LINE BY S47c ON 2026-09-14 in a `/Od /Zi` "
                + "build of the pinned 2026.9.10 source, instrumented with `fprintf` and run "
                + "(`python tools/probes/upstream-bestmatch-lost-candidate.py --trace`). It is a "
                + "LEAK ACROSS TWO ATTEMPTS AT ONE MATCH, not a ranking rule:\n"
                + "  1. `_regex.c:14555` - `RE_OP_SKIP` sets `state->slice_start = state->text_pos`. "
                + "On the minimised shape the trace reads `SKIP :14555 slice_start 0 -> 3`, during "
                + "the NORMAL (non-partial) attempt, which then fails.\n"
                + "  2. `_regex.c:18170` - `do_match`'s partial fallback restores `text_pos` ALONE, "
                + "so the second attempt runs with `slice=[3,3]` and `text_pos=0`.\n"
                + "  3. `_regex.c:17625` - `do_best_fuzzy_match`'s scan loop is guarded by "
                + "`state->slice_start <= start_pos && start_pos <= state->slice_end`. With "
                + "`slice_start=3` and `start_pos=0` that is FALSE, the body never runs once, and "
                + "`status` keeps the `RE_ERROR_FAILURE` it was initialised with at `:17599`. The "
                + "partial is not ranked and rejected - IT IS NEVER ATTEMPTED. The hypothesis this "
                + "entry carried before S47c named the right guard for the wrong reason: it blamed "
                + "the RETRY's `start_pos = state->match_pos`, and the trace shows the loop is "
                + "refused on its FIRST iteration.\n"
                + "WHY THE FLAG MATTERS, AND IT IS NOT A FILTER. `do_simple_fuzzy_match` is handed "
                + "the SAME leaked `slice=[3,3]` - the trace prints it - and still answers, because "
                + "it has no such guard. `(?b)` does not remove the match; it routes the retry "
                + "through the one entry point whose loop guard the leaked bound falsifies. And "
                + "`do_enhanced_fuzzy_match` restores the slice before every return that is not a "
                + "hard error (`:18003`; the `goto error` at `:18001` skips it, and that path aborts "
                + "the whole match anyway), which is upstream's own statement that the slice is "
                + "per-attempt state; `do_best_fuzzy_match` restores it only inside its "
                + "`found_match && fewest_errors > 0` branch (`:17848`), so an attempt that merely "
                + "fails leaks.\n"
                + "TWO FIXES, BOTH MEASURED, BOTH LEAVING UPSTREAM'S OWN SUITE AT 101 RUN / 0 FAILED. "
                + "(A) `do_match` saves the slice beside `text_pos` and restores both - WHICH IS "
                + "WHAT THIS PORT ALREADY DOES at `Matcher.cs:10098-10100`, chosen in S40b on "
                + "self-refutation grounds before the upstream mechanism was known. (B) "
                + "`do_best_fuzzy_match` restores the slice on every exit, as its sibling does. Both "
                + "fix all five wave rows, the minimised shape and its reversed twin; A also reaches "
                + "the non-BESTMATCH retry and changes one flagless answer (row 77937 gains "
                + "`fuzzy=(1,1,1)` and group 2), B changes nothing outside `(?b)`.\n"
                + "KEYED ON ROWS, like `bounded-lazy-repeat-partial`, `partial-retry-reversed-slice` "
                + "and `search-start-partial`'s second arm, and for their reason rather than for "
                + "convenience. Every predicate over 'upstream answered nothing and this port "
                + "answered a partial' also describes a port defect that INVENTS a partial, which is "
                + "a defect this port has had twice in Phase 5 alone (S40b's carried slice, S43's "
                + "own SaveBestMatch fix). The four tells are all in the pattern text, so a "
                + "predicate over them would classify by syntax rather than by behaviour and would "
                + "swallow the next real one. Widening means judging another row with the probe and "
                + "adding it here, not loosening a condition.",
            PinnedBy: "FuzzyBestMatchTests.Bestmatch_keeps_a_partial_that_upstreams_own_search_"
                + "loses_beside_a_skip and its negative control "
                + ".Bestmatch_without_the_verb_keeps_its_match_on_both_engines",
            Example: _bestmatchLostPartialRows,
            Applies: static (row, ours) =>
                _bestmatchLostPartial.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "bestmatch-loses-a-candidate",
            Reason: "UPSTREAM IS WRONG AND THIS PORT DIVERGES ON PURPOSE - ledger entry 12, fixed by "
                + "S46 on 2026-09-14. `END_FUZZY`'s backtrack arm is the only place a TRAILING "
                + "insertion can come from, and upstream guards it with "
                + "`total_errors(state->fuzzy_counts) + total_errors(inner_counts) < "
                + "state->max_errors` (upstream/src/_regex.c:15515-15517), which DOUBLE-COUNTS: "
                + "`END_FUZZY` merged `inner_counts` INTO `state->fuzzy_counts` twenty lines earlier "
                + "(:12473-12484), so the two terms are the same errors added twice. Every other "
                + "`max_errors` test in the file asks about ONE set of counts (`any_error_permitted` "
                + ":9672, `this_error_permitted` :9690, `insertion_permitted` :9708), and "
                + "`insertion_permitted` on the line above already bounds `inner_counts` by the "
                + "section's own limits, so this port drops the second term and nothing is lost.\n"
                + "WHY IT IS ONLY VISIBLE UNDER `(?b)`: plain fuzzy matching runs with `max_errors` "
                + "at PY_SSIZE_T_MAX (`do_simple_fuzzy_match` :18027) so the guard never bites. "
                + "`do_best_fuzzy_match` is where it becomes finite - the second pass climbs it only "
                + "to `fewest_errors` (:17732) and the widened-slice fallback uses `fewest_errors` "
                + "too (:17823) - so a match needing n trailing insertions needs `n > 2n-2`, false "
                + "for every n >= 2 AT EVERY BUDGET.\n"
                + "THERE IS NO SECOND ENGINE TO ASK, AND THAT WAS MEASURED RATHER THAN ASSERTED - "
                + "amendment 16 asks for a real run of one, so the absence has to be evidence too. "
                + "`python tools/probes/pcre2-has-no-fuzzy-matching.py`, pcre2 0.7.1 over libpcre2 "
                + "10.47, 2026-09-14: PCRE2 does not merely lack fuzzy matching, it reads the "
                + "suffix as LITERAL TEXT - `compile(r'(?:x){e<=3}').match('xyz')` is None and "
                + "`.match('x{e<=3}')` is (0, 7) - so a comparison built on it would answer "
                + "confidently and wrongly. Only `(?b)` fails loudly, and only because PCRE2 has no "
                + "such flag. Perl and .NET have no approximate matching either. `BESTMATCH` is "
                + "documented as a RANKING flag - "
                + "\"By default, fuzzy matching searches for the first match that meets the given "
                + "constraints ... The BESTMATCH flag will make it search for the best match "
                + "instead\" (upstream/README.rst:592) - so it chooses among the flagless engine's "
                + "candidates and cannot destroy them all. `regex.fullmatch(r'(?b)(?:x){e<=3}', "
                + "'xyz')` is None where the same engine without the flag answers (0, 3) with two "
                + "insertions, which is upstream contradicting its own definition. Measured on regex "
                + "2026.9.10, 2026-09-14, tools/probes/upstream-bestmatch-trailing-insertions.py; the S46 closing notes "
                + "carry the whole (k trailing chars, N budget) matrix and how to re-run it.\n"
                + "KEYED ON THE NINE ROWS **AND** ON THE RECORDED `bestmatchFreeOutcome` SINCE S47b, "
                + "where it was keyed on the recorded answer alone. The independent audit of "
                + "S44-S46 read the paragraph below - the entry's own admission that a port which "
                + "IGNORED `(?b)` satisfies the flagless test on every row - and graded this the "
                + "list's highest-risk entry for exactly that reason: the widest possible defect in "
                + "the feature the entry is about would have been tallied EXPECTED wherever it "
                + "landed. The owner ruled on 2026-09-14 that a pin is narrowed to the rows where "
                + "its contradiction was measured and every other `(?b)` divergence shows red for "
                + "triage, and that it widens only by judging another row with the probe and adding "
                + "it here. `OracleWaveTests.A_bestmatch_row_answered_as_though_the_flag_were_"
                + "absent_is_not_accounted_for` is the red-first test: `(?b)(?:cats|cat){e<=1}` over "
                + "'cat', where upstream's two answers DIFFER and its flagged one is right, "
                + "classified EXPECTED before the change and unclassified after.\n"
                + "WHAT THAT COSTS, stated because it is a real cost and not a free win: this "
                + "family fires about three times per three-seed gate and its rows RENUMBER with the "
                + "row count as well as with the seed - the seed-7 gate draws different questions at "
                + "6000 and at 6300 - so a new draw of the same mechanism now reds the wave until "
                + "someone judges it and adds it. That is the trade the owner decision names, and "
                + "the alternative was a predicate that cannot see the defect it is guarding.\n"
                + "A PREDICATE OVER THE TWO COMPARED ANSWERS WAS THE OTHER OPTION AND IT DOES NOT "
                + "WORK: on seed 20260914 row 76927 the two engines report the SAME "
                + "span, the SAME groups AND the SAME fuzzy counts (2,2,0), differing only over "
                + "which of positions 7 and 8 is the substitution and which the insertion, so "
                + "nothing on our side of the comparison distinguishes the family from a defect. "
                + "What does distinguish it is upstream's OWN answer with the flag deleted, which "
                + "the recorder now asks for on every `(?b)` row "
                + "(`_without_bestmatch`, one of `tools/record-oracle.py`'s `_CONTROLS`): on all "
                + "nine rows the fix moved - "
                + "four at seed 4242 where upstream lost the match outright, three where both "
                + "matched and the error mix differs, one `sub` and one `finditer` - this port's "
                + "answer is upstream's flagless answer EXACTLY, groups, counts, change positions "
                + "and all. Measured row by row, tools/probes/upstream-bestmatch-free-answer.py.\n"
                + "THE ENTRY IS NOW THE DOUBLED GUARD AND NOTHING ELSE, which it was not between "
                + "S46 and S47b. S46's flagless-only key swept in a SECOND mechanism with the same "
                + "signature and no established line - LEDGER ENTRY 13, `(?b)` plus a fuzzy section "
                + "plus a `(*SKIP)` plus `partial=True`, where upstream loses a partial its own "
                + "anchored `match` still finds. Seed 20260914 row 76345 - "
                + "`(?b)(?e)\\b(?:\\p{Ll}(*SKIP)[^\\d]|\\W)(?=(?:(\\p{ASCII}+)([^\\d]*)a){e<=2,s<=1})` "
                + "over 'aaa' - is entry 13's four conditions exactly, and S46 classified it here "
                + "without going looking for it. **It is NOT classified here any more.** Entry 13 "
                + "keeps its own pin one entry above, `bestmatch-loses-a-partial`, which is keyed on "
                + "its five judged wave rows and, since S47b, on its minimised shape as well; row "
                + "76345 is not among them and will red a wave that draws it again, which is the "
                + "owner's 2026-09-14 ruling applied to the row that prompted it.\n"
                + "WHAT THE FLAGLESS TEST ALONE COULD NOT CATCH, which is why it is no longer alone: "
                + "a port that IGNORED `(?b)` altogether answers the "
                + "flagless answer on every row, and this entry classified it. That is the one "
                + "thing the flagless answer cannot discriminate - it says the port picked a legal "
                + "candidate, not that it picked the BEST one. S46 MEASURED THAT RATHER THAN "
                + "ASSUMING IT, as control S46-C (`bestmatch-stops-ranking`, in tools/controls.json): "
                + "stopping the first walk's budget from tightening, so `(?b)` keeps the earliest "
                + "match rather than the best, leaves a 6000-row `fuzzy` wave at 0 / 0 / 1 "
                + "divergences over seeds 7, 4242 and 31337 against an unmutated 0 / 0 / 0, with "
                + "the accounted-for count moving 1 -> 3 at seed 7. So the wave DOES still report "
                + "the defect, at one row in eighteen thousand, and this entry swallowed two rows of "
                + "it at seed 7 - **which it no longer does**, because those two are wave draws and "
                + "the key is now the nine pinned questions. The instrument that actually catches it is the suite: the same "
                + "mutation fails 5 of 5,947 tests. Read that as the division of labour rather than "
                + "as a gap - `(?b)`'s ranking is pinned by `Gaps/Engine/FuzzyBestMatchTests.cs` "
                + "and the ported `test_bestmatch` family, and the oracle cannot substitute for "
                + "either, because upstream's own answer is the thing under suspicion here.",
            PinnedBy: "FuzzyBestMatchTests.Bestmatch_keeps_a_match_that_needs_two_trailing_"
                + "insertions, .Bestmatch_admits_trailing_insertions_up_to_the_sections_own_budget, "
                + ".Bestmatch_still_refuses_a_trailing_insertion_the_budget_cannot_afford and "
                + ".Bestmatch_and_enhancematch_together_keep_the_match_bestmatch_alone_would_lose",
            // Nine rows rather than one, because each is a judged member of the family and the
            // staleness alarm should re-test each: upstream losing the match outright, upstream
            // keeping it with a different error mix at the same error count, the two aggregate
            // operations whose outcome is not a match object, a `partial=True` row upstream
            // DOWNGRADES rather than loses, and a `finditer` that loses one match of three.
            // Recorded by
            // `python tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-candidate-rows.jsonl`, 2026-09-14.
            Example: _bestmatchLostCandidateRows,
            Applies: static (row, ours) =>
                _bestmatchLostCandidate.Contains(Question(row))
                && row.BestmatchFree is not null
                && string.Equals(ours.Describe(), row.BestmatchFree.Describe(), StringComparison.Ordinal)
        ),
        new(
            Id: "posix-fuzzy-contradicts-its-own-flagless-answer",
            Reason: "Upstream bug, and the inherited half of what S48b fixed on this side. POSIX is "
                + "a CHOOSING flag - leftmost-longest among the matches the ordinary engine can "
                + "make - so it may move WHICH match is answered, and only within what the flagless "
                + "engine can already produce. On these three rows it breaks that, and on every one "
                + "THIS PORT'S ANSWER IS UPSTREAM'S OWN POSIX-FREE ANSWER. Measured 2026-09-14, "
                + "tools/probes/upstream-posix-and-atomic-free-answers.py, on regex 2026.9.10.\n"
                + "ROW BY ROW, because the three are NOT one symptom - a first draft of this entry "
                + "said they were and the blind review reproduced the row that refutes it. Seed 7 "
                + "row 76983, `(?e)(?r)(?p)`: both engines answer the span (0, 3) and the same "
                + "group, and upstream charges (1, 0, 1) where its own POSIX-free engine fits that "
                + "span in (0, 0, 1). That is LEDGER ENTRY 9's mechanism seen from the other side - "
                + "S48b fixed this port's copy of it, the POSIX FAILURE arm's `RestoreBestMatch` "
                + "leaving `TotalErrors` holding the LOSING candidate's, and upstream still has it. "
                + "Seed 7 row 73895, `(?b)(?e)(?r)(?p)`: upstream reports (0, 7) at a cost of one "
                + "error, and its own POSIX-free `fullmatch` OVER THAT VERY SPAN answers it with "
                + "none. **Seed 20260914 row 76101 is a DIFFERENT defect and the cost does not move "
                + "at all**: both answers cost (1, 0, 1), and what changes is the SPAN - (0, 7) "
                + "under POSIX against (0, 8) without it, so the flag picked the SHORTER of two "
                + "equal-cost matches and the group the template reads captured somewhere else. "
                + "That is leftmost-longest inverted, which is LEDGER ENTRY 16's mechanism, and it "
                + "is pinned here because the discriminator and the judgement are the same ones.\n"
                + "S52 ADDED TWO MORE, both seed 7 of the three-seed 2000-row wave of commit 407c0cb "
                + "(2026-09-15), and the second is a symptom this entry had not seen. Row 24430 is "
                + "76983's mechanism reached through a `\\L<name>` list and with POSIX set as a FLAG "
                + "rather than written `(?p)`: the two matches of the scan agree on both spans and on "
                + "the second match's cost, and upstream charges the FIRST span one deletion where "
                + "its own POSIX-free engine spends NONE over the identical span - printed span by "
                + "span by the probe. **Row 24916 moves neither a cost nor a span: POSIX invents a "
                + "MATCH.** `subn` with `count=2` replaces twice under POSIX and once without it, on "
                + "a template that expands to nothing either way, so both answers carry the text "
                + "unchanged and only the count says what happened. A flag that chooses "
                + "leftmost-longest among the matches the ordinary engine can make cannot produce one "
                + "the flagless engine cannot, so this breaks the same contract by a third route.\n"
                + "KEYED ON THE FIVE ROWS AND ON THIS PORT'S ANSWER TO EACH, which is the owner's "
                + "2026-09-14 ruling applied as `bestmatch-loses-a-candidate` applies it: a pin "
                + "covers the rows whose contradiction was measured, every other POSIX fuzzy "
                + "divergence shows red for triage, and it widens only by judging another row with "
                + "the probe and adding it. `posixFreeOutcome` is required as well, AND required to "
                + "differ from upstream's drawn answer, so the staleness alarm re-tests the "
                + "discriminator rather than merely confirming it was asked - without that second "
                + "clause a recorder whose POSIX removal had silently become a no-op would leave "
                + "the entry classifying on evidence that says nothing.\n"
                + "WHY NOT KEY ON THE DISCRIMINATOR ALONE, which would be self-maintaining and is "
                + "what the atomic entry below does: on row 73895 the POSIX-free SCAN answers a "
                + "DIFFERENT SPAN - its first match starts at codepoint 4 rather than 0 - so "
                + "upstream's recorded flagless outcome is not this port's answer and cannot be the "
                + "key. The question that judges that row is anchored, and an anchored question is "
                + "not what the recorder writes down. Row 76983 fails the same test for a duller "
                + "reason: a POSIX row carries no change positions on either side (ledger entry 9 - "
                + "reading `fuzzy_changes` there segfaults the interpreter), so the comparison drops "
                + "them from both, and the rendered answers differ in that field alone.",
            PinnedBy: "FuzzyPosixTests.A_posix_enhancematch_span_costs_no_more_than_the_same_span_"
                + "costs_without_posix, .A_posix_fuzzy_match_spends_what_the_flagless_engine_spends "
                + "and .Posix_does_not_add_a_match_the_flagless_engine_cannot_make",
            Example: _posixOvercostRows,
            Applies: static (row, ours) =>
                _posixOvercost.TryGetValue(Question(row), out string? judged)
                // The discriminator has to have MOVED upstream's answer, not merely been asked.
                // Without this clause the recorder's POSIX removal could silently become a no-op -
                // a `(?p)` spelt somewhere the prefix rule does not reach, say - and the entry would
                // go on classifying on evidence that says nothing. All three rows differ here: one
                // in the counts, one in the span, one in the replacement text.
                && row.PosixFree is not null
                && !string.Equals(row.PosixFree.Describe(), row.Expected.Describe(), StringComparison.Ordinal)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "atomic-group-leaks-a-change-position",
            Reason: "Upstream bug, and LEDGER ENTRY 11's mechanism through a door S47's "
                + "`leakFreeFuzzy` cannot open. Upstream saves and restores the fuzzy COUNTS as a "
                + "block and unwinds the CHANGES one item at a time (`start_match` clears "
                + "`state->fuzzy_counts` and leaves `state->fuzzy_changes` alone, "
                + "upstream/src/_regex.c:11790-11792), so any construct that abandons a sub-attempt "
                + "WITHOUT backtracking through it leaves that sub-attempt's change entries behind. "
                + "An ATOMIC GROUP is exactly such a construct, and its leak is inside ONE attempt - "
                + "which is why the anchored question cannot see it: this row's own recorded "
                + "`leakFreeFuzzy` agrees with upstream's drawn answer, not with this port's, "
                + "because anchoring removes an EARLIER attempt's leak and there is no earlier "
                + "attempt here.\n"
                + "WHAT SETTLES IT IS UPSTREAM'S OWN CONTROL: spell the `(?>` as `(?:` - the same "
                + "body, the same alternatives, the backtracking cut gone - and upstream moves its "
                + "deletion from codepoint 2 to codepoint 3, which is UTF-16 4 on this astral "
                + "subject and is this port's answer. Everything else about the two answers is "
                + "identical: the same span, the same counts (2, 1, 1), the same two substitutions "
                + "and the same insertion. Measured 2026-09-14, "
                + "tools/probes/upstream-posix-and-atomic-free-answers.py, on regex 2026.9.10.\n"
                + "THE SECOND ROW SETTLES IT WITHOUT THE CONTROL AT ALL, which is why S52 sitting 9 "
                + "added it (seed 7 row 74510 of the 6000-row gate, 2026-09-15). Upstream counts "
                + "(1, 2, 0) - one substitution, two insertions - and then lists TWO substitutions "
                + "(codepoints 3 and 4) and ONE insertion (3). `fuzzy_changes` is documented as the "
                + "positions of the changes `fuzzy_counts` counts, so the two are views of one edit "
                + "script and that answer contradicts itself before this port is consulted. The cut-"
                + "free control then agrees with this port anyway: the same span (0, 7), the same "
                + "five groups, the same counts, and the list re-kinded to one substitution at 5 and "
                + "two insertions at 3 and 4. Row 74033's list is internally consistent and only its "
                + "deletion POSITION moves, so it needs the control; this one does not, and the "
                + "control agreeing on top is what makes the pair evidence rather than a reading.\n"
                + "KEYED ON THE ROW **AND** ON THE RECORDED `atomicFreeOutcome`, the shape "
                + "`bestmatch-loses-a-candidate` uses: the row makes the entry as narrow as one "
                + "judged question, and demanding that this port's whole answer equals upstream's "
                + "own cut-free answer makes the staleness alarm re-test the discriminator on every "
                + "run. A predicate over the two compared answers would not do: both engines report "
                + "the same span, the same counts and two of the three change positions, and differ "
                + "over one deletion - which is indistinguishable from this port computing a "
                + "position wrongly.",
            PinnedBy: "FuzzyCountsAndChangesTests.An_atomic_group_reports_the_deletion_the_cut_"
                + "free_pattern_reports",
            Example: _atomicLeakedChangeRows,
            Applies: static (row, ours) =>
                _atomicLeakedChange.Contains(Question(row))
                && row.AtomicFree is not null
                && string.Equals(ours.Describe(), row.AtomicFree.Describe(), StringComparison.Ordinal)
        ),
        new(
            Id: "reversed-lookahead-change-at-the-match-start",
            Reason: "Upstream bug, and LEDGER ENTRY 11's mechanism through a third door. Under "
                + "`(?r)`, a fuzzy section inside a LOOKAHEAD has its change positions reported at "
                + "the MATCH START rather than at the position the lookahead actually tested. The "
                + "counts are right and the positions are not, so the two engines agree on the "
                + "span, the groups and the counts and differ only over where the errors were "
                + "spent - which is indistinguishable from this port computing a position wrongly, "
                + "and is why this entry lists a row rather than a predicate.\n"
                + "WHAT SETTLES IT IS UPSTREAM CONTRADICTING ITSELF, on a reproducer minimised to "
                + "four constructs and a three-character subject, with no flags at all. "
                + "`A(?=[^A]{e<=1})A+\\D` over 'AAA': FORWARD, upstream answers (0, 3) with one "
                + "substitution at 1 - the position the lookahead tests, one past the leading `A` - "
                + "and this port answers the same thing. Add `(?r)`, which moves no bound and picks "
                + "the same candidate here (both engines still answer the span (0, 3) at the same "
                + "count), and UPSTREAM'S substitution moves to 0 while this port's stays at 1.\n"
                + "THE CONDITION IS A GENERAL REPEAT AFTER THE LOOKAHEAD, measured rather than "
                + "guessed, and the first draft of this entry had it wrong. It claimed the shift was "
                + "the lookahead's OFFSET from the match start; `AA(?=[^A]{e<=1})A+\\D` over 'AAAA' "
                + "kills that, because its offset is 2 and upstream reversed still answers 0 where "
                + "its own forward answer is 2. What actually separates the rows is the atom after "
                + "the lookahead: make it a FIXED count - `A(?=[^A]{e<=1})A\\D` over 'AAA', the "
                + "reproducer and nothing else changed - and both directions answer 1. With the "
                + "lookahead AT the match start there is nowhere for the position to move to, and "
                + "both directions answer 0. Measured 2026-09-14 on regex 2026.9.10, "
                + "tools/probes/upstream-reversed-lookahead-change-position.py, whose four rows are "
                + "those four and which exits non-zero if any of them stops behaving that way.\n"
                + "HOW MUCH OF THE WAVE ROW THIS ACCOUNTS FOR, stated exactly rather than "
                + "generously. The row has the reproducer's three ingredients - `(?r)`, a fuzzy "
                + "section inside a lookahead, and general repeats (`[A-Z]{1,}`, `\\D+`) after it - "
                + "but it has TWO fuzzy sections and spends one substitution in each, so the "
                + "minimisation isolates one mechanism and the row shows the sum. It also carries "
                + "an earlier attempt's leak ON TOP: trimming the leading '\\r\\n\\r\\n', which "
                + "leaves the match identical in shape, span and groups, moves upstream's pair from "
                + "an absolute (1, 0) that sits OUTSIDE the match to a relative (1, 2) that sits "
                + "inside, while this port answers a relative (2, 3) at every prefix length. So "
                + "what is judged here is that this port's positions are the ones upstream's own "
                + "forward matching gives for this family, not that the row's whole difference has "
                + "been reduced to one line of upstream.\n"
                + "NO RECORDED DISCRIMINATOR, and that is a real weakness rather than an oversight: "
                + "deleting the `(?r)` makes upstream answer None to THIS row, so its forward answer "
                + "cannot be written down, and `leakFreeFuzzy` gives a THIRD answer again because "
                + "anchoring cuts off a lookahead that must read past `endpos`. The entry is "
                + "therefore keyed on the one judged question and on this port's exact answer to it, "
                + "and it widens only by judging another row.\n"
                + "WHAT THIS PORT CHANGED, said out loud because it is the reason the row is here at "
                + "all: before S48b this port reproduced upstream's answer exactly, leak included. "
                + "S48b's `PopFuzzyCounts` truncation - the fix for mechanisms C and D - moved it "
                + "onto upstream's own forward answer. Verified by bisection against a worktree at "
                + "c8165b5 and by flipping the truncation's comparison, which restores upstream's "
                + "answer at every prefix length.",
            PinnedBy: "FuzzyCountsAndChangesTests.A_reversed_lookahead_reports_its_substitution_"
                + "where_the_lookahead_tested",
            Example: _reversedLookaheadChangeRows,
            Applies: static (row, ours) =>
                _reversedLookaheadChange.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "fuzzy-changes-of-the-wrong-kind-for-their-own-counts",
            Reason: "Upstream bug, and LEDGER ENTRY 11's mechanism through a fourth door. S52 "
                + "sitting 9, 2026-09-15, seed 7 row 74345 of the 6000-row gate.\n"
                + "WHAT SETTLES IT IS UPSTREAM CONTRADICTING ITSELF ON ONE ANSWER, with no ablation "
                + "and no comparison with this port. Upstream reports `fuzzy_counts` (0, 2, 0) - no "
                + "substitutions, TWO insertions, no deletions - and then reports `fuzzy_changes` "
                + "holding ONE substitution (codepoint 2) and ONE deletion (1) and NO insertion. "
                + "`fuzzy_changes` is documented as \"a tuple of the positions of the substitutions, "
                + "insertions and deletions\" of the match `fuzzy_counts` counts, so the two are "
                + "views of ONE edit script and cannot both be right. BOTH ENGINES AGREE ON THE "
                + "COUNTS, so there is no dispute about what the edit script is: it is two "
                + "insertions, and two insertion positions are the only thing either engine may "
                + "report. This port reports two insertions. Upstream reports none.\n"
                + "THE LEAK IS INSIDE ONE ATTEMPT, which is why the row is here rather than under "
                + "`fuzzy-changes-leaked-from-an-abandoned-attempt`: the row's own `leakFreeFuzzy` "
                + "reproduces upstream's drawn answer character for character, so S47's anchored "
                + "question - which removes an EARLIER attempt's leak - sees nothing. That is the "
                + "same field reading the same way on the atomic-group and reversed-lookahead rows "
                + "above, and it is what puts all three in the E/F/G half of ledger entry 11's table "
                + "rather than the A/B half this port fixed.\n"
                + "WHICH CONSTRUCT LEAKED IS NOT ESTABLISHED, and it is not guessed at here. The "
                + "pattern carries a fuzzy section inside a NEGATIVE LOOKAHEAD - a construct that "
                + "succeeds precisely when its body's sub-attempts are abandoned, which is the "
                + "defect class's own description - but every ablation that would isolate it MOVES "
                + "THE CANDIDATE and so cannot arbitrate this one. Measured 2026-09-15 on regex "
                + "2026.9.10, `python tools/probes/upstream-fuzzy-changes-of-the-wrong-kind.py`: "
                + "deleting the lookahead answers (1, 3) complete, making its body non-fuzzy answers "
                + "(2, 3) complete, giving its body a zero budget answers (2, 3) complete, and "
                + "spelling it POSITIVE answers (1, 3) complete - against the drawn (0, 3) partial. "
                + "Anchoring the drawn span instead holds the candidate and reproduces the "
                + "contradiction unchanged, which is the `leakFreeFuzzy` line said a second way.\n"
                + "WHAT THIS ENTRY DOES NOT ESTABLISH, said out loud: that codepoints 2 and 1 are "
                + "the RIGHT two insertion positions. The KIND is settled and the positions are not, "
                + "and this is the same weak arm `fuzzy-changes-leaked-from-an-abandoned-attempt` "
                + "already names. The instrument that covers it is not this list - it is "
                + "`OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` over "
                + "every fuzzy match of a whole wave, plus the gap test named below.\n"
                + "KEYED ON THE ROW AND ON THIS PORT'S EXACT ANSWER TO IT, the shape "
                + "`reversed-lookahead-change-at-the-match-start` uses, and for its reason: a "
                + "predicate over the two compared answers would say \"same span, same groups, same "
                + "counts, different positions\", which is indistinguishable from this port "
                + "computing a position wrongly. It widens only by judging another row.",
            PinnedBy: "FuzzyCountsAndChangesTests.A_negative_lookahead_s_abandoned_attempt_does_not_"
                + "re_kind_the_changes_that_follow_it",
            Example: _wrongKindChangeRows,
            Applies: static (row, ours) =>
                _wrongKindChange.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "turkic-default-folding",
            Reason: "UPSTREAM IS WRONG, and this port diverges on purpose - S45. "
                + "`CaseFolding.txt` marks two rows `T`, `0049; T; 0131` and `0130; T; 0069`, and "
                + "says of them: \"For non-Turkic languages, this mapping is normally not used\" "
                + "and \"The mappings with status T can be used or omitted depending on the desired "
                + "case-folding behavior. (The default option is to exclude them.)\" "
                + "`upstream/tools/build_regex_unicode.py` includes them in BOTH default tables "
                + "(`kind in {'S','C','T'}` at :455, `kind in {'F','C','T'}` at :459) and hard-codes "
                + "the Turkic pairing into the all-cases table at :1071-1074, so upstream applies a "
                + "Turkish locale rule with no locale asked for. Every `_IGN` opcode reads "
                + "`re_get_all_cases`, so `(?i)I` matches `ı` and `(?i)i` matches `İ` "
                + "upstream. `unicode_possible_turkic` (:1984) papers over the folding half by "
                + "passing all four through unchanged, which loses `0049; C; 0069` as well as "
                + "`0130; F; 0069 0307` - that second loss is ledger entry 7.\n"
                + "THREE SECOND ENGINES WERE RUN ON THE 25-CELL GRID before this entry was written "
                + "(2026-09-14, .scratch/s45-definition.py, .scratch/s45-perl.pl, "
                + ".scratch/s45-dotnet.ps1). PCRE2 10.47 under PCRE2_UTF|PCRE2_UCP|PCRE2_CASELESS "
                + "and .NET 10.0.10 under IgnoreCase|CultureInvariant agree cell for cell with this "
                + "port's simple folding; Perl 5.42.2's /i under (?u:...), which folds fully, "
                + "agrees cell for cell with its full folding. regex 2026.9.10 is the only one of "
                + "the four that answers the Turkic way. UTS #18 RL1.5 requires \"at least the "
                + "simple, DEFAULT Unicode case-insensitive matching\"; core spec 5.18.2 calls the "
                + "Turkish rule \"a case mapping that depends on the locale\". Nothing filed: this "
                + "is a port-right divergence, pinned permanently.\n"
                + "NARROW BY THE SPANS AND BY U+0130/U+0131, not by the subject's contents alone. "
                + "Requiring only IGNORECASE and one of the four somewhere in the subject would "
                + "classify any unrelated defect that landed on a row holding an `I`, which is what "
                + "the class remarks above warn against. Both blind passes reproduced a probe "
                + "against an earlier form of this predicate and both fixes are in it; "
                + "`DivergenceStartsOnATurkicI`'s own remarks carry the two residual limits, one "
                + "safe and one not, and the second is owed maintenance rather than a clause.\n"
                + "THE DEFAULT WAVE REACHES THIS FAMILY THINLY, AND NOT THROUGH THE GENERATOR YOU "
                + "WOULD EXPECT. Measured 2026-09-14 over the full 6300-row default wave: one row "
                + "at seed 7 (row 3762) and one at seed 20260914 (row 3734), both from "
                + "`interactions`, none at seed 4242. The `case-folding` generator itself draws "
                + "NONE at 300 rows at any of the three seeds, and 4, 11 and 3 at 2000 rows - the "
                + "four codepoints are in FOLD_TURKIC and in no other alphabet, so only about a "
                + "sixth of its rows can reach them and both members of a pair must line up within "
                + "one. So the examples, not the wave, are what keep this entry honest at the "
                + "default count; widening FOLD_TURKIC's share is owed maintenance, not this slice.\n"
                + "AN ANSWER THAT CARRIES NO SPANS AT ALL WAS INVISIBLE TO THIS ENTRY UNTIL S52, and "
                + "the rule below did not change to fix it - the EVIDENCE did. `sub`, `subf` and "
                + "`split` answer with a string, a list of parts and a count; a row upstream failed "
                + "WHILE MATCHING answers with an exception. The span test reads none of them, so a "
                + "row belonging to this family as plainly as any other reddened the run. Measured "
                + "2026-09-15 on the three-seed 2000-row wave of commit 58977bb, which was red at "
                + "all three seeds on three rows and two were this, at 42,000 rows a seed:\n"
                + "  seed 7    row 29165 `conditionals` subf  (?r)(?(?!s)[A-Z]{2}|(S))$ over "
                + "'s\\rS\\u0131'\n"
                + "  seed 4242 row 24416 `interactions` split ^(?:\\ufb01\\u0130){e<=2:\\S}"
                + "([^a-f]+)$ over '\\u0130\\u0130\\ufb01 \\ufb01\\ufb00'\n"
                + "BOTH WERE PROVED TO BE THE `T` ROWS BY AN ISOLATING CONTROL, not by resemblance "
                + "(tools/probes/upstream-turkic-without-spans.py and its port half "
                + "tools/probes/port-turkic-without-spans.ps1, 2026-09-15, regex 2026.9.10). On "
                + "the first, upstream matches 'S\\u0131' at (2,4) and only a range SPANNING `I` "
                + "reaches the dotless i: `[A-Z]` and `[A-Y]` both match it, `[A-H]` and `[J-Z]` "
                + "match nothing, and replacing the U+0131 kills the match outright. On the second, "
                + "upstream's fuzzy section eats three characters where this port eats two - and "
                + "swapping the U+0130 for `h` or for U+00C5, neither of which has a `T` row, moves "
                + "upstream onto this port's span in both cases.\n"
                + "THE FIX IS THE RECORDER'S `scanMatches`, upstream's own `finditer` over the row, "
                + "recorded as a second fact the way `subMatches` and `anchoredScan` are and read "
                + "here as a third source of spans. It is UPSTREAM'S SIDE ONLY, because this port's "
                + "`Replace` and `Split` hand back no spans either, so a row where only THIS port's "
                + "answer covers a Turkic letter is still reported and judged by hand.",
            PinnedBy: "Gaps.Engine.CaseFoldingTests, which asserts the whole 25-cell grid under "
                + "(?i) and (?fi) against the definitive source, plus "
                + "Gaps.Unicode.UnicodeCasingTests.The_four_turkic_codepoints_carry_the_default_"
                + "case_data_not_upstreams and Ported.CaseFolding.TurkicTests",
            Example: """
            {"generator": "rows", "pattern": "[İ]", "flags": 2, "namedLists": {}, "subject": "i", "operation": "match", "codepointSpan": [0, 1], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
            {"generator": "rows", "pattern": "[A-Z]", "flags": 2, "namedLists": {}, "subject": "ı", "operation": "match", "codepointSpan": [0, 1], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
            {"generator": "rows", "pattern": "İ", "flags": 16386, "namedLists": {}, "subject": "i̇", "operation": "match", "codepointSpan": [0, 1], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
            """,
            Applies: static (row, ours) => DivergenceStartsOnATurkicI(row, ours)
        ),
        new(
            Id: "turkic-default-folding-without-spans",
            Reason: "THE SAME UPSTREAM DEFECT AS THE ENTRY ABOVE - `CaseFolding.txt`'s two `T` rows "
                + "merged into both default tables - on the four answers that carry NO MATCH POSITION "
                + "AT ALL, and a SEPARATE, ROW-KEYED entry because the predicate above cannot safely "
                + "reach them. S52, 2026-09-15.\n"
                + "WHY THE SHAPE NEEDED ANYTHING NEW. `sub`, `subf` and `split` answer with a string, "
                + "a list of parts and a count, and a row upstream failed WHILE MATCHING answers with "
                + "an exception. The entry above narrows by asking which Turkic letters the "
                + "divergence's SPANS cover, and on these four there are none to read - so a row "
                + "belonging to the family as plainly as any other reddened the run. The three-seed "
                + "2000-row wave of commit 58977bb was red at all three seeds on three rows, and two "
                + "of them were exactly this:\n"
                + "  seed 7    row 29165 `conditionals` subf  (?r)(?(?!s)[A-Z]{2}|(S))$ over "
                + "'s\\rS\\u0131'\n"
                + "  seed 4242 row 24416 `interactions` split ^(?:\\ufb01\\u0130){e<=2:\\S}"
                + "([^a-f]+)$ over '\\u0130\\u0130\\ufb01 \\ufb01\\ufb00'\n"
                + "BOTH WERE PROVED TO BE THE `T` ROWS BY AN ISOLATING CONTROL, not by resemblance "
                + "(tools/probes/upstream-turkic-without-spans.py and its port half "
                + "tools/probes/port-turkic-without-spans.ps1, 2026-09-15, regex 2026.9.10). On the "
                + "first, upstream matches 'S\\u0131' at (2,4) and only a range SPANNING `I` reaches "
                + "the dotless i - `[A-Z]` and `[A-Y]` both match it, `[A-H]` and `[J-Z]` match "
                + "nothing - and replacing the U+0131 kills the match outright. On the second, "
                + "upstream's fuzzy section eats three characters where this port eats two, and "
                + "swapping the U+0130 for `h` or for U+00C5, neither of which has a `T` row, moves "
                + "upstream onto this port's span in both cases.\n"
                + "KEYED ON ROWS, LIKE `bestmatch-loses-a-candidate`, AND THAT IS THE WHOLE POINT OF "
                + "THE SPLIT. A DRAFT OF S52 INSTEAD FED THE RECORDER'S `scanMatches` INTO THE ENTRY "
                + "ABOVE, and its blind review reproduced the false positive S45 and S47b each spent "
                + "a pass closing: a fabricated total failure on `(?i)\\w` over 'x\\u0131' was "
                + "tallied EXPECTED. `\\w` matches U+0131 in every engine without consulting a `T` "
                + "row at all, but `CanFoldIntoALetterItDoesNotSpell` short-circuits on the `\\`, and "
                + "the WHOLE SCAN rather than the diverging match was supplying the covered letter - "
                + "so the widest possible defect on such a row would have been silenced. The row is "
                + "pinned in `OracleWaveTests._turkicRowsToRefuse`. Widening this entry means judging "
                + "another row with the probe and adding it, never loosening a condition; that is the "
                + "owner's 2026-09-14 ruling for this file and it costs a red wave until someone "
                + "looks, which is the right way round.\n"
                + "THE RECORDED `scanMatches` IS STILL THE EVIDENCE and the second half of `Applies`: "
                + "upstream's own `finditer` over the row, recorded by the recorder because the "
                + "answer has no spans, so a listed row whose scan stops touching a Turkic letter "
                + "stops being classified rather than silently staying pinned.",
            PinnedBy: "Gaps.Engine.CaseFoldingTests.An_answer_that_carries_no_span_diverges_on_the_"
                + "dotless_small_as_well and .A_range_spanning_the_plain_I_does_not_reach_the_"
                + "dotless_small, plus the whole 25-cell grid the entry above names",
            Example: _turkicWithoutSpansRows,
            Applies: static (row, ours) =>
                _turkicWithoutSpans.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
                && row.ScanMatches is { } scan
                && TurkicLettersCovered(row.Subject, scan).Any()
        ),
        new(
            Id: "turkic-default-folding-from-the-pattern-side",
            Reason: "THE SAME UPSTREAM DEFECT AS THE TWO ENTRIES ABOVE - `CaseFolding.txt`'s two `T` "
                + "rows merged into both default tables - on the rows where the Turkic letter is not "
                + "in the SUBJECT at all. A THIRD, row-keyed entry, because both predicates above ask "
                + "which Turkic letters the divergence's spans cover in the subject, and on these two "
                + "rows the answer is none. S52's second sitting, 2026-09-15.\n"
                + "WHERE THE LETTER ACTUALLY IS. Seed 20260915 row 25482 (`interactions`) has it as "
                + "the first letter of a `\\L<w1>` word, over a subject holding no Turkic letter "
                + "whatsoever; seed 20260915 row 34508 (`partial-sliced`) has it as the pattern's own "
                + "leading literal, on an EMPTY slice where the subject cannot be read at all. The "
                + "first entry's own remarks predicted the named-list false negative before either "
                + "row was drawn.\n"
                + "THE CONTROL IS STRONGER THAN THE ONE THE ENTRIES ABOVE USE, and deliberately so. "
                + "'Take the U+0130 away' does not isolate a `T` row here: swapping it for `h` also "
                + "shortens the fold from two characters to one, so a divergence about LENGTH would "
                + "survive the swap. What this one varies is only whether a `T` row is consulted - "
                + "U+00DF folds to `ss`, U+FB00 to `ff` and U+01F0 to `j` plus U+030C, all longer "
                + "than one character and none of them carrying a `T` row - and the two engines then "
                + "agree CELL FOR CELL. Measured 2026-09-15 on regex 2026.9.10, "
                + "tools/probes/upstream-turkic-from-the-pattern-side.py with its port half "
                + "tools/probes/port-turkic-from-the-pattern-side.ps1:\n"
                + "  row 25482, first letter of the list word -> where the zero-width answer lands\n"
                + "    U+0130  upstream (3,3) two substitutions   this port (2,2) ONE\n"
                + "    U+00DF  upstream (2,2) ONE                 this port (2,2) ONE\n"
                + "    U+FB00  upstream (2,2) ONE                 this port (2,2) ONE\n"
                + "    U+01F0  upstream (2,2) ONE                 this port (2,2) ONE\n"
                + "    h / i   upstream (3,3) two                 this port (3,3) two\n"
                + "  row 34508, the pattern's leading literal -> is a zero-width partial reported\n"
                + "    U+0130  upstream (5,5) partial             this port NONE\n"
                + "    U+00DF  upstream NONE                      this port NONE\n"
                + "    U+FB00  upstream NONE                      this port NONE\n"
                + "    U+01F0  upstream NONE                      this port NONE\n"
                + "    h/i/U+0131  upstream (5,5) partial         this port (5,5) partial\n"
                + "So on both rows this port treats U+0130 exactly as it treats every other letter "
                + "whose default full fold is longer than one character, and upstream treats it "
                + "exactly as it treats `h` - which is `0130; T; 0069`, the row `CaseFolding.txt` "
                + "says to exclude by default, and nothing else. The judgement, the three second "
                + "engines and the UTS #18 citation are the first entry's; this entry adds rows to "
                + "an argument already made, it does not make a new one.\n"
                + "ROW 34508 IS NOT `search-start-partial`, and the recorded field says so rather "
                + "than a reader having to. Upstream's own `match` reports the same zero-width "
                + "partial its `search` did, so `searchOnlyPartial` is FALSE and that entry rightly "
                + "declines the row: `search_start` is called only when searching "
                + "(upstream/src/_regex.c:11816), so what answered here is upstream's slow path.\n"
                + "KEYED ON THE TWO ROWS AND ON THIS PORT'S ANSWER TO EACH, which is the owner's "
                + "2026-09-14 ruling for this file. Widening means judging another row with the probe "
                + "and adding it, never loosening a condition - and there is no condition here that "
                + "could be loosened safely, because 'the pattern holds a U+0130' describes every "
                + "correct row of the `case-folding` generator as well as these two.",
            PinnedBy: "Gaps.Engine.CaseFoldingTests.A_leading_literal_that_folds_longer_than_itself_"
                + "reports_no_partial_on_an_empty_slice and .A_named_list_word_starting_on_an_"
                + "expanding_fold_costs_one_substitution_not_two, both of which run the whole control "
                + "grid above, plus the 25-cell grid the first Turkic entry names",
            Example: _turkicPatternSideRows,
            Applies: static (row, ours) =>
                _turkicPatternSide.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "turkic-default-folding-read-by-a-lookaround",
            Reason: "THE SAME UPSTREAM DEFECT AS THE THREE ENTRIES ABOVE - `CaseFolding.txt`'s two "
                + "`T` rows merged into both default tables - on a row where the Turkic letter is "
                + "read only by a LOOKAROUND, so nothing with a span touches it. S52 sitting 9, "
                + "2026-09-15, seed 20260915 row 88716 of the 6000-row gate.\n"
                + "WHY IT NEEDED A FOURTH ENTRY, and it is one clause rather than a new argument. "
                + "The subject is 'ııııaa' and BOTH of upstream's matches land "
                + "on the trailing `a` - (5, 6) and (4, 5) - so the span test "
                + "`turkic-default-folding` uses finds no Turkic letter, and so does the recorded "
                + "`scanMatches` that `turkic-default-folding-without-spans` reads as its staleness "
                + "alarm. Adding the row to that entry would have meant loosening its second "
                + "condition, which the owner's 2026-09-14 ruling for this file forbids. What reads "
                + "the letter is the NEGATIVE LOOKBEHIND `(?<!(?:a|\\p{ASCII})+)`, and a lookaround "
                + "is not part of any match by construction.\n"
                + "THE MECHANISM IS THE SET INSIDE THAT LOOKBEHIND, isolated to one line and not "
                + "inferred from the row. Under IGNORECASE|FULLCASE `[a\\p{ASCII}]` MATCHES U+0131 "
                + "while `a`, `[a]`, `[ab]`, `\\p{ASCII}` and `[\\p{ASCII}]` all answer None.\n"
                + "WHAT SWITCHES THE EXPANSION ON IS A SECOND MEMBER, and that was MEASURED after "
                + "a first draft got it wrong. The draft said \"a set is expanded by its members' "
                + "case partners\"; `[\\p{ASCII}]` refutes it, because that set's member holds `I` "
                + "and it reaches nothing. What actually divides the cells is how many members the "
                + "set has: `[\\p{ASCII}]` answers None and `[\\p{ASCII}\\p{ASCII}]` - the SAME "
                + "member twice, so the same characters - MATCHES, as do `[\\p{ASCII}z]` and "
                + "`[a\\p{ASCII}]`. A one-member set behaves like the bare property and a "
                + "two-member one case-expands the property's contents. `[ab]` reaching nothing "
                + "says it is the PROPERTY's members expanding rather than sets in general.\n"
                + "AND THE EXPANSION IS NOT ITSELF THE DEFECT, which is the control that makes "
                + "this the `T` rows rather than a set-folding bug. A multi-member set holding "
                + "`\\p{ASCII}` reaches every character whose partner is ASCII: U+212A KELVIN SIGN "
                + "(`212A; C; 006B`) and U+017F LATIN SMALL LETTER LONG S (`017F; C; 0073`) as "
                + "well as U+0131 (`0049; T; 0131`) and U+0130 (`0130; T; 0069`), and it reaches "
                + "neither U+00C5 nor U+00F1, whose partners are not ASCII. THIS PORT AGREES ON "
                + "THE TWO `C` ROWS AND REFUSES THE TWO `T` ROWS: over the 42-cell grid of those "
                + "six characters against seven spellings, 36 cells AGREE and the only six that "
                + "diverge are U+0131 and U+0130 against the three MULTI-MEMBER spellings. So both "
                + "the expansion and the one-member exception are shared behaviour, and the `T` "
                + "rows are the whole of the difference.\n"
                + "THE FOUR-SWAP CONTROL OVER THE WHOLE ROW SAYS THE SAME THING, and EVERY SWAP "
                + "MUST STAY NON-ASCII: the lookbehind reads ASCII-ness, so the obvious swaps - "
                + "`h` and `i` - change the question rather than removing the `T` row, and BOTH of "
                + "them reproduce upstream's count for that reason. A first draft of this "
                + "judgement used `h` and read the agreement as \"not Turkic after all\"; the "
                + "confound is recorded because the next reader will reach for `h` too. U+00F1, "
                + "U+01E7, U+0125 and U+00E5 are non-ASCII, fold to ONE character and carry no "
                + "`T` row: on all four the two engines AGREE, and only the dotless small i "
                + "diverges. Dropping IGNORECASE agrees too.\n"
                + "A FIRST DRAFT OF THIS ENTRY NAMED THE WRONG LOOKAROUND - the inner "
                + "`(?<=ı[\\w\\s])`, which decides the conditional - and the blind review "
                + "killed it by measuring: neutering that lookbehind leaves the engines still "
                + "disagreeing (upstream 3, this port 1), while deleting only the `a|` from the "
                + "outer one makes upstream agree with this port (1 and 1). The judgement did not "
                + "move - it is the `T` rows either way - but the construct did, and the "
                + "one-line set control above is what replaced the guess.\n"
                + "Measured 2026-09-15 on regex 2026.9.10, "
                + "`python tools/probes/upstream-turkic-without-spans.py`, in the two sections "
                + "headed `seed 20260915 row 88716` and `the MECHANISM of the lookaround row`.\n"
                + "KEYED ON THE ROW AND ON THIS PORT'S EXACT ANSWER, like its two row-keyed "
                + "siblings. There is no `scanMatches` clause because there is nothing for one to "
                + "read - that absence is the entry's reason for existing - so this entry is "
                + "narrower than its siblings by one guard, and it widens only by judging another "
                + "row with the probe.",
            PinnedBy: "Gaps.Engine.CaseFoldingTests.A_set_union_reaches_the_case_partners_of_its_"
                + "members_but_not_through_a_Turkic_row",
            Example: _turkicLookaroundRows,
            Applies: static (row, ours) =>
                _turkicLookaround.TryGetValue(Question(row), out string? judged)
                && string.Equals(ours.Describe(), judged, StringComparison.Ordinal)
        ),
        new(
            Id: "fuzzy-changes-leaked-from-an-abandoned-attempt",
            Reason: "UPSTREAM IS WRONG AND THIS PORT DIVERGES ON PURPOSE - ledger entry 11 "
                + "mechanism A, fixed here by S47 on 2026-09-14 under the owner's inherited-bug rule "
                + "(2026-09-12). `basic_match`'s `start_match` clears `state->fuzzy_counts` with a "
                + "`memset` and leaves `state->fuzzy_changes` alone "
                + "(upstream/src/_regex.c:11790-11792), so a search attempt abandoned without "
                + "unwinding - which is what `(*PRUNE)` and `(*SKIP)` do, and what every restart of a "
                + "scan does - leaves its entries at the BOTTOM of the change stack. "
                + "`match_fuzzy_changes` then reports the FIRST `sum(fuzzy_counts)` entries (:20522), "
                + "so a stale entry does not merely sit there unread: it DISPLACES the change the "
                + "winning attempt recorded. The counts survive because they are cleared; the list "
                + "does not because it is not.\n"
                + "THE JUDGEMENT NEEDS NO SECOND ENGINE, and there is none to ask - PCRE2, Perl and "
                + ".NET have no approximate matching at all (measured for ledger entry 12, "
                + "tools/probes/pcre2-has-no-fuzzy-matching.py). It rests on upstream's own "
                + "documentation and on upstream's own control. `fuzzy_changes` is documented as "
                + "\"a tuple of the positions of the substitutions, insertions and deletions\" of the "
                + "match `fuzzy_counts` counts, so the two are views of ONE edit script and cannot "
                + "both be right when they disagree. Upstream's control is the same pattern with the "
                + "verb deleted, where the abandoned attempt unwinds the ordinary way: "
                + "`(?:[ab][bc][wx]){e<=2}` over 'qab' gives upstream (0,0,1) with a DELETION at 3, "
                + "which is this port's answer, where `(?:[ab][bc](*PRUNE)[wx]){e<=2}` over the same "
                + "subject gives upstream (0,0,1) with a SUBSTITUTION at 0 - one deletion counted, "
                + "one substitution reported.\n"
                + "KEYED ON THE RECORDED `leakFreeFuzzy`, and NOT on the shape of the disagreement, "
                + "because the shape is exactly what cannot be judged. On every row of this family "
                + "the two engines agree on the span, the groups AND the fuzzy counts and differ only "
                + "over where the errors were spent, so a predicate saying 'same counts, different "
                + "positions' would swallow precisely the defect the oracle exists to catch. What "
                + "does distinguish it is upstream's OWN answer with the leak taken away: the "
                + "recorder asks `match(pos=start, endpos=end)`, which makes the winning attempt "
                + "upstream's FIRST attempt, so no earlier attempt exists to have left anything "
                + "behind (`_leak_free_fuzzy` in tools/record-oracle.py).\n"
                + "TWO ARMS, OF VERY DIFFERENT STRENGTH, and the second is the widest thing in this "
                + "file. STRONG: upstream answered the anchored question, and this port's fuzzy half "
                + "is upstream's own leak-free answer exactly, counts and positions. Measured over "
                + "the three-seed 6000-row gate of 2026-09-14 - 19 rows, 23 diverging matches - "
                + "upstream's leak-free answer is this port's answer on all 15 it would answer, with "
                + "no exception (.scratch/anchored.py, reproduced in the S47 closing notes). WEAK: "
                + "upstream would NOT answer the anchored question, so nothing can be demanded of it "
                + "and the entry accepts this port's positions on that match alone. Eight of the 23 "
                + "are there, in four rows, and they are three recognisable shapes rather than a "
                + "grab bag - a fuzzy section inside a LOOKAHEAD, which has to read past `endpos`; a "
                + "`\\K`, whose reported start is not where the attempt began; and a scan's second "
                + "match at a position an earlier match already used. WHAT THE WEAK ARM MASKS, said "
                + "out loud: a port defect in the change POSITIONS on a row of one of those three "
                + "shapes, where the counts and everything else are right, is classified rather than "
                + "reported. It fires about 1.3 times per 126,000-row seed. The instrument that "
                + "covers it is not this list - it is "
                + "`OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts`, a "
                + "property of this port's answers alone over every fuzzy match of a whole wave, plus "
                + "the minimised rows in the test named below.\n"
                + "BOTH ARMS ALSO DEMAND that everything but the fuzzy half already agrees, that the "
                + "counts agree on every differing match - counts that differ are mechanism B, which "
                + "has its own entry and its own argument - and that this port's own change lists "
                + "agree with its own counts, so a regression of the S47 invariant makes the entry "
                + "silent rather than absorbing the row.",
            PinnedBy: "Gaps.Engine.FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_"
                + "abandoned_attempt_s_errors_into_the_next_one, .A_search_attempt_that_fails_after_"
                + "a_lookaround_leaves_nothing_behind_for_the_next_one and .The_reported_changes_"
                + "agree_with_the_counts_on_every_shape_that_used_to_contradict_them",
            // Two rows, one per arm, so the staleness alarm re-tests both. Row 1 is ledger entry 11's
            // own minimised reproduction and upstream answers the anchored question on it, so it is
            // the STRONG arm; row 2 is row 77766 of the seed-4242 6000-row gate as the wave drew it,
            // a fuzzy section inside a lookahead before a `\K`, where upstream answers nothing
            // anchored - the WEAK arm. Recorded by `python tools/record-oracle.py --rows`, 2026-09-14.
            Example: _leakedChangeRows,
            Applies: static (row, ours) => OnlyTheChangePositionsLeaked(row, ours)
        ),
        new(
            Id: "fuzzy-counts-of-a-partial-are-the-innermost-sections",
            Reason: "UPSTREAM IS WRONG AND THIS PORT DIVERGES ON PURPOSE - ledger entry 11 "
                + "mechanism B, the other half of what S47 fixed. On a PARTIAL match the engine "
                + "returns from inside whichever fuzzy section was still open, and `state->fuzzy_"
                + "counts` at that moment holds THAT section's errors alone: the enclosing sections' "
                + "counts were pushed on the way in and are never merged back, because nothing "
                + "completes. `match_fuzzy_changes` then truncates the change list to "
                + "`sum(fuzzy_counts)` entries (upstream/src/_regex.c:20522), so upstream's reported "
                + "positions are the first few of the real edit script and its counts are the "
                + "innermost section's - two views of one script, and neither is the match's.\n"
                + "THE JUDGEMENT IS UPSTREAM'S OWN TRUNCATION, which is visible on the row without "
                + "any second engine: upstream's change positions are a PREFIX of this port's, per "
                + "kind and in record order, and its counts are componentwise no larger. On seed 7 "
                + "row 120049 - `(?b)(ab)(?:[ab]*?(?:\\Zoba[^a-f](?:\\1)){e<=3}){i<=2}` over "
                + "'abxbaobaya' - upstream reports (0,3,0) with insertions at 2, 5 and 7 and this "
                + "port reports (0,5,0) with insertions at 2, 5, 7, 8 and 9. Upstream's three ARE "
                + "this port's first three, which is what a stack truncated to a wrong total looks "
                + "like and is not what a port computing different positions would look like.\n"
                + "PREDICATE RATHER THAN ROWS, unlike its mechanism-A sibling, because here the "
                + "shape of the disagreement IS the evidence. Measured over the three-seed 6000-row "
                + "gate of 2026-09-14: 39 rows, every one a partial, the prefix relation holding on "
                + "all 35 that carry positions and the remaining 4 POSIX, where neither engine has "
                + "positions at all (ledger entry 9) and only the counts can be compared.\n"
                + "WHERE IT IS WIDE, said out loud: on 18 of those 35 upstream reports NO errors at "
                + "all - the innermost section had spent none - so the prefix it must be is the "
                + "empty one and the entry accepts whatever positions this port reports on that "
                + "match. That is the price of comparing against an engine whose answer is a "
                + "truncation of the truth, and the instruments that cover it are "
                + "`OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` and "
                + "the three partial rows of the test named below, not this list. The entry still "
                + "demands the span, the groups, `lastindex`, `lastgroup` and the partial flag agree "
                + "exactly, that BOTH engines called the match partial, that this port's counts are "
                + "no smaller than upstream's, and that this port's own change lists agree with its "
                + "own counts.",
            PinnedBy: "Gaps.Engine.FuzzyMatchingTests.The_reported_changes_agree_with_the_counts_on_"
                + "every_shape_that_used_to_contradict_them, whose last three rows are partials, and "
                + "OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts",
            // One row per shape the entry has to survive: upstream reporting a shorter prefix of the
            // real script, and upstream reporting nothing at all. Recorded by
            // `python tools/record-oracle.py --rows`, 2026-09-14.
            Example: _innermostPartialCountRows,
            Applies: static (row, ours) => UpstreamCountedOnlyTheInnermostSection(row, ours)
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

    /// <summary>Upstream's <c>IGNORECASE</c> flag bit, which is <c>regex.I</c>.</summary>
    private const int _ignoreCase = 0x2;

    /// <summary>The four dotted and dotless I codepoints, all of them BMP.</summary>
    private static readonly System.Buffers.SearchValues<char> _turkicI = System.Buffers.SearchValues.Create("Iiİı");

    /// <summary>
    /// Whether the divergence COVERS one of the four dotted or dotless I codepoints, with
    /// IGNORECASE in force and with a dotted or dotless form somewhere in the row - the shape
    /// every judged row of <c>turkic-default-folding</c> has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without IGNORECASE no case data is consulted at all, so no Turkic divergence is possible and
    /// the first clause is not a convenience.
    /// </para>
    /// <para>
    /// <b>The second clause is what stops this classifying an unrelated defect, and it was added
    /// because the first S45 blind pass reproduced exactly that.</b> A span test alone accepts a
    /// match covering plain ASCII <c>i</c>, which is most of a case-folding wave; the reviewer's
    /// probe put a total engine failure on <c>(?i)i.</c> against <c>ix</c> and this entry swallowed
    /// it as EXPECTED. The two Turkic-only rows of <c>CaseFolding.txt</c> are
    /// <c>0049 -&gt; 0131</c> and <c>0130 -&gt; 0069</c>, and each names exactly one of U+0130 and
    /// U+0131, so a divergence in this family needs one of those two in play.
    /// </para>
    /// <para>
    /// <b>The span test reads EVERY match of BOTH answers, not the first character of the first
    /// match, and that too came from a reproduced finding.</b> The second blind pass ran
    /// <c>fullmatch('aI', 'aı', I)</c> - the case-folding generator's commonest shape - where
    /// upstream matches <c>(0, 2)</c> beginning on <c>a</c> and this port does not match at all, and
    /// a start-character test missed it; likewise <c>finditer('[^I]', 'aıb')</c>, where the
    /// extra match this port finds is the SECOND one.
    /// </para>
    /// <para>
    /// <b>TWO LIMITS, both reproduced by the second blind pass and both left in place.</b>
    /// </para>
    /// <list type="number">
    /// <item>
    /// FALSE NEGATIVES, which are safe: the second clause reads the pattern as text, so a row that
    /// reaches U+0130 or U+0131 by an escape (<c>ı</c>), by <c>\N{LATIN SMALL LETTER DOTLESS
    /// I}</c>, through a <c>\L&lt;name&gt;</c> list, or by a RANGE that spans it without naming it
    /// (<c>[į-Ĳ]</c>) is NOT classified and reddens the run. Someone then judges it,
    /// which is the right way round. The <c>interactions</c> generator does draw
    /// <c>\L&lt;name&gt;</c>, so this is reachable, not hypothetical.
    /// </item>
    /// <item>
    /// <b>THE FALSE POSITIVE S45 RECORDED AS UNCLOSABLE IS CLOSED, by asking what the PATTERN offers
    /// rather than what the two answers say (S47b).</b> Upstream can only diverge here by USING a
    /// <c>T</c> row, and a <c>T</c> row is a PAIRING: it is worth something only where the pattern
    /// offers one side and the answer lands on the other. S45's probe is a fabricated total failure
    /// on <c>(?i)ı.</c> against <c>ıx</c>, where the pattern offers U+0131 and the answer lands on
    /// U+0131 - the same letter, which every engine matches without reading the case data at all -
    /// so the row is refused. S45 looked for the discriminator in the answers, where there is none:
    /// a fabricated total failure and a real one are the same two answers.
    /// </item>
    /// <item>
    /// <b>THREE REAL WAVE ROWS SAY WHAT THE RULE IS NOT, and every one of them was MEASURED, red
    /// against a draft of this predicate before it read the way it now does.</b>
    /// <list type="bullet">
    /// <item>
    /// Not "the pattern does not spell the letter": row 50168 of the seed-99991 6000-row
    /// <c>case-folding</c> gate is <c>match('iiİ', 'iİ', I|F)</c>, where the pattern spells the
    /// <c>i</c> upstream's span lands on AND spells the <c>İ</c> whose <c>T</c> row is the whole
    /// divergence. A <c>T</c> row is a PAIRING, so the test has to be about pairs.
    /// </item>
    /// <item>
    /// Not the pairing alone: row 6150 of the seed-31337 <c>interactions</c> wave spells <c>ı</c>
    /// and reaches the subject's <c>ı</c> through an <c>[A-Z]</c>, which pairs with nothing in the
    /// pattern's own text. Minimised as <c>match('ıı', 'ı[A-Z]', I)</c>.
    /// </item>
    /// <item>
    /// And not a LIST of folding constructs either: row 52004 of the same gate is
    /// <c>match('Iıi', r'(i)\1', I|F)</c>, where the pairing is between a backreference and the
    /// subject, so a list naming classes, properties and named lists misses it. Hence the two
    /// characters <c>[</c> and <c>\</c>, which every such construct starts with.
    /// </item>
    /// </list>
    /// </item>
    /// </list>
    /// <para>
    /// All four codepoints are in the BMP, so a UTF-16 index into the subject addresses one whole
    /// character and no surrogate arithmetic is needed; <c>OracleGroup.Index</c> is UTF-16 on both
    /// sides.
    /// </para>
    /// </remarks>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns><see langword="true"/> if the divergence belongs to the family.</returns>
    private static bool DivergenceStartsOnATurkicI(OracleRow row, IOracleOutcome ours)
    {
        if ((row.Flags & _ignoreCase) == 0 && !row.Pattern.Contains("(?i", StringComparison.Ordinal))
        {
            return false;
        }

        if (!HoldsADottedOrDotlessI(row.Pattern) && !HoldsADottedOrDotlessI(row.Subject))
        {
            return false;
        }

        // S47b: upstream can only diverge here by USING a `T` row, so the pattern has to offer one
        // side of a `T` pair where the answer landed on the other - or a construct that folds. A
        // pattern that writes U+0131 and an answer that lands on U+0131 pair the letter with itself,
        // which every engine matches without reading the case data at all.
        // A ROW WHOSE ANSWER CARRIES NO SPANS IS NOT CLASSIFIED HERE - see the sibling entry
        // `turkic-default-folding-without-spans`, which is keyed on rows for a measured reason. A
        // draft of S52 fed the recorder's `scanMatches` into this union instead, and its blind review
        // reproduced exactly the false positive S45 and S47b each spent a pass closing: a fabricated
        // total failure on `(?i)\w` over 'xı' - where `\w` matches U+0131 in every engine without
        // consulting a `T` row at all - was tallied EXPECTED, because `CanFoldIntoALetterItDoesNot
        // Spell` short-circuits on the `\` and the whole SCAN, rather than the diverging match, was
        // supplying the covered letter.
        char[] covered =
        [
            .. TurkicLettersCovered(row.Subject, row.Expected).Concat(TurkicLettersCovered(row.Subject, ours)),
        ];

        if (CanFoldIntoALetterItDoesNotSpell(row.Pattern))
        {
            return covered.Length > 0;
        }

        string spelled = PatternTextThatIsMatchedRatherThanRead(row.Pattern);

        return Array.Exists(covered, letter => spelled.Contains(TurkicPartnerOf(letter), StringComparison.Ordinal));
    }

    /// <summary>
    /// The pattern with every <c>(?...</c> HEADER blanked out, so what is left is the text the
    /// pattern can actually match a character against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pairing test above reads this and not the raw pattern, because the raw pattern carries
    /// letters nothing ever matches - and both of S47b's blind passes found a Turkic false positive
    /// walking back in through one.</b> The first pass: <c>TurkicPartnerOf('İ')</c> is <c>i</c> and
    /// <c>(?i)</c> puts an <c>i</c> in the text, so a fabricated total failure on <c>(?i)İ.</c>
    /// against <c>İx</c> still classified while its dotless original was refused. The second pass,
    /// over the fix for the first: a group NAME or a COMMENT does it too -
    /// <c>(?P&lt;I&gt;ı).</c>, <c>(?&lt;I&gt;ı).</c>, <c>(?P&lt;i&gt;İ).</c> and <c>(?#I)ı.</c> all
    /// classified, and the doc comment written with the first fix claimed in as many words that a
    /// group name could not do this. Both passes are pinned as rows of
    /// <c>OracleWaveTests._turkicRows</c>, asserted together, so the next fix cannot pass one shape
    /// and fail its twin.
    /// </para>
    /// <para>
    /// <b>ONE RULE, because the enumeration was what kept being incomplete.</b> Blank from
    /// <c>(?</c> up to and including the first <c>)</c>, <c>:</c>, <c>&gt;</c> or <c>'</c>. That
    /// covers a flag group (<c>(?i)</c>, <c>(?fi:</c>, <c>(?V1)</c>, <c>(?-i:</c>), a non-capturing
    /// group (<c>(?:</c>), an atomic one (<c>(?&gt;</c>), a name in any of its three spellings
    /// (<c>(?P&lt;g&gt;</c>, <c>(?&lt;g&gt;</c>, <c>(?'g'</c>), a named backreference or call
    /// (<c>(?P=g)</c>, <c>(?P&gt;g)</c>, <c>(?&amp;g)</c>), a comment (<c>(?#...)</c>) and a
    /// conditional's condition (<c>(?(1)</c>) - and in each case leaves the BODY, which is real
    /// matched text, alone.
    /// </para>
    /// <para>
    /// The one exception is a LOOKAROUND, whose body starts immediately: <c>(?=</c>, <c>(?!</c>,
    /// <c>(?&lt;=</c> and <c>(?&lt;!</c> are skipped, or the <c>&gt;</c> rule would eat the first
    /// character of a lookbehind's body. Hand-written rather than a
    /// <c>System.Text.RegularExpressions</c> call so the exception is visible as three comparisons
    /// rather than hidden in a pattern.
    /// </para>
    /// </remarks>
    /// <param name="pattern">The row's pattern.</param>
    /// <returns>The pattern with those headers replaced by spaces, or the pattern itself if it has none.</returns>
    private static string PatternTextThatIsMatchedRatherThanRead(string pattern)
    {
        int open = pattern.IndexOf("(?", StringComparison.Ordinal);

        if (open < 0)
        {
            return pattern;
        }

        char[] text = pattern.ToCharArray();

        while (open >= 0)
        {
            if (!IsLookaround(pattern, open))
            {
                for (int i = open + 2; i < text.Length; i++)
                {
                    if (text[i] is ')' or ':' or '>' or '\'')
                    {
                        Array.Fill(text, ' ', open, i - open + 1);
                        break;
                    }
                }
            }

            open = pattern.IndexOf("(?", open + 2, StringComparison.Ordinal);
        }

        return new string(text);
    }

    /// <summary>Whether the <c>(?</c> at this position opens a lookaround, whose body starts at once.</summary>
    /// <param name="pattern">The row's pattern.</param>
    /// <param name="open">The index of the <c>(</c>.</param>
    /// <returns><see langword="true"/> for <c>(?=</c>, <c>(?!</c>, <c>(?&lt;=</c> and <c>(?&lt;!</c>.</returns>
    private static bool IsLookaround(string pattern, int open)
    {
        int i = open + 2;

        if (i < pattern.Length && pattern[i] == '<')
        {
            i++;
        }

        return i < pattern.Length && (pattern[i] == '=' || pattern[i] == '!');
    }

    /// <summary>The letter <c>CaseFolding.txt</c>'s two <c>T</c> rows pair one of the four with.</summary>
    /// <remarks>
    /// The rows are <c>0049; T; 0131</c> and <c>0130; T; 0069</c>, so the pairs are I with U+0131
    /// and U+0130 with i, each read both ways. Called only on a letter that came out of
    /// <see cref="TurkicLettersInSpan"/>, so the default arm is unreachable and returns the letter
    /// itself rather than inventing one.
    /// </remarks>
    /// <param name="letter">One of the four.</param>
    /// <returns>Its Turkic partner.</returns>
    private static char TurkicPartnerOf(char letter) =>
        letter switch
        {
            'I' => 'ı',
            'ı' => 'I',
            'İ' => 'i',
            'i' => 'İ',
            _ => letter,
        };

    /// <summary>
    /// Whether the pattern can match a character it does not spell out, and so can reach one of the
    /// four through the case data rather than literally.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two characters answer nearly all of it, and the breadth is deliberate: a class folds its
    /// members, a property matches the other case under IGNORECASE, a named list and a
    /// BACKREFERENCE carry text from outside the pattern's own spelling, and <c>\N{...}</c> names a
    /// character rather than writing it. Every one of those begins with <c>[</c> or <c>\</c>, and
    /// enumerating them instead is how this test got three rows wrong before it was written this
    /// way - a list with classes and properties on it missed row 52004 of the seed-99991 gate,
    /// <c>match('Iıi', r'(i)\1', I|F)</c>, where the <c>T</c> row is consulted by a backreference.
    /// </para>
    /// <para>
    /// <b>The NAMED spellings are the exception, and S47b's second blind pass found them by taking
    /// that same row's named twin.</b> <c>(?P=g)</c>, <c>(?P&gt;g)</c>, <c>(?&amp;g)</c> and
    /// <c>(?R)</c> reach text the pattern does not spell and carry neither <c>[</c> nor <c>\</c>, so
    /// <c>match('Iıi', '(?P&lt;g&gt;i)(?P=g)', I|F)</c> - upstream (0, 2), no match here, the same
    /// family as row 52004 in every respect - was left unclassified and would have reddened a wave.
    /// Four prefixes, not a rule, because that is the whole of the list: every other construct that
    /// can reach an unspelled character is already caught by the two characters above.
    /// </para>
    /// <para>
    /// <b>So the pairing test below bites only on a pattern of literals and dots</b>, which is
    /// exactly where S45's recorded false positive lives and is the honest boundary: on a pattern
    /// holding a class or an escape, nothing about the row and the two answers can say whether the
    /// case data was consulted, and this list guesses in the direction that keeps real family rows.
    /// A DOT is deliberately not on it - it matches any character whatever the case data says, so no
    /// divergence can come out of one, and admitting it would re-open S45's probe, <c>(?i)ı.</c>
    /// </para>
    /// </remarks>
    /// <param name="pattern">The row's pattern.</param>
    /// <returns><see langword="true"/> if the pattern has such a construct.</returns>
    private static bool CanFoldIntoALetterItDoesNotSpell(string pattern) =>
        pattern.Contains('[', StringComparison.Ordinal)
        || pattern.Contains('\\', StringComparison.Ordinal)
        || pattern.Contains("(?P=", StringComparison.Ordinal)
        || pattern.Contains("(?P>", StringComparison.Ordinal)
        || pattern.Contains("(?&", StringComparison.Ordinal)
        || pattern.Contains("(?R", StringComparison.Ordinal);

    /// <summary>Whether the text holds U+0130 or U+0131, the two codepoints only this family uses.</summary>
    /// <param name="text">The pattern or the subject.</param>
    /// <returns><see langword="true"/> if either appears.</returns>
    private static bool HoldsADottedOrDotlessI(string text) =>
        text.Contains('İ', StringComparison.Ordinal) || text.Contains('ı', StringComparison.Ordinal);

    /// <summary>Every one of the four that a span of an answer covers.</summary>
    /// <remarks>
    /// A zero-width match covers nothing, so the character AT its position is read as well: a
    /// lookaround or an empty alternative can diverge on a character it never consumes.
    /// </remarks>
    /// <param name="subject">The row's subject.</param>
    /// <param name="outcome">One engine's answer.</param>
    /// <returns>The letters it matched over or at, with duplicates.</returns>
    private static IEnumerable<char> TurkicLettersCovered(string subject, IOracleOutcome outcome)
    {
        IEnumerable<MatchOutcome> matches = outcome switch
        {
            MatchOutcome match => [match],
            MatchesOutcome scan => scan.Matches,
            _ => [],
        };

        return matches
            .Select(static match => match.Groups)
            .Where(static groups => groups.Count > 0 && groups[0].Success)
            .SelectMany(groups => TurkicLettersInSpan(subject, groups[0].Index, groups[0].Length));
    }

    /// <summary>Every one of the four in one span of the subject.</summary>
    /// <param name="subject">The row's subject.</param>
    /// <param name="index">The span's start, in UTF-16 code units.</param>
    /// <param name="length">Its length; a zero-width span is read as the one character at it.</param>
    /// <returns>The letters in it, with duplicates.</returns>
    private static IEnumerable<char> TurkicLettersInSpan(string subject, int index, int length)
    {
        int start = Math.Clamp(index, 0, subject.Length);
        int end = Math.Clamp(index + Math.Max(1, length), start, subject.Length);

        for (int i = start; i < end; i++)
        {
            if (_turkicI.Contains(subject[i]))
            {
                yield return subject[i];
            }
        }
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

    /// <summary>
    /// Whether this port's scan is a prefix of upstream's and every match upstream reports beyond it
    /// is one the row itself refutes - either by carrying a capture outside its own span, or by
    /// ending where the pattern's trailing <c>$</c> cannot hold in the real subject.
    /// </summary>
    /// <param name="row">The row, for the pattern and the subject the tells are read against.</param>
    /// <param name="upstream">Upstream's scan.</param>
    /// <param name="ours">This port's scan.</param>
    /// <returns><see langword="true"/> if that is the whole of the difference.</returns>
    private static bool OnlyExtraMatchesTheRowItselfRefutes(OracleRow row, MatchesOutcome upstream, MatchesOutcome ours)
    {
        if (upstream.Matches.Count <= ours.Matches.Count)
        {
            return false;
        }

        // Rendering for rendering, so a difference in any span, capture, `lastindex` or `lastgroup`
        // among the matches the two scans share is reported rather than classified.
        for (int m = 0; m < ours.Matches.Count; m++)
        {
            if (!string.Equals(upstream.Matches[m].Describe(), ours.Matches[m].Describe(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (int m = ours.Matches.Count; m < upstream.Matches.Count; m++)
        {
            MatchOutcome extra = upstream.Matches[m];
            if (!CarriesACaptureOutsideItself(row.Pattern, extra) && !EndsWhereATrailingDollarIsFalse(row, extra))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether upstream's own stepwise walk of the same scan is this port's answer to it, which
    /// makes every match upstream's stateful scanner reports beyond this port's one that upstream
    /// itself does not find from a fresh state.
    /// </summary>
    /// <param name="row">The row, for the recorded walk.</param>
    /// <param name="upstream">Upstream's scan.</param>
    /// <param name="ours">This port's scan.</param>
    /// <returns><see langword="true"/> if upstream contradicts itself and this port agrees with it.</returns>
    /// <remarks>
    /// The same discriminator <c>overlapped-skip-stale-slice</c> uses, and for the same reason: the
    /// walk is upstream's own single-shot door, so the entry demands that upstream's scanner
    /// contradicts upstream's matcher AND that this port agrees with the matcher, rendering for
    /// rendering. A defect in this port's scan makes the second condition false and is reported.
    /// <para>
    /// The extra count check is what keeps this arm to the EXTRA MATCH shape rather than the moved
    /// span one, which <c>overlapped-skip-stale-slice-reversed</c> owns: a reversed row whose scans
    /// are the same length and differ in a span falls to that entry instead.
    /// </para>
    /// </remarks>
    private static bool UpstreamsOwnWalkIsThisPortsScan(OracleRow row, MatchesOutcome upstream, MatchesOutcome ours) =>
        row.AnchoredScan is { } anchored
        && upstream.Matches.Count > ours.Matches.Count
        && !string.Equals(upstream.Describe(), anchored.Describe(), StringComparison.Ordinal)
        && string.Equals(ours.Describe(), anchored.Describe(), StringComparison.Ordinal);

    /// <summary>
    /// Whether a substitution upstream made more times than this port did replaced, for every extra
    /// time, at a span the row itself refutes - the same two tells
    /// <see cref="OnlyExtraMatchesTheRowItselfRefutes"/> reads, at the spans the recorder's
    /// <c>subMatches</c> carries.
    /// </summary>
    /// <param name="row">The row, for the pattern, the subject and the recorded spans.</param>
    /// <param name="upstream">Upstream's substitution.</param>
    /// <param name="ours">This port's substitution.</param>
    /// <returns><see langword="true"/> if that is the whole of the difference.</returns>
    /// <remarks>
    /// The length check comes first and is not a formality: <c>subMatches</c> is a SECOND question
    /// asked of upstream - the same scan through <c>finditer</c> rather than through <c>subn</c> -
    /// so a row where the two answers disagree about how many times upstream replaced is a row this
    /// entry has no business reading, and it is reported instead. It also caught a real defect on
    /// the day it was written: the recorder truncated the list by the row's raw <c>count</c>, where
    /// a NEGATIVE count means upstream replaces nothing rather than "all but the last".
    /// <para>
    /// <b>The window assumes this port's replacements are upstream's first <c>ours.Count</c>, and
    /// they need not be</b> - S40d's blind review built one where they are not:
    /// <c>(?r)(?:\d*?(*SKIP)b|a)$</c> over 'a\nbbb\na' has this port replacing at (6, 7), (4, 5) and
    /// (0, 1) where upstream's first three are (6, 7), (4, 5) and (3, 4). What keeps that from
    /// classifying a defect is the tell rather than the window: a span this port itself replaced at
    /// ends where the trailing <c>$</c> IS true, so <see cref="EndsWhereATrailingDollarIsFalse"/>
    /// refuses it and the row is reported. Ten such rows were built and run, and all ten were
    /// reported (2026-09-13).
    /// </para>
    /// </remarks>
    private static bool OnlyExtraReplacementsTheRowItselfRefutes(OracleRow row, SubOutcome upstream, SubOutcome ours)
    {
        if (row.SubMatches is not { } replacements || replacements.Matches.Count != upstream.Count)
        {
            return false;
        }

        if (ours.Count >= upstream.Count || ours.Count < 0)
        {
            return false;
        }

        for (int m = ours.Count; m < replacements.Matches.Count; m++)
        {
            MatchOutcome extra = replacements.Matches[m];
            if (!CarriesACaptureOutsideItself(row.Pattern, extra) && !EndsWhereATrailingDollarIsFalse(row, extra))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a match records a capture that lies outside its own span, which nothing but a
    /// POSITIVE lookaround or a <c>\K</c> can justify - and the pattern has neither.
    /// </summary>
    /// <param name="pattern">The pattern, for the precondition.</param>
    /// <param name="match">The match.</param>
    /// <returns><see langword="true"/> if it contradicts itself this way.</returns>
    /// <remarks>
    /// <para>
    /// <b><c>\K</c> is in the exclusion list because the S36 blind review put it there, and the entry
    /// above overstated its own safety until it did.</b> <c>\K</c> moves the reported match start, so
    /// a group captured before it lies outside the match and this port produces that itself:
    /// <c>new FuzzyRegex(@"(?r)ab\K(cd)(*SKIP)").Matches("abcdabcd", overlapped: true)</c> gives
    /// (4, 6) with group 1 at (6, 8), and (0, 2) with group 1 at (2, 4). Without this clause a port
    /// defect that ended such a scan one match early would have been classified.
    /// </para>
    /// <para>
    /// <b>A NEGATIVE lookaround is not in the exclusion list, and S37 took it out.</b> The list read
    /// <c>(?=</c>, <c>(?!</c> and either lookbehind, which is wider than the justification: a
    /// negative lookaround only succeeds when its body FAILS, so nothing it matched survives into
    /// the match and it cannot put a capture anywhere. Measured on 2026-09-12 against regex
    /// 2026.7.19 and against this port, which agree on all four rows
    /// (<c>tools/probes/upstream-negative-lookaround-captures.py</c>, and rows 9 and 10 of the S37
    /// candidate replay):
    /// <code>
    /// regex.search(r'(?!(a))b', 'b')          # (0, 1), group 1 spans []
    /// regex.search(r'(?&lt;!(a))b', 'b')         # (0, 1), group 1 spans []
    /// regex.search(r'(a)(?!(?:(b))x)b', 'ab') # (0, 2), group 1 [(0, 1)], group 2 []
    /// </code>
    /// The row that paid for it is seed 7's row 5543 of a 6000-row <c>interactions</c> wave, a
    /// reversed overlapped <c>(*SKIP)</c> scan whose extra match carries <c>g2</c> at (5, 6) for a
    /// match spanning (3, 5). Deleting the verb, or making it <c>(*PRUNE)</c>, removes the extra
    /// match; writing the called group out leaves it; and upstream's own <c>search</c> and
    /// <c>match</c> at every position answer (3, 6) and never (3, 5). So it is
    /// <c>overlapped-skip-extra-match-reversed</c> exactly, and the only thing keeping it out was the
    /// <c>(?&lt;!</c> in its pattern.
    /// </para>
    /// </remarks>
    private static bool CarriesACaptureOutsideItself(string pattern, MatchOutcome match)
    {
        // Whitespace goes first, because under `(?x)` upstream reads the character after `(?<` with
        // `source.get()` rather than `get(True)` (upstream/regex/_regex_core.py:863), so `(?<  =(a))`
        // IS a positive lookbehind and holds a capture that can lie outside the match:
        // `regex.compile(r'(?x)(?<  =(a))b').search('ab')` is (1, 2) with group 1 at (0, 1), and this
        // port parses it the same way. The S37 blind review built a row on that spelling and watched
        // it be classified while the unspaced twin was reported. No generator emits it, but the test
        // below is the only thing standing between an engine defect and a silent classification, so
        // it reads a pattern with every space removed. That is conservative in the safe direction:
        // stripping can only ADD an exclusion, never remove one.
        string bare = new([.. pattern.Where(static character => !char.IsWhiteSpace(character))]);

        if (
            bare.Contains("(?=", StringComparison.Ordinal)
            || bare.Contains("(?<=", StringComparison.Ordinal)
            || bare.Contains(@"\K", StringComparison.Ordinal)
            || match.Groups.Count == 0
        )
        {
            return false;
        }

        OracleGroup whole = match.Groups[0];
        int start = whole.Index;
        int end = whole.Index + whole.Length;

        return match
            .Groups.Skip(1)
            .Where(static group => group.Success)
            .SelectMany(static group => group.Captures)
            .Any(capture => capture.Index < start || capture.Index + capture.Length > end);
    }

    /// <summary>
    /// Whether a match ends where the pattern's trailing <c>$</c> cannot hold in the real subject,
    /// which is what a <c>(*SKIP)</c>-moved <c>slice_end</c> makes upstream believe.
    /// </summary>
    /// <param name="row">The row, for the pattern and the subject.</param>
    /// <param name="match">The match.</param>
    /// <returns><see langword="true"/> if it contradicts itself this way.</returns>
    /// <remarks>
    /// Deliberately conservative at both ends. The <c>$</c> has to be the last thing in the pattern,
    /// unescaped, so that it is the match END it constrains rather than some position inside it; and
    /// the character the match ends on has to be one no engine reads as a line separator, so the
    /// verdict does not depend on which set of them upstream recognises. <c>\Z</c> and <c>\z</c> are
    /// not read: no row has needed them, and a row that does should red the run and be judged.
    /// </remarks>
    private static bool EndsWhereATrailingDollarIsFalse(OracleRow row, MatchOutcome match)
    {
        if (match.Groups.Count == 0 || !EndsWithAnUnescapedDollar(row.Pattern))
        {
            return false;
        }

        int end = match.Groups[0].Index + match.Groups[0].Length;

        return end < row.Subject.Length && !IsALineSeparatorAnywhere(row.Subject[end]);
    }

    /// <summary>
    /// Whether a character is one that any engine might read as ending a line. The union rather than
    /// upstream's own set, so a verdict above does not depend on which of them <c>$</c> recognises.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> if some engine would treat it as a line separator.</returns>
    private static bool IsALineSeparatorAnywhere(char character) =>
        character
            is '\n'
                or '\r'
                or (char)0x000b // VT
                or (char)0x000c // FF
                or (char)0x0085 // NEL
                or (char)0x2028 // LINE SEPARATOR
                or (char)0x2029; // PARAGRAPH SEPARATOR

    /// <summary>Whether the pattern's last character is a <c>$</c> that is not itself escaped.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> if it ends in a live <c>$</c>.</returns>
    private static bool EndsWithAnUnescapedDollar(string pattern)
    {
        if (!pattern.EndsWith('$'))
        {
            return false;
        }

        int backslashes = 0;
        for (int i = pattern.Length - 2; i >= 0 && pattern[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 == 0;
    }

    /// <summary>
    /// Whether the pattern contains a lookbehind, which is what makes a called group in an otherwise
    /// forward pattern run the other way round from the pattern that called it.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> if it contains one.</returns>
    private static bool HasLookbehind(string pattern) =>
        pattern.Contains("(?<=", StringComparison.Ordinal) || pattern.Contains("(?<!", StringComparison.Ordinal);

    /// <summary>
    /// Whether the pattern holds a lookaround that runs the other way round from the pattern itself,
    /// which is the direction mismatch a called group inherits.
    /// </summary>
    /// <param name="row">The row, for the pattern and for whether it is reversed.</param>
    /// <returns><see langword="true"/> if it does.</returns>
    /// <remarks>
    /// Narrower than <c>group-call-direction</c>'s <c>IsReversed(row) || HasLookbehind(pattern)</c>,
    /// and deliberately so: in a REVERSED pattern the mismatching lookaround is a LOOKAHEAD, and in a
    /// forward pattern it is a LOOKBEHIND. What it does not check is that the call is INSIDE that
    /// lookaround, which the row cannot say without a parser; a pattern holding both separately would
    /// satisfy this and has to be caught by the rest of the predicate.
    /// </remarks>
    private static bool CallsThroughAnOppositeDirectionLookaround(OracleRow row) =>
        IsReversed(row)
            ? row.Pattern.Contains("(?=", StringComparison.Ordinal)
                || row.Pattern.Contains("(?!", StringComparison.Ordinal)
            : HasLookbehind(row.Pattern);

    /// <summary>
    /// Whether upstream found strictly less than this port did, and agreed with it about everything
    /// it DID find.
    /// </summary>
    /// <param name="row">The row, for the subject a substitution is compared against.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns><see langword="true"/> if upstream's answer is this port's answer, minus something.</returns>
    /// <remarks>
    /// Four outcome shapes, one per shape the five judged rows take. A scan where upstream's list is
    /// an element-for-element identical PREFIX of this port's and strictly shorter; a substitution
    /// upstream made none of, leaving the subject untouched; a split upstream did not split, leaving
    /// the subject as its only part; and a single-match row upstream answered None.
    /// <para>
    /// The <c>split</c> arm is here because a fourth seed drew one and NOT before, which is worth
    /// recording: it was left out on purpose - "no row has shown it, and one that does should red the
    /// run and be judged rather than fall into a predicate nobody has tested" - and seed 99991 then
    /// reddened a 6000-row wave with exactly that row. It was judged by the same probe and the arm
    /// added. The single-match arm is the one still unexercised, and it is here because the defect is
    /// per attempt and a <c>search</c> row is one seed away, not as speculative cover.
    /// </para>
    /// </remarks>
    private static bool UpstreamFoundStrictlyLess(OracleRow row, IOracleOutcome ours) =>
        (row.Expected, ours) switch
        {
            (MatchesOutcome theirScan, MatchesOutcome ourScan) => theirScan.Matches.Count < ourScan.Matches.Count
                && !theirScan
                    .Matches.Where(
                        (match, i) =>
                            !string.Equals(match.Describe(), ourScan.Matches[i].Describe(), StringComparison.Ordinal)
                    )
                    .Any(),
            (SubOutcome { Count: 0 } theirs, SubOutcome mine) => mine.Count > 0
                && string.Equals(theirs.Text, row.Subject, StringComparison.Ordinal),
            // The same substitution arm seen from one row further on: this port matched, reached the
            // template, and REFUSED it. Upstream never got that far, because it lost the match, so
            // the row's two answers are a no-op substitution and an exception.
            //
            // What makes it the same family rather than a crash in this port is measured, not
            // assumed (tools/probes/upstream-group-call-loses-matches.py, its last section, 2026-09-13):
            // upstream raises `IndexError: list index out of range` for the identical template the
            // moment it has a match to expand it against - delete the call and
            // `regex.subf(r'(?r)\b(?<g>[ab]+)', '{{{0[-1]}ab{1[2]}', 'ba)((a)((a')` raises - and
            // `{1[2]}` names a third capture of a group that made one. So this port's answer IS
            // upstream's answer to the template; the whole of the divergence is the lost match.
            //
            // Narrow on the exception type, and the hole is worth saying out loud: an
            // ArgumentException thrown from the ENGINE rather than from the template would be
            // classified here. It is bounded by the rest of the predicate - a substitution row whose
            // pattern calls a group through an opposite-direction lookaround, where upstream
            // replaced nothing - and by the arm above it, which is what a non-throwing port hits.
            (
                SubOutcome { Count: 0 } theirs,
                ErrorOutcome { WhileMatching: true, Exception: nameof(ArgumentException) }
            ) => string.Equals(theirs.Text, row.Subject, StringComparison.Ordinal),
            (SplitOutcome theirs, SplitOutcome mine) => theirs.Parts.Count == 1
                && mine.Parts.Count > 1
                && string.Equals(theirs.Parts[0], row.Subject, StringComparison.Ordinal),
            (NoMatchOutcome, MatchOutcome) => true,
            _ => false,
        };

    /// <summary>
    /// Whether a divergence is one of the two cost-ranking families: a row carrying the given
    /// ranking flag, whose cost equation can separate the two rankings at all, where the two answers
    /// differ in nothing but which errors were spent, and this port spent cheaper ones without
    /// spending fewer.
    /// </summary>
    /// <remarks>
    /// The flag is a parameter rather than two copies of this method because the test is the same
    /// test - the two entries differ in what had to change inside the engine, not in what the
    /// divergence looks like from outside. A row carrying both flags reads as <c>BESTMATCH</c> to
    /// the dispatch (<c>:18107</c>) and matches both entries here; <see cref="For"/> returns the
    /// first, which is <c>enhancematch-ranks-by-cost</c>, and either is a true statement about it.
    /// </remarks>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="ours">This port's answer.</param>
    /// <param name="flag">The inline flag the family needs, <c>(?e)</c> or <c>(?b)</c>.</param>
    /// <returns><see langword="true"/> if the divergence belongs to that family.</returns>
    private static bool IsCostRankedDivergence(OracleRow row, IOracleOutcome ours, string flag)
    {
        if (!row.Pattern.Contains(flag, StringComparison.Ordinal) || !TryReadCosts(row.Pattern, out long[]? costs))
        {
            return false;
        }

        return (row.Expected, ours) switch
        {
            (MatchOutcome theirs, MatchOutcome mine) => IsCheaperSameMatch(theirs, mine, costs),
            (MatchesOutcome theirScan, MatchesOutcome ourScan) => theirScan.Matches.Count == ourScan.Matches.Count
                && theirScan.Matches.Count > 0
                && theirScan
                    .Matches.Select((match, i) => (Theirs: match, Mine: ourScan.Matches[i]))
                    .All(pair =>
                        string.Equals(pair.Theirs.Describe(), pair.Mine.Describe(), StringComparison.Ordinal)
                        || IsCheaperSameMatch(pair.Theirs, pair.Mine, costs)
                    ),
            _ => false,
        };
    }

    /// <summary>
    /// Whether two matches say the same thing except for the errors they spent, and this port's cost
    /// less while numbering at least as many.
    /// </summary>
    /// <param name="theirs">Upstream's match.</param>
    /// <param name="mine">This port's match.</param>
    /// <param name="costs">The per-error costs, in <c>SUB</c>, <c>INS</c>, <c>DEL</c> order.</param>
    /// <returns><see langword="true"/> if that is the whole of the difference.</returns>
    private static bool IsCheaperSameMatch(MatchOutcome theirs, MatchOutcome mine, long[] costs)
    {
        if (
            theirs.Fuzzy is null
            || mine.Fuzzy is null
            || !string.Equals(
                (theirs with { Fuzzy = null }).Describe(),
                (mine with { Fuzzy = null }).Describe(),
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        long theirCost = Cost(theirs.Fuzzy, costs);
        long myCost = Cost(mine.Fuzzy, costs);
        int theirErrors = theirs.Fuzzy.Substitutions + theirs.Fuzzy.Insertions + theirs.Fuzzy.Deletions;
        int myErrors = mine.Fuzzy.Substitutions + mine.Fuzzy.Insertions + mine.Fuzzy.Deletions;

        return myCost < theirCost && myErrors >= theirErrors;
    }

    /// <summary>What a match's errors cost under a cost equation.</summary>
    /// <param name="fuzzy">The errors.</param>
    /// <param name="costs">The per-error costs, in <c>SUB</c>, <c>INS</c>, <c>DEL</c> order.</param>
    /// <returns>The total.</returns>
    private static long Cost(OracleFuzzy fuzzy, long[] costs) =>
        (fuzzy.Substitutions * costs[0]) + (fuzzy.Insertions * costs[1]) + (fuzzy.Deletions * costs[2]);

    /// <summary>
    /// Reads the one cost equation a pattern declares, if it declares exactly one that could
    /// separate a cost ranking from an error-count ranking.
    /// </summary>
    /// <remarks>
    /// A cost coefficient is a run of digits immediately followed by <c>s</c>, <c>i</c> or <c>d</c>
    /// and then by <c>+</c> or <c>&lt;</c>, which is what tells <c>{2i+1d+1s&lt;=2}</c> apart from
    /// <c>{s&lt;=1,i&lt;=1,d&lt;=1}</c> - the second sets per-kind CAPS and declares no costs at all.
    /// Anything else - no equation, two of them because the sections nest, or coefficients that are
    /// all equal so that cost is a multiple of the error count - returns <see langword="false"/>, and
    /// the divergence is reported rather than classified.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="costs">The costs, in <c>SUB</c>, <c>INS</c>, <c>DEL</c> order.</param>
    /// <returns><see langword="true"/> if exactly one separating equation was found.</returns>
    private static bool TryReadCosts(string pattern, [NotNullWhen(true)] out long[]? costs)
    {
        costs = null;

        long[] found = [-1, -1, -1];

        for (int i = 0; i < pattern.Length; i++)
        {
            if (!char.IsAsciiDigit(pattern[i]) || (i > 0 && char.IsAsciiDigit(pattern[i - 1])))
            {
                continue;
            }

            int end = i;
            while (end < pattern.Length && char.IsAsciiDigit(pattern[end]))
            {
                end++;
            }

            if (end + 1 >= pattern.Length || (pattern[end + 1] != '+' && pattern[end + 1] != '<'))
            {
                continue;
            }

            int kind = pattern[end] switch
            {
                's' => 0,
                'i' => 1,
                'd' => 2,
                _ => -1,
            };

            if (kind < 0)
            {
                continue;
            }

            if (
                found[kind] >= 0
                || !long.TryParse(pattern.AsSpan(i, end - i), CultureInfo.InvariantCulture, out long value)
            )
            {
                // A second equation, from a nested section, or a coefficient too big to be one.
                return false;
            }

            found[kind] = value;
        }

        if (found[0] < 0 || found[1] < 0 || found[2] < 0 || (found[0] == found[1] && found[1] == found[2]))
        {
            return false;
        }

        costs = found;
        return true;
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

    /// <summary>Every match of an answer, or <see langword="null"/> where it is not a match at all.</summary>
    /// <param name="outcome">One engine's answer.</param>
    /// <returns>The matches, a single-match answer being a list of one.</returns>
    private static IReadOnlyList<MatchOutcome>? MatchesOf(IOracleOutcome outcome) =>
        outcome switch
        {
            MatchOutcome single => [single],
            MatchesOutcome scan => scan.Matches,
            _ => null,
        };

    /// <summary>
    /// Whether two matches say the same thing about everything except how the errors were spent.
    /// </summary>
    /// <param name="theirs">Upstream's match.</param>
    /// <param name="ours">This port's.</param>
    /// <returns><see langword="true"/> if only the fuzzy half can differ.</returns>
    private static bool AgreeApartFromTheFuzzyHalf(MatchOutcome theirs, MatchOutcome ours) =>
        string.Equals(
            (theirs with { Fuzzy = null }).Describe(),
            (ours with { Fuzzy = null }).Describe(),
            StringComparison.Ordinal
        );

    /// <summary>
    /// Whether a divergence is ledger entry 11 mechanism A: the two engines agree on everything
    /// including the fuzzy counts, and upstream's change POSITIONS are a stale attempt's.
    /// </summary>
    /// <remarks>
    /// The entry's own <see cref="ExpectedDivergence.Reason"/> carries the argument and names what
    /// the weak arm masks. Read it before widening anything here.
    /// </remarks>
    /// <param name="row">The row, carrying upstream's answer and the recorded leak-free one.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns><see langword="true"/> if the divergence belongs to the family.</returns>
    private static bool OnlyTheChangePositionsLeaked(OracleRow row, IOracleOutcome ours)
    {
        // Absent means the question was never asked, which is every non-fuzzy row and every wave
        // recorded before S47. A null ENTRY means it was asked and upstream would not answer.
        if (row.LeakFreeFuzzy is not { } leakFree)
        {
            return false;
        }

        if (MatchesOf(row.Expected) is not { } theirs || MatchesOf(ours) is not { } mine)
        {
            return false;
        }

        if (theirs.Count != mine.Count || theirs.Count != leakFree.Count)
        {
            return false;
        }

        bool anyDiffer = false;

        for (int m = 0; m < theirs.Count; m++)
        {
            if (string.Equals(theirs[m].Describe(), mine[m].Describe(), StringComparison.Ordinal))
            {
                continue;
            }

            anyDiffer = true;

            if (
                !AgreeApartFromTheFuzzyHalf(theirs[m], mine[m])
                || theirs[m].Fuzzy is not { } theirFuzzy
                || mine[m].Fuzzy is not { CountsAgreeWithPositions: true } ourFuzzy
            )
            {
                return false;
            }

            // Counts that differ are mechanism B, whose entry follows this one and whose argument is
            // a different one entirely.
            if (
                theirFuzzy.Substitutions != ourFuzzy.Substitutions
                || theirFuzzy.Insertions != ourFuzzy.Insertions
                || theirFuzzy.Deletions != ourFuzzy.Deletions
            )
            {
                return false;
            }

            // The STRONG arm. A null here is the WEAK one: upstream refused the anchored question,
            // so there is nothing to hold this port's positions to.
            if (
                leakFree[m] is { } free
                && !string.Equals(free.Describe(), ourFuzzy.Describe(), StringComparison.Ordinal)
            )
            {
                return false;
            }
        }

        return anyDiffer;
    }

    /// <summary>
    /// Whether a divergence is ledger entry 11 mechanism B: on a partial match upstream's counts are
    /// the innermost open section's, and its change positions are this port's script truncated to
    /// that total.
    /// </summary>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns><see langword="true"/> if the divergence belongs to the family.</returns>
    private static bool UpstreamCountedOnlyTheInnermostSection(OracleRow row, IOracleOutcome ours)
    {
        if (MatchesOf(row.Expected) is not { } theirs || MatchesOf(ours) is not { } mine)
        {
            return false;
        }

        if (theirs.Count != mine.Count)
        {
            return false;
        }

        bool anyDiffer = false;

        for (int m = 0; m < theirs.Count; m++)
        {
            if (string.Equals(theirs[m].Describe(), mine[m].Describe(), StringComparison.Ordinal))
            {
                continue;
            }

            anyDiffer = true;

            // Only a partial match can carry an open section's counter out of the engine, and both
            // engines must agree that this is one: a partial appearing on one side alone is a
            // different question and several entries above already account for those.
            if (
                !theirs[m].Partial
                || !mine[m].Partial
                || !AgreeApartFromTheFuzzyHalf(theirs[m], mine[m])
                || mine[m].Fuzzy is not { CountsAgreeWithPositions: true } ourFuzzy
            )
            {
                return false;
            }

            // A match upstream says spent no errors renders no fuzzy half at all, which is the
            // commonest shape of this family rather than an edge case - the innermost section had
            // spent none.
            OracleFuzzy theirFuzzy = theirs[m].Fuzzy ?? new OracleFuzzy(0, 0, 0, [], [], []);

            if (
                theirFuzzy.Substitutions > ourFuzzy.Substitutions
                || theirFuzzy.Insertions > ourFuzzy.Insertions
                || theirFuzzy.Deletions > ourFuzzy.Deletions
                || !IsAPrefixOf(theirFuzzy.SubstitutionPositions, ourFuzzy.SubstitutionPositions)
                || !IsAPrefixOf(theirFuzzy.InsertionPositions, ourFuzzy.InsertionPositions)
                || !IsAPrefixOf(theirFuzzy.DeletionPositions, ourFuzzy.DeletionPositions)
            )
            {
                return false;
            }
        }

        return anyDiffer;
    }

    /// <summary>
    /// Whether upstream's positions of one kind are the first few of this port's, in order.
    /// </summary>
    /// <remarks>
    /// Vacuously true where either side has no positions, which is a POSIX row: upstream cannot be
    /// asked for them at all and <c>OracleComparer</c> drops this port's to match (ledger entry 9),
    /// so such a row is judged on its counts alone.
    /// </remarks>
    /// <param name="theirs">Upstream's positions of one kind.</param>
    /// <param name="ours">This port's.</param>
    /// <returns><see langword="true"/> if upstream's are a prefix of ours.</returns>
    private static bool IsAPrefixOf(IReadOnlyList<int>? theirs, IReadOnlyList<int>? ours) =>
        theirs is null || ours is null || (theirs.Count <= ours.Count && theirs.SequenceEqual(ours.Take(theirs.Count)));

    // S44 DELETED `OnlyDifferenceIsACaptureUpstreamLeftEmpty`, both overloads. It existed only for
    // `group-call-direction`'s predicate - "the only captures that differ are ones where upstream
    // recorded a negative length or an empty span and this port recorded text" - which was issue
    // 614's signature and nothing else's. With the entry gone its one caller went, and S1144 and
    // IDE0052 said so on the next build. Deleted rather than kept "in case a row comes back": a
    // predicate with no entry cannot classify anything, and if 614 ever regresses upstream the
    // entry and its predicate come back together, out of git history.
    //
    // `HasGroupCall`, `IsReversed` and `HasLookbehind` are NOT deleted - `group-call-loses-the-match`
    // still uses them.
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
