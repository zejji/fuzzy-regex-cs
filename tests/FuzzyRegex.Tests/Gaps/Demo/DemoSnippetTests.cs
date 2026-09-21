using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Tests.Conventions;
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
    /// <summary>A file under <c>demo/web/src</c>, found from the repository root.</summary>
    private static string WebSource(string relative) => RepoFile(Path.Combine("demo/web/src", relative));

    /// <summary>
    /// A file anywhere in the repository, found from the test assembly's location through
    /// <see cref="TestTree.RepositoryRoot"/>. Not <c>[CallerFilePath]</c>: CI builds with
    /// <c>ContinuousIntegrationBuild</c>, which rewrites source paths to <c>/_/...</c>, and the
    /// Native AOT job failed on 2026-09-20 looking for <c>/_/demo/web/src/lib/flags.ts</c>.
    /// </summary>
    private static string RepoFile(string relative)
    {
        string file = Path.Combine(TestTree.RepositoryRoot().FullName, relative);

        File.Exists(file).Should().BeTrue("{0} lives at {1}", relative, file);
        return File.ReadAllText(file);
    }

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
        // S74 moved the list to `lib/flags.ts`, which is where the checkbox panel, the help
        // sentences and the exclusive pairs also live; `snippet.ts` imports it from there.
        string source = WebSource("lib/flags.ts");
        System.Text.RegularExpressions.Match array = Regex.Match(
            source,
            @"export const FLAG_NAMES = \[(?<body>[^\]]*)\] as const;",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );

        array.Success.Should().BeTrue("flags.ts must declare FLAG_NAMES as a literal array");
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
    /// Every name from this library that the snippet prints is the name the library actually uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generator writes C# as text, so each type and member it names is a string literal in
    /// TypeScript, which has no <c>nameof</c>. Rename <c>EnumerateMatches</c> in the library and
    /// nothing goes red: the page keeps working, the panel keeps printing, and the code it hands a
    /// visitor does not compile. Two of these were pinned before S75 - the flag names and the
    /// timeout - and the other sixteen were not.
    /// </para>
    /// <para>
    /// The expectations are built with <c>nameof</c> and reflection, never typed out, so a rename
    /// that reaches the library breaks this file at COMPILE time rather than at assertion time: a
    /// <c>nameof</c> naming a member that no longer exists is an error, which is the property the
    /// owner asked for. <c>partial</c> is read from the method's own
    /// <see cref="System.Reflection.ParameterInfo"/>, because an argument name is not a member and
    /// <c>nameof</c> cannot reach it.
    /// </para>
    /// </remarks>
    [Test]
    public void The_snippet_names_the_library_the_library_names_itself()
    {
        Problems(SnippetApi()).Should().BeEmpty();
    }

    /// <summary>
    /// The pin above fails when a name in the record is wrong, which is the only thing that makes it
    /// worth having.
    /// </summary>
    /// <remarks>
    /// The doctored record is a COPY, per the slice's instruction: editing <c>snippet.ts</c> to prove
    /// the point would leave the repository one forgotten undo away from shipping the defect. The
    /// second case is the one a smaller test would miss - a key removed from the record is a name the
    /// template prints and nobody checks.
    /// </remarks>
    [Test]
    public void The_pin_fails_when_a_name_in_the_record_is_wrong()
    {
        Dictionary<string, string> doctored = new(SnippetApi(), StringComparer.Ordinal)
        {
            ["enumerateMatches"] = "EnumerateAllMatches",
        };

        using (new AssertionScope())
        {
            Problems(doctored).Should().ContainSingle().Which.Should().Contain("enumerateMatches");

            Dictionary<string, string> shortened = new(SnippetApi(), StringComparer.Ordinal);
            shortened.Remove("deletions");
            Problems(shortened).Should().ContainSingle().Which.Should().Contain("deletions");
        }
    }

    /// <summary>
    /// Every way the record and the real API disagree, one sentence each. A method rather than an
    /// assertion so that the test above can hand it a record with a fault in it.
    /// </summary>
    private static List<string> Problems(IReadOnlyDictionary<string, string> record)
    {
        Dictionary<string, string> expectations = Expected();
        List<string> problems = [];

        foreach ((string key, string expected) in expectations)
        {
            if (!record.TryGetValue(key, out string? printed))
            {
                problems.Add($"CSHARP_API has no '{key}'; the snippet prints '{expected}' with nothing pinning it");
            }
            else if (!string.Equals(printed, expected, StringComparison.Ordinal))
            {
                problems.Add($"CSHARP_API.{key} is '{printed}' and the library calls it '{expected}'");
            }
        }

        // The other direction: an entry nobody checks is an entry that can say anything at all.
        problems.AddRange(
            record
                .Keys.Where(key => !expectations.ContainsKey(key))
                .Select(static key => $"CSHARP_API.{key} names nothing this test knows how to check")
        );

        return problems;
    }

    /// <summary>
    /// What each entry of <c>CSHARP_API</c> has to be, from the library itself.
    /// </summary>
    /// <remarks>
    /// The keys are roles rather than spellings: <c>matchType</c> and <c>matchMethod</c> are the same
    /// word today, and a rename could move one without the other.
    /// </remarks>
    private static Dictionary<string, string> Expected() =>
        new(StringComparer.Ordinal)
        {
            ["packageId"] = PackageId(),
            ["namespace"] = typeof(FuzzyRegex).Namespace!,
            ["regexType"] = nameof(FuzzyRegex),
            ["optionsType"] = nameof(FuzzyRegexOptions),
            // Fully qualified: `using System.Text.RegularExpressions` is in force here and both
            // namespaces have a Match, so a bare `nameof(Match)` does not compile.
            ["matchType"] = nameof(Fuzzy.Text.RegularExpressions.Match),
            ["countsType"] = nameof(FuzzyCounts),
            ["enumerateMatches"] = nameof(FuzzyRegex.EnumerateMatches),
            ["matchMethod"] = nameof(FuzzyRegex.Match),
            ["replace"] = nameof(FuzzyRegex.Replace),
            ["partialParameter"] = PartialParameterName(),
            ["success"] = nameof(Fuzzy.Text.RegularExpressions.Match.Success),
            ["index"] = nameof(Fuzzy.Text.RegularExpressions.Match.Index),
            ["length"] = nameof(Fuzzy.Text.RegularExpressions.Match.Length),
            ["partialMatch"] = nameof(Fuzzy.Text.RegularExpressions.Match.PartialMatch),
            ["fuzzyCounts"] = nameof(Fuzzy.Text.RegularExpressions.Match.FuzzyCounts),
            ["substitutions"] = nameof(FuzzyCounts.Substitutions),
            ["insertions"] = nameof(FuzzyCounts.Insertions),
            ["deletions"] = nameof(FuzzyCounts.Deletions),
        };

    /// <summary>
    /// The name of the <c>bool</c> argument the snippet passes by name to the instance
    /// <c>FuzzyRegex.Match</c>.
    /// </summary>
    /// <remarks>
    /// A named argument is the one thing here that <c>nameof</c> cannot see: renaming a parameter
    /// keeps the library compiling and breaks every caller that named it, the snippet included. The
    /// method has exactly one <c>bool</c> parameter, and that is asserted rather than assumed.
    /// </remarks>
    private static string PartialParameterName()
    {
        System.Reflection.ParameterInfo[] flags =
        [
            .. typeof(FuzzyRegex)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(static method => string.Equals(method.Name, nameof(FuzzyRegex.Match), StringComparison.Ordinal))
                .SelectMany(static method => method.GetParameters())
                .Where(static parameter => parameter.ParameterType == typeof(bool)),
        ];

        flags.Should().ContainSingle("the snippet passes exactly one bool by name to the instance Match");
        return flags[0].Name!;
    }

    /// <summary>The NuGet package the snippet's first line tells a visitor to install.</summary>
    private static string PackageId()
    {
        System.Text.RegularExpressions.Match declared = Regex.Match(
            RepoFile("src/FuzzyRegex/FuzzyRegex.csproj"),
            @"<PackageId>(?<id>[^<]+)</PackageId>",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );

        declared.Success.Should().BeTrue("the project must declare a PackageId for the snippet to name");
        return declared.Groups["id"].Value;
    }

    /// <summary>
    /// The <c>CSHARP_API</c> record in <c>snippet.ts</c>, read as text.
    /// </summary>
    /// <remarks>
    /// One entry per line, <c>key: 'value',</c>, so a <c>//</c> comment line inside the record is
    /// skipped by not matching rather than by being stripped.
    /// </remarks>
    private static Dictionary<string, string> SnippetApi()
    {
        System.Text.RegularExpressions.Match record = Regex.Match(
            WebSource("lib/snippet.ts"),
            @"export const CSHARP_API = \{(?<body>.*?)\n\} as const;",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );

        record.Success.Should().BeTrue("snippet.ts must declare CSHARP_API as a literal record");

        Dictionary<string, string> read = Regex
            .Matches(
                record.Groups["body"].Value,
                @"(?m)^\s{4}(?<key>[A-Za-z]+):\s*'(?<value>[^']*)',",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)
            )
            .ToDictionary(
                static entry => entry.Groups["key"].Value,
                static entry => entry.Groups["value"].Value,
                StringComparer.Ordinal
            );

        // The guard on the extraction: a regex that matched nothing would make every assertion above
        // pass by finding no disagreement at all.
        read.Should().NotBeEmpty("the record has entries and this is how they are read");
        return read;
    }

    /// <summary>
    /// The sentence each flag's help button shows is the enum member's own <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// S74 put a help button beside every checkbox, and the words behind it are the library's, not a
    /// second description written for the page. They are COPIED into <c>flags.ts</c> rather than
    /// generated into <c>help.json</c> because they belong to a control: the page survives a missing
    /// <c>help.json</c> by leaving the help tab shut, which for a panel is a closed panel and for a
    /// <c>(?)</c> button would be a button that explains nothing.
    /// </para>
    /// <para>
    /// So the copy is pinned here instead, the way <c>FLAG_NAMES</c> and <c>caps.ts</c> are: edit a
    /// doc comment in <c>FuzzyRegexOptions.cs</c> without editing the page and this test is what
    /// says so.
    /// </para>
    /// </remarks>
    [Test]
    public void The_flag_help_is_the_librarys_own_words()
    {
        Dictionary<string, string> documented = EnumSummaries();
        Dictionary<string, string> shown = FlagHelp();

        shown
            .Keys.Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                Enum.GetNames<FuzzyRegexOptions>().Order(StringComparer.Ordinal),
                "every member has a help sentence, and the panel offers no flag the library lacks"
            );

        foreach (string name in Enum.GetNames<FuzzyRegexOptions>().Order(StringComparer.Ordinal))
        {
            documented.Should().ContainKey(name, "FuzzyRegexOptions.{0} needs a <summary> for the page to show", name);
            shown[name].Should().Be(documented[name], "the help for {0} is the enum's own summary", name);
        }
    }

    /// <summary>
    /// The panel reads a flags string the way <c>DemoEngine.TryParseFlags</c> reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The string is the state: the panel shows what it holds and the engine is given it unchanged,
    /// so a token one of them reads and the other does not is a flag in force with its box unticked
    /// and the shut row saying <c>none</c> - no error anywhere, because both halves think they
    /// agree. Found by S74's blind review with <c>f=IgnoreCase%0A</c>, which the engine trimmed and
    /// applied while the panel dropped it.
    /// </para>
    /// <para>
    /// Two halves to the contract, and both are read out of the two files rather than restated here:
    /// the separators, and the whitespace <c>StringSplitOptions.TrimEntries</c> takes off a token,
    /// which is <c>char.IsWhiteSpace</c> and not JavaScript's <c>String.trim</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void The_flags_panel_splits_and_trims_where_the_engine_does()
    {
        string engine = RepoFile("demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs");
        string panel = WebSource("lib/flags.ts");

        // `[',', ' ', '|', '\t']` in the one file, `/[, |\t]+/` in the other, both read as text.
        System.Text.RegularExpressions.Match declared = Regex.Match(
            engine,
            @"_flagSeparators = \[(?<body>[^\]]*)\];",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );
        declared.Success.Should().BeTrue("DemoEngine must declare _flagSeparators as a literal array");

        System.Text.RegularExpressions.Match split = Regex.Match(
            panel,
            @"const FLAG_SEPARATORS = /\[(?<body>[^\]]*)\]\+/;",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );
        split.Success.Should().BeTrue("flags.ts must declare FLAG_SEPARATORS as a literal class");

        IEnumerable<string> separators = Regex
            .Matches(declared.Groups["body"].Value, @"'(?<char>\\?.)'", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(static match => match.Groups["char"].Value)
            .Order(StringComparer.Ordinal);
        IEnumerable<string> classed = Regex
            .Matches(split.Groups["body"].Value, @"\\?.", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(static match => match.Value)
            .Order(StringComparer.Ordinal);

        classed.Should().Equal(separators, "the panel splits a flags string where the engine splits it");

        // And what TrimEntries takes off each token: every whitespace character .NET knows, spelt in
        // flags.ts as code points because `String.trim` is a different set.
        System.Text.RegularExpressions.Match trimmed = Regex.Match(
            panel,
            @"const DOTNET_WHITESPACE = new Set\((?<body>.*?)\n\);",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );
        trimmed.Success.Should().BeTrue("flags.ts must declare DOTNET_WHITESPACE as a literal set");

        IEnumerable<int> listed = Regex
            .Matches(trimmed.Groups["body"].Value, @"0x(?<code>[0-9a-f]+)", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(static match => Convert.ToInt32(match.Groups["code"].Value, 16))
            .Order();

        listed
            .Should()
            .Equal(
                Enumerable.Range(0, char.MaxValue + 1).Where(static code => char.IsWhiteSpace((char)code)),
                "a token the engine trims and the panel does not is a flag in force with its box unticked"
            );
    }

    /// <summary>
    /// Each <c>FuzzyRegexOptions</c> member's <c>&lt;summary&gt;</c>, as one line of plain text.
    /// </summary>
    /// <remarks>
    /// The doc comment lines immediately above a member, and no others: <c>[^\n]*</c> rather than
    /// <c>.</c> under <c>Singleline</c>, or the run stretches back through the enum's own type-level
    /// comment and every member reads as the first <c>&lt;summary&gt;</c> in the file.
    /// </remarks>
    private static Dictionary<string, string> EnumSummaries() =>
        Regex
            .Matches(
                RepoFile("src/FuzzyRegex/FuzzyRegexOptions.cs"),
                @"(?m)^(?<doc>(?:[ \t]*///[^\n]*\n)+)[ \t]*(?<name>[A-Za-z0-9]+)\s*=",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)
            )
            .Select(static member =>
                (Name: member.Groups["name"].Value, Summary: PlainSummary(member.Groups["doc"].Value))
            )
            .Where(static member => member.Summary is not null)
            .ToDictionary(static member => member.Name, static member => member.Summary!, StringComparer.Ordinal);

    /// <summary>One doc-comment block's <c>&lt;summary&gt;</c> as plain text, or null if it has none.</summary>
    private static string? PlainSummary(string doc)
    {
        System.Text.RegularExpressions.Match summary = Regex.Match(
            doc,
            @"<summary>(?<text>.*?)</summary>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );

        if (!summary.Success)
        {
            return null;
        }

        string text = Regex.Replace(
            summary.Groups["text"].Value,
            @"(?m)^\s*///\s?",
            " ",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );

        // A cref is shown as the member it names: the page has no links to follow.
        text = Regex.Replace(
            text,
            @"<see cref=""[^""]*?\.?(?<member>[A-Za-z0-9]+)""\s*/>",
            "${member}",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );
        text = Regex.Replace(text, @"</?(?:c|b|i|para)>", "", RegexOptions.None, TimeSpan.FromSeconds(5));
        return Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5)).Trim();
    }

    /// <summary>The sentences <c>flags.ts</c> shows, read out of its <c>FLAG_HELP</c> record.</summary>
    private static Dictionary<string, string> FlagHelp()
    {
        string source = WebSource("lib/flags.ts");
        System.Text.RegularExpressions.Match record = Regex.Match(
            source,
            @"export const FLAG_HELP: Record<FlagName, string> = \{(?<body>.*?)\n\};",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5)
        );

        record.Success.Should().BeTrue("flags.ts must declare FLAG_HELP as a literal record");

        return Regex
            .Matches(
                record.Groups["body"].Value,
                """(?<name>[A-Za-z0-9]+):\s*(?<quote>['"])(?<text>(?:[^'"\\]|\\.|(?!\k<quote>)['"])*)\k<quote>,""",
                RegexOptions.None,
                TimeSpan.FromSeconds(5)
            )
            .ToDictionary(
                static entry => entry.Groups["name"].Value,
                static entry => Unescaped(entry.Groups["text"].Value),
                StringComparer.Ordinal
            );
    }

    /// <summary>
    /// A TypeScript string literal's value, read the way JavaScript reads it.
    /// </summary>
    /// <remarks>
    /// Every escape, and not only the backslash and the quote: a sentence about <c>\b</c> written
    /// with one backslash instead of two is a backspace character on the page, and a reader that
    /// left both forms alone could not tell the two apart - it passed the pin either way (measured
    /// 2026-09-20, S74 blind pass over the copy-linter delta). JavaScript drops the backslash of an
    /// escape it does not know, so <c>\B</c> is <c>B</c>, and that is the default arm below.
    /// </remarks>
    private static string Unescaped(string text)
    {
        var value = new StringBuilder(text.Length);
        int index = 0;

        while (index < text.Length)
        {
            if (text[index] != '\\' || index + 1 == text.Length)
            {
                value.Append(text[index]);
                index++;
                continue;
            }

            char escape = text[index + 1];
            index += 2;

            if ((escape == 'u' || escape == 'x') && TryCodeUnit(text, index - 1, out char unit, out int width))
            {
                value.Append(unit);
                index += width;
                continue;
            }

            value.Append(
                escape switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    'b' => '\b',
                    'f' => '\f',
                    'v' => '\v',
                    '0' => '\0',
                    _ => escape,
                }
            );
        }

        return value.ToString();
    }

    /// <summary>The character a <c>\uXXXX</c> or <c>\xXX</c> escape names, if it is one.</summary>
    private static bool TryCodeUnit(string text, int index, out char unit, out int width)
    {
        width = text[index] == 'u' ? 4 : 2;
        unit = '\0';

        if (index + width >= text.Length)
        {
            return false;
        }

        if (
            !ushort.TryParse(
                text.AsSpan(index + 1, width),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out ushort code
            )
        )
        {
            return false;
        }

        unit = (char)code;
        return true;
    }

    /// <summary>
    /// The reader above reads an escape the way the browser does, so a typo in one cannot pass.
    /// </summary>
    [Test]
    public void The_flag_help_reader_reads_an_escape_the_way_javascript_does()
    {
        Unescaped(@"\\b").Should().Be(@"\b", "two backslashes are the character class the sentence is about");
        Unescaped(@"\b").Should().Be("\b", "one backslash is a backspace, which is what a typo would put on the page");
        Unescaped(@"\B").Should().Be("B", "JavaScript drops the backslash of an escape it does not know");
        Unescaped(@"\'").Should().Be("'", "the quote the literal is written in");
        Unescaped(@"\u00df").Should().Be("ß", "a code-unit escape names its character");
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
