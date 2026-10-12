using System.Reflection;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The engine's matching code must stay code the JIT can tier with profile-guided optimisation and
/// inline.
/// </summary>
/// <remarks>
/// <para>
/// A method that contains <c>stackalloc</c> (IL <c>localloc</c>) cannot be given patchpoints for
/// on-stack replacement, so when it also has loops the JIT skips tier 0 and compiles it once,
/// fully optimised but with no profile data, and never recompiles it; and the JIT never inlines a
/// method with a <c>localloc</c>. <c>BasicMatchCore</c> was such a method: <c>DOTNET_JitDisasmSummary=1</c>
/// listed it as "Tier-0 switched to FullOpts" with "No PGO data". With its five small scratch buffers
/// held in fixed-size inline-array locals instead, it is compiled "Instrumented Tier0" and then
/// "Tier1", and a short <c>IsMatch</c> went from about 107.6 to 98.8 ns (best of three 5-second runs
/// each, Release, .NET 10, 2026-10-11). S62 did the same for every other fixed-size buffer the
/// matching code allocated (<c>InlineBuffers.cs</c>).
/// </para>
/// <para>
/// The scan reads opcode bytes without decoding operands, so an operand that happens to hold the
/// localloc bytes would fail the test spuriously; it can never pass a method that has a localloc.
/// </para>
/// </remarks>
public sealed class JitTieringTests
{
    // The one buffer whose size is the input's (a flag per required character, under 128), so an
    // inline array cannot hold it.
    private static readonly string[] _inputSized = ["RequiredStringScreen.GeneralWindowMayMatch"];

    [Test]
    public void The_matching_code_has_no_stackalloc_but_the_input_sized_ones()
    {
        IEnumerable<Type> types = typeof(Matcher)
            .Assembly.GetTypes()
            .Where(static type =>
                string.Equals(type.Namespace, typeof(Matcher).Namespace, StringComparison.Ordinal)
                || type == typeof(Encodings)
            );

        string[] withLocalloc =
        [
            .. types
                .SelectMany(static type =>
                    type.GetMethods(
                            BindingFlags.Static
                                | BindingFlags.Instance
                                | BindingFlags.Public
                                | BindingFlags.NonPublic
                                | BindingFlags.DeclaredOnly
                        )
                        .Where(HasLocalloc)
                        .Select(method => $"{type.Name}.{method.Name}")
                )
                .Order(StringComparer.Ordinal),
        ];

        withLocalloc
            .Should()
            .Equal(
                _inputSized,
                "a stackalloc keeps the JIT from tiering a loop with profile data and from inlining the method"
            );
    }

    [Test]
    public void The_main_matching_loop_has_no_stackalloc()
    {
        // Tiering is a JIT matter, and a native AOT binary has no IL to read: GetMethodBody throws
        // PlatformNotSupportedException there.
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            TUnit.Core.Skip.Test("no IL under native AOT; this rule runs under the JIT in the build-and-ratchet job");
        }

        MethodInfo method = typeof(Matcher).GetMethod("BasicMatchCore", BindingFlags.Static | BindingFlags.NonPublic)!;

        HasLocalloc(method).Should().BeFalse("a stackalloc keeps the JIT from tiering the loop with profile data");
    }

    private static bool HasLocalloc(MethodInfo method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();

        // localloc is the two-byte opcode 0xFE 0x0F (ECMA-335 III.3.47).
        return il is not null && il.Zip(il.Skip(1)).Any(static pair => pair is (0xFE, 0x0F));
    }
}
