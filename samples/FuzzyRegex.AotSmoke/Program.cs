using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fuzzy.Text.RegularExpressions;
using RegexMatchTimeoutException = System.Text.RegularExpressions.RegexMatchTimeoutException;

// Deliberately NOT under Fuzzy.Text.RegularExpressions: this is a consumer, and a namespace
// beginning "FuzzyRegex" would shadow the FuzzyRegex type it is here to exercise.
namespace FuzzyRegexSamples.AotSmoke;

/// <summary>
/// The consumer half of the Native AOT gate (S53): a console app that reaches
/// <c>src/FuzzyRegex</c> through a project reference, is published with <c>PublishAot=true</c>,
/// and asserts a real answer from every feature area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists when the whole suite already runs natively.</b>
/// <c>tools/run-aot-tests.ps1</c> publishes <c>tests/FuzzyRegex.Tests</c> as a native binary and
/// runs every test in it, which is far stronger evidence than any hand-written list of cases. What
/// it cannot show is what a CONSUMER sees: the test project roots three assemblies in the trimmer
/// so its reflection-based audits stay honest, so its binary is not a trimmed binary. This app
/// roots nothing. It is also the only honest place to measure binary size and startup time, which
/// are Phase 7's baseline.
/// </para>
/// <para>
/// <b>Where the expected answers come from.</b> Every one is a real upstream run, printed by
/// <c>tools/probes/aot-smoke-expectations.py</c> against <c>regex 2026.9.10</c> - not from this
/// port's own output, which is the thing under test. Run the probe to reproduce the answer quoted
/// beside each case. Spans are UTF-16 code units, which is what .NET counts; the probe prints both
/// columns because Python counts codepoints, and they differ on the Deseret case.
/// </para>
/// <para>
/// <b>The one case with no upstream counterpart</b> is the match timeout. Upstream has no
/// per-call timeout in the API this port's <c>MatchTimeout</c> mirrors, so that case asserts a
/// .NET API contract - the exception type fires - rather than a parity claim.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Whether this process is the natively published binary rather than a <c>dotnet run</c> of the
    /// same code, so the final line can say which runtime actually produced the answers. The app is
    /// useful under the JIT too - that is how its expected answers were first checked against the
    /// port - and a run that claimed "in a Native AOT binary" either way would be manufacturing the
    /// very evidence this gate exists to produce.
    /// </summary>
    /// <remarks>
    /// This reads correctly only because <c>PublishAot</c> is passed on the publish command line by
    /// <c>tools/run-aot-smoke.ps1</c> and is NOT set in this project's csproj. Set there, the SDK
    /// writes the matching feature switch into the ordinary build's runtimeconfig as well, and a
    /// plain <c>dotnet run</c> then reports dynamic code as unsupported - the honesty check below
    /// claimed a native run that had not happened. S53's blind review found exactly that, and
    /// moving the property is the fix; do not put it back in the csproj.
    /// </remarks>
    private static bool PublishedNatively => !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;

    /// <summary>The named list the <c>\L&lt;animals&gt;</c> case needs, hoisted out of the case.</summary>
    private static readonly Dictionary<string, IReadOnlyCollection<string>> _animals = new()
    {
        ["animals"] = ["cat", "dog"],
    };

    /// <summary>Runs every case and reports the misses.</summary>
    /// <returns><c>0</c> when every case matched its expected answer, <c>1</c> otherwise.</returns>
    internal static int Main()
    {
        TimeSpan toMain = Elapsed();

        // stdout has to carry Greek, Deseret and a ligature for the failure messages to be
        // readable at all; a Windows console defaults to a codepage that cannot encode them.
        Console.OutputEncoding = Encoding.UTF8;

        TimeSpan toConsoleReady = Elapsed();

        List<string> misses = [];
        int checks = 0;
        TimeSpan toFirstAnswer = TimeSpan.Zero;

        foreach ((string area, string expected, Func<string> answer) in Cases())
        {
            checks++;
            if (!Check(area, expected, answer))
            {
                misses.Add(area);
            }

            if (checks == 1)
            {
                toFirstAnswer = Elapsed();
            }
        }

        return Report(checks, misses, toMain, toConsoleReady, toFirstAnswer);
    }

    /// <summary>Runs one case and prints its verdict.</summary>
    /// <param name="area">The feature area's name.</param>
    /// <param name="expected">The answer upstream gave.</param>
    /// <param name="answer">The call to make.</param>
    /// <returns>Whether the answer matched.</returns>
    private static bool Check(string area, string expected, Func<string> answer)
    {
        string actual;
        try
        {
            actual = answer();
        }
        catch (Exception error)
        {
            // A feature area whose Unicode table the ILCompiler dropped throws rather than
            // returning a wrong answer, so an exception is a miss and not a crash. The whole
            // exception is printed, not just its message: under AOT the interesting failures are
            // NotSupportedException and MissingMetadataException, whose frames name the reflection
            // the trimmer removed.
            actual = $"threw {error}";
        }

        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Console.WriteLine($"  ok    {area}");
            return true;
        }

        Console.WriteLine($"  MISS  {area}");
        Console.WriteLine($"          expected  {expected}");
        Console.WriteLine($"          actual    {actual}");
        return false;
    }

    /// <summary>One cumulative timing line.</summary>
    /// <param name="name">What the mark is.</param>
    /// <param name="elapsed">Process start to that mark.</param>
    /// <param name="note">What happened before it.</param>
    /// <returns>The line.</returns>
    /// <remarks>
    /// Padded with <c>PadRight</c>/<c>PadLeft</c> rather than with an interpolation alignment
    /// specifier, because the repo's two formatting gates disagree about one: CSharpier writes
    /// <c>{name, -14}</c> with a space and IDE0055 rejects that space as a formatting error, so a
    /// line using one cannot satisfy both. Measured 2026-09-16 - `dotnet build --configuration
    /// Release`, which is CI's own build step, failed with two IDE0055 errors on this line while
    /// `dotnet csharpier check .` called the same line clean.
    /// </remarks>
    private static string Mark(string name, TimeSpan elapsed, string note)
    {
        string ms = elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

        // Concatenated rather than interpolated: IDE0071 rejects interpolating an expression that
        // is already a string, and these two are.
        return "  " + name.PadRight(14) + ms.PadLeft(9) + " ms   " + note;
    }

    /// <summary>How long this process has been running, from the OS's own start time.</summary>
    /// <returns>The elapsed time.</returns>
    private static TimeSpan Elapsed() => DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

    /// <summary>
    /// Prints the totals, four cumulative timings and the verdict. Four, because one "startup"
    /// number would hide which part of it is the library's: the marks split the process into
    /// runtime init before <c>Main</c>, the console's encoding switch, the first compile and match,
    /// and the rest of the cases.
    /// </summary>
    /// <param name="checks">How many cases ran.</param>
    /// <param name="misses">Which areas answered wrongly.</param>
    /// <param name="toMain">Process start to entering <c>Main</c>.</param>
    /// <param name="toConsoleReady">Process start to the console encoding being set.</param>
    /// <param name="toFirstAnswer">Process start to the first answer.</param>
    /// <returns>The process exit code.</returns>
    private static int Report(
        int checks,
        List<string> misses,
        TimeSpan toMain,
        TimeSpan toConsoleReady,
        TimeSpan toFirstAnswer
    )
    {
        TimeSpan total = Elapsed();

        Console.WriteLine();
        Console.WriteLine($"cases:   {checks}");
        Console.WriteLine($"misses:  {misses.Count}");
        Console.WriteLine(Mark("to Main", toMain, "runtime init, before a line of this app runs"));
        Console.WriteLine(Mark("to console", toConsoleReady, "Console.OutputEncoding = UTF8"));
        Console.WriteLine(Mark("to 1st answer", toFirstAnswer, "first pattern compiled and matched"));
        Console.WriteLine(Mark("to end", total, "all cases, including the timeout case's 50 ms"));

        if (misses.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"FAILED: {string.Join(", ", misses)}");
            return 1;
        }

        Console.WriteLine();

        Console.WriteLine(
            PublishedNatively
                ? "PASSED: every feature area answered correctly in a Native AOT binary."
                : "PASSED: every feature area answered correctly. NOTE: this was a JIT run, not a "
                    + "Native AOT one - publish with tools/run-aot-smoke.ps1 for the gate."
        );
        return 0;
    }

    /// <summary>Every case, in groups only because one method may not be this long.</summary>
    /// <returns>The cases: each one's name, the answer upstream gave, and the call to make.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> Cases() =>
        [
            .. LiteralAndClassCases(),
            .. SteeringCases(),
            .. GroupCases(),
            .. UnicodeCases(),
            .. FuzzyCases(),
            .. RewritingCases(),
            .. ApiCases(),
        ];

    /// <summary>Literals, classes and the two flags that change where a search looks.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> LiteralAndClassCases()
    {
        // regex 2026.9.10: search("world", "hello world") gave span 6,11 and value 'world'.
        yield return ("literals", "6..11 'world'", static () => Span(new FuzzyRegex("world").Match("hello world")));

        // regex 2026.9.10: search("[aeiou]+", "queueing") gave span 1,6 and value 'ueuei'.
        yield return ("classes", "1..6 'ueuei'", static () => Span(new FuzzyRegex("[aeiou]+").Match("queueing")));

        // regex 2026.9.10: the POSIX-flagged alternation over "ab" gave span 0,2 and value 'ab'.
        // Leftmost-LONGEST: without the flag the first alternative wins and the answer is 'a'.
        yield return ("posix", "0..2 'ab'", static () => Span(new FuzzyRegex("(?p)a|ab").Match("ab")));

        // regex 2026.9.10: the reverse-flagged digit run over "a1b22c" gave span 3,5 and value
        // '22'. Searching right to left finds the LAST run, not the first.
        yield return ("reverse", "3..5 '22'", static () => Span(new FuzzyRegex(@"(?r)\d+").Match("a1b22c")));
    }

    /// <summary>The constructs that steer the matcher: lookaround, recursion, conditionals, verbs.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> SteeringCases()
    {
        // regex 2026.9.10: sub of the thousands-separator lookaround pair over "1234567" gave
        // '1,234,567'. Both a lookbehind and a variable-length lookahead in one pattern.
        yield return (
            "lookaround",
            "1,234,567",
            static () => new FuzzyRegex(@"(?<=\d)(?=(?:\d{3})+$)").Replace("1234567", ",")
        );

        // regex 2026.9.10: the balanced-parentheses recursion over "x(a(b)c)y" gave span 1,8 and
        // value '(a(b)c)'. A possessive-quantified recursive call, so it exercises both.
        yield return (
            "recursion",
            "1..8 '(a(b)c)'",
            static () => Span(new FuzzyRegex(@"\((?:[^()]++|(?R))*+\)").Match("x(a(b)c)y"))
        );

        // regex 2026.9.10: the SKIP-then-FAIL verb pattern over "ab" gave span 1,2 and value 'b' -
        // the verbs made the first alternative give up the whole starting position.
        yield return ("verbs", "1..2 'b'", static () => Span(new FuzzyRegex("a(*SKIP)(*FAIL)|b").Match("ab")));

        // regex 2026.9.10: match("abcdef", "abc", partial=True) gave span 0,3, value 'abc' and
        // partial True.
        yield return (
            "partial",
            "0..3 'abc' partial",
            static () =>
            {
                Match match = new FuzzyRegex("abcdef").MatchAtStart("abc", partial: true);
                return $"{Span(match)}{(match.PartialMatch ? " partial" : "")}";
            }
        );
    }

    /// <summary>Conditionals and the two ways a group number can be shared.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> GroupCases()
    {
        // regex 2026.9.10: the yes-branch conditional over "xxab" gave span 2,4, value 'ab' and
        // group 1 'a'.
        yield return (
            "conditionals",
            "2..4 'ab' g1='a'",
            static () =>
            {
                Match match = new FuzzyRegex("(a)?(?(1)b|c)").Match("xxab");
                return $"{Span(match)} g1='{match.Groups[1].Value}'";
            }
        );

        // regex 2026.9.10: the same pattern over "xxc" gave span 2,3, value 'c' and group 1 as
        // Python's None - a group that never took part, which this port reports as unsuccessful.
        yield return (
            "conditionals-else",
            "2..3 'c' g1=unset",
            static () =>
            {
                Match match = new FuzzyRegex("(a)?(?(1)b|c)").Match("xxc");
                return $"{Span(match)} g1={(match.Groups[1].Success ? $"'{match.Groups[1].Value}'" : "unset")}";
            }
        );

        // regex 2026.9.10: the branch reset over "b" gave span 0,1, value 'b' and group 1 'b' -
        // both branches share group 1, so the second branch's capture lands there.
        yield return (
            "branch-reset",
            "0..1 'b' g1='b'",
            static () =>
            {
                Match match = new FuzzyRegex("(?|(a)|(b))").Match("b");
                return $"{Span(match)} g1='{match.Groups[1].Value}'";
            }
        );
    }

    /// <summary>The cases that can only be answered from a generated Unicode table.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> UnicodeCases()
    {
        // regex 2026.9.10: the Greek-property run over "abc" plus alpha-beta-gamma plus "def"
        // gave span 3,6 in both codepoints and UTF-16.
        yield return (
            "unicode-properties",
            "3..6 'αβγ'",
            static () => Span(new FuzzyRegex(@"\p{Greek}+").Match("abcαβγdef"))
        );

        // regex 2026.9.10: the Deseret-property run over "ab" plus U+10400 U+10401 plus "cd" gave
        // codepoint span 2,4 and UTF-16 span 2,6. The two columns differ here on purpose: this is
        // the case that would catch a port counting the wrong unit, and an astral plane is where a
        // table dropped by the trimmer shows up first.
        yield return (
            "unicode-properties-non-bmp",
            "2..6 '\U00010400\U00010401'",
            static () => Span(new FuzzyRegex(@"\p{Deseret}+").Match("ab\U00010400\U00010401cd"))
        );

        // regex 2026.9.10: the GREEK SMALL LETTER ALPHA character-name escape over "x" plus alpha
        // plus "y" gave span 1,2. This is the only construct that consults the character-NAME
        // table, the largest generated table in the library.
        yield return (
            "character-names",
            "1..2 'α'",
            static () => Span(new FuzzyRegex(@"\N{GREEK SMALL LETTER ALPHA}").Match("xαy"))
        );

        // regex 2026.9.10: the FULLCASE-plus-IGNORECASE pattern "fi" over "a" plus U+FB01 plus "b"
        // gave span 1,2. A two-character pattern matching a one-character ligature needs the full
        // case-folding table rather than a simple lowercase.
        yield return ("full-case-folding", "1..2 'ﬁ'", static () => Span(new FuzzyRegex("(?fi)fi").Match("aﬁb")));
    }

    /// <summary>Approximate matching, with its per-error counts and changed positions.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> FuzzyCases()
    {
        // regex 2026.9.10: the two-error foobar pattern over "xxfoxbarxx" gave span 1,8, value
        // 'xfoxbar', fuzzy_counts (1, 1, 0) and fuzzy_changes ([4], [1], []). Upstream's counts
        // tuple is (substitutions, insertions, deletions) and the changes are subject positions.
        yield return (
            "fuzzy-counts-and-changes",
            "1..8 'xfoxbar' s=1 i=1 d=0 subs=[4] ins=[1] dels=[]",
            static () => Fuzzy(new FuzzyRegex("(?:foobar){e<=2}").Match("xxfoxbarxx"))
        );

        // regex 2026.9.10: the same pattern and subject with ENHANCEMATCH gave span 2,8, value
        // 'foxbar', counts (1, 0, 0) and changes ([4], [], []) - the cheaper answer.
        yield return (
            "enhancematch",
            "2..8 'foxbar' s=1 i=0 d=0 subs=[4] ins=[] dels=[]",
            static () => Fuzzy(new FuzzyRegex("(?e)(?:foobar){e<=2}").Match("xxfoxbarxx"))
        );

        // regex 2026.9.10: the three-error foobar pattern with BESTMATCH over "xxfoobrxx" gave
        // span 2,7, value 'foobr', counts (0, 0, 1) and changes ([], [], [6]).
        yield return (
            "bestmatch",
            "2..7 'foobr' s=0 i=0 d=1 subs=[] ins=[] dels=[6]",
            static () => Fuzzy(new FuzzyRegex("(?b)(?:foobar){e<=3}").Match("xxfoobrxx"))
        );
    }

    /// <summary>Substitution and the scans that return more than one answer.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> RewritingCases()
    {
        // regex 2026.9.10: swapping two captured words by template over "hello world" gave
        // 'world hello'.
        yield return (
            "substitution-template",
            "world hello",
            static () => new FuzzyRegex(@"(\w+) (\w+)").Replace("hello world", @"\2 \1")
        );

        // regex 2026.9.10: doubling every digit run by callable over "a1b22c333" gave 'a2b44c666'.
        yield return (
            "substitution-callback",
            "a2b44c666",
            static () =>
                new FuzzyRegex(@"\d+").Replace(
                    "a1b22c333",
                    static match =>
                        (int.Parse(match.Value, CultureInfo.InvariantCulture) * 2).ToString(
                            CultureInfo.InvariantCulture
                        )
                )
        );

        // regex 2026.9.10: subf with the format template "{2} {1}" over "hello world" gave
        // 'world hello'.
        yield return (
            "substitution-format",
            "world hello",
            static () => new FuzzyRegex(@"(\w+) (\w+)").ReplaceFormat("hello world", "{2} {1}")
        );

        // regex 2026.9.10: splitting "a,b;c" on a comma-or-semicolon class gave three pieces:
        // 'a', 'b', 'c'.
        yield return ("split", "a|b|c", static () => string.Join('|', new FuzzyRegex("[,;]").Split("a,b;c")));

        // regex 2026.9.10: the same split with a CAPTURING separator gave five pieces, the
        // separators among them: 'a', ',', 'b', ';', 'c'.
        yield return (
            "split-captures",
            "a|,|b|;|c",
            static () => string.Join('|', new FuzzyRegex("([,;])").Split("a,b;c"))
        );

        // regex 2026.9.10: every digit run in "a1b22c333" was '1', '22', '333'.
        yield return (
            "matches",
            "1|22|333",
            static () => string.Join('|', new FuzzyRegex(@"\d+").Matches("a1b22c333").Select(static m => m.Value))
        );

        // regex 2026.9.10: every overlapping digit pair in "1234" was '12', '23', '34'.
        yield return (
            "overlapped",
            "12|23|34",
            static () =>
                string.Join('|', new FuzzyRegex(@"\d\d").Matches("1234", overlapped: true).Select(static m => m.Value))
        );
    }

    /// <summary>The parts of the surface that are neither matching nor rewriting.</summary>
    /// <returns>The cases.</returns>
    private static IEnumerable<(string Area, string Expected, Func<string> Answer)> ApiCases()
    {
        // regex 2026.9.10: a named list of "cat" and "dog" over "a dog here" gave span 2,5 and
        // value 'dog'. Named lists are compiled into the bytecode as a string set.
        yield return (
            "named-lists",
            "2..5 'dog'",
            static () => Span(new FuzzyRegex(@"\L<animals>", FuzzyRegexOptions.None, _animals).Match("a dog here"))
        );

        // regex 2026.9.10: a named group over "hi there" gave span 0,2, value 'hi' and the group
        // dictionary mapping 'word' to 'hi'.
        yield return (
            "named-groups",
            "0..2 'hi' word='hi'",
            static () =>
            {
                Match match = new FuzzyRegex(@"(?<word>\w+)").Match("hi there");
                return $"{Span(match)} word='{match.Groups["word"].Value}'";
            }
        );

        // regex 2026.9.10: escape of "a.b*c" with special_only gave a backslash before the dot and
        // before the star.
        yield return ("escape", @"a\.b\*c", static () => FuzzyRegex.Escape("a.b*c"));

        // No upstream counterpart: MatchTimeout is this port's .NET-shaped budget, so what is
        // asserted is the API contract - a runaway match is abandoned with
        // RegexMatchTimeoutException - and not a parity claim.
        //
        // The pattern was MEASURED, not assumed, because the textbook one does not work here.
        // With no budget at all (tools/probes/aot-smoke-slow-patterns.cs, Release, 2026-09-16)
        // this port answers the classic `(a+)+$` over 40 a's and a 'b' in 42-47 ms, so a 50 ms
        // budget would not reliably fire and the case would pass or fail by luck. `(a|aa)+$` over
        // the subject below - 32 a's and a 'b', which is the probe's `len=33` row, the same string
        // rather than a similar one - takes 2,400-3,300 ms unbounded over five runs: a margin of
        // at least 48x over the budget, and still bounded, so a regression that stopped the
        // timeout firing shows up as a MISS in a few seconds rather than as a hung smoke test.
        // `(a|a)*$` over 31 a's takes 379,561 ms and is deliberately NOT used here for that reason.
        yield return (
            "match-timeout",
            "RegexMatchTimeoutException",
            static () =>
            {
                FuzzyRegex pattern = new(@"(a|aa)+$", FuzzyRegexOptions.None, TimeSpan.FromMilliseconds(50));
                try
                {
                    pattern.Match(new string('a', 32) + "b");
                    return "no timeout";
                }
                catch (RegexMatchTimeoutException)
                {
                    return "RegexMatchTimeoutException";
                }
            }
        );
    }

    /// <summary>A match's UTF-16 span and value, or that it did not match.</summary>
    /// <param name="match">The match to render.</param>
    /// <returns>The rendering.</returns>
    private static string Span(Match match) =>
        match.Success ? $"{match.Index}..{match.Index + match.Length} '{match.Value}'" : "no match";

    /// <summary>A fuzzy match's span, value, per-type counts and changed positions.</summary>
    /// <param name="match">The match to render.</param>
    /// <returns>The rendering.</returns>
    private static string Fuzzy(Match match)
    {
        if (!match.Success)
        {
            return "no match";
        }

        FuzzyCounts counts = match.FuzzyCounts;
        FuzzyChanges changes = match.FuzzyChanges;

        return $"{Span(match)} s={counts.Substitutions} i={counts.Insertions} d={counts.Deletions} "
            + $"subs=[{string.Join(',', changes.Substitutions)}] "
            + $"ins=[{string.Join(',', changes.Insertions)}] "
            + $"dels=[{string.Join(',', changes.Deletions)}]";
    }
}
