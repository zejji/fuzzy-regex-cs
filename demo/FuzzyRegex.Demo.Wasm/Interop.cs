using System.Runtime.InteropServices.JavaScript;

namespace FuzzyRegexDemo.Wasm;

/// <summary>
/// The demo's entire JavaScript surface: one method, three strings in, one JSON string out.
/// </summary>
/// <remarks>
/// It is a one-line wrapper on purpose. <see cref="DemoEngine.Run"/> is plain C# with no interop
/// in it, so <c>tests/FuzzyRegex.Tests</c> compiles and pins it under the JIT on three operating
/// systems; what is left here is the attribute, which only a real browser can exercise, and
/// <c>wwwroot/harness.html</c> does that.
/// </remarks>
internal static partial class Interop
{
    /// <inheritdoc cref="DemoEngine.Run"/>
    [JSExport]
    internal static string Run(string pattern, string flags, string subject) => DemoEngine.Run(pattern, flags, subject);
}
