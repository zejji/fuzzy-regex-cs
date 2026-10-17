using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// While a fuzzy section is below a minimum error count, an item that matched exactly is also tried
/// with a text character inserted in front of it, so that a later item can take the error the
/// minimum asks for.
/// </summary>
/// <remarks>
/// <para>
/// Known defect D94, inherited from upstream. Errors are tried on an item only when it fails to match
/// (<c>fuzzy_match_item</c>, <c>upstream/src/_regex.c</c>:10185-10258), so in
/// <c>(?:a){1&lt;=s&lt;=1,i&lt;=1}</c> over 'ab' the <c>a</c> matches, the section ends with no
/// substitution and the match fails, although inserting the 'a' and substituting the 'b' for the
/// item is a fit with exactly the errors the section allows. Over 'ba', the mirror image, the
/// substitution comes first and the trailing insertion of ledger entry 51 completes it. A minimum
/// says the match holds at least that many errors (<c>upstream/README.rst</c>:538-566), and an
/// insertion may stand between any two items, so the first fit is missed only because of the order
/// in which errors are tried. No surveyed engine has fuzzy minimums.
/// </para>
/// <para>
/// The expected answers are those of <c>tools/probes/fuzzy-reference-matcher.py</c> with its rule 6a.
/// Upstream answers quoted below were measured on <c>regex</c> 2026.9.10 on 2026-10-17; the rows
/// named m6 to m105 are battery run 2's (<c>.scratch/battery/20261016-2/triage-matrix.md</c>, R2).
/// </para>
/// </remarks>
public sealed class FuzzyMinimumInsertionBeforeExactItemTests
{
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream: None on every row.
    [Test]
    [Arguments(@"(?:a){1<=s<=1,i<=1}", "ab", 1, 1)]
    [Arguments(@"(?:a){1<=s<=1,1<=i<=1}", "ab", 1, 1)]
    [Arguments(@"(?:ab){1<=s<=1,i<=1}", "abb", 1, 1)]
    [Arguments(@"(?:a[ab]){1<=s<=1,i<=1}", "aax", 1, 1)]
    [Arguments(@"(?:[ab]){1<=s<=1,i<=1}", "ax", 1, 1)]
    [Arguments(@"(?:\w){1<=s<=1,i<=1}", "a!", 1, 1)]
    [Arguments(@"(?:(a)){1<=s<=1,i<=1}", "ab", 1, 1)]
    [Arguments(@"(a)(?:\1){1<=s<=1,i<=1}", "aab", 1, 1)]
    [Arguments(@"(?i)(?:a){1<=s<=1,i<=1}", "Ab", 1, 1)]
    [Arguments(@"(?i)(?:ab){1<=s<=1,i<=1}", "ABB", 1, 1)]
    [Arguments(@"(?fi)(?:ab){1<=s<=1,i<=1}", "ABB", 1, 1)]
    [Arguments(@"(?fi)(?:ss){1<=s<=1,i<=1}", "ssx", 1, 1)]
    [Arguments(@"(?fi)(ss)(?:\1){1<=s<=1,i<=1}", "ssssx", 1, 1)]
    [Arguments(@"(?fi)(ab)(?:\1){1<=s<=1,i<=1}", "ababb", 1, 1)]
    [Arguments(@"(?r)(?:a){1<=s<=1,i<=1}", "ba", 1, 1)]
    [Arguments(@"(?r)(?:ab){1<=s<=1,i<=1}", "aab", 1, 1)]
    [Arguments(@"(?:aa){2<=s<=2,i<=2}", "aabb", 2, 2)]
    [Arguments(@"(?:a){1<=s<=1,i<=2}", "aab", 1, 2)]
    [Arguments(@"(?:(?:a){1<=s<=1,i<=1})+", "ab", 1, 1)]
    // Battery run 2: m77, m89 (F|I), m12.
    [Arguments(@"(?:(?:[ab]){1<=s<=1,i<=1}){1,2}+", "axx", 2, 1)]
    [Arguments(@"(?fi)(?:ba+){1<=s<=1,i<=1}", "baa", 1, 1)]
    [Arguments(@"^(?:ba){1<=s<=1,i<=1}(?:x){0,2}", "bax", 1, 1)]
    public void A_full_match_inserts_in_front_of_an_exact_item(
        string pattern,
        string text,
        int substitutions,
        int insertions
    )
    {
        Match m = new FuzzyRegex(pattern).FullMatch(text);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, text.Length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream: None on every row,
    // except m76's (0, 0) with no errors, the optional's empty exit.
    [Test]
    [Arguments(@"(?:a){1<=s<=1,i<=1}", "ab", 2)]
    [Arguments(@"(?:ab){1<=s<=1,i<=1}c", "abbc", 4)]
    // Battery run 2: m74 (V1), m76, m78, m105 (POSIX).
    [Arguments(@"(?V1)(?>a*b)(?:ax){1<=s<=1,i<=1}", "baxx", 4)]
    [Arguments(@"(?:(?:(?:xa){1<=s<=1,i<=1}){1,2}+)?+", "xaa", 3)]
    [Arguments(@"(?:(?:.b)+?(?:ab|a)){1<=s<=1,i<=1}", "bbab", 4)]
    [Arguments(@"(?p)(?:(?:ab){1,2}?x){1<=s<=1,i<=1}", "abxx", 4)]
    public void A_match_at_the_start_inserts_in_front_of_an_exact_item(string pattern, string text, int length)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_match_whose_start_was_moved_by_a_keep_out_still_counts_the_insertion()
    {
        // m83: match('(?:(?:ab|a)){1<=s<=1,i<=1}a+ba\Kb?', 'abaaba')  None
        Match m = new FuzzyRegex(@"(?:(?:ab|a)){1<=s<=1,i<=1}a+ba\Kb?").MatchAtStart("abaaba");

        (m.Success, m.Index, m.Length).Should().Be((true, 6, 0));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_group_inside_the_section_keeps_its_span()
    {
        // m14, flags F|I: match('(?:(?P<g1>b)(?!.?\g<1>)[ab]){1<=e<=2}', 'bxb')  None
        Match m = new FuzzyRegex(@"(?fi)(?:(?P<g1>b)(?!.?\g<1>)[ab]){1<=e<=2}").MatchAtStart("bxb");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 2));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream finds a later or
    // shorter match on every row (quoted).
    [Test]
    // m6: None.
    [Arguments(@"(?:(?!x)[ab]){1<=s<=1,i<=1}", "xaxbxxbb", 1, 2)]
    // m19: (1, 5) with one substitution and one insertion.
    [Arguments(@"(?:(?:(?>b)ba){1<=s<=1,i<=1}){1,2}+", "bbabba", 0, 4)]
    public void A_search_finds_the_earlier_fit(string pattern, string text, int index, int length)
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Length).Should().Be((true, index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    // m18: (3, 5), one substitution, group 1 (3, 3).
    [Arguments(@"(?|(a+ab)|(b?))(?:(?<=a*)a.){1<=s<=1,i<=1}", "aaabab", 2, 5, 2, 2)]
    // m82: (1, 4), one substitution, group 1 (3, 4).
    [Arguments(@"(?:(?>b)b){1<=s<=1,i<=1}(?|(a(?:ab|a))|(\w))", "bbaab", 0, 4, 3, 4)]
    // m100: (2, 6), one substitution, group 1 (2, 3).
    [Arguments(@"(?|((?:ab|a))(?<=x)(?:ab|a)[ab]|(b))(?:ab[ab]){1<=s<=1,i<=1}", "babbbb", 0, 5, 0, 1)]
    public void A_search_with_a_group_finds_the_earlier_fit(
        string pattern,
        string text,
        int start,
        int end,
        int groupStart,
        int groupEnd
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((groupStart, groupEnd));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_scan_takes_the_earlier_fit()
    {
        // m91, flags F|I: finditer('(?:\w(?:ab|a)){1<=s<=1,i<=1}', 'aabbbx')
        //                 (1, 4) and (4, 6), each with one substitution
        new FuzzyRegex(@"(?fi)(?:\w(?:ab|a)){1<=s<=1,i<=1}")
            .Matches("aabbbx")
            .Select(static m => (m.Index, m.Index + m.Length, m.FuzzyCounts))
            .Should()
            .Equal((0, 4, new FuzzyCounts(1, 1, 0)), (4, 6, new FuzzyCounts(1, 0, 0)));
    }

    // Upstream and this port agree on every row: the controls. They are the mirror images, where the
    // substitution comes first, a minimum an insertion meets at the end of the section (ledger
    // entry 51), a search that cannot insert where it began and so substitutes one character later,
    // and sections with no minimum.
    [Test]
    [Arguments(@"(?:a){1<=s<=1,i<=1}", "ba", false, 0, 2, 1, 1)]
    [Arguments(@"(?:ab){1<=s<=1,i<=1}", "aab", false, 0, 3, 1, 1)]
    [Arguments(@"(?:a){1<=e<=2}", "ab", false, 0, 2, 0, 1)]
    [Arguments(@"(?:ab){1<=e<=1}", "aab", false, 0, 3, 0, 1)]
    [Arguments(@"(?:a){1<=s<=1,i<=1}", "xab", true, 0, 1, 1, 0)]
    [Arguments(@"(?:a+){1<=s<=1,i<=1}", "ab", false, 0, 2, 1, 0)]
    [Arguments(@"(?:a|c){1<=s<=1,i<=1}", "ab", false, 0, 2, 1, 1)]
    [Arguments(@"(?:a){i<=1,s<=1}", "ab", false, 0, 2, 0, 1)]
    [Arguments(@"(?fi)(ab)(?:\1){1<=s<=1,i<=1}", "abaab", false, 0, 5, 1, 1)]
    public void A_fit_found_without_the_new_insertion_keeps_its_answer(
        string pattern,
        string text,
        bool search,
        int start,
        int end,
        int substitutions,
        int insertions
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match(text) : regex.FullMatch(text);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, 0));
    }

    // Upstream and this port agree. A search may not insert where it began (reference rule 5), so
    // each 'a' is passed over and each 'b' substituted.
    [Test]
    public void A_scan_does_not_insert_where_each_attempt_began()
    {
        new FuzzyRegex(@"(?:a){1<=s<=1,i<=1}")
            .Matches("abab")
            .Select(static m => (m.Index, m.Index + m.Length, m.FuzzyCounts))
            .Should()
            .Equal((1, 2, new FuzzyCounts(1, 0, 0)), (3, 4, new FuzzyCounts(1, 0, 0)));
    }

    // Upstream and this port agree: there is no fit. A substitution never replaces a character
    // with one the item matches, so '.' has nothing to substitute; one item cannot take two
    // substitutions; and an insertion cannot meet a deletion minimum.
    [Test]
    [Arguments(@"(?:.){1<=s<=1,i<=1}", "ab")]
    [Arguments(@"(?:a){2<=s<=2,i<=2}", "aaab")]
    [Arguments(@"(?:a){1<=d<=1,i<=1}", "ab")]
    public void No_fit_is_no_match(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text).Success.Should().BeFalse();
    }

    // Bounded in engine steps, not seconds (D13). Every character is one each item accepts, so no
    // substitution can ever be taken and the insertions D94 adds in front of each 'a' cannot help.
    // Measured in Debug on 2026-10-17 over 200 characters: 142,908 steps before D94, 1,151,234 with
    // D94 alone (the i<=2 row; 502,099 for the others), 143,308 with the pruning. Each bound is
    // about twice the pruned count.
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:a+){1<=s<=1,i<=2}", "a")]
    [Arguments(@"(?:a+){1<=s<=1,i<=1}", "a")]
    [Arguments(@"(?:[a-z]+){1<=s<=1,i<=1}", "hello")]
    public void Insertions_are_not_tried_where_no_substitution_can_follow(string pattern, string unit)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = string.Concat(Enumerable.Repeat(unit, 200 / unit.Length));

        EngineWork.ShouldTakeAtMostSteps(
            () => regex.Match(text).Success.Should().BeFalse(),
            300_000,
            "an insertion is skipped when no later character can take the missing substitution"
        );
    }
}
