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
            // Asked of nothing, not asked and discarded. Upstream never finished this row, so there
            // is no answer to compare against - and putting the question to this port anyway would
            // spend RowTimeout on a row whose verdict is already decided, which on a wave carrying
            // several of them is minutes of wall clock for no information.
            //
            // UNLESS THE ROW CARRIES ITS OWN DEADLINE (S52). Then not finishing IS the recorded
            // answer, because the shape was measured catastrophic on both engines at more than twenty
            // times that budget, and the row is put to this port like any other. The cost objection
            // does not apply either: the budget is a fraction of a second by construction, where
            // RowTimeout is ten.
            if (row.Expected is TimeoutOutcome && row.Timeout is null)
            {
                tally[OracleVerdict.Timeout] = tally.GetValueOrDefault(OracleVerdict.Timeout) + 1;
                continue;
            }

            // S43, on the same terms: upstream ran out of heap rather than out of time, and a row
            // it could not answer is a row this port cannot be scored against. Not asked, for the
            // same wall-clock reason - a BESTMATCH blowup costs this engine its whole RowTimeout too.
            if (row.Expected is ResourceOutcome)
            {
                tally[OracleVerdict.Resource] = tally.GetValueOrDefault(OracleVerdict.Resource) + 1;
                continue;
            }

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
    /// <remarks>
    /// The row's own budget where it has one, and <see cref="RowTimeout"/> otherwise. A timeout row's
    /// budget is the QUESTION upstream was asked, so giving this port a different one would compare
    /// two answers to two questions - and giving it ten seconds would spend ten seconds a row on a
    /// generator every one of whose rows is meant to run out.
    /// </remarks>
    public static IOracleOutcome? Run(OracleRow row) => Run(row, lazy: false);

    /// <summary>Puts a row's question to this port, eagerly or through the lazy twin.</summary>
    /// <param name="row">The row to run.</param>
    /// <param name="lazy">
    /// Ask an iteration row through <c>EnumerateMatches</c>/<c>EnumerateSplits</c> (S53b). No other
    /// operation has a lazy twin, so no other operation reads it.
    /// </param>
    /// <returns>This port's answer.</returns>
    public static IOracleOutcome? Run(OracleRow row, bool lazy)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Run(row, row.Timeout is double budget ? TimeSpan.FromSeconds(budget) : RowTimeout, lazy);
    }

    /// <summary>Puts a row's question to this port, with an explicit deadline.</summary>
    /// <remarks>
    /// The deadline is a parameter only so a test can prove it reaches the engine without waiting
    /// <see cref="RowTimeout"/> to find out. Every real caller uses the overload above.
    /// </remarks>
    /// <param name="row">The row to run.</param>
    /// <param name="timeout">How long the matching call may take.</param>
    /// <param name="lazy">
    /// Ask an iteration row through <c>EnumerateMatches</c>/<c>EnumerateSplits</c> rather than
    /// through <c>Matches</c>/<c>Split</c> (S53b). Every other operation ignores it, having no
    /// lazy twin. The wave itself is always run eagerly; this is for the test that runs each row
    /// both ways and requires the same answer.
    /// </param>
    /// <param name="ablate">
    /// Applied to the compiled pattern before it is asked anything, so that a caller can take one
    /// named piece of this engine's behaviour away and see what the row answers without it. Used by
    /// <see cref="RunWithoutTheAnchorPin"/> and <see cref="RunWithoutTheFoldFix"/> and by nothing
    /// else; the wave always passes
    /// <see langword="null"/>. It runs on a pattern this method compiled and drops, so nothing the
    /// caller shares is mutated.
    /// </param>
    /// <returns>This port's answer, as the overload above describes it.</returns>
    internal static IOracleOutcome? Run(
        OracleRow row,
        TimeSpan timeout,
        bool lazy = false,
        Action<FuzzyRegex>? ablate = null
    )
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
            compiled = FuzzyRegex.WithDefaultVersion(
                row.Pattern,
                (FuzzyRegexOptions)row.Flags,
                // NOT the row deadline: it is passed PER CALL below instead, so that every row of
                // every wave exercises S51's per-call budget rather than the constructor's. The
                // deadline itself is unchanged, and so is every verdict - measured, see the closing
                // notes for S51 - which is the point: the same waves, through the new plumbing.
                FuzzyRegex.InfiniteMatchTimeout,
                row.NamedLists.ToDictionary(
                    static entry => entry.Key,
                    static entry => (IReadOnlyCollection<string>)entry.Value,
                    StringComparer.Ordinal
                ),
                // The version the RECORDER resolved this row under, not this port's own default,
                // which S50b made Version1. A default rather than a flag: a row whose pattern says
                // (?V1) must still get version 1, and both bits at once is "VERSION0 and VERSION1
                // flags are mutually incompatible". It is a METHOD rather than the constructor it
                // used to be because this call site was captured silently once already: S56b gave
                // the public constructor a trailing int of its own and this row's version became a
                // compile budget, which the waves reported as three ordinary divergences.
                row.DefaultVersion
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

        ablate?.Invoke(compiled);

        try
        {
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
            //
            // Hoisted above the operation switch by S53b, which gave `sub` and `subf` the same pair:
            // the recorder now draws `pos`/`endpos` on a substitution row too, and one reading of
            // the slice serves every operation that takes one.
            int beginning = row.Pos ?? 0;
            int length =
                row.Pos is null && row.EndPos is null
                    ? -1
                    : Math.Max(0, (row.EndPos ?? row.Subject.Length) - beginning);

            if (row.Operation is "sub" or "subf")
            {
                string replaced = string.Equals(row.Operation, "sub", StringComparison.Ordinal)
                    ? compiled.Replace(
                        row.Subject,
                        row.Template!,
                        OurLimit(row.Count),
                        out int replacements,
                        beginning,
                        length,
                        timeout
                    )
                    : compiled.ReplaceFormat(
                        row.Subject,
                        row.Template!,
                        OurLimit(row.Count),
                        out replacements,
                        beginning,
                        length,
                        timeout
                    );

                return new SubOutcome(replaced, replacements);
            }

            if (row.Operation is "finditer" or "finditer-overlapped")
            {
                bool overlapped = string.Equals(row.Operation, "finditer-overlapped", StringComparison.Ordinal);

                // The lazy walk is the SAME QUESTION asked through the other entry point (S53b).
                // Nothing here compares them - that is
                // `The_lazy_walks_answer_exactly_what_the_eager_ones_do` in OracleWaveTests, which
                // runs each row both ways - but routing both through this one method is what makes
                // the two answers comparable at all.
                IEnumerable<Match> found = lazy
                    ? compiled.EnumerateMatches(row.Subject, overlapped: overlapped, timeout: timeout)
                    : compiled.Matches(row.Subject, overlapped: overlapped, timeout: timeout);

                return new MatchesOutcome([
                    .. found.Select(match => DescribeGroups(match, PositionsUnavailableUpstream(compiled))),
                ]);
            }

            if (string.Equals(row.Operation, "split", StringComparison.Ordinal))
            {
                return new SplitOutcome(
                    lazy
                        ? [.. compiled.EnumerateSplits(row.Subject, OurLimit(row.Count), timeout)]
                        : compiled.Split(row.Subject, OurLimit(row.Count), timeout)
                );
            }

            Match match = row.Operation switch
            {
                "search" => compiled.Match(row.Subject, beginning, length, row.Partial, timeout),
                "match" => compiled.MatchAtStart(row.Subject, beginning, length, row.Partial, timeout),
                // The operation is validated when the row is read, so there is no other case.
                _ => compiled.FullMatch(row.Subject, beginning, length, row.Partial, timeout),
            };

            return Describe(match, PositionsUnavailableUpstream(compiled));
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

    /// <summary>Puts a row's question to this port with the issue 563/564 anchor pin switched off.</summary>
    /// <remarks>
    /// <para>
    /// S57c fixed upstream issues 563 and 564: where a position assertion pins a fuzzy match to the
    /// search anchor, this port permits an insertion there and upstream does not
    /// (<c>docs/DIVERGENCES.md</c>, <c>_regex.c:10214</c>). The fix reads one field,
    /// <c>PatternObject.AnchorGuards</c>, which the optimiser fills in; emptying it on a compiled
    /// pattern leaves an engine that applies upstream's rule exactly, because
    /// <c>Matcher.AnchorIsPinned</c> then returns false at every site without looking at anything
    /// else.
    /// </para>
    /// <para>
    /// That is what makes the <c>fuzzy-insertion-at-a-pinned-anchor</c> entry a classification
    /// rather than a silencer: a row belongs to the family when taking this one piece away
    /// reproduces upstream's recorded answer exactly. A row this port gets wrong for any other
    /// reason still diverges without the pin, and is reported.
    /// </para>
    /// </remarks>
    /// <param name="row">The row to run.</param>
    /// <returns>What this port answers without the fix, on the row's own deadline.</returns>
    internal static IOracleOutcome? RunWithoutTheAnchorPin(OracleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Run(
            row,
            row.Timeout is double budget ? TimeSpan.FromSeconds(budget) : RowTimeout,
            lazy: false,
            ablate: static compiled => compiled.PatternObject.AnchorGuards = null
        );
    }

    /// <summary>Puts a row's question to this port with the S83 full-fold deletion fix switched off.</summary>
    /// <remarks>
    /// <para>
    /// S83 fixed a fuzzy deletion that finishes a full-case-folded string or backreference: upstream
    /// charges the next subject character's folding as a half-matched leftover even though no part
    /// of it was used (<c>docs/DIVERGENCES.md</c>, <c>_regex.c:14856</c>, <c>:14154</c>). Setting
    /// <c>PatternObject.ChargeUntouchedFoldings</c> on a compiled pattern makes
    /// <c>Matcher.FoldingIsPartUsed</c> apply upstream's test exactly, at every site that reads it.
    /// </para>
    /// <para>
    /// The <c>full-fold-fuzzy-deletion</c> entry keys on this the way
    /// <c>fuzzy-insertion-at-a-pinned-anchor</c> keys on <see cref="RunWithoutTheAnchorPin"/>: a row
    /// belongs to the family only when switching the fix off reproduces upstream's recorded answer.
    /// </para>
    /// </remarks>
    /// <param name="row">The row to run.</param>
    /// <returns>What this port answers without the fix, on the row's own deadline.</returns>
    internal static IOracleOutcome? RunWithoutTheFoldFix(OracleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Run(
            row,
            row.Timeout is double budget ? TimeSpan.FromSeconds(budget) : RowTimeout,
            lazy: false,
            ablate: static compiled => compiled.PatternObject.ChargeUntouchedFoldings = true
        );
    }

    /// <summary>Diffs one row's recorded answer against this port's.</summary>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="actual">This port's answer, or <see langword="null"/> for unsupported.</param>
    /// <returns>The verdict.</returns>
    public static OracleVerdict Compare(OracleRow row, IOracleOutcome? actual)
    {
        ArgumentNullException.ThrowIfNull(row);

        // S52's timeout rows, and they have to be decided BEFORE the blanket rule below, which would
        // otherwise swallow them. Upstream was given a budget it was measured to blow through by more
        // than twenty times, so what is compared is whether this port also ran out of it.
        if (row.Expected is TimeoutOutcome && row.Timeout is not null)
        {
            // Nothing else counts. `WhileMatching` because a REJECTION of the pattern is not running
            // out of time - upstream compiled this one and started matching it - and the exception
            // type by name because `_rejections` deliberately excludes this one: a port that times
            // out where upstream ANSWERED is a divergence, and that rule must not be weakened here.
            bool ranOutToo =
                actual is ErrorOutcome error
                && error.WhileMatching
                && string.Equals(
                    error.Exception,
                    nameof(System.Text.RegularExpressions.RegexMatchTimeoutException),
                    StringComparison.Ordinal
                );

            return ranOutToo ? OracleVerdict.Agree : OracleVerdict.Diverge;
        }

        // First, and before anything is read off `actual`. Upstream gave no answer at all, so no
        // answer this port gives can agree or disagree with it - including no answer, which would
        // otherwise read as `Unsupported` and say something false about the port's coverage.
        if (row.Expected is TimeoutOutcome)
        {
            return OracleVerdict.Timeout;
        }

        // S43, and for the identical reason: upstream hit an interpreter limit, so it gave no
        // answer that anything can agree or disagree with.
        if (row.Expected is ResourceOutcome)
        {
            return OracleVerdict.Resource;
        }

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

    /// <summary>
    /// Whether upstream could have been asked where a fuzzy match spent its errors, which it cannot
    /// be on a POSIX pattern.
    /// </summary>
    /// <param name="compiled">The pattern, as this port compiled it.</param>
    /// <returns><see langword="true"/> where the recorder had to omit the change positions.</returns>
    /// <remarks>
    /// <para>
    /// Reading <c>Match.fuzzy_changes</c> on a POSIX fuzzy match that spent an error is an access
    /// violation that kills the recorder outright - not an exception, so no <c>except</c> clause can
    /// see it, and <c>record-oracle.py --rows</c> over one such row exits 139 and writes no file at
    /// all. <c>Match.fuzzy_counts</c> on the same match answers correctly, so the recorder records
    /// the counts and omits the positions, and this side drops its own positions to match. Ledger
    /// entry 9; the reads are enumerated in <c>tools/probes/upstream-posix-fuzzy-safe-attributes.py</c>.
    /// </para>
    /// <para>
    /// Read off the COMPILED pattern rather than off <c>row.Flags</c>, because an inline <c>(?p)</c>
    /// never reaches the flags the row carries. Upstream's recorder reads its own
    /// <c>Pattern.flags</c> for the same reason and the two agree on every spelling - the flag, a
    /// leading <c>(?p)</c>, one written mid-pattern, and one inside a group
    /// (<c>tools/probes/upstream-posix-flag-is-visible-on-compiled.py</c>, regex 2026.9.10,
    /// 2026-09-14). Deriving it on each side independently rather than passing a recorded marker is
    /// deliberate: if the two ever stopped agreeing about which rows are POSIX, the row would be
    /// REPORTED as a divergence rather than quietly compared with the positions dropped.
    /// </para>
    /// </remarks>
    private static bool PositionsUnavailableUpstream(FuzzyRegex compiled) =>
        (compiled.Options & FuzzyRegexOptions.Posix) != FuzzyRegexOptions.None;

    private static IOracleOutcome Describe(Match match, bool positionsUnavailable) =>
        match.Success ? DescribeGroups(match, positionsUnavailable) : new NoMatchOutcome();

    private static MatchOutcome DescribeGroups(Match match, bool positionsUnavailable)
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
                        [.. group.Captures.Select(static capture => new OracleSpan(capture.Index, capture.Length))]
                    )
                    : new OracleGroup(number, false, 0, 0, [])
            );
        }

        FuzzyCounts counts = match.FuzzyCounts;
        FuzzyChanges changes = match.FuzzyChanges;
        OracleFuzzy fuzzy = new OracleFuzzy(
            counts.Substitutions,
            counts.Insertions,
            counts.Deletions,
            changes.Substitutions,
            changes.Insertions,
            changes.Deletions
        );

        if (positionsUnavailable)
        {
            fuzzy = fuzzy.WithoutPositions();
        }

        return new MatchOutcome(
            described,
            match.LastGroupNumber,
            match.LastGroupName,
            match.PartialMatch,
            // A match that used no errors renders no fuzzy half at all, which is what the recorder
            // writes for one too - so an exact match of a fuzzy pattern reads the same as a match of
            // an exact one, and every wave recorded before S38 still compares. That test comes after
            // the positions are dropped and not before it: an exact POSIX match is one upstream never
            // died on, and rendering it as `fuzzy=(0,0,0)` here would red every such row.
            fuzzy.IsExact
                ? null
                : fuzzy
        );
    }
}
