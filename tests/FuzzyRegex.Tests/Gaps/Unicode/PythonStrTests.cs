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
