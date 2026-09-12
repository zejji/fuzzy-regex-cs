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
        [.. lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select((line, index) => ParseRow(line, index + 1))];

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
            row.TryGetProperty("endpos", out JsonElement endpos) ? endpos.GetInt32() : null
        );
    }

    private static IOracleOutcome ReadOutcome(JsonElement outcome)
    {
        string kind = outcome.GetProperty("kind").GetString()!;
        return kind switch
        {
            "nomatch" => new NoMatchOutcome(),
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
                .. outcome.GetProperty("parts").EnumerateArray().Select(part => part.GetString()),
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
            match.TryGetProperty("partial", out JsonElement partial) && partial.GetBoolean()
        );

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
                property => property.Name,
                property => (IReadOnlyList<string>)[.. property.Value.EnumerateArray().Select(v => v.GetString()!)],
                StringComparer.Ordinal
            );

    /// <summary>Writes the run's verdict, and one block per divergence, to <see cref="ReportPath"/>.</summary>
    /// <param name="header">The wave's header, so the report says which oracle produced it.</param>
    /// <param name="rowCount">How many rows the wave held.</param>
    /// <param name="tally">How many rows fell into each verdict.</param>
    /// <param name="divergences">One rendered block per diverging row.</param>
    /// <returns>The single-line summary, which is also the first line of the report.</returns>
    public static string WriteReport(
        OracleHeader header,
        int rowCount,
        IReadOnlyDictionary<OracleVerdict, int> tally,
        IReadOnlyList<string> divergences
    )
    {
        string summary = string.Create(
            CultureInfo.InvariantCulture,
            $"agree {tally.GetValueOrDefault(OracleVerdict.Agree)}  "
                + $"unsupported {tally.GetValueOrDefault(OracleVerdict.Unsupported)}  "
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

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath, report.ToString());
        return summary;
    }

    /// <summary>Renders one diverging row: what was asked, what upstream said, what we said.</summary>
    /// <param name="row">The row that diverged.</param>
    /// <param name="actual">This port's answer, or <see langword="null"/> if it was unsupported.</param>
    /// <returns>The rendered block.</returns>
    public static string Describe(OracleRow row, IOracleOutcome? actual)
    {
        var block = new StringBuilder();
        block.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"DIVERGE row {row.Number} ({row.Generator}) {row.Operation} flags=0x{row.Flags:x}"
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
                        row.NamedLists.Select(entry =>
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
internal sealed record OracleRunSummary(
    IReadOnlyDictionary<OracleVerdict, int> Tally,
    IReadOnlyList<string> Divergences
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
    int? EndPos = null
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
        + (Matches.Count == 0 ? "" : " | " + string.Join(" || ", Matches.Select(match => match.Describe())));
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
        + string.Join(" ", Parts.Select(part => part is null ? "<null>" : OracleWave.Printable(part)));
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
internal sealed record MatchOutcome(
    IReadOnlyList<OracleGroup> Groups,
    int LastIndex,
    string? LastGroup,
    bool Partial = false
) : IOracleOutcome
{
    /// <inheritdoc />
    /// <remarks>
    /// The partial marker is appended only when it is set, so every row recorded before S31 - and
    /// every row of every generator that does not ask for a partial - renders exactly as it did.
    /// </remarks>
    public string Describe() =>
        "match "
        + string.Join(" ", Groups.Select(group => group.Describe()))
        + string.Create(CultureInfo.InvariantCulture, $" last={LastIndex}/{LastGroup ?? "-"}")
        + (Partial ? " partial" : "");
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
                $"{Number}:({Index},{Length})[{string.Join(",", Captures.Select(capture => capture.Describe()))}]"
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
}
