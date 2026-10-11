using System.Reflection;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The engine's main loop must stay a method the JIT can tier with profile-guided optimisation.
/// </summary>
/// <remarks>
/// <para>
/// A method that contains <c>stackalloc</c> (IL <c>localloc</c>) cannot be given patchpoints for
/// on-stack replacement, so when it also has loops the JIT skips tier 0 and compiles it once,
/// fully optimised but with no profile data, and never recompiles it. <c>BasicMatchCore</c> was such a
/// method: <c>DOTNET_JitDisasmSummary=1</c> listed it as "Tier-0 switched to FullOpts" with "No PGO
/// data". With its five small scratch buffers held in fixed-size inline-array locals instead, it is
/// compiled "Instrumented Tier0" and then "Tier1", and a short <c>IsMatch</c> went from about 107.6
/// to 98.8 ns (best of three 5-second runs each, Release, .NET 10, 2026-10-11).
/// </para>
/// <para>
/// The scan reads opcode bytes without decoding operands, so an operand that happens to hold the
/// localloc bytes would fail the test spuriously; it can never pass one that has a localloc.
/// </para>
/// </remarks>
public sealed class JitTieringTests
{
    [Test]
    public void The_main_matching_loop_has_no_stackalloc()
    {
        MethodInfo method = typeof(Matcher).GetMethod("BasicMatchCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;

        // localloc is the two-byte opcode 0xFE 0x0F (ECMA-335 III.3.47).
        bool hasLocalloc = il.Zip(il.Skip(1)).Any(static pair => pair is (0xFE, 0x0F));

        hasLocalloc.Should().BeFalse("a stackalloc keeps the JIT from tiering the loop with profile data");
    }

    [Test]
    public void The_main_loops_inline_buffers_are_as_long_as_what_they_hold()
    {
        // A full case folding fills ThreeUints, and one count per fuzzy kind fills ThreeLongs. The
        // stackallocs they replaced took their lengths from these two constants.
        UnicodeTables.MaxFolded.Should().Be(3);
        FuzzyValue.Count.Should().Be(3);
    }
}
