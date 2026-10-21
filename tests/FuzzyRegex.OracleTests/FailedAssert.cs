using System.Diagnostics;

namespace Fuzzy.Text.RegularExpressions.OracleTests;

/// <summary>A failed <c>Debug.Assert</c>, or a <c>Trace.Fail</c>, raised by <see cref="FailedAssertListener"/>.</summary>
/// <param name="message">The assert's message, and its detail message where it has one.</param>
public sealed class FailedAssertException(string message) : Exception(message);

/// <summary>
/// Turns a failed assert into a <see cref="FailedAssertException"/>, so the row that hit it is
/// answered and <see cref="OracleComparer.RunWave"/> can report it as a FAULT.
/// </summary>
/// <remarks>
/// <para>
/// Installed for the whole assembly, in place of every other listener. Without a listener that
/// throws, a failed <c>Debug.Assert</c> calls <c>Environment.FailFast</c> and the run reports
/// nothing. TUnit happens to install a throwing listener of its own (<c>TUnit.Engine.Exceptions.ThrowListener</c>,
/// seen 2026-10-16 with TUnit 1.65.68), but that is TUnit's internal choice, and its exception is a
/// plain <c>TUnitException</c>, which the comparer cannot tell from any other crash.
/// </para>
/// <para>
/// The engine compiled in Debug is what this matters for: <c>tools/run-oracle.ps1 -Configuration
/// Debug</c>, and the correctness battery, run the wave to check the engine's invariants.
/// </para>
/// </remarks>
internal sealed class FailedAssertListener : TraceListener
{
    [Before(HookType.Assembly)]
    public static void Install()
    {
        Trace.Listeners.Clear();
        Trace.Listeners.Add(new FailedAssertListener());
    }

    public override void Write(string? message) { }

    public override void WriteLine(string? message) { }

    public override void Fail(string? message, string? detailMessage) =>
        throw new FailedAssertException(
            string.IsNullOrEmpty(detailMessage) ? message ?? "" : $"{message} {detailMessage}"
        );
}
