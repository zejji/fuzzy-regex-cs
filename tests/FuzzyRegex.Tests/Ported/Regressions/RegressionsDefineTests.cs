using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about <c>(?(DEFINE)...)</c> groups (Hg issue 152: Request: (?(DEFINE)...)).
/// </summary>
/// <remarks>
/// <c>groupdict()</c> and <c>capturesdict()</c> have no counterpart on this API: each named group
/// is asserted individually via <c>Groups[name]</c> instead of comparing a whole dictionary, per
/// the orchestrator note on this packet.
/// </remarks>
public sealed class RegressionsDefineTests
{
    // Hg issue 158: Group issue with (?(DEFINE)...). (?smx) makes whitespace and the '#' comment
    // insignificant outside a character class, so the layout below is exactly upstream's.
    private const string _hgIssue158Pattern =
        @"(?smx)
(?(DEFINE)
  (?<subcat>
   ^,[^,]+,
   )
)

# Group 2 is defined on this line
^,([^,]+),

(?:(?!(?&subcat)[\r\n]+(?&subcat)).)+";

    // Built with explicit \n so the character offsets below don't depend on this file's line
    // endings; upstream's own triple-quoted string is LF-only.
    private const string _hgIssue158Data =
        "\n,Cat 1,\n,Brand 1,\nsome\nthing\n,Brand 2,\nother\nthings\n,Cat 2,\n,Brand,\nSome\nthing\n";

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#169")]
    public void Define_group_supplies_named_subroutines_referenced_later_in_the_pattern() =>
        FuzzyRegex
            .Match("5 elephants", @"(?(DEFINE)(?<quant>\d+)(?<item>\w+))(?&quant) (?&item)")
            .Value.Should()
            .Be("5 elephants");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#170")]
    public void Group_called_only_from_inside_define_reports_no_group_value() =>
        FuzzyRegex.Match("a", @"(?&routine)(?(DEFINE)(?<routine>.))").Groups["routine"].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#171")]
    public void Group_called_only_from_inside_define_still_records_its_capture() =>
        FuzzyRegex
            .Match("a", @"(?&routine)(?(DEFINE)(?<routine>.))")
            .Groups["routine"]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("a");

    // Hg issue 158: Group issue with (?(DEFINE)...).
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#209")]
    public void Define_group_referenced_via_a_negative_lookahead_leaves_group_1_unmatched_on_every_finditer_match()
    {
        Match[] matches = [.. new FuzzyRegex(_hgIssue158Pattern).Matches(_hgIssue158Data)];

        matches.Should().HaveCount(2);

        matches[0].Groups[1].Success.Should().BeFalse();
        matches[0].Groups[2].Index.Should().Be(2);
        matches[0].Groups[2].Length.Should().Be(5);

        matches[1].Groups[1].Success.Should().BeFalse();
        matches[1].Groups[2].Index.Should().Be(54);
        matches[1].Groups[2].Length.Should().Be(5);
    }

    // Hg issue 230: Is it a bug of (?(DEFINE)...).
    [Test]
    // Upstream's control for #273: this pattern has no (?(DEFINE) in it, so it is tagged for
    // the negative lookahead it does need rather than for the construct it is contrasted with.
    [Property("Upstream", "RegexTests.test_hg_bugs#272")]
    public void Negative_lookahead_run_without_define_finds_only_the_trailing_letters() =>
        FuzzyRegex.Matches("abcdefgh", @"(?:(?![a-d]).)+").Select(m => m.Value).Should().Equal("efgh");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#273")]
    public void Same_negative_lookahead_wrapped_in_a_define_subroutine_finds_the_same_match() =>
        FuzzyRegex
            .Matches("abcdefgh", @"(?(DEFINE)(?P<mydef>(?:(?![a-d]).)))(?&mydef)+")
            .Select(m => m.Value)
            .Should()
            .Equal("efgh");

    // Hg issue 252: Empty capture strings when using DEFINE group reference within look-behind
    // expression.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#300")]
    public void Define_group_called_directly_reports_no_group_value() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.))(?&func)").Groups[1].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#301")]
    public void Define_group_called_directly_reports_no_group_value_by_name() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.))(?&func)").Groups["func"].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#302")]
    public void Define_group_called_directly_still_records_its_single_capture() =>
        FuzzyRegex
            .Match("abc", @"(?(DEFINE)(?<func>.))(?&func)")
            .Groups["func"]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("a");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#303")]
    public void Define_group_called_from_a_lookahead_reports_no_group_value() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.))(?=(?&func))").Groups[1].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#304")]
    public void Define_group_called_from_a_lookahead_reports_no_group_value_by_name() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.))(?=(?&func))").Groups["func"].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#305")]
    public void Define_group_called_from_a_lookahead_still_records_its_single_capture() =>
        FuzzyRegex
            .Match("abc", @"(?(DEFINE)(?<func>.))(?=(?&func))")
            .Groups["func"]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("a");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#306")]
    public void Define_group_called_from_a_lookbehind_reports_no_group_value() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.)).(?<=(?&func))").Groups[1].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#307")]
    public void Define_group_called_from_a_lookbehind_reports_no_group_value_by_name() =>
        FuzzyRegex.Match("abc", @"(?(DEFINE)(?<func>.)).(?<=(?&func))").Groups["func"].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#308")]
    public void Define_group_called_from_a_lookbehind_still_records_its_single_capture() =>
        FuzzyRegex
            .Match("abc", @"(?(DEFINE)(?<func>.)).(?<=(?&func))")
            .Groups["func"]
            .Captures.Select(c => c.Value)
            .Should()
            .Equal("a");

    // Hg issue 329: Wrong group matches when question mark quantifier is used within a look
    // behind.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#377")]
    public void Define_group_with_an_internal_alternative_records_captures_only_for_the_branch_that_matched()
    {
        Match m = FuzzyRegex.Match(
            "x right",
            @"(?(DEFINE)(?<mydef>(?<wrong>THIS_SHOULD_NOT_MATCHx?)|(?<right>right))).*(?<=(?&mydef).*)"
        );

        m.Groups["mydef"].Captures.Select(c => c.Value).Should().Equal("right");
        m.Groups["wrong"].Captures.Select(c => c.Value).Should().BeEmpty();
        m.Groups["right"].Captures.Select(c => c.Value).Should().Equal("right");
    }
}
