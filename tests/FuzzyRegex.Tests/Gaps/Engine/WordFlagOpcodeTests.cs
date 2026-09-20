using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Tests.Ported;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The three matcher helpers that only the <c>WORD</c> flag can reach:
/// <c>TryMatchDefaultStartOfWord</c>, <c>TryMatchDefaultEndOfWord</c> - with
/// <c>AtDefaultWordStartOrEnd</c> under them - and <c>TryMatchAnyURev</c>.
/// </summary>
/// <remarks>
/// <para>
/// S57's coverage backstop found all three wholly unreached (<c>Matcher.cs:1528</c>, <c>:1843</c>,
/// <c>:1854</c>, <c>:1799</c>), together with the <c>ZeroWidthOpcode</c> of
/// <c>DefaultStartOfWord</c> and <c>DefaultEndOfWord</c> (<c>Nodes.cs:309</c>, <c>:316</c>). The
/// suite tests <c>(?w)</c> against <c>\b</c> (<c>EncodingAndWordOptionTests</c>) and against
/// <c>$</c> (<c>BacktrackingVerbTests</c>) but never against <c>\m</c>, <c>\M</c> or a reversed
/// <c>.</c>, which are the only three ways into these.
/// </para>
/// <para>
/// Each case carries the same pattern without <c>(?w)</c> beside it, because the whole point of
/// these opcodes is that the flag changes the answer: a test that did not show the difference would
/// pass just as well if the flag did nothing.
/// </para>
/// </remarks>
public sealed class WordFlagOpcodeTests
{
    /// <summary>
    /// <c>\m</c> under <c>WORD</c> asks the Unicode word-break algorithm, which holds an
    /// apostrophe inside a word, so <c>don't</c> begins one word rather than two.
    /// </summary>
    /// <remarks>
    /// Measured on regex 2026.9.10, 2026-09-20:
    /// <c>regex.findall(r"(?w)\m\w+", "don't stop")</c> gives <c>['don', 'stop']</c>, and
    /// <c>regex.findall(r"\m\w+", "don't stop")</c> gives <c>['don', 't', 'stop']</c>.
    /// </remarks>
    [Test]
    public void The_word_flag_moves_where_a_word_starts()
    {
        Upstream.Matches("don't stop", @"(?w)\m\w+").Select(static m => m.Value).Should().Equal("don", "stop");
        Upstream.Matches("don't stop", @"\m\w+").Select(static m => m.Value).Should().Equal("don", "t", "stop");
    }

    /// <summary>
    /// <c>\M</c> under <c>WORD</c>, the same way round: <c>don</c> is no longer a word end, so the
    /// first match starts inside the contraction.
    /// </summary>
    /// <remarks>
    /// Measured on regex 2026.9.10, 2026-09-20:
    /// <c>regex.findall(r"(?w)\w+\M", "don't stop")</c> gives <c>['t', 'stop']</c>, and
    /// <c>regex.findall(r"\w+\M", "don't stop")</c> gives <c>['don', 't', 'stop']</c>.
    /// </remarks>
    [Test]
    public void The_word_flag_moves_where_a_word_ends()
    {
        Upstream.Matches("don't stop", @"(?w)\w+\M").Select(static m => m.Value).Should().Equal("t", "stop");
        Upstream.Matches("don't stop", @"\w+\M").Select(static m => m.Value).Should().Equal("don", "t", "stop");
    }

    /// <summary>
    /// <c>.</c> under <c>WORD</c> is <c>ANY_U</c>, which refuses every Unicode line separator and
    /// not just <c>\n</c>; searched right to left it is <c>ANY_U_REV</c>.
    /// </summary>
    /// <remarks>
    /// Measured on regex 2026.9.10, 2026-09-20, writing U+0085 for the separator because a
    /// backslash-u escape is processed by the C# lexer inside a comment as well as inside a string:
    /// <c>regex.findall(r"(?rw).", "a" + U+0085 + "b")</c> gives <c>['b', 'a']</c> and
    /// <c>regex.findall(r"(?r).", ...)</c> on the same subject gives <c>['b', U+0085, 'a']</c>.
    /// With U+2028 in place of U+0085 the same two calls give <c>['b', 'a']</c> and
    /// <c>['b', U+2028, 'a']</c>.
    /// </remarks>
    // The separator arrives as a code point rather than as a string, because a backslash-u escape
    // for U+0085 or U+2028 IS a new line to the C# lexer, which processes it before it decides
    // where the literal ends - and inside a comment as well as inside a literal. Writing the
    // escape either way costs "Newline in constant".
    [Test]
    [Arguments(0x0085)]
    [Arguments(0x2028)]
    public void A_reversed_dot_under_the_word_flag_refuses_every_line_separator(int separator)
    {
        string mark = ((char)separator).ToString();
        string subject = $"a{mark}b";

        Upstream.Matches(subject, "(?rw).").Select(static m => m.Value).Should().Equal("b", "a");
        Upstream.Matches(subject, "(?r).").Select(static m => m.Value).Should().Equal("b", mark, "a");
    }
}
