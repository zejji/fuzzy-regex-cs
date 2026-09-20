using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using FuzzyRegexDemo.Wasm;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>
/// The C# the demo prints, checked against the C# the demo runs (S73).
/// </summary>
/// <remarks>
/// <para>
/// The snippet panel hands a visitor code and says, in effect, "this is how the page got that
/// answer". Two halves of that claim can rot silently and neither shows up in the browser: the
/// numbers and names the generator prints are copies of the library's own, and the answer the
/// snippet produces is supposed to be the answer on the screen.
/// </para>
/// <para>
/// So the TypeScript is read as text and its literals parsed, the way
/// <see cref="DemoCapsTests"/> reads <c>caps.ts</c> and for the same reason: importing it would put
/// a JavaScript runtime in a suite that also runs published as Native AOT.
/// </para>
/// </remarks>
public sealed class DemoSnippetTests
{
    /// <summary>A file under <c>demo/web/src</c>, found from this source file's own path.</summary>
    private static string WebSource(string relative)
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "../../../.."));
        string file = Path.Combine(repoRoot, "demo/web/src", relative);

        File.Exists(file).Should().BeTrue("the page's {0} lives at {1}", relative, file);
        return File.ReadAllText(file);
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Test]
    public void The_snippets_timeout_is_the_engines_timeout()
    {
        // The snippet prints `TimeSpan.FromSeconds(2)` and a comment calling it the demo's own. Two
        // seconds is DemoEngine.MatchTimeout; if that moves, the snippet would go on telling
        // visitors the page gave every match a budget it no longer gives.
        // Fully qualified: this namespace has a Match of its own, which is the type the port ships.
        System.Text.RegularExpressions.Match declaration = Regex.Match(
            WebSource("lib/caps.ts"),
            @"export const MATCH_TIMEOUT_SECONDS = (?<value>\d+);",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );

        declaration.Success.Should().BeTrue("caps.ts must declare MATCH_TIMEOUT_SECONDS as a plain number");
        int.Parse(declaration.Groups["value"].Value, CultureInfo.InvariantCulture)
            .Should()
            .Be((int)DemoEngine.MatchTimeout.TotalSeconds);
    }

    [Test]
    public void The_snippet_knows_every_flag_the_library_has()
    {
        // The generator prints the enum's own spelling of whatever the visitor typed, because the
        // engine reads flag names case-insensitively and `FuzzyRegexOptions.bestmatch` does not
        // compile. A member added to the library and missing here would be printed as typed, and a
        // visitor who wrote it in lower case would copy code that does not build.
        string source = WebSource("lib/snippet.ts");
        System.Text.RegularExpressions.Match array = Regex.Match(
            source,
            @"export const FLAG_NAMES: readonly string\[\] = \[(?<body>[^\]]*)\];",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );

        array.Success.Should().BeTrue("snippet.ts must declare FLAG_NAMES as a literal array");
        IEnumerable<string> printed = Regex
            .Matches(array.Groups["body"].Value, @"'(?<name>[A-Za-z0-9]+)'", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(static match => match.Groups["name"].Value);

        // Sorted and compared with Equal, not BeEquivalentTo: the equivalency engine cannot run
        // under native AOT (AotAssertionConventionTests). The order the two lists are written in is
        // not the point - one holds every member the other does.
        printed
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(Enum.GetNames<FuzzyRegexOptions>().Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The page's default case, answered by the engine, matches what the emitted snippet printed
    /// when it was compiled and run.
    /// </summary>
    /// <remarks>
    /// The two spans below are not this port's own output copied back in: they were printed by
    /// <c>dotnet run</c> over the snippet the panel emits, by
    /// <c>tools/probes/demo-snippet-compiles.mjs</c> on 2026-09-19 -
    /// <c>3+6 s=0 i=1 d=1</c> and <c>17+6 s=2 i=0 d=0</c>. This test is what keeps the page's
    /// answer and that compiled snippet the same answer; without it the two drift apart and the
    /// panel quietly starts showing code that does something else.
    /// </remarks>
    [Test]
    public void The_default_cases_snippet_prints_what_the_page_prints()
    {
        string json = DemoEngine.Run(
            "(?:colour){e<=2}",
            flags: "",
            "the color of the collar",
            mode: "",
            replacement: "",
            namedLists: ""
        );

        using JsonDocument parsed = JsonDocument.Parse(json);
        IEnumerable<(int Index, int Length, int Substitutions, int Insertions, int Deletions)> found = parsed
            .RootElement.GetProperty("matches")
            .EnumerateArray()
            .Select(static match =>
                (
                    match.GetProperty("index").GetInt32(),
                    match.GetProperty("length").GetInt32(),
                    match.GetProperty("counts").GetProperty("substitutions").GetInt32(),
                    match.GetProperty("counts").GetProperty("insertions").GetInt32(),
                    match.GetProperty("counts").GetProperty("deletions").GetInt32()
                )
            );

        found.Should().Equal((3, 6, 0, 1, 1), (17, 6, 2, 0, 0));
    }
}
