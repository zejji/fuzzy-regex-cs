using System.Reflection;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The bound that turns an engine hang into one named red test rather than a suite that never ends.
/// </summary>
/// <remarks>
/// <para>
/// S56's mutation run is the evidence that this matters. Of the 60 engine mutants the queue could
/// not score normally, 46 made the suite hang: 24 of them in <c>Optimiser.AddRepeatGuards</c>, where
/// clearing or inverting the <c>NodeStatus.VisitedAg</c> mark makes the work-list walk re-push a
/// node it has already visited, and 3 in <c>ByteStack.PushBlock</c>, where the doubling loop stops
/// reaching the capacity it needs. Reproduce either with
/// <c>python tools/probes/s56-mutant-behaviour.py optimiser bytestack</c>: both run past the probe's
/// 60 s deadline, where the unmutated engine answers in under 100 ms.
/// </para>
/// <para>
/// A hang is the worst test failure - the ratchet never reports and the driver waits on a spinning
/// host, which is what happened on 2026-09-13 (DECISIONS). <c>AssemblyTimeout.cs</c> is the answer,
/// and this test is what notices if that file is ever deleted or its bound raised past the point
/// where CI gives up first. It asserts our own harness, so it has no upstream answer to cite.
/// </para>
/// </remarks>
public sealed class HangBoundTests
{
    [Test]
    public void Every_test_in_the_assembly_carries_a_timeout_so_an_engine_hang_fails_instead_of_stalling()
    {
        TUnit.Core.TimeoutAttribute? timeout =
            typeof(HangBoundTests).Assembly.GetCustomAttribute<TUnit.Core.TimeoutAttribute>();

        timeout.Should().NotBeNull("an engine hang must fail one test, not stall the whole suite");
        timeout.Timeout.Should().Be(TimeSpan.FromMinutes(2));
    }
}
