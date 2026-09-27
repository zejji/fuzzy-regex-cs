using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.OpenDefects;

/// <summary>
/// One test per known, unfixed defect whose right answer is settled. They are <c>[Explicit]</c>,
/// so the ratchet does not run them; the number that fail is the measured size of the remaining
/// correctness work. The first step of each fix moves its test into the suite proper.
/// </summary>
/// <remarks>
/// Run them with
/// <c>dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/OpenDefectTests/*"</c>.
/// Upstream's answers were measured against <c>regex</c> 2026.9.10 on 2026-09-27 and are quoted
/// beside each assertion. Where upstream is wrong the expected value is argued from principle, and
/// the argument is written above the test.
/// </remarks>
[Explicit]
public sealed class OpenDefectTests
{
    // Queue item 2. `\G` holds only at the search anchor, 0 here, where the text is 'z', so the exact
    // tail `(?:x|(\Gab))` matches nowhere and nothing can match. The port throws
    // NotImplementedException; upstream raises "RuntimeError: invalid RE code" for any `\G` inside a
    // fuzzy section, so it has no answer to copy.
    [Test]
    public void A_search_anchor_inside_a_called_group_in_a_fuzzy_section_is_answered()
    {
        new FuzzyRegex(@"(?b)(?:.??(?1)){e<=1}(?:x|(\Gab))").Match("zab").Success.Should().BeFalse();
    }

    // Queue item 4 (checklist item 11). Neither subject contains the text the pattern needs after
    // the lazy repeat ('c'; 'ﬁİßx' under simple folding), so the answer is no match. Upstream:
    // MemoryError for the first, None for the second. The port exhausts its backtrack stack.
    [Test]
    public void A_lazy_repeat_of_a_fuzzy_single_character_section_fails_without_exhausting_the_stack()
    {
        new FuzzyRegex("(?fi)(?:(?:a){e<=1})+?(?=c)").Match("σ").Success.Should().BeFalse();
        new FuzzyRegex("(?V0i)(?:(?:ẞ){e<=1})*?ﬁİßx").Match("cabcatX").Success.Should().BeFalse();
    }

    // Queue item 5 (checklist item 13). A full match of 'a' has to consume the 'a', and deletions
    // (a pattern item left out) never consume text, so the answer is no match. Upstream: MemoryError.
    [Test]
    public void A_fullmatch_of_a_deletion_only_repeat_of_an_optional_item_fails_cleanly()
    {
        new FuzzyRegex("(?:(?:b?)*){d<=1}").FullMatch("a").Success.Should().BeFalse();
    }

    // Queue item 6 (checklist item 17). Upstream lets the fully folded 'ß' item be substituted by
    // one character: `(?fi)(?:ß){s<=1}` over 'a' is (0, 1) with counts (1, 0, 0). The same
    // substitution followed by an exact 'x' must then match 'ax'. Upstream: None.
    [Test]
    public void A_substituted_fully_folded_sharp_s_can_be_followed_by_more_pattern()
    {
        Match m = new FuzzyRegex("(?fi)(?:ßx){s<=1}").Match("ax");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // Queue item 7 (S3-F2). One inserted 'x' after the 'b' puts the lookahead in front of the 'c',
    // so the first match is (0, 2) with one insertion. Upstream: None (it never tries an insertion
    // before a failing lookaround).
    [Test]
    public void A_fuzzy_insertion_is_tried_before_a_failing_lookahead()
    {
        Match m = new FuzzyRegex("(?:b(?=c)){i<=1}").Match("bxc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // Queue item 8 (F-D). Each section's only item matches exactly, but the section needs at least
    // one error; inserting one text character before the item meets it. Upstream: None for all three.
    [Test]
    [Arguments(@"(?:a){1<=e<=2}b", "aab", 3)]
    [Arguments(@"(?:[ab]){1<=e<=2}a", "bba", 3)]
    [Arguments(@"(a)(?:\1){1<=e<=2}b", "aaab", 4)]
    public void A_section_minimum_error_count_is_met_by_an_insertion_before_an_exact_item(
        string pattern,
        string text,
        int length
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // Queue item 9. A negative lookahead succeeds only when its body fails, so nothing its body
    // captured survives; group 1 keeps only its own capture. Upstream agrees when the body spells
    // the group out (`(?!.(?:a))` gives ['a']) but keeps an entry when the body calls it (['a', 'a']).
    [Test]
    public void A_group_call_inside_a_failed_lookaround_leaves_no_capture_behind()
    {
        Match m = new FuzzyRegex("(a)(?:(?!.(?1))|.)+?b").Match("aaab");

        m.Success.Should().BeTrue();
        m.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((0, 1));
    }

    // Matrix wave triage (docs/plan/2026-09-27-matrix-wave-triage.md), seed 4242 row 1719,
    // minimised. The grammar G -> '' | G 'a' generates 'a', 'aa', 'aaa' and so on, so both subjects
    // are in its language. Matching 'aa' needs a call of G nested inside another call of G at the
    // same text position, and the innermost call takes the empty branch, so the path is finite.
    // Upstream finds (0, 2) for all three, and so does PCRE2 10.47 for the named-group form
    // (`^(?<g>|(?&g)a)$` over 'aa', 'aaa', 'aaaa'). The port refuses any call of a group at a
    // position where a call of it is already open (Matcher.cs, Opcode.GroupCall) and answers None,
    // None and (1, 2). The third row is the reversed form, which is how the wave reached it.
    [Test]
    [Arguments("(?:|(?R)a)", false)]
    [Arguments("(?r)(?:|a(?R))", false)]
    [Arguments("(?P<g>|(?&g)a)$", true)]
    public void A_left_recursive_call_nested_at_one_position_still_finds_its_match(string pattern, bool search)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match("aa") : regex.FullMatch("aa");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
    }
}
