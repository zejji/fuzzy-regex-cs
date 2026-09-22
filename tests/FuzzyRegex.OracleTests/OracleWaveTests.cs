using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// Drives the recorded oracle wave through this port and requires upstream's exact answer for
/// every row it can answer at all.
/// </summary>
/// <remarks>
/// <para>
/// Not a merge gate (design spec amendment 7): the oracle needs Python, its rows change from run
/// to run, and a flaky ground truth must not be able to block a merge. It runs on a schedule and
/// locally before any engine slice commits, and every divergence it finds is minimised into an
/// ordinary permanent test in <c>tests/FuzzyRegex.Tests/Gaps/</c>.
/// </para>
/// <para>
/// The three tests below the wave run are what make the wave run mean anything. Until the engine
/// lands, every real row reports <see cref="OracleVerdict.Unsupported"/>, so a harness that
/// silently compared nothing would look exactly like a harness that agreed on everything.
/// </para>
/// </remarks>
public sealed class OracleWaveTests
{
    /// <summary>
    /// Three rows recorded by <c>tools/record-oracle.py --rows</c> on 2026-08-31 against regex
    /// 2026.7.19: a two-group match, a match after an astral character, and a no-match. Recorder
    /// output, so parsing them exercises the same reader the wave run uses - with the one cosmetic
    /// change that U+1F600 is written literally here where the recorder writes ASCII and escapes it
    /// as a surrogate pair. JSON reads the two identically, and the length assertion in
    /// <see cref="The_recorder_translates_codepoint_indices_to_utf16"/> is what proves it.
    /// </summary>
    private const string _recordedRows = """
        {"generator": "rows", "pattern": "(a)(b)", "flags": 0, "namedLists": {}, "subject": "ab", "operation": "search", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}, {"number": 2, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": 2, "lastGroup": null}}
        {"generator": "rows", "pattern": "b", "flags": 0, "namedLists": {}, "subject": "😀ab", "operation": "search", "codepointSpan": [2, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 3, "length": 1, "captures": [[3, 1]]}], "lastIndex": -1, "lastGroup": null}}
        {"generator": "rows", "pattern": "zz", "flags": 0, "namedLists": {}, "subject": "ab", "operation": "search", "codepointSpan": null, "outcome": {"kind": "nomatch"}}
        """;

    /// <summary>
    /// A row whose flags and pattern name no version resolves against the DEFAULT_VERSION the
    /// RECORDER ran under, so this port has to compile it under that version and not under its own
    /// default. Since S50b the two differ on purpose, and the pair below is where the difference
    /// shows: version 1 folds fully, so it matches <c>ss</c> against <c>ß</c> and version 0 does
    /// not. Without the header's version reaching the compile, this row diverges.
    /// </summary>
    [Test]
    public void A_row_that_names_no_version_is_compiled_under_the_recorders_default()
    {
        const string recorded = """
            {"generator": "rows", "pattern": "ss", "flags": 2, "namedLists": {}, "subject": "ß", "operation": "fullmatch", "codepointSpan": null, "outcome": {"kind": "nomatch"}}
            """;

        OracleRow upstreamDefault = OracleWave.ParseRows(recorded)[0] with
        {
            DefaultVersion = (int)FuzzyRegexOptions.Version0,
        };
        OracleComparer.Compare(upstreamDefault, OracleComparer.Run(upstreamDefault)).Should().Be(OracleVerdict.Agree);

        // And the same row read as this port's own default is the divergence the pin prevents,
        // which is what proves the version is doing the work rather than the row being easy.
        OracleRow portDefault = upstreamDefault with
        {
            DefaultVersion = (int)FuzzyRegexOptions.Version1,
        };
        OracleComparer.Compare(portDefault, OracleComparer.Run(portDefault)).Should().Be(OracleVerdict.Diverge);
    }

    /// <summary>
    /// And a divergence block names the version it was compiled under, so a reader of the report
    /// never has to assume which of the two questions was asked.
    /// </summary>
    [Test]
    public void A_divergence_block_names_the_version_the_row_was_compiled_under()
    {
        OracleRow row = OracleWave.ParseRows(_recordedRows)[0] with
        {
            DefaultVersion = (int)FuzzyRegexOptions.Version0,
        };

        OracleWave.Describe(row, new NoMatchOutcome()).Should().Contain("version=V0");
        OracleWave
            .Describe(row with { DefaultVersion = (int)FuzzyRegexOptions.Version1 }, new NoMatchOutcome())
            .Should()
            .Contain("version=V1");
    }

    [Test]
    public void The_wave_agrees_with_upstream()
    {
        OracleWaveFile wave = OracleWave.Load();

        // A pattern that names no version resolves against upstream's DEFAULT_VERSION, and since
        // S50b that is NOT this port's own default - so every row is compiled under the version the
        // recorder ran under (OracleWave.Load stamps it onto each row) rather than under ours. What
        // has to hold is that the recorder stated a version at all, and one we can honour.
        // A wave whose DEFAULT_VERSION we cannot read is a wave we cannot compare against.
        wave.Header.DefaultVersion.Should()
            .BeOneOf((int)FuzzyRegexOptions.Version0, (int)FuzzyRegexOptions.Version1);
        wave.Rows.Should()
            .AllSatisfy(row =>
                row.DefaultVersion.Should().Be(wave.Header.DefaultVersion, "Load stamps the header onto every row")
            );
        wave.Rows.Should().NotBeEmpty("an empty wave would agree with anything");

        OracleRunSummary run = OracleComparer.RunWave(wave.Rows, OracleComparer.Run);
        string summary = OracleWave.WriteReport(wave.Header, wave.Rows.Count, run.Tally, run.Divergences, run.Expected);

        // Count, not the collection: a wave is hundreds of rows, and a failure that dumps every
        // block is unreadable. The report file holds them all.
        run.Divergences.Count.Should()
            .Be(
                0,
                "{0}. Full report at {1}. First divergence:{2}{3}",
                summary,
                OracleWave.ReportPath,
                Environment.NewLine,
                run.Divergences.Count > 0 ? run.Divergences[0] : ""
            );
    }

    [Test]
    public void Our_own_answers_never_contradict_themselves()
    {
        // S47 for the fuzzy limb (ledger entry 11), WIDENED BY S52c to every metamorphic invariant
        // of `docs/ORACLE-INVARIANTS.md` that one answer can break on its own - see
        // `SelfConsistency`. A property of THIS PORT'S answers alone, with upstream not consulted,
        // so it says something about every row it reaches whatever upstream says, where the wave run
        // above can only compare. That is the whole point: the oracle is blind to a bug the port
        // inherited line for line, because then the two engines agree.
        //
        // The fuzzy limb is the one with history. `fuzzy_counts` and `fuzzy_changes` are two views
        // of one edit script, so each list holds exactly as many positions as its own count;
        // upstream breaks that on four mechanisms and this port reproduced all four. The minimised
        // rows for the two S47 fixed are pinned by
        // `The_reported_changes_agree_with_the_counts_on_every_shape_that_used_to_contradict_them`
        // in `Gaps.Engine.FuzzyMatchingTests`, and this is the same property over a whole wave,
        // which is what catches a shape nobody minimised - it found mechanism D at seed 4242 on its
        // first run.
        //
        // EVERY MATCH OF A SCAN IS CHECKED TOO, not just the single-match rows: `finditer` and
        // `finditer-overlapped` are about a fifth of a default wave and the leak mechanisms show up
        // between the matches of one scan, which a single-match-only sweep would never see.
        //
        // WHAT THE FUZZY LIMB CANNOT REACH IS A POSIX PATTERN, and that is a limit of the comparison
        // shape rather than a choice. Upstream cannot be asked for the change positions of a POSIX
        // fuzzy match at all - reading them kills the interpreter, ledger entry 9 - so
        // `OracleComparer.Run` drops this port's positions as well, and there is nothing there left
        // to count. Ledger entry 11 mechanism C lives exactly there, which is why that entry
        // carries its reproduction by hand instead of relying on this. The other two limbs have no
        // such hole and run on every row.
        OracleWaveFile wave = OracleWave.Load();
        wave.Rows.Should().NotBeEmpty("an empty wave would agree with anything");

        List<string> contradictions = [];
        int checkedMatches = 0;

        foreach (OracleRow row in wave.Rows)
        {
            // Asked of nothing, exactly as `RunWave` skips them (OracleComparer:70-83): upstream
            // ran out of time or of heap, so putting the row to this engine costs a whole
            // `RowTimeout` each and a `MemoryError` row is one nothing here bounds. Six such rows
            // in a 12,000-row wave cost this test 28.7 of its 29.0 seconds before this guard, and
            // none of them can contribute a fuzzy match to count.
            if (row.Expected is TimeoutOutcome or ResourceOutcome)
            {
                continue;
            }

            IOracleOutcome? ours = OracleComparer.Run(row);
            IEnumerable<MatchOutcome> matches = ours switch
            {
                MatchOutcome single => [single],
                MatchesOutcome scan => scan.Matches,
                _ => [],
            };

            // STILL COUNTED SEPARATELY FROM THE OTHER MATCHES, and the floor below is still the
            // FUZZY one. S52c widened what is checked; it must not quietly widen what counts as a
            // wave worth believing. `tools/run-oracle.ps1`'s own documentation rests on this test
            // refusing a single-generator wave, because such a wave holds no fuzzy match at all.
            checkedMatches += matches.Count(static match => match.Fuzzy is { PositionsUnavailable: false });

            IReadOnlyList<string> broken = SelfConsistency.Check(row.Pattern, ours);
            if (broken.Count == 0)
            {
                continue;
            }

            // WHICH INVARIANTS UPSTREAM BROKE ON THE SAME ROW IS PART OF THE FAILURE, because the
            // two cases need completely different work: an invariant this port breaks where
            // upstream's own recorded answer is consistent is a PORT BUG to minimise and fix, and
            // one both engines break is the port reproducing an inherited contradiction, which is a
            // ledger entry and a judgement about which engine is right. Reading that off the row
            // costs nothing and having to re-derive it by hand is what S52c exists to stop.
            string alsoUpstream = row.SelfContradiction is { Count: > 0 } theirs
                ? " (upstream breaks " + string.Join(", ", theirs) + " on this row too)"
                : " (upstream's own answer to this row is consistent)";
            contradictions.Add(string.Join(", ", broken) + alsoUpstream + "\n" + OracleWave.Describe(row, ours));
        }

        // Without this the test would pass on a wave with no fuzzy row in it at all, which is what
        // every wave recorded before S38 was - and what a `-Generator rows` run still is.
        checkedMatches.Should().BeGreaterThan(0, "a wave with no fuzzy match in it discriminates nothing");
        contradictions.Should().BeEmpty();
    }

    [Test]
    public void The_lazy_walks_answer_exactly_what_the_eager_ones_do()
    {
        // S53b's `EnumerateMatches` and `EnumerateSplits` promise the same answer as `Matches` and
        // `Split`, found as it is asked for. They cannot share an implementation, because the eager
        // pair keeps ONE engine state across the whole walk and a lazy one cannot - a state owns
        // rented buffers and an abandoned iterator would never return them - so the two really are
        // two loops, and "the same answer" is a claim rather than a tautology.
        //
        // Upstream is not consulted here, and does not need to be: the eager pair is already
        // compared against upstream by the wave run above, so an eager-lazy disagreement is a bug
        // in exactly one of them whichever engine is right. Checked over the wave rather than over
        // hand-written cases because the shapes that separate the two loops are the awkward ones -
        // a zero-width match, a `(*SKIP)` that moves the slice, a reversed scan - and a wave holds
        // thousands of each.
        OracleWaveFile wave = OracleWave.Load();
        wave.Rows.Should().NotBeEmpty("an empty wave would agree with anything");

        List<string> disagreements = [];
        int compared = 0;

        foreach (OracleRow row in wave.Rows)
        {
            if (row.Operation is not ("finditer" or "finditer-overlapped" or "split"))
            {
                continue;
            }

            // Skipped for the reason the self-consistency sweep skips them: upstream ran out of
            // time or of heap, so asking this engine costs a whole RowTimeout for no information.
            if (row.Expected is TimeoutOutcome or ResourceOutcome)
            {
                continue;
            }

            IOracleOutcome? eager = OracleComparer.Run(row);
            IOracleOutcome? lazily = OracleComparer.Run(row, lazy: true);

            compared++;

            // Compared as their rendered descriptions, which is what a reader has to diff anyway:
            // the outcome records hold lists, so record equality would be reference equality and
            // would report every row as different.
            string first = OracleWave.Describe(row, eager);
            string second = OracleWave.Describe(row, lazily);

            if (!string.Equals(first, second, StringComparison.Ordinal))
            {
                disagreements.Add(
                    $"eager:{Environment.NewLine}{first}{Environment.NewLine}lazy:{Environment.NewLine}{second}"
                );
            }
        }

        compared.Should().BeGreaterThan(0, "a wave with no iteration row in it discriminates nothing");

        // Counted rather than dumped, as the wave run's own assertion is: a break in the lazy walk
        // disagrees on hundreds of rows at once, and the number is the measurement a control run
        // needs while the whole list is unreadable.
        disagreements
            .Count.Should()
            .Be(
                0,
                "the lazy walk answered differently on {0} of {1} iteration rows. First:{2}{3}",
                disagreements.Count,
                compared,
                Environment.NewLine,
                disagreements.Count > 0 ? disagreements[0] : ""
            );
    }

    [Test]
    public void The_self_consistency_checker_fires_on_a_contradiction_and_not_on_a_narrowing()
    {
        // THE SWEEP ABOVE CANNOT TEST THIS. It runs the checker over whatever a wave happens to
        // hold, and a checker that silently stopped firing would leave it green - which is the one
        // failure mode the whole of S52c exists to remove. These are hand-built answers, so each
        // case is the checker's own behaviour and nothing else's. The recorder's Python twin is
        // guarded the same way, in `_self_check` in `tools/record-oracle.py`.
        static MatchOutcome Match(params OracleGroup[] groups) => new(groups, -1, null);
        static OracleGroup Group(int number, int index, int length) =>
            new(number, true, index, length, [new OracleSpan(index, length)]);

        // A group whose span is not inside the match span, on a pattern with nothing that excuses
        // it. Match (0, 1), group 1 at (1, 1).
        MatchOutcome escaped = Match(Group(0, 0, 1), Group(1, 1, 1));
        SelfConsistency.Check("a(b)", escaped).Should().Equal("group-spans-inside-match");

        // And the two narrowings, each measured in
        // `tools/probes/upstream-free-tier-invariant-grounds.py` sections 3 and 5: `\K` moves the
        // reported start, and a lookaround consumes nothing, so on those patterns the SAME answer
        // is legitimate and the checker must stay silent.
        SelfConsistency.Check(@"a\K(b)", escaped).Should().BeEmpty();
        SelfConsistency.Check("a(?=(b))", escaped).Should().BeEmpty();

        // A `lastindex` naming a group that did not participate.
        MatchOutcome absent = new([Group(0, 0, 1), new OracleGroup(1, false, 0, 0, [])], 1, null);
        SelfConsistency.Check("(a)?b", absent).Should().Equal("lastindex-participated");

        // Counts and change positions that describe different edit scripts - ledger entry 11.
        MatchOutcome miscounted = Match(Group(0, 0, 2)) with
        {
            Fuzzy = new OracleFuzzy(1, 0, 0, [], [], []),
        };
        SelfConsistency.Check("(?:ab){e<=1}", miscounted).Should().Equal("fuzzy-counts-match-changes");

        // A POSIX row records counts and no positions at all (ledger entry 9), so there is nothing
        // to contradict and the checker must not read the missing lists as three empty ones.
        MatchOutcome unavailable = Match(Group(0, 0, 2)) with
        {
            Fuzzy = new OracleFuzzy(1, 0, 0, null, null, null),
        };
        SelfConsistency.Check("(?p)(?:ab){e<=1}", unavailable).Should().BeEmpty();

        // And an answer that breaks nothing, so a checker that fires on everything fails here.
        SelfConsistency.Check("a(b)", Match(Group(0, 0, 2), Group(1, 1, 1))).Should().BeEmpty();
        SelfConsistency.Check("a(b)", new NoMatchOutcome()).Should().BeEmpty();
    }

    [Test]
    public void Every_expected_divergence_still_diverges()
    {
        // The staleness alarm for ExpectedDivergences, and the only strict thing about that list: a
        // wave's rows come from a random seed, so there is no row identity to pin and no predicate
        // over the recorded half alone that is narrow enough to demand a divergence. Each entry
        // therefore carries the minimised row its family was found on, and this runs those through
        // the live engine. The day Phase 7 ports `search_start`, or someone fixes an entry's cause
        // without removing the entry, the row stops diverging and this goes red - which is what
        // Chromium's TestExpectations lacks and pytest's xfail_strict has.
        ExpectedDivergences.All.Should().NotBeEmpty();

        foreach (ExpectedDivergence entry in ExpectedDivergences.All)
        {
            entry.Reason.Should().NotBeEmpty("{0}: an unexplained expected divergence is a hidden one", entry.Id);
            entry.PinnedBy.Should().NotBeEmpty("{0}: the permanent test is the second alarm", entry.Id);

            // One row for a family a predicate describes, and every listed row for an entry keyed
            // on rows - `bounded-lazy-repeat-partial` is one, because no predicate for it exists
            // that does not also swallow a genuine missed partial. Each listed row has to earn its
            // place, or the list grows entries nobody can tell are stale.
            IReadOnlyList<OracleRow> rows = OracleWave.ParseRows(entry.Example);
            rows.Should().NotBeEmpty("{0}: an entry with no example cannot be checked for staleness", entry.Id);

            foreach (OracleRow row in rows)
            {
                IOracleOutcome ours = OracleComparer
                    .Run(row)
                    .Should()
                    .NotBeNull("{0}: the example must be a row this port can answer", entry.Id)
                    .And.Subject.Should()
                    .BeAssignableTo<IOracleOutcome>()
                    .Subject;

                OracleComparer
                    .Compare(row, ours)
                    .Should()
                    .Be(
                        OracleVerdict.Diverge,
                        "{0}: example row {1} is what makes the entry current",
                        entry.Id,
                        row.Number
                    );
                ExpectedDivergences
                    .For(row, ours)
                    .Should()
                    .BeSameAs(entry, "{0}: an entry must account for its own example row {1}", entry.Id, row.Number);
            }
        }
    }

    [Test]
    public void A_row_this_port_answers_upstreams_way_is_not_accounted_for_by_the_three_judged_entries()
    {
        // The over-classification guard for S48b's second sitting, and the control its three entries
        // rest on. Each is keyed on exact judged questions AND on this port's exact rendered answer,
        // so the thing to prove is that a DIFFERENT answer to the same question is reported rather
        // than swallowed - which is the failure mode the S47b audit found in the `(?b)` entry and
        // the owner ruled against on 2026-09-14.
        //
        // Two wrong answers per row, both realistic rather than arbitrary:
        //
        //   * upstream's OWN drawn answer, which is what this port gave on `73463` before S48b -
        //     verified by bisection against a worktree at c8165b5 - so it is the regression these
        //     entries could plausibly hide rather than a fabricated one; and
        //   * no match at all, which is what an unrelated engine defect landing on one of these rows
        //     looks like.
        string[] judged =
        [
            "posix-fuzzy-contradicts-its-own-flagless-answer",
            "atomic-group-leaks-a-change-position",
            "reversed-lookahead-change-at-the-match-start",
        ];

        foreach (string id in judged)
        {
            ExpectedDivergence entry = ExpectedDivergences
                .All.Should()
                .ContainSingle(e => string.Equals(e.Id, id, StringComparison.Ordinal))
                .Subject;

            foreach (OracleRow row in OracleWave.ParseRows(entry.Example))
            {
                ExpectedDivergences
                    .For(row, row.Expected)
                    .Should()
                    .BeNull(
                        "{0}: a port that reproduced upstream's own answer to row {1} is not this family",
                        id,
                        row.Number
                    );
                ExpectedDivergences
                    .For(row, new NoMatchOutcome())
                    .Should()
                    .BeNull("{0}: a total failure on row {1} is a defect, not this family", id, row.Number);
            }
        }
    }

    [Test]
    public void A_row_the_anchor_pin_does_not_explain_is_not_accounted_for()
    {
        // The control for `fuzzy-insertion-at-a-pinned-anchor`, written before the entry was
        // trusted with a wave: an entry whose predicate cannot tell its own family from a defect
        // is a silencer, not a classification (DECISIONS 2026-09-12).
        //
        // The entry is keyed on an ablation - switch off the S57c anchor pin, and this port gives
        // upstream's recorded answer - so the two answers to test it with are the two an ablation
        // cannot explain:
        //
        //   * upstream's OWN answer, which is what this port gave before S57c. If the entry
        //     accepted it, a revert of the fix would be classified as the fix; and
        //   * no match at all, which is the shape of an unrelated engine defect landing on one of
        //     these rows, and the only one of the two that is not already excluded by the row
        //     having to diverge before `For` is ever called.
        //
        // What neither case reaches is a change to the SHAPE of the pin rule, because the ablation
        // restores upstream's answer under a broken rule too. The entry's own text says so, with
        // the measurement; the rule's shape is held by `InheritedIssueTests` instead.
        ExpectedDivergence entry = ExpectedDivergences
            .All.Should()
            .ContainSingle(static e =>
                string.Equals(e.Id, "fuzzy-insertion-at-a-pinned-anchor", StringComparison.Ordinal)
            )
            .Subject;

        foreach (OracleRow row in OracleWave.ParseRows(entry.Example))
        {
            ExpectedDivergences
                .For(row, row.Expected)
                .Should()
                .BeNull("a port that reproduced upstream's own answer to row {0} is not this family", row.Number);
            ExpectedDivergences
                .For(row, new NoMatchOutcome())
                .Should()
                .BeNull("a total failure on row {0} is a defect, not this family", row.Number);
        }
    }

    [Test]
    [Arguments("full-fold-fuzzy-deletion")]
    [Arguments("full-fold-backreference-leftovers")]
    [Arguments("full-fold-backreference-retry")]
    public void A_row_the_fold_fix_does_not_explain_is_not_accounted_for(string id)
    {
        // The control for the three full-fold entries (S83, S84), built the same way as the anchor
        // pin's above: upstream's own answer is what this port gave before the fix, so accepting it
        // would classify a revert of the fix as the fix; and no match stands in for an unrelated
        // defect. Most of these rows' upstream answer IS no match, so for them the two cases
        // coincide.
        ExpectedDivergence entry = ExpectedDivergences
            .All.Should()
            .ContainSingle(e => string.Equals(e.Id, id, StringComparison.Ordinal))
            .Subject;

        foreach (OracleRow row in OracleWave.ParseRows(entry.Example))
        {
            ExpectedDivergences
                .For(row, row.Expected)
                .Should()
                .BeNull("a port that reproduced upstream's own answer to row {0} is not this family", row.Number);
            ExpectedDivergences
                .For(row, new NoMatchOutcome())
                .Should()
                .BeNull("a total failure on row {0} is a defect, not this family", row.Number);
        }
    }

    [Test]
    public void A_partial_of_the_wrong_span_is_not_accounted_for_as_a_boundary_partial()
    {
        // The control for `boundary-at-the-end-of-the-text`, named in that entry's own Reason.
        //
        // The entry is keyed on this port's ANSWER rather than on the pattern's shape, so what holds
        // it narrow is that the answer is the escalation's signature. `Matcher.DoMatch` builds
        // exactly one thing when a boundary runs the attempt out of text: a partial spanning the
        // attempt's start to the end of the available text, with `ClearGroups` run, so no capture
        // group is set and `lastindex` is -1. Every other answer on the same row is a different
        // defect, and the five below are the five ways to be one:
        //
        //   * the right shape over the wrong span, which is a partial the escalation did not build
        //   * a zero-width partial at the truncation point, which is the fault the escalation's
        //     consumed-something narrowing exists to prevent and which Control A of this slice
        //     caught the predicate classifying
        //   * the right span with a capture group still set, the shape a bug in the clearing
        //     itself would give
        //   * the right span as a COMPLETE match, which is the port claiming to have matched text
        //     upstream says it cannot
        //   * no match at all, the shape of an unrelated engine defect landing on this row
        //
        // Both example rows, because the span limb turns round under `(?r)`: a reversed match runs
        // out of text at its start, so the escalated partial reaches position 0 and its far end is
        // wherever the attempt had got to. The wrong span therefore has to be spelt in the row's own
        // direction, and it is the reversed row that caught the limb missing.
        ExpectedDivergence entry = ExpectedDivergences
            .All.Should()
            .ContainSingle(static e => string.Equals(e.Id, "boundary-at-the-end-of-the-text", StringComparison.Ordinal))
            .Subject;

        foreach (OracleRow row in OracleWave.ParseRows(entry.Example))
        {
            bool reversed = row.Pattern.Contains("(?r)", StringComparison.Ordinal);
            (MatchOutcome right, MatchOutcome wrongSpan) = reversed
                ? (Escalated(0, 1), Escalated(1, row.Subject.Length))
                : (Escalated(0, row.Subject.Length), Escalated(0, row.Subject.Length - 1));

            // The answer the escalation really builds, so each refusal below differs from an
            // accounted answer in exactly the one thing it is about.
            ExpectedDivergences.For(row, right).Should().NotBeNull("{0}", row.Pattern);

            ExpectedDivergences.For(row, wrongSpan).Should().BeNull("the span does not reach the end of the text");
            ExpectedDivergences
                .For(row, Escalated(reversed ? 0 : row.Subject.Length, reversed ? 0 : row.Subject.Length))
                .Should()
                .BeNull("a zero-width partial at the truncation point is the fault, not the family");
            ExpectedDivergences
                .For(row, right with { Groups = [right.Groups[0], Taken(1, 0, 1)] })
                .Should()
                .BeNull("the groups were not cleared");
            ExpectedDivergences
                .For(row, right with { Partial = false })
                .Should()
                .BeNull("this one is a complete match");
            ExpectedDivergences.For(row, new NoMatchOutcome()).Should().BeNull("a total failure is a defect");

            // And the other half, through the LIVE engine, so this fails the day the port stops
            // diverging on the row as well as the day the predicate stops reaching it.
            IOracleOutcome ours = OracleComparer.Run(row)!;

            OracleComparer.Compare(row, ours).Should().Be(OracleVerdict.Diverge, "{0}", row.Pattern);
            ExpectedDivergences
                .For(row, ours)
                .Should()
                .NotBeNull("{0} -> [{1}]", row.Pattern, ours.Describe())
                .And.Subject.As<ExpectedDivergence>()
                .Id.Should()
                .Be("boundary-at-the-end-of-the-text");
        }

        static MatchOutcome Escalated(int index, int end) =>
            new([Taken(0, index, end - index)], LastIndex: -1, LastGroup: null, Partial: true);

        static OracleGroup Taken(int number, int index, int length) =>
            new(number, Success: true, index, length, [new OracleSpan(index, length)]);
    }

    [Test]
    public void An_accounted_divergence_is_reported_but_does_not_fail_the_run()
    {
        // Half one: the wave loop reclassifies, tallies and renders it as EXPECTED rather than
        // dropping it. Without the rendering half, a classified row would be invisible in the report
        // and the list would be a silencer rather than a ledger.
        OracleRow accounted = OracleWave.ParseRows(ExpectedDivergences.All[0].Example)[0];
        OracleRunSummary run = OracleComparer.RunWave([accounted], OracleComparer.Run);

        run.Tally.GetValueOrDefault(OracleVerdict.Diverge).Should().Be(0);
        run.Tally.GetValueOrDefault(OracleVerdict.Expected).Should().Be(1);
        run.Divergences.Should().BeEmpty();
        run.Expected.Should().ContainSingle().Which.Should().StartWith("EXPECTED " + ExpectedDivergences.All[0].Id);

        // Half two: an ordinary wrong answer on a row no entry covers is still a divergence, so the
        // reclassification above is a statement about the list rather than about RunWave.
        OracleRow ordinary = OracleWave.ParseRows(_recordedRows)[0];
        OracleRunSummary wrong = OracleComparer.RunWave([ordinary], static _ => new NoMatchOutcome());

        wrong.Tally.GetValueOrDefault(OracleVerdict.Expected).Should().Be(0);
        wrong.Divergences.Should().ContainSingle().Which.Should().StartWith("DIVERGE row");
    }

    /// <summary>
    /// The row a port that ignored <c>(?b)</c> would get wrong, recorded by
    /// <c>python tools/record-oracle.py --rows</c> on 2026-09-14 against regex 2026.9.10.
    /// </summary>
    /// <remarks>
    /// Chosen because upstream's two answers to it DIFFER and upstream's <c>(?b)</c> answer is the
    /// right one: with the flag it matches <c>cat</c> against the second alternative for nothing,
    /// and without it the first alternative wins with one deletion at 3. So this port's answer must
    /// be upstream's flagged answer, and <see cref="OracleRow.BestmatchFree"/> is precisely what a
    /// port that dropped the flag would say.
    /// </remarks>
    private const string _bestmatchFlagIgnoredRow =
        """{"generator": "fuzzy", "pattern": "(?b)(?:cats|cat){e<=1}", "flags": 0, "namedLists": {}, "subject": "cat", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false}, "bestmatchFreeOutcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1], "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": [3]}}}""";

    [Test]
    public void A_bestmatch_row_answered_as_though_the_flag_were_absent_is_not_accounted_for()
    {
        // S47b, from the independent audit of S44-S46. `bestmatch-loses-a-candidate` classified any
        // `(?b)` row whose divergence landed on upstream's own flagless answer, and the entry's own
        // Reason admitted what that cannot tell apart: a port that IGNORED `(?b)` altogether answers
        // the flagless answer on every row, so the widest possible defect in the feature the entry
        // is about would have been tallied EXPECTED on every row it touched.
        OracleRow row = OracleWave.ParseRows(_bestmatchFlagIgnoredRow)[0];

        row.BestmatchFree.Should().NotBeNull("the row is only a discriminator if upstream answered both ways");
        row.BestmatchFree.Describe()
            .Should()
            .NotBe(row.Expected.Describe(), "a row where the flag changes nothing discriminates nothing");

        ExpectedDivergences.For(row, row.BestmatchFree).Should().BeNull();
    }

    [Test]
    public void A_bestmatch_row_answered_more_cheaply_than_the_judged_answer_is_not_accounted_for()
    {
        // S57e. Rows 25 and 27 of `bestmatch-loses-a-candidate` cannot use the flagless
        // discriminator, because upstream's flagless answer is its flagged answer on them, so the
        // entry keys them on this port's judged answer instead. "Cheaper than upstream" on its own
        // would not do: a port that dropped an error while matching the same span says exactly that,
        // and it is a defect, not this family.
        OracleRow row = OracleWave
            .ParseRows(
                ExpectedDivergences
                    .All.Single(static entry =>
                        string.Equals(entry.Id, "bestmatch-loses-a-candidate", StringComparison.Ordinal)
                    )
                    .Example
            )
            .Single(static candidate =>
                string.Equals(candidate.Pattern, @"(?b)(?e)\b(?:\d+\d\s){e<=3}", StringComparison.Ordinal)
            );

        MatchOutcome theirs = (MatchOutcome)row.Expected;
        theirs.Fuzzy.Should().NotBeNull();

        // Upstream answers (0, 6) for three substitutions; this port answers it for one substitution
        // and one insertion. The answer below is the same span for two substitutions - cheaper than
        // upstream, and not what this port says.
        MatchOutcome cheaperButNotOurs = theirs with
        {
            Fuzzy = new OracleFuzzy(2, 0, 0, [3, 4], [], []),
        };

        ExpectedDivergences.For(row, cheaperButNotOurs).Should().BeNull();
    }

    /// <summary>
    /// Six fabricated rows this entry must REFUSE, recorded by
    /// <c>python tools/record-oracle.py --rows</c> on 2026-09-14 against regex 2026.9.10.
    /// </summary>
    /// <remarks>
    /// Every one is the same trap in a different spelling: the pattern offers a Turkic letter and
    /// the answer lands on THAT SAME letter, which every engine matches without reading a <c>T</c>
    /// row, so a divergence there is some other defect. Row 1 is S45's own blind-review finding 6,
    /// recorded there as beyond any predicate. The other five are the mirrors and the
    /// text-that-is-never-matched spellings that S47b's two blind passes found walking back in
    /// after each fix: the dotted twin of row 1, then a group name in all three spellings and a
    /// comment, each of which puts the partner letter in the pattern TEXT where nothing matches it.
    /// <para>
    /// They are held apart from the rows that must be CLASSIFIED rather than numbered inside one
    /// list, because the numbering is exactly what went wrong: the second blind pass found this
    /// comment describing six rows when there were seven, every claim past the second off by one.
    /// </para>
    /// </remarks>
    private const string _turkicRowsToRefuse = """
        {"generator": "rows", "pattern": "(?i)ı.", "flags": 0, "namedLists": {}, "subject": "ıx", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "rows", "pattern": "(?i)İ.", "flags": 0, "namedLists": {}, "subject": "İx", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "rows", "pattern": "(?P<I>\u0131).", "flags": 2, "namedLists": {}, "subject": "\u0131x", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 1, "lastGroup": "I", "partial": false}}
        {"generator": "rows", "pattern": "(?#I)\u0131.", "flags": 2, "namedLists": {}, "subject": "\u0131x", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "rows", "pattern": "(?P<i>\u0130).", "flags": 2, "namedLists": {}, "subject": "\u0130x", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 1, "lastGroup": "i", "partial": false}}
        {"generator": "rows", "pattern": "(?<I>\u0131).", "flags": 2, "namedLists": {}, "subject": "\u0131x", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 1, "lastGroup": "I", "partial": false}}
        {"generator": "rows", "pattern": "(?i)\u0131", "flags": 0, "namedLists": {}, "subject": "\u0131", "operation": "sub", "template": "X", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "X", "count": 1}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}]}
        {"generator": "rows", "pattern": "(?i)\\w", "flags": 0, "namedLists": {}, "subject": "x\u0131", "operation": "sub", "template": "Q", "count": -1, "codepointSpan": null, "outcome": {"kind": "sub", "text": "QQ", "count": 2}, "scanMatches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}, {"groups": [{"number": 0, "success": true, "index": 1, "length": 1, "captures": [[1, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [1, 2]}]}
        """;

    /// <summary>
    /// Six rows this entry must CLASSIFY, five of them real wave rows, recorded the same day.
    /// </summary>
    /// <remarks>
    /// Row 1 is <c>fullmatch('aı', 'aI', I)</c>, the <c>case-folding</c> generator's commonest
    /// shape and the one S45's blind review found a start-character test missing: upstream matches
    /// (0, 2) and this port does not match at all. <b>A total failure, which is why the
    /// discriminator cannot be "the port matched"</b> - the slice text asked for that clause and it
    /// would have deleted most of the family. Row 2 is the shape whose divergence is its SECOND
    /// match rather than its first.
    /// <para>
    /// <b>Rows 3 to 6 are what stop the narrowing going too far, and each killed a draft of the
    /// rule.</b> Row 3 is row 6150 of the seed-31337 <c>interactions</c> wave minimised: the pattern
    /// spells U+0131 AND holds a class that only a <c>T</c> row lets reach the second U+0131. Rows 4
    /// and 5 are rows 50168 and 52004 of the seed-99991 6000-row gate verbatim, both from
    /// <c>case-folding</c>: the first spells the <c>i</c> upstream's span lands on AND the
    /// <c>İ</c> whose <c>T</c> row is the whole divergence, and the second reaches the pairing
    /// through a BACKREFERENCE. Row 6 is row 52004's named twin, which S47b's second blind pass
    /// wrote: <c>(?P&lt;g&gt;i)(?P=g)</c> carries neither <c>[</c> nor <c>\</c>, so the fix for the
    /// unnamed form left it unclassified and it would have reddened a wave.
    /// </para>
    /// </remarks>
    private const string _turkicRowsToClassify = """
        {"generator": "rows", "pattern": "aI", "flags": 2, "namedLists": {}, "subject": "aı", "operation": "fullmatch", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "rows", "pattern": "[^I]", "flags": 2, "namedLists": {}, "subject": "aıb", "operation": "finditer", "codepointSpan": null, "outcome": {"kind": "matches", "matches": [{"groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [0, 1]}, {"groups": [{"number": 0, "success": true, "index": 2, "length": 1, "captures": [[2, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "codepointSpan": [2, 3]}]}}
        {"generator": "rows", "pattern": "ı[A-Z]", "flags": 2, "namedLists": {}, "subject": "ıı", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "case-folding", "pattern": "iİ", "flags": 16386, "namedLists": {}, "subject": "iiİ", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
        {"generator": "case-folding", "pattern": "(i)\\1", "flags": 16386, "namedLists": {}, "subject": "Iıi", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 1, "lastGroup": null, "partial": false}}
        {"generator": "rows", "pattern": "(?P<g>i)(?P=g)", "flags": 16386, "namedLists": {}, "subject": "I\u0131i", "operation": "match", "codepointSpan": [0, 2], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 2, "captures": [[0, 2]]}, {"number": 1, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": 1, "lastGroup": "g", "partial": false}}
        """;

    [Test]
    public void A_failure_on_a_row_whose_turkic_letter_needs_no_turkic_rule_is_not_accounted_for()
    {
        // S47b, from the independent audit of S44-S46, closing the false positive S45's second blind
        // pass reproduced and recorded as unfixable. A total engine failure on `(?i)ı.` against `ıx`
        // was tallied EXPECTED, because the predicate asked only that a Turkic letter be in play -
        // and U+0131 is in play here against ITSELF, which every engine matches without consulting
        // a `T` row at all.
        //
        // ALL OF THEM, in one loop, because each of the first two fixes closed the spelling in front
        // of it and left its twin open - `(?i)İ.` after `(?i)ı.`, then four group-name and comment
        // spellings after those. Every row here is the identical trap and a fix that passes some of
        // them is the bug; the port is fabricated to fail outright on each, which is what an
        // unrelated engine defect landing on such a row would look like.
        //
        // THE LAST TWO ARE S52's, and the second of them is why that slice has a SEPARATE row-keyed
        // entry for span-less answers instead of widening the predicate. Its draft fed the
        // recorder's `scanMatches` into this rule's covered-letter union, and the blind review
        // reproduced the whole trap again on a `sub`: `(?i)\w` over 'xı' reaches U+0131 through a
        // `\w`, which every engine matches without consulting a `T` row, and the pattern's `\`
        // short-circuits the pairing test - so a total engine failure was tallied EXPECTED. Keep
        // both rows: the first is the shape that MUST still be refused with the scan present, and
        // the second is the shape the scan itself introduced.
        foreach (OracleRow row in OracleWave.ParseRows(_turkicRowsToRefuse))
        {
            ExpectedDivergences.For(row, new NoMatchOutcome()).Should().BeNull("{0}", row.Pattern);
        }
    }

    [Test]
    public void The_turkic_family_is_still_accounted_for_on_the_shapes_it_was_narrowed_around()
    {
        // The other half of the narrowing, and the half that makes it a narrowing rather than a
        // deletion. Every row goes through the LIVE engine, so this fails the day the port stops
        // diverging on one as well as the day the predicate stops reaching it - and the first row's
        // divergence is a total failure, the same shape as the six that must be refused, which is
        // why the discriminator cannot be "the port matched".
        foreach (OracleRow row in OracleWave.ParseRows(_turkicRowsToClassify))
        {
            IOracleOutcome ours = OracleComparer.Run(row)!;

            OracleComparer.Compare(row, ours).Should().Be(OracleVerdict.Diverge, "{0}", row.Pattern);
            ExpectedDivergences
                .For(row, ours)
                .Should()
                .NotBeNull("{0}", row.Pattern)
                .And.Subject.As<ExpectedDivergence>()
                .Id.Should()
                .Be("turkic-default-folding");
        }
    }

    [Test]
    public void An_answer_with_no_spans_is_classified_by_the_row_keyed_turkic_entry()
    {
        // S52. The three span-less shapes - a `sub`, a `split` and a `subf` whose template throws -
        // plus the two real wave rows that made them matter. Each goes through the LIVE engine, so
        // this fails the day this port stops diverging on one as well as the day the entry stops
        // reaching it, and it asserts the ID so that a row quietly migrating to the predicate-keyed
        // sibling - which is what S52's blind review proved is unsafe here - shows up as a failure.
        foreach (OracleRow row in OracleWave.ParseRows(ExpectedDivergences.SpanlessTurkicRows))
        {
            IOracleOutcome ours = OracleComparer.Run(row)!;

            OracleComparer.Compare(row, ours).Should().Be(OracleVerdict.Diverge, "{0}", row.Pattern);
            ExpectedDivergences
                .For(row, ours)
                .Should()
                .NotBeNull("{0} -> [{1}]", row.Pattern, ours.Describe())
                .And.Subject.As<ExpectedDivergence>()
                .Id.Should()
                .Be("turkic-default-folding-without-spans");

            // AND THE OTHER HALF, which the first version of this test did not have and which S52's
            // second blind pass reproduced as a defect: being one of the five listed QUESTIONS must
            // not be enough. The entry read nothing from the port's answer, so a total engine failure
            // on a listed row was tallied EXPECTED - the widest possible pin on exactly the rows a
            // pin exists to keep narrow. `NoMatchOutcome` is the same fabrication
            // `_turkicRowsToRefuse` uses above.
            ExpectedDivergences.For(row, new NoMatchOutcome()).Should().BeNull("{0}", row.Pattern);
        }
    }

    [Test]
    public void Corrupting_a_recorded_row_is_reported_as_a_divergence()
    {
        IReadOnlyList<OracleRow> rows = OracleWave.ParseRows(_recordedRows);
        rows.Should().HaveCount(3);

        // Half one: an engine that answers exactly what upstream answered must produce no
        // divergence at all. Without this the test below would pass on a comparator that reports
        // everything, which discriminates nothing.
        OracleRunSummary honest = OracleComparer.RunWave(rows, row => row.Expected);
        honest.Divergences.Should().BeEmpty();
        honest.Tally[OracleVerdict.Agree].Should().Be(3);

        // Half two: one corruption per row, in the three shapes a real engine defect takes - a
        // span off by one, a group missing from the match, and a no-match reported as a match.
        // Driven through the same RunWave the real wave goes through, so the rendered report is
        // exercised too, not just the verdict.
        OracleRunSummary corrupted = OracleComparer.RunWave(
            rows,
            row =>
                row.Number switch
                {
                    1 => WithoutLastGroup(row.Expected),
                    2 => ShiftFirstGroup(row.Expected),
                    _ => rows[1].Expected,
                }
        );

        corrupted.Tally.Should().Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Diverge] = 3 });
        corrupted.Divergences.Should().HaveCount(3);

        // Each block must name its row and quote both sides, or a divergence in a 600-row wave is
        // unactionable.
        corrupted.Divergences[0].Should().StartWith("DIVERGE row 1 (rows) search flags=0x0");
        corrupted.Divergences[0].Should().Contain("upstream match 0:(0,2)[(0,2)] 1:(0,1)[(0,1)] 2:(1,1)[(1,1)]");
        corrupted.Divergences[0].Should().Contain("port     match 0:(0,2)[(0,2)] 1:(0,1)[(0,1)]");
        corrupted.Divergences[1].Should().Contain("port     match 0:(4,1)[(3,1)]");
        corrupted.Divergences[2].Should().Contain("upstream no match");
    }

    [Test]
    public void A_row_this_port_cannot_answer_in_time_is_a_divergence_and_never_agreement()
    {
        // S26 gave each row a deadline. Two properties make that a fix rather than a mask, and
        // neither is visible in a wave run - a green wave looks the same either way - so both are
        // pinned here. Without the first, a runaway row hangs the run instead of failing it, which
        // is what it did while S17's control was being re-run: six minutes of silence on a wave the
        // honest engine answers in 589ms. Without the second, the deadline would *hide* a
        // divergence by filing a timed-out row as this port rejecting the input.
        OracleComparer
            .RowTimeout.Should()
            .BeLessThan(TimeSpan.FromMinutes(1), "a row that never finishes must fail the run, not hang it");
        OracleComparer.RowTimeout.Should().BePositive();

        ErrorOutcome timedOut = ErrorOutcome.From(
            new System.Text.RegularExpressions.RegexMatchTimeoutException("aaa", "(a|a)*b", OracleComparer.RowTimeout),
            whileMatching: true
        );

        // Upstream answered; we ran out of time. That is a wrong answer, not a rejection.
        OracleRow matched = OracleWave.ParseRows(_recordedRows)[0];
        OracleComparer.Compare(matched, timedOut).Should().Be(OracleVerdict.Diverge);

        // The allow-list of exceptions that count as "this port rejected the input" must not grow
        // to include this one. Asserted against a rejection upstream raised *while matching*, and
        // one whose class is not `error`, because those are the two conditions under which the
        // allow-list is the only thing left deciding: with either absent, the phase rule or the
        // message comparison answers first and this assertion would pass without touching it.
        OracleRow rejectedWhileMatching = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "x", "flags": 0, "namedLists": {}, "subject": "x", "operation": "search", "codepointSpan": null, "outcome": {"kind": "error", "exception": "TypeError", "message": "expected string", "whileMatching": true}}
            """
        )[0];
        OracleComparer.Compare(rejectedWhileMatching, timedOut).Should().Be(OracleVerdict.Diverge);

        // And the deadline reaches the engine, which is the half a value assertion cannot see: this
        // row does not stop on its own, so the test returning at all is the proof. 50ms rather than
        // `RowTimeout`, so proving it costs no wall clock - upstream's own figure for this pattern
        // at this length is 23 seconds (`RepeatTests`), and this port's is the same curve.
        //
        // What is deliberately *not* pinned: that the one-argument `Run` passes `RowTimeout` rather
        // than something else. Pinning that token needs a row that runs for the whole deadline, so
        // it would cost ten seconds on every oracle run for ever, to catch an edit to a single line
        // sitting directly under the field whose remarks explain why it exists.
        OracleRow catastrophic = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(a|a)*b", "flags": 0, "namedLists": {}, "subject": "aaaaaaaaaaaaaaaaaaaaaaaaaaxb", "operation": "search", "codepointSpan": null, "outcome": {"kind": "nomatch"}}
            """
        )[0];

        IOracleOutcome? ranOut = OracleComparer.Run(catastrophic, TimeSpan.FromMilliseconds(50));

        ranOut
            .Should()
            .BeOfType<ErrorOutcome>()
            .Which.Exception.Should()
            .Be(nameof(System.Text.RegularExpressions.RegexMatchTimeoutException));
        OracleComparer.Compare(catastrophic, ranOut).Should().Be(OracleVerdict.Diverge);
    }

    [Test]
    public void A_row_upstream_never_finished_is_skipped_counted_and_never_put_to_this_port()
    {
        // S40a's half of the recorder deadline. Upstream can loop for ever on a row a generator
        // drew - `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` on 2026.7.19 - and before the deadline
        // existed one such row killed a whole 6000-row wave with no file written at all. The
        // recorder now writes this shape instead.
        OracleRow hung = OracleWave.ParseRows(
            """
            {"generator": "verbs", "pattern": ".?x(?>a(*SKIP)z)", "flags": 0, "namedLists": {}, "subject": "xzxa", "operation": "search", "codepointSpan": null, "outcome": {"kind": "timeout", "seconds": 10.0}}
            """
        )[0];

        hung.Expected.Should().BeOfType<TimeoutOutcome>().Which.Seconds.Should().Be(10.0);

        // Whatever this port answers, the verdict is the same: there is nothing to compare against.
        // Both arms matter - filing it as a divergence would fail the run on upstream's bug, and
        // filing it as agreement would let a port that also hangs score as parity.
        OracleComparer.Compare(hung, new NoMatchOutcome()).Should().Be(OracleVerdict.Timeout);
        OracleComparer.Compare(hung, actual: null).Should().Be(OracleVerdict.Timeout);

        // And the engine is never asked, which is the part a verdict assertion cannot see: a wave
        // carrying several of these would otherwise spend RowTimeout on each for no information.
        var asked = new List<int>();
        OracleRunSummary run = OracleComparer.RunWave(
            [hung],
            row =>
            {
                asked.Add(row.Number);
                return new NoMatchOutcome();
            }
        );

        asked.Should().BeEmpty("a row with no ground truth is asked of nothing");
        run.Divergences.Should().BeEmpty();

        // Counted under a verdict of its own rather than dropped, which is what puts it in the
        // report's summary line: a generator that starts drawing rows upstream cannot answer has
        // quietly stopped testing what it claims to, and this number is where that shows. Asserted
        // as the WHOLE tally, so a future edit cannot fold it back into `unsupported` - which would
        // make a hanging upstream row read as a gap in this port's coverage.
        //
        // `WriteReport` is deliberately not called here: it writes TestResults/oracle/report.txt,
        // and clobbering the real wave's divergence report to check a substring of one line is the
        // worse trade - the more so as TUnit runs these in parallel with the wave run itself.
        run.Tally.Should().Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Timeout] = 1 });
    }

    [Test]
    public void A_row_carrying_its_own_deadline_is_compared_rather_than_skipped()
    {
        // S52's timeout rows, and the one place in the wave where a `timeout` outcome is an ANSWER
        // instead of a missing one. The difference is the row's own `timeout` field.
        //
        // Without it, a timeout says only that upstream did not finish inside the recorder's blanket
        // ten seconds - a statement about wall clock on the recording machine, which is why
        // `A_row_upstream_never_finished_is_skipped_counted_and_never_put_to_this_port` skips it. WITH
        // it, the row was drawn from a family measured catastrophic on BOTH engines at a length far
        // past the knee, and "the call raises rather than running past its budget" is then a property
        // of the engine rather than of the machine. Measured 2026-09-15 on regex 2026.9.10 and this
        // port at Release, at the generator's own shortest subject of 40 'a's: all ten shapes against
        // all eight operations are still running after 5s on both engines, 80 of 80 cells each,
        // against the 0.25s the rows carry - a margin over 20x, so no plausible machine turns one
        // verdict into the other. See `tools/probes/timeout-row-margin.py` and its port half.
        //
        // THE SUBJECT BELOW IS 32 'a's, which is SHORTER than the generator draws and is not a
        // measurement of the family: `--knee` puts the shapes' knees between 24 and 36, so 32 is
        // past this pattern's (24) and short of `(?:a|aa)+$`'s (36). It is chosen to keep this test
        // cheap, and it is sound here only because the one shape it uses blows up well below it.
        OracleRow budgeted = OracleWave.ParseRows(
            """
            {"generator": "timeout", "pattern": "(a|a)+$", "flags": 0, "namedLists": {}, "subject": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!", "operation": "search", "codepointSpan": null, "timeout": 0.05, "outcome": {"kind": "timeout", "seconds": 0.05}}
            """
        )[0];

        budgeted.Timeout.Should().Be(0.05);
        budgeted.Expected.Should().BeOfType<TimeoutOutcome>().Which.Seconds.Should().Be(0.05);

        // This port running out of the same budget is the agreement. `ErrorOutcome` rather than a
        // `TimeoutOutcome` of our own, because that is what the engine actually produces and inventing
        // a second shape for it here would hide which of the two the port really gave.
        ErrorOutcome ranOut = ErrorOutcome.From(
            new System.Text.RegularExpressions.RegexMatchTimeoutException("aaa", "(a|a)+$", TimeSpan.FromSeconds(0.05)),
            whileMatching: true
        );
        OracleComparer.Compare(budgeted, ranOut).Should().Be(OracleVerdict.Agree);

        // The same exception raised while COMPILING does not, which keeps the phase rule the rest of
        // this comparison rests on: upstream compiled this pattern and then ran out of time matching
        // it, so a port that failed before it started matching has not done the same thing. Not a
        // shape the engine can currently produce - the constructor is handed InfiniteMatchTimeout -
        // which is exactly why it is pinned rather than left to a future edit to notice.
        OracleComparer.Compare(budgeted, ranOut with { WhileMatching = false }).Should().Be(OracleVerdict.Diverge);

        // And answering is a DIVERGENCE, which is the arm that makes this row worth recording at all.
        // Upstream could not finish this shape in twenty times the budget; a port that finishes it has
        // either stopped being the same engine on it - so the row no longer tests the deadline, which
        // is the "a generator that never reaches the path it was written for" failure this slice is
        // hunting - or answered something upstream never got to check. Phase 7 optimisation reddening
        // this row is the correct outcome and the signal to redraw the family, not a reason to soften it.
        OracleComparer.Compare(budgeted, new NoMatchOutcome()).Should().Be(OracleVerdict.Diverge);

        // The port IS asked, unlike every other timeout row, and it is asked with the ROW'S budget
        // rather than `RowTimeout`. The wall clock is the only thing that can tell those apart, and
        // this pattern does not stop on its own: at `RowTimeout` the call takes ten seconds, so
        // returning well inside that is the proof the row's own field reached the engine.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        OracleRunSummary run = OracleComparer.RunWave([budgeted], OracleComparer.Run);
        watch.Stop();

        watch
            .Elapsed.Should()
            .BeLessThan(
                OracleComparer.RowTimeout / 2,
                "the row's own deadline reached the engine, not the comparer's blanket one"
            );
        run.Tally.Should().Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Agree] = 1 });
        run.Divergences.Should().BeEmpty();
    }

    [Test]
    public void A_row_upstream_ran_out_of_memory_on_is_skipped_counted_and_never_put_to_this_port()
    {
        // S43's half of the same problem, reached by running out of heap rather than out of time.
        // A repeat whose body can match empty, beside a fuzzy section, gives upstream nothing to
        // make progress on and it allocates until MemoryError in a second or two. Measured
        // 2026-09-13 on regex 2026.7.19, over 'bb.a\r.':
        //   (?b)(?P<g1>\p{L}*)+?(?:ab){e<=1}  MemoryError 1.78s
        //   (?e)(?P<g1>\p{L}*)+?(?:ab){e<=1}  MemoryError 1.76s
        //       (?P<g1>\p{L}*)+?(?:ab){e<=1}  MemoryError 1.75s   <- no ranking flag at all
        //       (?P<g1>\p{L}+)+?(?:ab){e<=1}  (0, 2)      0.00s   <- the body must consume
        // The row below keeps the `(?b)` spelling because it is the one the wave drew, not because
        // the flag causes it. Until S43 the recorder aborted the whole run on one of these, so a
        // single unanswerable row threw away every other row of a six-seed wave.
        OracleRow blown = OracleWave.ParseRows(
            """
            {"generator": "interactions", "pattern": "(?b)(?P<g1>\\p{L}*)+?(?:ab){e<=1}", "flags": 0, "namedLists": {}, "subject": "bb.a\r.", "operation": "search", "codepointSpan": null, "outcome": {"kind": "resource", "exception": "MemoryError"}}
            """
        )[0];

        blown.Expected.Should().BeOfType<ResourceOutcome>().Which.Exception.Should().Be("MemoryError");

        // Both arms, exactly as for a timeout: upstream did not reject the pattern, so filing this
        // as a divergence would fail the run on upstream's resource bug, and filing it as agreement
        // would let a port that also blows up score as parity.
        OracleComparer.Compare(blown, new NoMatchOutcome()).Should().Be(OracleVerdict.Resource);
        OracleComparer.Compare(blown, actual: null).Should().Be(OracleVerdict.Resource);

        var asked = new List<int>();
        OracleRunSummary run = OracleComparer.RunWave(
            [blown],
            row =>
            {
                asked.Add(row.Number);
                return new NoMatchOutcome();
            }
        );

        asked.Should().BeEmpty("a row with no ground truth is asked of nothing");
        run.Divergences.Should().BeEmpty();

        // The whole tally, so a future edit cannot fold it back into `unsupported` and make an
        // upstream blowup read as a gap in this port's coverage.
        run.Tally.Should().Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Resource] = 1 });
    }

    [Test]
    public void A_wrong_lastindex_alone_is_reported_as_a_divergence()
    {
        // Added S18 with 'lastindex'/'lastgroup'. They are compared because nothing about the group
        // spans distinguishes them - '((a))' against 'a' has lastindex 1 with groups 1 and 2 sharing
        // one span - so an engine that got only these two wrong would agree on every span in every
        // wave. The row's groups are left untouched here, which is what makes this a statement about
        // the two new fields rather than about the spans.
        OracleRow row = OracleWave.ParseRows(_recordedRows)[0];
        var expected = (MatchOutcome)row.Expected;

        expected.LastIndex.Should().Be(2, "group 2 of '(a)(b)' closes last");
        expected.LastGroup.Should().BeNull("neither group is named");

        OracleComparer.Compare(row, expected with { LastIndex = 1 }).Should().Be(OracleVerdict.Diverge);
        OracleComparer.Compare(row, expected with { LastGroup = "b" }).Should().Be(OracleVerdict.Diverge);
        OracleComparer.Compare(row, expected).Should().Be(OracleVerdict.Agree);
    }

    [Test]
    public void The_recorder_translates_codepoint_indices_to_utf16()
    {
        // The proof that the translation layer is live rather than a no-op. U+1F600 is one Python
        // codepoint and two UTF-16 code units, so upstream's own span for 'b' in "<grin>ab" is
        // (2, 3) by codepoint and the value our public API must return is (Index 3, Length 1).
        // Identical numbers here would mean the recorder was passing Python's indices straight
        // through, and every astral row in every wave would be quietly wrong.
        OracleRow row = OracleWave.ParseRows(_recordedRows)[1];

        row.Subject.Should().HaveLength(4, "the astral character occupies two UTF-16 code units");
        row.CodepointSpan.Should().Be((2, 3));

        OracleGroup whole = row.Expected.Should().BeOfType<MatchOutcome>().Which.Groups[0];
        whole.Index.Should().Be(3);
        whole.Length.Should().Be(1);
    }

    [Test]
    public void A_crash_in_this_port_is_not_reported_as_agreement()
    {
        // Upstream rejected the pattern; the three ways this port can respond are a matching
        // rejection, a mismatched rejection, and a crash. Only the first is agreement - without
        // the allow-list in OracleComparer, an IndexOutOfRangeException out of our own engine
        // would be filed as parity with upstream's parse error.
        OracleRow rejected = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "error", "exception": "error", "message": "missing )"}}
            """
        )[0];

        OracleComparer
            .Compare(rejected, new ErrorOutcome(nameof(FuzzyRegexParseException), "missing )"))
            .Should()
            .Be(OracleVerdict.Agree);
        OracleComparer
            .Compare(rejected, new ErrorOutcome(nameof(FuzzyRegexParseException), "missing ) or something"))
            .Should()
            .Be(OracleVerdict.Diverge, "upstream's own suite asserts on the message text");
        OracleComparer
            .Compare(rejected, new ErrorOutcome(nameof(IndexOutOfRangeException), "Index was outside the bounds"))
            .Should()
            .Be(OracleVerdict.Diverge, "a crash is not a rejection");
        OracleComparer.Compare(rejected, new NoMatchOutcome()).Should().Be(OracleVerdict.Diverge);
        OracleComparer.Compare(rejected, actual: null).Should().Be(OracleVerdict.Unsupported);

        // Now the rejections upstream raises as a plain Python class rather than as regex.error,
        // where neither the class name nor the message can be compared: `regex.compile('a',
        // V0|V1)` raises KeyError('regex.V0|V1') and this port's answer is an
        // ArgumentOutOfRangeException whose Message .NET decorates with the parameter name and the
        // actual value (RegexFlags.cs:168-174, both measured 2026-08-31). What is left to compare
        // is the *phase*: upstream rejected the input before matching anything, so a rejection
        // agrees and a crash out of the matcher does not.
        OracleRow versionConflict = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "a", "flags": 8448, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "error", "exception": "KeyError", "message": "regex.V0|V1"}}
            """
        )[0];

        ErrorOutcome rejectedTheFlags = VersionRejection(8448);
        rejectedTheFlags
            .Message.Should()
            .NotBe("regex.V0|V1", "the point of the phase rule is that .NET's message cannot equal upstream's");

        OracleComparer
            .Compare(versionConflict, rejectedTheFlags)
            .Should()
            .Be(
                OracleVerdict.Agree,
                "which .NET exception a Python KeyError becomes is the porting slice's call, and its message cannot port"
            );
        OracleComparer
            .Compare(versionConflict, MatcherCrash(3))
            .Should()
            .Be(
                OracleVerdict.Diverge,
                "upstream refused to compile the pattern, so anything thrown from the matcher is a crash"
            );
        OracleComparer
            .Compare(
                versionConflict,
                new ErrorOutcome(nameof(IndexOutOfRangeException), "Index was outside the bounds of the array.")
            )
            .Should()
            .Be(
                OracleVerdict.Diverge,
                "the allow-list is what keeps a crash during compilation from being filed as a rejection"
            );
    }

    [Test]
    public void A_pattern_upstreams_compiler_rejects_is_rejected_here_too()
    {
        // `{e<=1:\b}` is one of the five patterns upstream's parser compiled and its C compiler
        // then refused with `RuntimeError: invalid RE code` (measured 2026-08-31). Until S15 this
        // port compiled all five, and this test held that gap open as a divergence; S15 ported
        // re_compile, so the same row is now an agreement. **The oracle is what noticed the change
        // of behaviour** - this test went red the moment the rejection landed.
        OracleRow rejectedByUpstream = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?:abc){e<=1:\\b}", "flags": 0, "namedLists": {}, "subject": "abc", "operation": "search", "codepointSpan": null, "outcome": {"kind": "error", "exception": "RuntimeError", "message": "invalid RE code"}}
            """
        )[0];

        // Run, not a stand-in: the constructor really throws here, out of the node compiler.
        IOracleOutcome? answer = OracleComparer.Run(rejectedByUpstream);
        answer
            .Should()
            .BeOfType<ErrorOutcome>("this port now refuses the pattern upstream's compiler refuses")
            .Which.Exception.Should()
            .Be(nameof(NotSupportedException));
        OracleComparer.Compare(rejectedByUpstream, answer).Should().Be(OracleVerdict.Agree);

        // The rule that made this row a divergence is still load-bearing - it is what stops any
        // *other* missing rejection hiding behind the unported matcher for the rest of phase 3 -
        // so it stays pinned, now with a stand-in because the engine no longer produces one here.
        OracleComparer.Compare(rejectedByUpstream, new CompiledButUnmatched()).Should().Be(OracleVerdict.Diverge);

        // Where upstream did *not* reject the input, an unported matcher really does mean the
        // answer is unknown, and must not be reported as a divergence. Pinned with a stand-in from
        // S18 on, because the fixture row is '(a)(b)' and the engine now really matches it - the
        // rule itself stays load-bearing, since it is what keeps the seam of a construct no slice
        // has reached yet from being filed as a wrong answer.
        OracleRow matched = OracleWave.ParseRows(_recordedRows)[0];
        OracleComparer.Compare(matched, new CompiledButUnmatched()).Should().Be(OracleVerdict.Unsupported);

        // And the engine's real answer to that row agrees with upstream, which is what makes the
        // line above a statement about Compare rather than about a still-missing capability.
        IOracleOutcome? real = OracleComparer.Run(matched);
        real.Should().BeOfType<MatchOutcome>();
        OracleComparer.Compare(matched, real).Should().Be(OracleVerdict.Agree);
    }

    [Test]
    public void A_substitution_row_compares_its_text_its_count_and_the_phase_a_rejection_came_from()
    {
        // S24's row shape, and the rule change it forced. Upstream rejects an out-of-range group
        // reference in a template *while it substitutes* - regex.sub('x', r'\1', 'x') raises
        // `error: invalid group reference` after regex.compile has already succeeded (measured
        // 2026-09-01) - so the recorder writes whileMatching, and Compare requires the two sides to
        // have thrown at the same point rather than assuming upstream threw at compile time.
        // Before this, every such row was a false divergence.
        OracleRow rejectedWhileSubstituting = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "x", "flags": 0, "namedLists": {}, "subject": "x", "operation": "sub", "template": "\\1", "count": 0, "codepointSpan": null, "outcome": {"kind": "error", "exception": "error", "message": "invalid group reference", "whileMatching": true}}
            """
        )[0];

        rejectedWhileSubstituting.Template.Should().Be(@"\1");

        // Run, not a stand-in: Replace really throws here, out of the template expansion, and this
        // is the first row that reaches OracleComparer.Run's while-matching catch at all.
        IOracleOutcome? answer = OracleComparer.Run(rejectedWhileSubstituting);
        answer
            .Should()
            .BeOfType<ErrorOutcome>()
            .Which.WhileMatching.Should()
            .BeTrue("the pattern compiled and the template was rejected during the substitution");
        OracleComparer.Compare(rejectedWhileSubstituting, answer).Should().Be(OracleVerdict.Agree);

        // The phase rule still bites in both directions: a rejection at the wrong point is not the
        // same answer, whatever its type and message.
        OracleComparer
            .Compare(
                rejectedWhileSubstituting,
                new ErrorOutcome(nameof(FuzzyRegexParseException), "invalid group reference")
            )
            .Should()
            .Be(
                OracleVerdict.Diverge,
                "upstream got as far as substituting; refusing to compile is a different answer"
            );

        // And a successful substitution compares on both halves of upstream's subn pair.
        OracleRow replaced = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "aba", "operation": "sub", "template": "z", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "zbz", "count": 2}}
            """
        )[0];

        OracleComparer.Compare(replaced, OracleComparer.Run(replaced)).Should().Be(OracleVerdict.Agree);
        OracleComparer.Compare(replaced, new SubOutcome("zbz", 1)).Should().Be(OracleVerdict.Diverge);
        OracleComparer.Compare(replaced, new SubOutcome("zba", 2)).Should().Be(OracleVerdict.Diverge);
    }

    [Test]
    public void Both_ends_of_the_count_convention_are_translated_not_just_the_no_limit_end()
    {
        // upstream 0 is "no limit" and upstream negative is "no replacements at all"; this surface
        // spells those -1 and 0. Translating only the first asked the two engines opposite
        // questions and reported a correct port as RED. Found by S24's blind review with exactly
        // this row: regex.subn('a', 'z', 'aaa', count=-1) is ('aaa', 0).
        OracleRow noReplacements = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "aaa", "operation": "sub", "template": "z", "count": -1, "codepointSpan": null, "outcome": {"kind": "sub", "text": "aaa", "count": 0}}
            """
        )[0];

        OracleComparer.Compare(noReplacements, OracleComparer.Run(noReplacements)).Should().Be(OracleVerdict.Agree);

        // And the other end, so this is a statement about the mapping rather than about one value:
        // regex.subn('a', 'z', 'aaa', count=0) is ('zzz', 3).
        OracleRow noLimit = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "aaa", "operation": "sub", "template": "z", "count": 0, "codepointSpan": null, "outcome": {"kind": "sub", "text": "zzz", "count": 3}}
            """
        )[0];

        OracleComparer.Compare(noLimit, OracleComparer.Run(noLimit)).Should().Be(OracleVerdict.Agree);
    }

    [Test]
    public void A_posix_fuzzy_row_compares_its_error_counts_where_upstream_cannot_give_the_positions()
    {
        // Ledger entry 9. Reading `Match.fuzzy_changes` on a POSIX fuzzy match that spent an error
        // kills the CPython process outright - 0xC0000005, not an exception - so the recorder cannot
        // ask upstream where the errors were. `fuzzy_counts` on the same match is safe
        // (tools/probes/upstream-posix-fuzzy-safe-attributes.py, regex 2026.9.10, 2026-09-14: every
        // read `_describe_match` makes is safe except that one), so the recorder writes `fuzzyCounts`
        // and omits `fuzzyChanges`, and both sides render the fuzzy half without positions.
        //
        // Recorder output, `(?p)(?:abc){e<=1}` over 'axc': upstream matches (0, 3) having spent one
        // substitution. This port answers the same span and the same counts, and knows the
        // substitution was at index 1 - a fact upstream has no answer to compare against.
        OracleRow posix = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?p)(?:abc){e<=1}", "flags": 0, "namedLists": {}, "subject": "axc", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0]}}
            """
        )[0];

        OracleComparer.Compare(posix, OracleComparer.Run(posix)).Should().Be(OracleVerdict.Agree);

        // And the suppression reaches the POSITIONS only. A row recorded with different counts must
        // still diverge, or lifting the generator's exclusion would buy a cell of the matrix that
        // agrees with anything. Same row, upstream's substitution rewritten as a deletion.
        OracleRow wrongCounts = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?p)(?:abc){e<=1}", "flags": 0, "namedLists": {}, "subject": "axc", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [0, 0, 1]}}
            """
        )[0];

        OracleComparer.Compare(wrongCounts, OracleComparer.Run(wrongCounts)).Should().Be(OracleVerdict.Diverge);

        // A non-POSIX row is untouched by any of it: the positions are recorded, compared, and a
        // wrong one is a divergence. Without this half the test above would pass on a comparator
        // that had simply stopped comparing change positions everywhere.
        OracleRow ordinary = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?:abc){e<=1}", "flags": 0, "namedLists": {}, "subject": "axc", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [1], "insertions": [], "deletions": []}}}
            """
        )[0];

        OracleComparer.Compare(ordinary, OracleComparer.Run(ordinary)).Should().Be(OracleVerdict.Agree);

        OracleRow wrongPosition = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?:abc){e<=1}", "flags": 0, "namedLists": {}, "subject": "axc", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false, "fuzzyCounts": [1, 0, 0], "fuzzyChanges": {"substitutions": [2], "insertions": [], "deletions": []}}}
            """
        )[0];

        OracleComparer.Compare(wrongPosition, OracleComparer.Run(wrongPosition)).Should().Be(OracleVerdict.Diverge);
    }

    [Test]
    public void A_posix_fuzzy_row_that_spent_no_errors_still_renders_no_fuzzy_half_at_all()
    {
        // The suppression keys off POSIX, not off "this match spent errors", because this port has
        // no way to know in advance which rows upstream would have died on - the faulting condition
        // is a spent error, and leftmost-longest can make an apparently exact row spend one
        // (`(?p)(?:abc){e<=1}` over 'abcd'). An exact match renders no fuzzy half either way, so the
        // suppression must not turn one into `fuzzy=(0,0,0)` and red every such row.
        OracleRow exact = OracleWave.ParseRows(
            """
            {"generator": "rows", "pattern": "(?p)(?:abc){e<=1}", "flags": 0, "namedLists": {}, "subject": "abc", "operation": "search", "codepointSpan": [0, 3], "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 3, "captures": [[0, 3]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}
            """
        )[0];

        OracleComparer.Compare(exact, OracleComparer.Run(exact)).Should().Be(OracleVerdict.Agree);
    }

    /// <summary>
    /// This port's real answer to <c>regex.compile('a', V0|V1)</c>, thrown rather than hand-written
    /// so the assertion above is made against the message .NET actually decorates, not against an
    /// imitation of it. Wrapped in a method taking <paramref name="version"/> because that is what
    /// makes the <c>nameof</c> below name a real parameter, which is what S3928 and MA0015 ask for.
    /// </summary>
    /// <param name="version">The conflicting flag bits.</param>
    /// <returns>The outcome, as a rejection at compile time.</returns>
    private static ErrorOutcome VersionRejection(int version) =>
        ErrorOutcome.From(new ArgumentOutOfRangeException(nameof(version), version, "not a single version flag"));

    /// <summary>An index slip in the engine: the same exception type, but thrown while matching.</summary>
    /// <param name="index">The offending index.</param>
    /// <returns>The outcome, marked as thrown from the matching call.</returns>
    private static ErrorOutcome MatcherCrash(int index) =>
        ErrorOutcome.From(
            new ArgumentOutOfRangeException(nameof(index), index, "past the end of the subject"),
            whileMatching: true
        );

    private static MatchOutcome ShiftFirstGroup(IOracleOutcome outcome)
    {
        var match = (MatchOutcome)outcome;
        OracleGroup first = match.Groups[0];
        return match with { Groups = [first with { Index = first.Index + 1 }, .. match.Groups.Skip(1)] };
    }

    private static MatchOutcome WithoutLastGroup(IOracleOutcome outcome)
    {
        var match = (MatchOutcome)outcome;
        return match with { Groups = [.. match.Groups.Take(match.Groups.Count - 1)] };
    }
}
