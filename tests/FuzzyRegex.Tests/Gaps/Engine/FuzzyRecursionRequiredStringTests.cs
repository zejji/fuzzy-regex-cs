using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The fuzzy copy that <c>(?R)</c> in a fuzzy section compiles never carries the required-string
/// mark (D34).
/// </summary>
/// <remarks>
/// <para>
/// The compiler marks one exact run as the required string, and the engine skips comparing it at
/// the place the prefilter found it (<c>upstream/src/_regex.c</c>:14725-14727). A call to the
/// pattern as a whole from a fuzzy section compiles the optimised pattern a second time as fuzzy
/// (<c>upstream/regex/_regex_core.py</c>:4432-4441), so that marked run is compiled into the copy
/// too, and upstream sets <c>REQUIRED_OP</c> on it there (:4041-4048). Upstream loses nothing by the
/// skip, since its compare loop pushes nothing for characters that match. This port's does: a fuzzy
/// character that matched may still be deleted (ledger entry 42), and the skip left those choices
/// out, so a match within the budget was lost. A Debug build asserted instead.
/// </para>
/// <para>
/// <c>(?1)</c> never reached this: a group call copies the group recorded before optimisation,
/// which holds the unpacked characters rather than the marked run (<c>_regex_core.py</c>:4442-4449).
/// Upstream raises <c>MemoryError</c> on the lost-match rows below (regex 2026.9.10, measured
/// 2026-09-29), so the control is the same pattern with the run spelt so that it is not the
/// required string.
/// </para>
/// </remarks>
public sealed class FuzzyRecursionRequiredStringTests
{
    private static void ShouldMatch(Match m, int index, int length, FuzzyCounts counts)
    {
        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }

    // The exact run keeps the mark and the fuzzy copy of it does not.
    [Test]
    public void A_fuzzy_copy_of_the_whole_pattern_leaves_its_required_string_unmarked()
    {
        foreach (string pattern in (string[])["ab(?:x|(?R)){e<=1}", "(ab)(?:(?R)){d<1}", "(?:d(?:(?R)){d<=1})?aa"])
        {
            var regex = new FuzzyRegex(pattern);
            Node[] marked =
            [
                .. regex.PatternObject.NodeList.Where(static node => (node.Status & NodeStatus.Required) != 0),
            ];

            marked.Should().ContainSingle(pattern);
            (marked[0].Status & NodeStatus.Fuzzy).Should().Be(0, pattern);
        }
    }

    // AGREES WITH UPSTREAM 2026.9.10: the rows that tripped the Debug assertion.
    [Test]
    public void A_whole_pattern_call_in_a_fuzzy_section_answers_as_upstream()
    {
        // fullmatch('ab(?:x|(?R)){e<=1}', 'abab')   (0, 4) with (0, 0, 1)
        // fullmatch('ab(?:x|(?R)){e<=1}', 'ababx')  (0, 5) with (0, 0, 0)
        // search('ab(?:x|(?R)){e<=1}', 'abab')      (0, 3) with (1, 0, 0)
        // search('(ab)(?:(?R)){d<1}', 'abab')       None
        var regex = new FuzzyRegex("ab(?:x|(?R)){e<=1}");
        ShouldMatch(regex.FullMatch("abab"), 0, 4, new FuzzyCounts(0, 0, 1));
        ShouldMatch(regex.FullMatch("ababx"), 0, 5, new FuzzyCounts(0, 0, 0));
        ShouldMatch(regex.Match("abab"), 0, 3, new FuzzyCounts(1, 0, 0));
        new FuzzyRegex("(ab)(?:(?R)){d<1}").Match("abab").Success.Should().BeFalse();
    }

    // Upstream raises MemoryError on these rows. The called copy matches the first "a" of "aa" and
    // deletes the second, and the outer "aa" takes the rest. Before the fix the copy's "aa", at the
    // place the prefilter found the required string, was skipped whole: match and fullmatch found
    // nothing, and search found (1, 3). The last row is the control, "aa" spelt as no required
    // string, which always found the deletion.
    [Test]
    public void A_fuzzy_copy_of_the_required_string_can_delete_a_character_it_matched()
    {
        var regex = new FuzzyRegex("(?:d(?:(?R)){d<=1})?aa");
        ShouldMatch(regex.MatchAtStart("daaa"), 0, 4, new FuzzyCounts(0, 0, 1));
        ShouldMatch(regex.FullMatch("daaa"), 0, 4, new FuzzyCounts(0, 0, 1));
        ShouldMatch(regex.Match("daaab"), 0, 4, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:d(?:(?R)){d<=2})?ab").MatchAtStart("dab"), 0, 3, new FuzzyCounts(0, 0, 2));
        ShouldMatch(
            new FuzzyRegex("(?:d(?:(?R)){d<=1})?a(?:a|zzz)").MatchAtStart("daaa"),
            0,
            4,
            new FuzzyCounts(0, 0, 1)
        );
    }
}
