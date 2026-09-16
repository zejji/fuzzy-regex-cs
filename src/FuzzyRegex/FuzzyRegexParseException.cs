namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Thrown when a pattern cannot be compiled. Upstream <c>regex.error</c>, defined as
/// <c>class error(Exception)</c> in <c>upstream/regex/_regex_core.py</c>, which likewise carries
/// the pattern and the offset at which parsing failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exception mapping</b>. This type means upstream's <c>error</c> and nothing else. A bare
/// <c>ValueError</c> rejection - conflicting version or encoding flags, or a named list the
/// pattern never uses - is also reported through it, with no <see cref="Pattern"/> or
/// <see cref="Offset"/>, because upstream's <c>ValueError</c> carries neither (S07; see the
/// reasoning at the throw sites in <c>Parsing/PatternCompiler.cs</c>).
/// </para>
/// <para>
/// Upstream's other errors map onto other .NET types, not this one: <c>IndexError</c> from
/// <c>expand</c> becomes <see cref="ArgumentException"/>; an escaped internal error such as
/// <c>AttributeError</c> or <c>KeyError</c> becomes <see cref="NotSupportedException"/> or
/// <see cref="ArgumentOutOfRangeException"/>; the 1 GB backtracking bound and a runaway
/// recursion raise <see cref="InvalidOperationException"/>, never
/// <see cref="OutOfMemoryException"/>, where upstream raises <c>MemoryError</c>; a matching
/// timeout raises <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> where
/// upstream raises <c>TimeoutError</c>; and <c>\p{Infinity}</c> raises
/// <see cref="OverflowException"/>, carrying Python's own message.
/// </para>
/// </remarks>
public class FuzzyRegexParseException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public FuzzyRegexParseException()
        : this("The pattern could not be parsed.") { }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">A description of what was wrong with the pattern.</param>
    public FuzzyRegexParseException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance with the given message and cause.</summary>
    /// <param name="message">A description of what was wrong with the pattern.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FuzzyRegexParseException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance describing where in the pattern parsing failed.
    /// </summary>
    /// <param name="message">A description of what was wrong with the pattern.</param>
    /// <param name="pattern">The pattern that failed to parse.</param>
    /// <param name="offset">
    /// The zero-based UTF-16 offset into <paramref name="pattern"/> at which parsing failed.
    /// </param>
    public FuzzyRegexParseException(string message, string pattern, int offset)
        : base(message)
    {
        Pattern = pattern;
        Offset = offset;
    }

    /// <summary>The pattern that failed to parse, or <see langword="null"/> if not supplied.</summary>
    public string? Pattern { get; }

    /// <summary>
    /// The zero-based UTF-16 offset into <see cref="Pattern"/> at which parsing failed, or
    /// <c>-1</c> if not supplied.
    /// </summary>
    /// <remarks>
    /// <b>Indices are UTF-16 code units</b>, not codepoints, so this differs from upstream's
    /// <c>error.pos</c> for a pattern holding a non-BMP character before the failure point.
    /// </remarks>
    public int Offset { get; } = -1;
}
