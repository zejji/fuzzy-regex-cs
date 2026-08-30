using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// The <c>\N{...}</c> name table and the lookup over it.
/// </summary>
/// <remarks>
/// <c>tools/build-character-names.py</c> proves the table itself, against the host CPython's own
/// <c>unicodedata</c> - an implementation independent of the UCD files it reads - for all 148,853
/// names that CPython knows. What is left for these tests is everything Python cannot check: that
/// the table reached C# intact, and that the reverse of each algorithmic range is right.
/// </remarks>
public sealed class UnicodeCharacterNameTests
{
    [Test]
    public void The_stored_table_reached_csharp_intact()
    {
        var text = new StringBuilder();
        for (int i = 0; i < UnicodeCharacterNames.Names.Length; i++)
        {
            text.Append(UnicodeCharacterNames.Names[i])
                .Append(':')
                .Append(UnicodeCharacterNames.Codepoints[i].ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        using (new AssertionScope())
        {
            UnicodeCharacterNames.Names.Should().HaveCount(CharacterNameFixture.StoredNameCount);
            UnicodeCharacterNames.Codepoints.Should().HaveCount(CharacterNameFixture.StoredNameCount);
            UnicodeCharacterNames.AlgorithmicRanges.Should().HaveCount(CharacterNameFixture.AlgorithmicRangeCount);
            UnicodeCharacterNames.Version.Should().Be(CharacterNameFixture.UnicodeVersion);
            Convert
                .ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
                .Should()
                .Be(CharacterNameFixture.StoredNames);
        }
    }

    /// <summary>
    /// The lookup is a binary search, which silently returns the wrong answer - or none - if the
    /// table is not sorted the way the comparer sorts.
    /// </summary>
    [Test]
    public void The_stored_names_are_sorted_ordinally_and_unique()
    {
        UnicodeCharacterNames.Names.Should().BeInAscendingOrder(StringComparer.Ordinal).And.OnlyHaveUniqueItems();
    }

    [Test]
    public void Every_stored_name_resolves_to_its_own_codepoint()
    {
        using (new AssertionScope())
        {
            for (int i = 0; i < UnicodeCharacterNames.Names.Length; i++)
            {
                string name = UnicodeCharacterNames.Names[i];
                UnicodeCharacterNames.TryLookup(name, out int codepoint).Should().BeTrue("{0} is in the table", name);
                codepoint.Should().Be(UnicodeCharacterNames.Codepoints[i], "{0}", name);
            }
        }
    }

    /// <summary>
    /// Every codepoint in every algorithmic range, in both directions. The forward direction is
    /// checked in Python against the host's <c>unicodedata</c>; this is the reverse, which only
    /// exists in C# - the Hangul decomposition in particular.
    /// </summary>
    [Test]
    public void Every_algorithmic_name_resolves_to_its_own_codepoint()
    {
        int checkedNames = 0;

        using (new AssertionScope())
        {
            foreach ((string prefix, int first, int last) in UnicodeCharacterNames.AlgorithmicRanges)
            {
                for (int codepoint = first; codepoint <= last; codepoint++)
                {
                    string name = prefix + Name(prefix, codepoint);
                    UnicodeCharacterNames.TryLookup(name, out int resolved).Should().BeTrue("{0}", name);
                    resolved.Should().Be(codepoint, "{0}", name);
                    checkedNames++;
                }
            }
        }

        // A prefix table that had gone empty would make the loop above vacuously pass.
        checkedNames.Should().BeGreaterThan(100_000);
    }

    /// <summary>
    /// Every codepoint Unicode 17.0 added has a name here, which is the check the host CPython
    /// cannot make: its <c>unicodedata</c> is 16.0.0.
    /// </summary>
    [Test]
    public void Every_codepoint_added_in_unicode_17_can_be_named()
    {
        HashSet<int> stored = [.. UnicodeCharacterNames.Codepoints];
        int nameable = 0;

        foreach ((int first, int last) in CharacterNameFixture.RangesAddedIn17)
        {
            for (int codepoint = first; codepoint <= last; codepoint++)
            {
                bool algorithmic = UnicodeCharacterNames.AlgorithmicRanges.Any(range =>
                    range.First <= codepoint && codepoint <= range.Last
                );

                if (stored.Contains(codepoint) || algorithmic)
                {
                    nameable++;
                }
            }
        }

        nameable.Should().Be(CharacterNameFixture.CodepointsAddedIn17);
    }

    /// <summary>
    /// The contract, spelled out. Every expectation here was measured against CPython 3.14.6's
    /// <c>unicodedata.lookup</c> on 2026-08-30, not inferred.
    /// </summary>
    [Test]
    [Arguments("LATIN SMALL LETTER A", 0x61)]
    [Arguments("latin small letter a", 0x61)]
    [Arguments("Latin Small Letter A", 0x61)]
    [Arguments("LATIN SMALL LETTER A WITH ACUTE", 0xE1)]
    // Name aliases: NULL is a control alias, NUL an abbreviation, GHA a correction.
    [Arguments("NULL", 0x00)]
    [Arguments("NUL", 0x00)]
    [Arguments("LATIN CAPITAL LETTER GHA", 0x1A2)]
    // A real name beats an alias for the same string: BELL is U+1F514, not U+0007.
    [Arguments("BELL", 0x1F514)]
    [Arguments("CJK UNIFIED IDEOGRAPH-4E00", 0x4E00)]
    [Arguments("cjk unified ideograph-4e00", 0x4E00)]
    [Arguments("CJK UNIFIED IDEOGRAPH-9FFF", 0x9FFF)]
    [Arguments("TANGUT IDEOGRAPH-17000", 0x17000)]
    [Arguments("HANGUL SYLLABLE GA", 0xAC00)]
    [Arguments("HANGUL SYLLABLE ga", 0xAC00)]
    [Arguments("HANGUL SYLLABLE PWILH", 0xD4DB)]
    [Arguments("HANGUL SYLLABLE SSA", 0xC2F8)]
    // Not range-defined in UnicodeData.txt, so these are stored names, not computed ones.
    [Arguments("NUSHU CHARACTER-1B170", 0x1B170)]
    [Arguments("KHITAN SMALL SCRIPT CHARACTER-18B00", 0x18B00)]
    [Arguments("EGYPTIAN HIEROGLYPH-13460", 0x13460)]
    public void Known_names_resolve(string name, int expected)
    {
        UnicodeCharacterNames.TryLookup(name, out int codepoint).Should().BeTrue();
        codepoint.Should().Be(expected);
    }

    /// <summary>
    /// The lookup normalises case and nothing else: underscores, hyphens, run-together words and
    /// surrounding spaces are all rejected by CPython, so they have to be rejected here.
    /// </summary>
    [Test]
    [Arguments("LATIN_SMALL_LETTER_A")]
    [Arguments("LATIN-SMALL-LETTER-A")]
    [Arguments("LATINSMALLLETTERA")]
    [Arguments(" LATIN SMALL LETTER A ")]
    [Arguments("NOT A REAL NAME")]
    [Arguments("")]
    // No leading zeros in an algorithmic name, and the range is closed.
    [Arguments("CJK UNIFIED IDEOGRAPH-04E00")]
    [Arguments("CJK UNIFIED IDEOGRAPH-004E00")]
    [Arguments("CJK UNIFIED IDEOGRAPH-4E0")]
    [Arguments("CJK UNIFIED IDEOGRAPH-A000")]
    [Arguments("HANGUL SYLLABLE ")]
    [Arguments("HANGUL SYLLABLE XX")]
    [Arguments("HANGUL SYLLABLE SSS")]
    // A named sequence: CPython resolves this to three codepoints, and upstream then fails on
    // ord(). We do not carry named sequences at all - see docs/PORTMAP.md.
    [Arguments("KEYCAP DIGIT ZERO")]
    public void Unknown_names_do_not_resolve(string name)
    {
        UnicodeCharacterNames.TryLookup(name, out int codepoint).Should().BeFalse();
        codepoint.Should().Be(-1);
    }

    private static string Name(string prefix, int codepoint) =>
        string.Equals(prefix, UnicodeCharacterNames.HangulPrefix, StringComparison.Ordinal)
            ? HangulJamo(codepoint)
            : codepoint.ToString("X4", CultureInfo.InvariantCulture);

    private static string HangulJamo(int codepoint)
    {
        int index = codepoint - UnicodeCharacterNames.HangulFirst;
        return UnicodeCharacterNames.JamoLeading[index / 588]
            + UnicodeCharacterNames.JamoVowel[index % 588 / 28]
            + UnicodeCharacterNames.JamoTrailing[index % 28];
    }
}

/// <summary>What <c>tools/build-character-names.py</c> recorded about the table it wrote.</summary>
internal static class CharacterNameFixture
{
    private static readonly string _resourceName = typeof(CharacterNameFixture).Namespace + ".character-names.json";

    private static readonly Lazy<JsonDocument> _loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string UnicodeVersion => Root.GetProperty("unicodeVersion").GetString()!;

    public static int StoredNameCount => Root.GetProperty("storedNameCount").GetInt32();

    public static string StoredNames => Root.GetProperty("storedNames").GetString()!;

    public static int AlgorithmicRangeCount => Root.GetProperty("algorithmicRangeCount").GetInt32();

    public static int CodepointsAddedIn17 => Root.GetProperty("codepointsAddedIn17").GetInt32();

    public static IEnumerable<(int First, int Last)> RangesAddedIn17 =>
        Root.GetProperty("codepointRangesAddedIn17")
            .EnumerateArray()
            .Select(range => (range[0].GetInt32(), range[1].GetInt32()));

    private static JsonElement Root => _loaded.Value.RootElement;

    private static JsonDocument Load()
    {
        using Stream stream =
            typeof(CharacterNameFixture).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"the character-name fixture is not embedded as '{_resourceName}'");

        return JsonDocument.Parse(stream);
    }
}
