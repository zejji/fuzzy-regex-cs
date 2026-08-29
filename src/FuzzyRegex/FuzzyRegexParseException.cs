namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Thrown when a pattern cannot be parsed. Upstream <c>regex.error</c>, defined as
/// <c>class error(Exception)</c> in <c>upstream/regex/_regex_core.py</c>, which likewise carries
/// the pattern and the offset at which parsing failed.
/// </summary>
public class FuzzyRegexParseException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public FuzzyRegexParseException()
        : this("The pattern could not be parsed.")
    {
    }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">A description of what was wrong with the pattern.</param>
    public FuzzyRegexParseException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the given message and cause.</summary>
    /// <param name="message">A description of what was wrong with the pattern.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FuzzyRegexParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

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
    public int Offset { get; } = -1;
}
