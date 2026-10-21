using System.Xml.Linq;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// S68. Every SHIPPED row of <c>docs/DIVERGENCES.md</c> is either attached to the public members it
/// changes, through a <c>&lt;remarks&gt;</c> that links the row's <c>docs/COMPARISON.md</c> section, or
/// listed in <see cref="_noMember"/> with the reason no member fits. The check reads the generated
/// <c>FuzzyRegex.xml</c>, which is what IntelliSense and the NuGet package carry, so a note that
/// exists only in the source never counts. It extends S65's
/// <see cref="ComparisonCoversDivergencesTests"/>, whose row scan it reuses: a new SHIPPED row fails
/// here until someone decides where its note goes.
/// </summary>
public sealed class DivergenceRemarksTests
{
    /// <summary>
    /// Where COMPARISON.md is published: the package's own <c>RepositoryUrl</c>, read from the
    /// assembly's metadata so the address lives in one place (<c>src/FuzzyRegex/FuzzyRegex.csproj</c>).
    /// </summary>
    private static readonly string _comparisonUrl =
        typeof(FuzzyRegex)
            .Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(static a => string.Equals(a.Key, "RepositoryUrl", StringComparison.Ordinal))
            .Value + "/blob/main/docs/COMPARISON.md#";

    private const string _ns = "Fuzzy.Text.RegularExpressions.";

    private const string _mainConstructor =
        "M:"
        + _ns
        + "FuzzyRegex.#ctor(System.String,"
        + _ns
        + "FuzzyRegexOptions,System.TimeSpan,System.Collections.Generic.IReadOnlyDictionary{System.String,System.Collections.Generic.IReadOnlyCollection{System.String}},System.Int32)";

    private const string _namedListsConstructor =
        "M:"
        + _ns
        + "FuzzyRegex.#ctor(System.String,"
        + _ns
        + "FuzzyRegexOptions,System.Collections.Generic.IReadOnlyDictionary{System.String,System.Collections.Generic.IReadOnlyCollection{System.String}})";

    /// <summary>
    /// The public <c>EnumerateMatches</c> overloads. A plain member ID would also reach the internal
    /// overload that takes an <c>ArrayPool</c>, which IntelliSense never shows.
    /// </summary>
    private static readonly string[] _publicEnumerateMatches =
    [
        "M:" + _ns + "FuzzyRegex.EnumerateMatches(System.String,",
        "M:" + _ns + "FuzzyRegex.EnumerateMatches(System.ReadOnlySpan{System.Char},System.Nullable",
    ];

    /// <summary>
    /// Each SHIPPED row, by the heading S65 keys it on, and the doc-comment IDs of the members its
    /// note is on. An ID ending in a member name stands for every overload of it; an ID that
    /// already holds <c>(</c> stands for the overloads it is a prefix of.
    /// </summary>
    private static readonly (string Heading, string[] Members)[] _attached =
    [
        ("Version 1 is the default", [Option("Version1"), _mainConstructor, "P:" + _ns + "FuzzyRegex.Options"]),
        ("`(?V0)` really means version 0", [Option("Version0"), _mainConstructor]),
        (
            "The \"unterminated character set\" parse error names `FuzzyRegexOptions.Version0` and the `\\[` escape",
            [Option("Version0"), Option("Version1"), _mainConstructor]
        ),
        ("No `concurrent` parameter", [Type("FuzzyRegex")]),
        ("A `Match` may be read from any thread", [Type("Match"), Type("FuzzyRegex")]),
        ("A `CancellationToken` on every input-dependent method", [Type("FuzzyRegex"), "M:" + _ns + "Match.NextMatch"]),
        (
            "A per-call `timeout` on every input-dependent method",
            [Type("FuzzyRegex"), "M:" + _ns + "Match.NextMatch", "P:" + _ns + "FuzzyRegex.MatchTimeout"]
        ),
        (
            "`Match` means upstream's `search`",
            [Type("FuzzyRegex"), Method("FuzzyRegex.Match"), Method("FuzzyRegex.MatchAtStart")]
        ),
        (
            "`Regex`-shaped surface",
            [
                Type("Match"),
                Method("Match.Result"),
                Method("Match.ResultFormat"),
                "P:" + _ns + "Match.LastGroupNumber",
                "P:" + _ns + "Match.LastGroupName",
            ]
        ),
        (
            "Indices are UTF-16 code units",
            [Type("Capture"), Type("FuzzyRegex"), "P:" + _ns + "FuzzyRegexParseException.Offset"]
        ),
        ("Upstream's `pos`/`endpos` are reshaped to `beginning`/`length`", [Type("FuzzyRegex")]),
        (
            "`Split` spells \"no limit\" as `maxSplits = -1`",
            [
                "M:" + _ns + "FuzzyRegex.Split(System.String,System.Int32",
                "M:" + _ns + "FuzzyRegex.EnumerateSplits(System.String,System.Int32",
            ]
        ),
        (
            "`Replace`'s `count` is inverted the same way",
            [
                "M:" + _ns + "FuzzyRegex.Replace(System.String,System.String,System.Int32",
                "M:" + _ns + "FuzzyRegex.Replace(System.String," + _ns + "MatchEvaluator,System.Int32",
                "M:" + _ns + "FuzzyRegex.ReplaceFormat(System.String,System.String,System.Int32",
            ]
        ),
        ("There is no `findall`", [Method("FuzzyRegex.Matches"), .. _publicEnumerateMatches]),
        (
            "`Match.Groups` is an `IReadOnlyDictionary<string, Group>` as well as a list",
            ["P:" + _ns + "Match.Groups", Type("GroupCollection")]
        ),
        (
            "A lazy walk times each STEP, `Matches` included",
            [Method("FuzzyRegex.Matches"), .. _publicEnumerateMatches, Method("FuzzyRegex.EnumerateSplits")]
        ),
        (
            "`Split` returns `string?[]` and puts `null` where a capturing group did not take part",
            [Method("FuzzyRegex.Split"), Method("FuzzyRegex.EnumerateSplits")]
        ),
        (
            "Replacement templates speak upstream's language",
            [
                Method("Match.Result"),
                "M:" + _ns + "FuzzyRegex.Replace(System.String,System.String,System.Int32",
                "M:" + _ns + "FuzzyRegex.Replace(System.String,System.String,System.String,",
            ]
        ),
        ("Exception mapping", [Type("FuzzyRegexParseException")]),
        (
            "`Match.allcaptures`, `allspans`, `groupdict` and `capturesdict`",
            ["P:" + _ns + "Match.Groups", "P:" + _ns + "Group.Captures"]
        ),
        (
            "The \"unused keyword argument\" message interpolates the name as written",
            [_namedListsConstructor, _mainConstructor]
        ),
        ("Not ported at all", [Type("FuzzyRegex"), Type("Match")]),
        ("`(?e)` and `(?b)` rank candidates by fuzzy COST", [Option("BestMatch"), Option("EnhanceMatch")]),
        ("The Turkic `I` pairings are not applied by default", [Option("IgnoreCase")]),
        (
            "This port's search prefilters never change the slow path's answer",
            [Method("FuzzyRegex.Match"), .. _publicEnumerateMatches, _partial]
        ),
        ("A word or grapheme boundary decided at the end of the available text makes a partial match", [_partial]),
        ("A partial match is refused where the match failed on text the engine already held", [_partial]),
        ("`(?L)` works only when casing is not requested", [Type("FuzzyRegexOptions")]),
        (
            "A fuzzy recursive pattern whose calls fail is matched in milliseconds",
            ["P:" + _ns + "FuzzyRegex.MatchTimeout"]
        ),
        ("Reversed partial matches run out of text at the slice start", [_partial, Option("RightToLeft")]),
        (
            "Compile budget",
            [
                _mainConstructor,
                "F:" + _ns + "FuzzyRegex.DefaultMaxCompiledNodes",
                "P:" + _ns + "FuzzyRegex.MaxCompiledNodes",
            ]
        ),
        ("Nesting limit", [_mainConstructor]),
        (
            "A fuzzy deletion that finishes a full-case-folded string or backreference costs one edit",
            [Option("FullCase")]
        ),
        (
            "A full-folded backreference that ends half-way through a folding charges the rest as an edit",
            [Option("FullCase")]
        ),
        ("A retried fuzzy edit in a full-folded backreference steps past what it used up", [Option("FullCase")]),
        ("A fuzzy full-folded match can stop part-way into a folding", [Option("FullCase")]),
        (
            "A case-insensitive cased property under ASCII means the 52 ASCII letters",
            [Option("Ascii"), Option("IgnoreCase")]
        ),
        ("A scope that names no encoding keeps the one around it", [Option("Ascii")]),
        ("A POSIX class takes the scope's encoding", [Option("Ascii")]),
        ("A case-insensitive cased property answers the same bare and in a set", [Option("IgnoreCase")]),
        ("A case-insensitive set matches each member first, then combines them", [Option("IgnoreCase")]),
        ("An encoding named by positional flags inside a group replaces the one in force", [Option("Ascii")]),
        ("A scoped `(?a:...)` or `(?u:...)` answers exactly", [Option("Ascii"), Option("Unicode")]),
        (
            "A literal under a scoped `(?i:...)` is found in text that holds only its full case folding",
            [Option("IgnoreCase")]
        ),
        ("A lazy repeat finds a full-folded literal that starts at the repeat's last position", [Option("FullCase")]),
        ("The WORD flag's word boundary is Unicode's default one", [Option("Word")]),
        ("`\\X` matches the same grapheme cluster backwards as forwards", [Option("RightToLeft")]),
        (
            "A BESTMATCH or ENHANCEMATCH scan does not skip positions after an empty match",
            [Option("BestMatch"), Option("EnhanceMatch")]
        ),
        (
            "Under BESTMATCH and ENHANCEMATCH, `\\G` holds only where the search began",
            [Option("BestMatch"), Option("EnhanceMatch")]
        ),
        ("BESTMATCH counts only the errors a match contains", [Option("BestMatch"), Option("EnhanceMatch")]),
        (
            "BESTMATCH and ENHANCEMATCH never let their budget decide a negative lookaround or a conditional's test",
            [Option("BestMatch"), Option("EnhanceMatch")]
        ),
        (
            "BESTMATCH and ENHANCEMATCH answer a match the pattern can make",
            [Option("BestMatch"), Option("EnhanceMatch")]
        ),
        ("A run-out of text inside a lookaround's body no longer ends a partial call with a partial", [_partial]),
        ("A partial call reports a partial where upstream refuses a completion that exists", [_partial]),
        ("A partial search tries a start inside the text to come for a pattern with no first set", [_partial]),
        ("`\\b` over the empty text is a partial", [_partial]),
        ("A partial match reports a group that closes before a repeat's tail runs out of text", [_partial]),
        ("A negative lookahead around a lookahead that holds for every continuation reports no partial", [_partial]),
        (
            "A reversed partial search lets `$` hold before a final newline in the text to come",
            [_partial, Option("RightToLeft")]
        ),
        (
            "`BestMatch` and `EnhanceMatch` keep a fit that ends in trailing insertions",
            [Option("BestMatch"), Option("EnhanceMatch"), Method("FuzzyRegex.FullMatch")]
        ),
        ("A partial search's first-set check does not answer before the anchors it skips", [_partial]),
    ];

    /// <summary>
    /// The SHIPPED rows no public member governs: behaviour of a pattern construct, seen only in what
    /// a match returns, whose COMPARISON.md section names no member in its prose. Keyed like
    /// <see cref="_attached"/>.
    /// </summary>
    private static readonly (string Heading, string Reason)[] _noMember =
    [
        ("The Unicode data is version 18.0.0", "Unicode table data; no member selects it"),
        (
            "`\\X` keeps a conjunct linker with the next consonant, as Unicode 18.0.0's GB9c says",
            "pattern syntax: `\\X`"
        ),
        (
            "A digit's decimal value is derived from upstream's own tables",
            "internal table, by COMPARISON's own account"
        ),
        ("`\\N{...}` named sequences are not carried", "pattern syntax; only the parse error's text differs"),
        ("CPython's 4300-digit cap on `int(str)` is not ported", "pattern syntax; the pattern compiles here"),
        ("A group that can call itself before it has matched any text is refused", "pattern syntax: group calls"),
        (
            "A group call that would re-enter the same group at the same text position fails that PATH",
            "pattern syntax: group calls"
        ),
        (
            "In a branch reset, a group never takes a number another group in the same branch will use",
            "pattern syntax: branch reset"
        ),
        (
            "A search may open a fuzzy match with an inserted character at the position it started from",
            "fuzzy section semantics"
        ),
        ("A fuzzy section that is undone takes its error total with it", "fuzzy section semantics"),
        (
            "A fuzzy repeat takes an iteration that matches no text by deleting only when something needs it",
            "fuzzy section semantics"
        ),
        ("Inherited upstream bugs are fixed here", "a family of engine fixes, each in the ledger"),
        ("A fuzzy constraint that allows no errors limits the errors made inside it", "fuzzy constraint syntax"),
        ("A fuzzy item that matched exactly can still be deleted", "fuzzy section semantics"),
        ("A `(*SKIP)` acts when backtracking reaches it", "pattern syntax: backtracking verbs"),
        (
            "A verb, branch, group call or fuzzy section that starts every alternative stays in each one",
            "compiler behaviour on a pattern"
        ),
        (
            "A verb that backtracking reaches inside an unfinished atomic group or positive lookaround ends the attempt",
            "pattern syntax: backtracking verbs"
        ),
        (
            "A verb after a lazy leading repeat does not hide the starts inside the repeat's run",
            "pattern syntax: backtracking verbs"
        ),
        ("A verb inside a lookbehind acts in right-to-left order", "pattern syntax: backtracking verbs"),
        ("A `\\G` inside a fuzzy section is answered where upstream raises an error", "fuzzy section semantics"),
        ("A fuzzy run can edit `ß` or a ligature as one character", "fuzzy section semantics"),
        (
            "A lookaround that fails inside a fuzzy section can be passed by inserting a text character",
            "fuzzy section semantics"
        ),
        (
            "A fuzzy section's minimum error count can be met by a text character inserted after its last item",
            "fuzzy section semantics"
        ),
        ("Below a fuzzy section's minimum, an item that matched exactly is also tried", "fuzzy section semantics"),
        ("A fuzzy run can edit an expanding subject character as one character", "fuzzy section semantics"),
        ("A group call inside a lookaround leaves no capture behind", "pattern syntax: group calls"),
        ("A group call keeps none of the captures its body made", "pattern syntax: group calls"),
        (
            "A call to the whole pattern inside a pattern that is one fuzzy section returns to its caller",
            "pattern syntax: group calls"
        ),
        ("A verb that cuts through a fuzzy section closes it", "pattern syntax: backtracking verbs"),
        (
            "The calls inside a called group run with the call's direction and fuzziness too",
            "pattern syntax: group calls"
        ),
        (
            "A whole-pattern call that re-opens a fuzzy section where the same call is already open is refused",
            "pattern syntax: group calls"
        ),
        ("A call inside a lookaround in a fuzzy section runs its group exactly", "pattern syntax: group calls"),
        ("A call in a conditional's lookbehind test runs its group backwards", "pattern syntax: group calls"),
        (
            "A backwards copy of the whole pattern does not skip to the required string's forward end",
            "pattern syntax: group calls"
        ),
        ("A called copy adds nothing to the pattern's minimum width", "pattern syntax: group calls"),
        ("A called group's copy runs as the group written out at the call", "pattern syntax: group calls"),
        (
            "An alternation at the start of a negative condition's yes branch keeps all its alternatives",
            "pattern syntax: conditionals"
        ),
        ("A conditional's lookaround test is matched exactly inside a fuzzy section", "pattern syntax: conditionals"),
        ("A conditional whose negative test fails keeps the captures the test made", "pattern syntax: conditionals"),
        ("A DEFINE group with a no-branch is refused", "pattern syntax: conditionals"),
        ("A `\\K` inside a lookaround cannot make a scan or a search repeat itself", "pattern syntax: \\K"),
        (
            "A `\\K` inside a lookaround, a condition's test, an atomic group or a possessive repeat is undone",
            "pattern syntax: \\K"
        ),
        ("A `\\K` before a `(*PRUNE)` or `(*SKIP)` does not make a search skip start positions", "pattern syntax: \\K"),
        (
            "A search that reads a group after a guarded repeat finds the leftmost match",
            "engine behaviour on a pattern"
        ),
    ];

    /// <summary>The members COMPARISON.md's fuzzy worked examples are copied onto, verbatim.</summary>
    private static readonly string[] _withExamples =
    [
        Type("FuzzyRegex"),
        Option("BestMatch"),
        Option("EnhanceMatch"),
        _namedListsConstructor,
    ];

    private const string _partial = "P:" + _ns + "Match.PartialMatch";

    [Test]
    public void Every_SHIPPED_divergence_is_linked_from_the_members_it_changes()
    {
        string divergences = File.ReadAllText(DocPath("DIVERGENCES.md"));
        List<string> headingLines = ComparisonCoversDivergencesTests.ComparisonHeadingLines(
            File.ReadAllText(DocPath("COMPARISON.md"))
        );
        List<string> shipped = ComparisonCoversDivergencesTests.ShippedRowHeadings(divergences);
        shipped
            .Should()
            .HaveCountGreaterThan(
                100,
                "the row scan must find DIVERGENCES.md's SHIPPED rows (measured 101, 2026-10-18)"
            );

        // Every row is decided exactly once, and every decision names a row that exists.
        List<string> problems = [];
        foreach (string row in shipped)
        {
            int decisions =
                _attached.Count(a => row.StartsWith(a.Heading, StringComparison.Ordinal))
                + _noMember.Count(n => row.StartsWith(n.Heading, StringComparison.Ordinal));
            if (decisions != 1)
            {
                problems.Add(
                    $"row '{row}' is decided {decisions} times; attach it to a member or list it in _noMember"
                );
            }
        }

        problems.AddRange(
            _attached
                .Select(a => a.Heading)
                .Concat(_noMember.Select(n => n.Heading))
                .Where(h => !shipped.Exists(row => row.StartsWith(h, StringComparison.Ordinal)))
                .Select(h => $"'{h}' names no SHIPPED row")
        );

        List<(string Name, XElement Element)> entries = LoadDocEntries();
        int linksChecked = 0;
        foreach ((string heading, string[] members) in _attached)
        {
            string? row = shipped.Find(r => r.StartsWith(heading, StringComparison.Ordinal));
            string? anchor = row is null ? null : AnchorFor(row, headingLines);
            if (anchor is null)
            {
                problems.Add($"'{heading}' has no single COMPARISON.md heading to link");
                continue;
            }

            foreach (string member in members)
            {
                List<XElement> found = [.. entries.Where(e => Covers(member, e.Name)).Select(e => e.Element)];
                if (found.Count == 0)
                {
                    problems.Add($"'{member}' is in no doc entry");
                }

                foreach (XElement element in found)
                {
                    linksChecked++;
                    bool linked = element
                        .Elements("remarks")
                        .Descendants("see")
                        .Any(see =>
                            string.Equals(
                                (string?)see.Attribute("href"),
                                _comparisonUrl + anchor,
                                StringComparison.Ordinal
                            )
                        );
                    if (!linked)
                    {
                        problems.Add(
                            $"{(string)element.Attribute("name")!}: no <remarks> link to #{anchor} ('{heading}')"
                        );
                    }
                }
            }
        }

        // Measured 2026-10-18: 122 member entries checked. A broken member match finds fewer.
        linksChecked.Should().BeGreaterThan(120, "the member scan must actually reach the documented overloads");
        problems.Should().BeEmpty();
    }

    [Test]
    public void The_fuzzy_members_carry_COMPARISON_worked_examples_verbatim()
    {
        string comparison = File.ReadAllText(DocPath("COMPARISON.md")).ReplaceLineEndings("\n");
        HashSet<string> fences = [.. CSharpFences(comparison)];
        fences.Should().HaveCountGreaterThan(20, "the fence scan must find COMPARISON.md's C# examples");

        List<(string Name, XElement Element)> entries = LoadDocEntries();
        List<string> problems = [];
        int examples = 0;
        foreach (string member in _withExamples)
        {
            XElement? element = entries.Find(e => string.Equals(e.Name, member, StringComparison.Ordinal)).Element;
            List<XElement> codes = element is null ? [] : [.. element.Elements("example").Elements("code")];
            if (codes.Count == 0)
            {
                problems.Add($"{member}: no <example><code>");
            }

            foreach (XElement code in codes)
            {
                examples++;
                if (!fences.Contains(Normalise(code.Value)))
                {
                    problems.Add($"{member}: an <example> differs from every C# block in COMPARISON.md");
                }
            }
        }

        examples.Should().BeGreaterThan(8, "measured 9 example blocks, 2026-10-18");
        problems.Should().BeEmpty();
    }

    private static string Type(string name) => "T:" + _ns + name;

    private static string Option(string name) => "F:" + _ns + "FuzzyRegexOptions." + name;

    private static string Method(string name) => "M:" + _ns + name;

    /// <summary>Whether a doc-comment ID is one of the entries a spec entry stands for.</summary>
    /// <param name="spec">A full ID, a member ID standing for its overloads, or an ID prefix holding <c>(</c>.</param>
    /// <param name="name">A doc entry's ID.</param>
    /// <returns>Whether the entry is covered.</returns>
    private static bool Covers(string spec, string name) =>
        string.Equals(spec, name, StringComparison.Ordinal)
        || (
            spec.Contains('(', StringComparison.Ordinal)
            && !spec.EndsWith(')')
            && name.StartsWith(spec, StringComparison.Ordinal)
        )
        || (!spec.Contains('(', StringComparison.Ordinal) && name.StartsWith(spec + "(", StringComparison.Ordinal));

    /// <summary>
    /// The GitHub anchor of the one COMPARISON.md heading that holds <paramref name="heading"/>:
    /// lower case, everything but letters, digits, <c>_</c>, <c>-</c> and spaces dropped, spaces turned
    /// into hyphens. Checked 2026-10-18 against the ids GitHub rendered for this file (the contents
    /// API with <c>application/vnd.github.html</c>): 115 of 115 published headings agree.
    /// </summary>
    /// <param name="heading">The row heading, as S65 reads it.</param>
    /// <param name="headingLines">COMPARISON.md's heading lines.</param>
    /// <returns>The anchor, or <see langword="null"/> when no single heading holds the text.</returns>
    private static string? AnchorFor(string heading, List<string> headingLines)
    {
        List<string> hits = headingLines.FindAll(line => line.Contains(heading, StringComparison.Ordinal));
        if (hits.Count != 1)
        {
            return null;
        }

        string text = hits[0].TrimStart('#').Trim().ToLowerInvariant();
        return string.Concat(
            text.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-' or ' ').Select(c => c == ' ' ? '-' : c)
        );
    }

    /// <summary>Every <c>```csharp</c> fence in the markdown, normalised.</summary>
    /// <param name="markdown">COMPARISON.md with <c>\n</c> line endings.</param>
    /// <returns>The fences' bodies.</returns>
    private static IEnumerable<string> CSharpFences(string markdown)
    {
        List<string>? current = null;
        foreach (string line in markdown.Split('\n'))
        {
            if (line.StartsWith("```csharp", StringComparison.Ordinal))
            {
                current = [];
            }
            else if (line.StartsWith("```", StringComparison.Ordinal) && current is not null)
            {
                yield return Normalise(string.Join('\n', current));
                current = null;
            }
            else
            {
                current?.Add(line);
            }
        }
    }

    /// <summary>
    /// Lines trimmed and blank ends dropped, because the compiler writes a doc comment's code
    /// with its own indentation and the markdown has none.
    /// </summary>
    /// <param name="code">A code block.</param>
    /// <returns>The block in a form both sources share.</returns>
    private static string Normalise(string code) =>
        string.Join('\n', code.ReplaceLineEndings("\n").Split('\n').Select(static l => l.Trim())).Trim('\n');

    private static List<(string Name, XElement Element)> LoadDocEntries()
    {
        string xmlPath = Path.Combine(AppContext.BaseDirectory, "FuzzyRegex.xml");
        List<(string Name, XElement Element)> entries =
        [
            .. XDocument
                .Load(xmlPath)
                .Root!.Element("members")!
                .Elements("member")
                .Select(static m => ((string)m.Attribute("name")!, m)),
        ];
        entries.Should().HaveCountGreaterThan(1000, "the generated doc file must have loaded");
        return entries;
    }

    private static string DocPath(string fileName) =>
        Path.Combine(TestTree.RepositoryRoot().FullName, "docs", fileName);
}
