using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Escapes;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_escape</c> (lines 683-694) and
/// <c>test_bug_612074</c> (lines 788-790).
/// </summary>
public sealed class EscapeFunctionTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_re_escape#1")]
    public void Escaping_the_empty_string_returns_the_empty_string() => FuzzyRegex.Escape("").Should().Be("");

    // Upstream loops over all 256 code points 0-255, escaping each and checking it still matches
    // itself. Kept as a native loop rather than 256 [Arguments] rows - both assertions inside the
    // loop are the same invariant repeated over data, not independent behaviours, and 256 rows adds
    // bulk without adding signal.
    [Test]
    [Property("Upstream", "RegexTests.test_re_escape#2-3")]
    public void Every_code_point_0_to_255_matches_itself_once_escaped()
    {
        for (int i = 0; i < 256; i++)
        {
            string ch = ((char)i).ToString();
            Match m = FuzzyRegex.MatchAtStart(ch, FuzzyRegex.Escape(ch));

            m.Success.Should().BeTrue();
            (m.Index, m.Index + m.Length).Should().Be((0, 1));
        }
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_escape#4")]
    public void Escaping_all_256_code_points_together_still_matches_them_all()
    {
        string p = string.Concat(Enumerable.Range(0, 256).Select(i => (char)i));

        var pat = new FuzzyRegex(FuzzyRegex.Escape(p));
        Match m = pat.MatchAtStart(p);

        (m.Index, m.Index + m.Length).Should().Be((0, 256));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_bug_612074#1")]
    public void Escaping_a_character_for_use_inside_a_set_compiles()
    {
        string pattern = "[" + FuzzyRegex.Escape("‹") + "]";

        Action act = () => _ = new FuzzyRegex(pattern);

        act.Should().NotThrow();
    }
}
