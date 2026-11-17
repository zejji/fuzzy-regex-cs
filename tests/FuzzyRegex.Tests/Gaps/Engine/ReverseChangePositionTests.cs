using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D160: a match that reads the text backwards reports where it substituted or
/// inserted a character at the index of the character it edited, as a forward match does.
/// </summary>
/// <remarks>
/// <para>
/// A change position is an index into the text, and the same character was edited whichever way
/// the pattern read it, so the position cannot depend on the direction. Upstream records a change
/// at the position the match stood on before it stepped over the character (<c>new_text_pos -
/// step</c> in <c>fuzzy_match_item</c>, <c>upstream/src/_regex.c:10245</c>; the comparison's position
/// in the string paths, <c>:10483</c>). Reading forwards that is the character's own index; reading
/// backwards it is the index after it. A deletion takes no character, so its position is the gap,
/// which both directions agree on.
/// </para>
/// <para>
/// Measured on <c>regex</c> 2026.9.29 (2026-11-16, <c>.scratch/d160-probe.py</c>):
/// <c>(?r)(?:abc){s&lt;=1}</c> over 'adc' substitutions [2] (forward [1]);
/// <c>(?r)(?:abcd){s&lt;=2}</c> over 'xbcy' [4, 1], the first past the end of the text (forward
/// [0, 3]); <c>(?r)(?:abc){i&lt;=2}</c> over 'a12bc' insertions [3, 2] (forward [1, 2]); a forward
/// pattern's lookbehind reads backwards and is off by one too. This port answered as upstream does
/// until D160. The lists keep the order the match made the changes in, which for a reversed match
/// is right to left; only the positions change.
/// </para>
/// </remarks>
public sealed class ReverseChangePositionTests
{
    [Test]
    // upstream: [2] / forward [1]
    [Arguments("(?r)(?:abc){s<=1}", "adc", "1")]
    // upstream: [4, 1] / forward [0, 3]
    [Arguments("(?r)(?:abcd){s<=2}", "xbcy", "3,0")]
    // single-character items rather than a string; upstream [2]
    [Arguments("(?r)(?:[a][b][c]){s<=1}", "adc", "1")]
    // a case-folded string; upstream [2]
    [Arguments("(?ir)(?:abc){s<=1}", "ADC", "1")]
    // a lookbehind reads backwards inside a forward pattern; upstream [2]
    [Arguments("(?<=(?:abc){s<=1})d", "axcd", "1")]
    // a non-BMP character is two UTF-16 code units; upstream (codepoints) [2]
    [Arguments("(?r)(?:abc){s<=1}", "a\U0001F600c", "1")]
    public void A_reversed_substitution_is_reported_at_the_character_it_replaced(
        string pattern,
        string text,
        string expected
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        m.FuzzyChanges.Substitutions.Should().Equal(Positions(expected));
    }

    [Test]
    // upstream: [2] / forward [1]
    [Arguments("(?r)(?:ab){i<=1}", "a1b", "1")]
    // upstream: [3, 2] / forward [1, 2]
    [Arguments("(?r)(?:abc){i<=2}", "a12bc", "2,1")]
    // single-character items; upstream [2]
    [Arguments("(?r)(?:[a][b]){i<=1}", "a1b", "1")]
    // a case-folded string; upstream [3] / forward [2]
    [Arguments("(?ir)(?:abc){i<=1}", "AB1C", "2")]
    // a lookbehind; upstream [3]
    [Arguments("(?<=(?:abc){i<=1})d", "ab1cd", "2")]
    // the inserted character is a surrogate pair at 2-3; upstream (codepoints) [3], after it
    [Arguments("(?r)(?:abc){i<=1}", "ab\U0001F600c", "2")]
    public void A_reversed_insertion_is_reported_at_the_character_it_let_in(
        string pattern,
        string text,
        string expected
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        m.FuzzyChanges.Insertions.Should().Equal(Positions(expected));
    }

    [Test]
    // the character let in is the last one, read first; upstream [4], past the end of the text
    [Arguments("(?r)(?:abc){e<=1}", "abc1", "3")]
    // the character let in is the first one, read last
    [Arguments("(?r)(?:abc){e<=1}", "1abc", "0")]
    public void A_reversed_fullmatch_reports_an_insertion_at_either_end_inside_the_text(
        string pattern,
        string text,
        string expected
    )
    {
        Match m = new FuzzyRegex(pattern).FullMatch(text);

        m.FuzzyChanges.Insertions.Should().Equal(Positions(expected));
    }

    [Test]
    // upstream [1] both ways: a deletion is a gap, not a character
    [Arguments("(?r)(?:abc){d<=1}", "ac", "1")]
    // upstream [1, 2] both ways
    [Arguments("(?r)(?:abcd){d<=2}", "ad", "1,2")]
    public void A_reversed_deletion_is_reported_where_forward_reports_it(string pattern, string text, string expected)
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        m.FuzzyChanges.Deletions.Should().Equal(Positions(expected));
    }

    [Test]
    [Arguments("(?:abcd){e<=2}", "xbcy")]
    [Arguments("(?:abc){e<=2}", "a12bc")]
    [Arguments("(?:abcd){e<=2}", "ad")]
    [Arguments("(?i)(?:abc){e<=1}", "AB1C")]
    [Arguments("(?:abc){e<=1}", "ab\U0001F600c")]
    [Arguments("(?:abc){e<=1}", "abc1")]
    [Arguments("(?:abc){e<=1}", "1abc")]
    public void A_reversed_fullmatch_makes_the_changes_forward_makes(string pattern, string text)
    {
        FuzzyChanges forward = new FuzzyRegex(pattern).FullMatch(text).FuzzyChanges;
        FuzzyChanges reversed = new FuzzyRegex("(?r)" + pattern).FullMatch(text).FuzzyChanges;

        reversed.Substitutions.Order().Should().Equal(forward.Substitutions.Order());
        reversed.Insertions.Order().Should().Equal(forward.Insertions.Order());
        reversed.Deletions.Order().Should().Equal(forward.Deletions.Order());
    }

    private static int[] Positions(string list) => [.. list.Split(',').Select(int.Parse)];
}
