using AwesomeAssertions;
using AwesomeAssertions.Execution;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// <see cref="FuzzyRegex.Escape(string, bool, bool)"/> outside the eight assertions upstream's own
/// suite makes: whitespace that is whitespace to Python and not to .NET, characters above the BMP,
/// and a lone surrogate.
/// </summary>
/// <remarks>
/// <para>
/// The ported tests cover ASCII only. The interesting cases are all about <i>what counts as one
/// character</i>: upstream iterates a Python <c>str</c>, so a codepoint above U+FFFF takes one
/// backslash where a naive walk over UTF-16 code units would emit two, and
/// <c>str.isspace()</c> is a wider set than <see cref="char.IsWhiteSpace(char)"/>.
/// </para>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-08-31 with
/// <c>.scratch/s12_edges.py</c>, and the two tables it printed are pinned below:
/// <c>_METACHARS</c> is <c>#$&amp;()*+-.?[\]^{|}~</c> and <c>ALNUM</c> is ASCII letters and digits.
/// </para>
/// </remarks>
public sealed class EscapeTests
{
    [Test]
    // E '()[]{}?*+|^$\\.-#&~' -> every one escaped, under both values of special_only.
    [Arguments(true)]
    [Arguments(false)]
    public void Every_metacharacter_is_escaped(bool specialOnly) =>
        FuzzyRegex.Escape(@"()[]{}?*+|^$\.-#&~", specialOnly).Should().Be(@"\(\)\[\]\{\}\?\*\+\|\^\$\\\.\-\#\&\~");

    [Test]
    // E 'a\xa0b'   -> 'a\\\xa0b'    (NBSP: str.isspace() is True)
    // E 'a\x1cb'   -> 'a\\\x1cb'    (FILE SEPARATOR: isspace() in Python, not char.IsWhiteSpace)
    // E 'a\u2028b' -> 'a\\\u2028b'  (LINE SEPARATOR)
    // E 'a\tb\nc'  -> 'a\\\tb\\\nc'
    [Arguments(0x00A0)]
    [Arguments(0x001C)]
    [Arguments(0x2028)]
    [Arguments('\t')]
    [Arguments('\n')]
    public void Every_character_Python_calls_whitespace_is_escaped(int codepoint)
    {
        string subject = "a" + (char)codepoint + "b";

        using (new AssertionScope())
        {
            FuzzyRegex.Escape(subject, specialOnly: true).Should().Be(@"a\" + (char)codepoint + "b");
            FuzzyRegex.Escape(subject, specialOnly: false).Should().Be(@"a\" + (char)codepoint + "b");
        }
    }

    [Test]
    // literal_spaces only exempts U+0020, not whitespace in general: 'a\tb' keeps its backslash.
    public void Literal_spaces_exempts_the_space_and_nothing_else()
    {
        using (new AssertionScope())
        {
            FuzzyRegex.Escape("a b", literalSpaces: true).Should().Be("a b");
            FuzzyRegex.Escape("a\tb", literalSpaces: true).Should().Be("a\\\tb");
        }
    }

    [Test]
    // E '\U0001f600' special_only=True -> '\U0001f600', special_only=False -> '\\\U0001f600'.
    // One backslash for the whole codepoint: escaping each half of the surrogate pair separately
    // would give two, and the result would no longer be upstream's string.
    public void An_astral_character_is_one_character()
    {
        string emoji = char.ConvertFromUtf32(0x1F600);

        using (new AssertionScope())
        {
            FuzzyRegex.Escape("a" + emoji + "b", specialOnly: true).Should().Be("a" + emoji + "b");
            FuzzyRegex.Escape("a" + emoji + "b", specialOnly: false).Should().Be("a\\" + emoji + "b");
        }
    }

    [Test]
    // E 'a\ud800b' special_only=True -> 'a\ud800b', special_only=False -> 'a\\\ud800b'. A lone
    // surrogate is a legal element of a Python str and a legal char here, and it takes exactly one
    // backslash - which is why the walk is written out rather than using EnumerateRunes, whose
    // replacement of a lone surrogate with U+FFFD would corrupt the output.
    public void A_lone_surrogate_survives_and_takes_one_backslash()
    {
        string subject = "a" + (char)0xD800 + "b";

        using (new AssertionScope())
        {
            FuzzyRegex.Escape(subject, specialOnly: true).Should().Be(subject);
            FuzzyRegex.Escape(subject, specialOnly: false).Should().Be("a\\" + (char)0xD800 + "b");
        }
    }

    [Test]
    // E '_' and E '\xe9': ALNUM is ASCII letters and digits only, so both are escaped when
    // special_only is off - the underscore in particular, which \w would match.
    [Arguments('_')]
    [Arguments(0x00E9)]
    public void Anything_outside_ASCII_letters_and_digits_is_escaped_when_special_only_is_off(int codepoint)
    {
        string subject = ((char)codepoint).ToString();

        using (new AssertionScope())
        {
            FuzzyRegex.Escape(subject, specialOnly: true).Should().Be(subject);
            FuzzyRegex.Escape(subject, specialOnly: false).Should().Be("\\" + subject);
        }
    }

    [Test]
    public void A_null_input_is_rejected()
    {
        Action escape = static () => FuzzyRegex.Escape(null!);

        escape.Should().Throw<ArgumentNullException>().WithParameterName("input");
    }
}
