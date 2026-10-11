using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The default Unicode word boundary of the WORD flag, <c>(?w)</c>, against the Unicode 17.0.0
/// conformance file <c>WordBreakTest.txt</c>.
/// </summary>
/// <remarks>
/// <para>
/// Upstream's README says the flag "changes the definition of a 'word boundary' to that of a
/// default Unicode word boundary" (<c>upstream/README.rst:1011</c>), and its tables are Unicode
/// 17.0.0 (<c>RE_UNICODE_VERSION</c>), so the conformance file of that version is the reference.
/// Upstream fails 268 of its 1,944 lines (regex 2026.9.10, measured 2026-09-26), and this port
/// did too until the same day. Three defects account for all of them:
/// </para>
/// <list type="bullet">
/// <item>UAX #29 WB4, "Ignore Format and Extend characters, except after sot, CR, LF, and
/// Newline", was applied only to the character on the left, so WB6, WB7, WB7b, WB7c, WB11, WB12
/// and WB15/WB16 compared the neighbour two away without skipping them, and a run of them at the
/// start of the text was joined to what followed instead of standing alone.</item>
/// <item>WB15 and WB16, <c>sot (RI RI)* RI × RI</c> and <c>[^RI] (RI RI)* RI × RI</c>, were applied
/// whatever followed the regional indicators, not only before another one.</item>
/// <item>Upstream adds a rule it labels WB5a that keeps an apostrophe and a following vowel
/// together. UAX #29 has no such default rule; its notes offer "apostrophe ÷ vowels" as a
/// tailoring for French and Italian, which breaks there rather than joining.</item>
/// </list>
/// <para>
/// The rows are the file's own, one or two per family, with the file's line number, plus the three
/// the sweep reported. Positions in the file count codepoints; the test converts them to UTF-16.
/// </para>
/// </remarks>
public sealed class DefaultWordBoundaryTests
{
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers these as described in the remarks.
    [Test]
    [Property("Upstream", "none - gap test")]
    // An Extend or Format run at the start of the text stands alone (WB4's "except after sot").
    [Arguments(221, "\u0300A", new[] { 0, 1, 2 })]
    [Arguments(222, "\u0300\u0308A", new[] { 0, 2, 3 })]
    [Arguments(230, "\u0300\u03080", new[] { 0, 2, 3 })]
    [Arguments(281, "\u00ADA", new[] { 0, 1, 2 })]
    [Arguments(1069, "\u200D0", new[] { 0, 1, 2 })]
    // A regional indicator joins only another regional indicator (WB15, WB16).
    [Arguments(821, "\U0001F1E6A", new[] { 0, 1, 2 })]
    [Arguments(1845, "\U0001F1E6\U0001F1E7\U0001F1E8b", new[] { 0, 2, 3, 4 })]
    // No apostrophe-vowel rule in the defaults.
    [Arguments(1001, "'A", new[] { 0, 1, 2 })]
    [Arguments(1661, "1'A", new[] { 0, 1, 2, 3 })]
    // WB6/WB7 and WB11/WB12 look past Extend and Format (WB4).
    [Arguments(1360, "a:\u0308\u24C2", new[] { 0, 4 })]
    [Arguments(1670, "1'\u03080", new[] { 0, 4 })]
    [Arguments(1789, "1.\u20600", new[] { 0, 4 })]
    // The three the sweep reported, by the same rules: 'a', colon, U+0308, 'a'; '1', comma,
    // U+0308, '1'; 'a', apostrophe, U+2060 WORD JOINER, 'a'. Line 0: not in the file itself.
    [Arguments(0, "a:\u0308a", new[] { 0, 4 })]
    [Arguments(0, "1,\u03081", new[] { 0, 4 })]
    [Arguments(0, "a'\u2060a", new[] { 0, 4 })]
    public void The_default_word_boundary_follows_WordBreakTest(int line, string subject, int[] breaks)
    {
        _ = line;

        // Codepoint positions to UTF-16 ones.
        var toUtf16 = new List<int>();
        for (int i = 0; i <= subject.Length; i++)
        {
            if (i == subject.Length || !char.IsLowSurrogate(subject[i]))
            {
                toUtf16.Add(i);
            }
        }

        int[] expected = [.. breaks.Select(b => toUtf16[b])];
        int[] notExpected = [.. toUtf16.Where(p => !expected.Contains(p))];

        Starts(@"(?w)\b", subject).Should().Equal(expected);
        Starts(@"(?w)(?r)\b", subject).Order().Should().Equal(expected);
        Starts(@"(?w)\B", subject).Should().Equal(notExpected);
    }

    private static int[] Starts(string pattern, string subject) =>
        [.. new FuzzyRegex(pattern).Matches(subject).Select(static m => m.Index)];
}
