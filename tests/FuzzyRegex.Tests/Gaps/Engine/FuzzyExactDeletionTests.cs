using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy item that matched exactly may still be deleted, and that choice is tried before any
/// earlier one.
/// </summary>
/// <remarks>
/// <para>
/// Upstream tries errors on an item only when it fails to match (<c>fuzzy_match_item</c>,
/// <c>upstream/src/_regex.c</c>:10185-10258), and an item that matches pushes nothing to come back
/// to (:11924-11927 for one character, :14742-14745 for a string). So when the rest of the pattern
/// fails, deleting that item is never tried, and a match within the budget is lost:
/// <c>(?:a){d&lt;=1}a</c> finds nothing in <c>a</c>, although deleting the fuzzy <c>a</c> costs one
/// deletion. Ledger entry 42, finding F-A of the 2026-09-26 fuzzy sweep; inherited, so fixed under
/// the no-known-bugs rule.
/// </para>
/// <para>
/// A deletion is a pattern item absent from the text (<c>upstream/README.rst</c>:538-566), so
/// <c>(?:a){d&lt;=1}</c> has the paths of <c>(?:a|)</c>: the <c>a</c> first, then nothing. The
/// search is a complete, ordered depth-first one: the deletion of an exact item is tried as soon as
/// everything after its exact match has failed, before any earlier choice, which is where upstream
/// tries the errors of an item that fails (README.rst:609). Every upstream answer below was
/// measured on <c>regex</c> 2026.9.10 on 2026-09-26 and is quoted beside its assertion; the port's
/// answers on patterns the reference matcher covers equal
/// <c>tools/probes/fuzzy-reference-matcher.py</c>'s.
/// </para>
/// </remarks>
public sealed class FuzzyExactDeletionTests
{
    private static void ShouldMatch(Match m, int index, int length, FuzzyCounts counts)
    {
        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void An_item_that_matched_exactly_is_deleted_when_the_rest_of_the_pattern_fails()
    {
        // match('(?:a){d<=1}a', 'a')        None
        // fullmatch('(?:ab){d<=1}b', 'ab')  None
        // match('(?:aa){d<=1}a', 'aa')      None
        // match('(?:a+){d<=1}a', 'a')       None
        // match('(?:1){e<=1}[0-9]', '1x')   None
        ShouldMatch(new FuzzyRegex("(?:a){d<=1}a").MatchAtStart("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:ab){d<=1}b").FullMatch("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:aa){d<=1}a").MatchAtStart("aa"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:a+){d<=1}a").MatchAtStart("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:1){e<=1}[0-9]").MatchAtStart("1x"), 0, 1, new FuzzyCounts(0, 0, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Every_item_after_the_exact_one_may_be_deleted_too()
    {
        // match('(?:aa){e<=2}a', 'a')          None
        // search('(?:\w+ ){d<=2}\d', '5')      None
        // search('(?:a+b){d<=2}a', 'ab')       None
        ShouldMatch(new FuzzyRegex("(?:aa){e<=2}a").MatchAtStart("a"), 0, 1, new FuzzyCounts(0, 0, 2));
        ShouldMatch(new FuzzyRegex(@"(?:\w+ ){d<=2}\d").Match("5"), 0, 1, new FuzzyCounts(0, 0, 2));
        ShouldMatch(new FuzzyRegex("(?:a+b){d<=2}a").Match("ab"), 0, 1, new FuzzyCounts(0, 0, 2));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Every_kind_of_item_is_retried()
    {
        // search('(?r)a(?:a){d<=1}', 'a')             None   one character, reversed
        // match('(?:[ab]){d<=1}b', 'b')               None   a set
        // match('(?i)(?:AB){d<=1}b', 'ab')            None   a case-insensitive string
        // match('(?fi)(?:straße){d<=1}e', 'strasse')  None   a full-case-folded string
        // match('(?fi)(?:.st){d<=1}', 'ﬆ')       None   a folded string taking a ligature
        // match('(ab)(?:\1){d<=1}b', 'abab')          None   a group reference
        // match('(?fi)(a)(?:\1){d<=1}a', 'aA')        None   a full-case-folded group reference
        // search('(?r)a(?:\1){d<=1}(a)', 'aa')        None   a group reference, reversed
        ShouldMatch(new FuzzyRegex("(?r)a(?:a){d<=1}").Match("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:[ab]){d<=1}b").MatchAtStart("b"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?i)(?:AB){d<=1}b").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?fi)(?:straße){d<=1}e").MatchAtStart("strasse"), 0, 7, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?fi)(?:.st){d<=1}").MatchAtStart("ﬆ"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex(@"(ab)(?:\1){d<=1}b").MatchAtStart("abab"), 0, 4, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex(@"(?fi)(a)(?:\1){d<=1}a").MatchAtStart("aA"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex(@"(?r)a(?:\1){d<=1}(a)").Match("aa"), 0, 2, new FuzzyCounts(0, 0, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void The_deletion_is_tried_before_any_earlier_choice()
    {
        // search('(?:(?:a){d<=1}ab|a)', 'ab')  (0, 1) (0, 0, 0)
        // match('(?:ab){e<=2}b', 'bb')         (0, 2) (0, 0, 1)
        // match('(?:(?:ab){d<=1}b|abx)', 'ab') None
        // In the first row the fuzzy 'a' matches, then 'a' fails on 'b'; deleting the fuzzy 'a' is
        // the most recent choice, so it comes before the second branch. In the second, 'a'
        // fails and is substituted by the first 'b', the fuzzy 'b' matches the second, and the
        // literal 'b' finds the end: deleting the exact fuzzy 'b' is more recent than the
        // substitution, so it is taken before trying the deletion of 'a' that upstream finds.
        ShouldMatch(new FuzzyRegex("(?:(?:a){d<=1}ab|a)").Match("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:ab){e<=2}b").MatchAtStart("bb"), 0, 2, new FuzzyCounts(1, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:(?:ab){d<=1}b|abx)").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void An_earlier_start_is_found_first()
    {
        // search('(?:ab){d<=1}b', 'abxabb')  (3, 6) (0, 0, 0)
        // search('(?:ab){d<=1}b', 'abab')    None
        // search('(?:ab){d<=1}b', 'xab')     None
        ShouldMatch(new FuzzyRegex("(?:ab){d<=1}b").Match("abxabb"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:ab){d<=1}b").Match("abab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:ab){d<=1}b").Match("xab"), 1, 2, new FuzzyCounts(0, 0, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. A (*SKIP) reached through
    // the deletion path is honoured, as it is in the same pattern spelt without fuzzy matching:
    // Perl 5.42.3 and regex 2026.9.10 both give None for '(?:a|)ab(*SKIP)(*FAIL)|a' over 'ab'.
    [Test]
    public void A_skip_reached_through_a_deletion_is_honoured()
    {
        // search('(?:(?:a){d<=1}ab(*SKIP)(*FAIL)|b)', 'ab')  (1, 2) (0, 0, 0)
        // search('(?:(?:a){d<=1}ab(*SKIP)(*FAIL)|a)', 'ab')  (0, 1) (0, 0, 0)
        new FuzzyRegex("(?:(?:a){d<=1}ab(*SKIP)(*FAIL)|b)")
            .Match("ab")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?:(?:a){d<=1}ab(*SKIP)(*FAIL)|a)").Match("ab").Success.Should().BeFalse();
        new FuzzyRegex("(?:a|)ab(*SKIP)(*FAIL)|a").Match("ab").Success.Should().BeFalse();
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void The_retry_serves_a_minimum_error_count_and_the_better_match_modes()
    {
        // match('(?:ab){1<=e<=1}b', 'ab')     None
        // match('(?e)(?:ab){d<=1}b', 'ab')    None
        // match('(?b)(?:ab){d<=1}b', 'ab')    None
        ShouldMatch(new FuzzyRegex("(?:ab){1<=e<=1}b").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?e)(?:ab){d<=1}b").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?b)(?:ab){d<=1}b").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: rows from the 2026-09-26
    // grid on which the port on main (b70ba32) differed from the reference matcher.
    [Test]
    public void Grid_rows_follow_the_reference_matcher()
    {
        // search('(?:.+?c){d<=1}c*[^a]+?', 'ccaaa')                None
        // fullmatch('(.{0,2}?|[^a]+[^a]){d<=1}.', 'ccbc')          (0, 4) group 1 (0, 3) (0, 0, 0)
        // match('[ab]*?(?:b){d<=1}b[ab]', 'baabc')                 None
        // match('(b){e<=1}[ab]+(*SKIP)|.*?a*?', 'bccac')           (0, 0) (0, 0, 0)
        // match('b+([^a]c*?){i<=1,d<=1}[^a](*PRUNE)(*F).|c?b{1,2}', 'bbaa')  (0, 2) (0, 0, 0)
        ShouldMatch(new FuzzyRegex("(?:.+?c){d<=1}c*[^a]+?").Match("ccaaa"), 0, 2, new FuzzyCounts(0, 0, 1));

        Match grouped = new FuzzyRegex("(.{0,2}?|[^a]+[^a]){d<=1}.").FullMatch("ccbc");
        ShouldMatch(grouped, 0, 4, new FuzzyCounts(0, 0, 1));
        (grouped.Groups[1].Index, grouped.Groups[1].Length).Should().Be((0, 3));

        ShouldMatch(new FuzzyRegex("[ab]*?(?:b){d<=1}b[ab]").MatchAtStart("baabc"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(
            new FuzzyRegex("(b){e<=1}[ab]+(*SKIP)|.*?a*?").MatchAtStart("bccac"),
            0,
            1,
            new FuzzyCounts(0, 0, 1)
        );
        new FuzzyRegex("b+([^a]c*?){i<=1,d<=1}[^a](*PRUNE)(*F).|c?b{1,2}")
            .MatchAtStart("bbaa")
            .Success.Should()
            .BeFalse();
    }

    // AGREES WITH UPSTREAM 2026.9.10: the controls.
    [Test]
    public void Answers_upstream_already_gets_right_are_unchanged()
    {
        // fullmatch('(?:cats|cat){e<=1}', 'cat')               (0, 3) (0, 0, 1)   README.rst:609
        // search('(fuu){i<=2,d<=2,e<=5}', 'anaconda foo bar')  (7, 10) (0, 2, 2)  README.rst:622
        // match('(?:a|b){d<=1}a', 'a')                         (0, 1) (0, 0, 1)
        // search('(?:hello){e<=1} world', 'say hell world')    (4, 14) (0, 0, 1)
        ShouldMatch(new FuzzyRegex("(?:cats|cat){e<=1}").FullMatch("cat"), 0, 3, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(fuu){i<=2,d<=2,e<=5}").Match("anaconda foo bar"), 7, 3, new FuzzyCounts(0, 2, 2));
        ShouldMatch(new FuzzyRegex("(?:a|b){d<=1}a").MatchAtStart("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:hello){e<=1} world").Match("say hell world"), 4, 10, new FuzzyCounts(0, 0, 1));
    }

    // The run the exact-deletion narrowing measures (Matcher.ExactDeletionMayMatch): a chain of
    // fuzzy strings and one-character items, ended by anything else. In (?:ab[cd]e+){e<=2}x the
    // string "ab" and the set make a run of three, and the repeat ends it; the run's exit is the
    // node after it that reads text.
    [Test]
    public void A_run_is_the_chain_of_strings_and_one_character_items()
    {
        var regex = new FuzzyRegex("(?:ab[cd]e+x){e<=2}y");
        Node ab = regex.PatternObject.NodeList.First(static node => node.Op == Opcode.String);
        Node set = regex.PatternObject.NodeList.First(static node => node.Op == Opcode.SetUnion);

        ab.FuzzyRunLength.Should().Be(4, "'ab', the set, and the 'e' the compiler writes before e*");
        set.FuzzyRunLength.Should().Be(2);
        ab.FuzzyRunExit.Should().NotBeNull();
        ab.FuzzyRunExit.Op.Should().Be(Opcode.GreedyRepeat);
    }

    // The narrowing is off where its exchange argument fails: a test on which characters an error
    // may touch, and a verb that a left-out choice could have reached.
    [Test]
    public void The_narrowing_is_off_where_its_argument_fails()
    {
        new FuzzyRegex("(?:abc){e<=2}").PatternObject.NarrowExactDeletions.Should().BeTrue();
        // A minimum does not turn it off: the matcher narrows once every minimum is met.
        new FuzzyRegex("(?:abc){1<=e<=2}")
            .PatternObject.NarrowExactDeletions.Should()
            .BeTrue();
        new FuzzyRegex("(?:abc){1<=e<=2}").PatternObject.HasFuzzyMinimum.Should().BeTrue();
        new FuzzyRegex("(?:abc){e<=2:[a-z]}").PatternObject.NarrowExactDeletions.Should().BeFalse();
        new FuzzyRegex("(?:abc){e<=2}(*SKIP)x|y").PatternObject.NarrowExactDeletions.Should().BeFalse();
        new FuzzyRegex("(?:abc){e<=2}(*PRUNE)x|y").PatternObject.NarrowExactDeletions.Should().BeFalse();
    }

    // The narrowing leaves out an exact item's deletion when a match that deletes it exchanges for
    // one that keeps it and uses the character elsewhere. When the character goes to a trailing
    // insertion past the end of a capture group, the exchange moves the group's end, and a later
    // backreference or conditional that reads the group can then fail: in the first row the only
    // match deletes the fuzzy 'a', so group 1 is empty, and inserts the 'a' after the group. So the
    // run's exit is not looked for past the boundary of a tested group. Found by the blind review
    // of af59be7 (2026-09-26); every answer is the reference matcher's, or the port's with the
    // narrowing off.
    [Test]
    public void The_narrowing_does_not_cross_the_boundary_of_a_tested_group()
    {
        Match m = new FuzzyRegex(@"(?:(a)){e<=2}b\1").Match("ab");
        ShouldMatch(m, 0, 2, new FuzzyCounts(0, 1, 1));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 0));

        ShouldMatch(new FuzzyRegex(@"(?:(a)){e<=2}b\1").MatchAtStart("ab"), 0, 2, new FuzzyCounts(0, 1, 1));
        ShouldMatch(new FuzzyRegex(@"(?:(a)){i<=1,d<=1}b\1").Match("ab"), 0, 2, new FuzzyCounts(0, 1, 1));
        ShouldMatch(new FuzzyRegex(@"(?i)a(?:(a*b)){e<=2}(a)\1").FullMatch("abA"), 0, 3, new FuzzyCounts(0, 1, 1));
        ShouldMatch(new FuzzyRegex(@"(?be)a(?:([ab])){e<=2}(a)\1").MatchAtStart("aba"), 0, 3, new FuzzyCounts(0, 1, 1));
        ShouldMatch(new FuzzyRegex(@"(?fi)(?:(\w)){i<=1,d<=1}a\1").Match("baa"), 0, 2, new FuzzyCounts(0, 1, 1));
    }
}
