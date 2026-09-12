using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins the two places where <see cref="Source"/> cannot be a literal transcription of upstream's
/// Python, because Python's string model is not .NET's.
/// </summary>
/// <remarks>
/// These are gap tests, not ported ones: no upstream test exercises either difference, which is
/// exactly why they need pinning here.
/// </remarks>
public sealed class SourceScannerTests
{
    /// <summary>
    /// Every codepoint Python's <c>str.isspace</c> accepts, measured against CPython 3.14 on
    /// 2026-08-30 with <c>[cp for cp in range(0x110000) if chr(cp).isspace()]</c>. All 29 are in
    /// the BMP.
    /// </summary>
    private static readonly int[] _pythonWhitespace =
    [
        0x9,
        0xA,
        0xB,
        0xC,
        0xD,
        0x1C,
        0x1D,
        0x1E,
        0x1F,
        0x20,
        0x85,
        0xA0,
        0x1680,
        0x2000,
        0x2001,
        0x2002,
        0x2003,
        0x2004,
        0x2005,
        0x2006,
        0x2007,
        0x2008,
        0x2009,
        0x200A,
        0x2028,
        0x2029,
        0x202F,
        0x205F,
        0x3000,
    ];

    [Test]
    public void IsSpace_accepts_exactly_the_codepoints_Python_calls_whitespace()
    {
        // This is what (?x) skips, so a disagreement silently changes which characters a verbose
        // pattern contains. .NET's char.IsWhiteSpace is not the same set - it rejects U+001C to
        // U+001F - which is the whole reason Source has its own predicate.
        int[] accepted = [.. Enumerable.Range(0, 0x110000).Where(Source.IsSpace)];

        accepted.Should().Equal(_pythonWhitespace);
    }

    [Test]
    public void The_four_C0_separators_are_whitespace_to_Python_and_not_to_dotnet()
    {
        // The specific disagreement, stated once so a future reader does not have to diff two
        // 29-element lists to find it. Written as codepoints rather than as literals because
        // U+001C to U+001F are invisible in an editor.
        for (int c = 0x1C; c <= 0x1F; c++)
        {
            char.IsWhiteSpace((char)c).Should().BeFalse();
            Source.IsSpace(c).Should().BeTrue();
        }
    }

    [Test]
    public void A_non_BMP_literal_compiles_to_one_character_opcode_carrying_its_codepoint()
    {
        // Source reads whole codepoints, not UTF-16 code units, so the parser emits what upstream
        // emits. Verified against the local oracle 2026-08-30: intercepting _regex.compile for
        // the pattern '\U0001F63A' records code [12, 1, 128570, 1] - CHARACTER, POSITIVE_OP,
        // U+1F63A, SUCCESS - not a pair of surrogate CHARACTER opcodes.
        CompiledPattern compiled = PatternCompiler.Compile(char.ConvertFromUtf32(0x1F63A));

        compiled.Code.Should().Equal(12u, 1u, 0x1F63A, 1u);
    }

    [Test]
    public void A_non_BMP_literal_run_compiles_to_a_string_of_codepoints()
    {
        // Same oracle run: 'a\U0001F63Ab' records [74, 16, 3, 97, 128570, 98, 1] - STRING,
        // REQUIRED_OP, length 3, the three codepoints, SUCCESS. The length is 3 because upstream
        // counts codepoints; a UTF-16 reading would have said 4.
        CompiledPattern compiled = PatternCompiler.Compile("a" + char.ConvertFromUtf32(0x1F63A) + "b");

        compiled.Code.Should().Equal(74u, 16u, 3u, 97u, 0x1F63A, 98u, 1u);
    }

    [Test]
    public void A_parse_error_after_a_non_BMP_character_reports_a_UTF16_offset()
    {
        // The one place the port deliberately disagrees with upstream, because AGENTS.md says
        // every public index is a UTF-16 code unit. Verified against the local oracle 2026-08-30:
        // compiling '\U0001F63A(' raises "missing )" with pos=2, counting codepoints. Here the
        // same failure is at 3, counting the surrogate pair as the two chars .NET sees.
        Action compile = static () => PatternCompiler.Compile(char.ConvertFromUtf32(0x1F63A) + "(");

        FuzzyRegexParseException error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("missing )");
        error.Offset.Should().Be(3);
    }
}
