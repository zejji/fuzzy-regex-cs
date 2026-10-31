using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// D80: a named ablation names the fix that explains a row, and no other. Each test is a row the
/// bundle of switches every named ablation stacks on its own once credited to the wrong entry.
/// </summary>
public sealed class NamedAblationLabelTests
{
    /// <summary>
    /// Seed 936847660 row 49361, recorded against regex 2026.9.29: the D128 side finding. D93's
    /// switch changes nothing on it and D11's switch alone gives upstream's answer, but the D11
    /// composition credited it to <c>call-reentry-opening-a-section-is-refused</c>: its "D11 alone"
    /// control also stacked the named-ablation bundle (D81 off among them), which the composed run
    /// it controls did not, so the control disagreed and the composed run agreed.
    /// </summary>
    private const string _reentryRow = """
        {"generator":"fuzzy-overhang","pattern":"(?fi)(?r)(?:(?:\\Z(?:\\1)){1i+2d+1s<=4}){s<=2}(fi)","flags":0,"namedLists":{},"subject":"ﬀifi","operation":"search","partial":true,"codepointSpan":[0,0],"outcome":{"kind":"match","groups":[{"number":0,"success":true,"index":0,"length":0,"captures":[[0,0]]},{"number":1,"success":false,"index":0,"length":0,"captures":[]}],"lastIndex":-1,"lastGroup":null,"partial":true},"searchOnlyPartial":false}
        """;

    /// <summary>
    /// Rows recorded by <c>tools/record-oracle.py --rows</c> on 2026-10-31 against regex 2026.9.29
    /// (pinned): partial answers whose counts break an enclosing section's limit, ledger entry 81.
    /// The D81 switch alone gives upstream's answer on each. Seed 7 row 42099 on
    /// <c>fix/insertion-runout</c> is the same shape: every named ablation stacks the D81 switch,
    /// so the first entry whose own switch changes nothing claimed it.
    /// </summary>
    private const string _runOutRows = """
        {"generator": "rows", "pattern": "(?:(?:bc){e<=2}){s<=1}x", "flags": 0, "namedLists": {}, "subject": "xy", "operation": "match", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [1], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [1, 1, 0], "fuzzyChanges": {"substitutions": [0], "insertions": [1], "deletions": []}}]}
        {"generator": "rows", "pattern": "(?:(?:abc){e<=3}){s<=1}", "flags": 0, "namedLists": {}, "subject": "xy", "operation": "fullmatch", "partial": true, "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [2, 0, 0], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [], "deletions": []}}]}
        {"generator": "rows", "pattern": "(?:(?:abc){e<=3}){s<=1}", "flags": 0, "namedLists": {}, "subject": "xyz", "operation": "fullmatch", "partial": true, "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": true, "fuzzyCounts": [2, 1, 0], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [2], "deletions": []}}, "leakFreeFuzzy": [{"fuzzyCounts": [2, 1, 0], "fuzzyChanges": {"substitutions": [0, 1], "insertions": [2], "deletions": []}}]}
        """;

    [Test]
    public void A_row_D11_alone_explains_is_not_credited_to_an_ablation_that_changes_nothing()
    {
        OracleRow row = OracleWave.ParseRows(_reentryRow)[0] with { DefaultVersion = (int)FuzzyRegexOptions.Version0 };
        IOracleOutcome? ours = OracleComparer.Run(row);

        // The controls: the row diverges, D93's switch alone changes nothing, and D11's alone
        // gives upstream's answer. Without them this test could not go red for the right reason.
        row.Partial.Should().BeTrue();
        OracleComparer.Compare(row, ours).Should().NotBe(OracleVerdict.Agree);
        OracleWave
            .SameAnswer(
                ours,
                OracleComparer.Run(
                    row,
                    OracleComparer.RowTimeout,
                    ablate: static c => c.PatternObject.CallGuardComparesAcrossSections = true,
                    composeWithD11: false
                )
            )
            .Should()
            .BeTrue("D93's switch changes nothing on this row");
        OracleComparer
            .Compare(
                row,
                OracleComparer.Run(
                    row,
                    OracleComparer.RowTimeout,
                    ablate: static c => c.PatternObject.SecondPassAsBeforeD11 = true,
                    composeWithD11: false
                )
            )
            .Should()
            .Be(OracleVerdict.Agree, "D11's switch alone gives upstream's answer");

        OracleComparer
            .Compare(row, OracleComparer.RunWithTheReentryInANewSection(row))
            .Should()
            .NotBe(OracleVerdict.Agree, "D93's ablation composed with D11's is D11's answer, not D93's");
        (ExpectedDivergences.For(row, ours!)?.Id)
            .Should()
            .Be(
                "full-fold-run-edits-an-expanding-subject-character-whole",
                "the subject's 'ﬀ' is an expanding full-fold character, and D93's ablation changes nothing"
            );
    }

    [Test]
    public void A_row_the_run_out_check_alone_explains_is_credited_to_no_named_ablation()
    {
        foreach (OracleRow recorded in OracleWave.ParseRows(_runOutRows))
        {
            OracleRow row = recorded with { DefaultVersion = (int)FuzzyRegexOptions.Version0 };
            IOracleOutcome? ours = OracleComparer.Run(row);

            // The controls: the row diverges, and the D81 switch alone gives upstream's answer.
            OracleComparer.Compare(row, ours).Should().NotBe(OracleVerdict.Agree, row.Pattern);
            OracleComparer
                .Compare(
                    row,
                    OracleComparer.Run(
                        row,
                        OracleComparer.RowTimeout,
                        ablate: static c => c.PatternObject.RunOutAsBeforeD81 = true,
                        composeWithD11: false
                    )
                )
                .Should()
                .Be(OracleVerdict.Agree, "the D81 switch alone explains {0} over '{1}'", row.Pattern, row.Subject);

            // Ledger entry 81 has no entry of its own: its rows are judged by hand. So a named
            // ablation that claims one claims it only through the D81 switch it stacks.
            (ExpectedDivergences.For(row, ours!)?.Id)
                .Should()
                .BeNull("{0} over '{1}' is ledger entry 81's alone", row.Pattern, row.Subject);
        }
    }
}
