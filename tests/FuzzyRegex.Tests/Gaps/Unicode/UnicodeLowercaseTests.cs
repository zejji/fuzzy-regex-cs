using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// The lowercase table <c>Sequence._fix_full_casefold</c> needs, and the lookup over it.
/// </summary>
/// <remarks>
/// <c>tools/build-lowercase.py</c> proves the table itself against the host CPython's own
/// <c>str.lower()</c> - an implementation independent of the UCD files it reads - for all
/// 1,109,309 codepoints that host's Unicode version knows. What is left for these tests is what
/// Python cannot check: that the table reached C# intact, that the binary search over it is
/// sorted, and that <see cref="PythonStr"/> reads it the way upstream reads Python's.
/// </remarks>
public sealed class UnicodeLowercaseTests
{
    [Test]
    public void The_table_reached_csharp_intact()
    {
        var text = new StringBuilder();
        var rows = new List<(int From, int[] To)>();
        for (int i = 0; i < UnicodeLowercase.From.Length; i++)
        {
            rows.Add((UnicodeLowercase.From[i], [UnicodeLowercase.To[i]]));
        }

        rows.AddRange(UnicodeLowercase.Expanding);
        rows.Sort((a, b) => a.From.CompareTo(b.From));

        foreach ((int from, int[] to) in rows)
        {
            text.Append(from.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .AppendJoin(',', to.Select(c => c.ToString(CultureInfo.InvariantCulture)))
                .Append('\n');
        }

        using (new AssertionScope())
        {
            rows.Should().HaveCount(LowercaseFixture.MappingCount);
            UnicodeLowercase.Expanding.Should().HaveCount(LowercaseFixture.ExpandingCount);
            UnicodeLowercase.To.Should().HaveCount(UnicodeLowercase.From.Length);
            UnicodeLowercase.Version.Should().Be(LowercaseFixture.UnicodeVersion);
            Convert
                .ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))
                .Should()
                .Be(LowercaseFixture.Mappings);
        }
    }

    /// <summary>
    /// The lookup is a binary search, which silently returns the wrong answer - or none - if the
    /// table is not sorted.
    /// </summary>
    [Test]
    public void The_table_is_sorted_ascending_and_unique() =>
        UnicodeLowercase.From.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

    /// <summary>
    /// The three groups of codepoints where lower-casing folded text is not the identity, which is
    /// the whole reason the table exists. Measured against CPython 3.14.6 on 2026-08-30:
    /// <c>chr(0x49).lower()</c> is <c>'i'</c>, <c>chr(0x130).lower()</c> is two codepoints
    /// <c>U+0069 U+0307</c>, and the 86 upper-case Cherokee letters lower-case into the U+AB70
    /// block.
    /// </summary>
    [Test]
    [Arguments(new[] { 0x49 }, new[] { 0x69 })]
    [Arguments(new[] { 0x130 }, new[] { 0x69, 0x307 })]
    [Arguments(new[] { 0x13A0 }, new[] { 0xAB70 })]
    [Arguments(new[] { 0x13F5 }, new[] { 0x13FD })]
    [Arguments(new[] { 0x41, 0x42 }, new[] { 0x61, 0x62 })]
    // Already lower-case, and case-less, codepoints come back unchanged.
    [Arguments(new[] { 0x69, 0x20, 0x3C3 }, new[] { 0x69, 0x20, 0x3C3 })]
    // A supplementary-plane letter: Deseret capital long I lower-cases to U+10428.
    [Arguments(new[] { 0x10400 }, new[] { 0x10428 })]
    public void Lower_matches_pythons_str_lower(int[] input, int[] expected) =>
        PythonStr.Lower(input).Should().Equal(expected);

    /// <summary>
    /// The only reason a per-codepoint lower-casing is enough: CPython's one context-sensitive
    /// rule, final sigma, fires on U+03A3, and nothing folds to U+03A3 - so folded text, which is
    /// the only text this is applied to, can never contain one. Measured over all 1,114,112
    /// codepoints on 2026-08-30.
    /// </summary>
    [Test]
    public void No_codepoint_folds_to_a_capital_sigma()
    {
        for (int codepoint = 0; codepoint <= 0x10FFFF; codepoint++)
        {
            if (codepoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            int[] folded = RegexModule.FoldCase(RegularExpressions.Parsing.RegexFlags.FullCaseFolding, [codepoint]);

            if (Array.IndexOf(folded, 0x3A3) >= 0)
            {
                Assert.Fail($"U+{codepoint:X4} folds to a string containing U+03A3");
            }
        }
    }
}

/// <summary>The lowercase fixture, recorded by <c>tools/build-lowercase.py</c>.</summary>
internal static class LowercaseFixture
{
    private static readonly string _resourceName = typeof(LowercaseFixture).Namespace + ".lowercase.json";

    private static readonly Lazy<JsonDocument> _loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string UnicodeVersion => Root.GetProperty("unicodeVersion").GetString()!;

    public static int MappingCount => Root.GetProperty("mappingCount").GetInt32();

    public static int ExpandingCount => Root.GetProperty("expandingCount").GetInt32();

    public static string Mappings => Root.GetProperty("mappings").GetString()!;

    private static JsonElement Root => _loaded.Value.RootElement;

    private static JsonDocument Load()
    {
        using Stream stream =
            typeof(LowercaseFixture).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"the lowercase fixture is not embedded as '{_resourceName}'");

        return JsonDocument.Parse(stream);
    }
}
