using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.CompileParity;

/// <summary>
/// Drives <see cref="PatternCompiler"/> over every row of the compile-parity corpus and requires
/// upstream's exact output.
/// </summary>
/// <remarks>
/// <para>
/// These are gap tests, not ported ones, so they do not count towards the parity percentage - but
/// they are the strongest check the parser will get. The ported suite matches strings, and nothing
/// in Phase 2 can match a string; this compares bytecode.
/// </para>
/// <para>
/// A row whose seam still throws <see cref="NotImplementedException"/> skips at runtime, carrying
/// the <c>needs:</c> capability from the exception message so the status board counts it. Any
/// other outcome fails. That is what lets the ratchet stay green while the parser is half built
/// and turn red the moment a ported construct emits the wrong bytes.
/// </para>
/// </remarks>
public sealed class CompileParityTests
{
    /// <summary>
    /// The corpus rows whose bytecode this port DELIBERATELY does not reproduce, because S45
    /// replaced upstream's Turkic case data with the default one that <c>CaseFolding.txt</c>
    /// specifies - see <see cref="Fuzzy.Text.RegularExpressions.Unicode.TurkicDefaults"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each row is listed with what moved, all of it inside the four codepoints
    /// U+0049, U+0069, U+0130 and U+0131:
    /// </para>
    /// <list type="bullet">
    /// <item><c>(?fi)FFI</c> - the trailing <c>I</c> full-folds to <c>i</c> here and to <c>I</c>
    /// upstream, because upstream's <c>unicode_possible_turkic</c> passes it through and so loses
    /// the <c>0049; C; 0069</c> row.</item>
    /// <item><c>(?i)\Aİ\Z</c> and <c>(?i)\Aı\Z</c> - each is alone in its case set here
    /// and paired with <c>i</c> / <c>I</c> upstream, so the <c>_IGN</c> opcode changes.</item>
    /// <item><c>(?iV1)[\w--a]</c> - the set carries the expand-on-folding inventory, and U+0130 is
    /// in it. Upstream emits it as the ONE codepoint <c>304</c>, which is the defect itself: an
    /// entry in the expansion inventory whose folding does not expand. This port emits the two it
    /// folds to, <c>105 775</c> - <c>i</c> and U+0307.</item>
    /// </list>
    /// <para>
    /// The assertion for a listed row is that it STILL diverges, so the list cannot rot: a fifth
    /// row that starts diverging fails the equality below, and a listed row that stops diverging
    /// fails the inequality. Re-recording the corpus against a newer <c>regex</c> gets the same
    /// alarm either way.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> _turkicDivergentPatterns =
    [
        "(?fi)FFI",
        "(?i)\\Aİ\\Z",
        "(?i)\\Aı\\Z",
        "(?iV1)[\\w--a]",
    ];

    [Test]
    [MethodDataSource(typeof(Corpus), nameof(Corpus.Compiles))]
    public void Compiles_to_upstreams_bytecode(CompileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        CompiledPattern compiled = RunSeam(() =>
            PatternCompiler.Compile(row.Pattern, row.Flags, row.NamedLists, Corpus.DefaultVersion)
        );

        if (_turkicDivergentPatterns.Contains(row.Pattern))
        {
            compiled
                .Code.Should()
                .NotEqual(
                    row.Code,
                    "row #{0} is pinned as a deliberate Turkic divergence; if it now agrees with "
                        + "upstream, the pin is stale and belongs off the list",
                    row.Index
                );

            return;
        }

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal(row.Code, "the bytecode is the whole point of the corpus");
            compiled.Flags.Should().Be(row.ResolvedFlags);
            // Rendered to sorted lines rather than compared with BeEquivalentTo, which cannot run
            // under Native AOT (see Equivalence). Both are order-insensitive maps either way.
            Equivalence.Lines(compiled.GroupIndex).Should().Equal(Equivalence.Lines(row.GroupIndex));
            Equivalence.SetLines(compiled.NamedLists).Should().Equal(Equivalence.SetLines(row.CompiledNamedLists));
            // Order-sensitive on the outer list, because a position in it *is* the index the
            // parser assigned that named list and the index is baked into the bytecode; but
            // set equality within each entry, because upstream stores each as a frozenset.
            // BeEquivalentTo(..., WithStrictOrdering()) would be wrong on both counts: it
            // recurses, so it would also demand an order the frozenset does not have.
            compiled
                .NamedListIndexes.Should()
                .Equal(row.NamedListIndexes, (actual, expected) => actual.SetEquals(expected));
            compiled.ReqOffset.Should().Be(row.ReqOffset);
            compiled.ReqChars.Should().Equal(row.ReqChars);
            compiled.ReqFlags.Should().Be(row.ReqFlags);
            compiled.GroupCount.Should().Be(row.GroupCount);
        }
    }

    [Test]
    [MethodDataSource(typeof(Corpus), nameof(Corpus.Errors))]
    public void Rejects_what_upstream_rejects_with_the_same_message_and_offset(ErrorRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        // The message text is upstream's, ported verbatim: upstream's own suite asserts on it
        // (test_regex.py uses assertRaisesRegex throughout), so paraphrasing it would fail those
        // ported tests as well as this one.
        Action compile = () => PatternCompiler.Compile(row.Pattern, row.Flags, row.NamedLists, Corpus.DefaultVersion);

        // Throw<Exception>() rather than Throw<FuzzyRegexParseException>() so that the seam's own
        // NotImplementedException reaches SkipIfNotPorted as a skip instead of being reported as
        // the wrong exception type.
        Exception thrown = compile.Should().Throw<Exception>().Which;
        SkipIfNotPorted(thrown);

        using (new AssertionScope())
        {
            // Keyed on the recorded exception class, not on whether an offset was recorded.
            // regex.error takes pos=None by default and three sites raise it that way
            // (upstream/regex/_regex_core.py:1748, :1756, :1797), so "no offset" does not mean
            // "not a parse error" - keying on the offset would silently drop the type, offset
            // and pattern assertions for such a row.
            if (string.Equals(row.Exception, "error", StringComparison.Ordinal))
            {
                var error = thrown.Should().BeOfType<FuzzyRegexParseException>().Which;
                error.Message.Should().Be(row.Message);
                error.Pattern.Should().Be(row.Pattern);
                error.Offset.Should().Be(row.Position ?? -1, "upstream's error carries pos={0}", row.Position);
            }
            else
            {
                // Upstream rejects a flags conflict with a plain ValueError (upstream/regex/
                // _main.py:559-568), not with its own error type. Which .NET exception that
                // becomes is the porting slice's call, so only the message is pinned here - but
                // the pattern must still be rejected.
                thrown.Message.Should().Be(row.Message);
            }
        }
    }

    [Test]
    [MethodDataSource(typeof(Corpus), nameof(Corpus.Templates))]
    public void Compiles_replacement_templates_the_way_upstream_does(TemplateRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        IReadOnlyList<object> compiled = RunSeam(() =>
            PatternCompiler.CompileReplacement(row.Template, row.GroupCount, row.GroupIndex)
        );

        compiled.Should().Equal(row.Compiled);
    }

    /// <summary>
    /// Runs a corpus row against the seam, turning "this construct is not ported yet" into a skip
    /// and everything else into the test's own outcome.
    /// </summary>
    /// <remarks>
    /// A <see cref="NotImplementedException"/> whose message does not start with <c>needs:</c> is
    /// a failure, not a skip. Without that rule an unported construct could go missing from the
    /// status board's "waiting on a capability" table, which is what the next slice is chosen
    /// from.
    /// </remarks>
    private static T RunSeam<T>(Func<T> seam)
    {
        try
        {
            return seam();
        }
        catch (NotImplementedException notPorted)
        {
            SkipIfNotPorted(notPorted);
            throw;
        }
    }

    private static void SkipIfNotPorted(Exception thrown)
    {
        if (thrown is NotImplementedException && thrown.Message.StartsWith("needs:", StringComparison.Ordinal))
        {
            // Fully qualified: S11's (*SKIP) node is Parsing.Skip, which this file's usings also
            // bring into scope, so the bare name is ambiguous.
            TUnit.Core.Skip.Test(thrown.Message);
        }
    }
}
