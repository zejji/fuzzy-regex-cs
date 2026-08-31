using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Tests.Gaps.CompileParity;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Drives <see cref="PatternObject.Compile"/> - the port of upstream's <c>re_compile</c> - over
/// every row of the compile-parity corpus, and pins the patterns whose code list upstream's C
/// compiler refuses.
/// </summary>
/// <remarks>
/// <para>
/// A node graph is observable only through matching, which does not exist until S16. What is
/// observable now is accept versus reject, and the handful of counts the compiler records on its
/// way through: how many groups, which of them are named, how many repeats. That is what these
/// tests hold.
/// </para>
/// <para>
/// Upstream compiled every one of the corpus's compile rows all the way to nodes - the recorder
/// intercepts <c>_regex.compile</c>, and a row only reaches the file if that call returned - so
/// "every row builds" is upstream's own verdict, not an assumption.
/// </para>
/// </remarks>
public sealed class NodeGraphTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    /// <summary>
    /// The five patterns whose bytecode this port produces exactly as upstream does and whose node
    /// build upstream then refuses. Each attaches a fuzzy test to a preceding item, and a fuzzy
    /// test has to match exactly one character: upstream's parser checks only the syntactic shape,
    /// and its C compiler rejects the opcode.
    /// </summary>
    /// <remarks>
    /// Measured against <c>regex</c> 2026.7.19 on 2026-08-31 (<c>.scratch/s15_five.py</c>):
    /// <code>
    /// 'a{e&lt;=1:\\X}'      RuntimeError: invalid RE code
    /// 'a{e&lt;=1:\\b}'      RuntimeError: invalid RE code
    /// 'a{e&lt;=1:\\A}'      RuntimeError: invalid RE code
    /// 'a{e&lt;=1:\\Z}'      RuntimeError: invalid RE code
    /// 'a{e&lt;=1:\\L&lt;a&gt;}'  RuntimeError: invalid RE code
    /// </code>
    /// </remarks>
    /// <returns>The patterns.</returns>
    public static IEnumerable<string> RejectedFuzzyTests() =>
        [@"a{e<=1:\X}", @"a{e<=1:\b}", @"a{e<=1:\A}", @"a{e<=1:\Z}"];

    [Test]
    [MethodDataSource(typeof(Corpus), nameof(Corpus.Compiles))]
    public void Builds_a_node_graph_for_every_corpus_row(CompileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        CompiledPattern compiled = PatternCompiler.Compile(
            row.Pattern,
            row.Flags,
            row.NamedLists,
            Corpus.DefaultVersion
        );

        PatternObject pattern = PatternObject.Compile(compiled);

        using (new AssertionScope())
        {
            pattern.StartNode.Should().NotBeNull("upstream built a graph for this row");
            pattern.StartTest.Should().NotBeNull();
            pattern.NodeList.Should().NotBeEmpty();
            pattern.PublicGroupCount.Should().Be(row.GroupCount);

            // skip_one_way_branches retargets every pointer to a branch with a single exit and
            // discard_unused_nodes then drops it, so no such node can survive into the graph. This
            // is the one structural property of the optimiser that holds for every pattern.
            pattern
                .NodeList.Should()
                .NotContain(
                    node => node.Op == Opcode.Branch && node.Next2.Node == null,
                    "a one-way branch exists only to give the builders something to attach to"
                );
        }
    }

    [Test]
    [MethodDataSource(nameof(RejectedFuzzyTests))]
    public void Rejects_a_fuzzy_test_that_is_not_one_character(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern);

        // NotSupportedException, not FuzzyRegexParseException: upstream raises RuntimeError here
        // and not its own `error`, and this port reserves FuzzyRegexParseException for upstream's
        // `error` - the rule UpstreamInternalErrorTests already follows. DECISIONS 2026-08-31.
        compile.Should().Throw<NotSupportedException>().WithMessage("invalid RE code");
    }

    [Test]
    public void Rejects_a_named_list_as_a_fuzzy_test()
    {
        Dictionary<string, IReadOnlyCollection<string>> namedLists = new(StringComparer.Ordinal) { ["a"] = ["x"] };

        Action compile = () => _ = new FuzzyRegex(@"a{e<=1:\L<a>}", FuzzyRegexOptions.None, namedLists);

        compile.Should().Throw<NotSupportedException>().WithMessage("invalid RE code");
    }

    [Test]
    public void Records_the_groups_and_which_of_them_are_named()
    {
        PatternObject pattern = Build("(a)(?<b>c)(d)");

        using (new AssertionScope())
        {
            pattern.PublicGroupCount.Should().Be(3);
            pattern.TrueGroupCount.Should().Be(3);
            pattern.VisibleCaptureCount.Should().Be(3);
            pattern.GroupInfoList.Select(info => info.HasName).Should().Equal(false, true, false);

            // Every group closed, in order, so group_end_index counted three closures.
            pattern.GroupEndIndex.Should().Be(3);
            pattern.GroupInfoList.Select(info => info.EndIndex).Should().Equal(1L, 2L, 3L);
        }
    }

    [Test]
    public void Records_a_group_that_is_referenced()
    {
        PatternObject pattern = Build(@"(a)\1");

        pattern.GroupInfoList[0].Referenced.Should().BeTrue();
    }

    [Test]
    public void A_capture_hidden_in_a_define_is_not_visible()
    {
        // (?(DEFINE)...) compiles to a GROUP_EXISTS on group 0, which build_GROUP_EXISTS enters
        // with in_define set, so build_GROUP does not count the capture inside it.
        PatternObject pattern = Build("(?(DEFINE)(?<x>a))(?&x)");

        using (new AssertionScope())
        {
            pattern.PublicGroupCount.Should().Be(1);
            pattern.VisibleCaptureCount.Should().Be(0);
        }
    }

    [Test]
    [Arguments("a", 0)]
    [Arguments("a*", 1)]
    [Arguments("a*b*", 2)]
    [Arguments("(?:ab)*", 1)]
    [Arguments("(?:a*)*", 2)]
    public void Records_one_repeat_per_repeated_sequence(string source, int repeats)
    {
        PatternObject pattern = Build(source);

        using (new AssertionScope())
        {
            pattern.RepeatCount.Should().Be(repeats);
            pattern.RepeatInfoList.Should().HaveCount(repeats);
        }
    }

    [Test]
    public void Marks_the_inner_repeat_of_a_nested_repeat()
    {
        // record_repeat sets RE_STATUS_INNER when repeat_depth is above zero, and build_REPEAT
        // raises the depth for the body it compiles. The outer repeat is index 0.
        PatternObject pattern = Build("(?:a*)*");

        using (new AssertionScope())
        {
            (pattern.RepeatInfoList[0].Status & NodeStatus.Inner).Should().Be(0);
            (pattern.RepeatInfoList[1].Status & NodeStatus.Inner).Should().Be(NodeStatus.Inner);
        }
    }

    [Test]
    public void A_repeat_of_one_character_becomes_a_repeat_one()
    {
        // sequence_matches_one turns GREEDY_REPEAT into GREEDY_REPEAT_ONE; a two-character body
        // cannot take that path.
        using (new AssertionScope())
        {
            Build("a*").NodeList.Select(node => node.Op).Should().Contain(Opcode.GreedyRepeatOne);
            Build("(?:ab)*").NodeList.Select(node => node.Op).Should().Contain(Opcode.GreedyRepeat);
        }
    }

    [Test]
    public void A_fuzzy_pattern_is_marked_fuzzy_and_counted()
    {
        PatternObject pattern = Build("(?:cat){e<=1}");

        using (new AssertionScope())
        {
            pattern.IsFuzzy.Should().BeTrue();
            pattern.FuzzyCount.Should().Be(1);
            Build("cat").IsFuzzy.Should().BeFalse();
        }
    }

    [Test]
    public void The_required_string_becomes_a_free_standing_node()
    {
        // The parser reports 'abc' as the required string for this pattern; re_compile turns it
        // into a STRING node that is not part of the graph.
        PatternObject pattern = Build("x*abc");

        using (new AssertionScope())
        {
            pattern.ReqString.Should().NotBeNull();
            pattern.ReqString.Op.Should().Be(Opcode.String);
            pattern.ReqString.Values.Should().Equal('a', 'b', 'c');
            pattern.NodeList.Should().Contain(pattern.ReqString);
        }
    }

    [Test]
    public void A_pattern_that_calls_itself_records_its_own_call_reference()
    {
        PatternObject pattern = Build("(?:a(?0)?)");

        pattern.PatternCallRef.Should().Be(0);
    }

    [Test]
    public void The_minimum_width_is_the_shortest_the_pattern_can_match()
    {
        using (new AssertionScope())
        {
            Build("abc").MinWidth.Should().Be(3);
            Build("a*").MinWidth.Should().Be(0);
            Build("a|bcd").MinWidth.Should().Be(1);
            Build(@"\b").MinWidth.Should().Be(0);
        }
    }

    private static PatternObject Build(string source) =>
        PatternObject.Compile(PatternCompiler.Compile(source, 0, _noNamedLists, PatternCompiler.DefaultVersion));
}
