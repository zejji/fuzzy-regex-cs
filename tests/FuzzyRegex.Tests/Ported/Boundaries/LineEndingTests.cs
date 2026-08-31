using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Boundaries;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_line_ending</c> (line 4531):
/// <c>\R</c> matches every Unicode line ending as a single token, treating a CRLF pair as one
/// match rather than two.
/// </summary>
public sealed class LineEndingTests
{
    [Test]
    [Skip("needs:find-all - the boundary opcodes land in S20; FuzzyRegex.Matches is S25")]
    [Property("Upstream", "RegexTests.test_line_ending#1")]
    public void Backslash_R_matches_every_unicode_line_ending_as_one_token()
    {
        string subject = "\r\n\n\x0B\x0C\r\u0085\u2028\u2029";

        FuzzyRegex
            .Matches(subject, @"\R")
            .Select(m => m.Value)
            .Should()
            .Equal("\r\n", "\n", "\x0B", "\x0C", "\r", "\u0085", "\u2028", "\u2029");
    }
}
