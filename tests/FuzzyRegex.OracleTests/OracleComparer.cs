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
    ];

    /// <summary>Puts every row's question to an engine and tallies the verdicts.</summary>
    /// <param name="rows">The wave's rows.</param>
    /// <param name="engine">
    /// What answers a row. <see cref="Run"/> in the wave run; a deliberately wrong stand-in in
    /// <c>OracleWaveTests.Corrupting_a_recorded_row_is_reported_as_a_divergence</c>, which is the
    /// only way this loop can be shown to notice a wrong answer while the real engine still
    /// answers <see cref="OracleVerdict.Unsupported"/> to everything.
    /// </param>
    /// <returns>The tally and one rendered block per diverging row.</returns>
    public static OracleRunSummary RunWave(IEnumerable<OracleRow> rows, Func<OracleRow, IOracleOutcome?> engine)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(engine);

        var tally = new Dictionary<OracleVerdict, int>();
        var divergences = new List<string>();
        foreach (OracleRow row in rows)
        {
            IOracleOutcome? actual = engine(row);
            OracleVerdict verdict = Compare(row, actual);
            tally[verdict] = tally.GetValueOrDefault(verdict) + 1;
            if (verdict == OracleVerdict.Diverge)
            {
                divergences.Add(OracleWave.Describe(row, actual));
            }
        }

        return new OracleRunSummary(tally, divergences);
    }

    /// <summary>Puts a row's question to this port.</summary>
    /// <param name="row">The row to run.</param>
    /// <returns>
    /// This port's answer; <see langword="null"/> if it could not even compile the pattern because
    /// of an unported seam, or <see cref="CompiledButUnmatched"/> if it compiled and the seam is in
    /// the matcher. The two are not interchangeable: only the first knows nothing at all.
    /// </returns>
    public static IOracleOutcome? Run(OracleRow row)
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
                FuzzyRegex.InfiniteMatchTimeout,
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
            Match match = row.Operation switch
            {
                "search" => compiled.Match(row.Subject),
                "match" => compiled.MatchAtStart(row.Subject),
                // The operation is validated when the row is read, so there is no other case.
                _ => compiled.FullMatch(row.Subject),
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
            // ponytail: unreachable until S16 lands a matcher - every match entry point throws
            // NotImplementedException today, so the catch above takes everything. The first slice
            // that can throw from here must pin the attribution with a test; until then only
            // Compare's side of the flag is covered (OracleWaveTests).
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

            // Upstream rejected the input before it matched anything, so an exception this port
            // raised *while matching* is not the same answer however plausible its type looks: it
            // is a crash on a row this port should have refused to compile. Without this the
            // allow-list alone let an ArgumentOutOfRangeException out of the engine be filed as
            // parity with upstream's ValueError about conflicting flags.
            if (got.WhileMatching)
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

    private static IOracleOutcome Describe(Match match)
    {
        if (!match.Success)
        {
            return new NoMatchOutcome();
        }

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

        return new MatchOutcome(described);
    }
}
