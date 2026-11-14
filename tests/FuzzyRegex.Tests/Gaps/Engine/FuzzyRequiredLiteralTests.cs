using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Phase 11 item 8: the required-literal screen for a fuzzy section that opens with something
/// other than a literal, such as <c>(?:(\w+) jumps over){e&lt;=2}</c>. Every match aligns some text
/// inside it with the literal <c> jumps over</c>, within the section's error budget, so a subject
/// holding no such stretch holds no match (design: <c>docs/plan/2026-11-06-grouped-fuzzy-repeats-profile.md</c>).
/// </summary>
public sealed class FuzzyRequiredLiteralTests
{
    private const string _sentence = "the quick brown fox jumps over the lazy dog";
    private const string _nearMiss = "the quick brown fox jumsp ovr the lazy dog";

    // Answers from Python regex 2026.9.29, 2026-11-06 (regex.search, span and fuzzy_counts).
    [Test]
    [Arguments(@"(?:(\w+) jumps over){e<=2}", _sentence, 4, 30, 2, 0, 0)]
    [Arguments(@"(?:(\w+) jumps over){e<=1}", _sentence, 10, 30, 1, 0, 0)]
    [Arguments(@"(?:\w+ jumps over){e<=2}", _sentence, 4, 30, 2, 0, 0)]
    [Arguments(@"(?:(\w+) jumps (\w+)){e<=2}", _sentence, 4, 30, 2, 0, 0)]
    [Arguments(@"(?:(\w+?) jumps over){e<=2}", _sentence, 4, 30, 2, 0, 0)]
    public void A_match_is_what_upstream_finds(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match match = new FuzzyRegex(pattern).Match(subject);

        match.Success.Should().BeTrue();
        (match.Index, match.Index + match.Length).Should().Be((start, end));
        match.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    [Test]
    [Arguments(@"(?:(\w+) jumps over){e<=2}", true)]
    [Arguments(@"(?:(\w+) (?:jumps|leaps) over){e<=2}", true)] // ' over' ends every path
    [Arguments(@"(?i)(?:[a-z]+ lantern works){1i+1d+1s<=2}", true)]
    [Arguments(@"(?:\w+(?: jumps over)?){e<=2}", false)] // optional: a path skips it
    [Arguments(@"(?:\w+ (?:jumps over|leaps)){e<=2}", false)] // inside a branch
    [Arguments(@"(?:\w+(?= jumps) jumps over){e<=2}", false)] // a lookaround in the section
    [Arguments(@"(?:(\w+) \1 jumps over){e<=2}", false)] // a backreference
    [Arguments(@"x(?:\w+ jumps over){e<=2}", false)] // the section does not start the pattern
    [Arguments(@"(?:\w+ jumps over){e<=2}$", false)] // the section is not the whole pattern
    [Arguments(@"(?r)(?:\w+ jumps over){e<=2}", false)] // reversed
    [Arguments(@"(?:\w+ a){e<=2}", false)] // k >= m: every subject has a stretch
    [Arguments(@"(?:jumps over){e<=2}", false)] // all literal: the whole-pattern filter's case
    public void The_screen_is_built_only_for_its_shape(string pattern, bool built)
    {
        (new FuzzyRegex(pattern).PatternObject.RequiredLiteralFilter is not null).Should().Be(built);
    }

    [Test]
    public void The_screen_never_changes_an_answer()
    {
        // The screen only refuses, so a pattern searched with it and without it must give the same
        // answers. Seeded subjects built around each literal: exact, mutated by up to four edits,
        // and plain filler, so both refused and accepted subjects are covered.
        string[] patterns =
        [
            @"(?:(\w+) jumps over){e<=2}",
            @"(?:\w+ jumps over){e<=1}",
            @"(?:(\w+?) jumps over){s<=1,i<=1,d<=1}",
            @"(?:(\w+) (?:jumps|leaps) over){e<=2}",
            @"(?i)(?:([a-z]+) lantern works){1i+1d+1s<=2}",
            @"(?:(?:ab|cd)+ needle){e<=1}",
            @"(?:\d+ haystack){d<=2}",
        ];
        string[] literals =
        [
            " jumps over",
            " jumps over",
            " jumps over",
            " leaps over",
            " LANTERN works",
            " needle",
            " haystack",
        ];
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(20261106);
        var differences = new List<string>();
        int refused = 0;
        for (int p = 0; p < patterns.Length; p++)
        {
            var screened = new FuzzyRegex(patterns[p]);
            var plain = new FuzzyRegex(patterns[p]);
            screened.PatternObject.RequiredLiteralFilter.Should().NotBeNull(patterns[p]);
            plain.PatternObject.RequiredLiteralFilter = null;
            for (int i = 0; i < 400; i++)
            {
                string subject = Subject(random, literals[p]);
                string a = Describe(screened.Matches(subject));
                string b = Describe(plain.Matches(subject));
                if (!string.Equals(a, b, StringComparison.Ordinal))
                {
                    differences.Add($"{patterns[p]} over '{subject}': screened {a}, plain {b}");
                }

                refused += b.Length == 0 ? 1 : 0;
            }
        }
#pragma warning restore CA5394, S2245

        differences.Should().BeEmpty();
        refused.Should().BeGreaterThan(100, "the subjects must include ones with no match");

        static string Describe(MatchCollection matches) =>
            string.Join(
                ";",
                matches.Select(static m =>
                    $"{m.Index},{m.Length},{m.FuzzyCounts},{string.Join("/", m.Groups.Cast<Group>().Skip(1).Select(static g => $"{g.Index}:{g.Length}:{g.Success}"))}"
                )
            );
    }

    [Test]
    [Arguments(FuzzyRegexOptions.None)]
    [Arguments(FuzzyRegexOptions.BestMatch)]
    [Arguments(FuzzyRegexOptions.EnhanceMatch)]
    [Arguments(FuzzyRegexOptions.Posix)]
    [Arguments(FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase)]
    [Arguments(FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase | FuzzyRegexOptions.Version1)]
    public void The_screen_never_changes_an_answer_under_flags_windows_and_overlap(FuzzyRegexOptions options)
    {
        // The same differential as above, widened to what a blind review found unwitnessed: the
        // matching flags, a search window that ends early (the scan's end is the slice end),
        // overlapped searches, and non-ASCII subjects (which must switch the screen off, not
        // refuse).
        string[] patterns = [@"(?:(\w+) jumps over){e<=2}", @"(?:\w+ needle){s<=1,i<=1}", @"(?:(\w+?) haystack){d<=2}"];
        string[] literals = [" jumps over", " needle", " haystack"];
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(20261114 + (int)options);
        var differences = new List<string>();
        for (int p = 0; p < patterns.Length; p++)
        {
            var screened = new FuzzyRegex(patterns[p], options);
            var plain = new FuzzyRegex(patterns[p], options);
            screened.PatternObject.RequiredLiteralFilter.Should().NotBeNull(patterns[p]);
            plain.PatternObject.RequiredLiteralFilter = null;
            for (int i = 0; i < 150; i++)
            {
                string subject = Subject(random, literals[p]);
                if (random.Next(4) == 0)
                {
                    subject = subject.Insert(random.Next(subject.Length + 1), random.Next(2) == 0 ? "é" : "\U0001F600");
                }

                int beginning = random.Next(Math.Max(1, subject.Length / 3));
                int length = random.Next(2) == 0 ? -1 : random.Next(subject.Length - beginning + 1);
                foreach (bool overlapped in (bool[])[false, true])
                {
                    string a = Describe(screened.Matches(subject, beginning, length, overlapped));
                    string b = Describe(plain.Matches(subject, beginning, length, overlapped));
                    if (!string.Equals(a, b, StringComparison.Ordinal))
                    {
                        differences.Add(
                            $"{patterns[p]} over '{subject}' [{beginning},{length},{overlapped}]: screened {a}, plain {b}"
                        );
                    }
                }
            }
        }
#pragma warning restore CA5394, S2245

        differences.Should().BeEmpty();

        static string Describe(MatchCollection matches) =>
            string.Join(
                ";",
                matches.Select(static m =>
                    $"{m.Index},{m.Length},{m.FuzzyCounts},{string.Join("/", m.Groups.Cast<Group>().Skip(1).Select(static g => $"{g.Index}:{g.Length}:{g.Success}"))}"
                )
            );
    }

#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
    private static string Subject(Random random, string literal)
    {
        string[] words = ["the", "quick", "brown", "fox", "ab", "cd", "abcd", "12", "345", "dog", "x9"];
        var text = new System.Text.StringBuilder();
        int count = random.Next(0, 4);
        for (int i = 0; i < count; i++)
        {
            text.Append(words[random.Next(words.Length)]).Append(' ');
        }

        if (random.Next(4) != 0)
        {
            char[] chars = literal.ToCharArray();
            var mutated = new List<char>(chars);
            int edits = random.Next(0, 5);
            for (int e = 0; e < edits && mutated.Count > 0; e++)
            {
                int at = random.Next(mutated.Count);
                switch (random.Next(3))
                {
                    case 0:
                        mutated[at] = (char)random.Next('a', 'z' + 1);
                        break;
                    case 1:
                        mutated.Insert(at, (char)random.Next('a', 'z' + 1));
                        break;
                    default:
                        mutated.RemoveAt(at);
                        break;
                }
            }

            text.Append(words[random.Next(words.Length)]).Append(string.Concat(mutated));
        }

        text.Append(' ').Append(words[random.Next(words.Length)]);
        return text.ToString();
    }
#pragma warning restore CA5394, S2245

    [Test]
    [Arguments(@"(?:(\w+) jumps over){e<=2}")]
    [Arguments(@"(?:\w+ jumps over){e<=2}")]
    [Arguments(@"(?:(\w+?) jumps over){e<=2}")]
    public void A_subject_with_no_stretch_near_the_literal_is_refused_without_an_attempt(string pattern)
    {
        // ' jumps over' is 3 edits from ' jumsp ovr' (Python: '(?: jumps over){e<=2}' finds nothing
        // in it), so no match can align its literal with anything there.
        var regex = new FuzzyRegex(pattern);
        regex.IsMatch(_nearMiss).Should().BeFalse();

        if (!WorkCounter.Enabled)
        {
            return;
        }

        long before = WorkCounter.Steps;
        regex.IsMatch(_nearMiss).Should().BeFalse();
        (WorkCounter.Steps - before).Should().Be(0, "the screen refuses the subject before any attempt");
    }
}
