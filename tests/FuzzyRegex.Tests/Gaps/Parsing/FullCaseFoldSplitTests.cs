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

    private static CompiledPattern Compile(string pattern, int flags) =>
        PatternCompiler.Compile(pattern, flags, _noNamedLists, PatternCompiler.DefaultVersion);
}
