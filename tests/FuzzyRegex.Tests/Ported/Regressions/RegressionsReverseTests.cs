using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about right-to-left (reverse) searching.
/// </summary>
public sealed class RegressionsReverseTests
{
    // Hg issue 193: Alternation and .REVERSE flag. Upstream #222 is the forward control for
    // #223 and passes no flags at all, so it is tagged for what it actually needs.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#222")]
    public void Forward_search_finds_the_alternation_match_at_its_only_position()
    {
        Match m = Upstream.Match("111a222", "a|b");

        m.Index.Should().Be(3);
        m.Length.Should().Be(1);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#223")]
    public void Inline_reverse_flag_finds_the_alternation_match_nearest_the_end()
    {
        Match m = Upstream.Match("111a222", "(?r)a|b");

        m.Index.Should().Be(3);
        m.Length.Should().Be(1);
    }
}
