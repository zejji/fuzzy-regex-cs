using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// A group that can call itself before it has matched any text is refused when the pattern is
/// compiled (known defects D4 and D72, owner decision 2026-10-08). Such a call can only recurse at
/// the position it started from, so the match it stands for is never found: upstream runs out of
/// memory on these patterns and PCRE2 stops with "recursive call could loop indefinitely" at match
/// time. The check is <c>LeftRecursion.Check</c>.
/// </summary>
/// <remarks>
/// <para>
/// The check reads the exact pattern. A node is a group read in one direction; it calls another
/// when the call can be reached from the group's start through items that can match empty. Text,
/// sets and characters never match empty, even inside a fuzzy section, so a fuzzy pattern is
/// judged by what it would do with no errors. <c>(*FAIL)</c> does not match empty; other verbs,
/// anchors and lookarounds do. A lookaround's contents are read in its own direction from the
/// current position. Both branches of a conditional are read; a <c>DEFINE</c> matches empty and
/// is not read in place. A backreference or call matches empty if any group with that number can,
/// and a named list if it holds the empty string.
/// </para>
/// <para>
/// Patterns the check lets through keep main's runtime rule, which fails a call re-entered at the
/// position it is already at. The evidence for the rule is in <c>docs/KNOWN-DEFECTS.md</c> (D4).
/// </para>
/// </remarks>
public sealed class LeftRecursionRefusalTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Test]
    // A group calls itself first.
    [Arguments(@"(?<A>(?&A)a|b)")]
    // The whole pattern calls itself after an empty branch.
    [Arguments(@"(?:|(?R)a)")]
    // Two groups call each other first.
    [Arguments(@"(?<A>(?&B)a|c)(?<B>(?&A)b)")]
    // Only an optional item stands before the call.
    [Arguments(@"(?<A>x*(?&A)a|b)")]
    // A call of a group that can match empty.
    [Arguments(@"(?<E>)(?<A>(?&E)(?&A)a|b)")]
    // An anchor matches empty.
    [Arguments(@"(?<A>^(?&A)a|b)")]
    // Through a lookahead, which reads from where the group started.
    [Arguments(@"(?<A>(?=(?&A))a|b)")]
    // Through either branch of a conditional.
    [Arguments(@"(?<A>(?(1)b|(?&A))a)")]
    [Arguments(@"(?<A>(?(?=x)(?&A)|b)a)")]
    // A DEFINE in place matches empty.
    [Arguments(@"(?<A>(?(DEFINE)x)(?&A)a|b)")]
    // A backreference to a group that can be empty.
    [Arguments(@"(?<B>x?)(?<A>(?P=B)(?&A)a|b)")]
    // Reversed: the call is the first item read.
    [Arguments(@"(?r)(?<A>a(?&A)|b)")]
    // A lookbehind's call runs the group backwards, where the lookbehind is read first.
    [Arguments(@"(?<A>b(?<=(?&A))|c)")]
    // A fuzzy section does not hide the cycle: it is judged with no errors.
    [Arguments(@"(?:(?R)a){e<=1}")]
    // A group number with more than one group behind it, through a branch reset or a name used
    // twice, can match empty if any of them can, whichever is written first.
    [Arguments(@"(?|(a)|())(?<B>\1(?&B)|b)")]
    [Arguments(@"(?|()|(a))(?<B>\1(?&B)|b)")]
    [Arguments(@"(?:(?<n>a)|(?<n>))(?<B>(?P=n)(?&B)|b)")]
    [Arguments(@"(?:(?<n>)|(?<n>a))(?<B>(?P=n)(?&B)|b)")]
    // Conservative on purpose: the check does not follow which groups are set. A backreference to
    // a group not yet set fails rather than matching empty, and a conditional on a group not yet
    // set takes its no-branch, but both are read as if they could match empty, so these two are
    // refused although the call can never run before text. Neither is a known idiom.
    [Arguments(@"(?<A>\2(?&A)|b)(a*)")]
    [Arguments(@"(?<A>(?(1)(?&A)|x)(a))")]
    public void A_group_that_can_call_itself_before_matching_text_is_refused(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);

        compile.Should().Throw<FuzzyRegexParseException>().WithMessage("*can call itself without matching any text");
    }

    [Test]
    // Right recursion: text comes first.
    [Arguments(@"(?<A>a(?&A)|b)")]
    // (*FAIL) never matches empty, so the call is never reached.
    [Arguments(@"(?<A>(*FAIL)(?&A)|b)")]
    // A backreference to a group that always reads text.
    [Arguments(@"(?<B>x)(?<A>(?P=B)(?&A)a|b)")]
    // Reversed: the call is read last.
    [Arguments(@"(?r)(?<A>(?&A)a|b)")]
    // Fuzzy text is not treated as empty, though a deletion could remove it.
    [Arguments(@"(?<A>(?:x){e<=1}(?&A)|y)")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){e<=1}")]
    // A lookbehind whose call reads text before it reaches the group's call.
    [Arguments(@"(?<A>(?<=a(?&A))b|c)")]
    // A group nobody calls never runs.
    [Arguments(@"(?(DEFINE)(?<A>(?&A)a))b")]
    // Not refused, though the call comes back to where A started: the lookbehind steps back over
    // the 'a' A has just read, and the check does not follow positions. Main's runtime rule fails
    // that call instead.
    [Arguments(@"(?<A>a(?<=(?=(?&A))a)|b)")]
    public void A_group_that_must_match_text_before_calling_itself_is_not_refused(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);

        compile.Should().NotThrow();
    }

    /// <summary>
    /// The offset is the call's, just after its <c>(?</c>, where upstream puts its own errors about
    /// a call (<c>ab(?1)</c>: "unknown group" at 4, regex 2026.9.10).
    /// </summary>
    [Test]
    public void The_error_names_the_group_and_points_at_the_call()
    {
        Action compile = static () => _ = new FuzzyRegex(@"(?(DEFINE)(?<list>(?&list),\w+|\w+))^(?&list)$");

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("group 'list' can call itself without matching any text");
        error.Offset.Should().Be(20);
    }

    [Test]
    public void The_error_for_the_whole_pattern_names_the_pattern()
    {
        Action compile = static () => _ = new FuzzyRegex(@"(?:|(?R)a)");

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("the pattern can call itself without matching any text");
        error.Offset.Should().Be(6);
    }

    [Test]
    public void The_error_for_an_unnamed_group_gives_its_number()
    {
        Action compile = static () => _ = new FuzzyRegex(@"x(a?(?1)b|c)");

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("group 1 can call itself without matching any text");
        error.Offset.Should().Be(6);
    }

    /// <summary>
    /// The patterns of the engine tests that pinned what the runtime re-entry guard and the
    /// failed-call memo did with a left-recursive pattern. Each is now refused, and its old test
    /// is gone; the old answer is quoted beside it. Upstream raised MemoryError on every one, and
    /// PCRE2 10.47 "nested recursion at the same subject position" where it was asked.
    /// </summary>
    [Test]
    // GroupCallTests.A_call_re_entered_at_the_position_it_is_already_at_fails_instead_of_recursing_for_ever:
    // (0, 4), (0, 4), (0, 4), None, (0, 2), (0, 0) and (0, 3). The third was also the sample in
    // docs/COMPARISON.md.
    [Arguments(@"(?P<g1>(?:a?)(?&g1)?)")]
    [Arguments(@"(?P<g1>(?:a*)(?&g1)?)")]
    [Arguments(@"(?P<g1>(?:ab)?(?&g1)?)")]
    [Arguments(@"(?:(?R))")]
    [Arguments(@"(?P<g>(?&g)a|b)")]
    [Arguments(@"(?P<g1>(?:ab)??(?&g1)?)")]
    [Arguments(@"(?P<g1>x(?&g1)?|(?&g1)?y)")]
    // GroupCallTests.A_left_recursive_call_nested_at_one_position_still_finds_its_match, ...
    // A_left_recursion_nests_as_deep_as_the_text_it_has_reached and
    // A_nested_call_that_reaches_no_further_is_still_refused: (0, n) over a^n, None over 'b'.
    // (?:|(?R)a) is in the idiom table below.
    [Arguments(@"(?r)(?:|a(?R))")]
    [Arguments(@"(?P<g>|(?&g)a)$")]
    [Arguments(@"^(?<g>|(?&g)a)$")]
    // GroupCallTests.A_later_search_attempt_measures_its_progress_from_its_own_start: (1, 4).
    [Arguments(@"(?:b.*x|(?<g>|(?&g)a)c)")]
    // GroupCallTests.A_reversed_right_recursion_nests_at_one_position_once_it_has_reached_further: (0, 4).
    [Arguments(@"(?r)((?:ab|a(?R)))")]
    [Arguments(@"(?r)(?p)((?:ab|a(?R)))")]
    // GroupCallTests.A_call_that_reaches_nothing_new_but_sees_a_new_capture_is_let_through: (0, 1).
    [Arguments(@"(?(a)(?(b)x|(?<b>)(?R))|(?<a>)(?R))")]
    [Arguments(@"(?:\1x|\2()(?R)|()(?R))")]
    [Arguments(@"(?:(?P=b)x|(?P=a)(?<b>)(?R)|(?<a>)(?R))")]
    // GroupCallTests.A_group_nothing_reads_is_left_out_of_the_call_guard_key, k = 8 and 9: (0, 1).
    [Arguments(@"(?:()|()|()|()|()|()|()|())(?R)|\1x")]
    [Arguments(@"(?:()|()|()|()|()|()|()|()|())(?R)|\1x")]
    // GroupCallTests.A_group_read_in_a_fuzzy_pattern_is_compared_by_its_span: (0, 3), one substitution.
    [Arguments(@"(?e)()(?:(?R)(?R)(?:(?=a\1)b|)(?:(?:b(?(1)a|b))*)?\1|b){s<=1}")]
    // GroupCallTests.A_capture_taken_inside_a_lookahead_is_part_of_the_call_guard_key: (0, 3).
    [Arguments(@".*z|\1b|(?(1)(?=(?<g>aa))|(?=(?<g>a)))(?R)")]
    // GroupCallTests.A_group_set_empty_between_two_calls_lets_the_inner_call_through: (0, 2).
    [Arguments(@"(?:(?(g)|(?<g>)))*|(?(g)b|a)(?R)(?:(?<g>)(?P=g))+|(?:(?<g>)(?P=g))+(?:(?<g>))*(?R)")]
    // GroupCallTests.A_group_captured_inside_a_repeat_is_keyed_on_its_span: (0, 3).
    [Arguments(@"(?:(?:(?:(?(g)|z)b)?.|(?:a(?(g)b)|a(?:(?<g>)|a)*)))|(?<g>)(?R)(?:(?R)|\1)|(?:(?P=g)(?R)|(?R))")]
    // GroupCallTests.A_call_guard_keyed_on_what_is_read_stays_polynomial: (0, 20), (0, 24),
    // (0, 12), None for the next eight, (0, 3), None for the next four, and (0, 2).
    [Arguments(@"(?:(?=(a*))|a)(?R)|(?(1)x)")]
    [Arguments(@"(?:()|a)(?R)|(?(1)x)")]
    [Arguments(@"(?:()|()|()|a)(?R)|(?(1)x)|(?(2)x)|(?(3)x)")]
    [Arguments(@"(?:()|a)(?R)|\1x")]
    [Arguments(@"(?:(?=(a*))|a)(?R)|\1x")]
    [Arguments(@"(?:()|()|()|()|()|()|a)(?R)|\1\2\3\4\5\6x")]
    [Arguments(@"(?:()|()|()|()|()|()|()|a)(?R)|\1\2\3\4\5\6\7x")]
    [Arguments(@"(?:()|()|()|()|()|()|()|()|a)(?R)|\1\2\3\4\5\6\7\8x")]
    [Arguments(@"(?:()|()|()|()|()|()|a)(?R)|(?(1)|z)(?(2)|z)(?(3)|z)(?(4)|z)(?(5)|z)(?(6)|z)x")]
    [Arguments(@"(?:()|()|()|()|()|()|()|()|a)(?R)|(?(1)|z)(?(2)|z)(?(3)|z)(?(4)|z)(?(5)|z)(?(6)|z)(?(7)|z)(?(8)|z)x")]
    [Arguments(@"(?:()|()|()|()|a)(?R)|\3\4x")]
    [Arguments(@"(?:()|()|()|()|a)(?R)|\1\2\3\4x|(?:qq){s<=1}")]
    [Arguments(@"(?:()|()|()|()|()|a)(?R)|\1\2\3\4\5x|(?:qq){s<=1}")]
    [Arguments(@"(?:()|()|()|()|()|()|a)(?R)|\1\2\3\4\5\6x|(?:qq){s<=1}")]
    [Arguments(@"(?:(?:())?|(?:())?|(?:())?|(?:())?|(?:())?|a)(?R)|\1\2\3\4\5x")]
    [Arguments(@"(?(DEFINE)(?<H>(?&G)|.*z|(?&G)|a)(?<G>(?&H)b))(?&H)")]
    // FailedCallMemoTests: A_call_inside_a_lookaround_keeps_the_memo_off (the lookahead runs the
    // pattern forwards, where (?R)? comes first), a row of
    // A_capture_group_inside_a_lookaround_leaves_the_memo_on, and The_key_holds_the_text_reached,
    // (0, 2) with the memo on and off.
    [Arguments(@"(?r)(((?R)?R(?!.(?)(?R))(.))){2<=e<3}")]
    [Arguments(@"(?1)|((?2)|aa|(?2)|a)((?1)c)")]
    // FuzzyNeededEmptyIterationTests.A_set_the_optimiser_merged_keeps_its_exit_under_a_call_from_a_section:
    // (0, 2) over 'zx' with no errors.
    [Arguments(@"(?:a|)z|(?:(?R)x){d<=1}")]
    [Arguments(@"(?:a|b|)z|(?:(?R)x){d<=1}")]
    [Arguments(@"(?:a?)z|(?:(?R)x){d<=1}")]
    [Arguments(@"(?:(?:a|b)?)z|(?:(?R)x){d<=1}")]
    // FuzzyRecursionCountsTests.An_optional_item_before_the_recursion_reports_its_substitution and
    // Bestmatch_finds_the_error_free_match_that_the_recursion_hid.
    [Arguments(@"(?:z?(?R)?){e<=1}")]
    // OpenDefectTests.A_left_recursion_is_let_through_when_the_reach_is_full_before_the_first_call
    // (D4's red test): upstream (0, 2) on both, the port None.
    [Arguments(@".*z|(?:|(?R)a)")]
    [Arguments(@"(?:a|(?<g>))*?(?P=g)|(?<g>)a(?R)|(?R)(?P=g)x")]
    // D72's witnesses, a refused call inside a committing construct (register, 2026-10-04).
    [Arguments(@"(?(DEFINE)(?<A>(?!(?&A)b)a|(?>(?&A))b?(?&A)|(?:|a)))(?&A)")]
    [Arguments(@"(?(DEFINE)(?<A>(?>(?&A)|ab)|a))(?&A)")]
    [Arguments(@"(?(DEFINE)(?<A>(?:(?&A))?+b?|a))(?&A)")]
    // FuzzyBestMatchTests.Bestmatch_reversed_keeps_the_partial_both_of_upstreams_doors_hand_back
    // (row 74947 of the seed 20260921 gate): (0, 4) partial, one substitution. Reversed, (?&g2)
    // is read first.
    [Arguments(
        "(?b)(?r)(\\d*)+?(?P<g2>(?:\\p{ASCII}\U00010428[[:alpha:]]{0,2}){e<=1}(?&g2)?)"
            + "(?:(?:A\U00010428(?:A){e<=2,s<=1}){e<=1}(*SKIP)\\p{Ll}|[[:alpha:]])"
    )]
    public void A_pattern_an_engine_test_used_to_match_is_refused(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().EndWith("can call itself without matching any text");
        pattern[..error.Offset].Should().EndWith("(?", "the offset is just inside the call");
    }

    /// <summary>
    /// The left-recursive idioms of the D4 measurement (<c>.scratch/refmeas/idioms.py</c>). Upstream
    /// runs out of memory on each and PCRE2 stops at match time; the port answered None or, for
    /// <c>(?:|(?R)a)</c>, a match that only main's runtime rule made reachable.
    /// </summary>
    [Test]
    [Arguments(@"(?(DEFINE)(?<list>(?&list),\w+|\w+))^(?&list)$")]
    [Arguments(@"(?(DEFINE)(?<e>(?&e)\+(?&t)|(?&t))(?<t>\d+))^(?&e)$")]
    [Arguments(@"(?:|(?R)a)")]
    [Arguments(@"(?(DEFINE)(?<list>(?:(?&list),)?+\w+))^(?&list)$")]
    [Arguments(@"(?(DEFINE)(?<e>(?>(?&e)\+)?\d+))^(?&e)$")]
    [Arguments(@"(?(DEFINE)(?<e>(?:(?&e)[-+])?+(?&t))(?<t>\d+|\((?&e)\)))^(?&e)$")]
    [Arguments(@"(?(DEFINE)(?<e>(?(?=(?&e)\+)(?&e)\+(?&t)|(?&t)))(?<t>\d+))^(?&e)$")]
    [Arguments(@"(?<x>(?>\s*(?&x)?)a)")]
    public void A_left_recursive_idiom_is_refused(string pattern)
    {
        Action compile = () => _ = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);

        compile.Should().Throw<FuzzyRegexParseException>();
    }

    /// <summary>
    /// The other idioms of the same measurement compile and answer as they did before the check.
    /// The expected answers are main's at 9c6e84c0.
    /// </summary>
    [Test]
    [Arguments(@"\((?:[^()]|(?R))*\)", "search", "f(x, g(y, z))", "(1,13)")]
    [Arguments(@"\((?>[^()]+|(?R))*\)", "search", "f(x, g(y, z))", "(1,13)")]
    [Arguments(@"(?<p>\((?:[^()]++|(?&p))*\))", "search", "f(x, g(y, z))", "(1,13)")]
    [Arguments(@"(\((?:[^()]|(?1))*\))", "search", "f(x, g(y, z))", "(1,13)")]
    [Arguments(@"\{(?:[^{}]|(?R))*\}", "search", "f{x, g{y, z}}", "(1,13)")]
    [Arguments(@"(?<b>\[(?:[^\[\]]++|(?&b))*\])", "search", "f[x, g[y, z]]", "(1,13)")]
    [Arguments(@"^((.)(?:(?1)|.?)\2)$", "fullmatch", "racecar", "(0,7)")]
    [Arguments(@"^(?:(.)(?:(?R)|.?)\1)$", "fullmatch", "racecar", "None")]
    [Arguments(
        @"(?(DEFINE)(?<expr>(?&term)(?:[+-](?&term))*)(?<term>(?&factor)(?:[*/](?&factor))*)(?<factor>\d+|\((?&expr)\)))^(?&expr)$",
        "fullmatch",
        "1+2*(3-4)/5+(6*(7+8))-9",
        "(0,23)"
    )]
    [Arguments(
        @"(?(DEFINE)(?<v>\s*(?:(?&o)|(?&a)|""[^""]*""|-?\d+(?:\.\d+)?|true|false|null)\s*)(?<o>\{(?:(?&v)(?::(?&v))?(?:,(?&v):(?&v))*)?\})(?<a>\[(?:(?&v)(?:,(?&v))*)?\]))^(?&v)$",
        "fullmatch",
        @"{""a"": [1, 2, {""b"": null, ""c"": [true, false]}], ""d"": ""x""}",
        "(0,56)"
    )]
    [Arguments(@"<(\w+)>(?:[^<]|(?R))*</\1>", "search", "<a><b>x</b></a>", "(0,15)")]
    [Arguments(@"/\*(?:[^*/]|\*(?!/)|/(?!\*)|(?R))*\*/", "search", "/* a */", "(0,7)")]
    [Arguments(@"\((?:\s*(?:[^()\s]+|(?R)))*\s*\)", "search", "(define (f x) (+ x 1))", "(0,22)")]
    [Arguments(@"(?(DEFINE)(?<A>a(?&A)?b))(?&A)", "fullmatch", "aaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbb", "(0,40)")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){e<=1}", "search", "((a)(b)", "(0,4) s1i0d0")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){e<=2}", "search", "((a)(b)", "(0,4) s1i0d0")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){e<=3}", "search", "((a)(b)", "(0,7) s3i0d0")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){d<=2}", "search", "((a)(b)", "(0,7) s0i0d1")]
    [Arguments(@"(?:\((?:[^()]|(?R))*\)){i<=1,d<=2}", "search", "((a)(b)", "(0,7) s0i1d2")]
    public void An_idiom_that_is_not_left_recursive_answers_as_before(
        string pattern,
        string operation,
        string subject,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);

        Match match = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(subject)
            : regex.Match(subject);

        Answer(match).Should().Be(expected);
    }

    /// <summary>
    /// The check is linear in the pattern. A chain of nested groups, each calling the one inside
    /// it, needs a group's answer before its parent's; reading every body again on each round of
    /// the fixpoint took 20.3 s at 1,000 groups (blind review, 2026-10-08), and 10-16 s in a Release
    /// build with that loop put back (2026-10-18). The chain is now 500 groups, the most the parser
    /// allows (D110), where that quadratic loop would cost about a quarter as much: 2.5 s or more.
    /// The whole compile takes about 20 ms alone at 500 (Release and Debug, 2026-10-19), and at
    /// 1,000 it took up to 2.3 s alongside the rest of the suite, about 0.6 s scaled to 500, so the
    /// test runs on its own and the bound sits between the two.
    /// </summary>
    [Test]
    [NotInParallel]
    public void Five_hundred_nested_groups_that_call_each_other_compile_quickly()
    {
        // (?<g1>(?<g2>...(?<g500>)(?&g500))...(?&g2))x
        System.Text.StringBuilder text = new();
        for (int i = 1; i < 500; i++)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"(?<g{i}>");
        }

        text.Append("(?<g500>)");
        for (int i = 499; i >= 1; i--)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"(?&g{i + 1}))");
        }

        string pattern = text.Append('x').ToString();
        TimeSpan elapsed = TimeSpan.MaxValue;

        // The parser recurses once per nesting level, deeper than a test thread's stack allows in
        // a Debug build, so the compile runs on a thread of its own.
        Thread compile = new(
            () =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                _ = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);
                elapsed = watch.Elapsed;
            },
            64 * 1024 * 1024
        );
        compile.Start();
        compile.Join();

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// A named list can match empty only if it holds the empty string; otherwise it is text.
    /// </summary>
    [Test]
    public void A_named_list_is_empty_only_if_it_holds_the_empty_string()
    {
        Dictionary<string, IReadOnlyCollection<string>> noEmpty = new(StringComparer.Ordinal) { ["w"] = ["-", "!"] };
        Dictionary<string, IReadOnlyCollection<string>> withEmpty = new(StringComparer.Ordinal) { ["w"] = ["-", ""] };

        Match m = new FuzzyRegex(@"(?<e>\L<w>(?&e)|\d+)", FuzzyRegexOptions.None, _timeout, noEmpty).Match("--5");
        Action refused = () => _ = new FuzzyRegex(@"(?<e>\L<w>(?&e)|\d+)", FuzzyRegexOptions.None, _timeout, withEmpty);

        (m.Index, m.Length).Should().Be((0, 3));
        refused.Should().Throw<FuzzyRegexParseException>().WithMessage("*can call itself without matching any text");
    }

    /// <summary>A call under a repeat with a maximum of 0 never runs, so it cannot recurse.</summary>
    [Test]
    public void A_call_repeated_at_most_zero_times_is_not_a_recursion()
    {
        Match m = new FuzzyRegex(@"(?<A>(?&A){0}a|b)", FuzzyRegexOptions.None, _timeout).Match("ab");

        (m.Index, m.Length).Should().Be((0, 1));
    }

    private static string Answer(Match match)
    {
        if (!match.Success)
        {
            return "None";
        }

        string span = $"({match.Index},{match.Index + match.Length})";
        FuzzyCounts counts = match.FuzzyCounts;
        return counts == default ? span : $"{span} s{counts.Substitutions}i{counts.Insertions}d{counts.Deletions}";
    }
}
