using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// <see cref="FuzzyRegexOptions.Ascii"/>, <see cref="FuzzyRegexOptions.Unicode"/> and
/// <see cref="FuzzyRegexOptions.Word"/>, which S53b added: three upstream flags that were
/// reachable only from inside a pattern, as <c>(?a)</c>, <c>(?u)</c> and <c>(?w)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The proof that an option is the same thing as the inline flag is bytecode identity, not a
/// matching example: the compile-parity corpus already holds 29 rows upstream compiled with
/// <c>ASCII</c> or <c>UNICODE</c> passed as a flag, so the bit values themselves are pinned
/// against upstream there. What is new here is the public enum member, and what these tests add
/// is that it reaches the compiler as that bit. <c>WORD</c> has no corpus row - upstream's own
/// suite never passes it as a flag - so its answer is pinned against a measured upstream run and
/// by the oracle's <c>boundaries</c> generator, which draws it as a flags integer from S53b on.
/// </para>
/// <para>
/// Decision D (2026-08-30) kept these three out of the public enum. S53b supersedes it for them
/// only; <c>LOCALE</c>, <c>DEBUG</c> and <c>TEMPLATE</c> stay out. See DECISIONS.
/// </para>
/// </remarks>
public sealed class EncodingAndWordOptionTests
{
    [Test]
    public void The_three_new_members_carry_upstreams_bit_values()
    {
        // Measured against regex 2026.9.10 on 2026-09-16 (.scratch probe, recorded in the slice
        // notes): hex(regex.ASCII) is 0x80, hex(regex.UNICODE) is 0x20, hex(regex.WORD) is 0x800.
        ((int)FuzzyRegexOptions.Ascii)
            .Should()
            .Be(0x80);
        ((int)FuzzyRegexOptions.Unicode).Should().Be(0x20);
        ((int)FuzzyRegexOptions.Word).Should().Be(0x800);
    }

    [Test]
    [Arguments(FuzzyRegexOptions.Ascii, "(?a)")]
    [Arguments(FuzzyRegexOptions.Unicode, "(?u)")]
    [Arguments(FuzzyRegexOptions.Word, "(?w)")]
    public void The_option_and_the_leading_inline_flag_compile_to_identical_bytecode(
        FuzzyRegexOptions option,
        string inline
    )
    {
        // \b and \w are the two constructs all three flags dispatch on, so a pattern holding both
        // is the one that can tell them apart.
        const string body = @"\b\w+\b";

        CompiledPattern asOption = PatternCompiler.Compile(
            body,
            (int)option,
            namedLists: null,
            PatternCompiler.DefaultVersion
        );
        CompiledPattern asInline = PatternCompiler.Compile(
            inline + body,
            0,
            namedLists: null,
            PatternCompiler.DefaultVersion
        );

        asOption.Code.Should().Equal(asInline.Code);
        asOption.Flags.Should().Be(asInline.Flags);
    }

    [Test]
    public void Options_reports_the_flag_a_pattern_set_for_itself()
    {
        new FuzzyRegex(@"(?w)\bx\b")
            .Options.Should()
            .Be(
                FuzzyRegexOptions.Word
                    | FuzzyRegexOptions.Unicode
                    | FuzzyRegexOptions.Version1
                    | FuzzyRegexOptions.FullCase
            );
    }

    [Test]
    public void Unicode_is_on_every_pattern_that_named_no_encoding()
    {
        // Upstream's _main._compile ORs UNICODE into every str pattern's flags (upstream/regex/
        // _main.py lines 570-574), which is why its own test_getattr expects to see it. Measured
        // against regex 2026.9.10 on 2026-09-16: regex.compile(r'\bx\b', regex.U).flags is
        // 0x2020 - UNICODE plus upstream's VERSION0 default - so UNICODE is reported whether or
        // not the caller asked for it.
        new FuzzyRegex("a")
            .Options.Should()
            .HaveFlag(FuzzyRegexOptions.Unicode);
        new FuzzyRegex("a", FuzzyRegexOptions.Unicode).Options.Should().HaveFlag(FuzzyRegexOptions.Unicode);
        new FuzzyRegex("(?a)a").Options.Should().NotHaveFlag(FuzzyRegexOptions.Unicode);
    }

    [Test]
    public void Word_as_an_option_changes_what_matches()
    {
        // Measured against regex 2026.9.10 on 2026-09-16:
        //     regex.findall(r'\b\w+\b', "can't", regex.WORD)  ->  []
        //     regex.findall(r'\b\w+\b', "can't")              ->  ['can', 't']
        // UAX #29 joins a word across the apostrophe, so no boundary sits where \w+ has to stop.
        FuzzyRegex.Matches("can't", @"\b\w+\b", FuzzyRegexOptions.Word).Should().BeEmpty();

        FuzzyRegex.Matches("can't", @"\b\w+\b").Select(static m => m.Value).Should().Equal("can", "t");
    }

    [Test]
    public void Ascii_as_an_option_narrows_the_classes_to_ASCII()
    {
        // Measured against regex 2026.9.10 on 2026-09-16 (the same probe):
        //     regex.findall(r'\w+', 'aé', regex.ASCII)  ->  ['a']
        //     regex.findall(r'\w+', 'aé')               ->  ['aé']
        FuzzyRegex.Matches("aé", @"\w+", FuzzyRegexOptions.Ascii).Select(static m => m.Value).Should().Equal("a");

        FuzzyRegex.Matches("aé", @"\w+").Select(static m => m.Value).Should().Equal("aé");
    }

    [Test]
    [Arguments(FuzzyRegexOptions.Ascii | FuzzyRegexOptions.Unicode)]
    public void Two_encodings_at_once_are_rejected_with_upstreams_message(FuzzyRegexOptions options)
    {
        // Measured against regex 2026.9.10 on 2026-09-16: regex.compile('x', regex.ASCII |
        // regex.UNICODE) raises ValueError("ASCII, LOCALE and UNICODE flags are mutually
        // incompatible"). The check already existed for the inline forms; this is the option path
        // reaching it.
        Action compile = () => _ = new FuzzyRegex("x", options);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .WithMessage("ASCII, LOCALE and UNICODE flags are mutually incompatible");
    }
}
