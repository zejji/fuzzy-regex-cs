using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// What S19's repeat opcodes do that upstream's own suite does not pin: the UTF-16 translation of a
/// repeat count, the guard machinery's effect on a pattern that would otherwise re-enter for ever,
/// and the timeout actually firing on one that the guards cannot tame.
/// </summary>
/// <remarks>
/// Every expected value below is upstream's, measured against regex 2026.7.19 on 2026-08-31 and
/// quoted beside the assertion.
/// </remarks>
public sealed class RepeatTests
{
    /// <summary>
    /// The pattern the repeat guards do <b>not</b> tame, in this port or upstream. Measured against
    /// upstream 2026-08-31, searching <c>'a' * n + 'xb'</c>: n=8 0.13ms, n=14 7.4ms, n=20 442.6ms,
    /// n=26 23,318.9ms. That is a factor of about 8 per six characters, and 23 seconds for a
    /// twenty-eight character subject. This port measures 0.81ms, 39.4ms and 534.6ms at n=8, 14 and
    /// 20 - the same curve, a small constant factor slower.
    /// </summary>
    /// <remarks>
    /// The subject deliberately contains the <c>'b'</c> the pattern requires. Without it upstream
    /// answers instantly at any length, because <c>locate_required_string</c> rejects the subject
    /// before the engine runs - which is a Phase 7 prefilter this port has not got, so a
    /// <c>'b'</c>-free subject would be testing the deferral rather than the engine, and the test
    /// would silently stop timing out the day Phase 7 lands.
    /// </remarks>
    private const string _catastrophicPattern = "(a|a)*b";

    [Test]
    public void A_catastrophic_pattern_raises_the_match_timeout_rather_than_running_to_completion()
    {
        // The first real exercise of the S16 timeout plumbing: until S19 there was no pattern that
        // could run long enough to trip it (MatchSpineTests says so). Thirty-two characters is about
        // 8^2 times the 23 seconds upstream took for twenty-six, so a 200ms budget is not a race.
        var pattern = new FuzzyRegex(_catastrophicPattern, FuzzyRegexOptions.None, TimeSpan.FromMilliseconds(200));

        Action match = () => pattern.Match(new string('a', 32) + "xb");

        match.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();
    }

    [Test]
    [Arguments("(a*)*b", "b", true)]
    [Arguments("(a?)*b", "b", true)]
    [Arguments("(?:a?a?)*b", "b", true)]
    [Arguments("((a)*)*b", "b", true)]
    [Arguments("^(a+)+$", "!", false)]
    [Arguments("(a+)+b", "b", false)]
    [Arguments("(ab|cd)*e", "e", true)]
    public void The_repeat_guards_make_a_pattern_that_would_re_enter_for_ever_terminate(
        string pattern,
        string required,
        bool expected
    )
    {
        // The whole point of the guard machinery. Each of these has a body that can match empty or
        // can split a run of 'a's many ways, so without a position guard the repeat would try every
        // split; with one, a position already tried is refused.
        //
        // Every subject holds the character the pattern requires, so upstream's Phase 7
        // required-string prefilter cannot answer without running its engine either - which is what
        // makes this a comparison of the two engines. Upstream, measured 2026-08-31 on
        // 'a' * 26 + 'x' + required: 0.10ms, 0.07ms, 0.09ms, 0.50ms, 0.02ms, 0.13ms and 0.00ms
        // respectively, and this port 1.68ms, 1.10ms, 1.24ms, 3.81ms, 0.21ms, 1.88ms and 0.05ms -
        // both flat in n, which is the property under test.
        //
        // No timeout is set, so a regression here does not fail, it hangs, and the suite's own
        // runtime is the signal. That is deliberate: a timeout would turn "the guards stopped
        // working" into the same exception the test above asserts for a quite different reason.
        //
        // '(a|a)*b' is NOT in this list. It is exponential upstream too - 23 seconds at n=26 - so it
        // belongs to the timeout test above, not here.
        FuzzyRegex.Match(new string('a', 26) + "x" + required, pattern).Success.Should().Be(expected);
    }

    [Test]
    public void An_empty_body_repeat_captures_the_empty_iteration_at_the_end_of_the_run()
    {
        // The guards are what terminates this, and what the *last* capture is is the observable
        // consequence of where they stopped. Upstream, measured:
        //   regex.search('(a*)*', 'aaa').span(1)   == (3, 3)
        //   regex.search('(a*)*', 'aaa').spans(1)  == [(0, 3), (3, 3)]
        Match m = FuzzyRegex.Match("aaa", "(a*)*");

        (m.Index, m.Length).Should().Be((0, 3));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((3, 0));
        m.Groups[1]
            .Captures.Select(static c => (c.Index, c.Length))
            .Should()
            .Equal([(0, 3), (3, 0)], "the empty final iteration is a capture of its own");
    }

    [Test]
    public void A_repeat_count_is_a_count_of_codepoints_and_a_span_is_in_utf16_code_units()
    {
        // This is the whole reason S19 needed 'StepBy' and 'CountBetween': every repeat count in
        // upstream is a character count, and upstream can multiply it by a step because it indexes
        // the subject by codepoint. This port indexes by UTF-16 code unit, so a repeat of an astral
        // character has a count of n and a span of 2n.
        //
        // U+1F600 GRINNING FACE is one codepoint and two UTF-16 code units. Upstream, measured:
        //   regex.match('.{3}', '\U0001F600' * 4).span()  == (0, 3) in codepoints
        //   regex.match('.{5}', '\U0001F600' * 4)         is None
        const string astral = "\U0001F600\U0001F600\U0001F600\U0001F600";
        astral.Should().HaveLength(8, "four astral characters occupy eight UTF-16 code units");

        Match three = FuzzyRegex.MatchAtStart(astral, ".{3}");
        (three.Index, three.Length).Should().Be((0, 6), "three codepoints are six code units");

        FuzzyRegex.MatchAtStart(astral, ".{5}").Success.Should().BeFalse("there are only four characters");
        FuzzyRegex.MatchAtStart(astral, ".{4}").Length.Should().Be(8);

        // The greedy REPEAT_ONE retreat path, which is where 'CountBetween' is read: the repeat has
        // to give characters back one codepoint at a time until the tail can match. Upstream:
        //   regex.match('.+\U0001F600', '\U0001F600' * 4).span() == (0, 4) in codepoints
        Match retreated = FuzzyRegex.MatchAtStart(astral, ".+\U0001F600");
        (retreated.Index, retreated.Length).Should().Be((0, 8));

        // And the lazy one, which advances instead of retreating. Upstream:
        //   regex.match('.+?\U0001F600', '\U0001F600' * 4).span() == (0, 2) in codepoints
        Match lazy = FuzzyRegex.MatchAtStart(astral, ".+?\U0001F600");
        (lazy.Index, lazy.Length).Should().Be((0, 4), "two codepoints are four code units");

        // A counted repeat of an astral literal, so the count is compared against a subject whose
        // code-unit length is twice its character length. Upstream:
        //   regex.search('\U0001F600{2,3}', '\U0001F600' * 4).span() == (0, 3) in codepoints
        Match counted = FuzzyRegex.Match(astral, "\U0001F600{2,3}");
        (counted.Index, counted.Length).Should().Be((0, 6));
    }

    [Test]
    public void A_repeat_counts_a_lone_surrogate_as_one_character_and_a_pair_as_one_character()
    {
        // The boundary of 'MatchState.OneUnitPerCharacter', which decides whether the repeat arms
        // convert a count to a position by arithmetic or by walking. The flag is off for any subject
        // holding a high surrogate, so this subject takes the walk - and it holds both a *lone* high
        // surrogate, which is one character and one code unit, and a well-formed pair, which is one
        // character and two. A predicate that looked for pairs rather than for high surrogates would
        // still be right here; one that assumed every high surrogate starts a pair would not.
        //
        // Python holds lone surrogates in a str, so upstream answers this directly. Measured against
        // regex 2026.7.19 on 2026-09-01, subject '\ud800' 'ab' '\U0001F600' 'cd' - six codepoints,
        // seven UTF-16 code units:
        //   regex.match('.{3}', s).span()  == (0, 3) in codepoints
        //   regex.match('.+?c', s).span()  == (0, 5) in codepoints
        //   regex.match('.+d',  s).span()  == (0, 6) in codepoints
        const string mixed = "\uD800ab\U0001F600cd";
        mixed.Should().HaveLength(7, "six characters, one of which occupies two UTF-16 code units");

        Match three = FuzzyRegex.MatchAtStart(mixed, ".{3}");
        (three.Index, three.Length).Should().Be((0, 3), "the lone surrogate is one character of one unit");

        Match lazyAdvance = FuzzyRegex.MatchAtStart(mixed, ".+?c");
        (lazyAdvance.Index, lazyAdvance.Length).Should().Be((0, 6), "five characters, one of them a pair");

        Match greedyRetreat = FuzzyRegex.MatchAtStart(mixed, ".+d");
        (greedyRetreat.Index, greedyRetreat.Length).Should().Be((0, 7));
    }

    [Test]
    public void A_bounded_repeat_compares_its_count_inclusively_at_both_ends()
    {
        // The boundaries a '{m,n}' port gets wrong by one. Upstream, measured on 'a' * n:
        //   regex.fullmatch('a{2,4}', 'a')     is None
        //   regex.fullmatch('a{2,4}', 'aa')    spans (0, 2)
        //   regex.fullmatch('a{2,4}', 'aaaa')  spans (0, 4)
        //   regex.fullmatch('a{2,4}', 'aaaaa') is None
        FuzzyRegex.FullMatch("a", "a{2,4}").Success.Should().BeFalse();
        FuzzyRegex.FullMatch("aa", "a{2,4}").Length.Should().Be(2);
        FuzzyRegex.FullMatch("aaa", "a{2,4}").Length.Should().Be(3);
        FuzzyRegex.FullMatch("aaaa", "a{2,4}").Length.Should().Be(4);
        FuzzyRegex.FullMatch("aaaaa", "a{2,4}").Success.Should().BeFalse();

        // The same for a repeat that is not a single character, so it runs through
        // GREEDY_REPEAT / END_GREEDY_REPEAT rather than GREEDY_REPEAT_ONE. Upstream:
        //   regex.fullmatch('(?:ab){2,3}', 'ab')       is None
        //   regex.fullmatch('(?:ab){2,3}', 'ababab')   spans (0, 6)
        //   regex.fullmatch('(?:ab){2,3}', 'abababab') is None
        FuzzyRegex.FullMatch("ab", "(?:ab){2,3}").Success.Should().BeFalse();
        FuzzyRegex.FullMatch("abab", "(?:ab){2,3}").Length.Should().Be(4);
        FuzzyRegex.FullMatch("ababab", "(?:ab){2,3}").Length.Should().Be(6);
        FuzzyRegex.FullMatch("abababab", "(?:ab){2,3}").Success.Should().BeFalse();

        // '{0}' matches nothing and leaves the group unset, and '{0,}' is '*'. Upstream:
        //   regex.match('(a){0}', 'aaa').span()  == (0, 0), and .span(1) == (-1, -1)
        Match none = FuzzyRegex.MatchAtStart("aaa", "(a){0}");
        (none.Index, none.Length).Should().Be((0, 0));
        none.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void A_lazy_repeat_stops_at_the_minimum_and_a_greedy_one_takes_the_most_it_can()
    {
        // The two halves of the same pattern, which is the cheapest check that LAZY_REPEAT_ONE and
        // GREEDY_REPEAT_ONE have not been swapped. Upstream, measured:
        //   regex.match('a+', 'aaa').span()   == (0, 3)
        //   regex.match('a+?', 'aaa').span()  == (0, 1)
        //   regex.search('<.+>', '<a><b>').group()  == '<a><b>'
        //   regex.search('<.+?>', '<a><b>').group() == '<a>'
        FuzzyRegex.MatchAtStart("aaa", "a+").Length.Should().Be(3);
        FuzzyRegex.MatchAtStart("aaa", "a+?").Length.Should().Be(1);
        FuzzyRegex.Match("<a><b>", "<.+>").Value.Should().Be("<a><b>");
        FuzzyRegex.Match("<a><b>", "<.+?>").Value.Should().Be("<a>");

        // A lazy repeat must not advance past its maximum. Upstream:
        //   regex.match('a{2,3}?b', 'aaaab') is None
        //   regex.match('a{2,3}?b', 'aaab').span() == (0, 4)
        FuzzyRegex.MatchAtStart("aaaab", "a{2,3}?b").Success.Should().BeFalse();
        FuzzyRegex.MatchAtStart("aaab", "a{2,3}?b").Length.Should().Be(4);
    }

    [Test]
    public void A_lazy_repeat_over_a_long_subject_costs_time_proportional_to_its_length()
    {
        // The complexity guard for LAZY_REPEAT_ONE and GREEDY_REPEAT_ONE. Both backtrack arms
        // convert between a character count and a UTF-16 position, and both are re-entered once per
        // repeat position, so a conversion that walks the subject makes the whole scan quadratic.
        // Measured before the fix, in Release, on '.*?cd' over 'abc' * n + 'de': 6,002 characters
        // 0.213s, 12,002 0.273s, 24,002 1.265s, 48,002 4.185s - and the walk counters were exactly
        // n^2 + n^2/2 in each direction, which is the shape this test exists to refuse.
        //
        // The subject is four times upstream's test_bug_418626#3, so a quadratic scan costs sixteen
        // times what it costs there and a linear one four times. The engine's own timeout is what
        // enforces the ceiling, so a regression fails in twenty seconds instead of hanging the suite
        // for an hour.
        string subject = string.Concat(Enumerable.Repeat("abc", 80000)) + "de";
        var pattern = new FuzzyRegex(".*?cd", FuzzyRegexOptions.None, TimeSpan.FromSeconds(20));

        Match m = pattern.MatchAtStart(subject);

        (m.Index + m.Length).Should().Be(240001);
    }

    [Test]
    public void A_lazy_repeat_over_a_long_astral_subject_costs_time_proportional_to_its_length()
    {
        // The same guard for a subject whose characters do not each occupy one UTF-16 code unit, so
        // the arithmetic fast path cannot apply and 'CharacterIndex' is what has to keep it linear.
        // Measured on 'x' U+1F600 'y' repeated, before the index existed: 48,002 characters cost
        // 6,912,240,003 single-character steps, exactly 3n^2, and 6.25 seconds. With the index the
        // same match costs 2,328,005 steps and 0.034 seconds, and the step count doubles rather than
        // quadruples with the subject.
        string subject = string.Concat(Enumerable.Repeat("x\U0001F600y", 60000)) + "cd";
        subject.Should().HaveLength(240002, "180,002 characters, of which 60,000 take two code units");

        var pattern = new FuzzyRegex(".*?cd", FuzzyRegexOptions.None, TimeSpan.FromSeconds(20));

        Match m = pattern.MatchAtStart(subject);

        (m.Index + m.Length).Should().Be(240002);
    }
}
