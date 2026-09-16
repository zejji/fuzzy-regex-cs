using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Where a reversed match with <c>partial: true</c> runs out of text when the caller passed a
/// non-zero <c>beginning</c>: at the slice start, not at index 0 of the subject.
/// </summary>
/// <remarks>
/// <para>
/// These are the 33 cells of <c>tools/probes/port-reversed-partial-ignores-the-slice-start.ps1</c>
/// and its upstream twin <c>upstream-reversed-partial-ignores-the-slice-start.py</c>, written as
/// tests on the owner's ruling of 2026-09-15 (Option B of
/// <c>docs/plan/upstream-reports/ledger-24-briefing.md</c>): for a reversed match asked with
/// <c>partial=True</c>, the engine has run out of text when it reaches the slice start, and it
/// reports a partial there.
/// </para>
/// <para>
/// Upstream holds BOTH rules and picks one by optimisation path - its node handlers ask
/// <c>text_start</c> (always 0) and its optimiser and reversed string helpers ask
/// <c>slice_start</c> (<c>upstream/src/_regex.c:18435-18446</c>, <c>:8335-8405</c>) - so it
/// contradicts itself on this grid, and this port inherited both. Spec amendment 16 outcome (c):
/// fixed here, ledger entry 24, nothing filed until Phase 8.
/// </para>
/// <para>
/// Every expectation below carries its provenance. <b>UPSTREAM</b> means upstream's own Rule B path
/// already answers this cell that way and the answer was measured on <c>regex</c> 2026.9.10 on
/// 2026-09-16 by running the probe above. <b>RULING</b> means upstream answers <c>None</c> here
/// from its Rule A path and the expectation is the owner's ruling, which the DIVERGENCES.md row
/// "Reversed partial matches run out of text at the slice start" records.
/// </para>
/// <para>
/// Nothing else about <c>beginning</c> moves: <c>^</c> and <c>\A</c> still refuse a non-zero one,
/// and <c>\b</c> and lookbehind still see the character before it. The cells at the empty slice
/// <c>(0, 0)</c> of <c>"xyz"</c> and over the empty subject are the proof: <c>\b</c> there has no
/// word character on either side, so it genuinely fails and the answer is no match rather than a
/// partial.
/// </para>
/// </remarks>
public sealed class ReversedPartialSliceStartTests
{
    private const string _sharp = "ß"; // LATIN SMALL LETTER SHARP S
    private const string _fi = "ﬁ"; // LATIN SMALL LIGATURE FI
    private const string _dotlessI = "ı"; // LATIN SMALL LETTER DOTLESS I

    /// <summary>The two-character subject ledger 24's drawn row uses.</summary>
    private const string _drawnSubject = _fi + _dotlessI;

    /// <summary>
    /// Runs one grid cell the way the probes do: <c>MatchAtStart</c> over the slice
    /// <c>[pos, endpos)</c>, asking for a partial.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="pos">The slice start, upstream's <c>pos</c>.</param>
    /// <param name="endpos">The slice end, upstream's <c>endpos</c>.</param>
    /// <returns><c>"None"</c>, or the span and partial flag formatted as the probes print them.</returns>
    private static string Answer(string pattern, string subject, int pos, int endpos)
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.Version1).MatchAtStart(
            subject,
            pos,
            endpos - pos,
            partial: true
        );

        return m.Success ? $"({m.Index}, {m.Index + m.Length}) partial={m.PartialMatch}" : "None";
    }

    [Test]
    public void One_visible_character_answers_the_same_whether_it_is_the_subject_or_a_slice_of_one()
    {
        // The whole subject "a": all three shapes run out at index 0, which is both the subject
        // start and the slice start, so the two rules cannot be told apart here.
        // UPSTREAM: regex.compile('(?r)ya').match('a', 0, 1, partial=True) -> (0, 1) partial
        Answer("(?r)ya", "a", 0, 1).Should().Be("(0, 1) partial=True");

        // UPSTREAM: regex.compile(r'(?r)ya(.*?)\b').match('a', 0, 1, partial=True) -> (0, 1) partial
        Answer(@"(?r)ya(.*?)\b", "a", 0, 1).Should().Be("(0, 1) partial=True");

        // UPSTREAM: regex.compile(r'(?r)ya(.*)\b').match('a', 0, 1, partial=True) -> (0, 1) partial
        Answer(@"(?r)ya(.*)\b", "a", 0, 1).Should().Be("(0, 1) partial=True");

        // The same single visible character as the slice (2, 3) of "xya". The matchable text is
        // identical, so the answers must be too.
        // RULING: upstream answers None here from its node handlers (Rule A).
        Answer("(?r)ya", "xya", 2, 3).Should().Be("(2, 3) partial=True");

        // UPSTREAM: regex.compile(r'(?r)ya(.*?)\b').match('xya', 2, 3, partial=True)
        //   -> (2, 3) partial. Upstream's own Rule B path.
        Answer(@"(?r)ya(.*?)\b", "xya", 2, 3).Should().Be("(2, 3) partial=True");

        // RULING: upstream answers None here, which is the contradiction with the line above.
        Answer(@"(?r)ya(.*)\b", "xya", 2, 3).Should().Be("(2, 3) partial=True");
    }

    [Test]
    public void Greedy_and_lazy_agree_on_whether_a_reversed_partial_exists()
    {
        // A preference between several matches cannot decide whether any match exists. Over the
        // one-character slice (2, 3) of "xya" the group can only match the empty string either way.
        // This is metamorphic invariant 'greedy-lazy-existence-agree' (docs/ORACLE-INVARIANTS.md),
        // which upstream breaks on this row and which this port must not.

        // UPSTREAM: regex.compile(r'(?r)ya(.*?)\b').match('xya', 2, 3, partial=True) -> (2, 3) partial
        Answer(@"(?r)ya(.*?)\b", "xya", 2, 3).Should().Be("(2, 3) partial=True");

        // RULING: upstream answers None for the greedy form.
        Answer(@"(?r)ya(.*)\b", "xya", 2, 3).Should().Be("(2, 3) partial=True");

        // RULING: upstream answers None for the bounded form too.
        Answer(@"(?r)ya(.?)\b", "xya", 2, 3).Should().Be("(2, 3) partial=True");
    }

    [Test]
    public void The_drawn_row_and_its_cuts_all_answer_a_partial_at_the_empty_slice()
    {
        // Ledger 24's worked row and the five one-change cuts of it, every one at the empty slice
        // (2, 2) of the two-character subject. An empty slice has no matchable text at all, so
        // every reversed pattern that needs a character runs out immediately.

        // UPSTREAM: regex.compile('(?r)ßﬁ(.*?)\\b').match('ﬁı', 2, 2, partial=True)
        //   -> (2, 2) partial
        Answer($@"(?r){_sharp}{_fi}(.*?)\b", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");

        // UPSTREAM: the same without the trailing \b -> (2, 2) partial
        Answer($"(?r){_sharp}{_fi}(.*?)", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");

        // RULING: upstream answers None once the lazy group is removed, although \b still holds at
        // position 2 (the dotless i before it is a word character) and the literals still need text.
        Answer($@"(?r){_sharp}{_fi}\b", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");

        // RULING: upstream answers None for the bare literals.
        Answer($"(?r){_sharp}{_fi}", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");

        // RULING: upstream answers None for the single literal.
        Answer($@"(?r){_sharp}(.*?)\b", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");

        // RULING: upstream answers None for the greedy group, where the lazy one above gets a
        // partial - the same greedy-against-lazy contradiction, at the empty slice.
        Answer($@"(?r){_sharp}{_fi}(.*)\b", _drawnSubject, 2, 2).Should().Be("(2, 2) partial=True");
    }

    [Test]
    public void An_empty_slice_is_an_empty_slice_wherever_it_sits()
    {
        // The same pattern at every empty slice of "xyz". At (0, 0) the answer is genuinely no
        // match and not a partial, because \b needs a word character on one side and there is none:
        // position 0 is the subject start and the slice end truncates everything after it. That
        // cell is what proves the fix did not simply make every reversed call partial.

        // UPSTREAM: regex.compile(r'(?r)ab(.*?)\b').match('xyz', 0, 0, partial=True) -> None
        Answer(@"(?r)ab(.*?)\b", "xyz", 0, 0).Should().Be("None");

        // UPSTREAM: the same call at 1, 2 and 3 -> (1, 1), (2, 2) and (3, 3) partial. Upstream's
        // own Rule B path, and the port's Rule A answer of None was the defect.
        Answer(@"(?r)ab(.*?)\b", "xyz", 1, 1).Should().Be("(1, 1) partial=True");
        Answer(@"(?r)ab(.*?)\b", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
        Answer(@"(?r)ab(.*?)\b", "xyz", 3, 3).Should().Be("(3, 3) partial=True");

        // UPSTREAM: the empty subject -> None, for the same reason as (0, 0) above.
        Answer(@"(?r)ab(.*?)\b", "", 0, 0).Should().Be("None");
    }

    [Test]
    public void A_bare_reversed_literal_runs_out_at_every_empty_slice()
    {
        // The same question inverted: a pattern with no \b to fail, so every empty slice runs out.

        // UPSTREAM: regex.compile('(?r)a').match('xyz', 0, 0, partial=True) -> (0, 0) partial.
        // The one cell where the subject start and the slice start coincide, so both rules agree.
        Answer("(?r)a", "xyz", 0, 0).Should().Be("(0, 0) partial=True");

        // RULING: upstream answers None at 1, 2 and 3 - the identical empty slice, moved.
        Answer("(?r)a", "xyz", 1, 1).Should().Be("(1, 1) partial=True");
        Answer("(?r)a", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
        Answer("(?r)a", "xyz", 3, 3).Should().Be("(3, 3) partial=True");
    }

    [Test]
    public void Needing_more_text_never_makes_a_reversed_match_run_out_less()
    {
        // Metamorphic invariant 'minimum-width-partial-monotone' (docs/ORACLE-INVARIANTS.md), at
        // the empty slice (2, 2) of "xyz": a pattern needing one character cannot fail where a
        // pattern needing three gets a partial. Upstream inverts it here.

        // RULING: upstream answers None for the one- and two-character forms...
        Answer("(?r)a", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
        Answer("(?r)ab", "xyz", 2, 2).Should().Be("(2, 2) partial=True");

        // UPSTREAM: ...and (2, 2) partial for these two, which need two and three characters.
        Answer(@"(?r)ab(.*?)\b", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
        Answer(@"(?r)abc(.*?)\b", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
    }

    [Test]
    public void The_forward_direction_is_unchanged_and_was_already_uniform()
    {
        // The control. A forward partial has always fired at the slice end on every path, because
        // upstream's text_end IS the slice end; only the left edge had two rules. These five cells
        // must not move, and they are measured against upstream exactly as the others are.

        // UPSTREAM: regex.compile('a').match('xyz', 2, 2, partial=True) -> (2, 2) partial
        Answer("a", "xyz", 2, 2).Should().Be("(2, 2) partial=True");

        // UPSTREAM: the lazy and greedy forms agree forwards, where reversed they did not.
        Answer(@"ab(.*?)\b", "xyz", 2, 2).Should().Be("(2, 2) partial=True");
        Answer(@"ab(.*)\b", "xyz", 2, 2).Should().Be("(2, 2) partial=True");

        // UPSTREAM: one visible character as the subject and as a slice, forwards.
        Answer("ay", "ay", 0, 1).Should().Be("(0, 1) partial=True");
        Answer("ay", "xya", 2, 3).Should().Be("(2, 3) partial=True");
    }

    [Test]
    public void A_beginning_that_splits_a_surrogate_pair_answers_what_it_did_before_the_ruling()
    {
        // Raised by S52d's blind review. The reversed repeat walk stops at the slice start, so in
        // CODEPOINTS "arrived at the edge" and "reached or passed the edge" cannot differ - which is
        // why upstream, which indexes by codepoint, can spell the one `==` and the other `<=` and
        // never notice. In UTF-16 they can differ: a `beginning` that splits a surrogate pair lets
        // `PrevPos` step two code units and land one BELOW the slice start, and `<=` would report a
        // partial there where `==` does not.
        //
        // THE RULING SAYS NOTHING ABOUT THAT, so the slice moves the bound and not the comparison,
        // and this pins the answer it had before. There is no upstream run to cite: `regex` cannot
        // express a `pos` inside a character at all, so this is a UTF-16-only question and the
        // provenance is the decision itself (DECISIONS 2026-09-16, and the comment at the site).
        const string split = "a😀"; // 'a' then U+1F600, so code units 1 and 2 are a pair.

        // The slice (2, 3) starts INSIDE the pair. The walk overruns it - that is pre-existing and
        // not this slice's - and the answer stays what it was.
        Answer(@"(?r)\A[\s\S]*", split, 2, 3).Should().Be("None");
        Answer(@"(?r)^[\s\S]*", split, 2, 3).Should().Be("None");

        // The well-formed slice (1, 3) holds the whole pair, and there the ruling applies normally:
        // the repeat consumes the one character, runs out at the slice start, and `\A` could still
        // be reached if the caller widened `beginning` to 0 - which is what a partial promises.
        Answer(@"(?r)\A[\s\S]*", split, 1, 3).Should().Be("(1, 3) partial=True");

        // And with nothing after the repeat there is a complete match either way, so the overrun is
        // visible only through the partial: both slices answer the same span.
        Answer(@"(?r)[\s\S]*", split, 2, 3).Should().Be("(1, 3) partial=False");
        Answer(@"(?r)[\s\S]*", split, 1, 3).Should().Be("(1, 3) partial=False");
    }

    [Test]
    public void Nothing_else_about_a_non_zero_beginning_moves()
    {
        // The guard on the fix. Only the partial run-out question moved to the slice start; every
        // other view of `beginning` is Python `re`'s and upstream's, and the ported suite's 1,967
        // tests depend on it. Each expectation here is an upstream run made on 2026.9.10.

        // UPSTREAM: regex.compile('(?r)ab').search('abc', 1) -> None.
        // The match may not use text before `beginning`, which is why the text really has run out
        // there for a reversed match - the argument the ruling rests on.
        new FuzzyRegex("(?r)ab", FuzzyRegexOptions.Version1)
            .Match("abc", 1)
            .Success.Should()
            .BeFalse();

        // UPSTREAM: regex.compile('(?<=a)b').search('ab', 1) -> (1, 2). Lookbehind still sees the
        // character before `beginning`.
        Match behind = new FuzzyRegex("(?<=a)b", FuzzyRegexOptions.Version1).Match("ab", 1);
        behind.Success.Should().BeTrue();
        (behind.Index, behind.Index + behind.Length).Should().Be((1, 2));

        // UPSTREAM: regex.compile('^b').search('ab', 1) -> None. `^` still refuses a non-zero one.
        new FuzzyRegex("^b", FuzzyRegexOptions.Version1)
            .Match("ab", 1)
            .Success.Should()
            .BeFalse();

        // UPSTREAM: regex.compile(r'\Ab').search('ab', 1) -> None. So does `\A`.
        new FuzzyRegex(@"\Ab", FuzzyRegexOptions.Version1)
            .Match("ab", 1)
            .Success.Should()
            .BeFalse();

        // UPSTREAM: regex.compile(r'\bb').search('a b', 2) -> (2, 3), and regex.compile(r'\Bb')
        // .search('ab', 1) -> (1, 2). `\b` and `\B` still read the character before `beginning`.
        Match boundary = new FuzzyRegex(@"\bb", FuzzyRegexOptions.Version1).Match("a b", 2);
        boundary.Success.Should().BeTrue();
        (boundary.Index, boundary.Index + boundary.Length).Should().Be((2, 3));

        Match notBoundary = new FuzzyRegex(@"\Bb", FuzzyRegexOptions.Version1).Match("ab", 1);
        notBoundary.Success.Should().BeTrue();
        (notBoundary.Index, notBoundary.Index + notBoundary.Length).Should().Be((1, 2));

        // UPSTREAM: regex.compile('(?r)ab').match('xab', 2, 3) -> None with partial=False. A
        // reversed run-out is a PARTIAL and never a match: without partial=True the answer stays no
        // match, so the fix cannot invent a complete match that upstream does not have.
        new FuzzyRegex("(?r)ab", FuzzyRegexOptions.Version1)
            .MatchAtStart("xab", 2, 1)
            .Success.Should()
            .BeFalse();
    }
}
