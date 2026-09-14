using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_10328</c>
/// (lines 1486-1501).
/// </summary>
/// <remarks>
/// Assertions are numbered in source order over every <c>self.assert</c> line, including the
/// pre-3.7 branch of the version guard that this port does not use (#2), so a number here always
/// names the same upstream line.
/// <para>The V0 <c>subn</c> assertion is guarded upstream by <c>sys.version_info &gt;= (3, 7, 0)</c>;
/// only that branch is ported here. Each <c>finditer</c> assertion exercises
/// <c>(?&lt;=[^\n])\Z</c> and <c>$</c> in multiline mode rather than substitution, so it is tagged
/// <c>needs:anchors</c> instead of <c>needs:substitution</c>.</para>
/// </remarks>
public sealed class Bug10328Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_10328#1")]
    public void V0_replace_with_evaluator_tags_the_trailing_whitespace_and_missing_final_newline_groups()
    {
        var re = Upstream.Compile(@"(?mV0)(?P<trailing_ws>[ \t]+\r*$)|(?P<no_final_newline>(?<=[^\n])\Z)");

        string result = re.Replace("foobar ", static m => "<" + m.LastGroupName + ">", -1, out int replacements);

        result.Should().Be("foobar<trailing_ws><no_final_newline>");
        replacements.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_10328#3")]
    public void V0_matches_the_trailing_whitespace_and_the_empty_missing_final_newline()
    {
        var re = Upstream.Compile(@"(?mV0)(?P<trailing_ws>[ \t]+\r*$)|(?P<no_final_newline>(?<=[^\n])\Z)");

        re.Matches("foobar ").Select(static m => m.Value).Should().Equal(" ", "");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_10328#4")]
    public void V1_replace_with_evaluator_tags_the_trailing_whitespace_and_missing_final_newline_groups()
    {
        var re = Upstream.Compile(@"(?mV1)(?P<trailing_ws>[ \t]+\r*$)|(?P<no_final_newline>(?<=[^\n])\Z)");

        string result = re.Replace("foobar ", static m => "<" + m.LastGroupName + ">", -1, out int replacements);

        result.Should().Be("foobar<trailing_ws><no_final_newline>");
        replacements.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_10328#5")]
    public void V1_matches_the_trailing_whitespace_and_the_empty_missing_final_newline()
    {
        var re = Upstream.Compile(@"(?mV1)(?P<trailing_ws>[ \t]+\r*$)|(?P<no_final_newline>(?<=[^\n])\Z)");

        re.Matches("foobar ").Select(static m => m.Value).Should().Equal(" ", "");
    }
}
