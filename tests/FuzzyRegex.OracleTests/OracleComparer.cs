namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>Runs one recorded row through this port and diffs the answer against upstream's.</summary>
/// <remarks>
/// Split from the test that drives it so the discriminator itself can be tested: with no engine
/// yet, every real row reports <see cref="OracleVerdict.Unsupported"/>, and a harness that has
/// never been shown to notice a wrong answer is not evidence of anything. See
/// <c>OracleWaveTests.Corrupting_a_recorded_row_is_reported_as_a_divergence</c>.
/// </remarks>
internal static class OracleComparer
{
    /// <summary>How long one row may take before its answer counts as a wrong one.</summary>
    /// <remarks>
    /// Bounded rather than <see cref="FuzzyRegex.InfiniteMatchTimeout"/>, added in S26. A row this
    /// port cannot answer is a divergence worth reading; a row it never *stops* answering used to
    /// be a hung run, which looks exactly like a slow build and tells nobody anything. Measured
    /// while re-running S17's negative control on 2026-09-01: the mutant took over six minutes on
    /// a 1,200-row wave the honest engine answers in one second, and the harness reported nothing
    /// at all. Ten seconds is generous - every generator's subjects are eight characters or fewer,
    /// and the recorder would itself have stalled on a row upstream could not answer quickly.
    /// </remarks>
    internal static readonly TimeSpan RowTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The .NET exceptions that count as "this port rejected the input", when upstream raised
    /// something whose class name has no counterpart here.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than "any exception", and that is the whole point of it: upstream's
    /// non-<c>error</c> rejections are Python classes - a <c>ValueError</c> over conflicting
    /// flags, a <c>RuntimeError</c> out of <c>_regex.compile</c> - so their names cannot be
    /// compared, and treating any thrown exception as agreement would let an
    /// <c>IndexOutOfRangeException</c> in our own engine be recorded as parity.
    /// </remarks>
    private static readonly string[] _rejections =
    [
        nameof(FuzzyRegexParseException),
        nameof(ArgumentException),
        nameof(ArgumentNullException),
        nameof(ArgumentOutOfRangeException),
        nameof(NotSupportedException),
        // Added in S24 for the format-template language: a malformed `{...}` field is upstream's
        // ValueError out of str.format, and FormatException is what .NET calls that.
        nameof(FormatException),
    ];

    /// <summary>Puts every row's question to an engine and tallies the verdicts.</summary>
    /// <param name="rows">The wave's rows.</param>
    /// <param name="engine">
    /// What answers a row. <see cref="Run(OracleRow)"/> in the wave run; a deliberately wrong stand-in in
    /// <c>OracleWaveTests.Corrupting_a_recorded_row_is_reported_as_a_divergence</c>, which is the
    /// only way this loop can be shown to notice a wrong answer while the real engine still
    /// answers <see cref="OracleVerdict.Unsupported"/> to everything.
    /// </param>
    /// <returns>The tally, one rendered block per diverging row, and one per expected divergence.</returns>
    public static OracleRunSummary RunWave(IEnumerable<OracleRow> rows, Func<OracleRow, IOracleOutcome?> engine)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(engine);

        var tally = new Dictionary<OracleVerdict, int>();
        var divergences = new List<string>();
        var expected = new List<string>();
        foreach (OracleRow row in rows)
        {
            IOracleOutcome? actual = engine(row);
            OracleVerdict verdict = Compare(row, actual);

            // A divergence only. An agreement is never reclassified: the entries exist to account
            // for rows the two engines answer differently, and one that stopped diverging must show
            // up as agreement so `Every_expected_divergence_still_diverges` can call the entry stale.
            ExpectedDivergence? accounted =
                verdict == OracleVerdict.Diverge && actual is not null ? ExpectedDivergences.For(row, actual) : null;
            if (accounted is not null)
            {
                verdict = OracleVerdict.Expected;
            }

            tally[verdict] = tally.GetValueOrDefault(verdict) + 1;
            if (verdict == OracleVerdict.Diverge)
            {
                divergences.Add(OracleWave.Describe(row, actual));
            }
            else if (accounted is not null)
            {
                expected.Add(OracleWave.Describe(row, actual, accounted));
            }
        }

        return new OracleRunSummary(tally, divergences, expected);
    }

    /// <summary>Puts a row's question to this port.</summary>
    /// <param name="row">The row to run.</param>
    /// <returns>
    /// This port's answer; <see langword="null"/> if it could not even compile the pattern because
    /// of an unported seam, or <see cref="CompiledButUnmatched"/> if it compiled and the seam is in
    /// the matcher. The two are not interchangeable: only the first knows nothing at all.
    /// </returns>
    public static IOracleOutcome? Run(OracleRow row) => Run(row, RowTimeout);

    /// <summary>Puts a row's question to this port, with an explicit deadline.</summary>
    /// <remarks>
    /// The deadline is a parameter only so a test can prove it reaches the engine without waiting
    /// <see cref="RowTimeout"/> to find out. Every real caller uses the overload above.
    /// </remarks>
    /// <param name="row">The row to run.</param>
    /// <param name="timeout">How long the matching call may take.</param>
    /// <returns>This port's answer, as the overload above describes it.</returns>
    internal static IOracleOutcome? Run(OracleRow row, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(row);

        // Compiling and matching are caught separately, because *which of the two* threw is the
        // difference between this port rejecting the input and this port crashing on it, and only
        // the first can agree with an upstream rejection. Compare has no other way to tell: the
        // types overlap, an ArgumentOutOfRangeException being both a deliberate rejection from
        // RegexFlags and the shape an index slip in the engine would take.
        FuzzyRegex compiled;
        try
        {
            compiled = new FuzzyRegex(
                row.Pattern,
                (FuzzyRegexOptions)row.Flags,
                timeout,
                row.NamedLists.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlyCollection<string>)entry.Value,
                    StringComparer.Ordinal
                )
            );
        }
        catch (NotImplementedException)
        {
            // The seam for a construct no slice has landed yet. Informational, not a divergence:
            // the parity ratchet is what watches for a capability going missing again.
            return null;
        }
        catch (Exception e)
        {
            // Any other exception is this port's *answer*, recorded as such and handed to Compare,
            // which agrees only if upstream also rejected the input and the exception is a
            // rejection rather than a crash. The whole exception is handed over, not just its
            // message: when the answer turns out to be a crash in our own engine, the stack trace
            // in the report is the difference between a finding and a fishing trip.
            return ErrorOutcome.From(e);
        }

        try
        {
            if (row.Operation is "sub" or "subf")
            {
                string replaced = string.Equals(row.Operation, "sub", StringComparison.Ordinal)
                    ? compiled.Replace(row.Subject, row.Template!, OurLimit(row.Count), out int replacements)
                    : compiled.ReplaceFormat(row.Subject, row.Template!, OurLimit(row.Count), out replacements);

                return new SubOutcome(replaced, replacements);
            }

            if (row.Operation is "finditer" or "finditer-overlapped")
            {
                bool overlapped = string.Equals(row.Operation, "finditer-overlapped", StringComparison.Ordinal);

                return new MatchesOutcome([
                    .. compiled.Matches(row.Subject, overlapped: overlapped).Select(DescribeGroups),
                ]);
            }

            if (string.Equals(row.Operation, "split", StringComparison.Ordinal))
            {
                return new SplitOutcome(compiled.Split(row.Subject, OurLimit(row.Count)));
            }

            // Upstream's (pos, endpos) as this surface's (beginning, length). Both absent means the
            // whole subject, which this surface spells as a length of -1.
            //
            // Clamped at zero, and that is not cosmetic: -1 is this surface's "no limit" sentinel,
            // so an endpos below pos - which the recorder records as it was asked, and which
            // upstream answers by matching nothing - would otherwise become a search over the whole
            // rest of the subject and be filed as a divergence in this port. Raised by the S31
            // second blind pass, with `{"pattern": "c", "subject": "abc", "pos": 2, "endpos": 1}`:
            // upstream answers None and this port answered (2, 3).
            //
            // Each end is read on its own, so a row carrying only one of the pair still narrows the
            // slice at that end. The same pass found `endpos` without `pos` being dropped entirely.
            int beginning = row.Pos ?? 0;
            int length =
                row.Pos is null && row.EndPos is null
                    ? -1
                    : Math.Max(0, (row.EndPos ?? row.Subject.Length) - beginning);

            Match match = row.Operation switch
            {
                "search" => compiled.Match(row.Subject, beginning, length, row.Partial),
                "match" => compiled.MatchAtStart(row.Subject, beginning, length, row.Partial),
                // The operation is validated when the row is read, so there is no other case.
                _ => compiled.FullMatch(row.Subject, beginning, length, row.Partial),
            };

            return Describe(match);
        }
        catch (NotImplementedException)
        {
            // Not `null`. The pattern compiled, so this port has already answered whether the
            // input is acceptable, and only the match itself is unknown - see CompiledButUnmatched.
            return new CompiledButUnmatched();
        }
        catch (Exception e)
        {
            return ErrorOutcome.From(e, whileMatching: true);
        }
    }

    /// <summary>Diffs one row's recorded answer against this port's.</summary>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="actual">This port's answer, or <see langword="null"/> for unsupported.</param>
    /// <returns>The verdict.</returns>
    public static OracleVerdict Compare(OracleRow row, IOracleOutcome? actual)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (actual is null)
        {
            return OracleVerdict.Unsupported;
        }

        if (actual is CompiledButUnmatched)
        {
            // Upstream rejected the input and this port compiled it: that is a divergence already,
            // and reporting it as unsupported would hide every missing rejection behind the
            // unported matcher for the whole of phase 3. Where upstream matched or did not match,
            // the answer genuinely is still unknown.
            return row.Expected is ErrorOutcome ? OracleVerdict.Diverge : OracleVerdict.Unsupported;
        }

        if (row.Expected is ErrorOutcome expected)
        {
            if (actual is not ErrorOutcome got || !_rejections.Contains(got.Exception, StringComparer.Ordinal))
            {
                return OracleVerdict.Diverge;
            }

            // The two sides must have thrown at the same point. An exception this port raised
            // *while matching*, where upstream rejected the pattern before it matched anything, is
            // not the same answer however plausible its type looks: it is a crash on a row this
            // port should have refused to compile. Without this the allow-list alone let an
            // ArgumentOutOfRangeException out of the engine be filed as parity with upstream's
            // ValueError about conflicting flags.
            //
            // Upstream's own flag was recorded, not assumed, from S24 on: a substitution rejects an
            // out-of-range group reference while it is substituting, so "upstream rejected before
            // matching" is no longer true of every recorded error (measured 2026-09-01).
            if (got.WhileMatching != expected.WhileMatching)
            {
                return OracleVerdict.Diverge;
            }

            // regex.error is the one upstream class with a ported counterpart, and upstream's own
            // suite asserts on its text, so both the type and the message are compared. For the
            // rest, all that ports is that the input was rejected: which .NET exception a Python
            // KeyError or ValueError becomes is the porting slice's call, and the message often
            // cannot port at all - .NET decorates ArgumentException's Message with the parameter
            // name and the actual value, so `regex.compile('a', V0|V1)`'s "regex.V0|V1" has no
            // equal here (RegexFlags.cs:168-174, measured 2026-08-31).
            if (!string.Equals(expected.Exception, "error", StringComparison.Ordinal))
            {
                return OracleVerdict.Agree;
            }

            return
                string.Equals(got.Exception, nameof(FuzzyRegexParseException), StringComparison.Ordinal)
                && string.Equals(got.Message, expected.Message, StringComparison.Ordinal)
                ? OracleVerdict.Agree
                : OracleVerdict.Diverge;
        }

        return string.Equals(row.Expected.Describe(), actual.Describe(), StringComparison.Ordinal)
            ? OracleVerdict.Agree
            : OracleVerdict.Diverge;
    }

    /// <summary>
    /// A recorded limit, which is in upstream's convention, as this surface spells it.
    /// </summary>
    /// <param name="recorded">Upstream's number: 0 is no limit, negative is none at all.</param>
    /// <returns>This surface's number: -1 is no limit, 0 is none at all.</returns>
    /// <remarks>
    /// The two conventions are each other's mirror at <b>both</b> ends, and translating only one end
    /// asks the two engines opposite questions. A row with a negative count reached a false RED
    /// before S24's blind review found it; only a hand-written <c>-Rows</c> row can carry one for a
    /// substitution, since that generator emits upstream's own convention, but the iteration
    /// generator draws a negative <c>maxsplit</c> deliberately.
    /// </remarks>
    private static int OurLimit(int recorded) =>
        recorded switch
        {
            0 => -1,
            < 0 => 0,
            _ => recorded,
        };

    private static IOracleOutcome Describe(Match match) => match.Success ? DescribeGroups(match) : new NoMatchOutcome();

    private static MatchOutcome DescribeGroups(Match match)
    {
        GroupCollection groups = match.Groups;
        var described = new List<OracleGroup>(groups.Count);
        for (int number = 0; number < groups.Count; number++)
        {
            Group group = groups[number];
            described.Add(
                group.Success
                    ? new OracleGroup(
                        number,
                        true,
                        group.Index,
                        group.Length,
                        [.. group.Captures.Select(capture => new OracleSpan(capture.Index, capture.Length))]
                    )
                    : new OracleGroup(number, false, 0, 0, [])
            );
        }

        return new MatchOutcome(described, match.LastGroupNumber, match.LastGroupName, match.PartialMatch);
    }
}
