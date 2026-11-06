using System.Globalization;
using System.Text;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Tests.Conventions;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// <see cref="AsciiPropertyAnswers"/>, the committed table of what every property answers for the
/// 128 ASCII codepoints (D141), and the generator that writes its data file,
/// <c>src/FuzzyRegex/Unicode/AsciiPropertyAnswers.g.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator is here, not in <c>tools/</c>, because it must call the library's own predicates
/// (<see cref="Encodings.HasProperty(CaseEncoding, uint, uint)"/> and
/// <see cref="Matcher.HasPropertyIgn"/>), which are internal, and a second implementation of them
/// in a script would be a second thing to keep right. To regenerate after the Unicode tables
/// change, run this class with <c>FUZZYREGEX_REGENERATE=1</c> set:
/// </para>
/// <code>
/// $env:FUZZYREGEX_REGENERATE = '1'
/// dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/AsciiPropertyAnswersTableTests/*"
/// </code>
/// </remarks>
public sealed class AsciiPropertyAnswersTableTests
{
    private const string _regenerateVariable = "FUZZYREGEX_REGENERATE";

    [Test]
    public void The_committed_data_file_is_what_the_predicates_say_now()
    {
        string path = Path.Combine(
            TestTree.RepositoryRoot().FullName,
            "src",
            "FuzzyRegex",
            "Unicode",
            "AsciiPropertyAnswers.g.cs"
        );
        string expected = Render();

        if (string.Equals(Environment.GetEnvironmentVariable(_regenerateVariable), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(path, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        File.ReadAllText(path)
            .ReplaceLineEndings("\n")
            .Should()
            .Be(expected, $"the table is stale: regenerate it by running this class with {_regenerateVariable}=1 set");
    }

    [Test]
    public void Every_property_code_has_a_slot_with_the_predicates_answers_under_both_encodings()
    {
        // Coverage and staleness through the lookup itself, and encoding-independence: the ASCII
        // encoding clamps only codepoints above 127, so it cannot change an ASCII answer.
        int codes = 0;
        List<string> wrong = [];
        Span<ulong> answers = stackalloc ulong[2];

        foreach (uint property in AllPropertyCodes())
        {
            ++codes;
            foreach (bool ignoreCase in new[] { false, true })
            {
                answers.Clear();
                if (!AsciiPropertyAnswers.TryGet(ignoreCase, property, answers))
                {
                    wrong.Add($"0x{property:X8} ignoreCase={ignoreCase}: no slot");
                    continue;
                }

                foreach (CaseEncoding encoding in new[] { CaseEncoding.Unicode, CaseEncoding.Ascii })
                {
                    for (uint ch = 0; ch < 128; ch++)
                    {
                        bool kept = ((answers[(int)(ch >> 6)] >> (int)ch) & 1) != 0;
                        if (kept != Predicate(encoding, ignoreCase, property, ch))
                        {
                            wrong.Add($"0x{property:X8} ignoreCase={ignoreCase} {encoding} U+{ch:X4}");
                        }
                    }
                }
            }
        }

        wrong.Should().BeEmpty();
        codes.Should().Be(1459, "the 101 properties' value sets, measured 2026-11-05 against Unicode 18.0.0");
    }

    [Test]
    [Arguments(0x0065_0000u)] // a property id past the last
    [Arguments(0x0000_FFFFu)] // a value past the last of property 0
    public void A_code_outside_the_table_has_no_slot(uint property)
    {
        Span<ulong> answers = stackalloc ulong[2];

        AsciiPropertyAnswers.TryGet(ignoreCase: false, property, answers).Should().BeFalse();
        AsciiPropertyAnswers.TryGet(ignoreCase: true, property, answers).Should().BeFalse();
    }

    private static bool Predicate(CaseEncoding encoding, bool ignoreCase, uint property, uint ch) =>
        ignoreCase ? Matcher.HasPropertyIgn(encoding, property, ch) : Encodings.HasProperty(encoding, property, ch);

    /// <summary>
    /// The value count of each property id, in id order: the ids run 0..n-1 and each id's values
    /// run 0..count-1, which the table's slot arithmetic relies on, so it is checked here.
    /// </summary>
    private static int[] ValueCounts()
    {
        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();
        int ids = properties.Values.Max(static entry => entry.Id) + 1;
        var values = new SortedSet<int>[ids];
        foreach (PropertyEntry entry in properties.Values)
        {
            (values[entry.Id] ??= []).UnionWith(entry.Values.Values);
        }

        int[] counts = new int[ids];
        for (int id = 0; id < ids; id++)
        {
            SortedSet<int> set =
                values[id] ?? throw new InvalidOperationException($"property id {id} has no entry: ids are not dense");
            if (set.Min != 0 || set.Max != set.Count - 1)
            {
                throw new InvalidOperationException($"property id {id}'s values are not 0..{set.Count - 1}");
            }

            counts[id] = set.Count;
        }

        return counts;
    }

    private static IEnumerable<uint> AllPropertyCodes()
    {
        int[] counts = ValueCounts();
        for (int id = 0; id < counts.Length; id++)
        {
            for (int value = 0; value < counts[id]; value++)
            {
                yield return ((uint)id << 16) | (uint)value;
            }
        }
    }

    /// <summary>The data file's text, from the predicates under the Unicode encoding.</summary>
    private static string Render()
    {
        int[] counts = ValueCounts();
        uint[] codes = [.. AllPropertyCodes()];
        var tables = new List<(ulong Low, ulong High)>();
        var tableIndex = new Dictionary<(ulong Low, ulong High), int>();

        byte[] Index(bool ignoreCase)
        {
            byte[] index = new byte[codes.Length];
            for (int slot = 0; slot < codes.Length; slot++)
            {
                ulong low = 0;
                ulong high = 0;
                for (uint ch = 0; ch < 128; ch++)
                {
                    if (Predicate(CaseEncoding.Unicode, ignoreCase, codes[slot], ch))
                    {
                        if (ch < 64)
                        {
                            low |= 1UL << (int)ch;
                        }
                        else
                        {
                            high |= 1UL << (int)(ch - 64);
                        }
                    }
                }

                if (!tableIndex.TryGetValue((low, high), out int table))
                {
                    table = tables.Count;
                    tableIndex.Add((low, high), table);
                    tables.Add((low, high));
                }

                index[slot] = checked((byte)table);
            }

            return index;
        }

        byte[] plain = Index(ignoreCase: false);
        byte[] ignoreCase = Index(ignoreCase: true);

        int[] firstSlot = new int[counts.Length + 1];
        for (int id = 0; id < counts.Length; id++)
        {
            firstSlot[id + 1] = firstSlot[id] + counts[id];
        }

        var text = new StringBuilder();
        text.Append(
            """
            // <auto-generated/>
            // Written by tests/FuzzyRegex.Tests/Gaps/Unicode/AsciiPropertyAnswersTableTests.cs from the
            // library's own property predicates. Do not edit by hand: regenerate as that file says.
            #nullable enable

            namespace Fuzzy.Text.RegularExpressions.Unicode;

            internal static partial class AsciiPropertyAnswers
            {

            """
        );
        text.Append(
            CultureInfo.InvariantCulture,
            $"""
                /// <summary>How many property ids there are; a packed code's high 16 bits are below this.</summary>
                internal const int PropertyCount = {counts.Length};

                /// <summary>
                /// The slot of each property id's value 0, two bytes each, little-endian; the id's values
                /// take the slots up to the next id's. The last is the total, {codes.Length}.
                /// </summary>

            """
        );
        AppendArray(text, "FirstSlot", firstSlot.SelectMany(static n => LittleEndian((ulong)n, 2)));
        text.Append(
            """

                /// <summary>Each slot's table in <see cref="Tables"/>, for <c>PROPERTY</c>.</summary>

            """
        );
        AppendArray(text, "PlainTableIndex", plain.Select(static n => n.ToString(CultureInfo.InvariantCulture)));
        text.Append(
            """

                /// <summary>Each slot's table in <see cref="Tables"/>, for <c>PROPERTY_IGN</c>.</summary>

            """
        );
        AppendArray(
            text,
            "IgnoreCaseTableIndex",
            ignoreCase.Select(static n => n.ToString(CultureInfo.InvariantCulture))
        );
        text.Append(
            CultureInfo.InvariantCulture,
            $"""

                /// <summary>
                /// The {tables.Count} distinct answer sets, 16 bytes each: two little-endian 64-bit words,
                /// bit <c>ch</c> of the first for <c>ch</c> below 64, bit <c>ch - 64</c> of the second
                /// for the rest.
                /// </summary>

            """
        );
        AppendArray(
            text,
            "Tables",
            tables.SelectMany(static table => LittleEndian(table.Low, 8).Concat(LittleEndian(table.High, 8)))
        );
        text.Append("}\n");
        return text.ToString();
    }

    private static IEnumerable<string> LittleEndian(ulong value, int bytes) =>
        Enumerable
            .Range(0, bytes)
            .Select(i => "0x" + ((value >> (8 * i)) & 0xFF).ToString("X2", CultureInfo.InvariantCulture));

    private static void AppendArray(StringBuilder text, string name, IEnumerable<string> elements, int perLine = 16)
    {
        text.Append(CultureInfo.InvariantCulture, $"    private static ReadOnlySpan<byte> {name} =>\n    [\n");
        foreach (string[] line in elements.Chunk(perLine))
        {
            text.Append("        ").Append(string.Join(", ", line)).Append(",\n");
        }

        text.Append("    ];\n");
    }
}
