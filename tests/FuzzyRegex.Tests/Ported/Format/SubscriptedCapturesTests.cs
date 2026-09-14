using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Format;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_subscripted_captures</c>
/// (lines 4500-4514): a format template's <c>{n[i]}</c> subscript reaches into the individual
/// captures of a group that matched more than once, by group number or by group name, negative
/// indices included, through both <c>Match.expandf</c> and <c>subf</c>.
/// </summary>
public sealed class SubscriptedCapturesTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#1")]
    public void ResultFormat_group_zero_subscript_selects_the_whole_match_by_index() =>
        Upstream.MatchAtStart("abc", @"(?P<x>.)+").ResultFormat("{0} {0[0]} {0[-1]}").Should().Be("abc abc abc");

    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#2")]
    public void ResultFormat_group_one_subscript_indexes_into_its_repeated_captures() =>
        Upstream
            .MatchAtStart("abc", @"(?P<x>.)+")
            .ResultFormat("{1} {1[0]} {1[1]} {1[2]} {1[-1]} {1[-2]} {1[-3]}")
            .Should()
            .Be("c a b c c b a");

    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#3")]
    public void ResultFormat_named_group_subscript_indexes_into_its_repeated_captures() =>
        Upstream
            .MatchAtStart("abc", @"(?P<x>.)+")
            .ResultFormat("{x} {x[0]} {x[1]} {x[2]} {x[-1]} {x[-2]} {x[-3]}")
            .Should()
            .Be("c a b c c b a");

    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#4")]
    public void ReplaceFormat_group_zero_subscript_selects_the_whole_match_by_index() =>
        Upstream.ReplaceFormat("abc", @"(?P<x>.)+", "{0} {0[0]} {0[-1]}").Should().Be("abc abc abc");

    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#5")]
    public void ReplaceFormat_group_one_subscript_indexes_into_its_repeated_captures() =>
        Upstream
            .ReplaceFormat("abc", @"(?P<x>.)+", "{1} {1[0]} {1[1]} {1[2]} {1[-1]} {1[-2]} {1[-3]}")
            .Should()
            .Be("c a b c c b a");

    [Test]
    [Property("Upstream", "RegexTests.test_subscripted_captures#6")]
    public void ReplaceFormat_named_group_subscript_indexes_into_its_repeated_captures() =>
        Upstream
            .ReplaceFormat("abc", @"(?P<x>.)+", "{x} {x[0]} {x[1]} {x[2]} {x[-1]} {x[-2]} {x[-3]}")
            .Should()
            .Be("c a b c c b a");
}
