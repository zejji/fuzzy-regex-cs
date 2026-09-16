using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Substitution;

/// <summary>
/// The <c>beginning</c>/<c>length</c> pair S53b added to <see cref="FuzzyRegex.Replace(string, string, int, int, int, TimeSpan?, CancellationToken)"/>
/// and <see cref="FuzzyRegex.ReplaceFormat(string, string, int, int, int, TimeSpan?, CancellationToken)"/>, which is
/// upstream's <c>sub(pos, endpos)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value below is a line of <c>tools/probes/upstream-sub-with-a-slice.py</c>, run
/// against <c>regex</c> 2026.9.10 on 2026-09-16. The rule it establishes, in one sentence:
/// <b>matching happens only inside the slice, and the text outside it is copied through
/// unchanged.</b> The second half is the part a reader would not guess - a slice narrows what the
/// engine looks at, and it would be just as plausible for the result to be the replaced slice
/// alone. It is not: <c>pattern_subx</c> starts its join list at the whole subject's start and
/// runs its trailing segment to <c>str_info.length</c> rather than to the slice end
/// (<c>upstream/src/_regex.c:22092-22097</c>).
/// </para>
/// <para>
/// The oracle covers the same ground over drawn rows - <c>record-oracle.py</c> gained a
/// <c>pos</c>/<c>endpos</c> shape for <c>sub</c> and <c>subf</c> in S53b - so these are the pinned
/// edges rather than the whole of the evidence.
/// </para>
/// </remarks>
public sealed class ReplaceSliceTests
{
    [Test]
    public void The_slice_bounds_the_matching_and_the_text_outside_it_is_copied_through()
    {
        FuzzyRegex a = new("a");

        // sub('X', 'aaaaa')                  = 'XXXXX'
        a.Replace("aaaaa", "X").Should().Be("XXXXX");
        // sub('X', 'aaaaa', pos=1)           = 'aXXXX'
        a.Replace("aaaaa", "X", beginning: 1).Should().Be("aXXXX");
        // sub('X', 'aaaaa', endpos=3)        = 'XXXaa'
        a.Replace("aaaaa", "X", length: 3).Should().Be("XXXaa");
        // sub('X', 'aaaaa', pos=1, endpos=3) = 'aXXaa'
        a.Replace("aaaaa", "X", beginning: 1, length: 2).Should().Be("aXXaa");
    }

    [Test]
    public void The_replacement_count_is_of_matches_inside_the_slice()
    {
        // subn('X', 'aaaaa', pos=1, endpos=3) = ('aXXaa', 2)
        new FuzzyRegex("a")
            .Replace("aaaaa", "X", -1, out int replacements, beginning: 1, length: 2)
            .Should()
            .Be("aXXaa");

        replacements.Should().Be(2);
    }

    [Test]
    public void ReplaceFormat_takes_the_same_slice()
    {
        FuzzyRegex a = new("a");

        // subf('[{0}]', 'aaaaa', pos=1, endpos=3)  = 'a[a][a]aa'
        a.ReplaceFormat("aaaaa", "[{0}]", beginning: 1, length: 2).Should().Be("a[a][a]aa");

        // subfn('[{0}]', 'aaaaa', pos=1, endpos=3) = ('a[a][a]aa', 2)
        a.ReplaceFormat("aaaaa", "[{0}]", -1, out int replacements, beginning: 1, length: 2).Should().Be("a[a][a]aa");
        replacements.Should().Be(2);
    }

    [Test]
    public void An_evaluator_sees_only_the_matches_inside_the_slice()
    {
        // No upstream twin: upstream's callable form takes the same pos/endpos, and the rule under
        // test is that the LOOP is sliced, which the template rows above already pin. What this
        // adds is that the evaluator is not called for a match outside the slice.
        List<string> seen = [];

        new FuzzyRegex("a")
            .Replace(
                "aaaaa",
                match =>
                {
                    seen.Add($"{match.Index}");
                    return "X";
                },
                beginning: 1,
                length: 2
            )
            .Should()
            .Be("aXXaa");

        seen.Should().Equal("1", "2");
    }

    [Test]
    public void The_count_limit_and_the_slice_apply_together()
    {
        // sub('X', 'aaaaa', 1, pos=1) = 'aXaaa'
        new FuzzyRegex("a")
            .Replace("aaaaa", "X", count: 1, beginning: 1)
            .Should()
            .Be("aXaaa");
    }

    [Test]
    [Arguments(-2, -1, "aaaXX")] // sub('X', 'aaaaa', pos=-2)        = 'aaaXX'
    [Arguments(99, -1, "aaaaa")] // sub('X', 'aaaaa', pos=99)        = 'aaaaa'
    [Arguments(0, 99, "XXXXX")] // sub('X', 'aaaaa', endpos=99)      = 'XXXXX'
    [Arguments(3, -2, "aaaXX")] // NOT upstream's pos=3, endpos=1: see the comment below
    [Arguments(3, 0, "aaaaa")] // the empty slice, which is where upstream's pos>endpos lands
    public void Out_of_range_bounds_are_clamped_rather_than_rejected(int beginning, int length, string expected)
    {
        // A negative beginning counts back from the end of the subject, as upstream's pos does and
        // as Matches already did: one Limits helper serves every entry point taking the pair.
        //
        // Upstream's INVERTED slice - sub('X', 'aaaaa', pos=3, endpos=1), which clamps endpos up
        // to pos and so replaces nothing - cannot be spelled here at all, because this surface
        // takes a LENGTH rather than an endpos and any negative length means "the rest of the
        // subject". So (3, -2) is (3, rest) and gives 'aaaXX'; the empty slice a caller would
        // actually want is (3, 0), and that gives upstream's 'aaaaa'.
        new FuzzyRegex("a")
            .Replace("aaaaa", "X", beginning: beginning, length: length)
            .Should()
            .Be(expected);
    }

    [Test]
    public void The_slice_start_is_the_start_for_an_anchor_and_for_a_word_boundary()
    {
        // '^a'.sub('X', 'aaaaa', pos=1)  = 'aaaaa'
        new FuzzyRegex("^a")
            .Replace("aaaaa", "X", beginning: 1)
            .Should()
            .Be("aaaaa");

        // r'\ba'.sub('X', 'a aaa', pos=2) = 'a Xaa'
        new FuzzyRegex(@"\ba")
            .Replace("a aaa", "X", beginning: 2)
            .Should()
            .Be("a Xaa");
    }

    [Test]
    public void A_lookbehind_still_reads_the_text_before_the_slice()
    {
        // '(?<=a)b'.sub('X', 'abab', pos=1) = 'aXaX' - both b's are replaced, so the lookbehind at
        // position 1 saw the 'a' at position 0, which is outside the slice. Same rule as the
        // finditer note on Iteration.Next: pos moves slice_start, and text_start stays at 0.
        new FuzzyRegex("(?<=a)b")
            .Replace("abab", "X", beginning: 1)
            .Should()
            .Be("aXaX");
    }

    [Test]
    public void The_too_short_shortcut_is_measured_against_the_slice_so_a_bad_template_is_not_compiled()
    {
        FuzzyRegex xx = new("xx");

        // 'xx'.sub(r'\g<bad', 'xxxxx', pos=4) = 'xxxxx' - the slice is one character wide, the
        // pattern needs two, so pattern_subx returns before the template is compiled.
        xx.Replace("xxxxx", @"\g<bad", beginning: 4).Should().Be("xxxxx");

        // Without the slice the same call reaches the template compiler and is rejected:
        // "error: missing > at position 6".
        Action whole = () => xx.Replace("xxxxx", @"\g<bad");
        whole.Should().Throw<FuzzyRegexParseException>();
    }

    [Test]
    public void A_reversed_pattern_keeps_the_text_on_both_sides_of_the_slice()
    {
        // '(?r)a'.subn('X', 'aaaaa', pos=1, endpos=3) = ('aXXaa', 2). The leading 'a' and the
        // trailing 'aa' both survive, which is the join list starting at the whole subject's end
        // and finishing at its start.
        new FuzzyRegex("(?r)a")
            .Replace("aaaaa", "X", -1, out int replacements, beginning: 1, length: 2)
            .Should()
            .Be("aXXaa");

        replacements.Should().Be(2);
    }

    [Test]
    public void A_zero_width_pattern_inside_a_slice()
    {
        // 'x*'.sub('-', 'abxd', pos=1, endpos=3) = 'a-b--d'
        new FuzzyRegex("x*")
            .Replace("abxd", "-", beginning: 1, length: 2)
            .Should()
            .Be("a-b--d");

        // and the whole subject, for contrast: 'x*'.sub('-', 'abxd') = '-a-b--d-'
        new FuzzyRegex("x*")
            .Replace("abxd", "-")
            .Should()
            .Be("-a-b--d-");
    }

    [Test]
    public void The_slice_is_in_UTF16_code_units_where_upstreams_is_in_codepoints()
    {
        // '.'.subn('-', '\U0001F600ab', pos=1, endpos=2) = ('\U0001F600-b', 1). Upstream's pos and
        // endpos are codepoint indices, so its slice is the single 'a'; this surface indexes in
        // UTF-16 code units and the emoji is two of them, so the same slice is beginning 2,
        // length 1 (design spec section 4).
        new FuzzyRegex(".")
            .Replace("\U0001F600ab", "-", -1, out int replacements, beginning: 2, length: 1)
            .Should()
            .Be("\U0001F600-b");

        replacements.Should().Be(1);
    }

    [Test]
    public void The_static_overloads_take_the_slice_too()
    {
        FuzzyRegex.Replace("aaaaa", "a", "X", beginning: 1, length: 2).Should().Be("aXXaa");
        FuzzyRegex.ReplaceFormat("aaaaa", "a", "[{0}]", beginning: 1, length: 2).Should().Be("a[a][a]aa");
        FuzzyRegex.Replace("aaaaa", "a", static _ => "X", beginning: 1, length: 2).Should().Be("aXXaa");
    }
}
