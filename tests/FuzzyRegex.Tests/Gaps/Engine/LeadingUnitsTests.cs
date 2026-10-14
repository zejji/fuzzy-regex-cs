using System.Buffers;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

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
    /// <c>(?fi)st|x</c> and <c>(?fi)k|xy</c> have a <c>CharacterIgn</c> branch. The last rows put a
    /// zero-width test in front (C6), each one that holds at the start of a one-character subject
    /// on a word character, so the reference still sees every start.
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
    [Arguments("(?i)cat|dog")]
    [Arguments("(?fi)cat|dog")]
    [Arguments("(?i)kit|sit")]
    [Arguments("(?fi)kit|sit")]
    [Arguments("(?ai)kit|sit")]
    [Arguments("(?i)in|on")]
    [Arguments("(?fi)in|on")]
    [Arguments("(?i)(?:amber|copper|violet)+")]
    [Arguments("(?ai)straße|café")]
    [Arguments(@"(?i)\b(?:kit|sit)\b")]
    [Arguments(@"(?fi)\m(?:straße|café)")]
    [Arguments("(?im)^(?:kit|sit)")]
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
    /// D84: a case-insensitive alternation whose literals have no full-folding special gets a
    /// zero-width ignore-case first-set check in front of the branch (<c>SetUnionIgn</c> with a
    /// step of 0, <c>ParseFunctions.CheckFirstset</c>). The check reads nothing and can only refuse
    /// a position, so the walk looks through it as it does through the case-sensitive one, and the
    /// alternation gets its screen whether the flag is inline, an option, with or without full
    /// case folding, or a named list.
    /// </summary>
    /// <param name="pattern">A case-insensitive alternation of literals.</param>
    /// <param name="options">The options it compiles with.</param>
    [Test]
    [Arguments("cat|dog", FuzzyRegexOptions.IgnoreCase)]
    [Arguments("cat|dog", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)]
    [Arguments("(?i)cat|dog", FuzzyRegexOptions.None)]
    [Arguments("(?fi)cat|dog", FuzzyRegexOptions.None)]
    [Arguments("(?ai)kit|sit", FuzzyRegexOptions.None)]
    [Arguments("(?i)(cat)|(dog)", FuzzyRegexOptions.None)]
    [Arguments("(?ai)(c)at|cow", FuzzyRegexOptions.None)]
    [Arguments("(?i)(?:amber|copper|violet)+", FuzzyRegexOptions.None)]
    [Arguments(@"\L<w>", FuzzyRegexOptions.IgnoreCase)]
    [Arguments(@"\L<w>", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)]
    public void A_case_insensitive_alternation_behind_a_first_set_check_gets_leading_units(
        string pattern,
        FuzzyRegexOptions options
    )
    {
        var lists = new Dictionary<string, IReadOnlyCollection<string>>();
        if (pattern.Contains(@"\L<w>", StringComparison.Ordinal))
        {
            lists["w"] = ["amber", "copper", "violet"];
        }

        PatternObject compiled = new FuzzyRegex(pattern, options, lists).PatternObject;

        using (new AssertionScope())
        {
            compiled.StartNode!.Op.Should().BeOneOf(Opcode.SetUnionIgn, Opcode.CharacterIgn, Opcode.RangeIgn);
            compiled.StartNode.Step.Should().Be(0);
            compiled.LeadingUnits.Should().NotBeNull();
        }
    }

    /// <summary>
    /// The shapes the walk refuses. A fuzzy pattern can delete or replace its first character; a
    /// reversed one reads from the other end; a lone surrogate could be found in the middle of a
    /// pair; a lookbehind or <c>\G</c> at the head is not a literal, and neither is the set that
    /// single-character branches merge into. LOCALE never gets here: construction rejects
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

    /// <summary>
    /// D84's shapes, every match in UTF-16 units. Each subject starts a match on a character an
    /// ordinal ignore-case search would miss (U+212A KELVIN SIGN, U+017F LONG S, sharp s under full
    /// folding, U+0130 for <c>i</c>), and the reversed, partial and overlapped rows check the paths
    /// the screen is withheld from or reads differently. Version 1 is the default, so IgnoreCase
    /// alone folds fully, as <c>regex.V1 | regex.I</c> does. The two U+0130 rows keep this port's
    /// documented answer, not upstream's <c>0-2 6-8</c>: the Turkic pairings are not applied by
    /// default (<c>docs/DIVERGENCES.md</c>, ledger entry 7).
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="options">The options.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Every match's span, <c>start-end</c>, space-separated.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer with regex.V1 unless Version0, 2026-10-14")]
    [Arguments("kit|sit", FuzzyRegexOptions.IgnoreCase, "aKIT ſit Sit", "1-4 5-8 9-12")]
    [Arguments("kit|sit", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase, "aKIT ſit Sit", "1-4 5-8 9-12")]
    [Arguments("(?i)kit|sit", FuzzyRegexOptions.None, "aKIT ſit", "1-4 5-8")]
    [Arguments(
        "strasse|cafe",
        FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase,
        "xx STRAßE CAFE straẞe",
        "3-9 10-14 15-21"
    )]
    [Arguments("strasse|cafe", FuzzyRegexOptions.IgnoreCase, "xx STRAßE CAFE", "3-9 10-14")]
    [Arguments("ssa|xyz", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase, "ßa ẞA SSA", "0-2 3-5 6-9")]
    [Arguments("ssa|xyz", FuzzyRegexOptions.IgnoreCase, "ßa ẞA SSA", "0-2 3-5 6-9")]
    [Arguments("ssa|xyz", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version0, "ßa ẞA SSA", "6-9")]
    [Arguments("in|on", FuzzyRegexOptions.IgnoreCase, "İn ın IN", "6-8")]
    [Arguments("in|on", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase, "İn ın IN", "6-8")]
    [Arguments("(?ai)kit|sit", FuzzyRegexOptions.None, "aKit KIT", "5-8")]
    [Arguments("kit|sit", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.RightToLeft, "aKIT ſit", "5-8 1-4")]
    public void A_case_blind_alternation_finds_every_folded_match(
        string pattern,
        FuzzyRegexOptions options,
        string subject,
        string expected
    )
    {
        string spans = string.Join(
            ' ',
            new FuzzyRegex(pattern, options).Matches(subject).Select(static m => $"{m.Index}-{m.Index + m.Length}")
        );

        spans.Should().Be(expected);
    }

    [Test]
    [Property(
        "Upstream",
        "regex 2026.9.10, 2026-10-14: finditer(r'\\L<w>', 'a\\u212aIT \\u017fIT dog', I, w=[kit, sit, dog]) -> (1, 4), (5, 8), (9, 12); "
            + "finditer(r'\\L<w>', 'STRAßE strasse', I | F, w=[strasse, cafe]) -> (0, 6), (7, 14)"
    )]
    public void A_case_blind_named_list_finds_every_folded_match()
    {
        var simple = new Dictionary<string, IReadOnlyCollection<string>> { ["w"] = ["kit", "sit", "dog"] };
        var full = new Dictionary<string, IReadOnlyCollection<string>> { ["w"] = ["strasse", "cafe"] };

        using (new AssertionScope())
        {
            new FuzzyRegex(@"\L<w>", FuzzyRegexOptions.IgnoreCase, simple)
                .Matches("aKIT ſIT dog")
                .Select(static m => (m.Index, m.Index + m.Length))
                .Should()
                .Equal((1, 4), (5, 8), (9, 12));
            new FuzzyRegex(@"\L<w>", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase, full)
                .Matches("STRAßE strasse")
                .Select(static m => (m.Index, m.Index + m.Length))
                .Should()
                .Equal((0, 6), (7, 14));
        }
    }

    [Test]
    [Property(
        "Upstream",
        "regex 2026.9.10, 2026-10-14: search('cat|dog', 'xxDO', I, partial=True) -> (2, 4) partial; "
            + "search('kit|sit', 'xx\\u212a', I, partial=True) -> (2, 3) partial; "
            + "finditer('aba|bab', 'ABABA', I, overlapped=True) -> (0, 3), (1, 4), (2, 5)"
    )]
    public void A_case_blind_alternation_keeps_its_partial_and_overlapped_answers()
    {
        Match dog = new FuzzyRegex("cat|dog", FuzzyRegexOptions.IgnoreCase).Match("xxDO", partial: true);
        Match kelvin = new FuzzyRegex("kit|sit", FuzzyRegexOptions.IgnoreCase).Match("xxK", partial: true);

        using (new AssertionScope())
        {
            (dog.Index, dog.Length, dog.PartialMatch).Should().Be((2, 2, true));
            (kelvin.Index, kelvin.Length, kelvin.PartialMatch).Should().Be((2, 1, true));
            new FuzzyRegex("aba|bab", FuzzyRegexOptions.IgnoreCase)
                .Matches("ABABA", overlapped: true)
                .Select(static m => (m.Index, m.Index + m.Length))
                .Should()
                .Equal((0, 3), (1, 4), (2, 5));
        }
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
    /// The screen sweeps a run of units once and lets the attempts inside it through unscreened; the
    /// matches past a run of forty, one after a gap and one starting on the last unit of a second
    /// run, are still found.
    /// </summary>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer, 2026-10-11: (50, 54), (74, 80)")]
    public void A_run_of_units_screened_once_still_finds_every_match()
    {
        string subject =
            string.Concat(Enumerable.Repeat("sS", 20)) + "xxxxxxxxxx" + "café" + new string('ſ', 20) + "Straße";

        new FuzzyRegex("(?fi)straße|café")
            .Matches(subject)
            .Select(static m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((50, 54), (74, 80));
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
