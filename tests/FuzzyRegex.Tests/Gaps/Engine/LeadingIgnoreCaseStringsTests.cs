using System.Buffers;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// C6 option B: a case-insensitive alternation of literals is screened with whole strings under
/// <see cref="StringComparison.OrdinalIgnoreCase"/> (<c>PatternObject.LeadingStrings</c>) when .NET's
/// ignore-case rule finds every match this engine's does
/// (<c>RequiredStringScreen.OrdinalIgnoreCaseCovers</c>), and keeps the first-unit screen
/// (<c>LeadingUnits</c>) otherwise. Design: <c>docs/plan/2026-10-14-multi-string-search-design.md</c>,
/// Option B.
/// </summary>
public sealed class LeadingIgnoreCaseStringsTests
{
    /// <summary>
    /// Which screen a pattern gets. The eligible rows hold only characters whose case closure here
    /// is inside .NET's ignore-case class, and under full folding no run a ligature or sharp s can
    /// take. Each refused row breaks exactly one of those: <c>k</c> (U+212A KELVIN SIGN), <c>s</c>
    /// (U+017F LONG S), <c>fi</c>/<c>fl</c>/<c>ff</c> under full folding (a ligature), an astral
    /// character. <c>(?V0i)</c> folds simply, so <c>fi</c> is eligible there; <c>(?ai)</c> uses the ASCII encoding, where <c>k</c> and <c>s</c> have no
    /// third case.
    /// </summary>
    /// <param name="pattern">A pattern opening with an alternation of literals.</param>
    /// <param name="strings">Whether it should get the ignore-case string screen.</param>
    [Test]
    [Arguments("(?i)cat|dog", true)]
    [Arguments("(?fi)cat|dog", true)]
    [Arguments("(?V0i)cat|dog", true)]
    [Arguments("(?ai)kit|sit", true)]
    [Arguments("(?i)in|on", true)]
    [Arguments("(?i)(?:amber|copper|violet)+", true)]
    [Arguments("(?i)café|rêve", true)]
    [Arguments("(?i)λόγος|ab", true)]
    [Arguments("(?V0i)xfiy|qz", true)]
    [Arguments(@"(?i)\b(?:cat|dog)\b", true)]
    [Arguments("(?-i:Kit)|(?i:dog)", true)]
    [Arguments("(?i)kit|dog", false)]
    [Arguments("(?i)sit|dog", false)]
    [Arguments("(?i)fit|dog", false)]
    [Arguments("(?fi)flat|dog", false)]
    [Arguments("(?fi)off|dog", false)]
    [Arguments(@"(?i)\U00010400x|ab", false)]
    public void A_case_insensitive_alternation_gets_strings_only_when_dotnet_finds_every_match(
        string pattern,
        bool strings
    )
    {
        PatternObject compiled = new FuzzyRegex(pattern).PatternObject;

        using (new AssertionScope())
        {
            (compiled.LeadingStrings is not null).Should().Be(strings);
            (compiled.LeadingUnits is not null).Should().Be(!strings);
        }
    }

    /// <summary>
    /// The reference for "finds every match": for each literal, every way of replacing one, two or
    /// three of its characters with one BMP character that the engine still matches at the start
    /// of the subject is found at 0 by an ordinal ignore-case search for the literals. For an
    /// eligible set the search misses none. For each refused one it misses at least one, so the
    /// refusal is needed and the rule is not merely cautious.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="members">Its literals, comma-separated, as the walk collects them.</param>
    /// <param name="eligible">Whether the rule accepts the set.</param>
    [Test]
    [Arguments("(?i)cat|dog", "cat,dog", true)]
    [Arguments("(?i)amber|violet", "amber,violet", true)]
    [Arguments("(?i)café|λόγος", "café,λόγος", true)]
    [Arguments("(?ai)kit|sit", "kit,sit", true)]
    [Arguments("(?V0i)xfiy|qz", "xfiy,qz", true)]
    [Arguments("(?i)kit|dog", "kit,dog", false)]
    [Arguments("(?i)sit|dog", "sit,dog", false)]
    [Arguments("(?i)fit|dog", "fit,dog", false)]
    [Arguments("(?fi)off|dog", "off,dog", false)]
    [Arguments("(?fi)flat|dog", "flat,dog", false)]
    public void An_eligible_set_misses_no_match_and_a_refused_one_would(string pattern, string members, bool eligible)
    {
        var regex = new FuzzyRegex(pattern);
        string[] literals = members.Split(',');
        SearchValues<string> search = SearchValues.Create(literals, StringComparison.OrdinalIgnoreCase);

        List<string> missed = [];
        foreach (string literal in literals)
        {
            for (int width = 1; width <= 3; width++)
            {
                for (int at = 0; at + width <= literal.Length; at++)
                {
                    for (int ch = 0; ch <= char.MaxValue; ch++)
                    {
                        if (char.IsSurrogate((char)ch))
                        {
                            continue;
                        }

                        string subject = string.Concat(literal.AsSpan(0, at), [(char)ch], literal.AsSpan(at + width));
                        if (regex.IsMatchAtStart(subject) && subject.AsSpan().IndexOfAny(search) != 0)
                        {
                            missed.Add($"{literal}: U+{ch:X4} for [{at}, {at + width})");
                        }
                    }
                }
            }
        }

        using (new AssertionScope())
        {
            (regex.PatternObject.LeadingStrings is not null).Should().Be(eligible);
            if (eligible)
            {
                missed.Should().BeEmpty();
            }
            else
            {
                missed.Should().NotBeEmpty();
            }
        }
    }

    /// <summary>
    /// The per-character rule against <see cref="SearchValues{T}"/> itself over the whole BMP: for
    /// every character the rule accepts, a string search built the two ways .NET builds one (a
    /// single literal, and an ASCII literal beside a longer one) finds every member of the
    /// character's case closure. The rule is computed with <see cref="string.Equals(string, string, StringComparison)"/>'s
    /// comparison, so this pins that the search uses the same one. Under the Unicode encoding the
    /// rule refuses exactly <c>k</c>, <c>s</c> and their capitals in ASCII.
    /// </summary>
    [Test]
    public void The_per_character_rule_matches_what_the_string_search_finds()
    {
        List<string> wrong = [];
        List<char> refusedAscii = [];

        for (int ch = 0; ch <= char.MaxValue; ch++)
        {
            if (char.IsSurrogate((char)ch))
            {
                continue;
            }

            if (!RequiredStringScreen.OrdinalIgnoreCaseCovers(CaseEncoding.Unicode, (uint)ch))
            {
                if (ch < 0x80)
                {
                    refusedAscii.Add((char)ch);
                }

                continue;
            }

            string literal = ((char)ch).ToString();
            SearchValues<string> single = SearchValues.Create([literal], StringComparison.OrdinalIgnoreCase);
            SearchValues<string> pair = SearchValues.Create([literal + "q", "xyz"], StringComparison.OrdinalIgnoreCase);
            foreach (char other in CaseClosure((char)ch))
            {
                string subject = other.ToString();
                if (subject.AsSpan().IndexOfAny(single) != 0 || (subject + "q").AsSpan().IndexOfAny(pair) != 0)
                {
                    wrong.Add($"U+{ch:X4} / U+{(int)other:X4}");
                }
            }
        }

        using (new AssertionScope())
        {
            wrong.Should().BeEmpty();
            refusedAscii.Should().Equal('K', 'S', 'k', 's');
        }
    }

    /// <summary>
    /// Answers pinned to upstream: every row starts a match on a character an ordinal ignore-case
    /// search treats differently from this engine (Kelvin sign, long s, sharp s and a ligature
    /// under full folding), or on one it treats the same (final sigma, accented capitals), so a
    /// wrong eligibility call shows here as a lost match.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="expected">Every match's span, <c>start-end</c>, space-separated.</param>
    [Test]
    [Property("Upstream", "regex 2026.9.10, finditer with regex.V1 unless (?V0), 2026-10-15")]
    [Arguments("(?i)kelvin|ohm", "xKelvin OHM", "1-7 8-11")]
    [Arguments("(?i)last|first", "laſt FIRST", "0-4 5-10")]
    [Arguments("(?i)class|dog", "CLAß class", "0-4 5-10")]
    [Arguments("(?i)flat|dog", "ﬂat FLAT", "0-3 4-8")]
    [Arguments("(?i)λόγος|ab", "ΛΌΓΟΣ λόγοσ", "0-5 6-11")]
    [Arguments("(?i)café|ab", "xCAFÉ Café", "1-5 6-10")]
    [Arguments("(?V0i)xfiy|qz", "XﬁY XFIY", "4-8")]
    [Arguments(@"(?i)\b(?:kit|dog)\b", "a KIT DOG", "2-5 6-9")]
    [Arguments("(?i)amber|copper", "AMBER Copper", "0-5 6-12")]
    public void A_case_insensitive_alternation_finds_every_match(string pattern, string subject, string expected)
    {
        string spans = string.Join(
            ' ',
            new FuzzyRegex(pattern).Matches(subject).Select(static m => $"{m.Index}-{m.Index + m.Length}")
        );

        spans.Should().Be(expected);
    }

    /// <summary>
    /// The design note's Option F: an ignore-case string search that keeps stopping close by gives
    /// way to the code units for the rest of the call, since text where every position starts a
    /// literal costs an ignore-case search far more than the units' one sweep per run. Over such
    /// text the search falls back and still finds every match (the control has a class arm, so no
    /// screen); over ordinary text, where hits are far apart, it keeps the strings.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_string_search_that_skips_too_little_falls_back_to_units_and_keeps_every_match()
    {
        EngineWork.SkipUnlessCounting();
        string dense =
            string.Concat(Enumerable.Repeat("tH", 200)) + "th1 " + string.Concat(Enumerable.Repeat("hT", 100)) + "HT2";
        string sparse = string.Concat(Enumerable.Repeat("the quick brown fox jumps over a lazy dog ", 40)) + "th3";
        var screened = new FuzzyRegex(@"(?i)(?:th|ht)\d", FuzzyRegexOptions.None, EngineWork.HangGuard);
        var control = new FuzzyRegex(@"(?i)(?:th|ht|[0-9]x)\d", FuzzyRegexOptions.None, EngineWork.HangGuard);

        using (new AssertionScope())
        {
            screened.PatternObject.LeadingStrings.Should().NotBeNull();
            screened.PatternObject.LeadingFallbackUnits.Should().NotBeNull();
            control.PatternObject.LeadingStrings.Should().BeNull();
            FallbacksBy(() => screened.Count(dense)).Should().BePositive();
            FallbacksBy(() => screened.Count(sparse)).Should().Be(0);
            Spans(screened, dense).Should().Be(Spans(control, dense)).And.Be("400-403 604-607");
            Spans(screened, sparse).Should().Be(Spans(control, sparse));
        }
    }

    /// <summary>
    /// The fall-back units exist only beside ignore-case strings: an ordinal set is cheap per call
    /// and keeps its search, and a set on the units screen has nothing to fall back from.
    /// </summary>
    [Test]
    public void Only_an_ignore_case_string_screen_has_fallback_units()
    {
        using (new AssertionScope())
        {
            new FuzzyRegex("(?i)cat|dog").PatternObject.LeadingFallbackUnits.Should().NotBeNull();
            new FuzzyRegex("cat|dog").PatternObject.LeadingFallbackUnits.Should().BeNull();
            new FuzzyRegex("(?i)kit|dog").PatternObject.LeadingFallbackUnits.Should().BeNull();
        }
    }

    private static long FallbacksBy(Action action)
    {
        long before = WorkCounter.LeadingStringFallbacks;
        action();
        return WorkCounter.LeadingStringFallbacks - before;
    }

    private static string Spans(FuzzyRegex regex, string subject) =>
        string.Join(' ', regex.Matches(subject).Select(static m => $"{m.Index}-{m.Index + m.Length}"));

    private static HashSet<char> CaseClosure(char ch)
    {
        var closure = new HashSet<char> { ch };
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        var pending = new Stack<char>([ch]);
        while (pending.Count > 0)
        {
            int count = Encodings.AllCases(CaseEncoding.Unicode, pending.Pop(), cases);
            for (int i = 0; i < count; i++)
            {
                if (cases[i] <= char.MaxValue && closure.Add((char)cases[i]))
                {
                    pending.Push((char)cases[i]);
                }
            }
        }

        return closure;
    }
}
