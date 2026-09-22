using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S60b's <c>search_start</c>: the start-position prefilter the matcher consults
/// before every attempt, and the thirty-odd <c>search_start_*</c> helpers it dispatches to.
/// </summary>
/// <remarks>
/// <para>
/// The prefilter is meant to be invisible. It may only skip a start position at which the matcher
/// would certainly have failed, so every answer below is the answer the engine gave before the
/// prefilter existed, and the whole ported suite is the real test of that. What these tests add is
/// coverage of the dispatch itself: one pattern per arm of the switch, chosen so that the arm's own
/// scan does the skipping rather than the matcher. Without them an arm could sweep the wrong way,
/// or off the end, and no other test would reach it.
/// </para>
/// <para>
/// Every expected value was recorded from <c>regex</c> 2026.9.10 on 2026-09-22 by
/// <c>.scratch/s60b-arms.py</c>, and is quoted beside the assertion as Python spells it.
/// </para>
/// </remarks>
public sealed class SearchStartTests
{
    [Test]
    public void The_forward_character_sweep_skips_to_the_first_position_the_test_node_accepts()
    {
        // The start test is CHARACTER 'q', so 'search_start_CHARACTER' sweeps to each 'q' in turn and
        // the matcher is only ever asked about those. Three of them fail before the fourth matches.
        //
        // regex.findall(r'q[abc]d', 'zzqzqaqbd') -> [(6, 9, 'qbd')]
        MatchCollection matches = new FuzzyRegex("q[abc]d").Matches("zzqzqaqbd");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((6, 3, "qbd"));
    }

    [Test]
    public void A_dot_that_is_not_dotall_still_skips_the_line_break()
    {
        // '.' compiles to ANY, which has a sweep of its own: the newline is the one position it
        // refuses, and the prefilter has to refuse it too.
        //
        // regex.findall(r'.\?', 'abc?d') -> [(2, 4, 'c?')]
        MatchCollection matches = new FuzzyRegex(@".\?").Matches("abc?d");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((2, 2, "c?"));
    }

    [Test]
    public void A_dotall_dot_skips_nothing_at_all()
    {
        // ANY_ALL is the one arm that does nothing: every position matches '(?s).', so there is no
        // position to skip to and the scan would only waste the walk.
        //
        // regex.findall(r'(?s).x', 'ab\nx') -> [(2, 4, '\nx')]
        MatchCollection matches = new FuzzyRegex("(?s).x").Matches("ab\nx");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((2, 2, "\nx"));
    }

    [Test]
    public void The_reverse_character_sweep_walks_towards_the_start_of_the_slice()
    {
        // A reversed pattern gives a reversed start test, and the sweep runs the other way. Getting
        // the direction wrong here loses every match rather than one, which is why it is worth a
        // test of its own: the arm shares no code with the forward sweep except 'MatchesMany'.
        //
        // [(m.start(), m.end(), m.group()) for m in regex.finditer(r'(?r)[abc]q', 'zaqzbq')]
        //   -> [(4, 6, 'bq'), (1, 3, 'aq')]
        MatchCollection matches = new FuzzyRegex("(?r)[abc]q").Matches("zaqzbq");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((4, 2, "bq"), (1, 2, "aq"));
    }

    [Test]
    public void A_word_boundary_start_test_is_scanned_rather_than_matched()
    {
        // The zero-width arms have no sweep to reuse, so this port scans them with the matcher's own
        // 'TryMatchZeroWidth' instead of transliterating twenty-four one-line helpers. 'concat' holds
        // 'cat' at a position where '\b' does not, and that is the position the scan has to skip.
        //
        // regex.findall(r'\bcat', 'concat the cat') -> [(11, 14, 'cat')]
        MatchCollection matches = new FuzzyRegex(@"\bcat").Matches("concat the cat");

        matches.Select(static m => (m.Index, m.Length)).Should().Equal((11, 3));
    }

    [Test]
    public void The_negated_boundary_scan_keeps_the_positions_the_boundary_scan_drops()
    {
        // The same subject the other way round, so a scan that answered '\B' with '\b''s rule would
        // pass the test above and fail this one.
        //
        // regex.findall(r'\Bcat', 'the cat concat') -> [(11, 14, 'cat')]
        MatchCollection matches = new FuzzyRegex(@"\Bcat").Matches("the cat concat");

        matches.Select(static m => (m.Index, m.Length)).Should().Equal((11, 3));
    }

    [Test]
    public void A_line_start_and_a_line_end_are_scanned_from_the_position_they_hold_at()
    {
        // START_OF_LINE and END_OF_LINE under MULTILINE. Upstream reaches these by a jump rather than
        // a scan; the answer is the same and the walk is what differs.
        //
        // regex.findall(r'(?m)^cat', 'a cat\ncat') -> [(6, 9, 'cat')]
        // regex.findall(r'(?m)cat$', 'cats\ncat') -> [(5, 8, 'cat')]
        new FuzzyRegex("(?m)^cat")
            .Matches("a cat\ncat")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((6, 3));

        new FuzzyRegex("(?m)cat$").Matches("cats\ncat").Select(static m => (m.Index, m.Length)).Should().Equal((5, 3));
    }

    [Test]
    public void An_end_of_string_start_test_is_one_of_the_three_upstream_jumps_to()
    {
        // END_OF_STRING is one of the three opcodes upstream answers by assigning a position outright
        // rather than scanning for it. This port scans, which reaches the same position; the test is
        // here so that a later slice which does swap in the jump has something to check it against.
        //
        // regex.findall(r'cat\Z', 'cat cat') -> [(4, 7, 'cat')]
        MatchCollection matches = new FuzzyRegex(@"cat\Z").Matches("cat cat");

        matches.Select(static m => (m.Index, m.Length)).Should().Equal((4, 3));
    }

    [Test]
    public void The_word_start_and_word_end_scans_are_not_the_boundary_scan()
    {
        // START_OF_WORD and END_OF_WORD, which upstream spells '\m' and '\M'. They are separate
        // opcodes from BOUNDARY and each has its own helper.
        //
        // regex.findall(r'\mcat', 'concat cat') -> [(7, 10, 'cat')]
        // regex.findall(r'cat\M', 'cats cat') -> [(5, 8, 'cat')]
        new FuzzyRegex(@"\mcat")
            .Matches("concat cat")
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal((7, 3));

        new FuzzyRegex(@"cat\M").Matches("cats cat").Select(static m => (m.Index, m.Length)).Should().Equal((5, 3));
    }

    [Test]
    public void A_search_anchor_start_test_pins_the_search_to_where_it_began()
    {
        // '\G' holds only at the position the search started from, so the arm assigns that position
        // instead of scanning: sweeping for it would find the same one place and cost a walk.
        //
        // regex.compile(r'\Gcat').search('xcat') -> None
        // regex.compile(r'\Gcat').search('xcat', pos=1) -> <span=(1, 4), match='cat'>
        FuzzyRegex pattern = new(@"\Gcat");

        pattern.Match("xcat").Success.Should().BeFalse();

        Match anchored = pattern.Match("xcat", beginning: 1);

        (anchored.Index, anchored.Length).Should().Be((1, 3));
    }

    [Test]
    public void A_pattern_that_starts_with_a_string_makes_that_string_its_required_string()
    {
        // The STRING arm of the prefilter is unreachable, and this is the invariant that makes it
        // so: the required string is the first item of the sequence that yields one, so a pattern
        // whose start test is a STRING has that same string as its required string, and upstream's
        // own required-string clause then withholds the prefilter. Upstream computes the same value - measured on 2026-09-22
        // against regex 2026.9.10 by wrapping '_get_required_string', which answers
        // 'req_offset=0, req_chars=(99, 97, 116)' for every pattern below.
        //
        // The arm is ported anyway, because upstream has it and a later slice may reach it. It is
        // exercised by switching that clause off, which is a negative control rather than a test;
        // 'docs/plan/slices/notes/S60b-sittings.md' records it, and it is what found the arm
        // stepping one character where a STRING node steps its whole length.
        string[] sources = ["cat.*dog", "(cat)dog", "catz?dogdogdog", "cat[0-9]*", "cat(?:x|y)dog"];

        using (new AssertionScope())
        {
            foreach (string source in sources)
            {
                PatternObject pattern = Build(source);

                pattern.StartTest!.Op.Should().Be(Opcode.String, source);
                pattern.ReqString!.Values.Should().Equal(pattern.StartTest.Values, source);
            }
        }
    }

    [Test]
    public void A_string_prefixed_pattern_still_finds_every_match()
    {
        // The answer the test above leaves the engine free to give, pinned so that a slice which
        // does reach the STRING arm has something to check itself against.
        //
        // [(m.start(), m.end(), m.group()) for m in regex.finditer(r'cat[0-9]*', 'a cat cat7')]
        //   -> [(2, 5, 'cat'), (6, 10, 'cat7')]
        MatchCollection matches = new FuzzyRegex("cat[0-9]*").Matches("a cat cat7");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((2, 3, "cat"), (6, 4, "cat7"));
    }

    private static PatternObject Build(string source) =>
        PatternObject.Compile(
            PatternCompiler.Compile(
                source,
                0,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                PatternCompiler.DefaultVersion
            ),
            source
        );

    [Test]
    public void A_fuzzy_start_test_is_not_screened_at_all()
    {
        // A test node allowed to make errors may match where its characters are not, so screening on
        // it would skip a position the matcher goes on to accept. Upstream stops calling the
        // prefilter from here on; this port stops for the rest of the matching operation, because a
        // compiled pattern is shared between threads and is frozen after compilation.
        //
        // regex.findall(r'(?:cat){e<=1}dog', 'xcatdog') -> [(1, 7, 'catdog')]
        MatchCollection matches = new FuzzyRegex("(?:cat){e<=1}dog").Matches("xcatdog");

        matches.Select(static m => (m.Index, m.Length, m.Value)).Should().Equal((1, 6, "catdog"));
    }

    [Test]
    public void The_prefilter_is_withheld_from_a_pattern_that_can_move_the_slice()
    {
        // NARROWING 3. '(*SKIP)' moves the slice under the matcher, so the prefilter is withheld
        // from any pattern that holds one. This test does not discriminate: the answer is the same
        // with the narrowing off, and so is the whole suite and 4000 oracle rows. It is here as the
        // row a later sitting starts from when it lifts the narrowing, and what makes the narrowing
        // a judgement rather than a measurement is written up beside the clause in 'Matcher.cs'.
        //
        // regex.findall(r'\bqq(*SKIP)x', 'qqy qqx') -> [(4, 7, 'qqx')]
        MatchCollection matches = new FuzzyRegex(@"\bqq(*SKIP)x").Matches("qqy qqx");

        matches.Select(static m => (m.Index, m.Length)).Should().Equal((4, 3));
    }
}
