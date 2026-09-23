using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// S60b item 10: the reject-only prefilter for a pattern that is one fuzzy literal, such as
/// <c>(?:amber lantern works){e&lt;=2}</c>. See <see cref="FuzzyLiteralFilter"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the filter is.</b> A match with at most <c>k</c> errors leaves at least one of
/// <c>k + 1</c> disjoint pieces of the literal untouched, because one edit can damage only one
/// piece (Navarro, <i>A Guided Tour to Approximate String Matching</i>, ACM Computing Surveys 33(1),
/// 2001, section 8.1). So the search can skip every start position no untouched piece could belong
/// to, and refuse a subject holding no piece at all. It never chooses a match; the engine still
/// makes every attempt it would have made at the positions that are left.
/// </para>
/// <para>
/// <b>These pins are permanent.</b> The filter only makes searches faster, so every answer below is
/// the answer without it. Each row is built so that one specific mistake in the filter would change
/// it: a case fold the piece search cannot see, a piece offset or the error budget left out of the
/// start bound, a slice bound read wrongly, a partial match refused.
/// </para>
/// <para>
/// <b>Provenance.</b> Every expected value is upstream's, recorded against <c>regex 2026.9.10</c>
/// by <c>python tools/probes/upstream-fuzzy-literal-prefilter.py</c> on 2026-09-23, and quoted
/// beside the assertion. The probe runs every call with <c>regex.VERSION1</c>, this port's default;
/// under upstream's own default, V0, <c>(?i)</c> does not fold <c>ß</c> or <c>ﬁ</c>. Upstream prints
/// fuzzy counts as (substitutions, insertions, deletions).
/// </para>
/// </remarks>
public sealed class FuzzyLiteralPrefilterTests
{
    private static string Search(
        string pattern,
        string subject,
        int beginning = 0,
        int length = -1,
        bool partial = false
    )
    {
        FuzzyRegex regex = pattern.Contains(@"\L<phrases>", StringComparison.Ordinal)
            ? new FuzzyRegex(
                pattern,
                FuzzyRegexOptions.None,
                new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal) { ["phrases"] = _phrases }
            )
            : new FuzzyRegex(pattern);
        Match match = regex.Match(subject, beginning, length, partial);
        return match.Success
            ? $"({match.Index},{match.Index + match.Length}) {match.FuzzyCounts.Substitutions},{match.FuzzyCounts.Insertions},{match.FuzzyCounts.Deletions}"
            : "None";
    }

    private static readonly string[] _phrases = ["amber lantern works", "copper field studio", "violet stone archive"];

    private static PatternObject Build(string source) =>
        PatternObject.Compile(
            PatternCompiler.Compile(
                source,
                0,
                source.Contains(@"\L<phrases>", StringComparison.Ordinal)
                    ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["phrases"] = _phrases }
                    : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                PatternCompiler.DefaultVersion
            ),
            source
        );

    // ---------------------------------------------------------------------------------------
    // Which patterns get a filter.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_case_insensitive_fuzzy_phrase_gets_a_filter_with_one_more_piece_than_its_error_budget()
    {
        FuzzyLiteralFilter? filter = Build("(?i)(?:amber lantern works){e<=2}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.MaxErrors.Should().Be(2);
        filter.Pieces.Should().Equal("amber ", "lanter", "n works");
        filter.Offsets.Should().Equal(0, 6, 12);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_literal_full_case_folding_splits_into_several_nodes_is_one_literal_to_the_filter()
    {
        // '(?i)strasse lane' compiles to STRING_FLD 'st', STRING_IGN 'ra', STRING_FLD 'ss',
        // STRING_IGN 'e lane', so that 'ß' and U+FB06 can match. 'st', 'ss' and 'fi' are common
        // enough in English that a filter taking only one node would miss most phrases.
        FuzzyLiteralFilter? filter = Build("(?i)(?:strasse lane){e<=1}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().Equal("strass", "e lane");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_chain_is_read_back_into_the_literal_s_own_order()
    {
        // Under (?r) the chain runs from the literal's end: 'ne', then 'fi', then 'one ', then 'st'.
        FuzzyLiteralFilter? filter = Build("(?fi)(?r)(?:stone fine){e<=1}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().Equal("stone", " fine");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Every_branch_of_an_alternation_is_cut_into_pieces_of_its_own()
    {
        // A match is one branch with at most k edits, so it holds one of that branch's pieces.
        FuzzyLiteralFilter? filter = Build(
            "(?i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}"
        ).FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter
            .Pieces.Should()
            .Equal("amber ", "lanter", "n works", "copper", " field", " studio", "violet", " stone ", "archive");
        filter.Offsets.Should().Equal(0, 6, 12, 0, 6, 12, 0, 6, 13);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_named_list_is_an_alternation_to_the_filter()
    {
        // Upstream compiles '\L<name>' as a branch of its items, longest first.
        FuzzyLiteralFilter? filter = Build(@"(?i)(?:\L<phrases>){e<=2}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter
            .Pieces.Should()
            .Equal("violet", " stone ", "archive", "amber ", "lanter", "n works", "copper", " field", " studio");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Text_the_branches_share_is_part_of_every_branch_s_literal()
    {
        // The optimiser moves a common prefix out of the branches, and a branch can sit inside the
        // literal; either way each whole literal is cut, not the branch alone.
        Build("(?:amber lantern works|amber stone archive){e<=2}")
            .FuzzyLiteralFilter!.Pieces.Should()
            .Equal("amber ", "lanter", "n works", "amber ", "stone ", "archive");
        Build("(?:amber (?:lantern|stone) works){e<=1}")
            .FuzzyLiteralFilter!.Pieces.Should()
            .Equal("amber lan", "tern works", "amber st", "one works");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_alternation_reads_each_branch_back_into_its_own_order()
    {
        Build("(?fi)(?r)(?:stone fine|oak strasse){e<=1}")
            .FuzzyLiteralFilter!.Pieces.Should()
            .Equal("stone", " fine", "oak s", "trasse");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_error_budget_is_the_tightest_of_the_total_the_per_kind_limits_and_the_cost_equation()
    {
        // e<=5 alone would give six pieces; the per-kind limits allow only 1 + 1 + 0.
        Build("(?:abcdefghijkl){e<=5,i<=1,d<=1,s<=0}").FuzzyLiteralFilter!.MaxErrors.Should().Be(2);

        // A cost equation: each error costs at least 2, and the total may not exceed 3.
        Build("(?:abcdefghijkl){2i+2d+3s<=3}").FuzzyLiteralFilter!.MaxErrors.Should().Be(1);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?:café au lait){e<=1}")] // a literal that is not ASCII
    [Arguments("(?:abcdef){e<=3}")] // pieces too short to be worth searching for
    [Arguments("(?:abcdefghijkl){i}")] // no bound on the errors at all
    [Arguments("(?:abcdefghijkl){2i+0d<=4}")] // a free deletion leaves the budget unbounded
    [Arguments("x(?:abcdefghijkl){e<=1}")] // something outside the fuzzy section
    [Arguments("(?:abcdef(ghijkl)){e<=1}")] // a group inside it
    [Arguments("(?:abcdef[gh]ijkl){e<=1}")] // a set inside it
    [Arguments("(?:abcdefghijkl){e<=1:[a-z]}")] // an insertion class
    [Arguments("(?:abcdefghijkl)")] // not fuzzy
    [Arguments("(?:amber lantern [^x]orks){e<=1}")] // a negated character is a class
    [Arguments("(?:amber lantern works|ox){e<=2}")] // one branch too short to cut
    [Arguments("(?:amber lantern works|abcdef[gh]ijkl){e<=1}")] // a set in one branch
    [Arguments("(?:amber lantern works|){e<=1}")] // an empty branch matches anywhere
    [Arguments("(?:(?:amber|lantern|works|stone|field)(?:amber|lantern|works|stone|field)){e<=1}")] // 50 pieces
    public void A_pattern_that_is_not_one_bounded_fuzzy_ascii_literal_gets_no_filter(string pattern)
    {
        Build(pattern).FuzzyLiteralFilter.Should().BeNull();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Thousands_of_alternations_in_a_row_are_refused_without_walking_them_all()
    {
        // The walk recurses at each branch on a path. 8000 in a row overflowed the stack while the
        // pattern compiled. Upstream compiles it: regex.compile(p, regex.VERSION1).search("xx" +
        // "ab" * 8000 + "yy") is (1, 16002), regex 2026.9.10.
        string pattern = "(?:" + string.Concat(Enumerable.Repeat("(?:ab|cd)", 8000)) + "){e<=1}";

        Build(pattern).FuzzyLiteralFilter.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Folds the piece search cannot see. The literal is ASCII, but case-insensitive matching
    // pairs ASCII letters with characters that are not: an ordinal case-insensitive search for
    // "kelvin" does not find it in "Kelvin". Each subject damages every piece but the one
    // holding the fold, so a filter that searched these subjects would refuse them.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    // regex.search(r'(?i)(?:kelvin works){e<=1}', 'Kelvin wxrks') -> span=(0, 12) counts=(1, 0, 0)
    [Arguments("(?i)(?:kelvin works){e<=1}", "Kelvin wxrks", "(0,12) 1,0,0")]
    // regex.search(r'(?V1i)(?:strasse lane){e<=1}', 'straße lxne') -> span=(0, 11) counts=(1, 0, 0)
    [Arguments("(?i)(?:strasse lane){e<=1}", "straße lxne", "(0,11) 1,0,0")]
    // regex.search(r'(?V1i)(?:fine lanterns){e<=1}', 'ﬁne lantxrns') -> span=(0, 12) counts=(1, 0, 0)
    [Arguments("(?i)(?:fine lanterns){e<=1}", "ﬁne lantxrns", "(0,12) 1,0,0")]
    // regex.search(r'(?i)(?:sunset boulevard){e<=1}', 'ſunset boulevxrd') -> span=(0, 16) counts=(1, 0, 0)
    [Arguments("(?i)(?:sunset boulevard){e<=1}", "ſunset boulevxrd", "(0,16) 1,0,0")]
    // regex.search(r'(?i)(?:amber lantern works){e<=2}', 'café amber lantern wxrks') -> span=(4, 24) counts=(1, 1, 0)
    [Arguments("(?i)(?:amber lantern works){e<=2}", "café amber lantern wxrks", "(4,24) 1,1,0")]
    public void A_subject_that_is_not_ascii_is_searched_without_the_filter(
        string pattern,
        string subject,
        string expected
    )
    {
        Search(pattern, subject).Should().Be(expected);
    }

    // ---------------------------------------------------------------------------------------
    // The start bound. A match starting at p holds its untouched piece j no later than
    // p + offset(j) + k, so p can be no earlier than the piece's first occurrence minus both.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_start_bound_steps_back_by_the_piece_offset()
    {
        // 'abcd' is damaged, so 'efgh' at 6 is the only piece found: the bound is 6 - 4 - 1 = 1.
        // Leaving out the offset would start at 5 and miss the match at 2.
        // regex.search(r'(?:abcdefgh){e<=1}', 'zzabXdefgh') -> span=(2, 10) counts=(1, 0, 0)
        Search("(?:abcdefgh){e<=1}", "zzabXdefgh").Should().Be("(2,10) 1,0,0");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_start_bound_steps_back_by_the_whole_error_budget()
    {
        // 'def' at 6 and 'ghi' at 9 both give the bound 1, and the match does start at 1, with
        // both errors inserted before 'def'. A bound one character tighter misses it.
        // regex.search(r'(?:abcdefghi){e<=2}', 'zzaYbcdefghi') -> span=(1, 12) counts=(0, 2, 0)
        Search("(?:abcdefghi){e<=2}", "zzaYbcdefghi").Should().Be("(1,12) 0,2,0");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_pieces_are_searched_from_the_search_position_not_from_the_start_of_the_subject()
    {
        // regex.search(r'(?:abcdefgh){e<=1}', 'abcdefgh zz abcdXfgh', pos=5) -> span=(12, 20) counts=(1, 0, 0)
        Search("(?:abcdefgh){e<=1}", "abcdefgh zz abcdXfgh", beginning: 5).Should().Be("(12,20) 1,0,0");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_pieces_are_searched_only_up_to_the_end_of_the_slice()
    {
        // regex.search(r'(?:abcdefgh){e<=1}', 'abcdefgh zz abcdXfgh', endpos=6) -> None
        Search("(?:abcdefgh){e<=1}", "abcdefgh zz abcdXfgh", length: 6).Should().Be("None");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_subject_holding_no_piece_is_refused()
    {
        // regex.search(r'(?i)(?:amber lantern works){e<=2}', 'a record about something else') -> None
        Search("(?i)(?:amber lantern works){e<=2}", "a record about something else").Should().Be("None");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Every_match_in_a_subject_is_found_when_the_filter_skips_between_them()
    {
        // [m.span() for m in regex.finditer(r'(?i)(?:amber lantern works){e<=2}',
        //     'x amber lantern works yy AMBER LANTRN WORKS zz amberlantern work! qq amber')]
        //   -> [(1, 21), (24, 43), (47, 65)]
        new FuzzyRegex("(?i)(?:amber lantern works){e<=2}")
            .Matches("x amber lantern works yy AMBER LANTRN WORKS zz amberlantern work! qq amber")
            .Select(static m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((1, 21), (24, 43), (47, 65));
    }

    // ---------------------------------------------------------------------------------------
    // Modes. A partial match can be a prefix of the literal and hold no whole piece, so the
    // filter is withheld. Reverse, BESTMATCH and ENHANCEMATCH only choose among matches that
    // each hold an untouched piece, so the filter stays sound under them and stays on.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_partial_match_holding_no_whole_piece_is_still_found()
    {
        // regex.search(r'(?:amber lantern works){e<=2}', 'xx amb', partial=True) -> span=(1, 6) counts=(0, 2, 0) partial=True
        Match match = new FuzzyRegex("(?:amber lantern works){e<=2}").Match("xx amb", partial: true);

        (match.Index, match.Index + match.Length, match.PartialMatch).Should().Be((1, 6, true));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // regex.search(r'(?r)(?:amber lantern works){e<=2}', 'one amber lantxrn works two amber lantern wurks') -> span=(28, 47) counts=(1, 0, 0)
    [Arguments("(?r)(?:amber lantern works){e<=2}", "one amber lantxrn works two amber lantern wurks", "(28,47) 1,0,0")]
    // regex.search(r'(?r)(?:amber lantern works){e<=2}', 'nothing here but amber lantern wurks') -> span=(17, 36) counts=(1, 0, 0)
    [Arguments("(?r)(?:amber lantern works){e<=2}", "nothing here but amber lantern wurks", "(17,36) 1,0,0")]
    // A full-folded literal under (?r): its chain of nodes is walked from the literal's end. Found
    // by the fuzzy-literal oracle generator, seed 7, row 1468.
    // regex.search(r'(?fi)(?r)(?:stone fine){s<=1,i<=1}', 'xebaxsizdrfkSTone Fineoociokw r lo') -> span=(12, 23) counts=(1, 1, 0)
    [Arguments("(?fi)(?r)(?:stone fine){s<=1,i<=1}", "xebaxsizdrfkSTone Fineoociokw r lo", "(12,23) 1,1,0")]
    // regex.search(r'(?r)(?:amber lantern works){e<=2}', 'nothing to see here at all') -> None
    [Arguments("(?r)(?:amber lantern works){e<=2}", "nothing to see here at all", "None")]
    // regex.search(r'(?b)(?:amber lantern works){e<=2}', 'amber lantxrn wxrks and amber lantern works') -> span=(24, 43) counts=(0, 0, 0)
    [Arguments("(?b)(?:amber lantern works){e<=2}", "amber lantxrn wxrks and amber lantern works", "(24,43) 0,0,0")]
    // regex.search(r'(?e)(?:amber lantern works){e<=2}', 'amber lantxrn wxrks and amber lantern works') -> span=(0, 19) counts=(2, 0, 0)
    [Arguments("(?e)(?:amber lantern works){e<=2}", "amber lantxrn wxrks and amber lantern works", "(0,19) 2,0,0")]
    public void Reverse_bestmatch_and_enhancematch_searches_answer_as_upstream_does(
        string pattern,
        string subject,
        string expected
    )
    {
        Search(pattern, subject).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?r)(?:amber lantern works){e<=2}")]
    [Arguments("(?b)(?:amber lantern works){e<=2}")]
    [Arguments("(?e)(?:amber lantern works){e<=2}")]
    [Arguments("(?r)(?:amber lantern works|violet stone archive){e<=2}")]
    [Arguments("(?b)(?:amber lantern works|violet stone archive){e<=2}")]
    [Arguments("(?e)(?:amber lantern works|violet stone archive){e<=2}")]
    public void Reverse_bestmatch_and_enhancematch_keep_the_filter(string pattern)
    {
        Build(pattern).FuzzyLiteralFilter.Should().NotBeNull();
    }

    // ---------------------------------------------------------------------------------------
    // Alternations and named lists. Every branch has pieces of its own, and a subject is refused
    // only when no branch has any piece in it.
    // ---------------------------------------------------------------------------------------

    private const string _three = "(?i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}";

    [Test]
    [Property("Upstream", "none - gap test")]
    // regex.search(r'(?V1i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}', 'note: VIOLXT STONE ARCHIVX here') -> span=(6, 26) counts=(2, 0, 0)
    [Arguments(_three, "note: VIOLXT STONE ARCHIVX here", "(6,26) 2,0,0")]
    // regex.search(r'(?V1i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}', 'nothing of interest here at all') -> None
    [Arguments(_three, "nothing of interest here at all", "None")]
    // regex.search(r'(?V1i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}', 'xx vioXet stXne archive') -> span=(3, 23) counts=(2, 0, 0)
    [Arguments(_three, "xx vioXet stXne archive", "(3,23) 2,0,0")]
    // regex.compile(r'(?V1i)(?:\L<phrases>){e<=2}', phrases=[...]).search('the COPPER FIELD STUDXO') -> span=(3, 23) counts=(1, 1, 0)
    [Arguments(@"(?i)(?:\L<phrases>){e<=2}", "the COPPER FIELD STUDXO", "(3,23) 1,1,0")]
    // regex.compile(r'(?V1i)(?:\L<phrases>){e<=2}', phrases=[...]).search('nothing of interest here at all') -> None
    [Arguments(@"(?i)(?:\L<phrases>){e<=2}", "nothing of interest here at all", "None")]
    // regex.search(r'(?V1)(?:amber lantern works|amber stone archive){e<=2}', 'xx ambXr stone archXve') -> span=(3, 22) counts=(2, 0, 0)
    [Arguments("(?:amber lantern works|amber stone archive){e<=2}", "xx ambXr stone archXve", "(3,22) 2,0,0")]
    // regex.search(r'(?V1)(?:amber (?:lantern|stone) works){e<=1}', 'an ambXr stone works') -> span=(3, 20) counts=(1, 0, 0)
    [Arguments("(?:amber (?:lantern|stone) works){e<=1}", "an ambXr stone works", "(3,20) 1,0,0")]
    // regex.search(r'(?V1r)(?:amber lantern works|violet stone archive){e<=2}', 'violet stone archive and amber lantrn works') -> span=(25, 43) counts=(1, 0, 1)
    [Arguments(
        "(?r)(?:amber lantern works|violet stone archive){e<=2}",
        "violet stone archive and amber lantrn works",
        "(25,43) 1,0,1"
    )]
    // regex.search(r'(?V1i)(?:oak stone field|kelvin works){e<=1}', 'Kelvin wxrks') -> span=(0, 12) counts=(1, 0, 0)
    [Arguments("(?i)(?:oak stone field|kelvin works){e<=1}", "Kelvin wxrks", "(0,12) 1,0,0")]
    // regex.search(r'(?V1bi)(?:amber lantern works|violet stone archive){e<=2}', 'amber lantxrn works and violet stone archive') -> span=(24, 44) counts=(0, 0, 0)
    [Arguments(
        "(?b)(?i)(?:amber lantern works|violet stone archive){e<=2}",
        "amber lantxrn works and violet stone archive",
        "(24,44) 0,0,0"
    )]
    public void An_alternation_or_named_list_answers_as_upstream_does(string pattern, string subject, string expected)
    {
        Search(pattern, subject).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void An_alternation_s_pieces_are_searched_from_the_search_position()
    {
        // regex.search(r'(?V1i)(?:amber lantern works|copper field studio|violet stone archive){e<=2}', 'copper field studio, violet stone archive', pos=3) -> span=(19, 41) counts=(0, 2, 0)
        Search(_three, "copper field studio, violet stone archive", beginning: 3).Should().Be("(19,41) 0,2,0");
    }
}
