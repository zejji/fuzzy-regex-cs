using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 46 rows of the table whose pattern is invalid. Upstream asserts the error message text; we assert only that the pattern is rejected, as the rest of the port does, so the message each row expects is recorded here instead:
/// </para>
/// <list type="bullet">
/// <item><c>self.BAD_GROUP_NAME</c>, "bad character in group name": rows 2-4,7-9,200.</item>
/// <item><c>self.TRAILING_CHARS</c>, "unbalanced parenthesis": rows 50,92,136,280,294,405,422.</item>
/// <item><c>self.MISSING_RPAREN</c>, "missing )": rows 5,93,281,406,477,498.</item>
/// <item><c>self.BAD_SET</c>, "unterminated character set": rows 89-90,259-260,384-385.</item>
/// <item><c>self.INVALID_GROUP_REF</c>, "invalid group reference": rows 19,205,328-329.</item>
/// <item><c>self.NOTHING_TO_REPEAT</c>, "nothing to repeat": rows 272-273,397-398.</item>
/// <item><c>self.UNKNOWN_GROUP</c>, "unknown group": rows 10,16,203.</item>
/// <item><c>self.BAD_ESCAPE</c>, "bad escape (end of pattern)": rows 91,275,400.</item>
/// <item><c>self.BAD_CHAR_RANGE</c>, "bad character range": rows 258,383.</item>
/// <item><c>self.MULTIPLE_REPEAT</c>, "multiple repeat": rows 286,411.</item>
/// <item><c>self.MISSING_GT</c>, "missing >": rows 1.</item>
/// <item><c>self.OPEN_GROUP</c>, "cannot refer to an open group": rows 496.</item>
/// </list>
/// <para>Every row below is one of the above.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousParseErrorTests
{
    [Test]
    [Arguments("(?P<foo_123")]
    [Arguments("(?P<1>a)")]
    [Arguments("(?P<!>a)")]
    [Arguments("(?P<foo!>a)")]
    [Arguments("(?P<foo_123>a)(?P=foo_123")]
    [Arguments("(?P<foo_123>a)(?P=0)")]
    [Arguments("(?P<foo_123>a)(?P=-1)")]
    [Arguments("(?P<foo_123>a)(?P=!)")]
    [Arguments("(?P<foo_123>a)(?P=foo_124)")]
    [Arguments("(?<foo_123>a)\\g<foo_124>")]
    [Arguments("\\1")]
    [Arguments(")")]
    [Arguments("a[]b")]
    [Arguments("a[")]
    [Arguments("a\\")]
    [Arguments("abc)")]
    [Arguments("(abc")]
    [Arguments(")(")]
    [Arguments("(?P<i d>aaa)a")]
    [Arguments("(?P<id>aa)(?P=xd)")]
    [Arguments("\\g<1>")]
    [Arguments("a[b-a]")]
    [Arguments("a[]b")]
    [Arguments("a[")]
    [Arguments("*a")]
    [Arguments("(*)b")]
    [Arguments("a\\")]
    [Arguments("abc)")]
    [Arguments("(abc")]
    [Arguments("a**")]
    [Arguments(")(")]
    [Arguments("((((((((((a))))))))))\\41")]
    [Arguments("(?i)((((((((((a))))))))))\\41")]
    [Arguments("(?i)a[b-a]")]
    [Arguments("(?i)a[]b")]
    [Arguments("(?i)a[")]
    [Arguments("(?i)*a")]
    [Arguments("(?i)(*)b")]
    [Arguments("(?i)a\\")]
    [Arguments("(?i)abc)")]
    [Arguments("(?i)(abc")]
    [Arguments("(?i)a**")]
    [Arguments("(?i))(")]
    [Arguments("w(?# comment")]
    [Arguments("((.)\\1+)")]
    [Arguments("(")]
    [Skip("needs:parse-errors - the parser does not reject invalid patterns yet")]
    [Property(
        "Upstream",
        "RegexTests.test_various#1-5,7-10,16,19,50,89-93,136,200,203,205,258-260,272-273,275,280-281,286,294,328-329,383-385,397-398,400,405-406,411,422,477,496,498"
    )]
    public void Invalid_pattern_is_rejected(string pattern)
    {
        Action act = () => _ = new FuzzyRegex(pattern);

        act.Should().Throw<FuzzyRegexParseException>();
    }
}
