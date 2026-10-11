using System.Globalization;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// The rows of <c>JudgedDivergences.jsonl</c> and the <c>judged-row</c> entry that accounts for them.
/// </summary>
/// <remarks>
/// The entry matches on text: the question, and both answers' <see cref="IOracleOutcome.Describe"/>.
/// So each control below changes exactly one of the three and requires the entry to let go, and the
/// first test requires every row's live answer to be the recorded one, which is what keeps a judged
/// row from hiding a later change in this port.
/// </remarks>
public sealed class JudgedDivergenceTests
{
    private const string _id = "judged-row";

    /// <summary>An answer that is nothing but its description, for the controls.</summary>
    /// <param name="Text">What <see cref="Describe"/> returns.</param>
    private sealed record Described(string Text) : IOracleOutcome
    {
        public string Describe() => Text;
    }

    private static ExpectedDivergence Entry =>
        ExpectedDivergences
            .All.Should()
            .ContainSingle(static e => string.Equals(e.Id, _id, StringComparison.Ordinal))
            .Subject;

    [Test]
    public void Every_judged_row_is_accounted_for_by_the_judged_row_entry()
    {
        JudgedDivergences.All.Should().NotBeEmpty();

        foreach (JudgedRow judged in JudgedDivergences.All)
        {
            IOracleOutcome ours = OracleComparer
                .Run(judged.Row, TimeSpan.FromSeconds(10))
                .Should()
                .NotBeNull("{0}: a judged row must be one this port answers", judged.Source)
                .And.Subject.Should()
                .BeAssignableTo<IOracleOutcome>()
                .Subject;

            ours.Describe()
                .Should()
                .Be(judged.Port, "{0}: this port's answer is the one that was judged", judged.Source);
            OracleComparer
                .Compare(judged.Row, ours)
                .Should()
                .Be(judged.Crash is null ? OracleVerdict.Diverge : OracleVerdict.Fault, "{0}", judged.Source);
            // Through the wave loop too, which is where a crash row is claimed.
            OracleComparer
                .RunWave([judged.Row], _ => ours)
                .Tally.Should()
                .Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Expected] = 1 }, judged.Source);
            // The Id, so a row an earlier key now claims names that key: the judged line is then
            // redundant and should be deleted.
            (ExpectedDivergences.For(judged.Row, ours)?.Id)
                .Should()
                .Be(_id, "{0}: the judged-row entry accounts for every judged row", judged.Source);
        }
    }

    [Test]
    public void The_judged_rows_file_is_well_formed()
    {
        string ledger = File.ReadAllText(
            Path.Combine(OracleWave.RepoRoot, "docs", "plan", "upstream-reports", "LEDGER.md")
        );
        string defects = File.ReadAllText(Path.Combine(OracleWave.RepoRoot, "docs", "KNOWN-DEFECTS.md"));
        IReadOnlySet<string> pending = JudgedDivergences.PendingCandidates();

        JudgedDivergences.All.Should().NotBeEmpty();
        JudgedDivergences
            .All.Select(static judged => JudgedDivergences.QuestionOf(judged.Row))
            .Should()
            .OnlyHaveUniqueItems("a question is judged once");

        foreach (JudgedRow judged in JudgedDivergences.All)
        {
            string where = $"line {judged.Line} ({judged.Source})";
            judged.Witness.Should().NotBeNullOrWhiteSpace(where);
            judged.Source.Should().MatchRegex(@"^\d+/\d+$", where);
            DateOnly
                .TryParseExact(judged.Judged, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                .Should()
                .BeTrue("{0}: judged is a date", where);
            judged
                .Upstream.Should()
                .Be(
                    judged.Row.Expected is CrashedOutcome
                        ? JudgedDivergences.CrashedMarker
                        : judged.Row.Expected.Describe(),
                    "{0}: upstream is the recorded row's answer",
                    where
                );
            judged.Port.Should().NotBe(judged.Upstream, "{0}: a judged row diverges", where);
            judged.Cause.Should().NotBeNullOrWhiteSpace(where);

            foreach (string part in judged.Cause.Split(" + "))
            {
                bool exists = CauseExists(part, ledger, defects, pending);
                exists
                    .Should()
                    .BeTrue(
                        "{0}: cause '{1}' must be a LEDGER.md entry, a KNOWN-DEFECTS.md D-number or a pending "
                            + "candidate in the file's header",
                        where,
                        part
                    );
            }
        }
    }

    [Test]
    public void A_judged_row_whose_port_answer_changes_is_not_accounted_for()
    {
        ExpectedDivergence entry = Entry;

        foreach (JudgedRow judged in JudgedDivergences.All)
        {
            entry.Applies(judged.Row, new Described(judged.Port)).Should().BeTrue(judged.Source);
            entry
                .Applies(judged.Row, new Described(judged.Port + " "))
                .Should()
                .BeFalse("{0}: any other port answer is reported", judged.Source);
            entry
                .Applies(judged.Row, judged.Row.Expected)
                .Should()
                .BeFalse("{0}: a port that gives upstream's answer has changed", judged.Source);
            if (!string.Equals(judged.Port, new NoMatchOutcome().Describe(), StringComparison.Ordinal))
            {
                entry.Applies(judged.Row, new NoMatchOutcome()).Should().BeFalse(judged.Source);
            }
        }
    }

    [Test]
    public void A_judged_row_whose_upstream_answer_changes_is_not_accounted_for()
    {
        ExpectedDivergence entry = Entry;

        foreach (JudgedRow judged in JudgedDivergences.All)
        {
            var ours = new Described(judged.Port);
            IOracleOutcome[] others =
            [
                new NoMatchOutcome(),
                new MatchOutcome([new OracleGroup(0, Success: true, 1, 1, [new OracleSpan(1, 1)])], -1, null),
            ];
            foreach (
                IOracleOutcome other in others.Where(o =>
                    !string.Equals(o.Describe(), judged.Upstream, StringComparison.Ordinal)
                )
            )
            {
                entry
                    .Applies(judged.Row with { Expected = other }, ours)
                    .Should()
                    .BeFalse("{0}: upstream answering {1} is a different row", judged.Source, other.Describe());
            }
        }
    }

    [Test]
    public void A_question_that_differs_in_one_field_is_not_accounted_for()
    {
        ExpectedDivergence entry = Entry;

        foreach (JudgedRow judged in JudgedDivergences.All)
        {
            OracleRow row = judged.Row;
            var ours = new Described(judged.Port);
            (string Field, OracleRow Changed)[] variants =
            [
                ("pattern", row with { Pattern = row.Pattern + "x" }),
                ("flags", row with { Flags = row.Flags ^ 0x100 }),
                ("subject", row with { Subject = row.Subject + "x" }),
                (
                    "operation",
                    row with
                    {
                        Operation = string.Equals(row.Operation, "search", StringComparison.Ordinal)
                            ? "match"
                            : "search",
                    }
                ),
                ("partial", row with { Partial = !row.Partial }),
                ("pos", row with { Pos = (row.Pos ?? 0) + 1 }),
                ("endpos", row with { EndPos = (row.EndPos ?? row.Subject.Length) + 1 }),
                ("template", row with { Template = row.Template + "x" }),
                ("count", row with { Count = row.Count + 1 }),
                (
                    "named lists",
                    row with
                    {
                        NamedLists = new Dictionary<string, IReadOnlyList<string>>(row.NamedLists) { ["zz"] = ["z"] },
                    }
                ),
                ("default version", row with { DefaultVersion = (int)FuzzyRegexOptions.Version1 }),
            ];

            foreach ((string field, OracleRow changed) in variants)
            {
                entry
                    .Applies(changed, ours)
                    .Should()
                    .BeFalse("{0}: a question with another {1} is not the judged one", judged.Source, field);
            }
        }
    }

    [Test]
    public void A_judged_crash_row_whose_port_answer_changes_fails_the_run_and_shows_the_answer()
    {
        // The owner's rule (2026-10-10): this port never reproduces an upstream crash, so a crash row
        // pins this port's exact answer. Any other answer - a different result, or an error raised while
        // matching - leaves the row a FAULT, which fails the run, with the answer printed for judging.
        JudgedRow[] crashes = [.. JudgedDivergences.All.Where(static judged => judged.Crash is not null)];
        crashes.Should().NotBeEmpty("the file holds a judged crash row");

        foreach (JudgedRow judged in crashes)
        {
            IOracleOutcome[] others =
            [
                new Described(judged.Port + " "),
                new NoMatchOutcome(),
                new ErrorOutcome("IndexOutOfRangeException", "boom", WhileMatching: true),
            ];
            foreach (IOracleOutcome other in others)
            {
                OracleRunSummary run = OracleComparer.RunWave([judged.Row], _ => other);
                run.Tally.Should()
                    .Equal(new Dictionary<OracleVerdict, int> { [OracleVerdict.Fault] = 1 }, judged.Source);
                run.Faults.Should()
                    .ContainSingle()
                    .Which.Should()
                    .Contain("  port     " + other.Describe() + Environment.NewLine);
            }
        }
    }

    [Test]
    [Arguments(
        """{"cause": "D1", "upstream": "<upstream crashed>", "port": "no match", "witness": "w", "judged": "2026-10-10", "source": "1/1", "crash": "exit -11", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "match", "groups": [{"number": 0, "success": true, "index": 0, "length": 1, "captures": [[0, 1]]}], "lastIndex": -1, "lastGroup": null, "partial": false}}}""",
        "the recorded row is not a crash"
    )]
    [Arguments(
        """{"cause": "D1", "upstream": "<upstream crashed>", "port": "error while matching IndexOutOfRangeException: boom", "witness": "w", "judged": "2026-10-10", "source": "1/1", "crash": "exit -11", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "crashed", "exitCode": -11, "reproducedAlone": true}}}""",
        "never an error raised while matching"
    )]
    [Arguments(
        """{"cause": "D1", "upstream": "<upstream crashed>", "port": "no match", "witness": "w", "judged": "2026-10-10", "source": "1/1", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "crashed", "exitCode": -11, "reproducedAlone": true}}}""",
        "needs its crash evidence"
    )]
    [Arguments(
        """{"cause": "D1", "upstream": "<upstream crashed>", "port": "no match", "witness": "w", "judged": "2026-10-10", "source": "1/1", "crash": "exit -6", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "crashed", "exitCode": -11, "reproducedAlone": true}}}""",
        "does not name the recorded exit code -11"
    )]
    [Arguments(
        """{"cause": "D1", "upstream": "upstream crashed (exit -11), again when run alone", "port": "no match", "witness": "w", "judged": "2026-10-10", "source": "1/1", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "crashed", "exitCode": -11, "reproducedAlone": true}}}""",
        "so upstream must be <upstream crashed>"
    )]
    [Arguments(
        """{"cause": "D1", "upstream": "no match", "port": "match 0:(0,1)[(0,1)] last=-1/-", "witness": "w", "judged": "2026-10-10", "source": "1/1", "crash": "exit -11", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "b", "operation": "search", "codepointSpan": null, "outcome": {"kind": "matches", "matches": []}}}""",
        "crash evidence on a line whose upstream is not"
    )]
    public void The_loader_refuses_a_crash_line_that_breaks_the_rules(string line, string problem)
    {
        Action load = () => JudgedDivergences.Parse(line);

        load.Should().Throw<InvalidDataException>().WithMessage("*" + problem + "*");
    }

    [Test]
    public void The_loader_accepts_a_well_formed_crash_line_with_a_compile_time_refusal()
    {
        // The owner's second option: refusing the pattern when compiling it is allowed.
        const string line =
            """{"cause": "D1", "upstream": "<upstream crashed>", "port": "error FuzzyRegexParseException: no", "witness": "w", "judged": "2026-10-10", "source": "1/1", "crash": "exit -11, reproduced alone", "row": {"generator": "verbs", "pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search", "codepointSpan": null, "outcome": {"kind": "crashed", "exitCode": -11, "reproducedAlone": true}}}""";

        JudgedDivergences.Parse(line).Should().ContainSingle().Which.Crash.Should().Be("exit -11, reproduced alone");
    }

    /// <summary>Whether one part of a cause names something the repository records.</summary>
    /// <param name="part">A <c>ledger N</c>, a <c>DNN</c> or a pending candidate's name.</param>
    /// <param name="ledger">LEDGER.md's text.</param>
    /// <param name="defects">KNOWN-DEFECTS.md's text.</param>
    /// <param name="pending">The pending candidates the data file's header names.</param>
    /// <returns><see langword="true"/> if it does.</returns>
    private static bool CauseExists(string part, string ledger, string defects, IReadOnlySet<string> pending)
    {
        const string prefix = "ledger ";
        if (part.StartsWith(prefix, StringComparison.Ordinal))
        {
            string number = part[prefix.Length..];
            return number.Length > 0
                && number.All(char.IsAsciiDigit)
                && ledger.Contains("\n## " + number + ". ", StringComparison.Ordinal);
        }

        if (part.Length > 1 && part[0] == 'D' && part[1..].All(char.IsAsciiDigit))
        {
            // As a whole word, so D5 is not found inside D51.
            for (
                int at = defects.IndexOf(part, StringComparison.Ordinal);
                at >= 0;
                at = defects.IndexOf(part, at + 1, StringComparison.Ordinal)
            )
            {
                int end = at + part.Length;
                if (
                    (at == 0 || !char.IsAsciiLetterOrDigit(defects[at - 1]))
                    && (end == defects.Length || !char.IsAsciiLetterOrDigit(defects[end]))
                )
                {
                    return true;
                }
            }

            return false;
        }

        return pending.Contains(part);
    }
}
