using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Docs;

/// <summary>
/// Pins every runnable code example in <c>docs/COMPARISON.md</c>'s "Fuzzy syntax in one page" and
/// "Behaviour that differs and why" sections against the exact values their
/// <c>Console.WriteLine</c> calls would print, so a library regression that changes one of those
/// values fails here, in the ordinary test run, rather than only in
/// <c>tools/check-doc-examples.ps1</c> (still the CI gate, `ci.yml`, that catches the sample's
/// SOURCE drifting from the doc's own markdown, by actually extracting and running it - this file
/// does not re-derive its assertions from the markdown text, since doing so at test time would
/// need either a subprocess build or Roslyn scripting, and the latter is not Native-AOT-safe, which
/// the whole suite is published as, per `tools/run-aot-tests.ps1`). Each test calls the library
/// directly rather than redirecting <see cref="Console"/> itself, exactly as
/// <see cref="ReadmeSamples"/> does, for the same reason given there.
/// </summary>
public sealed class ComparisonSamples
{
    /// <summary>"`{e&lt;=n}`: allow up to `n` errors of any kind".</summary>
    [Test]
    public void Allow_up_to_n_errors_of_any_kind()
    {
        Match m = FuzzyRegex.Match("foxbar", "^(foobar){e<=1}$");

        m.Success.Should().BeTrue();

        (int subs, int ins, int dels) = m.FuzzyCounts;
        $"{subs} {ins} {dels}".Should().Be("1 0 0");
    }

    /// <summary>"`{s,i,d,e}`: separate budgets per kind of error".</summary>
    [Test]
    public void Separate_budgets_per_kind_of_error()
    {
        Match m = FuzzyRegex.Match("oobargoobaploowap", "(foobar){i<=2,s<=2,e<=2}");

        (m.Index, m.Length).Should().Be((5, 6));
    }

    /// <summary>"Cost forms: `{Ni+Md&lt;n}` weights errors instead of just counting them".</summary>
    [Test]
    public void Cost_forms_weight_errors_instead_of_just_counting_them()
    {
        const string subject = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";
        Match m = FuzzyRegex.Match(subject, "(foobar){i<=1,d<=2,s<=3,2d+1s<4}");

        (m.Index, m.Length).Should().Be((6, 7));
    }

    /// <summary>"`{0&lt;e&lt;5}`: two-sided form, at least one error and fewer than five".</summary>
    [Test]
    public void Two_sided_form_at_least_one_error_and_fewer_than_five()
    {
        Match fuzzy = FuzzyRegex.MatchAtStart("servic detection", "(?:service detection){0<e<5}");
        (fuzzy.Index, fuzzy.Length).Should().Be((0, 16));

        Match exact = FuzzyRegex.MatchAtStart("service detection", "(?:service detection){0<e<5}");
        exact.Success.Should().BeFalse();
    }

    /// <summary>"`{e&lt;=n:[set]}`: constrain which characters an edit may touch".</summary>
    [Test]
    public void Constrain_which_characters_an_edit_may_touch()
    {
        Match ok = FuzzyRegex.FullMatch("ae", @"(?:a){e<=1:[a-z]}");
        ok.Success.Should().BeTrue();

        Match rejected = FuzzyRegex.FullMatch("a-", @"(?:a){e<=1:[a-z]}");
        rejected.Success.Should().BeFalse();
    }

    /// <summary>
    /// "`FuzzyRegexOptions.BestMatch` / `(?b)`: take the best fuzzy match rather than the first".
    /// </summary>
    [Test]
    public void Rank_by_the_best_fuzzy_match_not_the_first()
    {
        Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?b)(foobar){e}");

        (m.Index, m.Length).Should().Be((11, 5));
    }

    /// <summary>
    /// "`FuzzyRegexOptions.EnhanceMatch` / `(?e)`: tighten a match after it is found".
    /// </summary>
    [Test]
    public void Tighten_a_match_after_it_is_found()
    {
        Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?e)(foobar){e}");

        (m.Index, m.Length).Should().Be((0, 3));
    }

    /// <summary>"`\L&lt;name&gt;`: fuzzy matching against a named list of words".</summary>
    [Test]
    public void Fuzzy_matching_against_a_named_list_of_words()
    {
        var namedLists = new Dictionary<string, IReadOnlyCollection<string>> { ["words"] = ["cat", "dog"] };
        MatchCollection matches = FuzzyRegex.Matches(
            " book dog cot desk ",
            @"(?e)\b\L<words>{e<=1}\b",
            FuzzyRegexOptions.None,
            namedLists
        );

        matches.Select(static m => m.Value).Should().Equal("dog", "cot");
    }

    /// <summary>"Version 1 is the default".</summary>
    [Test]
    public void Version_1_is_the_default()
    {
        Match m = FuzzyRegex.Match("d", "[[a-z]--[aeiou]]");

        m.Success.Should().BeTrue();
    }

    /// <summary>
    /// "The "unterminated character set" parse error names `FuzzyRegexOptions.Version0` and the
    /// `\[` escape".
    /// </summary>
    [Test]
    public void Unterminated_character_set_parse_error_names_version0_and_the_escape()
    {
        Action act = static () => _ = new FuzzyRegex("[[]");

        FuzzyRegexParseException ex = act.Should().Throw<FuzzyRegexParseException>().Which;
        ex.Message.Contains("Version0", StringComparison.Ordinal).Should().BeTrue();
    }

    /// <summary>"A `CancellationToken` on every input-dependent method".</summary>
    [Test]
    public void A_cancellation_token_on_every_input_dependent_method()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => FuzzyRegex.IsMatch("aaaaaaaaaa", "(a+)+b", cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    /// <summary>"A per-call `timeout` on every input-dependent method".</summary>
    [Test]
    public void A_per_call_timeout_on_every_input_dependent_method()
    {
        // The doc's own comment notes that (a|a)* with a tail that can never hold is exponential
        // here as in upstream, that (a+)+b would not demonstrate a timeout because this engine's
        // repeat guards answer it fast, and that since S60 a literal tail would not either -
        // the required-string prefilter refuses the subject before the engine runs.
        var pattern = new FuzzyRegex(@"(a|a)*\b\B");

        Action act = () => pattern.IsMatch(new string('a', 26), timeout: TimeSpan.FromMilliseconds(50));

        act.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();
    }

    /// <summary>
    /// "`Match` means upstream's `search`; upstream's anchored `match` is `MatchAtStart`".
    /// </summary>
    [Test]
    public void Match_means_search_matchatstart_is_the_anchored_match()
    {
        FuzzyRegex.Match("xab", "ab").Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart("xab", "ab").Success.Should().BeFalse();
    }

    /// <summary>"`Regex`-shaped surface".</summary>
    [Test]
    public void Regex_shaped_surface()
    {
        Match m = FuzzyRegex.Match("a", "(x)?(a)");

        m.LastGroupNumber.Should().Be(2);
        (m.LastGroupName ?? "null").Should().Be("null");
    }

    /// <summary>"**Indices are UTF-16 code units**, where upstream counts codepoints".</summary>
    [Test]
    public void Indices_are_utf16_code_units_not_codepoints()
    {
        // U+1F600 GRINNING FACE is one codepoint but two UTF-16 code units.
        Match m = FuzzyRegex.Match("\U0001F600b", "b");

        m.Index.Should().Be(2);
    }

    /// <summary>"Upstream's `pos`/`endpos` are reshaped to `beginning`/`length`".</summary>
    [Test]
    public void Pos_endpos_are_reshaped_to_beginning_length()
    {
        var pattern = new FuzzyRegex("b.");

        pattern.Match("abcde", beginning: 1, length: 2).Value.Should().Be("bc");
    }

    /// <summary>"`Split` spells "no limit" as `maxSplits = -1`".</summary>
    [Test]
    public void Split_spells_no_limit_as_maxsplits_minus_1()
    {
        var pattern = new FuzzyRegex(",");

        string.Join("|", pattern.Split("a,b,c", maxSplits: 0)).Should().Be("a,b,c");
        string.Join("|", pattern.Split("a,b,c")).Should().Be("a|b|c");
    }

    /// <summary>"`Replace`'s `count` is inverted the same way".</summary>
    [Test]
    public void Replaces_count_is_inverted_the_same_way()
    {
        var pattern = new FuzzyRegex("a");

        pattern.Replace("aaa", "b", count: 0).Should().Be("aaa");
        pattern.Replace("aaa", "b").Should().Be("bbb");
    }

    /// <summary>"There is no `findall`".</summary>
    [Test]
    public void There_is_no_findall()
    {
        MatchCollection matches = FuzzyRegex.Matches("abcdefgh", @"(\w\w\K\w\w)");

        matches.Select(static m => m.Value).Should().Equal("cd", "gh");
    }

    /// <summary>
    /// "`Match.Groups` is an `IReadOnlyDictionary&lt;string, Group&gt;` as well as a list".
    /// </summary>
    [Test]
    public void Match_groups_is_a_dictionary_as_well_as_a_list()
    {
        Match m = FuzzyRegex.Match("abc", @"(?<a>a)(b)(?<c>c)?");

        string.Join(",", m.Groups.Keys).Should().Be("0,a,2,c");
    }

    /// <summary>
    /// "`Split` returns `string?[]` and puts `null` where a capturing group did not take part".
    /// </summary>
    [Test]
    public void Split_returns_nullable_array_and_puts_null_for_an_unmatched_group()
    {
        var pattern = new FuzzyRegex("(x)|(1)");

        string.Join("|", pattern.Split("a1b").Select(static s => s ?? "null")).Should().Be("a|null|1|b");
    }

    /// <summary>"Replacement templates speak upstream's language".</summary>
    [Test]
    public void Replacement_templates_speak_upstreams_language()
    {
        var pattern = new FuzzyRegex(".");

        pattern.Replace("x", @"\n").Should().Be("\n");
    }

    /// <summary>"Exception mapping".</summary>
    [Test]
    public void Exception_mapping()
    {
        string result;
        try
        {
            _ = new FuzzyRegex("(");
            result = "no exception";
        }
        catch (FuzzyRegexParseException)
        {
            result = "parse error, as expected";
        }

        result.Should().Be("parse error, as expected");
    }

    /// <summary>"The "unused keyword argument" message interpolates the name as written".</summary>
    [Test]
    public void Unused_keyword_argument_message_interpolates_the_name_as_written()
    {
        var lists = new Dictionary<string, IReadOnlyCollection<string>> { ["é"] = ["x"] };

        Action act = () => _ = new FuzzyRegex("a", FuzzyRegexOptions.None, lists);

        FuzzyRegexParseException ex = act.Should().Throw<FuzzyRegexParseException>().Which;
        ex.Message.Should().Be("unused keyword argument 'é'");
    }

    /// <summary>"`(?e)` and `(?b)` rank candidates by fuzzy COST".</summary>
    [Test]
    public void Rank_candidates_by_fuzzy_cost()
    {
        // Under this weighted equation, this port finds the cheaper match by cost; upstream would
        // find a different, more-errors-but-cheaper-looking match because it counts errors, not cost.
        const string subject = "3oifaowefbaoraofuiebofasebfaobfaorfeoaro";
        Match m = FuzzyRegex.Match(subject, "(?b)(foobar){i<=1,d<=2,s<=3,2d+1s<4}");

        (m.Index, m.Length).Should().Be((26, 7));
    }

    /// <summary>"The Turkic `I` pairings are not applied by default".</summary>
    [Test]
    public void Turkic_i_pairings_are_not_applied_by_default()
    {
        Match m = FuzzyRegex.FullMatch("aı", "aI", FuzzyRegexOptions.IgnoreCase);

        m.Success.Should().BeFalse();
    }

    /// <summary>"The search prefilters are not ported".</summary>
    [Test]
    public void Search_prefilters_are_not_ported()
    {
        Match m = FuzzyRegex.Match("A", "(?ai)\\p{Ll}");

        m.Success.Should().BeTrue();
    }

    /// <summary>"The Unicode data is version 17.0.0".</summary>
    [Test]
    public void Unicode_data_is_version_17_0_0()
    {
        Match m = FuzzyRegex.Match("\U00011DE1", @"\N{TOLONG SIKI DIGIT ONE}");

        m.Success.Should().BeTrue();
    }

    /// <summary>"`\N{...}` named sequences are not carried".</summary>
    [Test]
    public void Named_sequences_are_not_carried()
    {
        Action act = static () => _ = new FuzzyRegex(@"\N{KEYCAP DIGIT ZERO}");

        FuzzyRegexParseException ex = act.Should().Throw<FuzzyRegexParseException>().Which;
        ex.Message.Contains("undefined character name", StringComparison.Ordinal).Should().BeTrue();
    }

    /// <summary>"`(?L)` works only when casing is not requested".</summary>
    [Test]
    public void L_flag_works_only_when_casing_is_not_requested()
    {
        var literal = new FuzzyRegex("(?L)a");
        literal.FullMatch("a").Success.Should().BeTrue();
        literal.FullMatch("A").Success.Should().BeFalse();

        Action act = static () => _ = new FuzzyRegex("(?Li)a");

        NotSupportedException ex = act.Should().Throw<NotSupportedException>().Which;
        ex.Message.Contains("(?L)", StringComparison.Ordinal).Should().BeTrue();
    }

    /// <summary>
    /// "A group call that would re-enter the same group at the same text position fails that
    /// PATH".
    /// </summary>
    [Test]
    public void A_group_call_that_would_reenter_the_same_group_fails_that_path()
    {
        Match m = FuzzyRegex.FullMatch("abab", "(?P<g1>(?:ab)?(?&g1)?)");

        (m.Index, m.Length).Should().Be((0, 4));
    }

    /// <summary>
    /// "In a branch reset, a group never takes a number another group in the same branch will use".
    /// </summary>
    [Test]
    public void In_a_branch_reset_a_group_never_takes_a_number_another_group_will_use()
    {
        Match m = FuzzyRegex.FullMatch("BUG!", "(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))");
        Match n = FuzzyRegex.FullMatch("!BUG", "(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))");

        m.Groups["bug"].Value.Should().Be("BUG");
        n.Groups[2].Value.Should().Be("!");
    }

    /// <summary>"Reversed partial matches run out of text at the slice start".</summary>
    [Test]
    public void Reversed_partial_matches_run_out_of_text_at_the_slice_start()
    {
        var pattern = new FuzzyRegex("(?r)ya", FuzzyRegexOptions.None);
        Match m = pattern.Match("xya", beginning: 2, length: 1, partial: true);

        (m.Success, m.PartialMatch, m.Index).Should().Be((true, true, 2));
    }

    /// <summary>
    /// "A fuzzy section may open with an inserted character at the search anchor, where a position
    /// assertion pins the match there".
    /// </summary>
    [Test]
    public void A_fuzzy_section_may_open_with_an_inserted_character_at_the_search_anchor()
    {
        Match m = new FuzzyRegex("(?m)^(?:abc){i<=1}").Match("xabc");

        (m.Success ? m.Value : "no match").Should().Be("xabc");
    }
}
