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
    [Test]
    [MethodDataSource(typeof(Corpus), nameof(Corpus.Compiles))]
    public void Compiles_to_upstreams_bytecode(CompileRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        CompiledPattern compiled = RunSeam(() =>
            PatternCompiler.Compile(row.Pattern, row.Flags, row.NamedLists, Corpus.DefaultVersion)
        );

        using (new AssertionScope())
        {
            compiled.Code.Should().Equal(row.Code, "the bytecode is the whole point of the corpus");
            compiled.Flags.Should().Be(row.ResolvedFlags);
            compiled.GroupIndex.Should().BeEquivalentTo(row.GroupIndex);
            compiled.NamedLists.Should().BeEquivalentTo(row.CompiledNamedLists);
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
            Skip.Test(thrown.Message);
        }
    }
}
