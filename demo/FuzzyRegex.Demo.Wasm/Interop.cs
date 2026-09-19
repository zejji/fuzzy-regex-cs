using System.Runtime.InteropServices.JavaScript;

namespace FuzzyRegexDemo.Wasm;

/// <summary>
/// The demo's entire JavaScript surface: one method, six strings in, one JSON string out.
/// </summary>
/// <remarks>
/// It is a one-line wrapper on purpose. The signature is spelled out because <c>Run</c> has two
/// overloads since the v2 contract, and an unqualified cref is CS0419 - an error here, where
/// warnings are errors, and one the Release publish is the first thing to report.
/// <see cref="DemoEngine.Run(string, string, string, string, string, string)"/> is plain C# with no interop
/// in it, so <c>tests/FuzzyRegex.Tests</c> compiles and pins it under the JIT on three operating
/// systems; what is left here is the attribute, which only a real browser can exercise, and
/// <c>wwwroot/harness.html</c> does that.
/// </remarks>
internal static partial class Interop
{
    /// <inheritdoc cref="DemoEngine.Run(string, string, string, string, string, string)"/>
    /// <remarks>
    /// Six strings and no options object: everything crossing this boundary is a string, so the
    /// marshalling stays the one shape <c>[JSExport]</c> is happiest with, and the page's request
    /// envelope in <c>wwwroot/worker.js</c> is what gives the six positions their names.
    /// </remarks>
    [JSExport]
    internal static string Run(
        string pattern,
        string flags,
        string subject,
        string mode,
        string replacement,
        string namedLists
    ) => DemoEngine.Run(pattern, flags, subject, mode, replacement, namedLists);
}
