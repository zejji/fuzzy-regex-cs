using AwesomeAssertions;
using AwesomeAssertions.Execution;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// The replacement-template compiler's error paths and its out-of-range escapes, neither of which
/// the compile-parity corpus can reach.
/// </summary>
/// <remarks>
/// <para>
/// The corpus holds the 62 templates upstream's own suite compiles <i>successfully</i>, recorded
/// from a run of that suite. Nothing in it is a malformed template, an escape whose value is
/// outside <c>chr()</c>'s range, or a <c>\g&lt;...&gt;</c> that names a group the pattern has not
/// got - so without this file the whole of <c>_compile_replacement</c>'s error handling would be
/// unverified logic.
/// </para>
/// <para>
/// Every expected value below was measured against <c>regex</c> 2026.7.19 on 2026-08-31 with
/// <c>.scratch/s12_edges.py</c>, which calls <c>_main._compile_replacement_helper</c> directly.
/// The offsets are into the <b>template</b>, not the pattern, because that is what upstream passes
/// as <c>source.string</c>.
/// </para>
/// <para>
/// Two things here are deliberately not what the pattern side does, and both were measured rather
/// than assumed. A bare <c>\N</c> in a template is an <b>error</b> where <c>\N</c> in a pattern is
/// the literal <c>N</c>: <c>parse_repl_named_char</c> rewinds and returns <c>None</c>, and
/// <c>_compile_replacement</c> then falls through to <c>bad escape \N</c>. And the name characters
/// are <c>ALPHA | {" "}</c>, narrower than the pattern side's
/// <c>ALNUM | {" ", "-"}</c>, so <c>\N{1}</c> and <c>\N{LATIN-SMALL-LETTER-A}</c> are errors too.
/// </para>
/// </remarks>
public sealed class ReplacementTemplateTests
{
    private static readonly IReadOnlyDictionary<string, int> _noGroups = new Dictionary<string, int>(
        StringComparer.Ordinal
    );

    [Test]
    // T '\\N{1}'                    !! error msg='bad escape \\N' pos=2
    // T '\\N{LATIN-SMALL-LETTER-A}' !! error msg='bad escape \\N' pos=2
    // T '\\N'                       !! error msg='bad escape \\N' pos=2
    // T '\\Nx'                      !! error msg='bad escape \\N' pos=2
    // T '\\N{'                      !! error msg='bad escape \\N' pos=2
    [Arguments(@"\N{1}")]
    [Arguments(@"\N{LATIN-SMALL-LETTER-A}")]
    [Arguments(@"\N")]
    [Arguments(@"\Nx")]
    [Arguments(@"\N{")]
    public void A_named_character_escape_with_no_usable_name_is_a_bad_escape(string template)
    {
        Action compile = () => Compile(template);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        using (new AssertionScope())
        {
            error.Message.Should().Be(@"bad escape \N");
            error.Offset.Should().Be(2);
            error.Pattern.Should().Be(template, "upstream's source.string here is the template");
        }
    }

    [Test]
    // T '\\N{NO SUCH NAME}' !! error msg='undefined character name' pos=16
    public void An_unknown_character_name_is_reported_at_the_closing_brace()
    {
        Action compile = static () => Compile(@"\N{NO SUCH NAME}");

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("undefined character name");
        error.Offset.Should().Be(16);
    }

    [Test]
    // T '\\N{LATIN CAPITAL LETTER A}' -> ['A']  (the corpus row), and letters-and-spaces is the
    // whole of the accepted alphabet.
    public void A_character_name_of_letters_and_spaces_resolves() =>
        Compile(@"\N{LATIN CAPITAL LETTER A}").Should().Equal("A");

    [Test]
    [Arguments(@"\q")]
    [Arguments(@"\e")]
    [Arguments(@"\c")]
    public void An_unknown_alphabetic_escape_is_rejected(string template)
    {
        Action compile = () => Compile(template);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be($@"bad escape \{template[1]}");
        error.Offset.Should().Be(2);
    }

    [Test]
    // T '\\x4'  !! error msg='incomplete escape \\x4' pos=3
    // T '\\u00' !! error msg='incomplete escape \\u00' pos=4
    [Arguments(@"\x4", @"incomplete escape \x4", 3)]
    [Arguments(@"\u00", @"incomplete escape \u00", 4)]
    public void A_hexadecimal_escape_that_runs_out_of_digits_is_incomplete(string template, string message, int offset)
    {
        Action compile = () => Compile(template);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        using (new AssertionScope())
        {
            error.Message.Should().Be(message);
            error.Offset.Should().Be(offset);
        }
    }

    [Test]
    // T '\\U0000FFFF' -> ['￿'] and T '\\U0001F600' -> ['\U0001f600']. A template escape is
    // not range-checked where a pattern escape is (parse_repl_hex_escape has no `< 0x110000`
    // test), so the highest legal codepoint arrives here intact and becomes a surrogate pair.
    public void A_hexadecimal_escape_carries_a_whole_codepoint()
    {
        using (new AssertionScope())
        {
            Compile(@"\U0000FFFF").Should().Equal(((char)0xFFFF).ToString());
            Compile(@"\U0001F600").Should().Equal(char.ConvertFromUtf32(0x1F600));
        }
    }

    [Test]
    // T '\\UFFFFFFFF' !! ValueError str='chr() arg not in range(0x110000)'. Upstream lets a plain
    // ValueError escape rather than raising its own error type, so there is no message to port;
    // what matters is that the template is rejected.
    public void A_hexadecimal_escape_above_the_last_codepoint_is_rejected()
    {
        Action compile = static () => Compile(@"\UFFFFFFFF");

        compile.Should().Throw<NotSupportedException>();
    }

    [Test]
    // T '\\ '  -> ['\\ ']  and  T '\\\n' -> ['\\\n']: an escaped non-backslash keeps both
    // characters, which is why _compile_replacement returns a list rather than one item.
    public void An_escaped_non_backslash_keeps_the_backslash()
    {
        using (new AssertionScope())
        {
            Compile(@"\ ").Should().Equal(@"\ ");
            Compile("\\\n").Should().Equal("\\\n");
        }
    }

    [Test]
    // T '\\\U0001f600' -> ['\\\U0001f600']: one backslash before the whole codepoint, not one
    // before each half of the surrogate pair. Source.Get reads codepoints, so this falls out.
    public void An_escaped_astral_character_keeps_one_backslash() =>
        Compile("\\" + char.ConvertFromUtf32(0x1F600)).Should().Equal("\\" + char.ConvertFromUtf32(0x1F600));

    [Test]
    // T '\\g<3>' on '(a)(b)' !! error msg='invalid group reference' pos=5
    public void A_numbered_group_reference_beyond_the_last_group_is_invalid()
    {
        Action compile = static () => Compile(@"\g<3>", groupCount: 2);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        using (new AssertionScope())
        {
            error.Message.Should().Be("invalid group reference");
            error.Offset.Should().Be(5);
        }
    }

    [Test]
    // T '\\g<99999999999999999999>' !! error msg='invalid group reference' pos=24. The number is
    // read with Python's unbounded int(), so this is a range failure rather than an overflow -
    // the same BigInteger path parse_name uses (DECISIONS 2026-08-30).
    public void A_group_number_too_large_for_an_int_is_still_only_an_invalid_reference()
    {
        Action compile = static () => Compile(@"\g<99999999999999999999>", groupCount: 2);

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        using (new AssertionScope())
        {
            error.Message.Should().Be("invalid group reference");
            error.Offset.Should().Be(24);
        }
    }

    [Test]
    // T '\\g<0>' on '(a)(b)' -> [0]: group 0 is the whole match and is allowed here, which is why
    // compile_repl_group passes allow_group0=True to parse_name.
    public void Group_zero_is_a_legal_reference() => Compile(@"\g<0>", groupCount: 2).Should().Equal(0);

    [Test]
    // T '\\g<n>' on '(?P<n>a)' -> [1] but T '\\g<N>' !! IndexError 'unknown group'. Upstream's
    // group index is a plain dict, so the lookup is case-sensitive.
    public void A_named_group_reference_is_case_sensitive()
    {
        Dictionary<string, int> groups = new(StringComparer.Ordinal) { ["n"] = 1 };

        Compile(@"\g<n>", 1, groups).Should().Equal(1);

        Action wrongCase = () => Compile(@"\g<N>", 1, groups);
        wrongCase
            .Should()
            .Throw<ArgumentException>("upstream raises IndexError('unknown group'), which is not its error type")
            .WithMessage("unknown group*");
    }

    [Test]
    // Measured with .scratch/s12_repl_errors.py against a subject that cannot match, which is what
    // proves each of these is raised while the template is compiled rather than while it is
    // expanded: the invalid-group-reference family, by contrast, returned the subject unchanged.
    [Arguments(@"\g<a", "missing >", 4)]
    [Arguments(@"\g<", "missing group name", 3)]
    [Arguments(@"\g", "missing <", 2)]
    [Arguments(@"\g<a a>", "bad character in group name", 6)]
    [Arguments(@"\g<1a1>", "bad character in group name", 6)]
    [Arguments(@"\g<-1>", "bad character in group name", 5)]
    [Arguments("\\", "bad escape (end of pattern)", 1)]
    public void A_malformed_group_reference_reports_upstreams_message_and_offset(
        string template,
        string message,
        int offset
    )
    {
        Action compile = () => Compile(template, 1, new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 });

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        using (new AssertionScope())
        {
            error.Message.Should().Be(message);
            error.Offset.Should().Be(offset);
        }
    }

    private static IReadOnlyList<object> Compile(
        string template,
        int groupCount = 0,
        IReadOnlyDictionary<string, int>? groupIndex = null
    ) => RegularExpressions.Parsing.PatternCompiler.CompileReplacement(template, groupCount, groupIndex ?? _noGroups);
}
