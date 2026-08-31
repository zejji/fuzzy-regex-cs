using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions about
/// fuzzy matching: <c>BESTMATCH</c>/<c>ENHANCEMATCH</c>, error budgets, <c>fuzzy_counts</c>,
/// <c>fuzzy_changes</c>, and the <c>{e&lt;=n:[set]}</c> character-restriction syntax.
/// </summary>
/// <remarks>
/// The DNA-sequence patterns and subjects (Hg issue 82, Hg issue 300, Git issue 427, Git issue 433)
/// are long and unreadable inline, so each lives in a named <c>private const</c> field. All data in
/// this file is ASCII, so no index shifts between the Python source and the C# port.
/// </remarks>
public sealed class RegressionsFuzzyTests
{
    // Hg issue 82: error range does not work.
    private const string _hgIssue82FuzzyPattern = "(CAGCCTCCCATTTCAGAATATACATCC){1<e<=2}";

    private const string _hgIssue82Sequence =
        "tcagacgagtgcgttgtaaaacgacggccagtCAGCCTCCCATTCAGAATATACATCCcgacggccagttaaaaacaatgccaaggaggtcatagctgtttcctgccagttaaaaacaatgccaaggaggtcatagctgtttcctgacgcactcgtctgagcgggctggcaagg";

    // Hg issue 300: segmentation fault.
    private const string _hgIssue300FuzzyPattern =
        "(?P<termini5>GGCGTCACACTTTGCTATGCCATAGCAT[AG]TTTATCCATAAGA"
        + "TTAGCGGATCCTACCTGACGCTTTTTATCGCAACTCTCTACTGTTTCTCCATAACAGAACATATTGA"
        + "CTATCCGGTATTACCCGGCATGACAGGAGTAAAA){e<=1}"
        + "(?P<gene>[ACGT]{1059}){e<=2}"
        + "(?P<spacer>TAATCGTCTTGTTTGATACACAAGGGTCGCATCTGCGGCCCTTTTGCTTTTTTAAG"
        + "TTGTAAGGATATGCCATTCTAGA){e<=0}"
        + "(?P<barcode>[ACGT]{18}){e<=0}"
        + "(?P<termini3>AGATCGG[CT]AGAGCGTCGTGTAGGGAAAGAGTGTGG){e<=1}";

    private const string _hgIssue300Sequence =
        "GCACGGCGTCACACTTTGCTATGCCATAGCATATTTATCCATAAGATTAGCGGATCCTACC"
        + "TGACGCTTTTTATCGCAACTCTCTACTGTTTCTCCATAACAGAACATATTGACTATCCGGTATTACC"
        + "CGGCATGACAGGAGTAAAAATGGCTATCGACGAAAACAAACAGAAAGCGTTGGCGGCAGCACTGGGC"
        + "CAGATTGAGAAACAATTTGGTAAAGGCTCCATCATGCGCCTGGGTGAAGACCGTTCCATGGATGTGG"
        + "AAACCATCTCTACCGGTTCGCTTTCACTGGATATCGCGCTTGGGGCAGGTGGTCTGCCGATGGGCCG"
        + "TATCGTCGAAATCTACGGACCGGAATCTTCCGGTAAAACCACGCTGACGCTGCAGGTGATCGCCGCA"
        + "GCGCAGCGTGAAGGTAAAACCTGTGCGTTTATCGATGCTGAACACGCGCTGGACCCAATCTACGCAC"
        + "GTAAACTGGGCGTCGATATCGACAACCTGCTGTGCTCCCAGCCGGACACCGGCGAGCAGGCACTGGA"
        + "AATCTGTGACGCCCTGGCGCGTTCTGGCGCAGTAGACGTTATCGTCGTTGACTCCGTGGCGGCACTG"
        + "ACGCCGAAAGCGGAAATCGAAGGCGAAATCGGCGACTCTCATATGGGCCTTGCGGCACGTATGATGA"
        + "GCCAGGCGATGCGTAAGCTGGCGGGTAACCTGAAGCAGTCCAACACGCTGCTGATCTTCATCAACCC"
        + "CATCCGTATGAAAATTGGTGTGATGTTCGGCAACCCGGAAACCACTTACCGGTGGTAACGCGCTGAA"
        + "ATTCTACGCCTCTGTTCGTCTCGACATCCGTTAAATCGGCGCGGTGAAAGAGGGCGAAAACGTGGTG"
        + "GGTAGCGAAACCCGCGTGAAAGTGGTGAAGAACAAAATCGCTGCGCCGTTTAAACAGGCTGAATTCC"
        + "AGATCCTCTACGGCGAAGGTATCAACTTCTACCCCGAACTGGTTGACCTGGGCGTAAAAGAGAAGCT"
        + "GATCGAGAAAGCAGGCGCGTGGTACAGCTACAAAGGTGAGAAGATCGGTCAGGGTAAAGCGAATGCG"
        + "ACTGCCTGGCTGAAATTTAACCCGGAAACCGCGAAAGAGATCGAGTGAAAAGTACGTGAGTTGCTGC"
        + "TGAGCAACCCGAACTCAACGCCGGATTTCTCTGTAGATGATAGCGAAGGCGTAGCAGAAACTAACGA"
        + "AGATTTTTAATCGTCTTGTTTGATACACAAGGGTCGCATCTGCGGCCCTTTTGCTTTTTTAAGTTGT"
        + "AAGGATATGCCATTCTAGACAGTTAACACACCAACAAAGATCGGTAGAGCGTCGTGTAGGGAAAGAG"
        + "TGTGGTACC";

    // Git issue 427: possible bug with BESTMATCH.
    private const string _gitIssue427Sequence = "TTCAGACGTGTGCTCTTCCGATCTCAATACCGACTCCTCACTGTGTGTCT";

    private const string _gitIssue427Pattern =
        "(?P<insert>.*)(?P<anchor>CTTCC){e<=1}(?P<umi>([ACGT]){4,6})(?P<sid>CAATACCGACTCCTCACTGTGT){e<=2}(?P<end>([ACGT]){0,6}$)";

    // Git issue 433: disagreement between fuzzy_counts and fuzzy_changes.
    private const string _gitIssue433Pattern =
        "(?P<insert>.*)(?P<anchor>AACACTGG){e<=1}(?P<umi>([AT][CG]){5}){e<=2}(?P<sid>GTAACCGAAG){e<=2}(?P<end>([ACGT]){0,6}$)";

    private const string _gitIssue433ExactSequence = "GGAAAACACTGGTCTCAGTCTCGTAACCGAAGTGGTCG";
    private const string _gitIssue433SubstitutedSequence = "GGAAAACACTGGTCTCAGTCTCGTCCCCGAAGTGGTCG";

    // ---- needs:fuzzy-bestmatch ----

    // Hg issue 161: Unexpected fuzzy match results.
    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#210-211")]
    [Arguments("(abcdefgh){e}", 14)]
    [Arguments("(abcdefghi){e}", 15)]
    public void Bestmatch_picks_the_best_scoring_span_over_a_noisy_prefix(string pattern, int end)
    {
        Match m = FuzzyRegex.Match("******abcdefghijklmnopqrtuvwxyz", pattern, FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(6);
        (m.Index + m.Length).Should().Be(end);
    }

    // Hg issue 196: Fuzzy matching on repeated regex not working as expected.
    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#228-229")]
    [Arguments("xxxxxx", 6)]
    [Arguments("xxxxx", 5)]
    public void Bestmatch_with_a_one_error_budget_matches_a_repeated_literal_short_of_the_count(string subject, int end)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, "(x{6}){e<=1}", FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(end);
    }

    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#230")]
    public void Bestmatch_fails_when_too_few_repeats_remain_for_the_error_budget() =>
        FuzzyRegex.MatchAtStart("x", "(x{6}){e<=1}", FuzzyRegexOptions.BestMatch).Success.Should().BeFalse();

    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#231-232")]
    [Arguments("xxxxxx", 6)]
    [Arguments("xxxxx", 5)]
    public void Bestmatch_reverse_with_a_one_error_budget_matches_a_repeated_literal_short_of_the_count(
        string subject,
        int end
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, "(?r)(x{6}){e<=1}", FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(end);
    }

    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#233")]
    public void Bestmatch_reverse_fails_when_too_few_repeats_remain_for_the_error_budget() =>
        FuzzyRegex.MatchAtStart("x", "(?r)(x{6}){e<=1}", FuzzyRegexOptions.BestMatch).Success.Should().BeFalse();

    // Hg issue 225: BESTMATCH in fuzzy match not working.
    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#265-266")]
    public void Bestmatch_with_insertion_and_deletion_finds_the_extra_char_span_and_reports_one_insertion()
    {
        Match m = FuzzyRegex.Match("12234", "(^1234$){i,d}", FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(5);
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#267-268")]
    public void Bestmatch_with_substitution_insertion_and_deletion_finds_the_extra_char_span_and_reports_one_insertion()
    {
        Match m = FuzzyRegex.Match("12234", "(^1234$){s,i,d}", FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(5);
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // Hg issue 226: Error matching at start of string.
    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#269-270")]
    public void Bestmatch_anchored_at_both_ends_spans_the_whole_noisy_prefix_as_insertions()
    {
        Match m = FuzzyRegex.Match("xxxxxxxx123", "(^123$){s,i,d}", FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(11);
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 8, 0));
    }

    // Git issue 427: possible bug with BESTMATCH.
    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#417-418")]
    public void Bestmatch_on_a_dna_sequence_finds_the_named_groups_across_the_whole_string()
    {
        Match m = FuzzyRegex.MatchAtStart(_gitIssue427Sequence, _gitIssue427Pattern, FuzzyRegexOptions.BestMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(50);
        m.Groups["insert"].Value.Should().Be("TTCAGACGTGTGCT");
        m.Groups["anchor"].Value.Should().Be("CTTCC");
        m.Groups["umi"].Value.Should().Be("GATCT");
        m.Groups["sid"].Value.Should().Be("CAATACCGACTCCTCACTGTGT");
        m.Groups["end"].Value.Should().Be("GTCT");
    }

    [Test]
    [Skip("needs:fuzzy-bestmatch - BESTMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#419-420")]
    public void Enhancematch_on_the_same_dna_sequence_finds_the_same_named_groups()
    {
        Match m = FuzzyRegex.MatchAtStart(_gitIssue427Sequence, _gitIssue427Pattern, FuzzyRegexOptions.EnhanceMatch);

        m.Index.Should().Be(0);
        (m.Index + m.Length).Should().Be(50);
        m.Groups["insert"].Value.Should().Be("TTCAGACGTGTGCT");
        m.Groups["anchor"].Value.Should().Be("CTTCC");
        m.Groups["umi"].Value.Should().Be("GATCT");
        m.Groups["sid"].Value.Should().Be("CAATACCGACTCCTCACTGTGT");
        m.Groups["end"].Value.Should().Be("GTCT");
    }

    // ---- needs:fuzzy-budget ----

    [Test]
    [Skip("needs:fuzzy-budget - quantifier-scoped fuzzy error budgets not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#56")]
    public void An_error_range_with_a_lower_bound_forces_at_least_that_many_errors_into_the_match() =>
        FuzzyRegex
            .Match(_hgIssue82Sequence, _hgIssue82FuzzyPattern, FuzzyRegexOptions.BestMatch)
            .Value.Should()
            .Be("tCAGCCTCCCATTCAGAATATACATCC");

    // Hg issue 306: Fuzzy match parameters not respecting quantifier scope.
    [Test]
    [Skip("needs:fuzzy-budget - quantifier-scoped fuzzy error budgets not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#371-372")]
    [Arguments("dogfood", 0, 0, 0)]
    [Arguments("dogfoot", 1, 0, 0)]
    public void Fuzzy_parameters_stay_scoped_to_the_quantifier_that_declares_them(
        string subject,
        int substitutions,
        int insertions,
        int deletions
    ) =>
        FuzzyRegex
            .Match(subject, @"(?e)(dogf(((oo){e<1})|((00){e<1}))d){e<2}")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(substitutions, insertions, deletions));

    // ---- needs:fuzzy-changes ----

    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#369-370")]
    public void Bestmatch_on_a_long_dna_sequence_reports_the_single_insertion_position()
    {
        Match m = FuzzyRegex.Match(_hgIssue300Sequence, _hgIssue300FuzzyPattern, FuzzyRegexOptions.BestMatch);

        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().Equal(1206);
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    // Hg issue 353: fuzzy changes negative indexes.
    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#380")]
    public void Fuzzy_changes_reports_deletion_positions_when_the_match_starts_mid_pattern()
    {
        Match m = FuzzyRegex.Match(
            "TTCCCCGCGCCAGCGGGGATAAACCG",
            @"(?be)(AGTGTTCCCCGCGCCAGCGGGGATAAACCG){s<=5,i<=5,d<=5,s+i+d<=10}"
        );

        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().Equal(0, 1, 3, 5);
    }

    // Git issue 364: Contradictory values in fuzzy_counts and fuzzy_changes.
    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#381-382")]
    public void Default_mode_reports_a_missing_letter_as_a_substitution_plus_a_deletion()
    {
        Match m = FuzzyRegex.MatchAtStart("c", @"(?:bc){e}");

        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 1));
        m.FuzzyChanges.Substitutions.Should().Equal(0);
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#383-384")]
    public void Enhancematch_mode_reports_the_same_missing_letter_as_a_single_deletion()
    {
        Match m = FuzzyRegex.MatchAtStart("c", @"(?e)(?:bc){e}");

        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().Equal(0);
    }

    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#385-386")]
    public void Bestmatch_inline_flag_reports_the_same_missing_letter_as_a_single_deletion()
    {
        Match m = FuzzyRegex.MatchAtStart("c", @"(?b)(?:bc){e}");

        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().Equal(0);
    }

    // Git issue 433: disagreement between fuzzy_counts and fuzzy_changes.
    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#421-422")]
    public void An_exact_dna_match_reports_no_errors_or_error_positions()
    {
        Match m = FuzzyRegex.MatchAtStart(_gitIssue433ExactSequence, _gitIssue433Pattern, FuzzyRegexOptions.BestMatch);

        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    [Skip("needs:fuzzy-changes - fuzzy_changes (per-position error indices) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#423-424")]
    public void A_dna_match_with_two_substitutions_reports_their_positions()
    {
        Match m = FuzzyRegex.MatchAtStart(
            _gitIssue433SubstitutedSequence,
            _gitIssue433Pattern,
            FuzzyRegexOptions.BestMatch
        );

        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(24, 25);
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    // ---- needs:fuzzy-counts ----

    // Hg issue 109: Edit distance of fuzzy match.
    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#89-94")]
    [Arguments(@"(?:cats|cat){e<=1}")]
    [Arguments(@"(?e)(?:cats|cat){e<=1}")]
    [Arguments(@"(?b)(?:cats|cat){e<=1}")]
    [Arguments(@"(?:cat){e<=1}")]
    [Arguments(@"(?e)(?:cat){e<=1}")]
    [Arguments(@"(?b)(?:cat){e<=1}")]
    public void A_single_substitution_is_reported_the_same_way_regardless_of_mode(string pattern) =>
        FuzzyRegex.MatchAtStart("caz", pattern).FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#95-97")]
    [Arguments(@"(?:cats){e<=2}", 1, 1, 0)]
    [Arguments(@"(?e)(?:cats){e<=2}", 0, 1, 0)]
    [Arguments(@"(?b)(?:cats){e<=2}", 0, 1, 0)]
    public void One_letter_gap_scores_differently_between_default_and_enhanced_modes(
        string pattern,
        int substitutions,
        int insertions,
        int deletions
    ) =>
        FuzzyRegex
            .MatchAtStart("c ats", pattern)
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(substitutions, insertions, deletions));

    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#98-100")]
    [Arguments(@"(?:cats){e<=2}")]
    [Arguments(@"(?e)(?:cats){e<=2}")]
    [Arguments(@"(?b)(?:cats){e<=2}")]
    public void Two_letter_gaps_are_reported_as_two_insertions_regardless_of_mode(string pattern) =>
        FuzzyRegex.MatchAtStart("c a ts", pattern).FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));

    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#101-103")]
    [Arguments(@"(?:cats){e<=1}")]
    [Arguments(@"(?e)(?:cats){e<=1}")]
    [Arguments(@"(?b)(?:cats){e<=1}")]
    public void One_letter_gap_within_a_tight_budget_is_reported_as_one_insertion_regardless_of_mode(string pattern) =>
        FuzzyRegex.MatchAtStart("c ats", pattern).FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));

    // Git issue 370: Confusions about Fuzzy matching behavior.
    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#387-391")]
    [Arguments(@"(?e)(?:^(\$ )?\d{1,3}(,\d{3})*(\.\d{2})$){e}", "$ 10,112.111.12", 6, 0, 5)]
    [Arguments(@"(?e)(?:^(\$ )?\d{1,3}(,\d{3})*(\.\d{2})$){s<=1}", "$ 10,112.111.12", 1, 0, 0)]
    [Arguments(@"(?e)(?:^(\$ )?\d{1,3}(,\d{3})*(\.\d{2})$){s<=1,i<=1,d<=1}", "$ 10,112.111.12", 1, 0, 0)]
    [Arguments(@"(?e)(?:^(\$ )?\d{1,3}(,\d{3})*(\.\d{2})$){s<=3}", "$ 10,1a2.111.12", 2, 0, 0)]
    [Arguments(@"(?e)(?:^(\$ )?\d{1,3}(,\d{3})*(\.\d{2})$){s<=2}", "$ 10,1a2.111.12", 2, 0, 0)]
    public void Money_pattern_fuzzy_counts_vary_with_the_quantifier_budget(
        string pattern,
        string subject,
        int substitutions,
        int insertions,
        int deletions
    ) =>
        FuzzyRegex
            .MatchAtStart(subject, pattern)
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(substitutions, insertions, deletions));

    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#392-393")]
    [Arguments(@"(?e)(?:0?,0(?:,0)?){s<=1,d<=1}")]
    [Arguments(@"(?e)(?:0??,0(?:,0)?){s<=1,d<=1}")]
    public void An_optional_leading_zero_lazy_or_greedy_still_reports_one_substitution(string pattern) =>
        FuzzyRegex.FullMatch(",0;0", pattern).FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

    // Git issue 403: Fuzzy matching with wrong distance (unnecessary substitutions).
    [Test]
    [Skip("needs:fuzzy-counts - fuzzy_counts (edit-distance breakdown) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#399")]
    public void Bestmatch_avoids_unnecessary_substitutions_when_deletions_explain_the_gap() =>
        FuzzyRegex
            .MatchAtStart("terstin", @"^(test){e<=5}$", FuzzyRegexOptions.BestMatch)
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(0, 3, 0));

    // ---- needs:fuzzy-enhancematch ----

    // Hg issue 201: ENHANCEMATCH crashes interpreter.
    [Test]
    [Skip("needs:fuzzy-enhancematch - ENHANCEMATCH flag not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#238")]
    public void Enhancematch_does_not_crash_on_overlapping_alternation_groups()
    {
        string[][] expected =
        [
            ["borwn", "borwn", "", "fax", "", "fax"],
            ["lzy", "", "lzy", "hog", "hog", ""],
        ];

        FuzzyRegex
            .Matches(
                "The quick borwn fax jumped over the lzy hog",
                @"((brown)|(lazy)){1<=e<=3} ((dog)|(fox)){1<=e<=3}",
                FuzzyRegexOptions.EnhanceMatch
            )
            .Select(m => m.Groups.Skip(1).Select(g => g.Value).ToArray())
            .Should()
            .BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    // ---- needs:fuzzy-matching ----

    // Hg issue 94: Python crashes when executing regex updates pattern.findall.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#62-63")]
    public void A_compiled_fuzzy_pattern_with_a_word_boundary_finds_no_match_in_ordinary_text()
    {
        var rx = new FuzzyRegex(@"\bt(est){i<2}", FuzzyRegexOptions.Version1);

        rx.Match("Some text").Success.Should().BeFalse();
        rx.Matches("Some text").Select(m => m.Value).Should().BeEmpty();
    }

    // Hg issue 147: Fuzzy match can return match points beyond buffer end.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#157-158")]
    [Arguments(@"(?i)(?:error){e}")]
    [Arguments(@"(?fi)(?:error){e}")]
    public void Fuzzy_iteration_over_a_short_subject_does_not_read_past_the_end(string pattern) =>
        FuzzyRegex
            .Matches("regex failure", pattern)
            .Select(m => (m.Index, End: m.Index + m.Length))
            .Should()
            .Equal((0, 5), (5, 10), (10, 13), (13, 13));

    // Hg issue 199: Segfault in re.compile.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#236")]
    public void A_pattern_with_a_fuzzy_recursive_group_reference_compiles()
    {
        Action act = () => _ = new FuzzyRegex("((?0)){e}");
        act.Should().NotThrow();
    }

    // Hg issue 200: AttributeError in regex.compile with latest regex.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#237")]
    public void A_pattern_starting_with_a_literal_nul_before_a_fuzzy_recursive_reference_compiles()
    {
        Action act = () => _ = new FuzzyRegex("\x00?(?0){e}");
        act.Should().NotThrow();
    }

    // Hg issue 210: Fuzzy matching and Backreference.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#252-253")]
    [Arguments(@"(2)(?:\1{5}){e<=1}")]
    [Arguments(@"(\d)(?:\1{5}){e<=1}")]
    public void Fuzzy_matching_honours_a_backreference_to_an_earlier_group(string pattern)
    {
        Match m = FuzzyRegex.Match("3222212", pattern);

        m.Index.Should().Be(1);
        (m.Index + m.Length).Should().Be(7);
    }

    // Hg issue 247: Unexpected result with fuzzy matching and lookahead expression.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#278-285")]
    [Arguments(@"(?:ESTONIA(?!\w)){e<=1}", "ESTONIAN WORKERS", "ESTONIAN")]
    [Arguments(@"(?:ESTONIA(?=\W)){e<=1}", "ESTONIAN WORKERS", "ESTONIAN")]
    [Arguments(@"(?:(?<!\w)ESTONIA){e<=1}", "BLUB NESTONIA", "NESTONIA")]
    [Arguments(@"(?:(?<=\W)ESTONIA){e<=1}", "BLUB NESTONIA", "NESTONIA")]
    [Arguments(@"(?r)(?:ESTONIA(?!\w)){e<=1}", "ESTONIAN WORKERS", "ESTONIAN")]
    [Arguments(@"(?r)(?:ESTONIA(?=\W)){e<=1}", "ESTONIAN WORKERS", "ESTONIAN")]
    [Arguments(@"(?r)(?:(?<!\w)ESTONIA){e<=1}", "BLUB NESTONIA", "NESTONIA")]
    [Arguments(@"(?r)(?:(?<=\W)ESTONIA){e<=1}", "BLUB NESTONIA", "NESTONIA")]
    public void Fuzzy_matching_respects_a_lookaround_boundary_forward_and_reverse(
        string pattern,
        string subject,
        string expected
    ) => FuzzyRegex.Match(subject, pattern).Value.Should().Be(expected);

    // Hg issue 248: Unexpected result with fuzzy matching and more than one non-greedy quantifier.
    [Test]
    [Skip("needs:fuzzy-matching - fuzzy quantifiers ({e<=n}) not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#286-289")]
    [Arguments(@"(?:A.*B.*CDE){e<=2}")]
    [Arguments(@"(?:A.*B.*?CDE){e<=2}")]
    [Arguments(@"(?:A.*?B.*CDE){e<=2}")]
    [Arguments(@"(?:A.*?B.*?CDE){e<=2}")]
    public void Fuzzy_matching_with_mixed_greedy_and_lazy_dot_star_still_spans_the_whole_match(string pattern) =>
        FuzzyRegex.Match("A B CYZ", pattern).Value.Should().Be("A B CYZ");

    // ---- needs:fuzzy-matching (parsed since S13, not matchable yet) ----

    // Hg issue 338: specifying allowed characters when fuzzy-matching.
    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#378-379")]
    [Arguments(@"(?:cat){e<=1:[u]}")]
    [Arguments(@"(?:cat){e<=1:u}")]
    public void Fuzzy_character_restriction_accepts_bracketed_or_bare_character_syntax(string pattern) =>
        FuzzyRegex.MatchAtStart("cut", pattern).Success.Should().BeTrue();

    // Git issue 371: Specifying character set when fuzzy-matching allows characters not in the set.
    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#394")]
    public void Fuzzy_character_restriction_rejects_digits_outside_the_allowed_set() =>
        FuzzyRegex
            .Match("cat dog starting at 00:01132.000. hello world", @"\b(?e)(?:\d{6,20}){i<=5:[\-\\\/]}\b")
            .Success.Should()
            .BeFalse();

    // Git issue 394: Unexpected behaviour in fuzzy matching with limited character set with IGNORECASE flag.
    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#397-398")]
    [Arguments(@"(\d+){i<=2:[ab]}")]
    [Arguments(@"(?i)(\d+){i<=2:[ab]}")]
    public void Fuzzy_character_restriction_stops_digit_runs_from_absorbing_non_digit_non_set_letters(string pattern) =>
        FuzzyRegex.Matches("123X4Y5", pattern).Select(m => m.Value).Should().Equal("123", "4", "5");

    // Git issue 415: Fuzzy character restrictions don't apply to insertions at "right edge".
    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#402-403")]
    public void Fuzzy_character_restriction_on_a_substitution_rejects_a_char_outside_the_set()
    {
        FuzzyRegex.MatchAtStart("te5t", @"t(?:es){s<=1:\d}t").Value.Should().Be("te5t");
        FuzzyRegex.MatchAtStart("tezt", @"t(?:es){s<=1:\d}t").Success.Should().BeFalse();
    }

    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#404-406")]
    public void Fuzzy_character_restriction_on_an_insertion_rejects_a_char_outside_the_set_and_reports_its_position()
    {
        Match matched = FuzzyRegex.MatchAtStart("tes5t", @"t(?:es){i<=1:\d}t");

        matched.Value.Should().Be("tes5t");
        matched.FuzzyChanges.Substitutions.Should().BeEmpty();
        matched.FuzzyChanges.Insertions.Should().Equal(3);
        matched.FuzzyChanges.Deletions.Should().BeEmpty();

        FuzzyRegex.MatchAtStart("teszt", @"t(?:es){i<=1:\d}t").Success.Should().BeFalse();
    }

    [Test]
    // This one carries no ":set" restriction, unlike the rest of Git issue 415, so it is tagged
    // for the multi-constraint error budget it does need.
    [Skip("needs:fuzzy-budget - multi-constraint error budgets ({i<=1,0<e<=1}) are not implemented yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#407")]
    public void A_captured_group_with_an_insertion_budget_and_a_nonzero_error_floor_still_matches() =>
        FuzzyRegex.MatchAtStart("tes5t", @"t(es){i<=1,0<e<=1}t").Value.Should().Be("tes5t");

    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#408")]
    public void Fuzzy_character_restriction_combined_with_a_nonzero_error_floor_reports_the_insertion_position()
    {
        Match m = FuzzyRegex.MatchAtStart("tes5t", @"t(?:es){i<=1,0<e<=1:\d}t");

        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().Equal(3);
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    // Git issue 442: Fuzzy regex matching doesn't seem to test insertions correctly.
    [Test]
    [Skip("needs:fuzzy-matching - the parser reads {e<=n:[set]} since S13; the engine has no FUZZY_EXT opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#429-430")]
    [Arguments(FuzzyRegexOptions.None)]
    [Arguments(FuzzyRegexOptions.IgnoreCase)]
    public void Fuzzy_insertion_restricted_to_a_space_does_not_let_a_word_boundary_insert_a_letter(
        FuzzyRegexOptions options
    ) => FuzzyRegex.Match("having", @"(?:\bha\b){i:[ ]}", options).Success.Should().BeFalse();
}
