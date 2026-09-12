using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.CompileParity;

/// <summary>
/// Pins the corpus fixture itself, so that losing it is a red ratchet rather than a quiet drop in
/// coverage.
/// </summary>
/// <remarks>
/// Every test in <see cref="CompileParityTests"/> skips until the parser lands, and a skipped test
/// is not in the ratchet baseline. If the embedded fixture went missing or came back empty, those
/// 1,600-odd tests would simply stop being generated and nothing would notice. These three do
/// pass, so they are baselined, so they notice.
/// </remarks>
public sealed class CorpusFixtureTests
{
    [Test]
    public void The_fixture_holds_upstreams_recorded_output()
    {
        // Exact counts would be a maintenance tax every time upstream adds a test. Non-empty,
        // and of the right order of magnitude, is what actually needs pinning.
        Corpus.Compiles().Should().HaveCountGreaterThan(1000);
        Corpus.Errors().Should().HaveCountGreaterThan(20);
        Corpus.Templates().Should().HaveCountGreaterThan(20);
        Corpus.RegexVersion.Should().NotBeNullOrWhiteSpace();
        Corpus.UpstreamCommit.Should().HaveLength(40);
        // Fully qualified: Tests.Gaps.Parsing exists too, and would otherwise win the lookup.
        Corpus.DefaultVersion.Should().Be(RegularExpressions.Parsing.PatternCompiler.DefaultVersion);
    }

    [Test]
    public void Every_row_has_a_unique_name_so_the_parity_baseline_can_key_on_it()
    {
        IEnumerable<string> names =
        [
            .. Corpus.Compiles().Select(static row => row.ToString()),
            .. Corpus.Errors().Select(static row => row.ToString()),
            .. Corpus.Templates().Select(static row => row.ToString()),
        ];

        names.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void No_row_name_carries_a_character_the_trx_report_cannot_hold()
    {
        // Several corpus patterns contain U+0000 and other control characters. XML 1.0 cannot
        // represent those in an attribute even escaped, so an unescaped test name would make the
        // TRX unparseable and take the whole ratchet down with it.
        IEnumerable<string> names =
        [
            .. Corpus.Compiles().Select(static row => row.ToString()),
            .. Corpus.Errors().Select(static row => row.ToString()),
            .. Corpus.Templates().Select(static row => row.ToString()),
        ];

        names.Should().AllSatisfy(static name => name.Should().NotContainAny(ControlCharacters()));
    }

    private static IEnumerable<string> ControlCharacters() =>
        Enumerable.Range(0, 0x20).Select(static c => ((char)c).ToString());
}
