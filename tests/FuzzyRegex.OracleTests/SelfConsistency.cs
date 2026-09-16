namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// The metamorphic invariants of <c>docs/ORACLE-INVARIANTS.md</c>, applied to one engine's own
/// answer with no comparison to anything.
/// </summary>
/// <remarks>
/// <para>
/// THE DIFFERENTIAL ORACLE IS BLIND TO A BUG THE PORT INHERITED LINE FOR LINE, because then the two
/// engines agree - <c>docs/plan/upstream-reports/LEDGER.md</c> entry 11 mechanisms C and D are
/// exactly that. What has found every serious inherited bug so far is one engine contradicting
/// ITSELF, by hand, when a session happened to look. This is that check, automatic and on every row.
/// </para>
/// <para>
/// THE SAME THREE INVARIANTS ARE CHECKED ON UPSTREAM by <c>_structural_violations</c> in
/// <c>tools/record-oracle.py</c>, which writes their ids into a row's <c>selfContradiction</c>. The
/// two implementations are deliberately parallel, statement for statement, because the question
/// "does this port break it where upstream does not" is only answerable when both sides run the same
/// check. S52c scope item 4.
/// </para>
/// <para>
/// A VIOLATION HERE IS A PORT BUG unless the same row carries the same id in
/// <see cref="OracleRow.SelfContradiction" />, in which case the port is reproducing upstream's own
/// contradiction and the ledger owes the reading. The failure message says which, because the two
/// need completely different work and telling them apart by hand is what S52c exists to stop.
/// </para>
/// </remarks>
internal static class SelfConsistency
{
    /// <summary>
    /// Every invariant this answer breaks, by id, or an empty list.
    /// </summary>
    /// <param name="pattern">The row's pattern, which decides the narrowing below.</param>
    /// <param name="outcome">The engine's answer.</param>
    /// <returns>The ids, sorted and without duplicates.</returns>
    public static IReadOnlyList<string> Check(string pattern, IOracleOutcome? outcome)
    {
        IEnumerable<MatchOutcome> matches = outcome switch
        {
            MatchOutcome single => [single],
            MatchesOutcome scan => scan.Matches,
            _ => [],
        };

        SortedSet<string> violations = [];
        bool spansAreInside = !SpanEscapesTheMatch(pattern);

        foreach (MatchOutcome match in matches)
        {
            // `fuzzy-counts-match-changes`, ledger entry 11. S47 put this property on
            // `OracleFuzzy` and swept a whole wave with it; this is that sweep, renamed to the
            // invariant's id so the port's violations and upstream's are counted under one name.
            if (match.Fuzzy is { CountsAgreeWithPositions: false })
            {
                violations.Add("fuzzy-counts-match-changes");
            }

            // `lastindex-participated`. Upstream documents `lastindex` as the last group that
            // PARTICIPATED, so naming one that did not is the match object contradicting itself.
            //
            // ONLY THE PARTICIPATION LIMB. ORACLE-INVARIANTS.md also claimed "and `lastgroup` names
            // the same group", which is FALSE and measured to be:
            // `tools/probes/upstream-free-tier-invariant-grounds.py` section 1 gives lastindex=2 and
            // lastgroup='x' for `(?P<x>a)(b)` over "ab", where group 2 has no name at all.
            if (
                match.LastIndex != -1
                && (
                    match.LastIndex < 0
                    || match.LastIndex >= match.Groups.Count
                    || !match.Groups[match.LastIndex].Success
                )
            )
            {
                violations.Add("lastindex-participated");
            }

            // `group-spans-inside-match`, narrowed - see `SpanEscapesTheMatch`.
            if (spansAreInside && match.Groups.Count > 1)
            {
                OracleGroup whole = match.Groups[0];
                int start = whole.Index;
                int end = whole.Index + whole.Length;

                foreach (OracleGroup group in match.Groups.Skip(1))
                {
                    if (!group.Success)
                    {
                        continue;
                    }

                    IEnumerable<OracleSpan> spans = [new OracleSpan(group.Index, group.Length), .. group.Captures];
                    if (spans.Any(span => span.Index < start || span.Index + span.Length > end))
                    {
                        violations.Add("group-spans-inside-match");
                        break;
                    }
                }
            }
        }

        return [.. violations];
    }

    /// <summary>
    /// Whether a pattern can legitimately report a group span outside its own match span.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true" /> where <c>group-spans-inside-match</c> does not apply.</returns>
    /// <remarks>
    /// <para>
    /// BOTH NARROWINGS ARE MEASURED, in <c>tools/probes/upstream-free-tier-invariant-grounds.py</c>
    /// sections 3 and 5 (regex 2026.9.10, 2026-09-16), not reasoned:
    /// </para>
    /// <list type="bullet">
    /// <item><c>\K</c> resets the reported match start, so a group before it lies outside the span -
    /// <c>(a)\Kb</c> over "ab" is match (1, 2) with group 1 at (0, 1).</item>
    /// <item>a lookaround consumes nothing, so a group inside one matches text the match never
    /// covered - <c>a(?=(b))</c> over "ab" is match (0, 1) with group 1 at (1, 2).</item>
    /// </list>
    /// <para>
    /// A GROUP CALL IS NOT NARROWED AROUND, though the invariant file's first draft said it was: the
    /// same probe measured <c>(a)b(?1)</c> and <c>(?P&lt;g&gt;a)b(?&amp;g)</c> over "aba" and each
    /// records group 1 at (0, 1), INSIDE the match. What the ledger records under a group call is one
    /// inside a LOOKAROUND, which the second narrowing already covers.
    /// </para>
    /// <para>
    /// Gated on the text, like the recorder's own controls: <c>(?=</c> inside a character class is
    /// literal text and such a pattern is skipped when it need not be. That direction loses a check;
    /// the other invents a violation, which is the one that wastes a triage.
    /// </para>
    /// </remarks>
    private static bool SpanEscapesTheMatch(string pattern) =>
        pattern.Contains(@"\K", StringComparison.Ordinal)
        || pattern.Contains("(?=", StringComparison.Ordinal)
        || pattern.Contains("(?!", StringComparison.Ordinal)
        || pattern.Contains("(?<=", StringComparison.Ordinal)
        || pattern.Contains("(?<!", StringComparison.Ordinal);
}
