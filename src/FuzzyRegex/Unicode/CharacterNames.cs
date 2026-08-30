namespace Fuzzy.Text.RegularExpressions.Unicode;

/// <summary>
/// Python's <c>unicodedata.lookup</c>, which upstream's <c>parse_named_char</c>
/// (<c>upstream/regex/_regex_core.py</c> line 1437) uses to resolve <c>\N{...}</c>.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no character-name API, so the table is generated from the Unicode 17.0.0 UCD by
/// <c>tools/build-character-names.py</c> - the one place this port needs a UCD file rather than
/// something upstream already generated.
/// </para>
/// <para>
/// The contract was measured against CPython 3.14.6 on 2026-08-30, not inferred: the lookup is
/// case-insensitive (<c>lookup("latin small letter a")</c> resolves), but it normalises nothing
/// else - <c>LATIN_SMALL_LETTER_A</c>, <c>LATIN-SMALL-LETTER-A</c>, <c>LATINSMALLLETTERA</c> and
/// <c>" LATIN SMALL LETTER A "</c> all raise. Name aliases resolve (<c>NUL</c> is U+0000,
/// <c>LATIN CAPITAL LETTER GHA</c> is U+01A2), a real name beating an alias for the same string.
/// The algorithmic names take the codepoint in upper-case hexadecimal with no leading zeros:
/// <c>CJK UNIFIED IDEOGRAPH-4E00</c> resolves and <c>CJK UNIFIED IDEOGRAPH-04E00</c> does not.
/// </para>
/// <para>
/// <b>Named sequences are not supported.</b> <c>unicodedata.lookup("KEYCAP DIGIT ZERO")</c>
/// returns three codepoints (measured 2026-08-30), and upstream immediately calls <c>ord()</c>
/// on the result, which raises a <c>TypeError</c> rather than compiling - so the only difference
/// a caller can see is which error they get. See <c>docs/PORTMAP.md</c>.
/// </para>
/// <para>
/// <b>Deliberate corner cut, S09.</b> The names are stored whole, as 40,951 string literals -
/// 1,044,804 characters, which is 2.1 MB of the shipped assembly's 3.5 MB. <b>Ceiling:</b> package
/// size, nothing functional. <b>Upgrade path:</b> the names are built from a vocabulary of a few
/// thousand words, so storing word indices and tokenising the query the same way - which is what
/// CPython does - would cut it to a few hundred kilobytes. Left until there is a reason: it is
/// more code, more ways to be wrong, and phase 7 is where size and speed get measured.
/// </para>
/// </remarks>
internal static partial class UnicodeCharacterNames
{
    /// <summary>Resolves a character name to its codepoint.</summary>
    /// <param name="name">The name, in any case.</param>
    /// <param name="codepoint">The codepoint, when the name is known.</param>
    /// <returns><see langword="true"/> if the name is known.</returns>
    internal static bool TryLookup(string name, out int codepoint)
    {
        ArgumentNullException.ThrowIfNull(name);

        codepoint = -1;
        if (name.Length == 0)
        {
            return false;
        }

        string upper = ToUpperAscii(name);

        // The stored names first. Nothing in the table starts with an algorithmic prefix
        // (asserted by the generator), so the two cannot disagree.
        int index = Array.BinarySearch(Names, upper, StringComparer.Ordinal);
        if (index >= 0)
        {
            codepoint = Codepoints[index];
            return true;
        }

        return TryLookupAlgorithmic(upper, out codepoint);
    }

    /// <summary>
    /// C's <c>toupper</c> in the C locale, which is what CPython's name comparison uses: ASCII
    /// and nothing else. Every stored name is upper-case ASCII, so a name carrying anything else
    /// simply fails to match - which is the answer CPython gives too.
    /// </summary>
    private static string ToUpperAscii(string name)
    {
        return string.Create(
            name.Length,
            name,
            static (destination, source) =>
            {
                for (int i = 0; i < source.Length; i++)
                {
                    char c = source[i];
                    destination[i] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
                }
            }
        );
    }

    private static bool TryLookupAlgorithmic(string upper, out int codepoint)
    {
        codepoint = -1;

        foreach ((string prefix, int first, int last) in AlgorithmicRanges)
        {
            if (!upper.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            ReadOnlySpan<char> suffix = upper.AsSpan(prefix.Length);

            if (string.Equals(prefix, HangulPrefix, StringComparison.Ordinal))
            {
                if (TryDecomposeHangul(suffix, out codepoint) && first <= codepoint && codepoint <= last)
                {
                    return true;
                }

                continue;
            }

            // Upper-case hexadecimal with no leading zeros and at least four digits, which is
            // what "%04X" produces - so parsing and re-formatting is the exact test.
            if (suffix.Length is < 4 or > 6)
            {
                continue;
            }

            int value = 0;
            bool digits = true;
            foreach (char c in suffix)
            {
                int digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'A' and <= 'F' => c - 'A' + 10,
                    _ => -1,
                };

                if (digit < 0)
                {
                    digits = false;
                    break;
                }

                value = (value << 4) | digit;
            }

            if (!digits || value < first || value > last || suffix.Length != HexLength(value))
            {
                continue;
            }

            codepoint = value;
            return true;
        }

        return false;
    }

    /// <summary>How many digits <c>"%04X"</c> would give this codepoint.</summary>
    private static int HexLength(int value)
    {
        if (value <= 0xFFFF)
        {
            return 4;
        }

        return value <= 0xFFFFF ? 5 : 6;
    }

    private static bool TryDecomposeHangul(ReadOnlySpan<char> jamo, out int codepoint)
    {
        codepoint = -1;

        // Syllable names are distinct - the generator proves it by resolving all 11,172 through
        // the host's unicodedata - so at most one (leading, vowel, trailing) triple can spell any
        // given name, and the first match found is the only match.
        for (int leading = 0; leading < JamoLeading.Length; leading++)
        {
            if (!jamo.StartsWith(JamoLeading[leading], StringComparison.Ordinal))
            {
                continue;
            }

            ReadOnlySpan<char> afterLeading = jamo[JamoLeading[leading].Length..];
            for (int vowel = 0; vowel < JamoVowel.Length; vowel++)
            {
                if (!afterLeading.StartsWith(JamoVowel[vowel], StringComparison.Ordinal))
                {
                    continue;
                }

                ReadOnlySpan<char> afterVowel = afterLeading[JamoVowel[vowel].Length..];
                for (int trailing = 0; trailing < JamoTrailing.Length; trailing++)
                {
                    if (!afterVowel.Equals(JamoTrailing[trailing], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    codepoint = HangulFirst + (((leading * JamoVowel.Length) + vowel) * JamoTrailing.Length) + trailing;
                    return true;
                }
            }
        }

        return false;
    }
}
