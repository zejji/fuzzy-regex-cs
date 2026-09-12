using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// Digests of upstream's Unicode tables and of its C extension's casing and property answers,
/// recorded by <c>tools/record-unicode-fixtures.py</c>.
/// </summary>
/// <remarks>
/// The tables are transliterated, not written, so the risk they carry is not "did someone make a
/// mistake in this function" but "did the transliterator drop, truncate or transpose a table". A
/// digest per table catches all three, and an exhaustive digest over every codepoint catches the
/// rest. Narrow any mismatch by plane and then by block in a throwaway script; a hash says only
/// that something is wrong.
/// </remarks>
internal static class UnicodeFixture
{
    // Derived from the namespace so a folder rename cannot leave a stale literal, as Corpus does.
    private static readonly string _resourceName = typeof(UnicodeFixture).Namespace + ".unicode-fixtures.json";

    private static readonly Lazy<FixtureFile> _loaded = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The <c>regex</c> version the fixture was recorded against.</summary>
    public static string RegexVersion => _loaded.Value.RegexVersion;

    /// <summary>The Unicode version upstream's tables declare.</summary>
    public static string UnicodeVersion => _loaded.Value.UnicodeVersion;

    /// <summary>The Unicode version the recording host's <c>unicodedata</c> reported.</summary>
    public static string PythonUnicodeVersion => _loaded.Value.PythonUnicodeVersion;

    /// <summary>Table name in the C source to its length and digest.</summary>
    public static IReadOnlyDictionary<string, TableDigest> Tables => _loaded.Value.Tables;

    /// <summary>Casing digest label - see the generator - to its SHA-256.</summary>
    public static IReadOnlyDictionary<string, string> Casing => _loaded.Value.Casing;

    /// <summary>The number of properties <c>get_properties</c> returned.</summary>
    public static int PropertyCount => _loaded.Value.PropertyCount;

    /// <summary>The number of (property, value) pairs across every property.</summary>
    public static int PropertyValuePairCount => _loaded.Value.PropertyValuePairCount;

    /// <summary>The digest of the whole property dictionary.</summary>
    public static string Properties => _loaded.Value.Properties;

    /// <summary>The codepoints <c>has_property_value</c> was sampled at.</summary>
    public static IReadOnlyList<int> HasPropertyValueCodepoints => _loaded.Value.HasPropertyValueCodepoints;

    /// <summary>The digest of every <c>has_property_value</c> answer at those codepoints.</summary>
    public static string HasPropertyValue => _loaded.Value.HasPropertyValue;

    /// <summary>How many codepoints the <c>str</c> predicate digest covers.</summary>
    public static int PythonStrCodepointCount => _loaded.Value.PythonStrCodepointCount;

    /// <summary>How many codepoints it skips because they are new in Unicode 17.0.</summary>
    public static int PythonStrSkippedNewIn17 => _loaded.Value.PythonStrSkippedNewIn17;

    /// <summary>The digest of CPython's <c>isalpha</c>, <c>isdigit</c> and <c>isidentifier</c>.</summary>
    public static string PythonStr => _loaded.Value.PythonStr;

    /// <summary>
    /// The digest convention the generator uses for a table: the decimal values, comma separated,
    /// hashed as UTF-8.
    /// </summary>
    /// <param name="values">The table's values.</param>
    /// <returns>The lower-case hexadecimal SHA-256.</returns>
    public static string Digest(IReadOnlyList<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var text = new StringBuilder(values.Count * 4);
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            text.Append(values[i]);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static FixtureFile Load()
    {
        using Stream stream =
            typeof(UnicodeFixture).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException(
                $"the Unicode fixture is not embedded as '{_resourceName}'. Available: "
                    + string.Join(", ", typeof(UnicodeFixture).Assembly.GetManifestResourceNames())
            );

        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        return new FixtureFile(
            root.GetProperty("regexVersion").GetString()!,
            root.GetProperty("unicodeVersion").GetString()!,
            root.GetProperty("pythonUnicodeVersion").GetString()!,
            root.GetProperty("tables")
                .EnumerateObject()
                .ToImmutableDictionary(
                    static property => property.Name,
                    static property => new TableDigest(
                        property.Value.GetProperty("length").GetInt32(),
                        property.Value.GetProperty("sha256").GetString()!
                    ),
                    StringComparer.Ordinal
                ),
            root.GetProperty("casing")
                .EnumerateObject()
                .ToImmutableDictionary(
                    static property => property.Name,
                    static property => property.Value.GetString()!,
                    StringComparer.Ordinal
                ),
            root.GetProperty("propertyCount").GetInt32(),
            root.GetProperty("propertyValuePairCount").GetInt32(),
            root.GetProperty("properties").GetString()!,
            [.. root.GetProperty("hasPropertyValueCodepoints").EnumerateArray().Select(static c => c.GetInt32())],
            root.GetProperty("hasPropertyValue").GetString()!,
            root.GetProperty("pythonStrCodepointCount").GetInt32(),
            root.GetProperty("pythonStrSkippedNewIn17").GetInt32(),
            root.GetProperty("pythonStr").GetString()!
        );
    }
}

/// <summary>One table's length and digest.</summary>
/// <param name="Length">How many values the table holds.</param>
/// <param name="Sha256">The digest of those values.</param>
public sealed record TableDigest(int Length, string Sha256);

/// <summary>The whole fixture, as read from the embedded JSON.</summary>
internal sealed record FixtureFile(
    string RegexVersion,
    string UnicodeVersion,
    string PythonUnicodeVersion,
    IReadOnlyDictionary<string, TableDigest> Tables,
    IReadOnlyDictionary<string, string> Casing,
    int PropertyCount,
    int PropertyValuePairCount,
    string Properties,
    IReadOnlyList<int> HasPropertyValueCodepoints,
    string HasPropertyValue,
    int PythonStrCodepointCount,
    int PythonStrSkippedNewIn17,
    string PythonStr
);
