using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M1, build chunk 10: the <c>(?w)</c> word tests and the reversed grapheme test in a partial
/// call's second pass, whose UAX #29 window can reach the text to come from inside the held text
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.1, rows W1-W6).
/// </summary>
/// <remarks>
/// Each expected answer is the port's own non-partial matcher over every continuation of one or two
/// characters from 'a', 'b', '1', U+05D0, space, ':', '"', ',', LF, CR, U+0301, '_', U+1F1E6,
/// U+200D, U+1F600, U+0915, U+094D, U+30A2 and '.' (<c>tools/probes/d11-port-judge-grid.cs</c> with
/// that alphabet, 2026-10-04). The port's own matcher is the judge for <c>(?w)</c> because upstream's
/// default word boundary differs from UAX #29 (ledger entry 40), and for a reversed <c>\X</c>
/// because upstream's takes one codepoint (ledger entry 43). Upstream 2026.9.10 answers None to every
/// row reported here, which resolves the window against the held text.
/// </remarks>
public sealed class PartialWindowReadTests
{
    /// <summary>
    /// The window reaches the text to come: the test holds for some continuations and not others,
    /// so it passes undetermined and the match is a partial.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately (ledger 21's ruling: a partial is never refused where a
    // continuation completes the match).
    [Test]
    // W1, WB6: 'a:' + 'a' keeps the colon inside the word.
    [Arguments("match", @"(?w)a\B", "a:", 0, 2)]
    // W2: the leftmost start a continuation completes.
    [Arguments("search", @"(?w)a\B", "xa:", 1, 3)]
    // W3, WB4: the combining mark is skipped to reach the character after it, in the text to come.
    [Arguments("match", "(?w)a\\B", "a:\u0301", 0, 3)]
    // W5, WB12: '1,' + '2'.
    [Arguments("match", @"(?w)1\B", "1,", 0, 2)]
    // W6, WB7b: a Hebrew letter, '"' and a Hebrew letter to come.
    [Arguments("match", "(?w)\u05D0\\B", "\u05D0\"", 0, 2)]
    // At the edge: 'a' + 'b'.
    [Arguments("match", @"(?w)a\B", "a", 0, 1)]
    // Under (?a) the default tests are the plain word tests, with their own M1.
    [Arguments("match", @"(?wa)a\B", "a", 0, 1)]
    // An empty text: \m holds when a word character comes.
    [Arguments("match", @"(?w)\m", "", 0, 0)]
    // Reversed, at the edge: 'b' + 'a'.
    [Arguments("match", @"(?wr)\Ba", "a", 0, 1)]
    // Reversed, WB7: the letter before the colon is in the text to come ('b:a').
    [Arguments("match", @"(?wr)\Ba", ":a", 0, 2)]
    // Reversed, WB4: the combining mark belongs to a letter to come ('b' U+0301 'a').
    [Arguments("match", "(?wr)\\Ba", "\u0301a", 0, 2)]
    // Reversed, WB7 across WB4: the letter before the colon is to come, past a mark ('b' U+0301 ':a'),
    // and the mark after the colon is skipped to reach it (':' U+0301 'a').
    [Arguments("match", "(?wr)\\Ba", "\u0301:a", 0, 3)]
    [Arguments("match", "(?wr)\\Ba", ":\u0301a", 0, 3)]
    // Reversed, WB15/WB16: one regional indicator more makes the count before the last one odd.
    [Arguments("match", "(?wr)\\B\U0001F1E6", "\U0001F1E6\U0001F1E6\U0001F1E6", 0, 6)]
    public void A_default_word_test_whose_window_reaches_the_text_to_come_is_undetermined(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = Partial(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    /// <summary>
    /// The controls: where every continuation gives the test the same answer, or a fullmatch needs
    /// the text to end there, no partial comes of it.
    /// </summary>
    // DIVERGES FROM UPSTREAM on nothing: upstream answers None to every row.
    [Test]
    // W4: a full match would need the continuation to be empty.
    [Arguments("fullmatch", @"(?w)a\B", "a:")]
    // \m after a word character, \M after a non-word character: decided false at the edge.
    [Arguments("match", @"(?w)a\m", "a")]
    [Arguments("match", @"(?w) \M", " ")]
    public void A_default_word_test_every_continuation_decides_reports_no_partial(
        string operation,
        string pattern,
        string text
    )
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// Over an empty text the first pass finds no boundary (WB1 and WB2 give none there), but every
    /// continuation makes the position the start (forward) or the end (reversed) of a non-empty
    /// text, which is a break: a decided answer that differs from the first pass, so a partial.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None.
    [Test]
    [Arguments(@"(?w)\b")]
    [Arguments(@"(?wr)\b")]
    public void A_default_boundary_over_an_empty_text_is_a_break_for_every_continuation(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 0));
    }

    /// <summary>
    /// Reversed, GB9c, GB11 and GB12/GB13 look left through a run that can reach the edge, and the
    /// text to come can end the run the way that keeps the cluster together: a Consonant before the
    /// Linker ('क्क' is one cluster), an Extended_Pictographic before the ZWJ, one regional
    /// indicator more. Upstream's reversed <c>\X</c> takes one codepoint (ledger entry 43).
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to every row.
    [Test]
    [Arguments("(?r)(?<!\u094D)\\X", "\u094D\u0915", 0, 2)]
    [Arguments("(?r)(?<!\u200D)\\X", "\u200D\U0001F600", 0, 3)]
    [Arguments("(?r)(?=(?:\U0001F1E6){2}$)\\X", "\U0001F1E6\U0001F1E6\U0001F1E6", 0, 6)]
    public void A_reversed_grapheme_test_whose_run_reaches_the_text_to_come_is_undetermined(
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    /// <summary>The control: a run of regional indicators that stops at a held letter is decided.</summary>
    [Test]
    public void A_reversed_grapheme_test_whose_run_stops_in_the_held_text_is_decided()
    {
        new FuzzyRegex("(?r)(?=(?:\U0001F1E6){2}$)\\X")
            .MatchAtStart("b\U0001F1E6\U0001F1E6\U0001F1E6", partial: true)
            .Success.Should()
            .BeFalse();
    }

    /// <summary>
    /// The enumeration's premise (VERIFICATION rule 10): one representative per combination of
    /// Word_Break, Extended_Pictographic and <c>\w</c> that any code point has, and none more.
    /// </summary>
    [Test]
    public void Every_character_class_the_default_word_rules_read_has_a_representative()
    {
        static (uint, uint, bool) Class(uint c) =>
            (
                UnicodeTables.GetWordBreak(c),
                UnicodeTables.GetExtendedPictographic(c),
                Encodings.HasProperty(CaseEncoding.Unicode, UnicodeTables.PropWord, c)
            );

        var classes = new HashSet<(uint, uint, bool)>();
        for (uint c = 0; c <= 0x10FFFF; c++)
        {
            if (c is < 0xD800 or > 0xDFFF)
            {
                _ = classes.Add(Class(c));
            }
        }

        static string Show(IEnumerable<(uint, uint, bool)> set) =>
            string.Join(" ", set.Distinct().Select(static k => k.ToString()).Order(StringComparer.Ordinal));

        Show(Matcher.DefaultWordRepresentatives.Select(Class)).Should().Be(Show(classes));
        Matcher.DefaultWordRepresentatives.Should().HaveCount(classes.Count);

        // The value masks run from Other to Regional_Indicator, and Other (0) is what the first
        // pass reads past the end of the text.
        classes.Max(static k => k.Item1).Should().Be((uint)UnicodeTables.WbreakRegionalindicator);
        UnicodeTables.WbreakOther.Should().Be(0);
    }

    private static Match Partial(string operation, string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);

        return operation switch
        {
            "match" => regex.MatchAtStart(text, partial: true),
            "fullmatch" => regex.FullMatch(text, partial: true),
            _ => regex.Match(text, partial: true),
        };
    }
}
