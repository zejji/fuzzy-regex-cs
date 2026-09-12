using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins <c>Sequence._fix_full_casefold</c> (<c>upstream/regex/_regex_core.py:3636-3668</c>), and in
/// particular the <c>.lower()</c> on line 3643 that decides which chunks of a literal get the
/// full-case-folding opcode.
/// </summary>
/// <remarks>
/// <para>
/// The function folds the literal, lower-cases the result, and marks every chunk that matches the
/// folded form of a character which expands on folding. Folding alone is not enough: <c>fold_case</c>
/// leaves <c>I</c> alone, because <c>I</c>/<c>i</c>/<c>ı</c> are the Turkic triple, so
/// <c>fold("fI")</c> is <c>"fI"</c> and only <c>.lower()</c> turns it into <c>"fi"</c> - which is
/// the folded form of the ligature U+FB01. Drop the <c>.lower()</c> and this literal gets
/// <c>STRING_IGN</c> instead of <c>STRING_FLD</c>.
/// </para>
/// <para>
/// No corpus row reaches it - upstream's own suite has no case-folded literal whose lower-casing
/// changes the answer - so these are gap tests. Every expected value below was measured against
/// the local oracle (<c>regex</c> 2026.7.19) on 2026-08-30 by intercepting
/// <c>regex._regex.compile</c>:
/// </para>
/// <code>
/// 'fI' flags=I|F|U   code = [75, 16, 2, 102, 73, 1]   req_chars = [102, 73]  req_flags = 16386
/// 'fI' flags=I|U     code = [77, 16, 2, 102, 73, 1]   req_chars = [102, 73]  req_flags = 2
/// 'fi' flags=I|F|U   code = [75, 16, 2, 102, 105, 1]
/// </code>
/// <para>
/// 75 is <see cref="Opcode.StringFld"/> and 77 is <see cref="Opcode.StringIgn"/>.
/// </para>
/// </remarks>
public sealed class FullCaseFoldSplitTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    /// <summary>
    /// <c>fI</c> is one chunk needing full case-folding, but only because the folded text is
    /// lower-cased before the expanding forms are looked for in it.
    /// </summary>
    [Test]
    public void A_literal_whose_folded_form_only_matches_an_expansion_after_lowercasing_uses_the_full_fold_opcode()
    {
        CompiledPattern compiled = Compile("fI", RegexFlags.IgnoreCase | RegexFlags.FullCase | RegexFlags.Unicode);

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal((uint)Opcode.StringFld, 16u, 2u, 102u, 73u, (uint)Opcode.Success);
            compiled.ReqChars.Should().Equal(102, 73);
            compiled.ReqFlags.Should().Be(RegexFlags.FullCase | RegexFlags.IgnoreCase);
        }
    }

    /// <summary>The same literal without <c>FULLCASE</c> takes the cheaper simple-folding opcode.</summary>
    [Test]
    public void The_same_literal_without_fullcase_uses_the_simple_fold_opcode()
    {
        CompiledPattern compiled = Compile("fI", RegexFlags.IgnoreCase | RegexFlags.Unicode);

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal((uint)Opcode.StringIgn, 16u, 2u, 102u, 73u, (uint)Opcode.Success);
            compiled.ReqFlags.Should().Be(RegexFlags.IgnoreCase);
        }
    }

    /// <summary>The already-lower-case spelling reaches the same opcode by the same route.</summary>
    [Test]
    public void The_already_lowercase_spelling_uses_the_full_fold_opcode_too()
    {
        CompiledPattern compiled = Compile("fi", RegexFlags.IgnoreCase | RegexFlags.FullCase | RegexFlags.Unicode);

        compiled.Code.Should().Equal((uint)Opcode.StringFld, 16u, 2u, 102u, 105u, (uint)Opcode.Success);
    }

    /// <summary>
    /// A character that expands on folding compiles to a branch of the character itself and its
    /// expansion, which is <c>Character._compile</c>'s <c>len(self.folded) &gt; 1</c> arm
    /// (<c>:2629-2632</c>). Measured: <c>'ß'</c> with <c>I|F|U</c> is
    /// <c>[10, 13, 1, 223, 36, 75, 0, 2, 115, 115, 20, 1]</c>.
    /// </summary>
    [Test]
    public void A_character_that_expands_on_folding_compiles_to_a_branch_of_itself_and_its_expansion()
    {
        CompiledPattern compiled = Compile("ß", RegexFlags.IgnoreCase | RegexFlags.FullCase | RegexFlags.Unicode);

        using (new AssertionScope())
        {
            compiled
                .Code.Should()
                .Equal(
                    (uint)Opcode.Branch,
                    (uint)Opcode.CharacterIgn,
                    1u,
                    0xDFu,
                    (uint)Opcode.Next,
                    (uint)Opcode.StringFld,
                    0u,
                    2u,
                    (uint)'s',
                    (uint)'s',
                    (uint)Opcode.End,
                    (uint)Opcode.Success
                );
            compiled.ReqChars.Should().Equal('s', 's');
        }
    }

    /// <summary>
    /// The same for a set: <c>SetBase._handle_case_folding</c> (<c>:3762-3790</c>) wraps the set in
    /// a branch with one <c>String</c> per distinct expansion the set matches. Measured to be the
    /// identical bytecode to the bare character above, because a one-member set is not a set.
    /// </summary>
    [Test]
    public void A_one_member_set_of_an_expanding_character_folds_the_same_way_as_the_character()
    {
        CompiledPattern set = Compile("[ß]", RegexFlags.IgnoreCase | RegexFlags.FullCase | RegexFlags.Unicode);
        CompiledPattern bare = Compile("ß", RegexFlags.IgnoreCase | RegexFlags.FullCase | RegexFlags.Unicode);

        set.Code.Should().Equal(bare.Code);
    }

    /// <summary>
    /// DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer. Found by S34's 2000-row
    /// <c>case-folding</c> wave as a crash both engines share, and minimised in S35.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>_fix_full_casefold</c> finds its chunks in the <b>folded</b> text and then slices the
    /// <b>unfolded</b> <c>characters</c> tuple with those offsets (<c>:3661</c> and <c>:3666</c>).
    /// The two are the same length only while nothing expands - and finding the things that expand
    /// is the entire job of the function, so the arithmetic is wrong exactly where it is used. Every
    /// expansion earlier in the run shifts every later offset right, which does two things: a
    /// character that needs the full fold can be sliced into a simple-<c>IGNORECASE</c> chunk and
    /// then not match, and the last chunk can start past the end of the run and produce an EMPTY
    /// <c>String</c> node, whose <c>get_firstset</c> (<c>:4036</c>) indexes <c>characters[0]</c> and
    /// raises. S29's note that it "works out because an expansion begins where its character does"
    /// is true of one expansion and false of two.
    /// </para>
    /// <para>
    /// Measured against <c>regex</c> 2026.7.19 and re-verified on 2026.9.10, 2026-09-12. The
    /// reference is CPython's <c>str.casefold()</c>, which is Unicode full case folding, so it is an
    /// oracle independent of both engines:
    /// </para>
    /// <code>
    /// regex.compile(r'(?r)^İﬁ', regex.I | regex.F)   IndexError: tuple index out of range
    /// regex.fullmatch(r'ﬁaﬁ', 'fiafi', regex.I | regex.F)    None; casefold says it matches
    /// regex.fullmatch(r'ﬀaﬃ', 'ffaffi', regex.I | regex.F)   None; casefold says it matches
    /// </code>
    /// <para>
    /// The port backs each chunk's START up to the character whose fold contains it, and leaves the
    /// end as upstream's drifted offset - that half of the drift can only pull in trailing
    /// characters that did not need the full fold, which cannot change an answer, and leaving it
    /// keeps the bytecode bit-identical to upstream on every pattern upstream gets right (corpus
    /// rows #323 and #333 both split differently under a both-ends mapping). The reasoning is in
    /// <c>Sequence.FixFullCasefold</c>'s remarks. Upstream is not fixed here; the drafted report is
    /// ledger entry 6 in <c>docs/plan/upstream-reports/LEDGER.md</c>.
    /// </para>
    /// <para>
    /// One nearby defect is deliberately NOT fixed here, because it is not this function's: the
    /// <c>expanded</c> list is built from <c>fold_case</c> alone while the text it is looked for in
    /// is <c>fold_case(...).lower()</c> (<c>:3639</c> against <c>:3643</c>). <c>U+0130</c> is the one
    /// character that differs - <c>fold_case</c> leaves it alone and only <c>.lower()</c> gives
    /// <c>i̇</c> - so no chunk is ever found for it and <c>İ</c> does not match
    /// <c>i̇</c> on either side. Lowering the list is a one-word change, but it would only
    /// half-work: the engine's own <c>STRING_FLD</c> folds with <c>fold_case</c> too, so
    /// <c>U+0130</c> would have to expand there as well, which is a change to the case-folding
    /// tables and to every construct that consults them. Recorded as ledger entry 7 instead of
    /// half-done here.
    /// </para>
    /// </remarks>
    [Test]
    public void A_run_with_two_expansions_maps_its_chunks_back_to_characters_instead_of_folded_offsets()
    {
        using (new AssertionScope())
        {
            // Two expansions with a plain character between them: the second 'fi' ligature used to
            // be sliced into the simple-IGNORECASE chunk and stopped matching its own expansion.
            new FuzzyRegex("ﬁaﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("fiafi")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 5);

            // The same with expansions of different lengths, so the drift is two rather than one.
            new FuzzyRegex("ﬀaﬃ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("ffaffi")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 6);
        }
    }

    /// <summary>
    /// The crash the S34 wave found, which is the same defect one step further on: the last chunk
    /// starts past the end of the run, the empty <c>String</c> node reaches
    /// <c>String.get_firstset</c>, and it indexes <c>characters[0]</c>. Reversed, because the
    /// firstset walk reaches the empty node first that way round; see the remarks on
    /// <see cref="A_run_with_two_expansions_maps_its_chunks_back_to_characters_instead_of_folded_offsets"/>.
    /// </summary>
    [Test]
    public void A_run_whose_last_chunk_starts_past_its_end_no_longer_builds_an_empty_string_node()
    {
        using (new AssertionScope())
        {
            new FuzzyRegex("(?r)^İﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("İﬁ")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 2);

            // The second way to run off the end, and the one the first fix missed: here the FIRST
            // chunk's end overshoots, because U+FB03 folds to three codepoints and 'characters' is
            // only three long, so 'pos' lands exactly on the end of the run and the next chunk has
            // nothing left to take. Found by S35's blind review.
            new FuzzyRegex("(?r)^ﬃaﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("ffiafi")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 6);
        }
    }

    /// <summary>
    /// The sweep the two tests above are minimised cases of: every run of one to four characters
    /// over an alphabet of expansions and plain letters, anchored and reversed so the first-set walk
    /// reaches whatever the split produced, must compile and must match its own full case folding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written because the first attempt at the fix passed both minimised cases and still crashed on
    /// 1,764 patterns of 30,940 - the blind review enumerated them, and a rule proved on two hand-
    /// picked runs is not a rule. 22,620 patterns here - 12 + 144 + 1,728 + 20,736 - about a second.
    /// </para>
    /// <para>
    /// <c>U+0130</c> is deliberately NOT in the alphabet. It is the one expanding character upstream
    /// never marks for the full fold at all, because the inventory it looks for is not lower-cased
    /// where the text it looks in is; this port follows upstream's folding tables, so <c>İ</c> does
    /// not match <c>i̇</c> here either. That is ledger entry 7 and a separate defect - see the
    /// remarks on <see cref="Sequence"/>'s <c>FixFullCasefold</c>.
    /// </para>
    /// </remarks>
    [Test]
    public void Every_short_run_of_expansions_and_plain_letters_compiles_and_matches_its_own_folding()
    {
        const string alphabet = "ﬀﬁﬃﬅßŉa" + "sfitn";
        List<string> broken = [];

        foreach (string run in Runs(alphabet, 4))
        {
            // CPython's str.casefold() has no .NET twin, so the expectation is spelled out from the
            // table below rather than computed by the same code the parser uses.
            string folded = string.Concat(
                run.Select(static c => _foldings.TryGetValue(c, out string? f) ? f : c.ToString())
            );

            try
            {
                Match match = new FuzzyRegex(
                    "(?r)^" + run,
                    FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase
                ).Match(folded);

                if (!match.Success || match.Index != 0 || match.Length != folded.Length)
                {
                    broken.Add($"{Escape(run)} against {Escape(folded)}: {match.Index},{match.Length},{match.Success}");
                }
            }
            catch (IndexOutOfRangeException e)
            {
                broken.Add($"{Escape(run)}: {e.GetType().Name}");
            }
        }

        broken.Should().BeEmpty();
    }

    /// <summary>The full case foldings the sweep's alphabet needs, from the Unicode CaseFolding.txt F and C mappings.</summary>
    private static readonly Dictionary<char, string> _foldings = new()
    {
        ['ﬀ'] = "ff",
        ['ﬁ'] = "fi",
        ['ﬃ'] = "ffi",
        ['ﬅ'] = "st",
        ['ß'] = "ss",
        ['ŉ'] = "ʼn",
    };

    private static IEnumerable<string> Runs(string alphabet, int maxLength)
    {
        List<string> current = [""];

        for (int length = 1; length <= maxLength; length++)
        {
            List<string> next = [];
            foreach (string prefix in current)
            {
                foreach (char c in alphabet)
                {
                    next.Add(prefix + c);
                }
            }

            foreach (string run in next)
            {
                yield return run;
            }

            current = next;
        }
    }

    private static string Escape(string text) =>
        string.Concat(text.Select(static c => c < 0x80 ? c.ToString() : $"\\u{(int)c:x4}"));

    /// <summary>
    /// The controls for the test above: the shapes that already worked must still work, so that the
    /// remapping is not just "mark everything as needing the full fold".
    /// </summary>
    [Test]
    public void Remapping_the_chunks_leaves_the_single_expansion_shapes_alone()
    {
        using (new AssertionScope())
        {
            // A chunk that genuinely spans two characters must still span them: 'f' then 'i' folds
            // to the same text as the ligature, so this pattern has to match the ligature.
            new FuzzyRegex("fi", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("ﬁ")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 1);

            // One expansion, before and after a plain character, and two in a row - the four shapes
            // upstream gets right, all measured as matching there on 2026-09-12.
            new FuzzyRegex("aﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("afi")
                .Should()
                .NotBeNull();
            new FuzzyRegex("ﬁa", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("fia")
                .Should()
                .NotBeNull();
            new FuzzyRegex("ﬁﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("fifi")
                .Should()
                .NotBeNull();
            new FuzzyRegex("ﬁaﬁ", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("ﬁaﬁ")
                .Should()
                .NotBeNull();

            // And a run with no expansion in it at all is untouched by the remapping.
            new FuzzyRegex("abc", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("ABC")
                .Should()
                .NotBeNull();

            // The evidence that leaving the chunk END as upstream's drifted offset cannot change an
            // answer. Here the drift pulls the 's' into the folded run, so the run's fold is 'fis'
            // and the subject folds to 'fiss' - the matcher must refuse rather than consume half of
            // the 'ss'. Upstream answers None too, measured 2026-09-12.
            new FuzzyRegex("ﬁs", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("fiß")
                .Success.Should()
                .BeFalse();

            // ... and the same run does match when the whole expansion is there.
            new FuzzyRegex("ﬁss", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)
                .Match("fiß")
                .Should()
                .Match<Match>(static m => m.Index == 0 && m.Length == 3);
        }
    }

    private static CompiledPattern Compile(string pattern, int flags) =>
        PatternCompiler.Compile(pattern, flags, _noNamedLists, PatternCompiler.DefaultVersion);
}
