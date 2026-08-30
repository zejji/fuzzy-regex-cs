using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// The property dictionary <c>_regex_core.PROPERTIES</c> is built from, and
/// <c>has_property_value</c> over every property and value it holds.
/// </summary>
/// <remarks>
/// The dictionary is the whole chain: <c>re_strings</c>, <c>re_properties</c>,
/// <c>re_property_values</c>, <c>munge_name</c> and <c>init_property_dict</c>. Getting any of them
/// wrong would leave <c>\p{...}</c> resolving to the wrong property id, which is a wrong match
/// rather than an error - so it is checked exhaustively rather than sampled.
/// <para>
/// The lookups themselves are sampled, not exhaustive: 5,200 property/value pairs against 1,595
/// codepoints. Exhaustive property parity is proved in Phase 3 by the match oracle, where
/// <c>\p{...}</c> can actually be run (design spec section 6).
/// </para>
/// </remarks>
public sealed class UnicodePropertyTests
{
    [Test]
    public void The_property_dictionary_is_upstreams()
    {
        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();

        var canonical = new StringBuilder();
        foreach (string name in properties.Keys.Order(StringComparer.Ordinal))
        {
            PropertyEntry entry = properties[name];
            canonical.Append(name).Append('=').Append(Format(entry.Id)).Append(':');
            canonical.AppendJoin(
                ',',
                entry.Values.Keys.Order(StringComparer.Ordinal).Select(v => $"{v}={Format(entry.Values[v])}")
            );
            canonical.Append('\n');
        }

        using (new AssertionScope())
        {
            properties.Should().HaveCount(UnicodeFixture.PropertyCount);
            Sha256(canonical.ToString()).Should().Be(UnicodeFixture.Properties);
        }
    }

    [Test]
    public void Has_property_value_matches_upstream_for_every_property_and_value()
    {
        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();
        IReadOnlyList<int> codepoints = UnicodeFixture.HasPropertyValueCodepoints;

        List<uint> codes = [];
        foreach (string name in properties.Keys.Order(StringComparer.Ordinal))
        {
            PropertyEntry entry = properties[name];
            codes.AddRange(
                entry
                    .Values.Keys.Order(StringComparer.Ordinal)
                    .Select(value => ((uint)entry.Id << 16) | (uint)entry.Values[value])
            );
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var line = new StringBuilder(24);
        foreach (uint code in codes)
        {
            foreach (int codepoint in codepoints)
            {
                line.Clear();
                line.Append(Format((int)code))
                    .Append(':')
                    .Append(Format(codepoint))
                    .Append(':')
                    .Append(Encodings.HasProperty(code, (uint)codepoint) ? '1' : '0')
                    .Append('\n');
                hash.AppendData(Encoding.UTF8.GetBytes(line.ToString()));
            }
        }

        using (new AssertionScope())
        {
            codes.Should().HaveCount(UnicodeFixture.PropertyValuePairCount);
            Convert.ToHexStringLower(hash.GetHashAndReset()).Should().Be(UnicodeFixture.HasPropertyValue);
        }
    }

    /// <summary>
    /// A handful of readable lookups, so a reader can see what the digests above are asserting
    /// about. Values measured against the local oracle on 2026-08-30.
    /// </summary>
    [Test]
    public void Spot_checks_read_the_way_upstream_does()
    {
        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();

        using (new AssertionScope())
        {
            // GREEK SMALL LETTER ALPHA is in the Greek script and is a lowercase letter.
            HasValue(properties, "SCRIPT", "GREEK", 0x3B1).Should().BeTrue();
            HasValue(properties, "GENERALCATEGORY", "LL", 0x3B1).Should().BeTrue();
            // L, L& and LC are group values that no lookup function ever returns: they are the
            // RE_PROP_*_MASK branch of unicode_has_property, which is easy to drop in a port.
            HasValue(properties, "GENERALCATEGORY", "L", 0x3B1).Should().BeTrue();
            HasValue(properties, "GENERALCATEGORY", "L&", 0x3B1).Should().BeTrue();
            HasValue(properties, "GENERALCATEGORY", "LC", 0x3B1).Should().BeTrue();
            HasValue(properties, "GENERALCATEGORY", "LU", 0x3B1).Should().BeFalse();

            // ARABIC-INDIC DIGIT ZERO is a decimal digit.
            HasValue(properties, "GENERALCATEGORY", "ND", 0x660).Should().BeTrue();

            // A codepoint with more than one script extension: U+0640 ARABIC TATWEEL.
            Span<byte> scripts = stackalloc byte[UnicodeTables.MaxScx];
            UnicodeTables.GetScriptExtensions(0x640, scripts).Should().BeGreaterThan(1);
            HasValue(properties, "SCRIPTEXTENSIONS", "ARABIC", 0x640).Should().BeTrue();
            HasValue(properties, "SCRIPTEXTENSIONS", "SYRIAC", 0x640).Should().BeTrue();
            HasValue(properties, "SCRIPTEXTENSIONS", "GREEK", 0x640).Should().BeFalse();

            // Unassigned: U+0378 is a hole in the Greek block.
            HasValue(properties, "GENERALCATEGORY", "CN", 0x378).Should().BeTrue();
            HasValue(properties, "GENERALCATEGORY", "ASSIGNED", 0x378).Should().BeFalse();
            HasValue(properties, "SCRIPT", "GREEK", 0x378).Should().BeFalse();
        }
    }

    private static bool HasValue(
        IReadOnlyDictionary<string, PropertyEntry> properties,
        string property,
        string value,
        int codepoint
    )
    {
        PropertyEntry entry = properties[property];
        return Encodings.HasProperty(((uint)entry.Id << 16) | (uint)entry.Values[value], (uint)codepoint);
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Sha256(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
