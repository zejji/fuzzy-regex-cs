namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// The matching work the thread-safety tests (S52b) drive the engine with: one family per
/// capability that owns a distinct amount of per-call state, each with its own subjects, and each
/// rendering its answer as a string so two runs can be compared for equality.
/// </summary>
/// <remarks>
/// <para>
/// The families are the ones S52b's slice file names - plain, fuzzy, <c>(?e)</c>, <c>(?b)</c>,
/// POSIX, partial, <c>(*SKIP)</c>, named lists, <c>sub</c>, <c>split</c> and <c>finditer</c> -
/// because those are where the deepest backtracking stacks and the most per-call bookkeeping live.
/// </para>
/// <para>
/// The subjects are generated from a fixed seed rather than read out of a recorded oracle wave.
/// The wave files hold (pattern, subject) pairs chosen to make this port and upstream disagree,
/// which is a different question from "does one pattern answer the same under load"; what these
/// tests need is a subject set that reaches each family's constructs and is reproducible from this
/// file alone, with no embedded resource to drift out of step. Each family therefore mixes
/// hand-written subjects that certainly match with generated ones over an alphabet that includes
/// astral characters, newlines and the family's own literals.
/// </para>
/// </remarks>
internal static class ThreadSafetyWorkload
{
    /// <summary>One capability family: a pattern, the subjects to run it against, and the call.</summary>
    /// <param name="Name">What the family is called in a failure message.</param>
    /// <param name="Pattern">The pattern source.</param>
    /// <param name="Options">The options to compile it with.</param>
    /// <param name="NamedLists">The <c>\L&lt;name&gt;</c> values, or null.</param>
    /// <param name="Subjects">The subjects to run it against.</param>
    /// <param name="Run">Renders one answer as a string.</param>
    internal sealed record Family(
        string Name,
        string Pattern,
        FuzzyRegexOptions Options,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? NamedLists,
        IReadOnlyList<string> Subjects,
        Func<FuzzyRegex, string, string> Run
    )
    {
        /// <summary>Compiles this family's pattern. A fresh instance every time, deliberately.</summary>
        /// <returns>The compiled pattern.</returns>
        internal FuzzyRegex Compile() =>
            NamedLists is null ? new FuzzyRegex(Pattern, Options) : new FuzzyRegex(Pattern, Options, NamedLists);
    }

    /// <summary>Every family, in a fixed order.</summary>
    internal static IReadOnlyList<Family> Families { get; } = BuildFamilies();

    /// <summary>How many (family, subject) pairs the whole workload is.</summary>
    internal static int PairCount => Families.Sum(static family => family.Subjects.Count);

    /// <summary>
    /// A match rendered so that every field a race could corrupt is in the string: the span, the
    /// text, every group's span and text, the fuzzy counts and changes, and the partial flag.
    /// </summary>
    /// <param name="match">The match to render.</param>
    /// <returns>The rendering.</returns>
    internal static string Render(Match match)
    {
        if (!match.Success)
        {
            return "-";
        }

        var text = new System.Text.StringBuilder();
        text.Append(match.Index)
            .Append(':')
            .Append(match.Length)
            .Append(':')
            .Append(match.Value)
            .Append(match.PartialMatch ? "|partial" : "|whole");

        foreach (Group group in match.Groups)
        {
            text.Append('|')
                .Append(group.Name)
                .Append('=')
                .Append(group.Success ? $"{group.Index}:{group.Length}:{group.Value}" : "-");

            foreach (Capture capture in group.Captures)
            {
                text.Append(';').Append(capture.Index).Append(':').Append(capture.Value);
            }
        }

        FuzzyCounts counts = match.FuzzyCounts;
        text.Append("|counts=")
            .Append(counts.Substitutions)
            .Append(',')
            .Append(counts.Insertions)
            .Append(',')
            .Append(counts.Deletions);

        // Read through the cached FuzzyChanges property deliberately: it is the one lazily
        // computed value a Match owns, so it is the one a second thread could see half-written.
        FuzzyChanges changes = match.FuzzyChanges;
        text.Append("|changes=")
            .Append(string.Join(',', changes.Substitutions))
            .Append('/')
            .Append(string.Join(',', changes.Insertions))
            .Append('/')
            .Append(string.Join(',', changes.Deletions));

        return text.ToString();
    }

    /// <summary>Builds the family table.</summary>
    /// <returns>The families.</returns>
    private static IReadOnlyList<Family> BuildFamilies()
    {
        const string letters = "abcdefgh";
        const string mixed = "abcXY 019\n\t";

        return
        [
            new Family(
                "plain",
                @"(\w+)\s+(\w+)",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(1, mixed, ["hello world", "one  two three", "", "\n\n", "a b"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "fuzzy",
                "(?:fuzzy){e<=2}",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(2, "fuzzyabc ", ["fuzzy", "fuzy", "fzzy", "xxfuzzZyxx", "nothing"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "enhancematch",
                "(?e)(?:kitten){i<=2,d<=2,s<=2,e<=3}",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(3, "kitensg ", ["kitten", "sitting", "kittn", "kiten", "zzz"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "bestmatch",
                "(?b)(?:foobar){e<=3}",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(4, "foobar ", ["foobar", "fobar", "fooxbar", "ffoobbar", "qqq"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "posix",
                "(?p)(a|ab)(c|bcd)(d*)",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(5, "abcd", ["abcd", "abcdd", "ac", "abcdxyz", ""]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "partial",
                @"(\d{4})-(\d{2})-(\d{2})",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(6, "0123-", ["2026-09-16", "2026-09", "2026-", "2026", ""]),
                static (pattern, subject) => Render(pattern.Match(subject, partial: true))
            ),
            new Family(
                "skip",
                "(?:a+(*SKIP)b|ac)",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(7, "abc", ["aaab", "aac", "aaac", "abab", "ccc"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "named-lists",
                @"\L<words>",
                FuzzyRegexOptions.IgnoreCase,
                new Dictionary<string, IReadOnlyCollection<string>> { ["words"] = ["alpha", "beta", "gamma"] },
                GenerateSubjects(8, "alphbetgm ", ["alpha", "BETA", "gamma", "delta", ""]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "sub",
                "([aeiou])",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(9, letters, ["aeiou", "rhythm", "", "banana"]),
                static (pattern, subject) => pattern.Replace(subject, "<$1>")
            ),
            new Family(
                "split",
                @"[\s,]+",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(10, "ab, \t", ["a, b,c  d", ",,,", "", "abc"]),
                static (pattern, subject) => string.Join('', pattern.Split(subject))
            ),
            // Recursion and a counted repeat are here for the reflection walk rather than for the
            // stress test: PatternObject's CallRefInfo and RepeatInfo lists stay empty unless a
            // pattern calls a group or repeats one a bounded number of times, and a field the walk
            // never reaches is a field the allowlist cannot pin.
            new Family(
                "recursion",
                @"(?<paren>\((?:[^()]|(?&paren))*\))",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(12, "()ab", ["(a)", "((a))", "(a(b)c)", "(((", ""]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "repeat",
                "(?:(a)|(b)){2,5}c",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(13, "abc", ["aabc", "ababc", "aaaaac", "ac", "c"]),
                static (pattern, subject) => Render(pattern.Match(subject))
            ),
            new Family(
                "finditer",
                "(a)(b)?",
                FuzzyRegexOptions.None,
                null,
                GenerateSubjects(11, "abc", ["ababab", "aaa", "", "bbb", "abcabc"]),
                static (pattern, subject) => string.Join('', pattern.Matches(subject).Select(Render))
            ),
        ];
    }

    /// <summary>
    /// A family's subjects: the hand-written ones that certainly reach the pattern's constructs,
    /// then generated ones over the family's own alphabet plus astral characters and newlines.
    /// </summary>
    /// <param name="seed">The generator seed, fixed per family so the set never moves.</param>
    /// <param name="alphabet">The characters this family's pattern cares about.</param>
    /// <param name="fixedSubjects">The hand-written subjects.</param>
    /// <returns>The subjects.</returns>
    private static List<string> GenerateSubjects(uint seed, string alphabet, string[] fixedSubjects)
    {
        // Astral characters are in the alphabet because a surrogate pair is the shape most likely
        // to be split by a wrongly computed index, and a newline because it changes what the
        // anchors and the line separators do.
        string[] extras = ["\U0001F600", "\U00010400", "\n", "\r\n", "İ", "ı"];

        uint state = seed;
        var subjects = new List<string>(fixedSubjects);

        for (int i = 0; i < 36; i++)
        {
            int length = (int)(Next(ref state) % 24);
            var text = new System.Text.StringBuilder();

            for (int c = 0; c < length; c++)
            {
                text.Append(
                    Next(ref state) % 10 == 0
                        ? extras[Next(ref state) % extras.Length]
                        : alphabet[(int)(Next(ref state) % alphabet.Length)].ToString()
                );
            }

            subjects.Add(text.ToString());
        }

        return subjects;
    }

    /// <summary>
    /// One step of a 32-bit xorshift, which is the generator these subjects come from.
    /// </summary>
    /// <remarks>
    /// Written out rather than taken from <see cref="Random"/> because <see cref="Random"/>'s
    /// sequence for a given seed is not contractually stable across .NET versions, and a subject
    /// set that changed under the runtime would make a red stress test impossible to reproduce.
    /// Nothing here is security-sensitive: these are test inputs.
    /// </remarks>
    /// <param name="state">The generator state, advanced in place. Never zero.</param>
    /// <returns>The next value.</returns>
    private static uint Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;

        return state;
    }
}
