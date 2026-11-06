namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Upstream <c>_UnscopedFlagSet</c> (<c>upstream/regex/_regex_core.py</c> lines 59-62): thrown
/// when a global flag such as <c>(?r)</c> is turned on part-way through a pattern, which means the
/// whole pattern has to be parsed again with that flag on from the start.
/// </summary>
/// <remarks>
/// Caught by <c>PatternCompiler.Compile</c>'s retry loop and never escapes it, which is why it is
/// internal despite S3871 - see the faithful-port block in <c>.editorconfig</c>.
/// </remarks>
/// <param name="globalFlags">The accumulated global flags.</param>
internal sealed class UnscopedFlagSetException(int globalFlags) : Exception
{
    /// <summary>The global flags the next parse attempt must start from.</summary>
    public int GlobalFlags { get; } = globalFlags;
}

/// <summary>
/// Upstream <c>ParseError</c> (<c>upstream/regex/_regex_core.py</c> lines 64-66): thrown when one
/// reading of the source fails and the caller wants to rewind and try another. Never reaches a
/// caller of the public API.
/// </summary>
internal sealed class ParseErrorException : Exception;
