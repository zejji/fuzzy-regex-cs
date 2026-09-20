using System.Globalization;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The six rows of the 6000-row oracle gate where a reversed partial match over a non-zero slice
/// answers what upstream answers over the same text cut out as a subject of its own.
/// </summary>
/// <remarks>
/// <para>
/// These rows share their mechanism with <see cref="ReversedPartialSliceStartTests"/> and the
/// owner's ruling of 2026-09-15 (Option B of
/// <c>docs/plan/upstream-reports/ledger-24-briefing.md</c>): for a reversed match asked with
/// <c>partial=True</c>, the text has run out when the match reaches the slice start. What is new
/// here is that upstream ANSWERS these calls rather than refusing them, so they were invisible to
/// the earlier grid, which is built from cells where upstream returns <c>None</c>.
/// </para>
/// <para>
/// Upstream reaches its answer down a different road. Its node handlers compare against
/// <c>text_start</c>, which <c>init_match</c> fixes at 0 (<c>upstream/src/_regex.c:18442</c>), so
/// over a slice they never report running out at <c>pos</c>. Its search instead retreats until
/// <c>search_start</c> (<c>:8400-8405</c>) sets <c>new_position-&gt;text_pos = state-&gt;slice_start</c>
/// and returns <c>RE_ERROR_PARTIAL</c>, and <c>:18185-18190</c> then overwrites the match's text
/// position with <c>slice_start</c>. The span upstream hands back is therefore
/// <c>(slice_start, match_pos)</c> of whichever attempt was current when it gave up, which is a
/// later and longer attempt than the first one that ran out.
/// </para>
/// <para>
/// Each test below carries both of upstream's answers, measured on <c>regex</c> 2026.9.10 on
/// 2026-09-20. <b>UPSTREAM OVER THE SLICE</b> is what upstream returns for the call as the gate
/// asked it, and is the answer this port deliberately does not give.
/// <b>UPSTREAM OVER THE CUT SUBJECT</b> is what upstream returns for the same pattern over
/// <c>subject[pos:endpos]</c> as a subject in its own right, shifted back by <c>pos</c>, and is the
/// answer this port does give. The second is the ruling's own consequence: the argument for Option
/// B is that a slice start behaves like a string start, so the two calls have to agree.
/// </para>
/// <para>
/// The oracle pins the same six rows as <c>reversed-partial-answers-the-cut-subject</c>, whose
/// predicate compares this port's answer with the cut-subject answer the recorder stores on every
/// such row. Both halves are re-runnable:
/// <c>python tools/probes/s57b-cut-subject-door.py 7 4242 20260920</c>.
/// </para>
/// <para>
/// The rule does not reach every row. Where the pattern reads <c>\b</c>, <c>\B</c> or a lookbehind
/// across <c>pos</c>, the two calls differ because a boundary looks at the character before the
/// slice and a cut subject has none, and the ruling deliberately left that alone. Those rows are
/// the ones where upstream answers no match over the slice, and
/// <c>reversed-partial-runs-out-at-the-slice-start</c> covers them.
/// </para>
/// </remarks>
public sealed class ReversedPartialCutSubjectTests
{
    /// <summary>How the gate's report renders an answer, so the expectations read like the report.</summary>
    /// <param name="m">The match to render.</param>
    /// <returns><c>"no match"</c>, or every group as <c>n:(index,length)</c> plus the partial flag.</returns>
    private static string Describe(Match m)
    {
        if (!m.Success)
        {
            return "no match";
        }

        IEnumerable<string> groups = Enumerable
            .Range(0, m.Groups.Count)
            .Select(n =>
                m.Groups[n].Success
                    ? string.Create(CultureInfo.InvariantCulture, $"{n}:({m.Groups[n].Index},{m.Groups[n].Length})")
                    : string.Create(CultureInfo.InvariantCulture, $"{n}:unset")
            );

        return string.Join(" ", groups) + (m.PartialMatch ? " partial" : "");
    }

    [Test]
    public void A_reversed_partial_over_a_slice_consumes_the_character_the_slice_holds()
    {
        // Gate seed 7 row 102670, generator `partial-sliced`, flags 0x400a.
        // The slice holds one character, U+FB01. Upstream's search retreats to the slice start and
        // reports an empty partial there; this port consumes the character and runs out one step
        // later, which is what the same call over the one-character subject gives.
        // UPSTREAM OVER THE SLICE:       0:(2,0) 1:unset partial
        // UPSTREAM OVER THE CUT SUBJECT: 0:(2,1) 1:unset partial
        FuzzyRegex re = new(
            @"(?r)([a]){0,}(?:[a\d](*SKIP)[\p{L}\p{N}]|[[:digit:]])",
            FuzzyRegexOptions.RightToLeft | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        );

        Describe(re.Match("İİﬁ\nﬁ", 2, 1, partial: true)).Should().Be("0:(2,1) 1:unset partial");
    }

    [Test]
    public void A_reversed_fullmatch_that_fills_its_slice_is_still_a_partial()
    {
        // Gate seed 4242 row 102408, generator `partial-sliced`, flags 0x100.
        // Both sides find the same two-character match. They disagree only about whether it is
        // complete: the pattern's `{1,}` would take more text if the slice had any, so over the cut
        // subject upstream calls it partial too.
        // UPSTREAM OVER THE SLICE:       0:(2,2) 1:(3,1)            (complete)
        // UPSTREAM OVER THE CUT SUBJECT: 0:(2,2) 1:(3,1) partial
        FuzzyRegex re = new(
            @"(?r)[\p{L}||\p{N}]{1,}(?P<g1>[^a-f])(?:(?(1)(?!(?&g1))[^a-f]))$",
            FuzzyRegexOptions.Version1
        );

        Describe(re.FullMatch("ısSıﬁ", 2, 2, partial: true)).Should().Be("0:(2,2) 1:(3,1) partial");
    }

    [Test]
    public void A_reversed_partial_over_an_empty_slice_sets_no_group()
    {
        // Gate seed 4242 row 103412, generator `partial-sliced`, flags 0x400a.
        // The slice is empty, so the first attempt runs out immediately and nothing inside the
        // pattern gets to match. Upstream retreats far enough for `(?P<g3>\d*?)` to match empty and
        // reports group 3 set; over the empty cut subject it leaves group 3 unset, as this port does.
        // UPSTREAM OVER THE SLICE:       0:(2,0) 1:unset 2:unset 3:(2,0) partial
        // UPSTREAM OVER THE CUT SUBJECT: 0:(2,0) 1:unset 2:unset 3:unset partial
        FuzzyRegex re = new(
            @"(?r)^(?P<g1>.{1,})??([\w\s])(?P<g3>\d*?)(?:(?(3)(?<=(?&g3))[[:digit:]]|\d)){0,}$",
            FuzzyRegexOptions.RightToLeft | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        );

        Describe(re.MatchAtStart("𝔘𐐨", 2, 0, partial: true)).Should().Be("0:(2,0) 1:unset 2:unset 3:unset partial");
    }

    [Test]
    public void A_reversed_partial_search_stops_at_the_first_attempt_that_runs_out()
    {
        // Gate seed 4242 row 107083, generator `partial-sliced`, flags 0x400a.
        // The slice is "\naa". Upstream's search keeps retreating and ends up reporting the empty
        // span it was left holding; this port reports the one character its first attempt consumed.
        // UPSTREAM OVER THE SLICE:       0:(1,0) 1:unset 2:unset partial
        // UPSTREAM OVER THE CUT SUBJECT: 0:(1,1) 1:unset 2:unset partial
        FuzzyRegex re = new(
            // The literal U+10428 DESERET SMALL LETTER LONG I, as the recorded row holds it. A
            // `𐐨` in a verbatim string would be six characters of pattern text, not one.
            @"(?r)^𐐨(?P<g1>\p{Lu}+)\1(?P<g2>[A])(?:(?(2)(?<=(?&g2))[[:alpha:]]|[\w\s]))",
            FuzzyRegexOptions.RightToLeft | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        );

        Describe(re.Match("\r\naaAA𐐨", 1, 3, partial: true)).Should().Be("0:(1,1) 1:unset 2:unset partial");
    }

    [Test]
    public void A_reversed_partial_keeps_the_groups_its_first_attempt_set()
    {
        // Gate seed 20260920 row 107737, generator `partial-sliced`, flags 0x4002.
        // The slice is "bb". This port's first attempt consumes both characters through
        // `([^\d]{2,2}){3}` and runs out with group 3 holding them, which is what upstream reports
        // over "bb" as a subject. Upstream over the slice throws that attempt away.
        // UPSTREAM OVER THE SLICE:       0:(1,0) 1:unset 2:unset 3:unset partial
        // UPSTREAM OVER THE CUT SUBJECT: 0:(1,2) 1:unset 2:unset 3:(1,2) partial
        FuzzyRegex re = new(
            @"(?r)(?P<g1>[b])(?:(?(1)(?=(?&g1))[a\d]|\p{Nd})){3}(\D)([^\d]{2,2}){3}\b",
            FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase
        );

        Describe(re.Match(" bbbb", 1, 2, partial: true)).Should().Be("0:(1,2) 1:unset 2:unset 3:(1,2) partial");
    }

    [Test]
    public void A_reversed_partial_on_a_turkic_subject_is_still_about_the_slice_start()
    {
        // Gate seed 20260920 row 107758, generator `partial-sliced`, flags 0x10a.
        // The oracle pinned this row as `turkic-default-folding` until 2026-09-20, because the
        // subject and the pattern are full of dotless i and the divergence's spans cover one. They
        // do not disagree about folding. Swapping EVERY U+0131 in both pattern and subject for `h`,
        // and again for U+00E5 - neither of which has a `T` row in CaseFolding.txt - leaves both
        // answers exactly where they were, which is the isolating control
        // `turkic-default-folding`'s own notes ask for before a row is claimed.
        // UPSTREAM OVER THE SLICE:       0:(3,0) partial
        // UPSTREAM OVER THE CUT SUBJECT: 0:(3,1) partial
        FuzzyRegex re = new(
            // The literal U+0131 LATIN SMALL LETTER DOTLESS I, as the recorded row holds it.
            @"(?r)ı[\p{L}||\p{N}](?:[\p{ASCII}--\p{L}]+?(*PRUNE)[\w\s]|\S)",
            FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.IgnoreCase
        );

        Describe(re.Match("ıßİı\r\n", 3, 1, partial: true)).Should().Be("0:(3,1) partial");
    }
}
