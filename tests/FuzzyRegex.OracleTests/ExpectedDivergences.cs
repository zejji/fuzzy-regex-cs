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
    /// fit needs two TRAILING insertions walks into an inherited bug that loses the match outright -
    /// <c>regex.fullmatch(r'(?b)(?:x){e&lt;=3}', 'xyz')</c> is <c>None</c> where the same pattern
    /// without <c>(?b)</c> answers <c>(0, 2, 0)</c>, measured on regex 2026.7.19 - so an insertion
    /// row would pin an unrelated defect instead of this entry's family. Ledger entry 12;
    /// <c>Gaps.Engine.FuzzyBestMatchTests.Bestmatch_loses_a_match_that_needs_two_trailing_insertions</c>.
    /// </para>
    /// </summary>
    private const string _bestCostRankedRow =
        """{"generator": "fuzzy", "pattern": "(?b)(?:ab|xyc){9i+1s+9d<=20}", "flags": 0, "namedLists": {}, "subject": "abc", "operation": "fullmatch", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [2], "deletions": []}}}""";

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
        {"generator": "verbs", "pattern": "(?r)([^a]{2,4}(*SKIP)[a\\d])((?:[^\\d]++(*SKIP)\\s|\\ ))", "flags": 0, "namedLists": {}, "subject": "b0 0\n A", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}]}}
        {"generator": "verbs", "pattern": "(?r)(?:[a\\d]*(*SKIP)\\D|\\p{Nd})(?:[\\p{L}\\p{N}]{1,3}(*SKIP)\\S|.)((?>\\s+(*PRUNE)A))", "flags": 0, "namedLists": {}, "subject": "İİAAA AS", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 4]}]}}
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
    /// The five rows of <c>search-start-partial</c>'s second symptom - upstream's prefilter reports
    /// a partial covering the whole searched region, and this port reports its OWN partial somewhere
    /// else - as <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-12.
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
    /// </remarks>
    private const string _searchStartElsewhereRows = """
        {"generator": "interactions", "pattern": "(?:\\w{2,}(*SKIP)\\w|\\w)\\B", "flags": 0, "namedLists": {}, "subject": "a.Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\b(?:.+(*SKIP)[^\\d]|\\p{Lu})([^[\\p{L}--[a-z]]])+(?(?=\\W)[\\w--[0-9]])", "flags": 16650, "namedLists": {}, "subject": "ﬃ\nﬃaa", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "(?r)[a](\\D)*(?:[a-f](*SKIP)[^a-f]|[[a-f]~~[d-k]])\\b", "flags": 16642, "namedLists": {}, "subject": "AA𝔘𐐀", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\m(?:[\\p{L}\\p{N}]{2,}(*SKIP)\\p{ASCII}|\\w)\\B", "flags": 264, "namedLists": {}, "subject": "a😀Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "(?r)^(?(?<![^\\p{L}])\\p{L}|[[:alpha:]])(?:\\.(\\S)){e<=2,i<=1}(?:[\\w--[0-9]](*SKIP)[^\\d]|[abz])", "flags": 256, "namedLists": {}, "subject": "\r.", "operation": "search", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
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
    /// The three rows of <c>partial-retry-reversed-slice</c>, each copied out of
    /// <c>TestResults/oracle/wave-&lt;seed&gt;.jsonl</c> on 2026-09-13 rather than retyped.
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
    /// </remarks>
    private const string _partialRetryReversedRows = """
        {"generator": "partial", "pattern": "(?r)\\b(?:[^a-f](*SKIP)[\\p{L}\\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})", "flags": 8, "namedLists": {}, "subject": "a\n", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(?r)\\M(?:[^[\\p{L}--[a-z]]](*SKIP)[a-f]|[A-Z])", "flags": 16642, "namedLists": {}, "subject": "A𐐨", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "partial", "pattern": "(?r)(?:[^a-f](*SKIP)\\S|[\\p{L}\\p{N}])(?(?<![\\w--[0-9]])[\\p{L}\\p{N}]|[\\w--[0-9]])", "flags": 256, "namedLists": {}, "subject": "𐐀_  ", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
        {"generator": "interactions", "pattern": "(?r)\\b(?:\\D(*SKIP)[\\p{L}\\p{N}]|[^a])(?:\\p{Ll}{0,2}?İ(?:\\p{Nd}){2i+1d+1s<=2}){1i+2d+1s<=3}", "flags": 0, "namedLists": {}, "subject": "ıİﬀ", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": false}
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
    ];

    /// <summary>
    /// <see cref="_partialRetryReversedRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _partialRetryReversed = OracleWave
        .ParseRows(_partialRetryReversedRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _partialRetryReversedOurs[i]))
        .ToDictionary(static pair => pair.Key, static pair => pair.Ours, StringComparer.Ordinal);

    /// <summary>
    /// The one row of <c>partial-retry-carried-slice-forward</c> - the entry above's mechanism in a
    /// pattern that runs LEFT TO RIGHT - copied out of
    /// <c>TestResults/oracle/wave-99991.jsonl</c> on 2026-09-13 rather than retyped.
    /// </summary>
    /// <remarks>
    /// Row 6897 of <c>pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions
    /// -Seeds 99991</c>. One row, because one is all any wave has drawn: the forward half of this
    /// mechanism needs a <c>(*SKIP)</c> whose moved <c>slice_start</c> changes which ALTERNATIVE the
    /// partial pass can still enter, and that is a narrower accident than the reversed half, which
    /// only needs the bound to hide an anchor.
    /// </remarks>
    private const string _partialRetryForwardRows = """
        {"generator": "interactions", "pattern": "\\b(?:(?:\\ _(\\W)){e<=1}(*SKIP)[A-Z]|[^a])(?:.?(?:(\\w+?)){i<=1:.}){e<=2,s<=1:[^a-z]}(?:(?:[abz]([abz])){2i+1d+1s<=2}(*PRUNE)[\\w\\s]|\\W)", "flags": 8, "namedLists": {}, "subject": "😀ß_ ", "operation": "search", "partial": true, "codepointSpan": [1, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 2, "length": 3, "captures": [[2, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 3, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [0, 1, 0], "fuzzyChanges": {"substitutions": [], "insertions": [4], "deletions": []}}, "searchOnlyPartial": false}
        """;

    /// <summary>
    /// This port's judged answer to <see cref="_partialRetryForwardRows"/>, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Upstream's own answer to the same row with the first verb spelled <c>(*PRUNE)</c>, and with it
    /// deleted: both give the substitution at 1 and the capture at (3, 4) in codepoints, which is
    /// UTF-16 (4, 1) on this astral subject. Measured 2026-09-13, the probe named in the entry.
    /// </remarks>
    private static readonly string[] _partialRetryForwardOurs =
    [
        "match 0:(2,3)[(2,3)] 1:(4,1)[(4,1)] 2:unset 3:unset last=1/- partial fuzzy=(1,0,0)[s:2][i:][d:]",
    ];

    /// <summary>
    /// <see cref="_partialRetryForwardRows"/> by its question, mapped to this port's judged answer.
    /// </summary>
    private static readonly Dictionary<string, string> _partialRetryForward = OracleWave
        .ParseRows(_partialRetryForwardRows)
        .Select(static (row, i) => (Key: Question(row), Ours: _partialRetryForwardOurs[i]))
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
    /// The five rows of <c>bestmatch-loses-a-partial</c>, each copied out of
    /// <c>TestResults/oracle/wave-&lt;seed&gt;.jsonl</c> on 2026-09-13 rather than retyped.
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
    /// </remarks>
    private const string _bestmatchLostPartialRows = """
        {"generator": "interactions", "pattern": "(?b)(?r)(?:[^\\d]+(*SKIP)\\p{L}|[^\\d])(?:(?:(\\p{Nd}{1})(?:(?P<g2>\\p{ASCII})){e<=2,i<=1}){s<=1,i<=1,d<=1}(*SKIP)\\p{L}|\\w)\\D", "flags": 16386, "namedLists": {}, "subject": " 𐐨 ", "operation": "match", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}}
        {"generator": "interactions", "pattern": "(?b)(?r)^(\\d)(?:([^\\d]{3,4}?)a(?:[[:alpha:]]{2,3}?){e<=2,s<=1:[A-Za-z_]}){s<=1,i<=1,d<=1}(?:\\S*?(*SKIP)\\w|[^\\d])", "flags": 10, "namedLists": {}, "subject": "a\n𐐀\r\na𝔘", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}}
        {"generator": "interactions", "pattern": "(?b)(?e)^(?:(?:AA){s<=1,i<=1,d<=1}(*SKIP)\\p{ASCII}|\\s)(?:(?:A([[a-z]--[aei]])(?:(\\D*)){e<=2,s<=1}){i<=1}(*SKIP)\\p{ASCII}|[A-Z])", "flags": 256, "namedLists": {}, "subject": "A", "operation": "fullmatch", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}}
        {"generator": "interactions", "pattern": "(?b)(?e)(?:a\\w){s<=1,i<=1,d<=1}(?:\\S(*SKIP)[\\p{L}\\p{N}]|\\W)", "flags": 8, "namedLists": {}, "subject": "\r\na𝔘\n", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}}
        {"generator": "interactions", "pattern": "(?b)ﬁ(?:(?:(.)ﬁﬁ){s<=1:[^a-z]}(*SKIP)[A-Z]|\\p{ASCII})(?P<g2>[[:digit:]])?", "flags": 16394, "namedLists": {}, "subject": "ﬁﬁﬁﬁßß\n ", "operation": "search", "partial": true, "codepointSpan": null, "outcome": {"kind": "nomatch"}}
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
    /// set. Row 77937 therefore rests on the weak form alone, and the entry's own
    /// <see cref="ExpectedDivergence.Reason"/> says so. The blind review found this because the
    /// probe compared spans while the prose claimed whole answers; it now prints groups and counts.
    /// </para>
    /// <para>
    /// Measured row by row rather than described -
    /// <c>python tools/probes/upstream-bestmatch-loses-a-partial.py</c>, whose second section
    /// replays all five whole, each asked its own operation, and whose third sweeps both anchored
    /// doors.
    /// </para>
    /// </remarks>
    private static readonly string[] _bestmatchLostPartialOurs =
    [
        "match 0:(0,4)[(0,4)] 1:unset 2:(0,1)[(0,1)] last=2/g2 partial",
        "match 0:(0,9)[(0,9)] 1:unset 2:(0,4)[(0,4)] last=2/- partial fuzzy=(1,1,1)[s:6][i:5][d:4]",
        "match 0:(0,1)[(0,1)] 1:unset 2:unset last=-1/- partial",
        "match 0:(5,1)[(5,1)] last=-1/- partial fuzzy=(1,0,0)[s:0][i:][d:]",
        "match 0:(8,0)[(8,0)] 1:unset 2:unset last=-1/- partial",
    ];

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
            Id: "group-call-direction",
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
                + "for `[ab]+`, which needs at least one character - so the entry covers both.\n"
                + "WIDENED BY S36 FROM `reverse-group-call-direction`, and renamed with it: the "
                + "defect is the direction not reaching a called group, not `(?r)`, so the forward "
                + "mirror - a LOOKBEHIND in an ordinary forward pattern - has it too. The composed "
                + "`interactions` wave drew it at seed 7: `(?P<g1>A*)(?<=(?&g1))` over 'A' records "
                + "g1's second capture as (2, 1), a start past the end of a one-character subject. "
                + "2026.9.10 answers (0, 1), which is this port's answer and always has been. That "
                + "row also broke the RECORDER, which indexed a two-entry codepoint table with 2 - "
                + "see `_utf16_index` in tools/record-oracle.py.",
            PinnedBy: "GroupCallTests.A_group_called_from_a_lookahead_under_reverse_matches_forwards_here"
                + " and .A_group_called_from_a_lookbehind_records_its_capture_inside_the_subject_here",
            Example: """
            {"generator": "recursion", "pattern": "(?r)(?<g>[ab]+)(?=(?&g))b", "flags": 0, "namedLists": {}, "subject": "abbaa", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}, {"number": 1, "success": true, "index": 0, "length": 2, "captures": [[2, -1], [0, 2]]}], "lastIndex": 1, "lastGroup": "g", "partial": false}}
            {"generator": "interactions", "pattern": "(?P<g1>A*)(?<=(?&g1))", "flags": 0, "namedLists": {}, "subject": "A", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1], [2, -1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [0, 1]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 0, "captures": [[1, 0]]}, {"number": 1, "success": true, "index": 1, "length": 0, "captures": [[1, 0], [2, -1]]}], "lastIndex": 1, "lastGroup": "g1", "partial": false, "codepointSpan": [1, 1]}]}}
            """,
            // Narrow on three counts: the pattern must contain a group call AND run the called body
            // the other way round from itself - reversed with any lookaround, or forward with a
            // lookbehind - which is the defect's precondition and not a symptom; the two answers must
            // agree capture for capture everywhere else; and the only captures allowed to differ are
            // ones where upstream recorded nothing - a negative length, or an empty span - and this
            // port recorded text. An ordinary engine defect that shortens or moves a capture is
            // reported, because its upstream side is a real span.
            //
            // The hole, said out loud: a called group whose body CAN match empty would make an
            // empty capture upstream's honest answer, so a port defect that wrongly extended that
            // capture would be classified. Closing it needs the body's minimum width, which the
            // row does not carry.
            Applies: static (row, ours) =>
                HasGroupCall(row.Pattern)
                && (IsReversed(row) || HasLookbehind(row.Pattern))
                && OnlyDifferenceIsACaptureUpstreamLeftEmpty(row.Expected, ours)
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
                + "(upstream/src/_regex.c:14551), the symptoms differ accordingly, and one entry "
                + "spanning both would have to state each half separately anyway.\n"
                + "THE SAME TWO PASSES. A `partial` search runs a non-partial pass and then a "
                + "partial one from the same `text_pos` (upstream do_match, :18160). Upstream "
                + "restores `text_pos` and nothing else, so a bound the verb moved in the first pass "
                + "is still moved in the second. S40b restores both bounds here; upstream does not.\n"
                + "WHAT THE MOVED BOUND COSTS IS DIFFERENT THIS WAY ROUND, and it is worth saying "
                + "because it is why the reversed entry's own argument does not transfer. The two "
                + "engines agree on the SPAN - both answer a partial at codepoints (1, 4) - and "
                + "differ in which ALTERNATIVE the partial pass could still enter, and therefore in "
                + "which error was spent and which group captured. Upstream takes the `[^a]` branch "
                + "and charges an insertion at 3, with every group unset. This port takes the branch "
                + "the `(*SKIP)` sits in, charges a substitution at 1, and captures (3, 4).\n"
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
                + "KEYED ON ITS ROW, like every sibling named above. No predicate over 'the two "
                + "engines agree on the span and disagree on which error they spent' is safe here - "
                + "that is also what a genuine fuzzy-path defect looks like, and this port has "
                + "shipped one in Phase 5 already (S43's own SaveBestMatch fix, which reported the "
                + "errors a POSIX fuzzy match spent as zero). Widening means judging another row "
                + "with the probe and adding it. Phase 7 must not import upstream's answer here.",
            PinnedBy: "PartialMatchingTests.A_forward_skip_does_not_move_the_slice_start_the_partial_"
                + "pass_searches",
            Example: _partialRetryForwardRows,
            Applies: static (row, ours) =>
                _partialRetryForward.TryGetValue(Question(row), out string? judged)
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
                + "Minimised: `(?b)(?:ab){e<=1}(?:\\S(*SKIP)\\w|\\W)` over 'ab.' - upstream's "
                + "search is None and its own `match('ab.', 2, partial=True)` is (2, 3) partial. "
                + "Four conditions, each necessary on that shape: `(?b)` (`(?e)` in its place keeps "
                + "the match, so it is `do_best_fuzzy_match` and not fuzzy ranking at large), a "
                + "fuzzy section, a `(*SKIP)` (the same pattern without the verb keeps its match "
                + "under `(?b)`), and `partial=True`. Measured 2026-09-13 on regex 2026.7.19, "
                + "tools/probes/upstream-bestmatch-loses-a-partial.py, whose second section replays "
                + "all five wave rows whole, each asked its own operation.\n"
                + "FAULTING MECHANISM: `do_best_fuzzy_match` (upstream/src/_regex.c:17584). THE "
                + "EXACT LINE IS NOT ESTABLISHED and the report must say so or establish it first. "
                + "The shape is suggestive - the retry sets `start_pos = state->match_pos` and "
                + "tightens `state->max_errors`, and the loop guard `state->slice_start <= "
                + "start_pos && start_pos <= state->slice_end` is one a `(*SKIP)` moving "
                + "`slice_start` can falsify - but that is a hypothesis with the right shape, not a "
                + "measurement. No debugger and no ASAN build, for the reason ledger entry 9 gives.\n"
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
