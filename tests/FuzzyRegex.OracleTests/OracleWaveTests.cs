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
