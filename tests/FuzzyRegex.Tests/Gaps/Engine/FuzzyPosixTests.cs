using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Leftmost-longest matching over a fuzzy section - the one combination of Phase 4's POSIX and
/// Phase 5's fuzzy matching that no oracle wave can record, because reading upstream's answer to it
/// kills the interpreter.
/// </summary>
/// <remarks>
/// <para>
/// <b>Upstream faults on <c>fuzzy_changes</c> here, and this port answers.</b> Reading
/// <c>match.fuzzy_changes</c> raises no exception: it takes the process down with an access
/// violation (<c>0xC0000005</c> on Windows, SIGSEGV elsewhere), so no <c>except</c> clause can see
/// it and the recorder cannot write such a row down at any wave size. It is a new memory-safety bug
/// of the same family as upstream issues 611-614, the 2026 fuzzing campaign, found by the composed
/// <c>interactions</c> wave S43 added.
/// </para>
/// <para>
/// <b>The condition is two things and nothing else:</b> POSIX - inline or as the flag - and a fuzzy
/// match that actually SPENT an error. Any error kind does it. No alternation is needed, and a first
/// draft of this file thought one was, because every negative control it happened to try was also a
/// zero-error match. Re-runnable as
/// <c>python tools/probes/upstream-posix-fuzzy-changes-crash.py</c>, whose fourteen rows are the
/// evidence for both halves: every zero-error row is safe, every row with a non-zero count faults.
/// </para>
/// <para>
/// <b>Nothing here is guessed, which is the part worth knowing.</b> Upstream computes the match
/// correctly and dies only when the changes are read, so the probe prints the span and the counts
/// first and a faulting row still reports them - those are upstream's own. The changes then come
/// from the same pattern with <c>(?p)</c> removed, which is safe and which POSIX cannot alter here,
/// because each of these subjects has exactly one match of exactly one length. Both halves are
/// quoted beside each assertion.
/// </para>
/// <para>
/// The generator suppresses POSIX on a row that carries a fuzzy section (see
/// <c>_generate_interactions</c> in <c>tools/record-oracle.py</c>), so this file is the whole of the
/// POSIX-times-fuzzy coverage. The ledger carries the report; nothing is filed upstream yet.
/// </para>
/// </remarks>
public sealed class FuzzyPosixTests
{
    [Test]
    public void A_posix_fuzzy_match_that_spent_a_substitution_reports_its_changes_here()
    {
        // regex.compile(r'(?p)(?:abc){e<=1}').match('axc')
        //   -> span (0, 3), counts (1, 0, 0), then an access violation reading fuzzy_changes
        // regex.compile(r'(?:abc){e<=1}').match('axc')          <- the same match without POSIX
        //   -> span (0, 3), counts (1, 0, 0), changes ((1,), (), ())
        Match m = new FuzzyRegex("(?p)(?:abc){e<=1}").MatchAtStart("axc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    public void A_posix_fuzzy_match_that_spent_a_deletion_reports_its_changes_here()
    {
        // regex.compile(r'(?p)(?:abc){d<=1}').match('ac')
        //   -> span (0, 2), counts (0, 0, 1), then an access violation reading fuzzy_changes
        // regex.compile(r'(?:abc){d<=1}').match('ac')
        //   -> span (0, 2), counts (0, 0, 1), changes ((), (), (1,))
        Match m = new FuzzyRegex("(?p)(?:abc){d<=1}").MatchAtStart("ac");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
    }

    [Test]
    public void A_posix_fuzzy_match_that_spent_an_insertion_reports_its_changes_here()
    {
        // regex.compile(r'(?p)(?:abc){i<=1}').match('abxc')
        //   -> span (0, 4), counts (0, 1, 0), then an access violation reading fuzzy_changes
        // regex.compile(r'(?:abc){i<=1}').match('abxc')
        //   -> span (0, 4), counts (0, 1, 0), changes ((), (2,), ())
        Match m = new FuzzyRegex("(?p)(?:abc){i<=1}").MatchAtStart("abxc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(2);
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    public void The_posix_option_faults_upstream_on_the_same_terms_as_the_inline_flag()
    {
        // regex.compile(r'(?:abc){e<=1}', regex.POSIX).match('axc')
        //   -> span (0, 3), counts (1, 0, 0), then an access violation reading fuzzy_changes
        // The two routes reach the parser differently - one is a flag on the call, the other is
        // pattern text - and they fault identically, which is what says the bug is in the matcher
        // rather than in how the flag was spelt.
        Match m = new FuzzyRegex("(?:abc){e<=1}", FuzzyRegexOptions.Posix).MatchAtStart("axc");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    [Test]
    public void A_posix_fuzzy_match_that_spent_nothing_is_safe_upstream_and_agrees_here()
    {
        // The other half of the condition, and the reason this file does not claim more than it
        // measured. Upstream renders these without faulting, so they are quoted outright:
        //   regex.compile(r'(?p)(?:abc){e<=1}').match('abc')
        //     -> span (0, 3), counts (0, 0, 0), changes ((), (), ())
        //   regex.compile(r'(?p)(?:aa|a){e<=1}').match('aa')
        //     -> span (0, 2), counts (0, 0, 0), changes ((), (), ())
        Match exact = new FuzzyRegex("(?p)(?:abc){e<=1}").MatchAtStart("abc");

        exact.Success.Should().BeTrue();
        (exact.Index, exact.Length).Should().Be((0, 3));
        exact.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        exact.FuzzyChanges.Substitutions.Should().BeEmpty();

        Match longest = new FuzzyRegex("(?p)(?:aa|a){e<=1}").MatchAtStart("aa");

        longest.Success.Should().BeTrue();
        (longest.Index, longest.Length).Should().Be((0, 2));
        longest.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Leftmost_longest_keeps_the_first_branch_and_the_insertion_that_made_it_long_enough()
    {
        // regex.compile(r'(?p)(?:a|aa){e<=1}').match('aa')
        //   -> span (0, 2), counts (0, 1, 0), then an access violation reading fuzzy_changes
        //
        // The row the wave would have drawn, and the one that makes the counts load-bearing rather
        // than decorative. Upstream takes the FIRST branch, 'a', and spends an insertion to cover
        // the second character, reaching length 2 by paying for it - rather than taking 'aa', which
        // covers the same two characters exactly. Both candidates are length 2, so leftmost-longest
        // does not separate them, and upstream keeps the one it found first. This port agrees,
        // counts and all.
        //
        // IT DID NOT BEFORE S43, and that is why this test is worth its place: with the counts not
        // restored, the port reported this match with (0, 0, 0) - the insertion it had really spent
        // erased by the POSIX restore. The span was right and the cost was a lie, which no wave
        // could ever have caught, because upstream dies rendering this match's changes.
        Match m = new FuzzyRegex("(?p)(?:a|aa){e<=1}").MatchAtStart("aa");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().HaveCount(1);
    }

    /// <summary>
    /// POSIX chooses which match is answered; it does not make the chosen span cost more than the
    /// same span costs without it.
    /// </summary>
    /// <remarks>
    /// Row 76983 of the seed-7 6000-row gate, as the wave drew it. Both engines answer the span
    /// <c>(0, 3)</c> and the same group, and <b>upstream charges <c>(1, 0, 1)</c> under POSIX where
    /// its own POSIX-free engine fits that very span in <c>(0, 0, 1)</c></b>. Measured 2026-09-14 on
    /// regex 2026.9.10, <c>tools/probes/upstream-posix-and-atomic-free-answers.py</c>; the oracle
    /// entry <c>posix-fuzzy-contradicts-its-own-flagless-answer</c> classifies the wave row.
    /// <para>
    /// <b>This is ledger entry 9 seen from upstream's side.</b> S48b fixed this port's copy of the
    /// same defect - the POSIX <c>FAILURE</c> arm's <c>RestoreBestMatch</c> put the counts back and
    /// left <c>TotalErrors</c> holding the LOSING candidate's, so the <c>(?e)</c> walk cut itself
    /// off at <c>3 &gt;= 3</c> - and upstream still has it. The assertion is therefore an invariant
    /// of this port against ITSELF, POSIX against no POSIX, which is the only standard available
    /// when upstream's own answer is the thing under suspicion.
    /// </para>
    /// </remarks>
    [Test]
    public void A_posix_enhancematch_span_costs_no_more_than_the_same_span_costs_without_posix()
    {
        const string pattern = @"(?e)(?r)(?:[^\d][a\d]\p{L}){s<=1,i<=1,d<=1}(\p{Lu})+\b";
        const string subject = " ﬀS";

        Match posix = new FuzzyRegex("(?p)" + pattern).Match(subject);
        Match plain = new FuzzyRegex(pattern).Match(subject);

        posix.Success.Should().BeTrue();
        plain.Success.Should().BeTrue();

        (posix.Index, posix.Length).Should().Be((0, 3));
        (posix.Index, posix.Length).Should().Be((plain.Index, plain.Length));

        // The whole of the claim: the flag moved no bound, so it must not have moved the cost.
        // Upstream answers (1, 0, 1) to the POSIX question and (0, 0, 1) to the other.
        posix.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        posix.FuzzyCounts.Should().Be(plain.FuzzyCounts);
    }

    /// <summary>
    /// A POSIX fuzzy match spends the errors its own POSIX-free engine spends, at the span the two
    /// agree on.
    /// </summary>
    /// <remarks>
    /// Row 73895 of the seed-7 6000-row gate, and the second shape the entry
    /// <c>posix-fuzzy-contradicts-its-own-flagless-answer</c> covers. Upstream, under
    /// <c>(?b)(?e)(?r)(?p)</c>, reports the span <c>(0, 7)</c> in codepoints at a cost of one
    /// error - and <b>its own POSIX-free <c>fullmatch</c> over that very span answers it with
    /// none</b>. This port answers the span with none, and with the captures upstream's own
    /// POSIX-free engine gives.
    /// <para>
    /// The anchored question is what judges this row, because upstream's POSIX-free SCAN starts its
    /// first match at codepoint 4 rather than 0 - POSIX is doing its proper job of choosing the
    /// longer match, and doing it at a price its own engine does not charge. Measured 2026-09-14 on
    /// regex 2026.9.10, <c>tools/probes/upstream-posix-and-atomic-free-answers.py</c>.
    /// </para>
    /// <para>
    /// The subject's two astral characters are why the UTF-16 span is <c>(0, 9)</c> where upstream's
    /// codepoint span is <c>(0, 7)</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void A_posix_fuzzy_match_spends_what_the_flagless_engine_spends()
    {
        const string pattern =
            @"(?b)(?e)(?r)(?:(?P<g1>\D+?)([\p{L}||\p{N}]*)\w){e<=2,s<=1}(?P<g3>[A])(?:(?(3)(?!(?P>g3))[\w--[0-9]]))*?";
        string subject = "\nAA" + char.ConvertFromUtf32(0x1F600) + char.ConvertFromUtf32(0x1F600) + "aa ";

        // The flag bits the wave drew it at, 0x10A, written inline: VERSION1, MULTILINE, IGNORECASE.
        Match m = new FuzzyRegex("(?imV1)(?p)" + pattern).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 9));

        // Upstream charges one substitution for this span; its own POSIX-free fullmatch over the
        // same span charges nothing, and so does this port.
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // And the captures are upstream's own POSIX-free anchored answer: codepoints (0, 5), (5, 5)
        // and (6, 7), which are UTF-16 (0, 7), (7, 7) and (8, 9) across the two astral characters.
        (m.Groups[1].Index, m.Groups[1].Length)
            .Should()
            .Be((0, 7));
        (m.Groups[2].Index, m.Groups[2].Length).Should().Be((7, 0));
        (m.Groups[3].Index, m.Groups[3].Length).Should().Be((8, 1));
    }

    /// <summary>
    /// POSIX does not create a match the flagless engine cannot make, so a bounded substitution
    /// replaces once here and not twice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row 24916 of the seed-7 2000-row wave of commit <c>407c0cb</c> (2026-09-15), and the third
    /// shape the entry <c>posix-fuzzy-contradicts-its-own-flagless-answer</c> covers. It is the one
    /// where neither the cost nor the span moves. Upstream's <c>subn</c> with <c>count=2</c> reports
    /// <b>two</b> replacements under POSIX and <b>one</b> without it, and because the template
    /// <c>\1</c> expands to the empty group either way the two answers carry the same text - the
    /// count is the only field that says what happened, which is why no predicate over the rendered
    /// answer could have found this and the entry is keyed on rows.
    /// </para>
    /// <para>
    /// POSIX is documented as leftmost-longest CHOOSING among the matches the ordinary engine can
    /// make, so an extra zero-width match that only appears when the flag is set breaks the contract
    /// as plainly as charging a span more errors does. Measured 2026-09-15 on regex 2026.9.10,
    /// <c>tools/probes/upstream-posix-and-atomic-free-answers.py</c>; this port answers upstream's own
    /// POSIX-free count.
    /// </para>
    /// </remarks>
    [Test]
    public void Posix_does_not_add_a_match_the_flagless_engine_cannot_make()
    {
        // The flag bits the wave drew it at, 0x1400A: POSIX, FULLCASE, MULTILINE, IGNORECASE. The
        // recorder resolves a version-less pattern under upstream's own default, so Version0 here.
        FuzzyRegex pattern = new(
            @"(?b)(?:([a]*)[a]*){s<=1}\g<1>\K$",
            FuzzyRegexOptions.Posix
                | FuzzyRegexOptions.FullCase
                | FuzzyRegexOptions.Multiline
                | FuzzyRegexOptions.IgnoreCase
                | FuzzyRegexOptions.Version0
        );

        string replaced = pattern.Replace("\rA\n", "\\1", 2, out int replacements);

        replacements.Should().Be(1);
        replaced.Should().Be("\rA\n");
    }

    /// <summary>
    /// POSIX and <c>BESTMATCH</c> together do not destroy a match that either flag alone keeps -
    /// not even when the fuzzy section they are wrapped round spends no error at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sweep row 35 of <c>tools/probes/sweep-divergence-rows.jsonl</c>, minimised by S52 sitting 17
    /// to three ASCII characters, and the fourth shape the entry
    /// <c>posix-fuzzy-contradicts-its-own-flagless-answer</c> covers: the one where POSIX moves
    /// neither a cost nor a span nor a count, because there is no answer left to move. Upstream
    /// reports <c>None</c>, and gives the same match back the moment either flag is deleted.
    /// </para>
    /// <para>
    /// <b>Measured on regex 2026.9.10, 2026-09-15</b>, the last block of
    /// <c>python tools/probes/upstream-bestmatch-sweep-group-a.py</c>, which prints all four flag
    /// combinations and the four negative controls below:
    /// <code>
    /// regex.compile(r'(?b)(?r)\K(.(.{2}){i&lt;=1})', regex.POSIX).match('baa')  -> None
    /// regex.compile(r'(?r)\K(.(.{2}){i&lt;=1})',     regex.POSIX).match('baa')  -> (0,0) g1=(0,3) g2=(1,3)
    /// regex.compile(r'(?b)(?r)\K(.(.{2}){i&lt;=1})', 0          ).match('baa')  -> (0,0) g1=(0,3) g2=(1,3)
    /// </code>
    /// </para>
    /// <para>
    /// <b>The fuzzy section has to be there and has to spend nothing</b>, which is the part that
    /// makes this the strongest form of ledger entry 16. Every door that answers reports counts
    /// <c>(0, 0, 0)</c>, so no fuzzy matching happens on the winning path; delete the
    /// <c>{i&lt;=1}</c> entirely and upstream answers under both flags; make it <c>{s&lt;=1}</c>
    /// instead and it answers as well. The <c>\K</c> and the <c>(?r)</c> are load-bearing too -
    /// without either, upstream answers the span <c>(0, 3)</c> under both flags.
    /// </para>
    /// <para>
    /// Upstream's own reproduction of entry 16 needs <c>finditer(overlapped=True)</c> over a
    /// nine-character subject and loses only the LONGEST of seven matches. This is a plain
    /// <c>match</c>, no scan anywhere, and the whole answer goes.
    /// </para>
    /// </remarks>
    [Test]
    public void Posix_and_bestmatch_together_keep_a_match_that_either_flag_alone_keeps()
    {
        // POSIX as the flag, as the row drew it; the recorder resolves a version-less pattern under
        // upstream's own default, so Version0 here.
        FuzzyRegex pattern = new(@"(?b)(?r)\K(.(.{2}){i<=1})", FuzzyRegexOptions.Posix | FuzzyRegexOptions.Version0);

        Match m = pattern.Match("baa");

        m.Success.Should().BeTrue();

        // Upstream's own answer with either flag deleted, all ASCII so UTF-16 and codepoints agree.
        // Upstream's spans above are (start, END); these are (index, LENGTH), so its g2 (1, 3) is
        // (1, 2) here.
        (m.Index, m.Length)
            .Should()
            .Be((0, 0));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 3));
        (m.Groups[2].Index, m.Groups[2].Length).Should().Be((1, 2));

        // Nothing fuzzy happened on the path that wins, which is what makes the loss indefensible.
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }
}
