using System.Text.Json;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>One entry in the demo's sidebar: a worked example the page can load into its inputs.</summary>
/// <param name="Title">The sidebar label, and the key the upstream answers are recorded under.</param>
/// <param name="Note">The one paragraph of prose that says why the example is worth trying.</param>
/// <param name="Pattern">The regular expression.</param>
/// <param name="Flags">Zero or more <see cref="FuzzyRegexOptions"/> member names.</param>
/// <param name="Subject">The text to search.</param>
public sealed record DemoExampleRow(string Title, string Note, string Pattern, string Flags, string Subject)
{
    /// <summary>The title, so a failing row names itself in the test report.</summary>
    public override string ToString() => Title;
}

/// <summary>
/// The demo's sidebar, read from the file the browser reads.
/// </summary>
/// <remarks>
/// <para>
/// <c>demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json</c> is embedded into this assembly rather than
/// copied beside it, following <c>Gaps/CompileParity/corpus.json</c>: a test that reads from the
/// working directory passes or fails depending on where the runner was started, and this suite is
/// also published and run natively (<c>tools/run-aot-tests.ps1</c>), where "beside the test" is a
/// different place again.
/// </para>
/// <para>
/// Parsed with <see cref="JsonDocument"/> and not with a deserialiser for the same reason the corpus
/// is: the reflection-based <c>JsonSerializer</c> overloads are trimmed away in the Native AOT leg.
/// </para>
/// </remarks>
internal static class DemoExamples
{
    private static readonly string _resourceName = typeof(DemoExamples).Namespace + ".examples.json";

    private static readonly Lazy<DemoExampleRow[]> _loaded = new(Load);

    /// <summary>Every example, in the order the sidebar shows them.</summary>
    public static IEnumerable<DemoExampleRow> All() => _loaded.Value;

    private static DemoExampleRow[] Load()
    {
        using Stream stream =
            typeof(DemoExamples).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException(
                $"the demo's examples are not embedded as '{_resourceName}'. Available: "
                    + string.Join(", ", typeof(DemoExamples).Assembly.GetManifestResourceNames())
            );

        using JsonDocument document = JsonDocument.Parse(stream);

        return
        [
            .. document
                .RootElement.EnumerateArray()
                .Select(static element => new DemoExampleRow(
                    element.GetProperty("title").GetString()!,
                    element.GetProperty("note").GetString()!,
                    element.GetProperty("pattern").GetString()!,
                    element.GetProperty("flags").GetString()!,
                    element.GetProperty("subject").GetString()!
                )),
        ];
    }
}
