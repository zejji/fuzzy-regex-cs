using System.Text.RegularExpressions;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The edge cases an optimiser is tempted to special-case, pinned before Phase 7 touches the
/// engine: zero-width and empty matches, anchors under every flag combination, a matching timeout
/// firing inside a long scan, the megabyte subjects the benchmark suite measures, and pathological
/// backtracking.
/// </summary>
/// <remarks>
/// <para>
/// <b>These pins are permanent.</b> Phase 7 is where a prefilter, a fast path or a cached scan
/// position gets written, and every one of them is a chance to answer a different question quickly.
/// A Phase 7 slice that turns one of these red has changed an answer, and the fix is in the
/// optimisation rather than in the test. They sit beside the three files ROADMAP.md already names
/// as permanent for the same reason - <c>BacktrackingVerbTests</c>, <c>PartialMatchingTests</c> and
/// <c>ReverseMatchingTests</c>, which pin the answers upstream's own start optimisations get wrong.
/// </para>
/// <para>
/// <b>Provenance.</b> Every expected value upstream can express was measured by running
/// <c>tools/probes/upstream-optimiser-traps.py</c> against the installed <c>regex</c> 2026.9.10 on
/// 2026-09-16, and the answer is quoted beside the assertion. The two that upstream has no
/// equivalent for are the matching timeout and the lazy walk, both of which are
/// <c>docs/DIVERGENCES.md</c> rows: upstream has no timeout at all (its <c>TimeoutError</c> maps to
/// <c>RegexMatchTimeoutException</c>, "Exception mapping") and its <c>finditer</c> is a scanner
/// holding one state ("A lazy walk times each STEP").
/// </para>
/// <para>
/// The megabyte subjects are built to the same recipe as <c>bench/FuzzyRegex.Benchmarks/Corpus.cs</c>
/// and as the probe, so these pins pin exactly what the benchmark suite measures. The recipe is
/// repeated rather than shared because the test project does not reference the benchmark project;
/// the probe's printed lengths (1048631 and 1048625) are what keeps the three copies honest, and
/// the first test below asserts them.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class OptimiserTrapsTests
{
    /// <summary>The filler sentence: forty-four characters, no <c>needle</c>, no digits.</summary>
    private const string _sentence = "the quick brown fox jumps over the lazy dog ";

    /// <summary>What the long subjects are padded to before their distinguishing tail.</summary>
    private const int _megabyte = 1024 * 1024;

    /// <summary>A megabyte of filler ending in the one findable <c>needle</c>.</summary>
    private static readonly string _long = Pad("a needle in a haystack.");

    /// <summary>A megabyte of filler ending in a strict prefix of the partial pattern.</summary>
    private static readonly string _longPartial = Pad("a needle in a hay");

    /// <summary>
    /// A hundred kilobytes of the same filler: the size at which draining the LAZY walk to the end
    /// is affordable. Measured 2026-09-16 with <c>dotnet run -c Release --project
    /// bench/FuzzyRegex.Benchmarks -- sizing</c>: the full lazy walk of <c>\w+</c> costs 117 ms
    /// here and 12,643 ms over the megabyte, against 4.51 ms and 111 ms for the eager one. The
    /// per-step <c>MatchState</c> is quadratic in the subject (<c>OPTIMISATION-NOTES.md</c>), so
    /// the megabyte case is pinned only where the walk stops early.
    /// </summary>
    private static readonly string _dense = Pad("a needle in a haystack.", 100 * 1024);

    /// <summary>
    /// The benchmark suite's fuzzy-ranking subject: a misspelled <c>haystack</c> the ranking modes
    /// can improve on. Same string as <c>Corpus.Fuzzy</c>.
    /// </summary>
    private const string _fuzzySubject = _sentence + "and finds a haystakc.";

    /// <summary>Builds a subject of at least <paramref name="size"/> filler characters plus a tail.</summary>
    /// <param name="tail">The distinguishing tail, appended once.</param>
    /// <param name="size">How much filler to lay down first.</param>
    /// <returns>The subject.</returns>
    private static string Pad(string tail, int size = _megabyte)
    {
        System.Text.StringBuilder builder = new(size + tail.Length);
        while (builder.Length < size)
        {
            builder.Append(_sentence);
        }

        return builder.Append(tail).ToString();
    }

    /// <summary>Formats every match of a pattern as the probe prints its spans.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="options">The options to compile under.</param>
    /// <returns>Space-separated <c>(start,end)</c> pairs, or the empty string for no match.</returns>
    private static string Spans(string pattern, string subject, FuzzyRegexOptions options = FuzzyRegexOptions.None) =>
        string.Join(
            ' ',
            new FuzzyRegex(pattern, options).Matches(subject).Select(static m => $"({m.Index},{m.Index + m.Length})")
        );

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_megabyte_subjects_are_the_ones_the_probe_and_the_benchmarks_use()
    {
        // The three copies of this recipe drift silently otherwise, and a pin measured against a
        // different subject from the benchmark is not a pin on the benchmark.
        //
        // regex 2026.9.10: len(LONG)=1048631 len(LONG_PARTIAL)=1048625 len(DENSE)=102455
        //                  len(FUZZY)=65.
        _long.Length.Should().Be(1048631);
        _longPartial.Length.Should().Be(1048625);
        _dense.Length.Should().Be(102455);
        _fuzzySubject.Length.Should().Be(65);
    }

    // ---------------------------------------------------------------------------------------
    // Zero-width and empty matches. Every one of these is a position an optimiser that advances
    // "past the match" rather than "past the match or one character" either skips or loops on.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void An_empty_pattern_matches_at_every_position_including_the_end()
    {
        // regex 2026.9.10: finditer('', 'abc') -> [(0,0), (1,1), (2,2), (3,3)]; on '' -> [(0,0)].
        Spans("", "abc").Should().Be("(0,0) (1,1) (2,2) (3,3)");
        Spans("", "").Should().Be("(0,0)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_star_quantifier_reports_the_empty_match_that_follows_a_consumed_one()
    {
        // The trap: after 'a*' consumes (1,4) of 'baaac' there is still an empty match AT 4, and
        // then one at the end. An optimiser that resumes the scan at the match end and forbids an
        // empty match there loses both.
        //
        // regex 2026.9.10: finditer('a*', 'abc') -> [(0,1), (1,1), (2,2), (3,3)], and
        //                  finditer('a*', 'baaac') -> [(0,0), (1,4), (4,4), (5,5)], and
        //                  finditer('x*', 'abc') -> [(0,0), (1,1), (2,2), (3,3)].
        Spans("a*", "abc").Should().Be("(0,1) (1,1) (2,2) (3,3)");
        Spans("a*", "baaac").Should().Be("(0,0) (1,4) (4,4) (5,5)");
        Spans("x*", "abc").Should().Be("(0,0) (1,1) (2,2) (3,3)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Zero_width_assertions_match_at_the_positions_they_assert_about()
    {
        // regex 2026.9.10: finditer(r'\b', 'ab cd') -> [(0,0), (2,2), (3,3), (5,5)], and
        //                  finditer('(?=b)', 'abcb') -> [(1,1), (3,3)].
        Spans(@"\b", "ab cd").Should().Be("(0,0) (2,2) (3,3) (5,5)");
        Spans("(?=b)", "abcb").Should().Be("(1,1) (3,3)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Substitution_and_split_see_the_empty_matches_at_both_ends()
    {
        // regex 2026.9.10: sub('', '-', 'abc') -> '-a-b-c-', and
        //                  split('', 'abc') -> ['', 'a', 'b', 'c', ''], and
        //                  split('a*', 'baaac') -> ['', 'b', '', 'c', ''].
        new FuzzyRegex("")
            .Replace("abc", "-")
            .Should()
            .Be("-a-b-c-");
        new FuzzyRegex("").Split("abc").Should().Equal("", "a", "b", "c", "");
        new FuzzyRegex("a*").Split("baaac").Should().Equal("", "b", "", "c", "");
    }

    // ---------------------------------------------------------------------------------------
    // Anchors under every flag combination. A leading-anchor optimisation that reads the flags
    // wrong answers one line instead of all of them, which is the S16 bug a wave found.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The anchor grid over <c>"a\nb\na"</c>, measured on <c>regex</c> 2026.9.10 on 2026-09-16.
    /// </summary>
    /// <param name="pattern">The anchored pattern.</param>
    /// <param name="options">The flag combination.</param>
    /// <param name="expected">The spans upstream answers, as the probe prints them.</param>
    [Test]
    [Arguments("^a", FuzzyRegexOptions.None, "(0,1)")]
    [Arguments("a$", FuzzyRegexOptions.None, "(4,5)")]
    [Arguments(@"\Aa", FuzzyRegexOptions.None, "(0,1)")]
    [Arguments(@"a\Z", FuzzyRegexOptions.None, "(4,5)")]
    [Arguments("^a", FuzzyRegexOptions.Multiline, "(0,1) (4,5)")]
    [Arguments("a$", FuzzyRegexOptions.Multiline, "(0,1) (4,5)")]
    [Arguments(@"\Aa", FuzzyRegexOptions.Multiline, "(0,1)")]
    [Arguments(@"a\Z", FuzzyRegexOptions.Multiline, "(4,5)")]
    [Arguments("^a", FuzzyRegexOptions.Singleline, "(0,1)")]
    [Arguments("a$", FuzzyRegexOptions.Singleline, "(4,5)")]
    [Arguments(@"\Aa", FuzzyRegexOptions.Singleline, "(0,1)")]
    [Arguments(@"a\Z", FuzzyRegexOptions.Singleline, "(4,5)")]
    [Arguments("^a", FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Singleline, "(0,1) (4,5)")]
    [Arguments("a$", FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Singleline, "(0,1) (4,5)")]
    [Arguments(@"\Aa", FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Singleline, "(0,1)")]
    [Arguments(@"a\Z", FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Singleline, "(4,5)")]
    [Property("Upstream", "none - gap test")]
    public void Anchors_answer_the_same_under_every_flag_combination(
        string pattern,
        FuzzyRegexOptions options,
        string expected
    ) => Spans(pattern, "a\nb\na", options).Should().Be(expected);

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_dollar_anchor_steps_over_a_trailing_newline_and_the_Z_anchor_does_not()
    {
        // The classic special case: '$' matches before a final newline, '\Z' does not, and
        // MULTILINE does not change either answer on this subject.
        //
        // regex 2026.9.10: finditer('a$', 'a\n') -> [(0,1)], with MULTILINE -> [(0,1)], and
        //                  finditer(r'a\Z', 'a\n') -> [].
        Spans("a$", "a\n").Should().Be("(0,1)");
        Spans("a$", "a\n", FuzzyRegexOptions.Multiline).Should().Be("(0,1)");
        Spans(@"a\Z", "a\n").Should().Be("");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void An_empty_line_anchors_to_itself_at_every_line_start()
    {
        // regex 2026.9.10: finditer('^$', '') -> [(0,0)], and
        //                  finditer('^$', '\n', MULTILINE) -> [(0,0), (1,1)].
        Spans("^$", "").Should().Be("(0,0)");
        Spans("^$", "\n", FuzzyRegexOptions.Multiline).Should().Be("(0,0) (1,1)");
    }

    // ---------------------------------------------------------------------------------------
    // Pathological backtracking, and the timeout.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Both_pathological_backtracking_shapes_complete_with_no_match_on_a_short_run()
    {
        // regex 2026.9.10: search('(a+)+b', 'a'*16) -> None, 'a'*22 -> None, 'a'*24 -> None, and
        //                  search('(a+)+b', 'a'*24 + 'b') -> (0, 25),
        //                  with the same four answers for '(a|a)*b'.
        //
        // Upstream answers all of them instantly because locate_required_string rejects a subject
        // with no 'b' in it before the engine runs (S19). This port has no prefilter yet
        // (OPTIMISATION-NOTES.md), so it explores the space, and THE TWO SHAPES ARE NOT ALIKE here
        // - measured 2026-09-16 with the `sizing` mode of the benchmark project:
        //
        //   n     (a+)+b     (a|a)*b
        //   18    0.67 ms    160.92 ms
        //   22    0.21 ms  2,649.52 ms
        //   24    0.36 ms 10,646.58 ms
        //
        // The right-hand column's absolute values swing on a busy machine - n=24 came out at
        // 6,497 ms, 9,832 ms and 10,647 ms across three runs. What reproduces, and what is being
        // relied on here, is the SHAPE: one column flat, the other doubling per character.
        //
        // '(a+)+b' is flat in n because the inner 'a+' is one greedy repeat with nothing to
        // redistribute; '(a|a)*b' doubles per character. Both are pinned so that a Phase 7 change
        // moving a shape between the two classes is visible, and the run lengths here are the ones
        // that keep the test fast.
        //
        // THE ANSWER is what is pinned, not the speed: a prefilter must still answer no match, and
        // must still find (0,25) on the subject that ends in 'b'.
        var nested = new FuzzyRegex("(a+)+b");
        var alternation = new FuzzyRegex("(a|a)*b");

        nested.IsMatch(new string('a', 16)).Should().BeFalse();
        nested.IsMatch(new string('a', 24)).Should().BeFalse();
        alternation.IsMatch(new string('a', 16)).Should().BeFalse();

        Match byNesting = nested.Match(new string('a', 24) + "b");
        Match byAlternation = alternation.Match(new string('a', 24) + "b");

        (byNesting.Index, byNesting.Length).Should().Be((0, 25));
        (byAlternation.Index, byAlternation.Length).Should().Be((0, 25));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Pathological_backtracking_stops_at_the_timeout_rather_than_running_forever()
    {
        // The exponential shape over a run long enough that 2^n is not reachable: 2,649.52 ms at
        // n=22 (sizing run, 2026-09-16), against the 100 ms budget below.
        //
        // S60 changed the tail from 'b' to '\b\B', and the reason is the point of this test. The
        // old comment here read "upstream answers None instantly because of the prefilter it has
        // and this port does not". This port has it now, so '(a|a)*b' over a run of 'a' is refused
        // by both engines before matching starts and costs nothing to fail. What a prefilter cannot
        // do is rescue a pattern with no literal in it: '\b\B' is a contradiction, false at every
        // position, so the exponential search still has to be made and the only bound is still the
        // timeout - which remains documented behaviour rather than a defect.
        //
        // n=22 rather than something larger on purpose: if the timeout ever STOPS firing, this
        // test costs 2.6 seconds instead of the 86.5 seconds '(a+)+b' at n=2000 was measured to
        // take. A pathological-case test should fail fast when it fails.
        //
        // DIVERGENCES.md, "Exception mapping": a matching timeout raises
        // RegexMatchTimeoutException where upstream raises TimeoutError.
        //
        // The '\1' was added on 2026-09-26: the failure memo makes '(a|a)*\b\B' fail in linear
        // time, and a pattern with a backreference gets no memo. 2,170 ms at n=22 with it
        // (Release, 2026-09-26).
        var pattern = new FuzzyRegex(@"(a|a)*\1\b\B");
        string subject = new('a', 22);

        Action act = () => pattern.IsMatch(subject, timeout: TimeSpan.FromMilliseconds(100));

        act.Should().Throw<RegexMatchTimeoutException>();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_timeout_fires_inside_a_long_scan_rather_than_only_between_matches()
    {
        // The pattern is one that NEVER MATCHES, so the megabyte is one uninterrupted scan with no
        // match boundary anywhere in it: the only way to notice a one-millisecond budget is to poll
        // the clock inside the matching loop. `\w+` would not test that - it finds 214,493 matches,
        // so an engine that only checked between matches would pass and the Phase 7 fast path this
        // is here to guard against would slip straight through.
        //
        // regex 2026.9.10: search(r'\b\B', LONG) -> None, so there is genuinely nothing to find.
        // DIVERGENCES.md, "Exception mapping": a matching timeout raises RegexMatchTimeoutException
        // where upstream raises TimeoutError. Upstream has no timeout to compare with.
        //
        // S60 changed this from 'zebra'. That was a REAL catch by this test, not a stale workload:
        // the required-string prefilter it was written to guard against landed, and a vectorised
        // IndexOf over the megabyte answers None in microseconds, so no budget could fire. The
        // prefilter now polls the same cancellation check the matching loop uses, once per 64 Ki
        // chunk (Matcher.StringSearch), which is what keeps the promise for a subject large enough
        // to matter; this test keeps its own half of the contract by using a pattern with no
        // literal, so it still measures the position-by-position scan.
        //
        // S60b: the warm-up call comes first because a cold run can spend the whole budget on JIT
        // before the first poll, and then the test passes on a scan that never polls again. That
        // hid a real regression for one run: once 'try_match' answered '\B' itself, search_start's
        // retry loop walked the megabyte without returning to the matching loop, and the test
        // passed alone and failed only in the full suite.
        var pattern = new FuzzyRegex(@"\b\B");
        _ = pattern.IsMatch(_long);

        Action act = () => pattern.IsMatch(_long, timeout: TimeSpan.FromMilliseconds(1));

        act.Should().Throw<RegexMatchTimeoutException>();

        // And the eager walk is bounded too, which is the other half of the contract.
        Action walk = () => _ = new FuzzyRegex(@"\w+").Matches(_long, timeout: TimeSpan.FromMilliseconds(1)).Count;

        walk.Should().Throw<RegexMatchTimeoutException>();
    }

    [Test]
    [Property("Upstream", "regex 2026.9.10, the answers only; upstream is quadratic here")]
    public void A_leading_repeat_inside_a_capture_group_is_skipped_like_a_bare_one()
    {
        // When a search attempt that starts with a single-character repeat fails, upstream moves
        // the next attempt past every character the repeat matched (the FAILURE arm, :15719). An
        // attempt starting inside that run can only try a subset of the paths the failed one
        // tried. Upstream looks only at the start node, so the same repeat inside a capture group
        // gets no skip and the search is quadratic. regex 2026.9.10, 2026-09-23, over 'a' * n:
        //
        //   n       (?s).*a\d    (?s)(.*)a\d
        //   2,000   0.20 ms      75.84 ms
        //   4,000   0.09 ms      301.63 ms
        //   8,000   0.18 ms      1,239.38 ms
        //
        // This port looked the same (2,643 ms at n = 8,000) until S60b item 17 looked through the
        // group. The one-second budget is several hundred times what the skip needs.
        var pattern = new FuzzyRegex(@"(?s)(.*)a\d");
        string subject = new('a', 8_000);
        _ = pattern.IsMatch("warm-up");

        Action act = () => pattern.IsMatch(subject, timeout: TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
        pattern.IsMatch(subject).Should().BeFalse();

        // regex 2026.9.10: search(r'(?s)(.*)a\d', 'xa1ya2') -> (0, 6), group 1 (0, 4)
        Match greedy = pattern.Match("xa1ya2");
        (greedy.Index, greedy.Length, greedy.Groups[1].Index, greedy.Groups[1].Length).Should().Be((0, 6, 0, 4));

        // regex 2026.9.10: finditer(r'(\d+)x', '123 45x') -> one match (4, 7), group 1 (4, 6)
        Match digits = new FuzzyRegex(@"(\d+)x").Match("123 45x");
        (digits.Index, digits.Length, digits.Groups[1].Index, digits.Groups[1].Length).Should().Be((4, 3, 4, 2));

        // regex 2026.9.10: search(r'(a*)b', 'aaac aab') -> (5, 8), group 1 (5, 7)
        Match run = new FuzzyRegex("(a*)b").Match("aaac aab");
        (run.Index, run.Length, run.Groups[1].Index, run.Groups[1].Length).Should().Be((5, 3, 5, 2));
    }

    [Test]
    [Property("Upstream", "regex 2026.9.10, 2026-09-24")]
    public void A_lazy_leading_repeat_inside_a_capture_group_is_not_skipped()
    {
        // The skip is sound only when the failed attempt tried every end position of the run. A
        // greedy repeat does; a lazy one can be cut short by a verb, so the look-through stops at
        // greedy repeats. Upstream skips for the bare lazy repeat but not the grouped one:
        // finditer(r'a*?(*SKIP)', 'aaab') gives (0, 0), (4, 4), and the grouped form below gives
        // every position.
        // regex 2026.9.10: search(r'(?i)(a*?)(*SKIP)b', 'aab') -> (2, 3)
        Match skip = new FuzzyRegex(@"(?i)(a*?)(*SKIP)b").Match("aab");
        (skip.Success, skip.Index, skip.Length).Should().Be((true, 2, 1));

        // regex 2026.9.10: finditer(r'(a*?)(*SKIP)', 'aaab') -> (0, 0), (1, 1), (2, 2), (3, 3), (4, 4)
        new FuzzyRegex(@"(a*?)(*SKIP)")
            .Matches("aaab")
            .Select(static match => match.Index)
            .Should()
            .Equal(0, 1, 2, 3, 4);
    }

    [Test]
    [Property("Upstream", "regex 2026.9.10, 2026-09-24, the answers; upstream tries every position")]
    public void An_alternation_of_literals_searches_for_all_of_them_at_once()
    {
        // A single literal is found with one vectorised search; an alternation of literals had
        // none, so every position ran each alternative in turn. Over the four megabytes below
        // this port took about 120 ms and upstream 162 ms; S60b item 14 searches for every
        // alternative's first literal at once and lets the engine pick the branch. The budget is
        // several times what the old scan needed and far more than the search takes.
        // regex 2026.9.10: search(<this pattern>, <this subject>) -> (4194346, 4194352)
        var pattern = new FuzzyRegex("amber|basil|cedar|delta|ember|flint|grove|heron|needle");
        string subject = Pad("a needle in a haystack.", 4 * _megabyte);
        _ = pattern.IsMatch("warm-up");

        Action act = () => pattern.Match(subject, timeout: TimeSpan.FromMilliseconds(25));

        act.Should().NotThrow();
        Match found = pattern.Match(subject);
        (found.Index, found.Length).Should().Be((4_194_346, 6));

        // regex 2026.9.10, finditer, spans as (start, end): cat|dog over 'hotdog catalog' gives
        // (3, 6) and (7, 10); (cat|dog)s over 'dogcats' gives (3, 7) with group 1 'cat'; x|yz
        // over 'yyzx' gives (1, 3) and (3, 4); abc|bcd over 'abcd' gives (0, 3).
        Spans("cat|dog", "hotdog catalog").Should().Equal((3, 3), (7, 3));
        Spans("(cat|dog)s", "dogcats").Should().Equal((3, 4));
        new FuzzyRegex("(cat|dog)s").Match("dogcats").Groups[1].Value.Should().Be("cat");
        Spans("x|yz", "yyzx").Should().Equal((1, 2), (3, 1));
        Spans("abc|bcd", "abcd").Should().Equal((0, 3));

        // regex 2026.9.10: search('cat|dog', 'cat dog', pos=1) -> (4, 7); endpos=3 over 'xcatx' ->
        // None, since the literal must end inside the slice; partial=True over 'xca' -> (1, 3),
        // a partial match the literal search alone would not find.
        new FuzzyRegex("cat|dog")
            .Match("cat dog", 1)
            .Index.Should()
            .Be(4);
        new FuzzyRegex("cat|dog").Match("xcatx", 0, 3).Success.Should().BeFalse();
        Match partial = new FuzzyRegex("cat|dog").Match("xca", partial: true);
        (partial.Success, partial.Index, partial.Length, partial.PartialMatch).Should().Be((true, 1, 2, true));

        static List<(int, int)> Spans(string pattern, string subject) =>
            [.. new FuzzyRegex(pattern).Matches(subject).Select(static match => (match.Index, match.Length))];
    }

    [Test]
    [Property("Upstream", "regex 2026.9.10, 2026-09-24, the answers; upstream tries every member")]
    [NotInParallel]
    public void A_long_named_list_tries_only_the_members_that_can_start_here()
    {
        // A named list compiles to one alternative per member, and every attempt used to try each
        // alternative's first character in turn: a thousand members cost a thousand tests at every
        // word boundary. S60b item 9 jumps straight to the members that start with the text's
        // next one or two code units. Over the megabyte below this port took about 1,840 ms before
        // and 80 ms after, and upstream 1,099 ms. The test runs on its own because the rest of the
        // suite, running alongside, made the same search take 700 ms. The members come from a small LCG so the Python probe can
        // rebuild them.
        // regex 2026.9.10: search(r'\b\L<words>\b', <this subject>, words=<these members>) ->
        // (1048610, 1048616)
        List<string> members = [.. Enumerable.Range(0, 1000).Select(Member), "needle"];
        var words = new Dictionary<string, IReadOnlyCollection<string>> { ["words"] = members };
        var pattern = new FuzzyRegex(@"\b\L<words>\b", FuzzyRegexOptions.None, words);
        string subject = Pad("a needle in a haystack.", _megabyte);
        _ = pattern.IsMatch("warm-up");

        Func<Match> act = () => pattern.Match(subject, timeout: TimeSpan.FromMilliseconds(500));

        Match found = act.Should().NotThrow().Subject;
        (found.Index, found.Length).Should().Be((1_048_610, 6));

        // Ten members, enough to be indexed (BranchChain.MinAlternatives): one and two code units
        // long, several sharing a first unit, and one with an astral first character. Upstream's
        // answers, from regex 2026.9.10 finditer with w=['ab', 'abc', 'b', 'xyz', 'ac', 'cab',
        // 'a', 'ba', '\U0001F600x', 'bb'], as (start, end) spans in code points: \L<w> over
        // 'abcab xyz' gives (0, 3), (3, 5) and (6, 9). \L<w>c over 'abcc' gives (0, 4).
        // (?:\L<w>)+?$ over 'abcab' gives (0, 5). x|\L<w>|y over 'yxab' gives (0, 1), (1, 2) and
        // (2, 4). \L<w>(*SKIP)d|b over 'abcd abd' gives (0, 4) and (5, 8). \L<w> over
        // '\U0001F600x\U0001F600ba' gives (0, 2) and (3, 5). \L<w>$ over 'zab' gives (1, 3). With
        // partial=True, \L<w> over 'abcab xy' gives (0, 3), (3, 5) and the partial match (6, 8),
        // whose second code unit is past the slice.
        Spans(@"\L<w>", "abcab xyz").Should().Equal((0, 3), (3, 2), (6, 3));
        Spans(@"\L<w>c", "abcc").Should().Equal((0, 4));
        Spans(@"(?:\L<w>)+?$", "abcab").Should().Equal((0, 5));
        Spans(@"x|\L<w>|y", "yxab").Should().Equal((0, 1), (1, 1), (2, 2));
        Spans(@"\L<w>(*SKIP)d|b", "abcd abd").Should().Equal((0, 4), (5, 3));
        Spans(@"\L<w>", "\U0001F600x\U0001F600ba").Should().Equal((0, 3), (5, 2));
        Spans(@"\L<w>$", "zab").Should().Equal((1, 2));
        Spans(@"\L<w>", "abcab xy", partial: true).Should().Equal((0, 3), (3, 2), (6, 2));

        static string Member(int index)
        {
            uint state = unchecked(((uint)index * 2654435761u) + 1);
            char[] letters = new char[5 + (index % 4)];
            for (int i = 0; i < letters.Length; i++)
            {
                state = unchecked((state * 1103515245u) + 12345u);
                letters[i] = (char)('a' + ((state >> 16) % 26));
            }

            return new string(letters);
        }

        static List<(int, int)> Spans(string pattern, string subject, bool partial = false)
        {
            var small = new Dictionary<string, IReadOnlyCollection<string>>
            {
                ["w"] = ["ab", "abc", "b", "xyz", "ac", "cab", "a", "ba", "\U0001F600x", "bb"],
            };
            return
            [
                .. new FuzzyRegex(pattern, FuzzyRegexOptions.None, small)
                    .Matches(subject, 0, subject.Length, overlapped: false, partial)
                    .Select(static match => (match.Index, match.Length)),
            ];
        }
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [NotInParallel]
    public void An_indexed_alternation_with_many_unkeyed_members_builds_in_linear_space()
    {
        // Half of these 8,000 alternatives start with a character set, which the index cannot key,
        // and the other half each start with a different character. The first version of
        // BranchChain copied the unkeyed half into every key's list: 4,000 keys times 4,000
        // entries, three times over, about 128 MB for this pattern and 2 GB at 32,000 alternatives
        // (S60b item 9 review). Built once more on its own here, the index should cost a few
        // entries per alternative.
        string pattern = string.Join(
            "|",
            Enumerable
                .Range(0, 8000)
                .Select(static i => i % 2 == 0 ? $"{(char)(0x4E00 + i)}q" : $"[{(char)(0x4E00 + i)}z]q")
        );
        List<Node> nodes = new FuzzyRegex(pattern).PatternObject.NodeList;
        nodes.Count(static node => node.Chain is not null).Should().Be(7999);
        foreach (Node node in nodes)
        {
            node.Chain = null;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        BranchChain.Build(nodes);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().BeLessThan(8 * _megabyte);
        var regex = new FuzzyRegex(pattern);
        regex.Match($"x{(char)(0x4E00 + 7999)}q").Index.Should().Be(1);
        regex.Match($"x{(char)(0x4E00 + 7998)}zq").Index.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_two_ranking_modes_improve_on_the_plain_fuzzy_answer()
    {
        // This pins the three fuzzy-ranking BENCHMARKS, not just the engine. Their first version
        // ran `(?:haystack){e<=3}` against a subject containing nothing like it, so all three
        // answered no match in identical time and the workload named as the likeliest place to
        // regress was measuring a failed scan. What makes it a real workload is that the plain
        // answer and the ranked answer DIFFER, and that is what is asserted here.
        //
        // regex 2026.9.10: search('(?:haystack){e<=3}', FUZZY) -> span (54,63), fuzzy_counts
        //                  (0,2,1), where (?e) and (?b) both -> span (56,63), counts (0,0,1).
        // Python's fuzzy_counts is (substitutions, insertions, deletions), the same order as
        // FuzzyCounts here.
        Match plain = new FuzzyRegex("(?:haystack){e<=3}").Match(_fuzzySubject);
        Match enhanced = new FuzzyRegex("(?e)(?:haystack){e<=3}").Match(_fuzzySubject);
        Match best = new FuzzyRegex("(?b)(?:haystack){e<=3}").Match(_fuzzySubject);

        (plain.Index, plain.Index + plain.Length).Should().Be((54, 63));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 1));
        (enhanced.Index, enhanced.Index + enhanced.Length).Should().Be((56, 63));
        enhanced.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        (best.Index, best.Index + best.Length).Should().Be((56, 63));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    // ---------------------------------------------------------------------------------------
    // The megabyte subjects, workload by workload.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_scan_finds_the_single_literal_at_the_end()
    {
        // regex 2026.9.10: search('needle', LONG).span() -> (1048610, 1048616).
        Match found = new FuzzyRegex("needle").Match(_long);

        (found.Index, found.Index + found.Length).Should().Be((1048610, 1048616));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_scan_counts_the_same_matches_upstream_does()
    {
        // regex 2026.9.10: len(finditer('[a-z]{3}[^a-z]', LONG)) -> 214490, and
        //                  len(finditer('QUICK', LONG, IGNORECASE|FULLCASE)) -> 23832, and
        //                  len(finditer(r'\w+', LONG)) -> 214493.
        new FuzzyRegex("[a-z]{3}[^a-z]")
            .Count(_long)
            .Should()
            .Be(214490);
        new FuzzyRegex("QUICK", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
            .Count(_long)
            .Should()
            .Be(23832);
        new FuzzyRegex(@"\w+").Count(_long).Should().Be(214493);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_reverse_scan_that_finds_nothing_walks_the_whole_subject()
    {
        // regex 2026.9.10: search('(?r)zebra', LONG) -> None.
        new FuzzyRegex("zebra", FuzzyRegexOptions.RightToLeft)
            .IsMatch(_long)
            .Should()
            .BeFalse();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_partial_match_reports_the_tail_that_could_still_complete()
    {
        // regex 2026.9.10: compile(r'a needle in a haystack\.').search(LONG_PARTIAL, partial=True)
        //                  -> span (1048608, 1048625), partial=True.
        Match found = new FuzzyRegex(@"a needle in a haystack\.").Match(_longPartial, partial: true);

        (found.Index, found.Index + found.Length, found.PartialMatch).Should().Be((1048608, 1048625, true));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_eager_walk_of_a_megabyte_answers_the_matches_upstream_does()
    {
        // regex 2026.9.10: len(finditer(r'\w+', LONG)) -> 214493, first two spans [(0,3), (4,9)],
        //                  last span (1048622, 1048630).
        MatchCollection eager = new FuzzyRegex(@"\w+").Matches(_long);

        eager.Count.Should().Be(214493);
        (eager[0].Index, eager[0].Index + eager[0].Length).Should().Be((0, 3));
        (eager[1].Index, eager[1].Index + eager[1].Length).Should().Be((4, 9));
        (eager[^1].Index, eager[^1].Index + eager[^1].Length).Should().Be((1048622, 1048630));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_eager_and_lazy_walks_answer_the_same_matches_end_to_end()
    {
        // Drained to the end, both ways, at the size where draining the lazy one is affordable -
        // see the _dense remarks: 117 ms here against 12.6 seconds over the megabyte, because the
        // per-step MatchState makes the lazy walk quadratic in the subject.
        //
        // regex 2026.9.10: len(finditer(r'\w+', DENSE)) -> 20957, first two spans [(0,3), (4,9)],
        //                  last span (102446, 102454).
        var pattern = new FuzzyRegex(@"\w+");

        MatchCollection eager = pattern.Matches(_dense);
        List<Match> lazy = [.. pattern.EnumerateMatches(_dense)];

        eager.Count.Should().Be(20957);
        lazy.Count.Should().Be(20957);
        (lazy[0].Index, lazy[0].Index + lazy[0].Length).Should().Be((0, 3));
        (lazy[1].Index, lazy[1].Index + lazy[1].Length).Should().Be((4, 9));
        (lazy[^1].Index, lazy[^1].Index + lazy[^1].Length).Should().Be((102446, 102454));
        lazy.Select(static m => (m.Index, m.Length)).Should().Equal(eager.Select(static m => (m.Index, m.Length)));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_lazy_walk_stopped_after_two_answers_the_same_first_two()
    {
        // The other half of the pair: a caller who wants two matches of a megabyte should not pay
        // for 214,493 of them. What is pinned here is the ANSWER; the cost is in the baselines,
        // where the same two matches cost 0.14 ms lazily against 111 ms eagerly (sizing run,
        // 2026-09-16) - which is also why this is the only megabyte case walked lazily at all.
        //
        // regex 2026.9.10: first two spans of finditer(r'\w+', LONG) -> [(0,3), (4,9)].
        List<Match> firstTwo = [.. new FuzzyRegex(@"\w+").EnumerateMatches(_long).Take(2)];

        firstTwo.Should().HaveCount(2);
        (firstTwo[0].Index, firstTwo[0].Index + firstTwo[0].Length).Should().Be((0, 3));
        (firstTwo[1].Index, firstTwo[1].Index + firstTwo[1].Length).Should().Be((4, 9));
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_template_substitution_answers_a_subject_of_the_same_length()
    {
        // regex 2026.9.10: len(sub(r'(\w+) (\w+)', r'\2 \1', LONG)) -> 1048631, which is
        // len(LONG): swapping adjacent words moves characters without adding or losing any.
        //
        // The template is upstream's `\2 \1`, not .NET's `$2 $1`: Replace takes upstream's syntax
        // and ReplaceFormat takes the .NET one, so `$2 $1` here is a five-character literal and the
        // answer comes back 405,146 characters short. Measured on this port, 2026-09-16.
        new FuzzyRegex(@"(\w+) (\w+)")
            .Replace(_long, @"\2 \1")
            .Length.Should()
            .Be(1048631);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_megabyte_split_answers_the_same_pieces_upstream_does()
    {
        // regex 2026.9.10: split(r'\w+', LONG) -> 214494 pieces, first three ['', ' ', ' '],
        // last '.'.
        string?[] pieces = new FuzzyRegex(@"\w+").Split(_long);

        pieces.Should().HaveCount(214494);
        pieces.Take(3).Should().Equal("", " ", " ");
        pieces[^1].Should().Be(".");
    }
}
