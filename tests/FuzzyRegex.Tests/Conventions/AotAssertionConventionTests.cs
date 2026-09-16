using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// S53. The suite is published as a Native AOT binary and run as the dynamic half of the AOT gate
/// (<c>tools/run-aot-tests.ps1</c>, and the <c>aot</c> job in <c>ci.yml</c>), so an assertion that
/// only works under the JIT breaks the gate rather than failing honestly.
/// </summary>
/// <remarks>
/// <c>BeEquivalentTo</c> is the one such assertion found. Its equivalency engine calls
/// <c>MethodInfo.MakeGenericMethod()</c>, which native AOT cannot satisfy, so every use throws
/// <c>NotSupportedException: ... is missing native code</c> in the published binary - 1,547 of
/// 6,153 tests, measured 2026-09-16 before S53 replaced the twelve call sites with
/// <see cref="Equivalence"/>. This is the ratchet on that: the next one fails here, under the JIT,
/// on the day it is written, instead of turning the AOT leg red for a reason nobody reading a
/// normal test run would recognise.
/// </remarks>
public sealed class AotAssertionConventionTests
{
    /// <summary>
    /// The one assertion whose implementation is not AOT-compatible, as the call text to look for.
    /// </summary>
    /// <remarks>
    /// No trailing <c>(</c>, which is what S53's blind review found this matching on first: the
    /// explicit-type-argument spelling <c>.BeEquivalentTo&lt;string&gt;(...)</c> is legal C#,
    /// reaches the same equivalency engine, and slipped straight past a needle ending in a
    /// parenthesis. Matching the member name alone catches every call shape.
    /// </remarks>
    private const string _bannedCall = ".BeEquivalentTo";

    /// <summary>
    /// The one file entitled to write the banned call in code: this one, which has to name it in
    /// order to look for it.
    /// </summary>
    /// <remarks>
    /// <see cref="Equivalence"/> needs no exemption although it names the call repeatedly, because
    /// all of its mentions are in documentation comments, which <see cref="IsProse"/> skips.
    /// </remarks>
    private const string _fileThatDeclaresTheRule = "AotAssertionConventionTests.cs";

    [Test]
    public void No_test_source_calls_an_assertion_that_native_AOT_cannot_run()
    {
        IReadOnlyList<FileInfo> sources = [.. TestTree.Sources()];

        sources
            .Should()
            .HaveCountGreaterThan(
                50,
                "the scan must actually find this project's source, or the rule below passes vacuously"
            );

        List<string> violations = [];
        foreach (FileInfo source in sources)
        {
            if (source.Name.Equals(_fileThatDeclaresTheRule, StringComparison.Ordinal))
            {
                continue;
            }

            int number = 0;
            foreach (string line in File.ReadLines(source.FullName))
            {
                number++;
                if (!IsProse(line) && line.Contains(_bannedCall, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{source.Name}({number}): BeEquivalentTo cannot run under native AOT, in any call "
                            + "shape; render the values and compare them with Should().Equal(...), as "
                            + "Equivalence does"
                    );
                }
            }
        }

        violations.Should().BeEmpty();
    }

    /// <summary>
    /// Whether a line is a comment rather than code, so that writing ABOUT the banned call is not
    /// itself a violation.
    /// </summary>
    /// <remarks>
    /// S53's blind review raised this too: the first version matched every line, so a doc comment
    /// explaining why the call is banned failed the build. It cost two rounds while the slice was
    /// being written, and it would cost the same to whoever next documents the rule. A trailing
    /// comment on a line of real code is deliberately still a violation - erring towards flagging
    /// is right for a rule whose false negative is a red CI leg on another machine.
    /// </remarks>
    /// <param name="line">The source line.</param>
    /// <returns>Whether it is a comment line.</returns>
    private static bool IsProse(string line)
    {
        string trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
    }
}
