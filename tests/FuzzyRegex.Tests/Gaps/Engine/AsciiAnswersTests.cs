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
    public void Equal_one_character_tests_share_one_table()
    {
        // D141: a leaf test's answers depend only on its predicate family, encoding and values, so
        // two such nodes, in one pattern or two, read the same table rather than each filling one.
        Node[] first = Tests(Build(@"(?i)k\w"));
        Node[] second = Tests(Build(@"(?i)k\w\w"));

        first.Should().HaveCount(2);
        second.Should().HaveCount(3);
        second[0].AsciiAnswers.Should().BeSameAs(first[0].AsciiAnswers);
        second[1].AsciiAnswers.Should().BeSameAs(first[1].AsciiAnswers);
        second[2].AsciiAnswers.Should().BeSameAs(first[1].AsciiAnswers);

        // A different encoding is a different key: KELVIN SIGN folds to 'k' under Unicode only.
        Node ascii = Tests(Build(@"(?a)(?i)k"))[0];
        ascii.AsciiAnswers.Should().NotBeSameAs(first[0].AsciiAnswers);

        static Node[] Tests(PatternObject pattern) =>
            [
                .. pattern
                    .NodeList.Where(static node => node.AsciiAnswers is not null)
                    .OrderBy(static node => node.Index),
            ];
    }

    // D141: each pair differs in exactly one field of the shared-table key and has different ASCII
    // answers, so dropping that field from the key would hand the second node the first's table.
    [Test]
    [Arguments(@"\p{Lu}", @"(?i)\p{Lu}", nameof(Opcode.Property), nameof(Opcode.PropertyIgn))] // the predicate family
    [Arguments("(?i)K", "(?ai)K", nameof(Opcode.CharacterIgn), nameof(Opcode.CharacterIgn))] // the encoding
    [Arguments("(?i)k", "(?i)s", nameof(Opcode.CharacterIgn), nameof(Opcode.CharacterIgn))] // the first value
    [Arguments("(?i)[a-f]", "(?i)[a-z]", nameof(Opcode.RangeIgn), nameof(Opcode.RangeIgn))] // the second value
    public void Each_key_field_separates_tables(string first, string second, string firstOp, string secondOp)
    {
        Node one = Build(first).NodeList.Single(static node => node.AsciiAnswers is not null);
        Node two = Build(second).NodeList.Single(static node => node.AsciiAnswers is not null);

        one.Op.ToString().Should().Be(firstOp);
        two.Op.ToString().Should().Be(secondOp);
        two.AsciiAnswers.Should().NotEqual(one.AsciiAnswers);
        foreach (Node node in new[] { one, two })
        {
            for (uint ch = 0; ch < 128; ch++)
            {
                bool kept = ((node.AsciiAnswers![(int)(ch >> 6)] >> (int)ch) & 1) != 0;
                kept.Should().Be(Matcher.MatchesOneUncached(node.Encoding, node, ch), $"{node.Op} U+{ch:X4}");
            }
        }
    }

    [Test]
    public void Threads_filling_through_one_cache_share_one_right_table_per_key()
    {
        // D141: the cache is shared by every compile in the process, so threads that miss on the
        // same key at the same moment race to publish. Each must end with the predicate's answers
        // and all must end with the one table that won.
        string[] corpus = [.. Corpus()];
        var cache = new AsciiAnswerCache();
        var nodes = new System.Collections.Concurrent.ConcurrentBag<Node>();
        int threads = Math.Max(4, Environment.ProcessorCount) * 2;
        using var start = new Barrier(threads);

        Parallel.For(
            0,
            threads,
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            thread =>
            {
                PatternObject[] patterns = [.. corpus.Select(Build)];
                foreach (Node node in patterns.SelectMany(static pattern => pattern.NodeList))
                {
                    node.AsciiAnswers = null;
                }

                start.SignalAndWait();
                foreach (List<Node> list in patterns.Select(static pattern => pattern.NodeList))
                {
                    Matcher.FillAsciiAnswers(list, cache);
                    foreach (Node node in list.Where(static node => node.AsciiAnswers is not null))
                    {
                        nodes.Add(node);
                    }
                }
            }
        );

        int leaves = 0;
        using (new AssertionScope())
        {
            foreach (Node node in nodes)
            {
                ulong[] fresh = new ulong[2];
                Matcher.ComputeAsciiAnswers(node, fresh);
                node.AsciiAnswers.Should().Equal(fresh, node.Op.ToString());
            }

            foreach (
                IGrouping<AsciiAnswerCache.Key?, Node> group in nodes
                    .GroupBy(AsciiAnswerCache.KeyOf)
                    .Where(static group => group.Key is not null)
            )
            {
                ++leaves;
                group.Select(static node => node.AsciiAnswers).Distinct().Should().ContainSingle();
            }
        }

        leaves.Should().Be(cache.Count).And.BeGreaterThan(20);
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
