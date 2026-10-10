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
/// <para>
/// A row upstream CRASHED on is judged the same way, with <see cref="CrashedMarker"/> as its upstream
/// answer and the crash evidence beside it. The owner's rule (2026-10-10) is that this port never
/// reproduces an upstream crash: on such a pattern it answers correctly or refuses the pattern when
/// compiling it. So a crash row pins this port's exact answer, and that answer may be a result or a
/// compile-time refusal but never an error raised while matching; <see cref="Parse"/> refuses the
/// line otherwise.
/// </para>
/// </remarks>
internal static class JudgedDivergences
{
    /// <summary>The manifest name the project file gives the data file.</summary>
    public const string ResourceName = "JudgedDivergences.jsonl";

    /// <summary>
    /// The upstream answer of a judged crash row. No <see cref="IOracleOutcome.Describe"/> text starts
    /// with <c>&lt;</c>, so it cannot stand for any answer upstream gave; the exit code is on the
    /// recorded row and in the crash evidence.
    /// </summary>
    public const string CrashedMarker = "<upstream crashed>";

    /// <summary>
    /// How every error this port raised while matching renders (<see cref="ErrorOutcome.Describe"/>),
    /// which a crash row may not pin.
    /// </summary>
    private const string _errorWhileMatching = "error while matching ";

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
    /// <remarks>
    /// On a crash row upstream's recorded answer is the crash itself, so the row must be recorded as
    /// one. No error raised while matching can equal the pinned port answer: <see cref="Parse"/> refuses
    /// a crash line whose port text starts the way every such error renders.
    /// </remarks>
    public static bool Claims(OracleRow row, IOracleOutcome ours) =>
        _byQuestion.TryGetValue(QuestionOf(row), out JudgedRow? judged)
        && (
            judged.Crash is null
                ? string.Equals(row.Expected.Describe(), judged.Upstream, StringComparison.Ordinal)
                : row.Expected is CrashedOutcome
        )
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

    /// <summary>Reads the data file's lines, refusing a crash line that breaks the rules above.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>Every judged row, in file order.</returns>
    /// <exception cref="InvalidDataException">A crash line is not a recorded crash, carries no evidence, or pins an error raised while matching.</exception>
    internal static List<JudgedRow> Parse(string text)
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
            var row = new JudgedRow(
                i + 1,
                judged.GetProperty("cause").GetString()!,
                judged.GetProperty("upstream").GetString()!,
                judged.GetProperty("port").GetString()!,
                judged.GetProperty("witness").GetString()!,
                judged.GetProperty("judged").GetString()!,
                judged.GetProperty("source").GetString()!,
                judged.TryGetProperty("crash", out JsonElement crash) ? crash.GetString() : null,
                rowLine,
                OracleWave.ParseRows(rowLine)[0]
            );
            if (CrashProblem(row) is { } problem)
            {
                throw new InvalidDataException($"{ResourceName} line {row.Line} ({row.Source}): {problem}");
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>What is wrong with a line's crash fields, or <see langword="null"/> if nothing is.</summary>
    /// <param name="judged">The line.</param>
    /// <returns>The problem.</returns>
    private static string? CrashProblem(JudgedRow judged)
    {
        bool marked = string.Equals(judged.Upstream, CrashedMarker, StringComparison.Ordinal);
        bool recorded = judged.Row.Expected is CrashedOutcome;
        if (!marked && judged.Crash is not null)
        {
            return "crash evidence on a line whose upstream is not " + CrashedMarker;
        }

        if (!marked)
        {
            return recorded ? $"the recorded row is a crash, so upstream must be {CrashedMarker}" : null;
        }

        if (judged.Row.Expected is not CrashedOutcome crashed)
        {
            return $"upstream is {CrashedMarker} but the recorded row is not a crash";
        }

        if (string.IsNullOrWhiteSpace(judged.Crash))
        {
            return "a crash line needs its crash evidence";
        }

        if (
            !judged.Crash.Contains(
                string.Create(CultureInfo.InvariantCulture, $"exit {crashed.ExitCode}"),
                StringComparison.Ordinal
            )
        )
        {
            return $"the crash evidence does not name the recorded exit code {crashed.ExitCode}";
        }

        return judged.Port.StartsWith(_errorWhileMatching, StringComparison.Ordinal)
            ? "a crash row pins an answer or a compile-time refusal, never an error raised while matching"
            : null;
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
/// <param name="Crash">
/// On a crash row only: the exit code and, where known, where upstream crashed and how it was reproduced.
/// </param>
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
    string? Crash,
    string RowLine,
    OracleRow Row
);
