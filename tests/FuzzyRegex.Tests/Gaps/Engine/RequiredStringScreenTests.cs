using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The two facts about the case tables that <c>Engine.RequiredStringScreen</c>'s shortcuts rest on.
/// The screen may only refuse a subject the matcher would refuse, so each shortcut has to keep
/// every subject character the full rule would accept; these tests check that against the tables
/// themselves rather than against examples.
/// </summary>
public sealed class RequiredStringScreenTests
{
    /// <summary>
    /// The window walk compares two ASCII characters with the one-for-one rule alone. That is only
    /// complete if no case of an ASCII character folds to anything but that character's lowercase:
    /// then the folding rule can never accept a pair the one-for-one rule rejects.
    /// </summary>
    [Test]
    public void Every_case_of_an_ascii_character_fully_folds_to_its_lowercase()
    {
        List<string> wrong = [];
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];

        foreach (CaseEncoding encoding in new[] { CaseEncoding.Unicode, CaseEncoding.Ascii })
        {
            for (uint ch = 0; ch < 0x80; ch++)
            {
                uint lower = ch is >= 'A' and <= 'Z' ? ch | 0x20 : ch;
                int count = Encodings.AllCases(encoding, ch, cases);

                for (int c = 0; c < count; c++)
                {
                    int length = Encodings.FullCaseFold(encoding, cases[c], folded);
                    if (length != 1 || folded[0] != lower)
                    {
                        wrong.Add($"{encoding} U+{ch:X4}: case U+{cases[c]:X4} folds to {length} character(s)");
                    }
                }
            }
        }

        wrong.Should().BeEmpty();
    }

    /// <summary>
    /// The screen only checks windows whose first character is one of the code units
    /// <c>RequiredStringScreen.FirstUnits</c> lists. Every character the window walk could let
    /// take the first required character must therefore be listed. The reference here is looser
    /// than the walk (any character of any case's folding may be the one that matches), so it
    /// checks a superset.
    /// </summary>
    /// <param name="pattern">A pattern whose required string starts (or, reversed, ends) with the
    /// character of interest.</param>
    [Test]
    [Arguments("(?i)szz")]
    [Arguments("(?i)kzz")]
    [Arguments("(?i)izz")]
    [Arguments("(?i)Izz")]
    [Arguments("(?i)ßzz")]
    [Arguments("(?i)σzz")]
    [Arguments("(?i)ǅzz")]
    [Arguments("(?i)İzz")]
    [Arguments("(?i)̇zz")]
    [Arguments("(?i)ιzz")]
    [Arguments("(?i)hzz")]
    [Arguments("(?i)\U00010400zz")]
    [Arguments("(?V0i)szz")]
    [Arguments("(?ri)zzs")]
    [Arguments("(?ri)zzß")]
    [Arguments("(?ri)zzσ")]
    public void Every_character_that_could_take_the_first_required_character_is_searched_for(string pattern)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;
        Node required = compiled.ReqString!;
        var units = compiled.ReqScreenUnits!;
        bool reverse = (compiled.Flags & RegexFlags.Reverse) != 0;
        uint want = required.Values[reverse ? required.Values.Count - 1 : 0];
        CaseEncoding encoding = required.Encoding;

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        List<string> missing = [];

        for (uint ch = 0; ch <= 0x1FFFF; ch++)
        {
            if (ch is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            bool couldTake = Matcher.SameCharIgn(encoding, want, ch);
            int count = Encodings.AllCases(encoding, ch, cases);

            for (int c = 0; c < count && !couldTake; c++)
            {
                int length = Encodings.FullCaseFold(encoding, cases[c], folded);
                for (int f = 0; f < length && !couldTake; f++)
                {
                    couldTake = Matcher.SameCharIgn(encoding, want, folded[f]);
                }
            }

            if (couldTake && !char.ConvertFromUtf32((int)ch).All(units.Contains))
            {
                missing.Add($"U+{ch:X4}");
            }
        }

        missing.Should().BeEmpty();
    }

    /// <summary>
    /// <c>RequiredStringScreen.Specials</c> is written out rather than built at run time, because
    /// building it reads the case tables for every codepoint (170 ms on first use). It must stay
    /// what the tables say.
    /// </summary>
    [Test]
    public void The_special_characters_are_what_the_case_tables_say() =>
        RequiredStringScreen.Specials.Should().Equal(BuildSpecials(0x10FFFF));

    /// <summary>
    /// Every codepoint up to <paramref name="last"/> with a case <c>y</c> whose full folding is longer
    /// than one character, whose case set does not hold the codepoint back, whose folding is outside
    /// its case set, or whose folding's case set does not hold <c>y</c>.
    /// </summary>
    /// <param name="last">The highest codepoint to examine.</param>
    /// <returns>The codepoints, ascending.</returns>
    private static uint[] BuildSpecials(uint last)
    {
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> caseCases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> foldCases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        var specials = new List<uint>();

        for (uint ch = 0; ch <= last; ch++)
        {
            if (ch is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            int count = Encodings.AllCases(CaseEncoding.Unicode, ch, cases);
            bool special = false;

            for (int c = 0; c < count && !special; c++)
            {
                uint y = cases[c];
                int caseCount = Encodings.AllCases(CaseEncoding.Unicode, y, caseCases);
                int foldedLength = Encodings.FullCaseFold(CaseEncoding.Unicode, y, folded);
                int foldCaseCount = Encodings.AllCases(CaseEncoding.Unicode, folded[0], foldCases);

                special =
                    foldedLength > 1
                    || !caseCases[..caseCount].Contains(ch)
                    || !caseCases[..caseCount].Contains(folded[0])
                    || !foldCases[..foldCaseCount].Contains(y);
            }

            if (special)
            {
                specials.Add(ch);
            }
        }

        return [.. specials];
    }
}
