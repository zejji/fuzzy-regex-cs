using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The behaviour of the S16 engine spine that upstream's own suite does not pin: the span
/// convention over UTF-16, the search loop's boundaries, and the one divergence the differential
/// oracle found while the slice was being written.
/// </summary>
/// <remarks>
/// Every expected value below is upstream's, recorded against regex 2026.7.19 on 2026-08-31 and
/// quoted beside the assertion. VERIFICATION.md rule 7: a divergence that lives only in a wave
/// disappears the next time the seed changes.
/// </remarks>
public sealed class MatchSpineTests
{
    [Test]
    public void A_leading_anchor_does_not_swallow_the_character_the_optimiser_hoisted_in_front_of_it()
    {
        // THE oracle finding of S16: 41 of 2400 rows, every one a pattern with a leading anchor.
        // 'optimise_pattern' hoists the first character test of such a pattern in front of the
        // anchor as a CHARACTER node carrying RE_ZEROWIDTH_OP and step 0, so '^a' is really
        // CHARACTER(step 0) - START_OF_STRING - CHARACTER(step 1). The port stepped that first node
        // by one anyway, which left START_OF_STRING testing position 1, so nothing with a leading
        // anchor could match. Upstream, measured: regex.match('^a', 'ab').span() == (0, 1).
        FuzzyRegex.MatchAtStart("ab", "^a").Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart("b", @"\Gb").Success.Should().BeTrue();
        FuzzyRegex.MatchAtStart("c", @"\Ac\Z").Success.Should().BeTrue();
        FuzzyRegex.Match("cd", "^c").Value.Should().Be("c");

        // The other half: the hoisted test must still be able to reject. regex.match('^a', 'ba')
        // is None.
        FuzzyRegex.MatchAtStart("ba", "^a").Success.Should().BeFalse();
    }

    [Test]
    public void An_astral_subject_is_stepped_one_codepoint_at_a_time_and_reported_in_utf16_units()
    {
        // U+1F600 GRINNING FACE is one Python codepoint and two UTF-16 code units. Upstream,
        // measured: regex.search('b', '\U0001F600ab').span() == (2, 3) *in codepoints*. The public
        // value here must be (Index 3, Length 1), because every index this port reports is a UTF-16
        // code unit (AGENTS.md). If the engine stepped by code unit rather than by codepoint it
        // would still find 'b', so the sharper half is the dot below.
        const string subject = "\U0001F600ab";
        subject.Should().HaveLength(4, "the astral character occupies two UTF-16 code units");

        Match b = FuzzyRegex.Match(subject, "b");
        (b.Index, b.Length, b.Value).Should().Be((3, 1, "b"));

        // '.' matches one *codepoint*, so it takes the whole surrogate pair and reports a length of
        // two. Upstream: regex.match('.', '\U0001F600').span() == (0, 1) in codepoints, and
        // m.group() is the single astral character - which is two units here.
        Match dot = FuzzyRegex.MatchAtStart(subject, ".");
        (dot.Index, dot.Length).Should().Be((0, 2), "one codepoint is two UTF-16 code units");
        dot.Value.Should().Be("\U0001F600");

        // And the search advance steps whole codepoints too: '.b' can only match from position 2,
        // never from the low surrogate at position 1. Upstream: regex.search('.b', that).span() is
        // (1, 3) in codepoints, i.e. 'ab'.
        Match dotB = FuzzyRegex.Match(subject, ".b");
        (dotB.Index, dotB.Length, dotB.Value).Should().Be((2, 2, "ab"));
    }

    [Test]
    public void The_search_tries_the_empty_position_at_the_end_of_the_subject()
    {
        // The last position a search tries is slice_end itself, not slice_end - 1: 'next_match_2'
        // fails only when text_pos > slice_end (upstream/src/_regex.c:11862). Upstream, measured:
        // regex.search('$', 'ab').span() == (2, 2) and regex.search('', 'ab').span() == (0, 0).
        Match end = FuzzyRegex.Match("ab", "$");
        (end.Index, end.Length).Should().Be((2, 0));

        Match empty = FuzzyRegex.Match("ab", "");
        (empty.Index, empty.Length).Should().Be((0, 0));

        // An empty subject still gets one attempt, at position 0.
        FuzzyRegex.Match("", "").Success.Should().BeTrue();
        FuzzyRegex.Match("", "$").Success.Should().BeTrue();
        FuzzyRegex.Match("", "a").Success.Should().BeFalse();
    }

    [Test]
    public void Pos_moves_the_slice_but_not_the_start_of_the_string()
    {
        // Upstream documents open start and closed end bounds: state_init_2 sets text_start to 0
        // whatever 'pos' is, but text_end to 'endpos' (upstream/src/_regex.c:18435-18439). So '^'
        // does not match at pos, and '$' does match at endpos. Measured:
        //   regex.compile('^b').search('ab', 1) is None
        //   regex.compile('a$').search('ab', 0, 1).span() == (0, 1)
        new FuzzyRegex("^b")
            .Match("ab", beginning: 1)
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("a$").Match("ab", beginning: 0, length: 1).Value.Should().Be("a");

        // \G, by contrast, is anchored to where this operation was asked to start.
        new FuzzyRegex(@"\Gb")
            .Match("ab", beginning: 1)
            .Value.Should()
            .Be("b");
    }

    [Test]
    public void Beginning_and_length_are_clamped_the_way_get_limits_clamps_pos_and_endpos()
    {
        // Upstream's get_limits (:21627): a negative index counts back from the end, then both ends
        // are clamped to the string and the end is raised to the start. Measured:
        //   regex.compile('b').search('ab', -1).span() == (1, 2)
        //   regex.compile('a').search('ab', 0, 99).span() == (0, 1)
        //   regex.compile('a').search('ab', 5) is None
        new FuzzyRegex("b")
            .Match("ab", beginning: -1)
            .Value.Should()
            .Be("b");
        new FuzzyRegex("a").Match("ab", beginning: 0, length: 99).Value.Should().Be("a");
        new FuzzyRegex("a").Match("ab", beginning: 5).Success.Should().BeFalse();
    }

    [Test]
    public void Length_means_the_same_thing_whichever_way_the_beginning_was_written()
    {
        // Upstream takes 'pos' and 'endpos' and clamps each independently; this port takes a
        // beginning and a length, so the two conventions only compose if the beginning is resolved
        // before the length is added to it. Adding the length to the raw -2 gave an endpos of 1,
        // which clamped back up to the start and searched an empty slice.
        // Raised by the S16 blind review; upstream, measured:
        //   regex.compile('d').search('abcde', 3, 5).span() == (3, 4)
        var d = new FuzzyRegex("d");

        d.Match("abcde", beginning: -2, length: 3).Value.Should().Be("d");
        d.Match("abcde", beginning: 3, length: 3).Value.Should().Be("d", "the same slice, written the other way");

        // And the length must still bite: (3, 4) contains 'd' but not 'e'.
        new FuzzyRegex("e")
            .Match("abcde", beginning: -2, length: 1)
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("e").Match("abcde", beginning: 3, length: 1).Success.Should().BeFalse();
    }

    [Test]
    public void Multiline_changes_which_anchor_opcode_the_pattern_compiles_to()
    {
        // Without MULTILINE, '^' is START_OF_STRING and '$' is END_OF_STRING_LINE; with it they are
        // START_OF_LINE and END_OF_LINE, which are four different opcodes and four different
        // predicates in the port. Measured:
        //   [m.span() for m in regex.finditer('^b', 'a\nb')] == []            (no MULTILINE)
        //   regex.search('^b', 'a\nb', flags=regex.M).span() == (2, 3)
        //   regex.search('a$', 'a\nb').span() is None; with M it is (0, 1)
        FuzzyRegex.Match("a\nb", "^b").Success.Should().BeFalse();
        FuzzyRegex.Match("a\nb", "^b", FuzzyRegexOptions.Multiline).Value.Should().Be("b");

        FuzzyRegex.Match("a\nb", "a$").Success.Should().BeFalse();
        FuzzyRegex.Match("a\nb", "a$", FuzzyRegexOptions.Multiline).Value.Should().Be("a");

        // '$' without MULTILINE still matches before a *trailing* newline, which is what
        // END_OF_STRING_LINE means. Measured: regex.search('a$', 'a\n').span() == (0, 1).
        FuzzyRegex.Match("a\n", "a$").Value.Should().Be("a");
    }

    [Test]
    public void An_infinite_match_timeout_lets_a_match_run_and_a_finite_one_is_carried_into_the_state()
    {
        // The timeout plumbing, which is all S16 can pin: a pathological pattern needs quantifiers
        // (S19), so there is nothing here that can actually run long enough to trip it. What is
        // testable is that neither setting changes the answer, and that the finite one does not
        // trip on work that finishes.
        new FuzzyRegex("abc", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout)
            .Match("xabcy")
            .Value.Should()
            .Be("abc");

        new FuzzyRegex("abc", FuzzyRegexOptions.None, TimeSpan.FromMinutes(1)).Match("xabcy").Value.Should().Be("abc");
    }

    [Test]
    public void An_unsuccessful_match_reports_the_shape_the_built_in_regex_reports()
    {
        // Upstream returns None; this port returns an unsuccessful Match, as Regex does. Group 0 is
        // still readable and still empty, because a caller that checks Success first must not be
        // the only caller that does not throw.
        Match none = FuzzyRegex.Match("zzz", "abc");

        none.Success.Should().BeFalse();
        (none.Index, none.Length, none.Value).Should().Be((0, 0, ""));
        none.Groups.Count.Should().Be(1);
        none.Groups[0].Success.Should().BeFalse();
    }

    [Test]
    public void Group_zero_is_readable_and_every_other_group_still_names_its_seam()
    {
        // What S16 promised: group 0, its span, and its single capture. Groups 1 upwards are S18,
        // and report that rather than reporting themselves as absent - a missing answer, not a
        // wrong one.
        Match m = FuzzyRegex.Match("xabcy", "abc");

        m.Groups.Count.Should().Be(1);
        m.Groups[0].Value.Should().Be("abc");
        m.Groups[0].Name.Should().Be("0");
        m.Groups[0].Captures.Count.Should().Be(1);
        (m.Groups[0].Captures[0].Index, m.Groups[0].Captures[0].Length).Should().Be((1, 3));

        Action pastTheEnd = () => _ = m.Groups[1];
        pastTheEnd.Should().Throw<ArgumentOutOfRangeException>("the pattern declares no group 1");

        Match grouped = FuzzyRegex.Match("xabcy", "abc");
        grouped.Groups[0].Should().BeSameAs(grouped, "group 0 is the whole match");
    }

    [Test]
    public void A_pattern_with_a_group_reports_the_group_it_captured()
    {
        // START_GROUP and END_GROUP landed in S18, so this is no longer a seam. Group 0 is still the
        // whole match and group 1 is the capture, each with its own one-element capture list.
        Match m = FuzzyRegex.Match("xab", "(a)b");

        m.Groups.Count.Should().Be(2);
        (m.Index, m.Length).Should().Be((1, 2));
        m.Groups[1].Success.Should().BeTrue();
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((1, 1));
        m.Groups[1].Name.Should().Be("1");
        m.Groups[1].Captures.Select(c => (c.Index, c.Length)).Should().Equal((1, 1));
        m.LastGroupNumber.Should().Be(1);
        m.LastGroupName.Should().BeNull("group 1 has no name");
    }

    [Test]
    public void The_value_span_points_into_the_subject_without_copying_it()
    {
        Match m = FuzzyRegex.Match("xabcy", "abc");

        m.ValueSpan.ToString().Should().Be("abc");
        m.ValueSpan.Length.Should().Be(m.Length);
        m.ToString().Should().Be("abc");
    }
}
