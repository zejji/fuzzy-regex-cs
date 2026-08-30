using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// The three CPython <c>str</c> predicates the parser leans on, over every codepoint CPython can
/// answer for.
/// </summary>
/// <remarks>
/// These are not upstream's - they are the host language's, and a port has to rebuild them from
/// some Unicode data. <see cref="PythonStr"/> rebuilds them from upstream's tables, and this is
/// what says the two agree. The 4,803 codepoints Unicode 17.0 added are excluded: the recording
/// host's <c>unicodedata</c> is 16.0.0 and has no answer for them.
/// </remarks>
public sealed class PythonStrTests
{
    [Test]
    public void The_str_predicates_match_cpython_for_every_codepoint_it_knows()
    {
        HashSet<int> newIn17 = [];
        foreach ((int first, int last) in CharacterNameFixture.RangesAddedIn17)
        {
            for (int codepoint = first; codepoint <= last; codepoint++)
            {
                newIn17.Add(codepoint);
            }
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var line = new StringBuilder(16);
        int counted = 0;

        for (int codepoint = 0; codepoint <= 0x10FFFF; codepoint++)
        {
            if (newIn17.Contains(codepoint))
            {
                continue;
            }

            line.Clear();
            line.Append(codepoint.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(PythonStr.IsAlpha(codepoint) ? '1' : '0')
                .Append(PythonStr.IsDigit(codepoint) ? '1' : '0')
                .Append(PythonStr.IsIdentifierStart(codepoint) ? '1' : '0')
                .Append(PythonStr.IsIdentifierContinue(codepoint) ? '1' : '0')
                .Append('\n');
            hash.AppendData(Encoding.UTF8.GetBytes(line.ToString()));
            counted++;
        }

        using (new AssertionScope())
        {
            counted.Should().Be(UnicodeFixture.PythonStrCodepointCount);
            newIn17.Should().HaveCount(UnicodeFixture.PythonStrSkippedNewIn17);
            Convert.ToHexStringLower(hash.GetHashAndReset()).Should().Be(UnicodeFixture.PythonStr);
        }
    }

    /// <summary>
    /// The string-level wrappers, which have to iterate codepoints rather than UTF-16 units:
    /// Python sees one character where we see a surrogate pair.
    /// </summary>
    [Test]
    public void Digit_strings_are_judged_by_codepoint()
    {
        using (new AssertionScope())
        {
            PythonStr.IsDigitString("123").Should().BeTrue();
            // Superscript two: a digit to Python, not to `0 <= c <= 9`.
            PythonStr.IsDigitString("²").Should().BeTrue();
            // ARABIC-INDIC DIGIT ZERO to THREE.
            PythonStr.IsDigitString("٠١٢٣").Should().BeTrue();
            // MATHEMATICAL BOLD DIGIT ZERO, U+1D7CE: one codepoint, two UTF-16 units.
            PythonStr.IsDigitString("\U0001D7CE").Should().BeTrue();
            PythonStr.IsDigitString("").Should().BeFalse();
            PythonStr.IsDigitString("1a").Should().BeFalse();
            // ONE HALF is Numeric_Type=Numeric, not Digit, so Python says no.
            PythonStr.IsDigitString("½").Should().BeFalse();
            // A lone high surrogate.
            PythonStr.IsDigitString("\uD835").Should().BeFalse();
        }
    }

    /// <summary>
    /// <see cref="PythonStr.DecimalValue"/> derives a digit's value from its position in its own
    /// run rather than from a value table, because upstream's tables carry none. This is what says
    /// the derivation is right, for every codepoint .NET has an answer for.
    /// </summary>
    /// <remarks>
    /// .NET's table is the cross-check rather than the source: it is an older Unicode version, so
    /// it returns -1 for the ten codepoints U+11DE0-U+11DE9 that Unicode 17.0 added. Reading it
    /// directly is what the first fix did, and it made the port throw on group names upstream
    /// accepts - the second S11 review pass caught it.
    /// </remarks>
    [Test]
    public void Every_decimal_digits_value_matches_dotnets_table()
    {
        int checked_ = 0;
        int ours = 0;

        using (new AssertionScope())
        {
            for (int codepoint = 0; codepoint <= 0x10FFFF; codepoint++)
            {
                if (codepoint is >= 0xD800 and <= 0xDFFF)
                {
                    continue;
                }

                int expected = CharUnicodeInfo.GetDecimalDigitValue(char.ConvertFromUtf32(codepoint), 0);
                int actual = PythonStr.DecimalValue(codepoint);
                if (actual >= 0)
                {
                    ours++;
                }

                if (expected < 0)
                {
                    // .NET does not know this codepoint is a decimal digit. Either we agree, or it
                    // is one of the ten Unicode 17.0 added, which we deliberately do know about.
                    continue;
                }

                checked_++;
                actual.Should().Be(expected, "U+{0:X4} is decimal digit {1}", codepoint, expected);
            }
        }

        // Guards against the loop silently checking nothing.
        checked_.Should().BeGreaterThan(600);
        ours.Should().Be(checked_ + 10, "the ten Unicode 17.0 digits U+11DE0-U+11DE9 are ours alone");
    }

    /// <summary>
    /// Python's <c>int()</c>, which is not <c>str.isdigit</c>: it takes only
    /// <c>Numeric_Type=Decimal</c>, so <c>int("²")</c> raises where <c>"²".isdigit()</c> is true.
    /// </summary>
    /// <remarks>
    /// The last five cases are <b>narrower</b> than Python, which accepts a sign, surrounding
    /// whitespace and underscores between digits. None can reach here: upstream calls <c>int()</c>
    /// on a group name, and a name is either <c>get_while(DIGITS)</c> - ASCII digits only - or a
    /// string <c>str.isdigit</c> has already accepted, which none of those five is. Pinned so that
    /// the narrowing is a decision rather than an accident.
    /// </remarks>
    [Test]
    public void Int_accepts_decimal_digits_and_nothing_else()
    {
        using (new AssertionScope())
        {
            Parse("123").Should().Be("123");
            // ARABIC-INDIC DIGIT ONE, TWO.
            Parse("١٢").Should().Be("12");
            // MATHEMATICAL BOLD DIGIT ZERO to TWO: one codepoint each, two UTF-16 units each.
            Parse("\U0001D7CE\U0001D7CF\U0001D7D0").Should().Be("12");
            Parse("007").Should().Be("7");
            Parse("99999999999999999999").Should().Be("99999999999999999999");
            // TOLONG SIKI DIGIT ONE, added in Unicode 17.0 and unknown to .NET's table.
            Parse("\U00011DE1").Should().Be("1");

            // Superscript two is a digit to str.isdigit and not to int().
            Parse("²").Should().BeNull();
            // CIRCLED DIGIT ONE, Numeric_Type=Digit.
            Parse("①").Should().BeNull();
            Parse("1²").Should().BeNull();
            Parse("").Should().BeNull();
            Parse("1a").Should().BeNull();
            Parse("-1").Should().BeNull();
            Parse("+1").Should().BeNull();
            Parse(" 1").Should().BeNull();
            Parse("1_0").Should().BeNull();
            Parse("\uD835").Should().BeNull();
        }
    }

    /// <summary>The parsed value rendered in ASCII digits, or null where Python's int() raises.</summary>
    /// <remarks>A string, not the value: <c>Should().Be(123)</c> on a boxed
    /// <see cref="System.Numerics.BigInteger"/> compares it against a boxed <c>int</c> and fails
    /// with "Expected 123 but found 123".</remarks>
    private static string? Parse(string text) =>
        PythonStr.TryParseInt(text, out System.Numerics.BigInteger value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : null;

    [Test]
    public void Identifiers_are_judged_by_codepoint()
    {
        using (new AssertionScope())
        {
            PythonStr.IsIdentifier("name").Should().BeTrue();
            PythonStr.IsIdentifier("_name1").Should().BeTrue();
            PythonStr.IsIdentifier("été").Should().BeTrue();
            // GREEK SMALL LETTER ALPHA, then a combining mark, which is XID_Continue.
            PythonStr.IsIdentifier("ά").Should().BeTrue();
            PythonStr.IsIdentifier("").Should().BeFalse();
            PythonStr.IsIdentifier("1name").Should().BeFalse();
            PythonStr.IsIdentifier("na-me").Should().BeFalse();
            // A combining mark cannot start one.
            PythonStr.IsIdentifier("́a").Should().BeFalse();
            PythonStr.IsIdentifier("\uD835").Should().BeFalse();
        }
    }
}
