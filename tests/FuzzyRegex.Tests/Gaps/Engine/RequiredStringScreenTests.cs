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
    /// The screen only checks windows that hold one of the code units
    /// <c>RequiredStringScreen.ScreenUnits</c> lists, where the anchor character belongs. Every
    /// character the window walk could let take the anchor character must therefore be listed. The
    /// reference here is looser than the walk (any character of any case's folding may be the one
    /// that matches), so it checks a superset.
    /// </summary>
    /// <param name="pattern">A pattern whose required string starts (or, reversed, ends) with the
    /// character of interest, or holds it as its anchor.</param>
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
    [Arguments("(?i)abkzz")]
    [Arguments("(?ri)zzkba")]
    [Arguments("(?i)ab\U00010400zz")]
    public void Every_character_that_could_take_the_first_required_character_is_searched_for(string pattern)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;
        Node required = compiled.ReqString!;
        var units = compiled.ReqScreenUnits!;
        bool reverse = (compiled.Flags & RegexFlags.Reverse) != 0;
        int anchor = compiled.ReqScreenAnchor;
        uint want = required.Values[reverse ? required.Values.Length - 1 - anchor : anchor];
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
    /// D169. Text that holds the required string's first character everywhere but never its rare
    /// one: each window the screen checked used to be walked nearly to its end, so the walk was
    /// the text's length times the string's. The screen now searches for the string's rarest
    /// character and checks only the windows that hold it where it belongs.
    /// </summary>
    /// <param name="flags">The inline flags.</param>
    /// <param name="reverse">Whether the flags read right to left.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(?i)", false)]
    [Arguments("(?fi)", false)]
    [Arguments("(?ai)", false)]
    [Arguments("(?ri)", true)]
    [Arguments("(?rfi)", true)]
    [Arguments("(?r)", true)]
    public void A_window_is_checked_only_where_the_rarest_required_character_is(string flags, bool reverse)
    {
        // The rare character sits at the reading-order end, so that every window holding the
        // first character matches almost to the end before failing. The two calls walked
        // 3,951,400 characters forward and 3,961,601 reversed on main, 2026-11-23 (Debug).
        string pairs = string.Concat(Enumerable.Repeat("ab", 199));
        string literal = reverse ? "bc" + pairs : pairs + "cb";
        var pattern = new FuzzyRegex(flags + literal, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = string.Concat(Enumerable.Repeat("ab", 5_000));

        EngineWork.ShouldWalkAtMostCharacters(
            () =>
            {
                pattern.IsMatch(subject).Should().BeFalse();
                Match match = pattern.Match(subject + literal + subject);
                (match.Success, match.Index, match.Length).Should().Be((true, subject.Length, literal.Length));
            },
            maxCharacters: 2 * (literal.Length + subject.Length),
            because: "only a window holding the rare character is walked"
        );
    }

    /// <summary>
    /// D169. The answers around the rare-character search, each regex 2026.9.29's
    /// <c>search</c> and <c>finditer</c> under <c>VERSION1</c>, this port's default, over the same slice
    /// (2026-11-23, UTF-16 units): a case
    /// that takes one code unit before the searched character (Kelvin sign, long s), a case that
    /// takes two (an astral letter) or one subject character that takes two required ones (sharp
    /// s, dotted capital I), the slice edges, and the reverse mirror of each.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="beginning">Where the slice starts.</param>
    /// <param name="length">The slice length, or -1 for the rest.</param>
    /// <param name="found">The first match as index and length, or empty for none.</param>
    /// <param name="all">Every match, each as index and length, separated by ';'.</param>
    [Test]
    [Arguments("(?fi)ssz", "x\u00dfz", 0, -1, "1,2", "1,2")]
    [Arguments("(?fi)ssz", "xssz", 0, -1, "1,3", "1,3")]
    [Arguments("(?i)\U00010400z", "x\U00010428z", 0, -1, "1,3", "1,3")]
    [Arguments("(?i)kkq", "a\u212aKq", 0, -1, "1,3", "1,3")]
    [Arguments("(?i)saq", "\u017faq", 0, -1, "0,3", "0,3")]
    [Arguments("(?i)ssq", "\u017fsq", 0, -1, "0,3", "0,3")]
    // Python answers None for these two: it pairs i with the dotted capital I (Turkic), which
    // this port does not, so here the capital fully folds to i and a combining dot
    // (docs/DIVERGENCES.md, "The Turkic I pairings").
    [Arguments("(?fi)i\u0307q", "x\u0130q", 0, -1, "1,2", "1,2")]
    [Arguments("(?i)i\u0307q", "x\u0130q", 0, -1, "1,2", "1,2")]
    [Arguments("(?ri)qkk", "aqK\u212ab", 0, -1, "1,3", "1,3")]
    [Arguments("(?ri)qkk", "aqK\u212abqkk", 0, -1, "5,3", "5,3;1,3")]
    [Arguments("(?ri)zss", "z\u00dfx", 0, -1, "0,2", "0,2")]
    [Arguments("(?rfi)zss", "z\u00dfx", 0, -1, "0,2", "0,2")]
    [Arguments("(?r)qaa", "qaabqaa", 0, -1, "4,3", "4,3;0,3")]
    [Arguments("(?i)aab", "AAAB aab xaab", 0, -1, "1,3", "1,3;5,3;10,3")]
    [Arguments("(?i)aab", "xaab", 1, 3, "1,3", "1,3")]
    [Arguments("(?i)aab", "xaab", 2, 2, "", "")]
    [Arguments("(?i)aab", "xaab", 1, 2, "", "")]
    [Arguments("(?ri)baa", "baax", 0, 3, "0,3", "0,3")]
    [Arguments("(?ri)baa", "baax", 0, 2, "", "")]
    [Arguments("(?ri)baa", "xbaa", 1, 3, "1,3", "1,3")]
    [Arguments("(?ri)baa", "xbaa", 2, 2, "", "")]
    [Arguments("(?ai)kkq", "\u212aKq kKq", 0, -1, "4,3", "4,3")]
    [Arguments("(?i)ab\U00010400", "xab\U00010428", 0, -1, "1,4", "1,4")]
    [Arguments("(?i)ab\U00010400", "x\U00010428ab\U00010428", 0, -1, "3,4", "3,4")]
    [Arguments("(?fi)stq", "\ufb06qx", 0, -1, "0,2", "0,2")]
    [Arguments("(?i)\U00010400\U00010400z", "x\U00010428\U00010400z", 0, -1, "1,5", "1,5")]
    [Arguments("(?r)z\U00010400\U00010400", "az\U00010400\U00010400", 0, -1, "1,5", "1,5")]
    [Arguments("(?i)saq", "\U0001df95aq", 0, -1, "", "")]
    [Arguments("(?i)saq", "x\U0001df95aqsaq", 0, -1, "5,3", "5,3")]
    [Arguments("(?i)[s]saq", "\U0001df95aq", 0, -1, "0,4", "0,4")]
    [Arguments("(?i)[s]saq", "x\u00dfaq", 0, -1, "1,3", "1,3")]
    [Arguments("(?rfi)qst", "xq\ufb06", 0, -1, "1,2", "1,2")]
    [Arguments("(?fi)st\u00e9", "\ufb06\u00c9", 0, -1, "0,2", "0,2")]
    [Arguments("(?rfi)\u00e9st", "\u00c9\ufb06", 0, -1, "0,2", "0,2")]
    public void The_rare_character_search_keeps_every_answer(
        string pattern,
        string subject,
        int beginning,
        int length,
        string found,
        string all
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        Match match = regex.Match(subject, beginning, length);
        (match.Success ? $"{match.Index},{match.Length}" : "").Should().Be(found);
        string.Join(";", regex.Matches(subject, beginning, length).Select(m => $"{m.Index},{m.Length}"))
            .Should()
            .Be(all);
        regex.IsMatch(subject, beginning, length).Should().Be(found.Length > 0);
    }

    /// <summary>
    /// D169. Which required character the screen searches for: the one rarest in the string,
    /// then rarest in English, then the first; never one past a character that a subject
    /// character of another width, or one that takes two required characters, could take. The
    /// rows that stop early are each one of those: sharp s (ss), the st ligature (st, both
    /// directions), dotted capital I (i and a combining dot), an astral letter, and an s at the
    /// window's edge, which U+1DF95 (two units, folding to ss) may take alone with the tail of
    /// its folding.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="anchor">The reading-order index searched for.</param>
    [Test]
    [Arguments("(?i)kkq", 2)]
    [Arguments("(?i)saq", 0)]
    [Arguments("(?i)[s]saq", 0)]
    [Arguments("(?i)xsaq", 3)]
    [Arguments("(?i)aab", 2)]
    [Arguments("(?ai)kkq", 2)]
    [Arguments("(?ri)qkk", 2)]
    [Arguments("(?r)qaa", 2)]
    [Arguments("(?i)abkzz", 2)]
    [Arguments("(?i)ab\U00010400", 2)]
    [Arguments("(?i)ababababcb", 8)]
    [Arguments("(?ri)bcabababab", 8)]
    [Arguments("(?fi)ssz", 0)]
    [Arguments("(?rfi)zss", 0)]
    [Arguments("(?fi)stq", 0)]
    [Arguments("(?rfi)qst", 0)]
    [Arguments("(?fi)st\u00e9", 0)]
    [Arguments("(?rfi)\u00e9st", 0)]
    [Arguments("(?fi)i\u0307q", 0)]
    [Arguments("(?i)\U00010400z", 0)]
    [Arguments("(?i)\U00010400\U00010400z", 0)]
    [Arguments("(?r)z\U00010400\U00010400", 0)]
    [Arguments("(?i)x\U00010400z", 1)]
    public void The_screen_searches_for_the_rarest_character_at_a_fixed_distance(string pattern, int anchor) =>
        new FuzzyRegex(pattern).PatternObject.ReqScreenAnchor.Should().Be(anchor);

    /// <summary>
    /// D169. The fixed distance, checked against the case tables rather than examples, for every
    /// codepoint and every required character before the anchor: no case of the codepoint folds to
    /// a run that could take that character and the next one too, in reading order; and if the
    /// codepoint could take the character alone it is one code unit. It takes it alone one for one,
    /// or with the last folding character of a case (reading order) when that is also the first,
    /// or when the character is the first required one, which a folding may start part-way
    /// through. Each row's anchor is past at least one character that a special can take.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [Test]
    [Arguments("(?i)ahjntwyq")]
    [Arguments("(?ri)qywtnjha")]
    [Arguments("(?fi)ahjntwyq")]
    [Arguments("(?i)kskq")]
    [Arguments("(?i)asazq")]
    [Arguments("(?ri)qtsa")]
    [Arguments("(?i)\u03c3\u03b9\u03c3\u03b9w")]
    public void Every_character_before_the_anchor_takes_one_code_unit_and_one_required_character(string pattern)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;
        Node required = compiled.ReqString!;
        int anchor = compiled.ReqScreenAnchor;
        bool reverse = (compiled.Flags & RegexFlags.Reverse) != 0;
        CaseEncoding encoding = required.Encoding;
        int length = required.Values.Length;
        uint At(int index) => required.Values[reverse ? length - 1 - index : index];

        anchor.Should().BeGreaterThan(1, "the row must put characters in front of the anchor");

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        List<string> wrong = [];

        for (uint ch = 0; ch <= 0x1FFFF; ch++)
        {
            if (ch is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            int count = Encodings.AllCases(encoding, ch, cases);
            for (int j = 0; j < anchor; j++)
            {
                bool alone = Matcher.SameCharIgn(encoding, At(j), ch);
                for (int c = 0; c < count; c++)
                {
                    int foldedLength = Encodings.FullCaseFold(encoding, cases[c], folded);
                    for (int f = 0; f < foldedLength; f++)
                    {
                        if (!Matcher.SameCharIgn(encoding, At(j), folded[f]))
                        {
                            continue;
                        }

                        int after = reverse ? f - 1 : f + 1;
                        int previous = reverse ? f + 1 : f - 1;
                        bool last = after < 0 || after >= foldedLength;
                        bool first = previous < 0 || previous >= foldedLength;
                        if (!last && Matcher.SameCharIgn(encoding, At(j + 1), folded[after]))
                        {
                            wrong.Add($"U+{ch:X4} takes required characters {j} and {j + 1}");
                        }

                        alone |= last && (first || j == 0);
                    }
                }

                if (alone && ch > char.MaxValue)
                {
                    wrong.Add($"U+{ch:X4} takes required character {j} in two code units");
                }
            }
        }

        wrong.Should().BeEmpty();
    }

    /// <summary>
    /// D169. Why no test reaches <c>RequiredStringScreen.ChooseAnchor</c>'s refusal of a required
    /// character that a special of two code units could take alone away from the window's edge:
    /// that needs a case whose full folding is one character in the BMP. A folding outside the BMP
    /// takes only a required character whose closure holds it, which is refused already for
    /// holding a character of two units. No special outside the BMP has one (U+1DF95 folds to "ss",
    /// and to itself under the ASCII encoding). If the tables gain one, this goes red; then write
    /// the witness.
    /// </summary>
    [Test]
    public void No_special_outside_the_bmp_folds_to_one_character_inside_it()
    {
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        List<string> single = [];

        foreach (CaseEncoding encoding in new[] { CaseEncoding.Unicode, CaseEncoding.Ascii })
        {
            foreach (uint special in RequiredStringScreen.Specials.Where(static ch => ch > char.MaxValue))
            {
                int count = Encodings.AllCases(encoding, special, cases);
                for (int c = 0; c < count; c++)
                {
                    if (Encodings.FullCaseFold(encoding, cases[c], folded) == 1 && folded[0] <= char.MaxValue)
                    {
                        single.Add($"{encoding} U+{special:X4}: case U+{cases[c]:X4}");
                    }
                }
            }
        }

        single.Should().BeEmpty();
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
