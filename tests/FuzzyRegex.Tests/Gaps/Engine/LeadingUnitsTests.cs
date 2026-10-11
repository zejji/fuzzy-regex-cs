using System.Buffers;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>PatternObject.LeadingUnits</c>: the code units a search attempt can start on when a pattern
/// opens with an alternation whose branches open with literals, some of them case-insensitive. The
/// search jumps to the next of them before each attempt, so the set must hold the first code unit
/// of every character a match can start with, under simple and full case folding alike. Design:
/// <c>docs/plan/2026-10-11-casefold-first-char-screen.md</c>, option A.
/// </summary>
public sealed class LeadingUnitsTests
{
    /// <summary>
    /// Every subject character a match can start with is admitted. The reference is the engine
    /// itself: a partial match anchored at the start of a one-character subject, which the screen
    /// never runs on, succeeds exactly when a match can begin with that character. Every
    /// codepoint of the BMP and plane 1 (Deseret, Adlam and the other cased astral scripts) is
    /// checked, and every 97th one above. The set must also stay inside what the engine admits
    /// plus the special characters <c>RequiredStringScreen.FirstUnits</c>'s rule adds, so it
    /// cannot quietly grow to "everything". A branch of one character merges with its neighbours
    /// into a set, which the walk does not take, so each pattern keeps one branch longer.
    /// <c>(?fi)st|x</c> and <c>(?fi)k|xy</c> have a <c>CharacterIgn</c> branch.
    /// </summary>
    /// <param name="pattern">A pattern opening with an alternation of literals.</param>
    [Test]
    [Arguments("(?fi)straße|café")]
    [Arguments("(?i)straße|café")]
    [Arguments("(?fi)ss|x")]
    [Arguments("(?fi)st|x")]
    [Arguments("(?fi)ß|xy")]
    [Arguments("(?fi)ẞx|q")]
    [Arguments("(?fi)ſx|q")]
    [Arguments("(?fi)ﬅ|xy")]
    [Arguments("(?fi)ﬆx|q")]
    [Arguments("(?fi)k|xy")]
    [Arguments(@"(?fi)Kx|q")]
    [Arguments("(?fi)i|xy")]
    [Arguments("(?fi)ı|xy")]
    [Arguments("(?fi)İ|xy")]
    [Arguments("(?fi)ᾀ|xy")]
    [Arguments("(?fi)ι|xy")]
    [Arguments("(?fi)ΐx|q")]
    [Arguments("(?fi)µ|xy")]
    [Arguments("(?fi)é|xy")]
    [Arguments(@"(?fi)\U00010400|xy")]
    [Arguments(@"(?fi)\U0001E900x|q")]
    [Arguments("(?-i:Abc)|(?fi:ß)x|(?fi:é)")]
    [Arguments("(?fi)(?:ﬅa|ba)c|(de)")]
    public void Every_character_a_match_can_start_with_is_searched_for(string pattern)
    {
        var regex = new FuzzyRegex(pattern);
        SearchValues<char> units = regex.PatternObject.LeadingUnits!;
        units.Should().NotBeNull();

        var bound = new HashSet<char>();
        foreach (uint special in RequiredStringScreen.Specials)
        {
            bound.Add(char.ConvertFromUtf32((int)special)[0]);
        }

        List<string> missing = [];
        for (int ch = 0; ch <= 0x10FFFF; ch += ch < 0x20000 ? 1 : 97)
        {
            if (ch is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            string subject = char.ConvertFromUtf32(ch);
            if (regex.MatchAtStart(subject, partial: true).Success)
            {
                bound.Add(subject[0]);
                if (!units.Contains(subject[0]))
                {
                    missing.Add($"U+{ch:X4}");
                }
            }
        }

        List<string> extra = [];
        for (int unit = 0; unit <= char.MaxValue; unit++)
        {
            if (units.Contains((char)unit) && !bound.Contains((char)unit))
            {
                extra.Add($"U+{unit:X4}");
            }
        }

        using (new AssertionScope())
        {
            missing.Should().BeEmpty();
            extra.Should().BeEmpty();
        }
    }

    [Test]
    public void A_case_insensitive_alternation_gets_leading_units_and_a_case_sensitive_one_keeps_its_strings()
    {
        PatternObject folded = new FuzzyRegex("(?fi)straße|café").PatternObject;
        PatternObject exact = new FuzzyRegex("straße|café").PatternObject;

        using (new AssertionScope())
        {
            folded.LeadingUnits.Should().NotBeNull();
            folded.LeadingStrings.Should().BeNull();
            exact.LeadingUnits.Should().BeNull();
            exact.LeadingStrings.Should().NotBeNull();
        }
    }

    /// <summary>
    /// The shapes the walk refuses. A fuzzy pattern can delete or replace its first character; a
    /// reversed one reads from the other end; a lone surrogate could be found in the middle of a
    /// pair; a lookbehind or <c>\G</c> at the head is not a literal, and neither is the set that
    /// single-character branches merge into. Under simple case folding the compiler puts an
    /// ignore-case first-set check in front of the branch, which <c>SearchStart</c> already sweeps
    /// for, and the walk does not look through it. LOCALE never gets here: construction rejects
    /// every case-insensitive LOCALE node (<c>LocaleFlagTests</c>).
    /// </summary>
    /// <param name="pattern">A pattern the screen must leave alone.</param>
    [Test]
    [Arguments("(?fi)(?:straße|café){e<=1}")]
    [Arguments("(?fi)straße{e<=1}|café")]
    [Arguments("(?rfi)straße|café")]
    [Arguments(@"(?fi)\udc00x|ßz")]
    [Arguments("(?fi)(?<=a)bc|de")]
    [Arguments(@"(?fi)\Gbc|de")]
    [Arguments("(?fi)bc|")]
    [Arguments("(?fi)ﬅ|x")]
    [Arguments("(?ai)straße|café")]
    public void The_walk_refuses_what_it_cannot_screen(string pattern)
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;

        compiled.LeadingUnits.Should().BeNull();
    }

    /// <summary>
    /// Answers pinned to upstream, each in UTF-16 units. Every one starts on a character the
    /// one-for-one case rule alone would not find (a ligature, long s, sharp s, the Kelvin sign,
    /// a Greek form with iota subscript, an astral case pair) or tests the walk's look-through.
    /// One row departs from upstream on purpose: <c>i</c> + U+0307 over <c>İ</c> matches here and
    /// not upstream, because this port folds <c>İ</c> fully to <c>i</c> + U+0307, as PCRE2, .NET
    /// and Perl's full folding do (<c>Unicode/TurkicDefaults.cs</c>). The <c>K</c> in the Kelvin rows
    /// is U+212A.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="start">The start, or -1 for no match.</param>
    /// <param name="end">The end.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, search, 2026-10-11; spans converted to UTF-16 units")]
    [Arguments("(?fi)straße|café", "ﬅraße", 0, 5)]
    [Arguments("(?fi)straße|café", "xſtrasse", 1, 8)]
    [Arguments("(?fi)straße|café", "Größe STRASSE", 6, 13)]
    [Arguments("(?fi)straße|café", "Größe", -1, -1)]
    [Arguments("(?fi)straße|café", "xxCAFÉ", 2, 6)]
    [Arguments("(?ai)straße|café", "xſtraße", -1, -1)]
    [Arguments("(?i)straße|café", "xſtraße", 1, 7)]
    [Arguments("(?fi)ssx|q", "aßx", 1, 3)]
    [Arguments("(?fi)ssx|q", "aẞx", 1, 3)]
    [Arguments("(?i)k|q", "aK", 1, 2)]
    [Arguments(@"(?fi)i̇x|q", "aİx", 1, 3)]
    [Arguments("(?fi)ᾀ|x", "aἀι", 1, 3)]
    [Arguments("(?i)\U00010400x|y", "a\U00010428x", 1, 4)]
    [Arguments("(?i)\U00010428a|b", "\U00010400a", 0, 3)]
    [Arguments("(?fi)(?:a|b)c|d", "xxBC", 2, 4)]
    [Arguments("(?fi)ᾀ|xy", "aἀι", 1, 3)]
    [Arguments(@"(?fi)Kx|qy", "aKx", 1, 3)]
    [Arguments("(?fi)(?:ab|ßa)c|de", "xxABC", 2, 5)]
    [Arguments("(?fi)(?:ab|ßa)c|de", "xxSSAC", 2, 6)]
    [Arguments("(?fi)(?:ab|ßa)c|de", "xxẞAC", 2, 5)]
    [Arguments("(?fi)(?:ab|ßa)c|de", "xxſSAC", 2, 6)]
    public void A_folded_start_character_is_found(string pattern, string subject, int start, int end)
    {
        Match match = new FuzzyRegex(pattern).Match(subject);

        (match.Success ? (match.Index, match.Index + match.Length) : (-1, -1)).Should().Be((start, end));
    }

    [Test]
    [Property("Upstream", "regex 2026.9.10, search, 2026-10-11")]
    public void A_folded_start_character_opens_the_right_group()
    {
        Match match = new FuzzyRegex("(?fi)(straße)|(café)").Match("..CafÉ");

        using (new AssertionScope())
        {
            (match.Index, match.Length).Should().Be((2, 4));
            match.Groups[1].Success.Should().BeFalse();
            match.Groups[2].Value.Should().Be("CafÉ");
        }
    }

    /// <summary>
    /// A partial match can begin where no branch's first character is: at the end of the subject,
    /// as an empty prefix of every branch. The screen is withheld from partial matching.
    /// </summary>
    [Test]
    [Property("Upstream", "regex 2026.9.10: search(p, 'xx', partial=True) -> (2, 2) partial; 'xxﬅr' -> (2, 4) partial")]
    public void A_partial_search_is_not_screened()
    {
        var regex = new FuzzyRegex("(?fi)straße|café");

        Match empty = regex.Match("xx", partial: true);
        Match ligature = regex.Match("xxﬅr", partial: true);

        using (new AssertionScope())
        {
            (empty.Success, empty.Index, empty.Length, empty.PartialMatch).Should().Be((true, 2, 0, true));
            (ligature.Success, ligature.Index, ligature.Length, ligature.PartialMatch).Should().Be((true, 2, 2, true));
        }
    }

    /// <summary>
    /// A fuzzy pattern can substitute its first character, so a match starts on a character no
    /// branch opens with.
    /// </summary>
    [Test]
    [Property(
        "Upstream",
        "regex 2026.9.10: search('(?fi)(?:straße|café){e<=1}', 'xxtraße') -> (1, 7), one substitution"
    )]
    public void A_fuzzy_alternation_is_not_screened()
    {
        Match match = new FuzzyRegex("(?fi)(?:straße|café){e<=1}").Match("xxtraße");

        (match.Index, match.Length).Should().Be((1, 6));
    }

    /// <summary>
    /// A literal opening with a lone low surrogate is refused: a search for it would stop inside a
    /// surrogate pair, where no attempt ever starts, and the attempt there would read the low half
    /// on its own.
    /// </summary>
    [Test]
    [Property("Upstream", "regex 2026.9.10: search(r'(?fi)\\udc00x|ßz', '\\U00010000x') -> None")]
    public void A_lone_low_surrogate_head_is_not_found_inside_a_pair()
    {
        Match match = new FuzzyRegex(@"(?fi)\udc00x|ßz").Match("\U00010000x");

        match.Success.Should().BeFalse();
    }
}
