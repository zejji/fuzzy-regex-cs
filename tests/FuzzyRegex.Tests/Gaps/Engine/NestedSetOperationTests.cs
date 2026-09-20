using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Tests.Ported;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A set operation whose MEMBER is itself a set operation - <c>[[[a-z]--[aeiou]]&amp;&amp;[a-m]]</c>,
/// two levels deep rather than one.
/// </summary>
/// <remarks>
/// <para>
/// S57's coverage backstop found these the only arms of the matcher's switch that the whole suite
/// never enters: <c>MatchesMember</c>'s <c>SetDiff</c>, <c>SetInter</c> and <c>SetSymDiff</c> cases
/// (<c>Matcher.cs:444-448</c>), their three twins in <c>MatchesMemberIgn</c>
/// (<c>Matcher.cs:607-621</c>), and <c>InSetInterIgn</c> (<c>Matcher.cs:685</c>) whole. Upstream
/// reaches them from <c>matches_member</c> (<c>_regex.c:3025</c>) and <c>in_set_inter_ign</c>
/// (<c>_regex.c:3218</c>) for exactly this shape. The ported suite tests set operations one level
/// deep - <c>SetTests.Set_difference_findall</c> and
/// <c>SetTests.Set_operators_over_properties_count_over_every_byte_value</c> - and upstream's own
/// <c>test_set</c> never nests one inside another, so nothing had ever run the recursion.
/// </para>
/// <para>
/// Set operations need version 1, which this port defaults to and upstream does not; the patterns
/// below say <c>(?V1)</c> so that the string asked here is the string asked of upstream.
/// </para>
/// </remarks>
public sealed class NestedSetOperationTests
{
    private const string _lower = "abcdefghijklmnopqrstuvwxyz";
    private const string _upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Measured on regex 2026.9.10, 2026-09-20:
    /// <c>''.join(regex.findall(r'[[[a-z]--[aeiou]]&amp;&amp;[a-m]]', 'abcdefghijklmnopqrstuvwxyz',
    /// regex.V1))</c> gives <c>'bcdfghjklm'</c>; the same call with <c>[[[a-z]&amp;&amp;[a-m]]--[aeiou]]</c>
    /// gives <c>'bcdfghjklm'</c> and with <c>[[[a-z]~~[aeiou]]&amp;&amp;[a-f]]</c> gives <c>'bcdf'</c>.
    /// </summary>
    [Test]
    [Arguments(@"(?V1)[[[a-z]--[aeiou]]&&[a-m]]", _lower, "bcdfghjklm")]
    [Arguments(@"(?V1)[[[a-z]&&[a-m]]--[aeiou]]", _lower, "bcdfghjklm")]
    [Arguments(@"(?V1)[[[a-z]~~[aeiou]]&&[a-f]]", _lower, "bcdf")]
    public void A_set_operation_may_be_a_member_of_another(string pattern, string subject, string expected) =>
        string.Concat(Upstream.Matches(subject, pattern).Select(static m => m.Value)).Should().Be(expected);

    /// <summary>
    /// The same three patterns under <c>IGNORECASE</c> over the upper-case alphabet, which is what
    /// runs <c>MatchesMemberIgn</c>'s three set-operation arms and <c>InSetInterIgn</c>.
    /// </summary>
    /// <remarks>
    /// Measured on regex 2026.9.10, 2026-09-20:
    /// <c>''.join(regex.findall(r'[[[a-z]--[aeiou]]&amp;&amp;[a-m]]', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ',
    /// regex.I | regex.V1))</c> gives <c>'BCDFGHJKLM'</c>; <c>[[[a-z]&amp;&amp;[a-m]]--[aeiou]]</c> gives
    /// <c>'BCDFGHJKLM'</c> and <c>[[[a-z]~~[aeiou]]&amp;&amp;[a-f]]</c> gives <c>'BCDF'</c>.
    /// </remarks>
    [Test]
    [Arguments(@"(?iV1)[[[a-z]--[aeiou]]&&[a-m]]", _upper, "BCDFGHJKLM")]
    [Arguments(@"(?iV1)[[[a-z]&&[a-m]]--[aeiou]]", _upper, "BCDFGHJKLM")]
    [Arguments(@"(?iV1)[[[a-z]~~[aeiou]]&&[a-f]]", _upper, "BCDF")]
    public void A_nested_set_operation_folds_case_like_a_flat_one(string pattern, string subject, string expected) =>
        string.Concat(Upstream.Matches(subject, pattern).Select(static m => m.Value)).Should().Be(expected);

    /// <summary>
    /// A negated class whose single member is a set operation, so the recursion runs under a
    /// negation. Measured on regex 2026.9.10, 2026-09-20:
    /// <c>''.join(regex.findall(r'[^[[a-z]--[aeiou]]]', 'abcdef', regex.V1))</c> gives <c>'ae'</c>.
    /// </summary>
    [Test]
    public void A_negated_class_may_hold_a_set_operation() =>
        string.Concat(Upstream.Matches("abcdef", @"(?V1)[^[[a-z]--[aeiou]]]").Select(static m => m.Value))
            .Should()
            .Be("ae");
}
