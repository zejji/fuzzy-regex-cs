using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// A wave of differential-oracle rows: what upstream answered for each (pattern, flags, named
/// lists, subject, operation), recorded by <c>tools/record-oracle.py</c>.
/// </summary>
/// <remarks>
/// <para>
/// The matching-time analogue of the compile-parity corpus, and the reason it is not committed:
/// the corpus is derived from upstream's own finite suite, while a wave is generated from a seed
/// out of an infinite space. Pinning one sample would turn a generative tester into a fixture.
/// Only minimised divergences become permanent tests - see the header of the recorder for the
/// workflow, and VERIFICATION.md rule 7 for why it is not optional.
/// </para>
/// <para>
/// The recorded spans are already UTF-16 <c>(Index, Length)</c>: the recorder converts Python's
/// codepoint <c>(start, end)</c> against the subject it has in hand, so this side compares
/// verbatim and never converts. That is deliberate - the span convention is enforced in exactly
/// two places, here and in <c>Match</c>/<c>Group</c>'s accessors, so a slip at the accessor shows
/// up as a divergence instead of as a silent agreement (DECISIONS 2026-08-31).
/// </para>
/// </remarks>
internal static class OracleWave
{
    /// <summary>
    /// The repository root: the nearest ancestor of the test binary holding <c>global.json</c>.
    /// </summary>
    /// <remarks>
    /// Derived rather than configured. The recorder and this consumer have to agree on one path,
    /// and an environment variable to carry it would be a second thing to get wrong for a value
    /// neither side ever needs to vary. Declared before the two paths that use it: static
    /// initialisers run in declaration order.
    /// </remarks>
    private static readonly string _repoRoot = FindRepoRoot();

    /// <summary>Where the recorder writes the wave and where this consumer reads it.</summary>
    public static string WavePath { get; } = Path.Combine(_repoRoot, "TestResults", "oracle", "wave.jsonl");

    /// <summary>Where the run's verdict and every divergence block is written.</summary>
    /// <remarks>Under <c>TestResults/</c>, which is what oracle.yml uploads as an artifact.</remarks>
    public static string ReportPath { get; } = Path.Combine(_repoRoot, "TestResults", "oracle", "report.txt");

    private static string FindRepoRoot()
    {
        for (
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"no global.json in any ancestor of '{AppContext.BaseDirectory}', so the repository "
                + "root cannot be located."
        );
    }

    /// <summary>Reads the recorded wave from <see cref="WavePath"/>.</summary>
    /// <returns>The header and every row.</returns>
    /// <exception cref="InvalidOperationException">There is no wave to read.</exception>
    public static OracleWaveFile Load()
    {
        if (!File.Exists(WavePath))
        {
            // A missing wave is a red run, not an empty one. The whole point of the harness is
            // that it ran, so "no rows" must never look like "nothing diverged".
            throw new InvalidOperationException(
                $"there is no recorded wave at '{WavePath}'. Record one and run the consumer with "
                    + "`pwsh -File tools/run-oracle.ps1`, which does both."
            );
        }

        string[] lines = File.ReadAllLines(WavePath);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException($"the wave at '{WavePath}' is empty.");
        }

        using JsonDocument headerDocument = JsonDocument.Parse(lines[0]);
        JsonElement header = headerDocument.RootElement;
        if (!string.Equals(header.GetProperty("kind").GetString(), "header", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"the first line of '{WavePath}' is not the header.");
        }

        var parsed = new OracleHeader(
            header.GetProperty("regexVersion").GetString()!,
            header.GetProperty("versionSource").GetString()!,
            header.GetProperty("pinnedVersion").GetString()!,
            header.GetProperty("upstreamCommit").GetString()!,
            header.GetProperty("defaultVersion").GetInt32(),
            header.GetProperty("waves").GetRawText()
        );

        IReadOnlyList<OracleRow> rows = ParseRows(lines.Skip(1));
        int declared = header.GetProperty("rowCount").GetInt32();
        if (declared != rows.Count)
        {
            // A truncated file - the recorder killed part-way, a partial copy - would otherwise
            // read as a smaller wave that agreed on everything it contained.
            throw new InvalidOperationException(
                $"the wave at '{WavePath}' declares {declared} rows and holds {rows.Count}."
            );
        }

        return new OracleWaveFile(parsed, rows);
    }

    /// <summary>Parses rows from recorded JSONL text, one row per line.</summary>
    /// <param name="text">The recorded lines, without the header.</param>
    /// <returns>The parsed rows, numbered from 1 in file order.</returns>
    public static IReadOnlyList<OracleRow> ParseRows(string text) =>
        ParseRows(text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyList<OracleRow> ParseRows(IEnumerable<string> lines) =>
        [
            .. lines
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Select(static (line, index) => ParseRow(line, index + 1)),
        ];

    private static OracleRow ParseRow(string line, int number)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement row = document.RootElement;

        string operation = row.GetProperty("operation").GetString()!;
        if (
            operation
            is not (
                "search"
                or "match"
                or "fullmatch"
                or "sub"
                or "subf"
                or "finditer"
                or "finditer-overlapped"
                or "split"
            )
        )
        {
            // Rejected at read time rather than at run time, so an unknown operation cannot be
            // reported as a divergence in the port.
            throw new InvalidOperationException($"row {number}: unknown operation '{operation}'.");
        }

        // A substitution row carries the template; nothing else does. A sub row without a template
        // would silently become "does this pattern match", which every port agrees with, so it is
        // refused here as well as in the recorder.
        string? template = null;
        if (operation is "sub" or "subf")
        {
            if (!row.TryGetProperty("template", out JsonElement templateElement))
            {
                throw new InvalidOperationException($"row {number}: a '{operation}' row has no template.");
            }

            template = templateElement.GetString()!;
        }

        // The limit, in upstream's convention, which a substitution and a split both carry.
        int count = 0;
        if (operation is "sub" or "subf" or "split")
        {
            count = row.TryGetProperty("count", out JsonElement limit) ? limit.GetInt32() : 0;
        }

        return new OracleRow(
            number,
            row.GetProperty("generator").GetString()!,
            row.GetProperty("pattern").GetString()!,
            row.GetProperty("flags").GetInt32(),
            ReadNamedLists(row.GetProperty("namedLists")),
            row.GetProperty("subject").GetString()!,
            operation,
            ReadOutcome(row.GetProperty("outcome")),
            ReadCodepointSpan(row.GetProperty("codepointSpan")),
            template,
            count,
            row.TryGetProperty("partial", out JsonElement partial) && partial.GetBoolean(),
            row.TryGetProperty("pos", out JsonElement pos) ? pos.GetInt32() : null,
            row.TryGetProperty("endpos", out JsonElement endpos) ? endpos.GetInt32() : null,
            row.TryGetProperty("searchOnlyPartial", out JsonElement searchOnly)
                && searchOnly.ValueKind == JsonValueKind.True,
            ReadMatchList(row, "anchoredScan"),
            ReadMatchList(row, "subMatches"),
            row.TryGetProperty("bestmatchFreeOutcome", out JsonElement bestmatchFree)
                ? ReadOutcome(bestmatchFree)
                : null,
            ReadLeakFreeFuzzy(row)
        );
    }

    /// <summary>
    /// The recorder's <c>leakFreeFuzzy</c>: upstream's own fuzzy half per recorded match, asked
    /// again anchored at the span it reported, or <see langword="null"/> where the row carries none.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>One entry per recorded match - itself <see langword="null"/> where upstream would
    /// not answer - or <see langword="null"/> where the question was never asked.</returns>
    private static IReadOnlyList<OracleFuzzy?>? ReadLeakFreeFuzzy(JsonElement row) =>
        row.TryGetProperty("leakFreeFuzzy", out JsonElement asked) && asked.ValueKind == JsonValueKind.Array
            ?
            [
                .. asked
                    .EnumerateArray()
                    .Select(static answer => answer.ValueKind == JsonValueKind.Null ? null : ReadFuzzy(answer)),
            ]
            : null;

    /// <summary>
    /// One of the recorder's second-fact match lists - <c>anchoredScan</c> or <c>subMatches</c> - or
    /// <see langword="null"/> when the row does not carry that one. A row carries an
    /// <c>anchoredScan</c> only when it is an overlapped <c>finditer</c> whose pattern contains
    /// <c>(*SKIP)</c> and, if reversed, reads nothing at the end of the subject; it carries
    /// <c>subMatches</c> only when it is a substitution whose pattern contains <c>(*SKIP)</c>. Every
    /// other row, and every wave recorded before the field existed, reads as <see langword="null"/>.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="name">The field.</param>
    /// <returns>The matches, or <see langword="null"/>.</returns>
    private static MatchesOutcome? ReadMatchList(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement matches) && matches.ValueKind == JsonValueKind.Array
            ? new MatchesOutcome([.. matches.EnumerateArray().Select(ReadMatch)])
            : null;

    private static IOracleOutcome ReadOutcome(JsonElement outcome)
    {
        string kind = outcome.GetProperty("kind").GetString()!;
        return kind switch
        {
            "nomatch" => new NoMatchOutcome(),
            // S40a. No ground truth at all for this row: upstream never finished it.
            "timeout" => new TimeoutOutcome(outcome.GetProperty("seconds").GetDouble()),
            // S43, and the same "no ground truth" case by a different route: upstream hit a limit
            // of the interpreter rather than finishing or rejecting.
            "resource" => new ResourceOutcome(outcome.GetProperty("exception").GetString()!),
            // 'whileMatching' is optional and defaults to false, which is what every wave recorded
            // before S24 means: until substitution landed, upstream's only recorded rejections came
            // out of regex.compile. A hand-written minimisation row need not carry it either.
            "error" => new ErrorOutcome(
                outcome.GetProperty("exception").GetString()!,
                outcome.GetProperty("message").GetString()!,
                WhileMatching: outcome.TryGetProperty("whileMatching", out JsonElement phase) && phase.GetBoolean()
            ),
            "sub" => new SubOutcome(outcome.GetProperty("text").GetString()!, outcome.GetProperty("count").GetInt32()),
            "match" => ReadMatch(outcome),
            "matches" => new MatchesOutcome([.. outcome.GetProperty("matches").EnumerateArray().Select(ReadMatch)]),
            // A part is null where a capturing group took no part in that match, and that is the
            // whole difference between upstream's split and Regex.Split's, so null is read as null
            // rather than coalesced to "".
            "split" => new SplitOutcome([
                .. outcome.GetProperty("parts").EnumerateArray().Select(static part => part.GetString()),
            ]),
            _ => throw new InvalidOperationException($"unknown outcome kind '{kind}'."),
        };
    }

    private static MatchOutcome ReadMatch(JsonElement match) =>
        new(
            [.. match.GetProperty("groups").EnumerateArray().Select(ReadGroup)],
            match.GetProperty("lastIndex").GetInt32(),
            match.GetProperty("lastGroup").GetString(),
            // Optional and false by default, like 'whileMatching': every wave recorded before S31
            // means "not a partial match", and a hand-written minimisation row need not carry it.
            match.TryGetProperty("partial", out JsonElement partial) && partial.GetBoolean(),
            // Optional and absent by default for the same reason, since S38: a row with no fuzzy
            // half used no errors.
            ReadFuzzy(match)
        );

    private static OracleFuzzy? ReadFuzzy(JsonElement match)
    {
        if (!match.TryGetProperty("fuzzyCounts", out JsonElement counts))
        {
            return null;
        }

        // Absent where upstream cannot be asked, which is a POSIX fuzzy match that spent an error:
        // reading `Match.fuzzy_changes` on one is an access violation that takes the whole recorder
        // with it (ledger entry 9), where `fuzzy_counts` on the same match answers correctly. The
        // recorder omits the key on exactly those rows and the comparison drops the positions from
        // both sides - never the counts, which are compared as they always were.
        if (!match.TryGetProperty("fuzzyChanges", out JsonElement changes))
        {
            return new OracleFuzzy(counts[0].GetInt32(), counts[1].GetInt32(), counts[2].GetInt32(), null, null, null);
        }

        return new OracleFuzzy(
            counts[0].GetInt32(),
            counts[1].GetInt32(),
            counts[2].GetInt32(),
            ReadPositions(changes, "substitutions"),
            ReadPositions(changes, "insertions"),
            ReadPositions(changes, "deletions")
        );
    }

    private static int[] ReadPositions(JsonElement changes, string name) =>
        [.. changes.GetProperty(name).EnumerateArray().Select(static position => position.GetInt32())];

    private static OracleGroup ReadGroup(JsonElement group) =>
        new(
            group.GetProperty("number").GetInt32(),
            group.GetProperty("success").GetBoolean(),
            group.GetProperty("index").GetInt32(),
            group.GetProperty("length").GetInt32(),
            [.. group.GetProperty("captures").EnumerateArray().Select(ReadSpan)]
        );

    private static OracleSpan ReadSpan(JsonElement span) => new(span[0].GetInt32(), span[1].GetInt32());

    private static (int Start, int End)? ReadCodepointSpan(JsonElement span) =>
        span.ValueKind == JsonValueKind.Null ? null : (span[0].GetInt32(), span[1].GetInt32());

    private static Dictionary<string, IReadOnlyList<string>> ReadNamedLists(JsonElement namedLists) =>
        namedLists
            .EnumerateObject()
            .ToDictionary(
                static property => property.Name,
                static property =>
                    (IReadOnlyList<string>)[.. property.Value.EnumerateArray().Select(static v => v.GetString()!)],
                StringComparer.Ordinal
            );

    /// <summary>Writes the run's verdict, and one block per divergence, to <see cref="ReportPath"/>.</summary>
    /// <param name="header">The wave's header, so the report says which oracle produced it.</param>
    /// <param name="rowCount">How many rows the wave held.</param>
    /// <param name="tally">How many rows fell into each verdict.</param>
    /// <param name="divergences">One rendered block per diverging row.</param>
    /// <param name="expected">
    /// One rendered block per row <see cref="ExpectedDivergences"/> accounted for. Printed rather
    /// than dropped: an expected divergence nobody can see in the report is a hidden one.
    /// </param>
    /// <returns>The single-line summary, which is also the first line of the report.</returns>
    public static string WriteReport(
        OracleHeader header,
        int rowCount,
        IReadOnlyDictionary<OracleVerdict, int> tally,
        IReadOnlyList<string> divergences,
        IReadOnlyList<string>? expected = null
    )
    {
        string summary = string.Create(
            CultureInfo.InvariantCulture,
            $"agree {tally.GetValueOrDefault(OracleVerdict.Agree)}  "
                + $"unsupported {tally.GetValueOrDefault(OracleVerdict.Unsupported)}  "
                + $"expected {tally.GetValueOrDefault(OracleVerdict.Expected)}  "
                + $"timeout {tally.GetValueOrDefault(OracleVerdict.Timeout)}  "
                + $"resource {tally.GetValueOrDefault(OracleVerdict.Resource)}  "
                + $"diverge {tally.GetValueOrDefault(OracleVerdict.Diverge)}  of {rowCount} rows"
        );

        var report = new StringBuilder();
        report.AppendLine(summary);
        report.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"oracle: regex {header.RegexVersion} ({header.VersionSource}), pinned "
                    + $"{header.PinnedVersion}, upstream {header.UpstreamCommit}, DEFAULT_VERSION "
                    + $"{header.DefaultVersion}"
            )
        );
        report.AppendLine("waves: " + header.Waves);
        foreach (string divergence in divergences)
        {
            report.AppendLine();
            report.AppendLine(divergence);
        }

        foreach (string block in expected ?? [])
        {
            report.AppendLine();
            report.AppendLine(block);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath, report.ToString());
        return summary;
    }

    /// <summary>Renders one diverging row: what was asked, what upstream said, what we said.</summary>
    /// <param name="row">The row that diverged.</param>
    /// <param name="actual">This port's answer, or <see langword="null"/> if it was unsupported.</param>
    /// <param name="accounted">
    /// The <see cref="ExpectedDivergences"/> entry that accounts for this row, if one does. Changes
    /// the heading from <c>DIVERGE</c> to <c>EXPECTED</c> and names the entry, so a reader can tell a
    /// classified row from an unaccounted one at a glance.
    /// </param>
    /// <returns>The rendered block.</returns>
    public static string Describe(OracleRow row, IOracleOutcome? actual, ExpectedDivergence? accounted = null)
    {
        var block = new StringBuilder();
        block.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{(accounted is null ? "DIVERGE" : "EXPECTED " + accounted.Id)} row {row.Number} "
                    + $"({row.Generator}) {row.Operation} flags=0x{row.Flags:x}"
            )
        );
        block.AppendLine("  pattern  " + Printable(row.Pattern));
        block.AppendLine("  subject  " + Printable(row.Subject));

        // Only when the row narrowed the slice, so every pre-S31 block still renders as it did -
        // and so a narrowed row is reproducible, which is the whole reason the field exists. Either
        // end on its own counts as narrowed: a row carrying only `endpos` printed no slice line at
        // all until the S31 second blind pass, which made its divergence unreadable.
        if (row.Pos is not null || row.EndPos is not null)
        {
            block.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  slice    utf16 [{row.Pos ?? 0}, {row.EndPos ?? row.Subject.Length})"
                )
            );
        }

        if (row.Template is { } template)
        {
            block.AppendLine(
                string.Create(CultureInfo.InvariantCulture, $"  template {Printable(template)}   [count {row.Count}]")
            );
        }

        if (row.NamedLists.Count > 0)
        {
            block.AppendLine(
                "  lists    "
                    + string.Join(
                        ", ",
                        row.NamedLists.Select(static entry =>
                            entry.Key + "=[" + string.Join(",", entry.Value.Select(Printable)) + "]"
                        )
                    )
            );
        }

        string codepoints = row.CodepointSpan is { } span
            ? string.Create(CultureInfo.InvariantCulture, $"   [python codepoints {span.Start},{span.End}]")
            : "";
        block.AppendLine("  upstream " + row.Expected.Describe() + codepoints);
        block.Append("  port     " + (actual?.Describe() ?? "unsupported"));
        if (actual is ErrorOutcome { Detail: { } detail })
        {
            block.AppendLine().Append("  " + detail.Replace("\n", "\n  ", StringComparison.Ordinal));
        }

        return block.ToString();
    }

    /// <summary>
    /// A string as ASCII, so the report is diffable and a control character or an astral subject
    /// cannot corrupt it.
    /// </summary>
    /// <param name="value">The string to render.</param>
    /// <returns>The quoted, escaped string.</returns>
    public static string Printable(string value)
    {
        var rendered = new StringBuilder(value.Length + 2);
        rendered.Append('\'');
        foreach (char c in value)
        {
            if (c == '\\')
            {
                rendered.Append(@"\\");
            }
            else if (char.IsControl(c) || c > '~')
            {
                rendered.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                rendered.Append(c);
            }
        }

        return rendered.Append('\'').ToString();
    }
}

/// <summary>A recorded wave: its header and its rows.</summary>
internal sealed record OracleWaveFile(OracleHeader Header, IReadOnlyList<OracleRow> Rows);

/// <summary>What one pass over a wave produced.</summary>
/// <param name="Tally">How many rows fell into each verdict.</param>
/// <param name="Divergences">One rendered block per diverging row, in wave order.</param>
/// <param name="Expected">
/// One rendered block per row <see cref="ExpectedDivergences"/> accounted for, in wave order. These
/// do not fail the run and are printed anyway.
/// </param>
internal sealed record OracleRunSummary(
    IReadOnlyDictionary<OracleVerdict, int> Tally,
    IReadOnlyList<string> Divergences,
    IReadOnlyList<string> Expected
);

/// <summary>Which oracle produced a wave, and how it was generated.</summary>
internal sealed record OracleHeader(
    string RegexVersion,
    string VersionSource,
    string PinnedVersion,
    string UpstreamCommit,
    int DefaultVersion,
    string Waves
);

/// <summary>One question put to both engines, and upstream's answer to it.</summary>
/// <param name="Number">The row's 1-based position in the wave, so a divergence is quotable.</param>
/// <param name="Generator">Which generator produced it, or <c>rows</c> if it was explicit.</param>
/// <param name="Pattern">The pattern.</param>
/// <param name="Flags">Upstream's flag bits, which are also <see cref="FuzzyRegexOptions"/>'s.</param>
/// <param name="NamedLists">The named lists, each already sorted by the recorder.</param>
/// <param name="Subject">The subject to match against.</param>
/// <param name="Operation">One of <c>search</c>, <c>match</c> or <c>fullmatch</c>.</param>
/// <param name="Expected">Upstream's answer, in UTF-16 spans.</param>
/// <param name="CodepointSpan">
/// Python's own untranslated codepoint span for the whole match, or <see langword="null"/> when
/// there was no match. Never compared - it is what makes the recorder's index translation visible
/// in the file and in a divergence block rather than merely trusted.
/// </param>
/// <param name="Template">
/// The replacement or format template, for a <c>sub</c> or <c>subf</c> row, and
/// <see langword="null"/> for every other operation.
/// </param>
/// <param name="Count">
/// The replacement limit in <b>upstream's</b> convention, where 0 means no limit. This surface
/// spells no limit as -1, so <see cref="OracleComparer.Run(OracleRow)"/> translates it; recording upstream's
/// own number keeps the wave a transcript of what upstream was asked.
/// </param>
/// <param name="Partial">
/// Whether the row was asked with upstream's <c>partial=True</c>. Part of the question, not of the
/// answer: the same pattern and subject give different results with it and without it, so a row that
/// lost this flag would be compared against an answer to a different question.
/// </param>
/// <param name="Pos">
/// Upstream's <c>pos</c>, or <see langword="null"/> for the whole subject. Also part of the
/// question. It exists because half of upstream's partial arms are bounded by
/// <c>slice_start</c>/<c>slice_end</c> and half by <c>text_start</c>/<c>text_end</c>, and the two
/// are indistinguishable until the slice is narrower than the subject.
/// </param>
/// <param name="EndPos">
/// Upstream's <c>endpos</c>, or <see langword="null"/> for the end of the subject. Independent of
/// <paramref name="Pos"/>: a row may carry either end, both or neither.
/// </param>
/// <param name="SearchOnlyPartial">
/// Recorded only on a <c>search</c> asked with <c>partial</c> that upstream answered with a partial:
/// whether upstream's own <c>match</c>, over the span that search reported, answers something OTHER
/// than the same partial. True means upstream's two doors disagree at that position, which is the
/// signature of its <c>search_start</c> prefilter and nothing else; false means the partial is the
/// matcher's own answer, and a port that misses it has a bug rather than a missing optimisation.
/// Never compared - it is a second fact about upstream, and only
/// <see cref="ExpectedDivergences"/> reads it.
/// </param>
/// <param name="AnchoredScan">
/// Recorded only on an OVERLAPPED <c>finditer</c> row whose pattern contains <c>(*SKIP)</c>, and -
/// if that row is reversed - whose pattern reads nothing at the end of the subject: the same scan
/// asked of upstream one match at a time, each step from a fresh
/// state, so that the slice a <c>(*SKIP)</c> moved cannot carry from one match into the next.
/// Upstream's stateful scanner carries it - nothing in <c>init_match</c>, <c>do_match</c> or
/// <c>scanner_search_or_match</c> puts the slice back - so a difference between this and the row's
/// own answer is upstream contradicting its own matcher. Groups and all, not just the spans,
/// because a stale slice shows in a capture as readily as in a whole-match span.
/// <see langword="null"/> on every other row and on any wave recorded before S34. Never compared;
/// only <see cref="ExpectedDivergences"/> reads it. <b>Both exclusions are load-bearing</b> - a
/// non-overlapped step needs <c>must_advance</c>, which no Python call carries, and a reversed one
/// needs <c>endpos</c>, which truncates the subject and so changes what an end-of-subject assertion
/// means. S40d narrowed the second to the patterns that actually hold such an assertion. The
/// recorder's <c>_anchored_scan</c> docstring has the measurements.
/// </param>
/// <param name="SubMatches">
/// Recorded only on a <c>sub</c> or <c>subf</c> row whose pattern contains <c>(*SKIP)</c>: the spans
/// upstream replaced at, asked separately as a <c>finditer</c> because <c>subn</c> walks the same
/// scanner. A substitution's outcome is a string and a count, so without this a diverging sub row
/// carries no match positions for a tell to read - which is exactly why seed 20260913's row 116388
/// could not be judged with the rest of its family until S40d. Groups and all, so the same two tells
/// <see cref="ExpectedDivergences"/> reads on a scan apply unchanged. Never compared, and read only
/// after its length is checked against the recorded replacement count.
/// </param>
/// <param name="BestmatchFree">
/// Recorded only on a row that carries <c>BESTMATCH</c>, and only when upstream could answer the
/// question: upstream's own answer to the SAME row with the flag deleted. <c>BESTMATCH</c> is
/// documented as a ranking flag - it chooses among the flagless engine's candidates rather than
/// inventing or destroying one (<c>upstream/README.rst:592</c>) - so this is the set the flagged
/// answer is supposed to be drawn from, and it is what makes
/// <c>bestmatch-loses-a-candidate</c> judgeable: on one row of that family the two engines
/// agree on the span, the groups AND the fuzzy counts, and differ only over which position is a
/// substitution and which an insertion, so no predicate over the two compared answers can tell the
/// family from a defect. <see langword="null"/> on every other row and on any wave recorded before
/// S46. Never compared; only <see cref="ExpectedDivergences"/> reads it.
/// </param>
/// <param name="LeakFreeFuzzy">
/// A fourth, and the same kind of thing again (S47, ledger entry 11 mechanism A). One entry per
/// recorded match, in the recorded order: upstream's own fuzzy half for that match, asked again as
/// <c>match(pos=start, endpos=end)</c> so that the winning attempt is upstream's FIRST attempt and no
/// earlier one can have left anything on its change stack. An entry is <see langword="null"/> where
/// upstream would not answer that question - a fuzzy section inside a lookahead has to read past
/// <c>endpos</c>, a <c>\K</c> reports a start the attempt did not begin at, and a scan's second match
/// at one position cannot be reached at all - and the whole list is <see langword="null"/> where no
/// recorded match had a fuzzy half to ask about, which is every non-fuzzy row and every wave recorded
/// before S47. Never compared; only <see cref="ExpectedDivergences"/> reads it.
/// </param>
internal sealed record OracleRow(
    int Number,
    string Generator,
    string Pattern,
    int Flags,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NamedLists,
    string Subject,
    string Operation,
    IOracleOutcome Expected,
    (int Start, int End)? CodepointSpan,
    string? Template = null,
    int Count = 0,
    bool Partial = false,
    int? Pos = null,
    int? EndPos = null,
    bool SearchOnlyPartial = false,
    MatchesOutcome? AnchoredScan = null,
    MatchesOutcome? SubMatches = null,
    IOracleOutcome? BestmatchFree = null,
    IReadOnlyList<OracleFuzzy?>? LeakFreeFuzzy = null
);

/// <summary>What a matching operation answered.</summary>
internal interface IOracleOutcome
{
    /// <summary>The whole answer as one line, which is what the comparison is over.</summary>
    /// <returns>The rendered answer.</returns>
    string Describe();
}

/// <summary>The pattern did not match.</summary>
internal sealed record NoMatchOutcome : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() => "no match";
}

/// <summary>Upstream ran out of its deadline on this row, so there is no ground truth for it.</summary>
/// <param name="Seconds">The deadline upstream was given, which the recorder writes as a constant.</param>
/// <remarks>
/// <para>
/// Recorded by <c>tools/record-oracle.py</c> from S40a on, and never produced by this port: it is a
/// statement about upstream, not an answer either engine gave. Upstream can loop for ever on a row a
/// generator drew - <c>regex.search('.?x(?&gt;a(*SKIP)z)', 'xzxa')</c> on 2026.7.19 is one - and
/// before the deadline existed such a row killed the whole wave silently, with no file written at all.
/// </para>
/// <para>
/// A kind of its own rather than an <see cref="ErrorOutcome"/>, because upstream did not reject the
/// pattern: filing it as a rejection would score a port that answers as diverging and a port that
/// also hangs as agreeing, which is both halves of the comparison backwards. The row is skipped the
/// way an unsupported one is, and counted separately so a generator that starts drawing hanging
/// shapes shows up in the summary line instead of going quiet.
/// </para>
/// </remarks>
internal sealed record TimeoutOutcome(double Seconds) : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() => string.Create(CultureInfo.InvariantCulture, $"timed out after {Seconds}s");
}

/// <summary>Upstream hit a limit of the interpreter - memory, stack or range - so it never answered.</summary>
/// <remarks>
/// <para>
/// S43. The same "no ground truth" case as <see cref="TimeoutOutcome"/>, reached by running out of
/// heap rather than out of time, and treated identically for the identical reason: upstream did not
/// reject the pattern, so scoring this port's answer against it would call a port that answers
/// diverging and a port that also blows up agreeing.
/// </para>
/// <para>
/// The recorder used to abort the whole run on one of these. The composed fuzzy wave made them
/// routine - a repeat whose body can match empty, beside a fuzzy section, gives upstream nothing to
/// make progress on and it allocates until <c>MemoryError</c> in a second or two - and one
/// unanswerable row should not destroy every other row of a six-seed run. It is upstream's 551/554
/// resource-blowup family; see <c>exhausted</c> in <c>tools/record-oracle.py</c> for the measured
/// rows, including the ones that show no ranking flag is involved.
/// </para>
/// </remarks>
internal sealed record ResourceOutcome(string Exception) : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() => $"upstream ran out of resources ({Exception})";
}

/// <summary>This port compiled the pattern, and cannot match yet.</summary>
/// <remarks>
/// Not the same as knowing nothing, which is why it is not simply <see langword="null"/>. The port
/// has already answered one question - whether the input is acceptable - and on a row upstream
/// rejected, having accepted it *is* the divergence, decided before any matcher is consulted. That
/// is the shape S13 deliberately left open (five patterns upstream's <c>re_compile</c> rejects and
/// this port compiles), so it must not read as <c>unsupported</c> for the whole of phase 3.
/// </remarks>
internal sealed record CompiledButUnmatched : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() => "compiled; matching unported";
}

/// <summary>The operation was rejected.</summary>
/// <param name="Exception">
/// The exception's type name - Python's class name on the recorded side, .NET's on ours, which is
/// why <see cref="OracleComparer"/> compares these two by rule rather than by name.
/// </param>
/// <param name="Message">The message.</param>
/// <param name="Detail">
/// The whole exception, stack trace and all, when this port raised it. Never compared and never
/// recorded - it is what makes a divergence caused by a crash in our own engine actionable from
/// the report alone, instead of one line saying an unexpected exception happened somewhere.
/// </param>
/// <param name="WhileMatching">
/// Whether this port threw from the matching call rather than from compiling the pattern. Only
/// ever set on our own side, and the only thing that separates "this port rejected the input", as
/// upstream did, from "this port crashed on a pattern it should have rejected" - the exception
/// types overlap, so nothing else can tell them apart.
/// </param>
internal sealed record ErrorOutcome(string Exception, string Message, string? Detail = null, bool WhileMatching = false)
    : IOracleOutcome
{
    /// <summary>The outcome of an exception this port raised, stack trace and all.</summary>
    /// <param name="thrown">The exception.</param>
    /// <param name="whileMatching">Whether it was thrown from the matching call.</param>
    /// <returns>The outcome.</returns>
    public static ErrorOutcome From(Exception thrown, bool whileMatching = false)
    {
        ArgumentNullException.ThrowIfNull(thrown);
        return new ErrorOutcome(thrown.GetType().Name, thrown.Message, thrown.ToString(), whileMatching);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The phase is part of the rendering, so a report does not show two identical-looking lines
    /// for the two answers that <see cref="OracleComparer.Compare"/> treats differently.
    /// </remarks>
    public string Describe() => (WhileMatching ? "error while matching " : "error ") + Exception + ": " + Message;
}

/// <summary>A substitution ran, giving this text and this many replacements.</summary>
/// <param name="Text">The subject with the matches replaced.</param>
/// <param name="Count">
/// How many replacements were made - upstream's <c>subn</c> pair. Compared as well as the text
/// because they can disagree: a <c>count</c> that counted scans rather than replacements, or an
/// empty replacement of an empty match, both produce the right string and the wrong number.
/// </param>
internal sealed record SubOutcome(string Text, int Count) : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"sub {Count} {OracleWave.Printable(Text)}");
}

/// <summary>A whole scan: every match it produced, in the order it produced them.</summary>
/// <param name="Matches">The matches. Empty when the scan found none.</param>
/// <remarks>
/// The sequence is compared as one string, which is the point of the shape: a scan that finds the
/// right matches in the wrong order, or stops one match early, or repeats a zero-width match at one
/// position, agrees on every individual match and diverges here.
/// </remarks>
internal sealed record MatchesOutcome(IReadOnlyList<MatchOutcome> Matches) : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"matches {Matches.Count}")
        + (Matches.Count == 0 ? "" : " | " + string.Join(" || ", Matches.Select(static match => match.Describe())));
}

/// <summary>A split: the pieces of the subject, with the groups interleaved.</summary>
/// <param name="Parts">
/// The pieces, in order. A <see langword="null"/> entry is a capturing group that took no part in
/// that match, which upstream spells <c>None</c> and this port spells <see langword="null"/> - the
/// difference the built-in <c>Regex.Split</c> loses by omitting the entry entirely.
/// </param>
internal sealed record SplitOutcome(IReadOnlyList<string?> Parts) : IOracleOutcome
{
    /// <inheritdoc />
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"split {Parts.Count} ")
        + string.Join(" ", Parts.Select(static part => part is null ? "<null>" : OracleWave.Printable(part)));
}

/// <summary>The pattern matched, with one entry per group, group 0 being the whole match.</summary>
/// <param name="Groups">Every group, by ascending number.</param>
/// <param name="LastIndex">
/// Upstream's <c>lastindex</c> with <c>None</c> as <c>-1</c>, which is what
/// <c>Match.LastGroupNumber</c> reports. Compared because it is not derivable from
/// <paramref name="Groups"/>: it is the group that *closed* last, so <c>((a))</c> against
/// <c>'a'</c> gives 1 although groups 1 and 2 both succeed with the same span.
/// </param>
/// <param name="LastGroup">
/// Upstream's <c>lastgroup</c>, which is <c>Match.LastGroupName</c>. Also not derivable: it names
/// the last *named* group even when an unnamed one succeeded later.
/// </param>
/// <param name="Partial">
/// Upstream's <c>Match.partial</c>, which is <c>Match.PartialMatch</c>. Only a row asking for a
/// partial match can produce a true here, and the span alone does not carry it: a partial and a
/// complete match of the same text are the same span and different answers.
/// </param>
/// <param name="Fuzzy">
/// The errors the match used and where, or <see langword="null"/> when it used none. Not derivable
/// from anything else on the row: two fuzzy matches of the same span can have spent different
/// errors in different places, and the counts are the whole point of a fuzzy engine.
/// </param>
internal sealed record MatchOutcome(
    IReadOnlyList<OracleGroup> Groups,
    int LastIndex,
    string? LastGroup,
    bool Partial = false,
    OracleFuzzy? Fuzzy = null
) : IOracleOutcome
{
    /// <inheritdoc />
    /// <remarks>
    /// The partial marker is appended only when it is set, so every row recorded before S31 - and
    /// every row of every generator that does not ask for a partial - renders exactly as it did. The
    /// fuzzy half follows the same rule for S38.
    /// </remarks>
    public string Describe() =>
        "match "
        + string.Join(" ", Groups.Select(static group => group.Describe()))
        + string.Create(CultureInfo.InvariantCulture, $" last={LastIndex}/{LastGroup ?? "-"}")
        + (Partial ? " partial" : "")
        + (Fuzzy is null ? "" : " " + Fuzzy.Describe());
}

/// <summary>
/// How many errors a fuzzy match used and where. Upstream <c>Match.fuzzy_counts</c> and
/// <c>Match.fuzzy_changes</c>, in UTF-16 code units.
/// </summary>
/// <param name="Substitutions">How many characters were substituted.</param>
/// <param name="Insertions">How many were inserted.</param>
/// <param name="Deletions">How many were deleted.</param>
/// <param name="SubstitutionPositions">
/// Where each substitution was, in the order they were used, or <see langword="null"/> where
/// upstream has no answer to give - see <see cref="PositionsUnavailable"/>.
/// </param>
/// <param name="InsertionPositions">Where each insertion was, or <see langword="null"/>.</param>
/// <param name="DeletionPositions">
/// Where each deletion was, already shifted by one per earlier deletion - so these are positions in
/// a string with the missing characters put back, and may be past the end of the match. Or
/// <see langword="null"/>.
/// </param>
internal sealed record OracleFuzzy(
    int Substitutions,
    int Insertions,
    int Deletions,
    IReadOnlyList<int>? SubstitutionPositions,
    IReadOnlyList<int>? InsertionPositions,
    IReadOnlyList<int>? DeletionPositions
)
{
    /// <summary>Whether the match used no errors at all, which renders as nothing.</summary>
    internal bool IsExact => Substitutions == 0 && Insertions == 0 && Deletions == 0;

    /// <summary>
    /// Whether the change positions are missing on purpose, because upstream cannot be asked for
    /// them: <c>Match.fuzzy_changes</c> on a POSIX fuzzy match that spent an error is an access
    /// violation that kills the interpreter, where <c>Match.fuzzy_counts</c> on the same match
    /// answers correctly (ledger entry 9). All three lists are null together or none of them is.
    /// </summary>
    internal bool PositionsUnavailable => SubstitutionPositions is null;

    /// <summary>
    /// The S47 invariant: each change list holds exactly as many positions as its own count, because
    /// the counts and the positions are two views of one edit script. Vacuously true where there are
    /// no positions to count - see <see cref="PositionsUnavailable"/>.
    /// </summary>
    internal bool CountsAgreeWithPositions =>
        PositionsUnavailable
        || (
            Substitutions == SubstitutionPositions!.Count
            && Insertions == InsertionPositions!.Count
            && Deletions == DeletionPositions!.Count
        );

    /// <summary>Drops the change positions, leaving the counts, for comparison against a row upstream could not answer.</summary>
    /// <returns>The same counts with no positions.</returns>
    internal OracleFuzzy WithoutPositions() =>
        this with
        {
            SubstitutionPositions = null,
            InsertionPositions = null,
            DeletionPositions = null,
        };

    /// <summary>The fuzzy half as one token of a <see cref="MatchOutcome"/>'s line.</summary>
    /// <returns>The rendered counts and positions.</returns>
    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"fuzzy=({Substitutions},{Insertions},{Deletions})")
        + (
            PositionsUnavailable
                // Spelled out rather than rendered as three empty lists, so a divergence block on
                // such a row says why it carries no positions instead of looking like an engine that
                // spent errors nowhere.
                ? "[changes unavailable upstream]"
                : $"[s:{string.Join(",", SubstitutionPositions!)}]"
                    + $"[i:{string.Join(",", InsertionPositions!)}]"
                    + $"[d:{string.Join(",", DeletionPositions!)}]"
        );
}

/// <summary>One group's result.</summary>
/// <param name="Number">The group number.</param>
/// <param name="Success">Whether the group took part in the match.</param>
/// <param name="Index">The UTF-16 index of its last capture.</param>
/// <param name="Length">The UTF-16 length of its last capture.</param>
/// <param name="Captures">Every capture it made, oldest first.</param>
internal sealed record OracleGroup(int Number, bool Success, int Index, int Length, IReadOnlyList<OracleSpan> Captures)
{
    /// <summary>The group as one token of a <see cref="MatchOutcome"/>'s line.</summary>
    /// <returns>The rendered group.</returns>
    /// <remarks>
    /// A group that did not take part renders as <c>unset</c> and nothing else: upstream reports
    /// <c>(-1, -1)</c> there and .NET reports <c>(0, 0)</c>, so comparing the numbers would fail a
    /// correct port, and there is no capture list to compare either.
    /// </remarks>
    public string Describe() =>
        Success
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{Number}:({Index},{Length})[{string.Join(",", Captures.Select(static capture => capture.Describe()))}]"
            )
            : string.Create(CultureInfo.InvariantCulture, $"{Number}:unset");
}

/// <summary>One capture, in UTF-16 code units, as the public API reports it.</summary>
/// <param name="Index">Where it starts.</param>
/// <param name="Length">How long it is.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct OracleSpan(int Index, int Length)
{
    /// <summary>The span as one token of a group's rendering.</summary>
    /// <returns>The rendered span.</returns>
    public string Describe() => string.Create(CultureInfo.InvariantCulture, $"({Index},{Length})");
}

/// <summary>What comparing one row produced.</summary>
internal enum OracleVerdict
{
    /// <summary>Both engines gave the same answer.</summary>
    Agree,

    /// <summary>They gave different answers. Any one of these fails the run.</summary>
    Diverge,

    /// <summary>This port cannot answer yet. Informational: the engine lands slice by slice.</summary>
    Unsupported,

    /// <summary>
    /// They gave different answers, and <see cref="ExpectedDivergences"/> names the family, says
    /// which engine is right and points at the permanent test that pins it. Does not fail the run;
    /// always printed, with its id, so it is accounted for rather than hidden.
    /// </summary>
    Expected,

    /// <summary>
    /// Upstream ran out of its deadline, so there is nothing to compare against. Informational, and
    /// counted in the summary line rather than dropped: a generator that starts drawing rows
    /// upstream cannot answer has stopped testing what it claims to test, and the count is how
    /// anyone notices.
    /// </summary>
    Timeout,

    /// <summary>
    /// Upstream ran out of memory, stack or range, so there is nothing to compare against. Counted
    /// beside <see cref="Timeout"/> and for the same reason: it is how anyone notices that a
    /// generator has started drawing rows upstream cannot answer.
    /// </summary>
    Resource,
}
