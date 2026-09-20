using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The required-string prefilter: <c>locate_required_string</c> (<c>upstream/src/_regex.c:11082</c>)
/// and the case-sensitive FORWARD <c>string_search</c> arm it calls (<c>:6596</c>), ported by S60.
/// <c>string_search_rev</c> and the folded arms are NOT ported - see
/// <c>Matcher.LocateRequiredString</c>'s <c>default:</c> arm.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the prefilter is.</b> When the parser finds a substring every match must contain, the
/// compiler builds it as a free-standing node (<c>PatternObject.ReqString</c>) and records how far
/// into the match it sits (<c>ReqOffset</c>). The locator finds that substring once, steps back by
/// the offset, and hands the search a start position instead of position zero. Everything the
/// engine then does is unchanged, which is why this is a speed change and not a behaviour one -
/// and why every test here asserts an ANSWER rather than a timing.
/// </para>
/// <para>
/// <b>These pins are permanent</b>, for the reason <see cref="OptimiserTrapsTests"/> gives: a
/// prefilter is a chance to answer a different question quickly. A later slice that turns one red
/// has changed an answer, and the fix is in the optimisation rather than in the test.
/// </para>
/// <para>
/// <b>Provenance.</b> Every expected value is upstream's, recorded against <c>regex 2026.9.10</c>
/// by running <c>tools/probes/upstream-required-string-prefilter.py</c> on 2026-09-20, and quoted
/// beside the assertion. The probe prints <c>search</c> AND <c>match</c> for every row, because
/// upstream's <c>search_start</c> prefilter screens start positions with predicates its own matcher
/// does not use (S22) and the two can therefore disagree. <b>S60 does not port
/// <c>search_start</c></b>, only the locator, so the rows below are ones where upstream's two
/// answers agree or where the difference is `match`'s anchoring rather than the prefilter.
/// </para>
/// <para>
/// <b>The verb rows are the exception, and they are the point of the fixture.</b> ROADMAP's owner
/// rule of 2026-09-12 says upstream's start optimisations answer WRONGLY on <c>(*SKIP)</c> patterns
/// - PCRE2 agrees with this port with its own optimiser on or off, and upstream's
/// <c>..(*SKIP)xx</c> retries below the position the verb committed past. So the verb rows assert
/// THIS PORT's answer, which is pinned permanently by
/// <see cref="BacktrackingVerbTests"/> and by <c>docs/DIVERGENCES.md</c>, and upstream's differing
/// answer is quoted beside it as the record rather than as the target. The prefilter's obligation
/// is to take its bounds from the live <c>MatchState</c>, exactly as the slow path does, so that a
/// position a verb moved past is never re-searched.
/// </para>
/// </remarks>
public sealed class RequiredStringPrefilterTests
{
    /// <summary>Formats the first match of a pattern as the probe prints its span.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <returns><c>(start,end)</c>, or <c>None</c> when there is no match.</returns>
    private static string Search(string pattern, string subject)
    {
        Match match = new FuzzyRegex(pattern).Match(subject);
        return match.Success ? $"({match.Index},{match.Index + match.Length})" : "None";
    }

    /// <summary>Formats every match of a pattern as the probe prints its spans.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <returns>Space-separated <c>(start,end)</c> pairs, or the empty string for no match.</returns>
    private static string Spans(string pattern, string subject) =>
        string.Join(
            ' ',
            new FuzzyRegex(pattern).Matches(subject).Select(static m => $"({m.Index},{m.Index + m.Length})")
        );

    // ---------------------------------------------------------------------------------------
    // Slice item 15: the first unit and the required unit are the same character.
    //
    // PCRE2's own study pass records the trap in 'pcre2_study.c': "Patterns such as /a*a/ don't
    // work if both the start unit and required unit are the same." The locator finds the required
    // 'a' and steps back by 'req_offset'; a pattern whose head can also match that same 'a' has a
    // match starting EARLIER than the step-back lands. Getting this wrong shortens the match
    // rather than losing it, which is why it needs its own assertion on the span and not just on
    // success.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_pattern_whose_first_unit_is_also_its_required_unit_still_matches_greedily()
    {
        // regex 2026.9.10: search('a*a', 'a')   -> (0,1)
        //                  search('a*a', 'aaa') -> (0,3)
        //                  search('a*a', 'baa') -> (1,3)
        Search("a*a", "a").Should().Be("(0,1)");
        Search("a*a", "aaa").Should().Be("(0,3)");
        Search("a*a", "baa").Should().Be("(1,3)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_required_string_reachable_from_inside_the_pattern_s_own_loop_starts_at_the_loop()
    {
        // The same trap with a distinguishing tail, so a short answer cannot pass by coincidence:
        // the required string is 'ab' and the loop 'a*' can consume the 'a' the locator lands on.
        //
        // regex 2026.9.10: search('a*ab', 'aab')  -> (0,3)
        //                  search('a*ab', 'xaab') -> (1,4)
        Search("a*ab", "aab").Should().Be("(0,3)");
        Search("a*ab", "xaab").Should().Be("(1,4)");
    }

    // ---------------------------------------------------------------------------------------
    // What the prefilter exists for. S19 measured it: upstream's apparent instant answer on
    // '(a|a)*b' against a subject holding no 'b' is 'locate_required_string' rejecting the subject
    // before the engine runs, not a faster engine. Upstream is exponential on it too (23.3 s at
    // n=26) the moment the prefilter cannot apply.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_subject_without_the_required_string_is_rejected_without_the_exponential_search()
    {
        // 22 'a's and no 'b'. Without the locator this is 2^22 backtracks; with it the subject is
        // refused before the first attempt. The assertion is the ANSWER, and it is the same answer
        // either way: with 'Matcher.cs' and 'PatternObject.cs' stashed back to 3f1bf91 this whole
        // fixture is still 19 of 19 green, at 16.5 s against 0.4 s (measured 2026-09-20). So no row
        // here goes red when the prefilter is deleted - that is what a transparent optimisation
        // means - and the only thing this length buys is that the difference is visible in the
        // duration. Speed is the benchmark's job; these rows exist to stop the speed changing the
        // answer.
        //
        // regex 2026.9.10: search('(a|a)*b', 'a'*22)  -> None
        //                  search('(a|a)*b', 'a'*8+'b') -> (0,9)
        Search("(a|a)*b", new string('a', 22)).Should().Be("None");
        Search("(a|a)*b", new string('a', 8) + "b").Should().Be("(0,9)");
    }

    // ---------------------------------------------------------------------------------------
    // Slice item 5, and the reason this fixture exists at all: a prefilter must take the slice
    // from the same state the slow path does.
    //
    // (*SKIP) and (*PRUNE) move MatchState.SliceStart mid-attempt. The locator caches where it
    // found the required string in ReqPos/ReqEnd and reuses it while the search position has not
    // passed it (upstream :11112). A cache that outlives a verb's commit, or a search whose limit
    // comes from the pattern rather than from the live state, re-offers a start position the verb
    // ruled out.
    //
    // THESE ARE THIS PORT'S ANSWERS, PINNED PERMANENTLY. Where upstream differs it is upstream's
    // own start optimisation retrying below the committed position - ROADMAP, owner rule
    // 2026-09-12 - and the fix for a red row here is in the prefilter, never in the test.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_skip_that_commits_past_a_position_is_not_re_searched_by_the_prefilter()
    {
        // regex 2026.9.10: search('(?:a(*SKIP)x|b)needle', 'aqbneedle') -> (2,9)
        //                  search('a(*SKIP)bc',            'aabc')      -> (1,4)
        //                  search('a(*PRUNE)bc',           'aabc')      -> (1,4)
        Search("(?:a(*SKIP)x|b)needle", "aqbneedle").Should().Be("(2,9)");
        Search("a(*SKIP)bc", "aabc").Should().Be("(1,4)");
        Search("a(*PRUNE)bc", "aabc").Should().Be("(1,4)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_skip_inside_a_scan_still_refuses_the_whole_subject()
    {
        // The scan consumes to the end of '\w+', the verb commits there, and the required string
        // lies BEHIND that position - so no start position survives.
        //
        // regex 2026.9.10: search('\\w+(*SKIP)needle', 'xx needle') -> None
        Search(@"\w+(*SKIP)needle", "xx needle").Should().Be("None");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_required_string_before_a_skip_is_re_located_for_the_next_attempt()
    {
        // The first 'needle' fails at 'x', the verb commits past it, and the locator must find the
        // SECOND 'needle' rather than re-offering the cached first one.
        //
        // regex 2026.9.10: search('needle(*SKIP)x', 'needleyneedlex') -> (7,14)
        Search("needle(*SKIP)x", "needleyneedlex").Should().Be("(7,14)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_failing_pattern_with_a_required_string_exhausts_every_position()
    {
        // (*FAIL) makes every attempt fail after the required string has matched, so the locator
        // is asked again at each position and has to terminate rather than re-offer the same one.
        //
        // regex 2026.9.10: search('needle(*FAIL)', 'a needle here') -> None
        Search("needle(*FAIL)", "a needle here").Should().Be("None");
    }

    // ---------------------------------------------------------------------------------------
    // req_offset: how far into the match the required string sits. A fixed-width head gives a
    // non-negative offset and the locator steps back by exactly it; a variable-width head gives
    // -1 and the locator may only bound the search, not place it.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_required_string_at_a_fixed_offset_places_the_start_by_stepping_back()
    {
        // regex 2026.9.10: search('..needle', 'xy needle no') -> (1,9)
        Search("..needle", "xy needle no").Should().Be("(1,9)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_required_string_behind_a_variable_head_only_bounds_the_search()
    {
        // 'a+' has no fixed width, so the offset is -1 and the match starts before the required
        // string by an amount only the engine can discover.
        //
        // regex 2026.9.10: search('a+needle', 'zzaaneedle') -> (2,10)
        Search("a+needle", "zzaaneedle").Should().Be("(2,10)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_subject_missing_the_required_string_answers_none()
    {
        // regex 2026.9.10: search('xyz+needle', 'nothing here at all') -> None
        Search("xyz+needle", "nothing here at all").Should().Be("None");
    }

    // ---------------------------------------------------------------------------------------
    // The reverse arm is NOT ported, and this row guards the hole rather than the code. S60 landed
    // 'Opcode.String' only; a reversed pattern falls through 'LocateRequiredString's default: arm
    // and searches every position, so this row passes today with no prefilter under it at all. It
    // is here because the arm that fills the hole is the one most likely to get the offset's
    // direction wrong, and this is the answer it must still give.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_search_locates_the_last_required_string()
    {
        // regex 2026.9.10: search('(?r)needle',   'needle and needle')   -> (11,17)
        //                  search('(?r)needle..', 'needle and needlexy') -> (11,19)
        Search("(?r)needle", "needle and needle").Should().Be("(11,17)");
        Search("(?r)needle..", "needle and needlexy").Should().Be("(11,19)");
    }

    // ---------------------------------------------------------------------------------------
    // Bounds. 'pos' and 'endpos' move SliceStart/SliceEnd, and the locator's limit has to come
    // from those rather than from the subject - the same rule as the verb slice, reached by an
    // ordinary API call instead of by a verb.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_prefilter_searches_the_slice_and_not_the_subject()
    {
        // regex 2026.9.10: search('needle', 'needle needle', pos=3)    -> (7,13)
        //                  search('needle', 'needle needle', endpos=5) -> None
        FuzzyRegex pattern = new("needle");

        Match bounded = pattern.Match("needle needle", beginning: 3);
        bounded.Success.Should().BeTrue();
        $"({bounded.Index},{bounded.Index + bounded.Length})".Should().Be("(7,13)");

        pattern.Match("needle needle", beginning: 0, length: 5).Success.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Partial matching. A truncated required string is the one case where string_search reports
    // a find it did NOT complete, and upstream refuses to cache that (:11125, "if (!is_partial)").
    // Caching it would let a later attempt resume from a position no complete string occupies.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_truncated_required_string_is_reported_partial_and_not_cached()
    {
        // regex 2026.9.10: search('needle',  'a nee',    partial=True) -> (2,5)  partial=True
        //                  search('needlex', 'a needle', partial=True) -> (2,8)  partial=True
        Match truncated = new FuzzyRegex("needle").Match("a nee", partial: true);
        truncated.Success.Should().BeTrue();
        truncated.PartialMatch.Should().BeTrue();
        $"({truncated.Index},{truncated.Index + truncated.Length})".Should().Be("(2,5)");

        Match complete = new FuzzyRegex("needlex").Match("a needle", partial: true);
        complete.Success.Should().BeTrue();
        complete.PartialMatch.Should().BeTrue();
        $"({complete.Index},{complete.Index + complete.Length})".Should().Be("(2,8)");
    }

    // ---------------------------------------------------------------------------------------
    // Shapes with no single required string, which must be left exactly as they were: the locator
    // returns the current position and the slow path runs.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_pattern_with_no_single_required_string_is_unaffected()
    {
        // regex 2026.9.10: search('needle|haystack', 'a haystack') -> (2,10)
        //                  search('(needle)',        'a needle')   -> (2,8)
        //                  search('x*needle',        '  needle')   -> (2,8)
        Search("needle|haystack", "a haystack").Should().Be("(2,10)");
        Search("(needle)", "a needle").Should().Be("(2,8)");
        Search("x*needle", "  needle").Should().Be("(2,8)");
    }

    // ---------------------------------------------------------------------------------------
    // A walk, not a single match: the locator's ReqPos cache lives across the whole scan, so a
    // repeated required string is where a stale cache shows up.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_walk_over_a_repeated_required_string_finds_every_occurrence()
    {
        // regex 2026.9.10: [m.span() for m in finditer('needle', 'needle x needle y needle')]
        //                  -> [(0,6), (9,15), (18,24)]
        Spans("needle", "needle x needle y needle").Should().Be("(0,6) (9,15) (18,24)");
    }

    // ---------------------------------------------------------------------------------------
    // The prefilter is a new way to spend time, so it is also a new way to ignore a budget.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_scan_whose_prefilter_sweeps_a_long_tail_is_still_cancellable()
    {
        // No upstream provenance to quote: upstream has no timeout and no token, so this is
        // entirely this port's surface (DIVERGENCES.md, "Exception mapping").
        //
        // WHAT THIS DOES NOT PIN, because S60 tried to and could not. The sweep in
        // 'Matcher.StringSearch' has no cancellation poll of its own, and a test that fires one
        // cannot be written: every attempt reaches the prefilter through 'basic_match's opening
        // check at ':5102', whose gate 'state.Iterations == 0' is open EVERY time, because
        // 'MatchState.InitMatch' zeroes 'Iterations' per attempt (MatchState.cs:749). A budget
        // already spent is caught there; a budget that expires during the sweep is a race with the
        // machine. The reasoning and the measurement that made the poll unnecessary are at the
        // sweep itself.
        //
        // WHAT IT DOES PIN is the guarantee that survives: a scan that spends its time inside the
        // prefilter rather than inside the matching loop still answers a cancelled token, on the
        // NEXT attempt at the latest. The tail holds no second 'needle', so this walk's second
        // MoveNext is a prefilter sweep and nothing else - which is exactly the shape that would
        // go quiet if a future change let the prefilter run ahead of that check.
        using var cancellation = new CancellationTokenSource();
        var pattern = new FuzzyRegex("needle");
        string subject = "needle" + new string('x', 200_000);

        using IEnumerator<Match> walk = pattern
            .EnumerateMatches(subject, cancellationToken: cancellation.Token)
            .GetEnumerator();

        walk.MoveNext().Should().BeTrue();
        walk.Current.Index.Should().Be(0);
        walk.Current.Length.Should().Be(6);

        cancellation.Cancel();
        Action act = () => walk.MoveNext();

        act.Should().Throw<OperationCanceledException>();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_prefilter_finds_a_needle_deep_inside_a_long_subject()
    {
        // Written for a sweep cut into 64 Ki chunks, where 65,534 put the needle across the first
        // boundary; the chunking is gone and the row stays, because any future sweep that works in
        // blocks - a Boyer-Moore skip table, a block-at-a-time SIMD scan - drops an occurrence
        // lying across a boundary in the same way, and nothing shorter than a subject this long
        // reaches that fault.
        //
        // regex 2026.9.10: search('needle', 'x'*65534 + 'needle' + 'x'*100).span() -> (65534, 65540)
        string subject = new string('x', 65_534) + "needle" + new string('x', 100);

        Search("needle", subject).Should().Be("(65534,65540)");
    }

    // ---------------------------------------------------------------------------------------
    // The two arithmetic traps: upstream counts characters where this port counts code units.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void An_astral_required_string_is_not_cut_short_by_the_offset_limit()
    {
        // Upstream bounds the anchored search at 'slice_start + req_offset + value_count', which
        // counts CHARACTERS. Transliterating that addition here bounds it in code units instead,
        // and an astral needle is two units per character, so the search window closes before the
        // needle it is looking for can fit and the subject is refused. Stepping the same number of
        // NextPos positions is the same bound expressed in the units this port indexes in.
        //
        // regex 2026.9.10: match(r'(\U0001F600.)\1', '\U0001F600a\U0001F600a').span() -> (0, 4)
        //   in codepoints, which is (0, 6) in UTF-16 code units, group 1 at (0, 3).
        Match m = FuzzyRegex.MatchAtStart("\U0001F600a\U0001F600a", "(\U0001F600.)\\1");

        (m.Index, m.Length).Should().Be((0, 6));

        // And the searching arm, where the needle itself is astral.
        // regex 2026.9.10: search('\U0001F600needle', 'ab\U0001F600needle').span() -> (2, 9)
        //   in codepoints, which is (2, 10) in UTF-16 code units.
        Search("\U0001F600needle", "ab\U0001F600needle").Should().Be("(2,10)");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_skip_verb_keeps_the_prefilter_from_choosing_where_the_first_attempt_starts()
    {
        // The pin that governs this whole slice lives in BacktrackingVerbTests; this row is here
        // so the reason is recorded beside the prefilter's own tests. Upstream's req_offset=3 puts
        // the first attempt at 3, the '(*SKIP)' steps 3 to 5, and position 4 - the one that
        // matches - is never tried. This port refuses to make that jump at all when a '(*SKIP)' is
        // in the pattern (PatternObject.HasSkipVerb).
        //
        // NOT upstream's answer, deliberately. regex 2026.9.10 answers None here; PCRE2 10.47
        // answers (4, 8) with its start optimiser on AND off, and so does this port. ROADMAP's
        // owner rule (2026-09-12) pins the port's answer, and BacktrackingVerbTests quotes the
        // PCRE2 manual on why upstream and Perl differ.
        Search("(?:..(*SKIP)x|q)x", "ab cd xx").Should().Be("(4,8)");

        // Refusing the subject outright stays on even with the verb present: a string every match
        // must contain is missing whatever order the attempts run in.
        // regex 2026.9.10: search(r'\w+(*SKIP)needle', 'xx needle') -> None
        Search(@"\w+(*SKIP)needle", "xx needle").Should().Be("None");
    }
}
