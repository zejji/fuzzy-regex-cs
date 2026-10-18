using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// A fuzzy minimum needs a maximum. <c>{1&lt;=d}</c> is not a constraint but six literal characters,
/// because upstream's <c>parse_cost_constraint</c> (<c>upstream/regex/_regex_core.py:722-725</c>)
/// gives up when no <c>&lt;=</c> or <c>&lt;</c> follows the error kind, and <c>parse_fuzzy</c> then
/// leaves the <c>{</c> to be read as a literal. Known defect D95 was this misread (closed as F95, not
/// a defect). Every expected value was measured against <c>regex</c> 2026.9.10 on 2026-10-18 and
/// this port gives the same (<c>.scratch/s1016/d95/</c>).
/// </summary>
public sealed class FuzzyMinimumWithoutMaximumTests
{
    /// <summary>
    /// A minimum with no maximum after a group is literal text. regex 2026.9.10: each subject below
    /// matches in full with no errors, and <c>(?:qx){1&lt;=d}</c> over 'x' is None.
    /// </summary>
    [Test]
    [Arguments("(?:(?P<g1>q(?1)|x)){1<=d}", "x{1<=d}", "(0,7)")]
    [Arguments("(?:(?P<g1>q(?1)|x)){1<=d}", "x", "None")]
    [Arguments("(?:qx){1<=d}", "qx{1<=d}", "(0,8)")]
    [Arguments("(?:qx){1<=d}", "x", "None")]
    [Arguments("(?:qx){0<=d}", "qx{0<=d}", "(0,8)")]
    [Arguments("(?:qx){1<=e}", "qx{1<=e}", "(0,8)")]
    [Arguments("(?:qx){1<=e}", "x", "None")]
    [Arguments("(?:qx){1<d}", "qx{1<d}", "(0,7)")]
    public void A_minimum_without_a_maximum_is_literal_text(string pattern, string subject, string expected) =>
        Answer(new FuzzyRegex(pattern).Match(subject)).Should().Be(expected);

    /// <summary>
    /// With a maximum the same minimum is a constraint. regex 2026.9.10: <c>(?:qx){1&lt;=d&lt;=9999}</c>
    /// over 'x' is (0, 1) with one deletion, and <c>(?:qx){1&lt;=e&lt;=2}</c> over 'x' (0, 1) with a
    /// substitution and a deletion.
    /// </summary>
    [Test]
    [Arguments("(?:qx){1<=d<=9999}", "x", "(0,1) s0i0d1")]
    [Arguments("(?:qx){1<=d<=5}", "x", "(0,1) s0i0d1")]
    [Arguments("(?:qx){0<d<2}", "x", "(0,1) s0i0d1")]
    [Arguments("(?:qx){1<=e<=2}", "x", "(0,1) s1i0d1")]
    [Arguments("(?:(?P<g1>q(?1)|x)){1<=d<=3}", "x", "(0,1) s0i0d3")]
    public void A_minimum_with_a_maximum_is_a_constraint(string pattern, string subject, string expected) =>
        Answer(new FuzzyRegex(pattern).Match(subject)).Should().Be(expected);

    private static string Answer(Match match)
    {
        if (!match.Success)
        {
            return "None";
        }

        string span = $"({match.Index},{match.Index + match.Length})";
        FuzzyCounts counts = match.FuzzyCounts;
        return counts == default ? span : $"{span} s{counts.Substitutions}i{counts.Insertions}d{counts.Deletions}";
    }
}
