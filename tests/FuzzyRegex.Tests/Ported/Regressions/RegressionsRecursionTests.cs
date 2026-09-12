using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c> (lines 3084-4410),
/// the assertions about recursion and, for one assertion that turns out to need
/// <c>Groups[n].Captures</c> rather than recursion itself, captures.
/// </summary>
public sealed class RegressionsRecursionTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#54")]
    public void Named_recursive_group_captures_every_nested_parenthesized_run()
    {
        // Hg issue 78: "Captures" doesn't work for recursive calls.
        FuzzyRegex
            .Match("aaa(((1+0)+1)+1)bbb", @"(?<rec>\((?:[^()]++|(?&rec))*\))")
            .Groups["rec"]
            .Captures.Select(static c => c.Value)
            .Should()
            .Equal("(1+0)", "((1+0)+1)", "(((1+0)+1)+1)");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#3")]
    public void Recursive_atomic_alternation_matches_every_top_level_balanced_group()
    {
        // Hg issue 31: atomic and normal groups in recursive patterns.
        FuzzyRegex
            .Matches("a(bcd(e)f)g(h)", @"\((?:(?>[^()]+)|(?R))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(bcd(e)f)", "(h)");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#4")]
    public void Recursive_alternation_without_an_atomic_group_matches_every_top_level_balanced_group() =>
        FuzzyRegex
            .Matches("a(bcd(e)f)g(h)", @"\((?:(?:[^()]+)|(?R))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(bcd(e)f)", "(h)");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#5")]
    public void Recursive_atomic_alternation_stops_at_the_first_unbalanced_close_paren() =>
        FuzzyRegex
            .Matches("a(b(cd)e)f)g)h", @"\((?:(?>[^()]+)|(?R))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(b(cd)e)");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#6")]
    public void Recursive_atomic_alternation_matches_the_innermost_balanced_group_in_an_unbalanced_subject() =>
        FuzzyRegex
            .Matches("a(bc(d(e)f)gh", @"\((?:(?>[^()]+)|(?R))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(d(e)f)");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#7")]
    public void Recursive_atomic_alternation_matches_the_innermost_balanced_group_when_searched_right_to_left() =>
        FuzzyRegex
            .Matches("a(bc(d(e)f)gh", @"(?r)\((?:(?>[^()]+)|(?R))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(d(e)f)");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#8")]
    public void Recursive_possessive_alternation_matches_the_innermost_balanced_group() =>
        FuzzyRegex
            .Matches("a(b(c(de)fg)h", @"\((?:[^()]*+|(?0))*\)")
            .Select(static m => m.Value)
            .Should()
            .Equal("(c(de)fg)");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#32")]
    public void Numbered_group_recursion_inside_a_lookbehind_matches_the_preceding_group() =>
        // Hg issue 49: regex.search("(a)(?<=b(?1))", "baz", regex.V1) returns None incorrectly.
        FuzzyRegex.Match("baz", "(?V1)(a)(?<=b(?1))").Value.Should().Be("a");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#37")]
    public void Mutually_recursive_numbered_groups_match_without_using_the_second_branch()
    {
        // Hg issue 51: regex.search("((a)(?1)|(?2))", "a", flags=regex.V1) returns None
        // incorrectly.
        Match m = FuzzyRegex.Match("a", "(?V1)((a)(?1)|(?2))");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().BeFalse();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#47")]
    public void Repeated_recursive_group_matches_the_shortest_valid_repetition_count()
    {
        // Hg issue 66: regex.search("((a|b(?1)c){3,5})", "baaaaca", flags=regex.V1).groups()
        // returns ('baaaac', 'baaaac') instead of ('aaaa', 'a').
        Match m = FuzzyRegex.Match("baaaaca", "((a|b(?1)c){3,5})");

        m.Value.Should().Be("aaaa");
        m.Groups[1].Value.Should().Be("aaaa");
        m.Groups[2].Value.Should().Be("a");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#254")]
    public void Named_recursive_atomic_group_matches_balanced_parens_around_a_sign()
    {
        // Hg issue 211: Segmentation fault with recursive matches and atomic groups.
        Match m = FuzzyRegex.MatchAtStart("((-))", @"\A(?P<whole>(?>\((?&whole)\)|[+\-]))\Z");

        (m.Index, m.Index + m.Length).Should().Be((0, 5));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#255")]
    public void Named_recursive_atomic_group_fails_when_a_sign_is_followed_by_an_extra_quantifier() =>
        FuzzyRegex.MatchAtStart("((-)+)", @"\A(?P<whole>(?>\((?&whole)\)|[+\-]))\Z").Success.Should().BeFalse();
}
