using System.Globalization;
using System.Text.Json;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>
/// The oracle rows judged one at a time, read from <c>JudgedDivergences.jsonl</c>, which the
/// <c>judged-row</c> entry of <see cref="ExpectedDivergences"/> accounts for.
/// </summary>
/// <remarks>
/// <para>
/// A scheduled sweep draws fresh rows, and some are new rows of an upstream bug already judged and
/// pinned. Most keys are tables of individually judged rows, so they do not recognise them, and three
/// attempts on 2026-10-09 to recognise such rows automatically (every upstream-behaviour switch at
/// once; re-recording with (*SKIP) made (*PRUNE); re-recording with the lazy repeat made greedy) would
/// each also have claimed a port bug of the same shape. So the judgement stays with a person or an
/// agent, and is recorded as data, the way a sanitizer suppression file records one: the question,
/// both engines' answers, the cause and a minimal witness.
/// </para>
/// <para>
/// A row is accounted for only while the question is exactly the recorded one AND both answers
/// are exactly the recorded ones. If this port's answer changes, the row is reported again, so a
/// regression cannot hide behind an old judgement; if upstream's changes, the row is reported and
/// <c>record-oracle.py --recheck</c> names it too, since the rows are this entry's examples.
/// </para>
/// </remarks>
internal static class JudgedDivergences
{
    /// <summary>The manifest name the project file gives the data file.</summary>
    public const string ResourceName = "JudgedDivergences.jsonl";

    /// <summary>The data file's text, as embedded.</summary>
    public static string Text { get; } = ReadText();

    /// <summary>Every judged row, in file order.</summary>
    public static IReadOnlyList<JudgedRow> All { get; } = Parse(Text);

    /// <summary>Every judged row's recorded row, one per line: the entry's examples.</summary>
    public static string Example { get; } = string.Join('\n', All.Select(static judged => judged.RowLine));

    private static readonly Dictionary<string, JudgedRow> _byQuestion = All.ToDictionary(
        static judged => QuestionOf(judged.Row),
        StringComparer.Ordinal
    );

    /// <summary>
    /// Whether a divergence is a judged row: the same question, upstream's recorded answer and this
    /// port's judged answer, all three exactly.
    /// </summary>
    /// <param name="row">The row, carrying upstream's answer.</param>
    /// <param name="ours">This port's answer.</param>
    /// <returns><see langword="true"/> if the row is judged and neither answer has changed.</returns>
    public static bool Claims(OracleRow row, IOracleOutcome ours) =>
        _byQuestion.TryGetValue(QuestionOf(row), out JudgedRow? judged)
        && string.Equals(row.Expected.Describe(), judged.Upstream, StringComparison.Ordinal)
        && string.Equals(ours.Describe(), judged.Port, StringComparison.Ordinal);

    /// <summary>Everything a row asks, as one string.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The question.</returns>
    /// <remarks>
    /// Wider than <c>ExpectedDivergences.Question</c>, which leaves out the template, the count, the
    /// named lists and the default version: those tables hold no substitution or list rows, and a
    /// sweep can draw any of them.
    /// </remarks>
    public static string QuestionOf(OracleRow row)
    {
        string lists = string.Join(
            '\u0001',
            row.NamedLists.OrderBy(static list => list.Key, StringComparer.Ordinal)
                .Select(static list => list.Key + "=" + string.Join('\u0002', list.Value))
        );
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{row.Pattern}\0{row.Flags}\0{row.Subject}\0{row.Operation}\0{row.Partial}\0{row.Pos}\0{row.EndPos}\0{row.Template}\0{row.Count}\0{lists}\0{row.DefaultVersion}"
        );
    }

    /// <summary>The names the data file's header allows as a cause before they have a ledger entry.</summary>
    /// <returns>The names.</returns>
    public static IReadOnlySet<string> PendingCandidates()
    {
        const string marker = "// Pending candidates:";
        string line =
            Text.Split('\n')
                .Select(static l => l.TrimEnd('\r'))
                .FirstOrDefault(static l => l.StartsWith(marker, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{ResourceName} has no '{marker}' header line.");
        return line[marker.Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ReadText()
    {
        using Stream stream =
            typeof(JudgedDivergences).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"the test assembly does not embed {ResourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static List<JudgedRow> Parse(string text)
    {
        var rows = new List<JudgedRow>();
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement judged = document.RootElement;
            string rowLine = judged.GetProperty("row").GetRawText();
            rows.Add(
                new JudgedRow(
                    i + 1,
                    judged.GetProperty("cause").GetString()!,
                    judged.GetProperty("upstream").GetString()!,
                    judged.GetProperty("port").GetString()!,
                    judged.GetProperty("witness").GetString()!,
                    judged.GetProperty("judged").GetString()!,
                    judged.GetProperty("source").GetString()!,
                    rowLine,
                    OracleWave.ParseRows(rowLine)[0]
                )
            );
        }

        return rows;
    }
}

/// <summary>One line of <c>JudgedDivergences.jsonl</c>.</summary>
/// <param name="Line">The line number in the file, for messages.</param>
/// <param name="Cause">
/// One or more of <c>ledger N</c>, <c>DNN</c> or a pending candidate, joined by <c> + </c> where two
/// mechanisms stack on one row.
/// </param>
/// <param name="Upstream">Upstream's recorded answer, as <see cref="IOracleOutcome.Describe"/> renders it.</param>
/// <param name="Port">This port's judged answer, rendered the same way.</param>
/// <param name="Witness">The mechanism's minimal pattern and subject, with both engines' answers.</param>
/// <param name="Judged">The date of the judgement.</param>
/// <param name="Source">The sweep's seed and row, <c>seed/row</c>.</param>
/// <param name="RowLine">The recorded row, byte for byte as the recorder wrote it.</param>
/// <param name="Row">The recorded row, parsed.</param>
internal sealed record JudgedRow(
    int Line,
    string Cause,
    string Upstream,
    string Port,
    string Witness,
    string Judged,
    string Source,
    string RowLine,
    OracleRow Row
);
