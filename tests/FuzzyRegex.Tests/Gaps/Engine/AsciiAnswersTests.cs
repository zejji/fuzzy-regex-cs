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
        // D141: KELVIN SIGN and LONG S fold to ASCII letters under Unicode only.
        "K",
        "s",
        "S",
        "[k]",
        "[ks]",
        @"[\w\d]",
        @"[\w\s]+",
        @"[[\p{L}--\p{Lu}]&&[a-z]]",
        @"[\p{Nd}~~[0-5]]",
        @"[^[^a-z]]",
        @"[[a-f]&&[[d-z]--[x]]]",
        @"\p{Lt}",
        @"\P{L}",
        @"\p{Uppercase}",
        @"\p{Lowercase}",
        @"[\p{Uppercase}\p{Lowercase}]",
    ];

    // Patterns of more than one character, for the tests that read nodes rather than match.
    private static readonly string[] _sequences = [@"\w+@\w+\.com", "(?:k|K|s)+[0-9]{2}", @"[\w\s]+x[\w\s]"];

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
        "(?L)",
        "(?ra)",
    ];

    [Test]
    public void Every_ascii_answer_is_what_the_nodes_predicate_says()
    {
        var opcodes = new HashSet<Opcode>();
        int nodesChecked = 0;
        int patterns = 0;

        using (new AssertionScope())
        {
            foreach (string source in Corpus())
            {
                ++patterns;
                PatternObject pattern = Build(source);
                bool[] isMember = SetMembers(pattern);
                foreach (Node node in pattern.NodeList)
                {
                    if (node.AsciiAnswers is not { } answers)
                    {
                        // D141: a set member is asked by its set, never on its own, so it keeps none.
                        (isMember[node.Index] || !Matcher.HasAsciiAnswers(node.Op, node.Encoding))
                            .Should()
                            .BeTrue($"{source} {node.Op}");
                        continue;
                    }

                    opcodes.Add(node.Op);
                    ++nodesChecked;
                    for (uint ch = 0; ch < 128; ch++)
                    {
                        bool kept = ((answers[(int)(ch >> 6)] >> (int)ch) & 1) != 0;
                        kept.Should()
                            .Be(Matcher.MatchesOneUncached(node.Encoding, node, ch), $"{source} {node.Op} U+{ch:X4}");
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
        patterns.Should().BeGreaterThanOrEqualTo(200);
        nodesChecked.Should().BeGreaterThan(200);
    }

    [Test]
    public void A_set_member_keeps_no_ascii_answers()
    {
        // D141: the members are answered by MatchesMember through their set, which is the node that
        // keeps the answers, so filling theirs was compile time spent on bits nothing reads.
        int eligible = 0;

        using (new AssertionScope())
        {
            foreach (string source in Corpus())
            {
                PatternObject pattern = Build(source);
                bool[] isMember = SetMembers(pattern);
                foreach (
                    Node member in pattern.NodeList.Where(node =>
                        isMember[node.Index] && Matcher.HasAsciiAnswers(node.Op, node.Encoding)
                    )
                )
                {
                    ++eligible;
                    member.AsciiAnswers.Should().BeNull($"{source} {member.Op}");
                }
            }
        }

        // Members whose opcode would otherwise keep answers (\w, \p{..}, nested sets) are reached.
        eligible.Should().BeGreaterThan(100);
    }

    [Test]
    public void Every_property_node_reads_the_generated_table()
    {
        // D141: a property code with no slot in AsciiPropertyAnswers would still be answered right,
        // by the predicate, but would silently lose the table. Every code the parser builds must
        // have one.
        int properties = 0;

        using (new AssertionScope())
        {
            foreach (string source in Corpus().Concat(_propertySources))
            {
                foreach (
                    Node node in Build(source)
                        .NodeList.Where(static node =>
                            node.Op
                                is Opcode.Property
                                    or Opcode.PropertyRev
                                    or Opcode.PropertyIgn
                                    or Opcode.PropertyIgnRev
                        )
                )
                {
                    ++properties;
                    AsciiAnswers.Shared(node).Should().NotBeNull($"{source} 0x{node.Values[0]:X8}");
                }
            }
        }

        properties.Should().BeGreaterThan(100);
    }

    [Test]
    public void Nodes_of_one_property_share_one_table_array()
    {
        // D141 (A2): a property's table is one array per distinct answer set for the process, so
        // compiling \w does not allocate one. The plain and case-insensitive forms of \p{Lu} differ.
        Node[] words = Tables(Build(@"\w+@\w+"));
        Node plain = Tables(Build(@"\p{Lu}"))[0];
        Node ignoreCase = Tables(Build(@"(?i)\p{Lu}"))[0];

        // Each \w is a repeat body and its optimiser test node, so there are more than two.
        words.Should().HaveCountGreaterThanOrEqualTo(2);
        words.Should().OnlyContain(node => ReferenceEquals(node.AsciiAnswers, words[0].AsciiAnswers));
        ignoreCase.AsciiAnswers.Should().NotBeSameAs(plain.AsciiAnswers);
        ignoreCase.AsciiAnswers.Should().NotEqual(plain.AsciiAnswers);

        static Node[] Tables(PatternObject pattern) =>
            [
                .. pattern
                    .NodeList.Where(static node => node.AsciiAnswers is not null)
                    .OrderBy(static node => node.Index),
            ];
    }

    // Property spellings beyond the corpus: POSIX classes, binary properties, scripts, blocks and
    // negated forms, each a different path through ParseFunctions.LookupProperty.
    private static readonly string[] _propertySources =
    [
        "[[:alpha:]]x",
        "[[:^digit:]]x",
        @"\p{Alphabetic}",
        @"\P{Alphabetic}",
        @"\p{Script=Han}",
        @"\p{InBasicLatin}",
        @"\p{Blk=Greek}",
        @"\p{IsLatin}",
        @"\p{Any}",
        @"\p{Assigned}",
        @"\p{Hex_Digit}",
        @"(?i)\p{Lt}",
        @"(?a)\w",
        @"(?u:\d)",
    ];

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

    private static IEnumerable<string> Corpus()
    {
        foreach (string flags in _flags)
        {
            foreach (string body in _bodies.Concat(_sequences))
            {
                yield return "(?V1)" + flags + body;
            }
        }
    }

    private static bool[] SetMembers(PatternObject pattern)
    {
        bool[] members = new bool[pattern.NodeList.Count];
        foreach (
            Node set in pattern.NodeList.Where(static node => node.Op is >= Opcode.SetDiff and <= Opcode.SetUnionRev)
        )
        {
            for (Node? member = set.Next2.Node; member is not null; member = member.Next1.Node)
            {
                members[member.Index] = true;
            }
        }

        return members;
    }

    private static PatternObject Build(string source) =>
        PatternObject.Compile(
            PatternCompiler.Compile(source, 0, _noNamedLists, PatternCompiler.DefaultVersion),
            source
        );
}
