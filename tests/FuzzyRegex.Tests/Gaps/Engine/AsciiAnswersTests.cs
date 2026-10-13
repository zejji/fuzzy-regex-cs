using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <see cref="Node.AsciiAnswers"/>, the ASCII answers a one-character node keeps so the matcher
/// need not run its predicate for an ASCII codepoint (S62): for every opcode that keeps them, under
/// every flag that changes a predicate's answer, each of the 128 bits must be what the predicate
/// says.
/// </summary>
public sealed class AsciiAnswersTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    // Each body runs plain, case-insensitive, case-insensitive with full folding, ASCII, and
    // backwards; the sets are version 1 so that '--', '&&' and '~~' build the four set kinds.
    private static readonly string[] _bodies =
    [
        "k",
        "K",
        "ſ",
        "ß",
        @"\w",
        @"\W",
        @"\d",
        @"\s",
        @"\p{Lu}",
        @"\p{Ll}",
        @"\p{scx=Latin}",
        @"\p{Greek}",
        "[a-z]",
        "[^a-z]",
        "[K-k]",
        "[à-ÿ]",
        "[a-z0-9_]",
        @"[^\w\d]",
        @"[\w--\d]",
        @"[\w&&[a-m]]",
        @"[\w~~[a-m]]",
        @"[[a-z]--[aeiou]]",
        @"[\p{Lu}x]",
        @"[\w\s--[\n]]",
    ];

    private static readonly string[] _flags =
    [
        "",
        "(?i)",
        "(?fi)",
        "(?a)",
        "(?ai)",
        "(?u)",
        "(?ui)",
        "(?r)",
        "(?ri)",
        "(?rfi)",
    ];

    [Test]
    public void Every_ascii_answer_is_what_the_nodes_predicate_says()
    {
        var opcodes = new HashSet<Opcode>();
        int nodesChecked = 0;

        using (new AssertionScope())
        {
            foreach (string flags in _flags)
            {
                foreach (string body in _bodies)
                {
                    string source = "(?V1)" + flags + body;
                    foreach (Node node in Build(source).NodeList)
                    {
                        if (node.AsciiAnswers is not { } answers)
                        {
                            Matcher.HasAsciiAnswers(node.Op, node.Encoding).Should().BeFalse(source);
                            continue;
                        }

                        opcodes.Add(node.Op);
                        ++nodesChecked;
                        for (uint ch = 0; ch < 128; ch++)
                        {
                            bool kept = ((answers[(int)(ch >> 6)] >> (int)ch) & 1) != 0;
                            kept.Should()
                                .Be(
                                    Matcher.MatchesOneUncached(node.Encoding, node, ch),
                                    $"{source} {node.Op} U+{ch:X4}"
                                );
                        }
                    }
                }
            }
        }

        // The grid must reach every opcode that keeps answers, or it is not testing them all.
        Opcode[] keeping =
        [
            .. Enum.GetValues<Opcode>().Where(static op => Matcher.HasAsciiAnswers(op, CaseEncoding.Unicode)),
        ];
        opcodes.Order().Should().Equal(keeping.Order());
        nodesChecked.Should().BeGreaterThan(200);
    }

    [Test]
    public void A_locale_node_keeps_no_ascii_answers()
    {
        Build(@"(?L)\w").NodeList.Should().NotContain(static node => node.AsciiAnswers != null);
    }

    [Test]
    public void Matching_an_ascii_character_agrees_with_matching_without_the_answers()
    {
        // The public surface, against the predicate: every ASCII character, every body and flag.
        using (new AssertionScope())
        {
            foreach (string flags in _flags)
            {
                foreach (string body in _bodies)
                {
                    string source = "(?V1)" + flags + body;
                    var regex = new FuzzyRegex(source);
                    // A full-folded 'ß' builds a string, not a one-character node: skipped.
                    if (Build(source).NodeList.FirstOrDefault(static node => node.AsciiAnswers != null) is not { } test)
                    {
                        continue;
                    }

                    for (char ch = '\0'; ch < (char)128; ch++)
                    {
                        bool expected = Matcher.MatchesOneUncached(test.Encoding, test, ch) == test.Match;
                        regex.IsMatch(ch.ToString()).Should().Be(expected, $"{source} U+{(int)ch:X4}");
                    }
                }
            }
        }
    }

    private static PatternObject Build(string source) =>
        PatternObject.Compile(
            PatternCompiler.Compile(source, 0, _noNamedLists, PatternCompiler.DefaultVersion),
            source
        );
}
