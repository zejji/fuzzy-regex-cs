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
    /// The four rows of <c>overlapped-skip-extra-match-reversed</c>, as
    /// <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-12. All four are listed rather
    /// than one, because the entry has two tells, the third row is the one whose ground truth was
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
    /// reason rows 2 and 3 are here whole.
    /// </remarks>
    private const string _reversedExtraMatchRows = """
        {"generator": "verbs", "pattern": "(?r)(?:.{2}(*SKIP)A|x)$", "flags": 8, "namedLists": {}, "subject": "bxA", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 3]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}}
        {"generator": "verbs", "pattern": "(?r)([^a]{2,4}(*SKIP)[a\\d])((?:[^\\d]++(*SKIP)\\s|\\ ))", "flags": 0, "namedLists": {}, "subject": "b0 0\n A", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 6]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 2, "success": true, "index": 4, "length": 2, "captures": [[4, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}]}}
        {"generator": "verbs", "pattern": "(?r)(?:[a\\d]*(*SKIP)\\D|\\p{Nd})(?:[\\p{L}\\p{N}]{1,3}(*SKIP)\\S|.)((?>\\s+(*PRUNE)A))", "flags": 0, "namedLists": {}, "subject": "İİAAA AS", "operation": "finditer-overlapped", "oracle": "prefilter-free", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 7, "captures": [[0, 7]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 7]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 5]}, {"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 5, "length": 2, "captures": [[5, 2]]}], "lastIndex": 1, "lastGroup": null, "partial": false, "codepointSpan": [0, 4]}]}}
        {"generator": "interactions", "pattern": "(?r)(?:\\D{1,1}(*SKIP)[\\p{ASCII}&&\\p{L}]|[[a-f]~~[d-k]])(?P<g1>.*)??(?P<g2>[A])(?:(?(2)(?<!(?&g2))\\p{Nd}))\\b", "flags": 264, "namedLists": {}, "subject": "A\r\nAAA", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 3, "length": 3, "captures": [[3, 3]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [3, 6]}, {"groups": [{"number": 0, "success": true, "index": 3, "length": 2, "captures": [[3, 2]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}, {"number": 2, "success": true, "index": 5, "length": 1, "captures": [[5, 1]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [3, 5]}]}}
        """;

    /// <summary>
    /// The four rows of <c>search-start-partial</c>'s second symptom - upstream's prefilter reports
    /// a partial covering the whole searched region, and this port reports its OWN partial somewhere
    /// else - as <c>tools/record-oracle.py --rows</c> wrote them on 2026-09-12.
    /// </summary>
    /// <remarks>
    /// Row 1 is the family minimised by hand; rows 2, 3 and 4 are rows 3497 (seed 4242), 2689 and
    /// 4313 (seed 20260912) of a 6000-row <c>interactions</c> wave. <b>Two of the three come from
    /// one seed and seed 7 draws none</b>, so the family is evidenced at two seeds of the three, not
    /// at three - the S37 blind review's second pass corrected "one per seed" here.
    /// </remarks>
    private const string _searchStartElsewhereRows = """
        {"generator": "interactions", "pattern": "(?:\\w{2,}(*SKIP)\\w|\\w)\\B", "flags": 0, "namedLists": {}, "subject": "a.Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\b(?:.+(*SKIP)[^\\d]|\\p{Lu})([^[\\p{L}--[a-z]]])+(?(?=\\W)[\\w--[0-9]])", "flags": 16650, "namedLists": {}, "subject": "ﬃ\nﬃaa", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "(?r)[a](\\D)*(?:[a-f](*SKIP)[^a-f]|[[a-f]~~[d-k]])\\b", "flags": 16642, "namedLists": {}, "subject": "AA𝔘𐐀", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        {"generator": "interactions", "pattern": "\\m(?:[\\p{L}\\p{N}]{2,}(*SKIP)\\p{ASCII}|\\w)\\B", "flags": 264, "namedLists": {}, "subject": "a😀Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
        """;

    /// <summary>
    /// This port's judged answer to each row of <see cref="_searchStartElsewhereRows"/>, in the same
    /// order, as the report renders it.
    /// </summary>
    /// <remarks>
    /// Rows 1, 2 and 4 are the answer upstream's own <c>match(pos, partial=True)</c> gives at the
    /// position this port stopped at. <b>Row 3 is not, and that is why this arm lists rows.</b> It is
    /// reversed, so <c>match</c> anchors at the end; sweeping <c>endpos</c> instead, upstream answers
    /// (0, 1) partial and (0, 2) and (0, 3) complete, and never this port's zero-width partial at
    /// (0, 0). What judges it is the verb: delete the <c>(*SKIP)</c>, or make it <c>(*PRUNE)</c>, and
    /// upstream's own search answers (0, 0) partial - this port's answer.
    /// </remarks>
    private static readonly string[] _searchStartElsewhereOurs =
    [
        "match 0:(4,0)[(4,0)] last=-1/- partial",
        "match 0:(2,3)[(2,3)] 1:unset last=-1/- partial",
        "match 0:(0,0)[(0,0)] 1:unset last=-1/- partial",
        "match 0:(5,0)[(5,0)] last=-1/- partial",
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
    /// The five rows of <c>group-call-loses-the-match</c>, as <c>tools/record-oracle.py --rows</c>
    /// wrote them on 2026-09-12, and all five because between them they are the four OUTCOME SHAPES
    /// the defect appears in - an empty scan, a scan one match short, a substitution that replaced
    /// nothing, and a split that split nothing.
    /// </summary>
    /// <remarks>
    /// Rows 4182 (seed 7), 4407 (seed 20260912), 5087 and 5773 (seed 4242) and 1624 (seed 99991) of a
    /// 6000-row <c>interactions</c> wave, each as the wave drew it. The fifth arrived the way this
    /// list is meant to work: <c>split</c> was deliberately left out of the predicate as a shape no
    /// row had shown, the fourth seed drew one, the run went red rather than quietly classifying it,
    /// and it was judged by the same probe as the other four. Minimisation was tried and is recorded in
    /// the entry's own <see cref="ExpectedDivergence.Reason"/> as having failed: every shrink that
    /// kept upstream contradicting ITSELF lost the divergence, because this port reproduces
    /// upstream's answer on the short forms.
    /// </remarks>
    private const string _groupCallLostMatchRows = """
        {"generator": "interactions", "pattern": "(?P<g1>\\S)(?:(?(1)(?<!(?P>g1))[[:alpha:]]))??([a]{0,0})?\\2\\b", "flags": 0, "namedLists": {}, "subject": "aa𐐨𐐨A", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}}
        {"generator": "interactions", "pattern": "\\b(?(?![\\w\\s])[[:digit:]])(\\w)(?P<g2>[^\\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*", "flags": 16650, "namedLists": {}, "subject": "İİ\nİİﬁﬁ ", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 3, "captures": [[1, 3], [1, 3]]}], "lastIndex": 2, "lastGroup": "g2", "partial": false, "codepointSpan": [0, 4]}]}}
        {"generator": "interactions", "pattern": "(?r)\\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\\S)){3}(\\p{Nd}+?)?", "flags": 10, "namedLists": {}, "subject": "AA..0", "operation": "finditer-overlapped", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}}
        {"generator": "interactions", "pattern": "(?r)^([[:alpha:]]+)(?P<g2>[😀])(?:(?(2)(?=(?P>g2))[^\\p{L}]))+?", "flags": 10, "namedLists": {}, "subject": "a😀\r", "operation": "subf", "template": "{g2}{{", "count": 2, "codepointSpan": null, "outcome": {"kind": "sub", "text": "a😀\r", "count": 0}}
        {"generator": "interactions", "pattern": "(?P<g1>[𐐀A]{2,2})(?:(?(1)(?<!(?P>g1))[^\\d]|[a-f]))?([abz]{0,2})$", "flags": 65536, "namedLists": {}, "subject": "𐐀𐐀A", "operation": "split", "count": 0, "codepointSpan": null, "outcome": {"kind": "split", "parts": ["𐐀𐐀A"]}}
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
                + "upstream issue 589. Phase 7 owns the prefilter and must not import this answer.\n"
                + "WIDENED BY S37 TO THE SECOND SYMPTOM: this port answers its OWN partial somewhere "
                + "else rather than nothing at all. Three rows of a 6000-row `interactions` wave show "
                + "it - one at seed 4242 and two at 20260912, none at seed 7 - and all three carry a "
                + "`(*SKIP)`, which is the piece S36's widening put "
                + "into the generator. `(?:\\w{2,}(*SKIP)\\w|\\w)\\B` over 'a.Aa' is the minimised "
                + "shape: upstream's `search(partial=True)` is (0, 4) partial - the WHOLE subject, "
                + "from the search start to the end of the text - and its own "
                + "`match('a.Aa', 0, 4, partial=True)` over that very span is None. This port answers "
                + "the zero-width partial at (4, 4), where `\\w` ran out of text, which is upstream's "
                + "own `match(pos=4, partial=True)` answer. Delete the verb and upstream's search "
                + "gives a COMPLETE match at (2, 3); make it `(*PRUNE)` and it gives this port's "
                + "partial. Measured 2026-09-12, tools/probes/upstream-search-start-whole-region-partial.py.\n"
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
                + "subject_finds_no_partial_here and .A_skip_alternation_partial_starts_where_this_"
                + "port_ran_out_of_text",
            Example: """
            {"generator": "partial", "pattern": "(?r)\\b$", "flags": 0, "namedLists": {}, "subject": "", "operation": "search", "partial": true, "codepointSpan": [0, 0], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 0, "captures": [[0, 0]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
            {"generator": "interactions", "pattern": "(?:\\w{2,}(*SKIP)\\w|\\w)\\B", "flags": 0, "namedLists": {}, "subject": "a.Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 4, "captures": [[0, 4]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
            {"generator": "interactions", "pattern": "\\b(?:.+(*SKIP)[^\\d]|\\p{Lu})([^[\\p{L}--[a-z]]])+(?(?=\\W)[\\w--[0-9]])", "flags": 16650, "namedLists": {}, "subject": "ﬃ\nﬃaa", "operation": "search", "partial": true, "codepointSpan": [0, 5], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
            {"generator": "interactions", "pattern": "(?r)[a](\\D)*(?:[a-f](*SKIP)[^a-f]|[[a-f]~~[d-k]])\\b", "flags": 16642, "namedLists": {}, "subject": "AA𝔘𐐀", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 6, "captures": [[0, 6]]}, {"number": 1, "success": false, "index": 0, "length": 0, "captures": []}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
            {"generator": "interactions", "pattern": "\\m(?:[\\p{L}\\p{N}]{2,}(*SKIP)\\p{ASCII}|\\w)\\B", "flags": 264, "namedLists": {}, "subject": "a😀Aa", "operation": "search", "partial": true, "codepointSpan": [0, 4], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 5, "captures": [[0, 5]]}], "lastIndex": -1, "lastGroup": null, "partial": true}, "searchOnlyPartial": true}
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
                + "the recorder needs no per-row isolation. Ledger entry 5.",
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
            // Closing it properly needs upstream's own answer to each extra match, which is the
            // reversed `anchoredScan` tools/record-oracle.py refuses - moving `endpos` truncates the
            // subject and changes what `$` means, which is the very thing this entry is about.
            // Phase 6 oracle hardening.
            Applies: static (row, ours) =>
                row.Pattern.Contains("(*SKIP)", StringComparison.Ordinal)
                && IsReversed(row)
                && string.Equals(row.Operation, "finditer-overlapped", StringComparison.Ordinal)
                && row.Expected is MatchesOutcome theirScan
                && ours is MatchesOutcome ourScan
                && OnlyExtraMatchesTheRowItselfRefutes(row, theirScan, ourScan)
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
            Reason: "Upstream bug, NOT fixed by issue 614 and still present in 2026.9.10 (all five "
                + "wave rows replayed against .venvs/regex-2026.9.10 on 2026-09-12, identical). "
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
            Example: _groupCallLostMatchRows,
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
                HasGroupCall(row.Pattern)
                && CallsThroughAnOppositeDirectionLookaround(row)
                && UpstreamFoundStrictlyLess(row, ours)
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
            (SplitOutcome theirs, SplitOutcome mine) => theirs.Parts.Count == 1
                && mine.Parts.Count > 1
                && string.Equals(theirs.Parts[0], row.Subject, StringComparison.Ordinal),
            (NoMatchOutcome, MatchOutcome) => true,
            _ => false,
        };

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
