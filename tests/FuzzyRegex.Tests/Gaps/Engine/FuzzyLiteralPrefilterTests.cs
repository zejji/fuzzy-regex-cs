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
        // literal; either way each whole literal is cut, not the branch alone. A piece two literals
        // share, 'amber ' here, is searched for once.
        Build("(?:amber lantern works|amber stone archive){e<=2}")
            .FuzzyLiteralFilter!.Pieces.Should()
            .Equal("amber ", "lanter", "n works", "stone ", "archive");
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
    public void A_fuzzy_run_holding_a_sharp_s_keeps_its_filter()
    {
        // Ledger entry 49 gives the run a second arm that reads the 'ß' as one character. Both arms
        // are the one literal 'strasse lane', cut only between pattern characters, so never inside
        // the 'ss' that is the 'ß'.
        FuzzyLiteralFilter? filter = Build("(?fi)(?:straße lane){e<=1}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().Equal("strass", "e lane");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_run_of_many_expanding_characters_is_one_literal_cut_between_them()
    {
        // Enumerating each 'ß's choice of itself or its folding gave 2^8 + 1 literals, more pieces
        // than MaxPieces, and the filter went off from four on (the blind review of 251afa0). Eight
        // characters, k = 2: three pieces of 2, 3 and 3 characters, never half an 'ss'.
        FuzzyLiteralFilter? filter = Build("(?fi)(?:ßßßßßßßß){e<=2}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().Equal("ssss", "ssssss");
        filter.Offsets.Should().Equal(0, 10);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_search_for_a_run_of_many_expanding_characters_through_ascii_text_stays_fast()
    {
        // 6,165 ms without the filter on Release, 1.7 ms before ledger entry 49 (the blind review
        // of 251afa0). Counted in engine steps rather than timed, because a busy machine is not a
        // slow engine (D13): the filter refuses every position, so the matching loop takes no
        // steps at all (Debug, 2026-09-28), and without it every position is a fuzzy attempt.
        var regex = new FuzzyRegex("(?fi)(?:ßßßßßßßß){e<=2}", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = string.Concat(Enumerable.Repeat("die strasse haus ", 12_000));
        int count = -1;

        EngineWork.ShouldTakeAtMostSteps(
            () => count = regex.Matches(subject).Count,
            100_000,
            "the filter, not the matcher, rules out the positions"
        );

        count.Should().Be(0);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // Ledger entry 49; the expected values are this port's, argued there. Upstream: None for the
    // first three, and (0, 12) with one substitution for the last, regex 2026.9.10, 2026-09-28.
    // The whole 'ß' substituted, next to a piece the search finds.
    [Arguments("(?fi)(?:straße lane){e<=1}", "straxe lane", "(0,11) 1,0,0")]
    // The whole 'ß' deleted.
    [Arguments("(?fi)(?:straße lane){e<=1}", "strae lane", "(0,10) 0,0,1")]
    // The only untouched piece is on the one-character path, and it is not the one holding 'ß'.
    [Arguments("(?fi)(?:straße lane){e<=1}", "a strae lane", "(2,12) 0,0,1")]
    [Arguments("(?fi)(?:straße lane){e<=1}", "xtrasse lane", "(0,12) 1,0,0")]
    public void A_sharp_s_edited_as_one_character_is_found_through_the_filter(
        string pattern,
        string subject,
        string expected
    )
    {
        Build(pattern).FuzzyLiteralFilter.Should().NotBeNull();
        Search(pattern, subject).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_lone_character_an_ascii_letter_equals_ignoring_case_switches_the_filter_off()
    {
        // Full case folding cuts 'oak strassK' after its 'ss', so KELVIN SIGN is a CHARACTER_IGN node
        // of its own, and the engine pairs it with 'k'. The only piece the subject holds untouched is
        // 'trassK', which an ordinal search does not see in 'trassk', so a filter would refuse the
        // subject. Upstream: regex.search('(?i)(?:oak strassK){e<=1}', 'xak strassk', V1) ->
        // span=(0, 11) counts=(1, 0, 0), regex 2026.9.10, 2026-09-28. 'é', which no ASCII letter
        // equals, keeps the filter.
        Build("(?i)(?:oak strassK){e<=1}").FuzzyLiteralFilter.Should().BeNull();
        Search("(?i)(?:oak strassK){e<=1}", "xak strassk").Should().Be("(0,11) 1,0,0");
        Build("(?i)(?:oak strassé){e<=1}").FuzzyLiteralFilter.Should().NotBeNull();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?:café au lait){e<=1}")] // a literal that is not ASCII
    [Arguments("(?i)(?:oak strass😀){e<=1}")] // a lone character outside the BMP is two code units
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

    // ---------------------------------------------------------------------------------------
    // D14: what the filter learns in one step of a scan it keeps for the next, on the scan's
    // MatchState. A piece with no occurrence left is not searched for again, and text already known
    // to be ASCII is not read again, so a Matches walk is linear in the subject, not quadratic.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    // ' works' never occurs, so the piece search ran to the end of the subject on every step of
    // the walk. Release, 1,000,000 characters: 2,303 ms before D14, 234 ms after.
    [Arguments("(?:amber lantern works){e<=2}", "amber lantern wxrks ...... ")]
    // The second branch never occurs. Release: 4,951 ms before D14, 30 ms after.
    [Arguments("(?:amber lantern works|violet stone archive){e<=2}", "amber lantern works ...... ")]
    public void A_matches_walk_over_a_long_subject_is_linear_when_a_piece_never_occurs(string pattern, string unit)
    {
        // Counted, not timed (D13): the characters the filter hands to its searches, which is the
        // quantity D14 made linear. Until 2026-09-28 this was a ratio of two timings, which needed
        // [NotInParallel], warm-up and three runs, and still moved from 15x to 21x beside a CPU
        // burner. Over 99,981 characters the two rows search 625,807 and 325,864 characters
        // (Debug, 2026-09-28), a few per character of the subject; the filter before D14 searches
        // the rest of the subject on every step, about n^2 / 27.
        string subject = string.Concat(Enumerable.Repeat(unit, 100_000 / unit.Length));
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        int count = 0;

        long searched = EngineWork.CharactersSearchedBy(() => count = regex.Matches(subject).Count);

        count.Should().Be(subject.Length / unit.Length);
        searched.Should().BeLessThanOrEqualTo(20L * subject.Length, "each stretch is searched once per scan");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_walk_over_a_long_subject_does_not_search_again_what_it_has_searched()
    {
        // Counted, not timed (D13), as in the forward test above: the reverse filter's calls read
        // the whole subject before each position before D14 (4,416 ms of a 4.4 s Release walk over a
        // million characters, 293 ms after). Now 399,924 characters searched over 99,981, the whole
        // reverse walk included (Debug, 2026-09-28). Until 2026-09-28 this was a 500 ms budget on
        // the filter's own calls, marked as waiting for this counter.
        const string unit = "amber lantern works ...... ";
        string subject = string.Concat(Enumerable.Repeat(unit, 100_000 / unit.Length));
        var regex = new FuzzyRegex(
            "(?r)(?:violet stone archive|amber lantern works){e<=2}",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );
        int count = 0;

        long searched = EngineWork.CharactersSearchedBy(() => count = regex.Matches(subject).Count);

        count.Should().Be(subject.Length / unit.Length);
        searched.Should().BeLessThanOrEqualTo(20L * subject.Length, "each stretch is searched once per scan");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_remembered_piece_is_searched_again_when_the_scan_moves_back_before_its_search()
    {
        // 'abcd' searched from 5 is found at 12, which says nothing about 0-4. BESTMATCH's second
        // pass goes back like this. From 0 'abcd' is at 0, so the bound is 0; the stale 12 gave 11.
        FuzzyLiteralFilter filter = Build("(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "abcdefgh zz abcdefgh";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.NextStart(text, 5, text.Length, memory).Should().Be(11);
        filter.NextStart(text, 0, text.Length, memory).Should().Be(0);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_remembered_piece_is_searched_again_when_the_scan_moves_past_it()
    {
        // From 0 both pieces are found at once; from 5 neither is left.
        FuzzyLiteralFilter filter = Build("(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "abcdefgh zzzzzzzz";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.NextStart(text, 0, text.Length, memory).Should().Be(0);
        filter.NextStart(text, 5, text.Length, memory).Should().Be(FuzzyLiteralFilter.NoMatch);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_piece_remembered_as_absent_is_searched_again_when_the_slice_grows()
    {
        // Absent before 10 is not absent before 20: BESTMATCH narrows and restores the slice.
        FuzzyLiteralFilter filter = Build("(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "abcdefgh zz abcdefgh";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.NextStart(text, 5, 10, memory).Should().Be(FuzzyLiteralFilter.NoMatch);
        filter.NextStart(text, 5, text.Length, memory).Should().Be(11);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Text_known_to_be_ascii_is_only_trusted_where_it_joins_the_query_without_a_gap()
    {
        // The 'é' at 2 stops the first call. From 3 the rest is ASCII, and a stretch that began at
        // 0 must not be stretched over the 'é' to cover it; from 0 again the answer is still that
        // the filter cannot tell.
        FuzzyLiteralFilter filter = Build("(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "zzézz abcdefgh";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.NextStart(text, 0, text.Length, memory).Should().Be(FuzzyLiteralFilter.CannotTell);
        filter.NextStart(text, 3, text.Length, memory).Should().Be(5);
        filter.NextStart(text, 0, text.Length, memory).Should().Be(FuzzyLiteralFilter.CannotTell);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_witness_holds_only_for_the_slice_start_and_the_stretch_it_was_found_in()
    {
        FuzzyLiteralFilter filter = Build("(?r)(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "abcdzzzzzzzzzzzzzzzzzzzz";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        // 'abcd' at 0 is a witness for any stretch from 0 that holds it...
        filter.MayMatchBefore(text, 0, 20, memory).Should().BeTrue();
        // ...but not for a stretch that ends before the piece does,
        filter.MayMatchBefore(text, 0, 3, memory).Should().BeFalse();
        // nor for a slice that starts after it.
        filter.MayMatchBefore(text, 5, 20, memory).Should().BeFalse();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_piece_remembered_as_absent_in_reverse_is_searched_again_over_a_longer_stretch()
    {
        FuzzyLiteralFilter filter = Build("(?r)(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "zzzzzzzzzzzzabcdzzzz";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.MayMatchBefore(text, 0, 10, memory).Should().BeFalse();
        filter.MayMatchBefore(text, 0, 20, memory).Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_non_ascii_character_is_a_reverse_witness_only_for_a_stretch_holding_it()
    {
        FuzzyLiteralFilter filter = Build("(?r)(?:abcdefgh){e<=1}").FuzzyLiteralFilter!;
        const string text = "zzzzzézzzzzz";
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();

        filter.MayMatchBefore(text, 0, 10, memory).Should().BeTrue();
        filter.MayMatchBefore(text, 0, 6, memory).Should().BeTrue();
        filter.MayMatchBefore(text, 0, 5, memory).Should().BeFalse();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?:abcdefgh){e<=1}")]
    [Arguments("(?i)(?:abcdefgh|ijklmnop){e<=1}")]
    [Arguments("(?r)(?:abcdefgh){e<=1}")]
    [Arguments("(?ri)(?:abcdefgh|ijklmnop){e<=1}")]
    public void A_scan_s_memory_never_changes_an_answer(string pattern)
    {
        // Every guard at once: random subjects, random query orders - mostly the steps of a walk,
        // sometimes a jump back or a new slice - each answered with the scan's memory and again with
        // none. The answer is a function of the subject and the positions alone.
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        FuzzyLiteralFilter filter = Build(pattern).FuzzyLiteralFilter!;
        string[] chunks = ["abcd", "efgh", "ijkl", "mnop", "ABCD", "abcdefgh", "z", "zz", "é", "  "];
        var random = new Random(20260928);
        int answered = 0;
        for (int round = 0; round < 300; round++)
        {
            string text = string.Concat(
                Enumerable.Range(0, random.Next(1, 12)).Select(_ => chunks[random.Next(chunks.Length)])
            );
            FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();
            int sliceStart = 0;
            int sliceEnd = text.Length;
            int pos = filter.Reverse ? text.Length : 0;
            for (int step = 0; step < 40; step++)
            {
                if (random.Next(8) == 0)
                {
                    sliceStart = random.Next(text.Length + 1);
                    sliceEnd = random.Next(sliceStart, text.Length + 1);
                }

                int stride = filter.Reverse ? -random.Next(4) : random.Next(4);
                pos = random.Next(4) == 0 ? random.Next(text.Length + 1) : Math.Clamp(pos + stride, 0, text.Length);
                int at = Math.Clamp(pos, sliceStart, sliceEnd);
                object remembered = filter.Reverse
                    ? filter.MayMatchBefore(text, sliceStart, at, memory)
                    : filter.NextStart(text, at, sliceEnd, memory);
                object fresh = filter.Reverse
                    ? filter.MayMatchBefore(text, sliceStart, at, filter.NewScanMemory())
                    : filter.NextStart(text, at, sliceEnd, filter.NewScanMemory());
                remembered.Should().Be(fresh, $"'{text}' [{sliceStart}, {sliceEnd}) at {at}, step {step}");
                answered++;
            }
        }

#pragma warning restore CA5394, S2245
        answered.Should().Be(300 * 40);
    }
}
