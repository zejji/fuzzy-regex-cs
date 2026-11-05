using System.Text;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D145: the fuzzy literal filter in a forward partial search. A partial match runs out at the end
/// of the text, so it starts within <see cref="FuzzyLiteralFilter.PartialReach"/> code units of it,
/// and the search starts each attempt at the earlier of the filter's answer and that tail.
/// </summary>
/// <remarks>
/// Expected values are upstream's, from <c>regex 2026.9.29</c> with <c>(?V1)</c>, recorded
/// 2026-11-05; upstream spans are in code points and are converted to UTF-16 here where the subject
/// holds an astral character. Upstream prints fuzzy counts as (substitutions, insertions, deletions).
/// </remarks>
public sealed class FuzzyLiteralPartialFilterTests
{
    private static string Sig(Match m)
    {
        if (!m.Success)
        {
            return "-";
        }

        string kind = m.PartialMatch ? "P" : "C";
        return $"{kind}({m.Index},{m.Length}) {m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions}";
    }

    private static string Partial(string pattern, string text, int beginning = 0, int length = -1) =>
        Sig(new FuzzyRegex(pattern).Match(text, beginning, length, partial: true));

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "none - gap test")]
    public void A_partial_search_of_a_long_subject_tries_only_the_starts_near_its_end()
    {
        // regex.search(r'(?:a needle in a haystack\.){e<=1}', 'x' * 1000000 + 'a needle in a hays', partial=True)
        //   -> span=(999999, 1000018) counts=(0, 1, 0) partial=True
        // Release before D145: 139-316 ms a call, an attempt at every one of the million starts.
        string subject = new string('x', 1_000_000) + "a needle in a hays";
        var regex = new FuzzyRegex(@"(?:a needle in a haystack\.){e<=1}", FuzzyRegexOptions.None, EngineWork.HangGuard);

        EngineWork.ShouldTakeAtMostSteps(
            () => Sig(regex.Match(subject, partial: true)).Should().Be("P(999999,19) 0,1,0"),
            10_000,
            "only the starts within the partial reach of the end are attempted"
        );
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // regex.search(r'(?V1)(?f)(?i)(?:\ aeaeeeea){e<=2,i<=1}', 'aaeaeeeeacacddd aea eeee', partial=True) -> span=(15, 24) counts=(0, 1, 0) partial=True
    [Arguments(@"(?f)(?i)(?:\ aeaeeeea){e<=2,i<=1}", "aaeaeeeeacacddd aea eeee", "P(15,9) 0,1,0")]
    // An astral character costs two units for one error, so the reach is m + 2k, not m + k: the
    // match reads 11 units, m + k is 10.
    // regex.search(r'(?V1)(?:abcdefgh){e<=2}', 'zzabc\U0001F600\U0001F600defg', partial=True) -> span=(2, 11) counts=(0, 2, 0) partial=True
    [Arguments("(?:abcdefgh){e<=2}", "zzabc\U0001F600\U0001F600defg", "P(2,11) 0,2,0")]
    // regex.search(r'(?V1e)(?:abcdefghij){e<=2}', 'q' * 30 + 'abcdefg', partial=True) -> span=(28, 37) counts=(0, 2, 0) partial=True
    [Arguments("(?e)(?:abcdefghij){e<=2}", "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabcdefg", "P(28,9) 0,2,0")]
    // regex.search(r'(?V1b)(?:abcdefghij){e<=2}', 'q' * 30 + 'abXdefg', partial=True) -> span=(29, 37) counts=(1, 1, 0) partial=True
    [Arguments("(?b)(?:abcdefghij){e<=2}", "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabXdefg", "P(29,8) 1,1,0")]
    // regex.search(r'(?V1e)(?:abcdefghij){e<=2}', 'qq abcdefXhij ' + 'q' * 30 + 'abcdefg', partial=True) -> span=(3, 13) counts=(1, 0, 0) partial=False
    [Arguments("(?e)(?:abcdefghij){e<=2}", "qq abcdefXhij qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabcdefg", "C(3,10) 1,0,0")]
    // regex.search(r'(?V1b)(?:abcdefghij){e<=2}', 'qq abcdXfXhij ' + 'q' * 30 + 'abcdefg', partial=True) -> span=(3, 13) counts=(2, 0, 0) partial=False
    [Arguments("(?b)(?:abcdefghij){e<=2}", "qq abcdXfXhij qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabcdefg", "C(3,10) 2,0,0")]
    // A complete match before a partial one is the answer.
    // regex.search(r'(?V1)(?:abcdefgh){e<=1}', 'zz abcdefgh zz abcd', partial=True) -> span=(2, 11) counts=(0, 1, 0) partial=False
    [Arguments("(?:abcdefgh){e<=1}", "zz abcdefgh zz abcd", "C(2,9) 0,1,0")]
    public void A_forward_partial_search_answers_as_upstream_does(string pattern, string text, string expected)
    {
        Partial(pattern, text).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // Under V0 U+00DF is a lone character node of one value, which U+1DF95, two units, equals ignoring
    // case. The partial below reads 13 units for 9 of the 10 values and one insertion: m + 2k is 12,
    // and the weighted w + 2k is 14. Counting values, the search would start one unit late.
    // regex.search(r'(?V0i)(?:\xdf(?:abcd|wxyz)\xdf(?:efgh|ijkl)){i<=1}', 'q' * 20 + '\U0001F600\U0001DF95abcd\U0001DF95efg', partial=True)
    //   -> span=(20, 30) counts=(0, 1, 0) partial=True, in code points
    [Arguments("(?V0i)")]
    // Under V1 U+00DF is its folding 'ss', two values, and U+1DF95 folds to 'ss'.
    // regex.search(r'(?V1i)(?:\xdf(?:abcd|wxyz)\xdf(?:efgh|ijkl)){i<=1}', the same, partial=True)
    //   -> span=(20, 30) counts=(0, 1, 0) partial=True, in code points
    [Arguments("(?V1i)")]
    public void A_value_that_is_not_ascii_weighs_two_units_of_reach(string flags)
    {
        var regex = new FuzzyRegex(flags + "(?:\u00DF(?:abcd|wxyz)\u00DF(?:efgh|ijkl)){i<=1}");
        regex.PatternObject.FuzzyLiteralFilter.Should().NotBeNull();
        regex.PatternObject.FuzzyLiteralFilter.PartialReach.Should().Be(14);

        Sig(regex.Match("qqqqqqqqqqqqqqqqqqqq\U0001F600\U0001DF95abcd\U0001DF95efg", partial: true))
            .Should()
            .Be("P(20,13) 0,1,0");
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    // The slice ends inside the literal: the partial runs out at endpos, not at the subject's end.
    // regex.search(r'(?V1)(?:abcdefgh){e<=1}', 'q' * 12 + 'abcdefghqq', endpos=16, partial=True) -> span=(11, 16) counts=(0, 1, 0) partial=True
    [Arguments(0, 16, "P(11,5) 0,1,0")]
    // regex.search(r'(?V1)(?:abcdefgh){e<=1}', 'q' * 12 + 'abcdefghqq', pos=2, endpos=16, partial=True) -> span=(11, 16) counts=(0, 1, 0) partial=True
    [Arguments(2, 14, "P(11,5) 0,1,0")]
    public void A_partial_search_of_a_slice_runs_out_at_the_slice_end(int beginning, int length, string expected)
    {
        Partial("(?:abcdefgh){e<=1}", "qqqqqqqqqqqqabcdefghqq", beginning, length).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_partial_scan_yields_the_complete_match_and_then_the_partial_one()
    {
        // [(m.span(), m.fuzzy_counts, m.partial) for m in regex.finditer(r'(?V1)(?:abcdefgh){e<=1}', 'zz abcdefgh zz abcd', partial=True)]
        //   -> [((2, 11), (0, 1, 0), False), ((14, 19), (0, 1, 0), True)]
        new FuzzyRegex("(?:abcdefgh){e<=1}")
            .Matches("zz abcdefgh zz abcd", partial: true)
            .Select(Sig)
            .Should()
            .Equal("C(2,9) 0,1,0", "P(14,5) 0,1,0");
    }

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "none - gap test")]
    public void A_reverse_partial_search_asks_the_filter_only_in_its_first_pass()
    {
        // regex.search(r'(?V1r)(?:abcdefghij){e<=2}', 'efghij' + 'q' * 30, partial=True) -> span=(0, 8) counts=(0, 2, 0) partial=True
        // The first pass looks for a complete match, and the filter refuses it; the partial pass
        // must not ask the reverse filter, which would refuse the partial too.
        const string text = "efghijqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq";
        var regex = new FuzzyRegex("(?r)(?:abcdefghij){e<=2}");
        regex.PatternObject.FuzzyLiteralFilter.Should().NotBeNull();
        long before = WorkCounter.CharactersSearched;
        regex.Match(text).Success.Should().BeFalse();
        long firstPass = WorkCounter.CharactersSearched - before;

        EngineWork.ShouldSearchAtMostCharacters(
            () => Sig(regex.Match(text, partial: true)).Should().Be("P(0,8) 0,2,0"),
            firstPass,
            "the partial pass of a reverse search does not ask the filter"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "none - gap test")]
    // Neither piece, 'abcd' nor 'efgh', occurs whole: the filter answers that no complete match is
    // left, and the search goes on from the tail, 33 - (8 + 2).
    // regex.search(r'(?V1)(?:abcdefgh){e<=1}', 'q' * 30 + 'abc', partial=True) -> span=(29, 33) counts=(0, 1, 0) partial=True
    [Arguments(
        "(?:abcdefgh){e<=1}",
        "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabc",
        "P(29,4) 0,1,0",
        nameof(WorkCounter.FilterNoMatchTails)
    )]
    // 'abcdXfgh' at 30 is one edit from the literal, so the filter answers 30, but the section allows
    // no substitution. The tail, 38 - (8 + 2), is earlier, and the search starts there.
    // regex.search(r'(?V1)(?:abcdefgh){i<=1}', 'q' * 30 + 'abcdXfgh', partial=True) -> span=(37, 38) counts=(0, 1, 0) partial=True
    [Arguments(
        "(?:abcdefgh){i<=1}",
        "qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqabcdXfgh",
        "P(37,1) 0,1,0",
        nameof(WorkCounter.FilterStartsHeldBack)
    )]
    public void The_partial_tail_is_where_the_search_starts(
        string pattern,
        string text,
        string expected,
        string counted
    )
    {
        var regex = new FuzzyRegex(pattern);
        (long heldBefore, long noMatchBefore) = (WorkCounter.FilterStartsHeldBack, WorkCounter.FilterNoMatchTails);
        Sig(regex.Match(text, partial: true)).Should().Be(expected);
        EngineWork.SkipUnlessCounting();
        (
            string.Equals(counted, nameof(WorkCounter.FilterStartsHeldBack), StringComparison.Ordinal)
                ? WorkCounter.FilterStartsHeldBack - heldBefore
                : WorkCounter.FilterNoMatchTails - noMatchBefore
        )
            .Should()
            .BeGreaterThan(0);
    }

    /// <summary>
    /// The premise of <see cref="FuzzyLiteralFilter.PartialReach"/>'s weights: an astral character,
    /// two code units, read without an error is matched against values weighing two or more. It
    /// equals no ASCII character ignoring case, and it folds to two characters or more, or to one
    /// that is not ASCII. Every literal value is from the Basic Multilingual Plane, and a value that
    /// is not ASCII weighs two.
    /// </summary>
    /// <remarks>
    /// Not vacuous: the Unicode 18 tables pair U+1DF95 with U+00DF ignoring case and fold it to
    /// <c>ss</c>, as upstream does: <c>regex.search(r'(?V1i)\u00DF', '\U0001DF95')</c> and
    /// <c>regex.search(r'(?V1fi)ss', '\U0001DF95')</c> both match (0, 1) on <c>regex 2026.9.29</c>,
    /// 2026-11-05. Counting U+00DF as one unit, a weight of plain value counts, was the first
    /// design of <see cref="FuzzyLiteralFilter.PartialReach"/>; this test is what refuted it.
    /// </remarks>
    [Test]
    public void An_astral_character_read_without_an_error_weighs_two_values()
    {
        uint[] buffer = new uint[Math.Max(UnicodeTables.MaxCases, UnicodeTables.MaxFolded)];
        var bad = new List<string>();
        int bmpCases = 0;
        foreach (CaseEncoding encoding in new[] { CaseEncoding.Ascii, CaseEncoding.Unicode })
        {
            for (uint c = 0x10000; c <= 0x10FFFF; c++)
            {
                int cases = Encodings.AllCases(encoding, c, buffer);
                for (int i = 0; i < cases; i++)
                {
                    if (buffer[i] > 0xFFFF)
                    {
                        continue;
                    }

                    bmpCases++;
                    if (buffer[i] <= 0x7F || !Matcher.SameCharIgn(encoding, c, buffer[i]))
                    {
                        bad.Add($"{encoding} case U+{c:X} ~ U+{buffer[i]:X}");
                    }
                }

                if (Encodings.FullCaseFold(encoding, c, buffer) == 1 && buffer[0] <= 0x7F)
                {
                    bad.Add($"{encoding} fold U+{c:X} -> U+{buffer[0]:X}");
                }
            }
        }

        bad.Should().BeEmpty();
        bmpCases.Should().BeGreaterThan(0, "U+1DF95 pairs with U+00DF");
    }

    // ---------------------------------------------------------------------------------------
    // The differential grid: every row answered with the filter and again without it.
    // ---------------------------------------------------------------------------------------

    private static readonly string[] _alpha = ["a", "b", "c", "d", "e", " "];

    private static readonly string[] _noise =
    [
        "a",
        "b",
        "c",
        "d",
        "e",
        " ",
        "x",
        "A",
        "B",
        "\u00E9",
        "\u00DF",
        "K",
        "\U0001F600",
        "\U00010400",
        "s",
        "S",
        "k",
    ];

    private static readonly string[] _constraints =
    [
        "{e<=1}",
        "{e<=2}",
        "{e<=3}",
        "{i<=1}",
        "{i<=2}",
        "{d<=1}",
        "{s<=1}",
        "{s<=2}",
        "{i<=2,d<=1}",
        "{e<=2,i<=1}",
        "{2i+2d+1s<=4}",
        "{1i+1d+1s<=2}",
        "{1<=e<=2}",
        "{i<=3,s<=1}",
        "{e<=1:[a-z]}",
        "{e<=4}",
    ];

    private static readonly string[] _flags =
    [
        "",
        "",
        "",
        "(?i)",
        "(?f)(?i)",
        "(?e)",
        "(?b)",
        "(?p)",
        "(?r)",
        "(?ri)",
        "(?e)(?i)",
        "(?b)(?i)",
    ];

    private static string FullSig(Match m)
    {
        if (!m.Success)
        {
            return "-";
        }

        FuzzyChanges changes = m.FuzzyChanges;
        return Sig(m)
            + $" s:{string.Join(",", changes.Substitutions)} i:{string.Join(",", changes.Insertions)} d:{string.Join(",", changes.Deletions)}";
    }

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "none - gap test")]
    [Arguments(20261105)]
    [Arguments(7)]
    public void Every_partial_search_answers_the_same_with_the_filter_and_without_it(int seed)
    {
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(seed);
        string Pick(string[] from) => from[random.Next(from.Length)];

        string Literal()
        {
            var sb = new StringBuilder();
            int n = random.Next(6, 23);
            for (int i = 0; i < n; i++)
            {
                sb.Append(Pick(_alpha));
            }

            if (random.Next(6) == 0)
            {
                sb.Insert(random.Next(sb.Length + 1), "ss");
            }

            return sb.ToString();
        }

        string Mutate(string s, int edits)
        {
            var sb = new StringBuilder(s);
            for (int e = 0; e < edits; e++)
            {
                int op = random.Next(3);
                int at = random.Next(sb.Length + 1);
                string c = random.Next(5) == 0 ? Pick(_noise) : Pick(_alpha);
                if (op == 0)
                {
                    sb.Insert(at, c);
                }
                else if (at < sb.Length)
                {
                    sb.Remove(at, 1);
                    if (op == 2)
                    {
                        sb.Insert(at, c);
                    }
                }
            }

            return sb.ToString();
        }

        string Noise(int n, bool ascii)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                sb.Append(ascii || random.Next(8) != 0 ? Pick(_alpha) : Pick(_noise));
            }

            return sb.ToString();
        }

        var covered = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Cover(string key) => covered[key] = covered.GetValueOrDefault(key) + 1;
        var differences = new List<string>();
        int rows = 0;
        while (rows < 2500)
        {
            string flags = Pick(_flags);
            int count = random.Next(4) == 0 ? random.Next(2, 4) : 1;
            List<string> literals = [.. Enumerable.Range(0, count).Select(_ => Literal())];
            string pattern =
                flags
                + "(?:"
                + string.Join("|", literals.Select(l => l.Replace(" ", @"\ ", StringComparison.Ordinal)))
                + ")"
                + Pick(_constraints);
            var on = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(10));
            FuzzyLiteralFilter? filter = on.PatternObject.FuzzyLiteralFilter;
            if (filter is null)
            {
                continue;
            }

            var off = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(10));
            off.PatternObject.FuzzyLiteralFilter = null;

            // Noise, maybe a damaged copy, noise, then usually a damaged prefix at the end.
            string literal = Pick([.. literals]);
            if (flags.Contains("(?f)", StringComparison.Ordinal) && random.Next(3) == 0)
            {
                literal = literal.Replace("ss", "\u00DF", StringComparison.Ordinal);
            }

            bool ascii = random.Next(3) != 0;
            var subject = new StringBuilder(Noise(random.Next(0, 40), ascii));
            if (random.Next(3) == 0)
            {
                subject.Append(Mutate(literal, random.Next(0, 4)));
            }

            subject.Append(Noise(random.Next(0, 30), ascii));
            int tail = random.Next(4);
            if (tail <= 2)
            {
                subject.Append(Mutate(literal[..random.Next(0, literal.Length)], random.Next(0, 3)));
                if (tail == 1)
                {
                    subject.Append(Noise(random.Next(0, 3), ascii));
                }
            }

            string text = subject.ToString();
            int beginning = 0;
            int length = text.Length;
            if (random.Next(4) == 0)
            {
                beginning = random.Next(0, text.Length + 1);
                length = random.Next(0, text.Length - beginning + 1);
            }

            bool scan = random.Next(4) == 0;
            bool overlapped = random.Next(5) == 0;
            string Answer(FuzzyRegex regex) =>
                scan
                    ? string.Join(
                        ";",
                        regex.Matches(text, beginning, length, overlapped: overlapped, partial: true).Select(FullSig)
                    )
                    : FullSig(regex.Match(text, beginning, length, partial: true));

            string expected = Answer(off);
            string actual = Answer(on);
            rows++;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                differences.Add(
                    $"{pattern} | {text} | {beginning},{length} scan {scan} | without {expected} | with {actual}"
                );
            }

            // Rule 11: which constructs each mechanism met, read from the states of a search from the
            // start and, in a scan, a search from the end of each complete match.
            long held = 0;
            long noMatch = 0;
            long afterComplete = 0;
            int[] starts =
            [
                beginning,
                .. scan
                    ? on.Matches(text, beginning, length, partial: true)
                        .Where(static m => !m.PartialMatch && m.Length > 0)
                        .Select(static m => m.Index + m.Length)
                    : [],
            ];
            foreach (int from in starts)
            {
                (long held0, long noMatch0) = (WorkCounter.FilterStartsHeldBack, WorkCounter.FilterNoMatchTails);
                _ = on.Match(text, from, beginning + length - from, partial: true);
                long heldHere = WorkCounter.FilterStartsHeldBack - held0;
                long noMatchHere = WorkCounter.FilterNoMatchTails - noMatch0;
                held += heldHere;
                noMatch += noMatchHere;
                afterComplete += from == beginning ? 0 : heldHere + noMatchHere;
            }

            if (flags.Contains("(?r", StringComparison.Ordinal))
            {
                (held + noMatch).Should().Be(0, "a reverse partial search does without the filter");
                continue;
            }

            if (held + noMatch == 0)
            {
                continue;
            }

            if (held > 0)
            {
                Cover("held back");
            }

            if (noMatch > 0)
            {
                Cover("no match tail");
            }

            if (afterComplete > 0)
            {
                Cover("after a complete match");
            }

            Cover("flags " + (flags.Length == 0 ? "none" : flags));
            Cover(count > 1 ? "alternation" : "one literal");
            Cover(filter.HasScan ? "scan" : "no scan");
            Cover(filter.Pieces.Length > 0 ? "pieces" : "no pieces");
            Cover(beginning != 0 || length != text.Length ? "slice" : "whole subject");
            Cover(text.Any(static c => c > 0x7F) ? "non-ASCII subject" : "ASCII subject");
            Cover(text.Any(char.IsSurrogate) ? "astral subject" : "no astral");
            Cover(expected.Contains("P(", StringComparison.Ordinal) ? "partial answer" : "no partial answer");
            Cover(expected.Contains("C(", StringComparison.Ordinal) ? "complete answer" : "no complete answer");
            Cover(scan ? "Matches" : "Match");
        }
#pragma warning restore CA5394, S2245

        differences.Should().BeEmpty();
        if (WorkCounter.Enabled)
        {
            covered
                .Keys.Should()
                .Contain(
                    [
                        "held back",
                        "no match tail",
                        "flags none",
                        "flags (?i)",
                        "flags (?f)(?i)",
                        "flags (?e)",
                        "flags (?b)",
                        "alternation",
                        "scan",
                        "pieces",
                        "slice",
                        "non-ASCII subject",
                        "astral subject",
                        "partial answer",
                        "after a complete match",
                        "Matches",
                    ],
                    string.Join("; ", covered.Select(static kv => $"{kv.Key}={kv.Value}"))
                );
        }
    }
}
