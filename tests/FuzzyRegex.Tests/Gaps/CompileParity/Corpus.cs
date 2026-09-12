using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.CompileParity;

/// <summary>
/// The compile-parity corpus: upstream's own compiler output for every pattern its test suite
/// compiles, recorded by <c>tools/record-compile-corpus.py</c>.
/// </summary>
/// <remarks>
/// Phase 2 turns pattern text into bytecode and nothing else, so the ported suite - which matches
/// strings - cannot verify it. This fixture can, bit for bit, and it is the done-criterion for
/// every parser slice (S06).
/// </remarks>
internal static class Corpus
{
    // Derived, not spelled out: the resource id is the project's RootNamespace plus the file's
    // path, which is this type's namespace, so a folder rename cannot leave a stale literal.
    private static readonly string _resourceName = typeof(Corpus).Namespace + ".corpus.json";

    private static readonly Lazy<CorpusFile> _loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>One row per pattern upstream's suite compiles successfully.</summary>
    public static IEnumerable<CompileRow> Compiles() => _loaded.Value.Compiles;

    /// <summary>One row per pattern upstream's suite rejects with a parse error.</summary>
    public static IEnumerable<ErrorRow> Errors() => _loaded.Value.Errors;

    /// <summary>One row per replacement template upstream's suite compiles.</summary>
    public static IEnumerable<TemplateRow> Templates() => _loaded.Value.Templates;

    /// <summary>The <c>regex</c> version the fixture was recorded against.</summary>
    public static string RegexVersion => _loaded.Value.RegexVersion;

    /// <summary>The upstream commit the fixture was recorded against.</summary>
    public static string UpstreamCommit => _loaded.Value.UpstreamCommit;

    /// <summary>Upstream's <c>DEFAULT_VERSION</c> during the recording.</summary>
    public static int DefaultVersion => _loaded.Value.DefaultVersion;

    private static CorpusFile Load()
    {
        using Stream stream =
            typeof(Corpus).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException(
                $"the corpus fixture is not embedded as '{_resourceName}'. Available: "
                    + string.Join(", ", typeof(Corpus).Assembly.GetManifestResourceNames())
            );

        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        return new CorpusFile(
            root.GetProperty("regexVersion").GetString()!,
            root.GetProperty("upstreamCommit").GetString()!,
            root.GetProperty("defaultVersion").GetInt32(),
            [.. root.GetProperty("compiles").EnumerateArray().Select(ReadCompile)],
            [.. root.GetProperty("errors").EnumerateArray().Select(ReadError)],
            [.. root.GetProperty("templates").EnumerateArray().Select(ReadTemplate)]
        );
    }

    private static CompileRow ReadCompile(JsonElement row, int index) =>
        new(
            index,
            row.GetProperty("pattern").GetString()!,
            row.GetProperty("flags").GetInt32(),
            ReadNamedLists(row.GetProperty("namedLists")),
            row.GetProperty("resolvedFlags").GetInt32(),
            [.. row.GetProperty("code").EnumerateArray().Select(static c => c.GetUInt32())],
            ReadGroupIndex(row.GetProperty("groupIndex")),
            ReadNamedListSets(row.GetProperty("compiledNamedLists")),
            // Sets, not lists: each entry is a frozenset upstream, so the recorder had to invent an
            // order (sorted) to write it as JSON, and asserting on that invented order would fail a
            // correct port.
            [.. row.GetProperty("namedListIndexes").EnumerateArray().Select(ReadStringSet)],
            // Int64: an offset is a repeat's maximum width, which runs to UNLIMITED - 1.
            row.GetProperty("reqOffset").GetInt64(),
            [.. row.GetProperty("reqChars").EnumerateArray().Select(static c => c.GetInt32())],
            row.GetProperty("reqFlags").GetInt32(),
            row.GetProperty("groupCount").GetInt32()
        );

    private static ErrorRow ReadError(JsonElement row, int index) =>
        new(
            index,
            row.GetProperty("pattern").GetString()!,
            row.GetProperty("flags").GetInt32(),
            ReadNamedLists(row.GetProperty("namedLists")),
            row.GetProperty("exception").GetString()!,
            row.GetProperty("message").GetString()!,
            // null for a ValueError: only regex.error carries an offset into the pattern.
            row.GetProperty("position")
                is { ValueKind: JsonValueKind.Number } position
                ? position.GetInt32()
                : null
        );

    private static TemplateRow ReadTemplate(JsonElement row, int index) =>
        new(
            index,
            row.GetProperty("pattern").GetString()!,
            row.GetProperty("template").GetString()!,
            row.GetProperty("groupCount").GetInt32(),
            ReadGroupIndex(row.GetProperty("groupIndex")),
            // Heterogeneous by design: upstream's compiled template alternates group numbers with
            // literal runs (upstream/regex/_main.py lines 710-741).
            [
                .. row.GetProperty("compiled")
                    .EnumerateArray()
                    .Select(static item =>
                        item.ValueKind == JsonValueKind.Number ? (object)item.GetInt32() : item.GetString()!
                    ),
            ]
        );

    /// <summary>The caller's named lists: a list, because their order reaches the bytecode.</summary>
    private static ImmutableDictionary<string, IReadOnlyList<string>> ReadNamedLists(JsonElement element) =>
        element
            .EnumerateObject()
            .ToImmutableDictionary(
                static property => property.Name,
                static property =>
                    (IReadOnlyList<string>)[.. property.Value.EnumerateArray().Select(static v => v.GetString()!)],
                StringComparer.Ordinal
            );

    /// <summary>The compiled named lists: sets, because upstream stores each as a frozenset.</summary>
    private static ImmutableDictionary<string, IReadOnlySet<string>> ReadNamedListSets(JsonElement element) =>
        element
            .EnumerateObject()
            .ToImmutableDictionary(
                static property => property.Name,
                static property => ReadStringSet(property.Value),
                StringComparer.Ordinal
            );

    private static IReadOnlySet<string> ReadStringSet(JsonElement element) =>
        element.EnumerateArray().Select(static v => v.GetString()!).ToImmutableHashSet(StringComparer.Ordinal);

    private static ImmutableDictionary<string, int> ReadGroupIndex(JsonElement element) =>
        element
            .EnumerateObject()
            .ToImmutableDictionary(
                static property => property.Name,
                static property => property.Value.GetInt32(),
                StringComparer.Ordinal
            );
}

/// <summary>The whole fixture, as read from the embedded JSON.</summary>
internal sealed record CorpusFile(
    string RegexVersion,
    string UpstreamCommit,
    int DefaultVersion,
    IReadOnlyList<CompileRow> Compiles,
    IReadOnlyList<ErrorRow> Errors,
    IReadOnlyList<TemplateRow> Templates
);

/// <summary>A pattern upstream compiled, and everything it produced.</summary>
public sealed record CompileRow(
    int Index,
    string Pattern,
    int Flags,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NamedLists,
    int ResolvedFlags,
    IReadOnlyList<uint> Code,
    IReadOnlyDictionary<string, int> GroupIndex,
    IReadOnlyDictionary<string, IReadOnlySet<string>> CompiledNamedLists,
    IReadOnlyList<IReadOnlySet<string>> NamedListIndexes,
    long ReqOffset,
    IReadOnlyList<int> ReqChars,
    int ReqFlags,
    int GroupCount
)
{
    /// <inheritdoc />
    public override string ToString() => RowName.Format(Index, Pattern, Flags);
}

/// <summary>
/// A pattern upstream rejected, and the error it gave. <c>Exception</c> is upstream's Python
/// exception class - <c>error</c> for a parse error, <c>ValueError</c> for a flags conflict - and
/// <c>Position</c> is the offset into the pattern, null when the exception carries none, which is
/// every <c>ValueError</c>.
/// </summary>
public sealed record ErrorRow(
    int Index,
    string Pattern,
    int Flags,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NamedLists,
    string Exception,
    string Message,
    int? Position
)
{
    /// <inheritdoc />
    public override string ToString() => RowName.Format(Index, Pattern, Flags);
}

/// <summary>A replacement template upstream compiled, and what it produced.</summary>
public sealed record TemplateRow(
    int Index,
    string Pattern,
    string Template,
    int GroupCount,
    IReadOnlyDictionary<string, int> GroupIndex,
    IReadOnlyList<object> Compiled
)
{
    /// <inheritdoc />
    public override string ToString() => RowName.Format(Index, Template, flags: null);
}

/// <summary>Renders a corpus row as a test name.</summary>
/// <remarks>
/// Three constraints, all learned the hard way rather than guessed: the name goes into the TRX as
/// an XML attribute, so a raw <c>\x00</c> - which several patterns contain - would make the report
/// unparseable and take the ratchet with it; the parity baseline is keyed on the name, so it has
/// to be unique and stable across runs, hence the row index; and a slice's baseline diff is only
/// useful if the name says which pattern turned on, hence the pattern text.
/// </remarks>
internal static class RowName
{
    private const int _maxSubjectLength = 60;

    public static string Format(int index, string subject, int? flags)
    {
        var name = new StringBuilder(_maxSubjectLength + 24);
        name.Append('#').Append(index.ToString(CultureInfo.InvariantCulture)).Append(' ');
        Escape(subject, name);
        if (flags is not null)
        {
            name.Append(" flags=0x").Append(flags.Value.ToString("x", CultureInfo.InvariantCulture));
        }

        return name.ToString();
    }

    private static void Escape(string subject, StringBuilder into)
    {
        into.Append('\'');
        foreach (char c in subject.Length > _maxSubjectLength ? subject[.._maxSubjectLength] : subject)
        {
            if (c is '\\')
            {
                into.Append(@"\\");
            }
            else if (char.IsControl(c) || c > '~')
            {
                into.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                into.Append(c);
            }
        }

        into.Append('\'');
        if (subject.Length > _maxSubjectLength)
        {
            into.Append("...");
        }
    }
}
