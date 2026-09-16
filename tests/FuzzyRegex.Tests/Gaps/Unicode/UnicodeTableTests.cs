using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// Every table in <c>src/FuzzyRegex/Unicode/*.g.cs</c> against a digest taken independently from
/// <c>upstream/src/_regex_unicode.c</c>.
/// </summary>
/// <remarks>
/// <c>tools/transliterate-unicode.py</c> emits the tables; <c>tools/record-unicode-fixtures.py</c>
/// digests them with a second, deliberately naive parse of the same C file that shares no code
/// with the first. A bug in either one shows up here as a mismatch. Without this, the only check
/// on 402,293 transliterated numbers would be the transliterator agreeing with itself.
/// </remarks>
public sealed class UnicodeTableTests
{
    [Test]
    public void Every_table_upstream_declares_is_present()
    {
        IEnumerable<string> ours = UnicodeTables.AllTables().Select(static t => t.Name);

        // Sorted and compared with Equal rather than BeEquivalentTo, which cannot run under
        // Native AOT (see Equivalence); the question asked is still set equality.
        Equivalence
            .Sorted(ours)
            .Should()
            .Equal(Equivalence.Sorted(UnicodeFixture.Tables.Keys), "no table may be dropped or invented");
    }

    [Test]
    public void Every_table_has_upstreams_length_and_contents()
    {
        using (new AssertionScope())
        {
            foreach ((string name, uint[] values) in UnicodeTables.AllTables())
            {
                TableDigest expected = UnicodeFixture.Tables[name];
                values.Length.Should().Be(expected.Length, "{0} is truncated or padded", name);
                UnicodeFixture.Digest(values).Should().Be(expected.Sha256, "{0} has the wrong contents", name);
            }
        }
    }

    [Test]
    public void The_tables_are_the_unicode_version_upstream_generated_them_from()
    {
        UnicodeTables.UnicodeVersion.Should().Be(UnicodeFixture.UnicodeVersion);
    }

    /// <summary>
    /// The header's constants have to line up with the tables they index, or a lookup silently
    /// walks off the end of a buffer the caller sized from them.
    /// </summary>
    [Test]
    public void The_size_constants_match_the_tables_they_size()
    {
        using (new AssertionScope())
        {
            // RE_MAX_CASES - 1 slots per entry in the flattened `others` array, one `delta` each.
            UnicodeTables
                .AllCasesTable4Others.Length.Should()
                .Be(UnicodeTables.AllCasesTable4Delta.Length * (UnicodeTables.MaxCases - 1));
            // RE_MAX_FOLDED slots per entry, and the third level indexes entries, not slots.
            (UnicodeTables.FullFoldingTable4Data.Length % UnicodeTables.MaxFolded)
                .Should()
                .Be(0);
            UnicodeTables.PropertyFunctionCount.Should().Be(101);

            // GetScriptExtensions writes into a caller buffer of RE_MAX_SCX bytes, so no run in
            // the run table may be longer than that.
            int longest = 0;
            int run = 0;
            foreach (byte entry in UnicodeTables.ScriptExtensionsTable5)
            {
                run = entry == 0 ? 0 : run + 1;
                longest = Math.Max(longest, run);
            }

            longest.Should().BeLessThanOrEqualTo(UnicodeTables.MaxScx);
        }
    }
}
