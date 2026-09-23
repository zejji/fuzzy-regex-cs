using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>Matcher.SameCharIgn</c>'s ASCII fast path against upstream's <c>same_char_ign</c>
/// (<c>upstream/src/_regex.c</c> line 2849), which enumerates every case of the first character.
/// </summary>
/// <remarks>
/// The reference below is that enumeration, written out over <c>Encodings.AllCases</c>, whose
/// output <see cref="Unicode.UnicodeCasingTests"/> checks against upstream for every codepoint.
/// The fast path must agree with it on every pair of ASCII characters, under every encoding the
/// flags can select.
/// </remarks>
public sealed class SameCharIgnTests
{
    /// <summary>
    /// Every pair of ASCII characters, under every combination of the three encoding flags.
    /// </summary>
    /// <remarks>
    /// The Unicode encoding gives <c>k</c> and <c>s</c> a third case each (KELVIN SIGN U+212A and
    /// LONG S U+017F) and pairs <c>I</c> with <c>i</c> through <c>TurkicDefaults</c>. Neither third
    /// case is ASCII, so on an ASCII pair both encodings reduce to "equal, or the same letter in
    /// the other case". LOCALE casing is not supported, and the fast path must still refuse it.
    /// </remarks>
    [Test]
    public void The_ascii_fast_path_agrees_with_the_full_case_enumeration_on_every_ascii_pair()
    {
        int[] encodingFlags = [RegexFlags.Ascii, RegexFlags.Locale, RegexFlags.Unicode];
        List<string> mismatches = [];

        for (int mask = 0; mask < 1 << encodingFlags.Length; mask++)
        {
            int flags = 0;
            for (int bit = 0; bit < encodingFlags.Length; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    flags |= encodingFlags[bit];
                }
            }

            CaseEncoding encoding = Encodings.Select(flags);

            for (uint ch1 = 0; ch1 < 0x80; ch1++)
            {
                for (uint ch2 = 0; ch2 < 0x80; ch2++)
                {
                    string expected = Describe(() => ReferenceSameCharIgn(encoding, ch1, ch2));
                    string actual = Describe(() => Matcher.SameCharIgn(encoding, ch1, ch2));

                    if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    {
                        mismatches.Add($"flags 0x{flags:X} U+{ch1:X4} U+{ch2:X4}: {actual}, want {expected}");
                    }
                }
            }
        }

        mismatches.Should().BeEmpty();
    }

    /// <summary>
    /// A non-ASCII character on either side still takes the full enumeration: KELVIN SIGN and
    /// LONG S match their ASCII letters under the Unicode encoding and not under ASCII.
    /// </summary>
    /// <param name="ch1">The first character.</param>
    /// <param name="ch2">The second.</param>
    /// <param name="unicode">The answer under the Unicode encoding.</param>
    [Test]
    // regex 2026.9.10: regex.match('(?i)k', 'K').span() == (0, 1); '(?ai)k' is None
    [Arguments('k', 0x212A, true)]
    // regex 2026.9.10: regex.match('(?i)K', 'K').span() == (0, 1); '(?ai)K' is None
    [Arguments(0x212A, 'K', true)]
    // regex 2026.9.10: regex.match('(?i)s', 'ſ').span() == (0, 1); '(?ai)s' is None
    [Arguments('s', 0x017F, true)]
    // regex 2026.9.10: regex.match('(?i)ſ', 'S').span() == (0, 1); '(?ai)ſ' is None
    [Arguments(0x017F, 'S', true)]
    // Deliberate: upstream regex.match('(?i)i', 'İ').span() == (0, 1), but this port keeps
    // U+0130 alone in its case set - docs/DIVERGENCES.md, "The Turkic I pairings are not applied".
    [Arguments('i', 0x0130, false)]
    public void A_non_ascii_character_takes_the_full_case_enumeration(int ch1, int ch2, bool unicode)
    {
        Matcher.SameCharIgn(CaseEncoding.Unicode, (uint)ch1, (uint)ch2).Should().Be(unicode);
        Matcher.SameCharIgn(CaseEncoding.Ascii, (uint)ch1, (uint)ch2).Should().BeFalse();
    }

    /// <summary>Upstream's <c>same_char_ign</c>, with no fast path.</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch1">One codepoint.</param>
    /// <param name="ch2">The other.</param>
    /// <returns><see langword="true"/> if they are the same, ignoring case.</returns>
    private static bool ReferenceSameCharIgn(CaseEncoding encoding, uint ch1, uint ch2)
    {
        if (ch1 == ch2)
        {
            return true;
        }

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        int count = Encodings.AllCases(encoding, ch1, cases);

        for (int i = 1; i < count; i++)
        {
            if (cases[i] == ch2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>An answer, or the exception type that replaced it, as one comparable string.</summary>
    /// <param name="call">The comparison.</param>
    /// <returns><c>True</c>, <c>False</c>, or the exception's type name.</returns>
    private static string Describe(Func<bool> call)
    {
        try
        {
            return call().ToString();
        }
        catch (NotSupportedException exception)
        {
            return exception.GetType().Name;
        }
    }
}
