using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

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

    [Test]
    public void The_wave_agrees_with_upstream()
    {
        OracleWaveFile wave = OracleWave.Load();

        // A pattern that names no version resolves against upstream's DEFAULT_VERSION, so a wave
        // recorded under a different default is asking a different question from the one this
        // port answers.
        wave.Header.DefaultVersion.Should()
            .Be(
                PatternCompiler.DefaultVersion,
                "the wave must be recorded under the default version this port compiles with"
            );
        wave.Rows.Should().NotBeEmpty("an empty wave would agree with anything");

        OracleRunSummary run = OracleComparer.RunWave(wave.Rows, OracleComparer.Run);
        string summary = OracleWave.WriteReport(wave.Header, wave.Rows.Count, run.Tally, run.Divergences);

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
