using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D116: a BESTMATCH pattern whose lookarounds against the scan hold no fuzzy item and no call
/// keeps the walk memo (F107), and answers exactly as it does with the memo withheld, which is how
/// main before D116 ran every such pattern.
/// </summary>
/// <remarks>
/// <para>
/// Random <c>(?b)</c> and <c>(?br)</c> patterns over short texts of <c>' abx'</c>, from the
/// generator of the D116 design note (<c>docs/plan/2026-11-16-d116-bestmatch-lookbehind-design.md</c>,
/// section 3): the lookaround outside the fuzzy section, inside it as a plain lookaround, and
/// inside it as a conditional's test. Each row runs twice, as compiled and with
/// <see cref="PatternObject.LooksBackAtTheAnchor"/> forced on, and the answers must agree.
/// </para>
/// <para>
/// A grid is evidence only where the memo fired (<c>docs/VERIFICATION.md</c> rule 11), so each
/// mode also counts the rows whose Debug step count the memo changed and requires a share of them.
/// Main before D116 withheld the memo from every one of these rows, so it fails that count.
/// </para>
/// </remarks>
public sealed class BestMatchWalkMemoGridTests
{
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("outside", 4_000)]
    [Arguments("insideplain", 4_000)]
    [Arguments("insidecond", 4_000)]
    public void An_exact_lookaround_against_the_scan_keeps_the_walk_memo_without_moving_an_answer(string mode, int rows)
    {
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var rng = new Random(116);
        bool inside = mode.StartsWith("inside", StringComparison.Ordinal);
        bool plainOnly = string.Equals(mode, "insideplain", StringComparison.Ordinal);
        bool condOnly = string.Equals(mode, "insidecond", StringComparison.Ordinal);
        bool reversed = false;

        string Pick(params string[] choices) => choices[rng.Next(choices.Length)];
        string Atom() => Pick("a", "b", "x", "[ab]", ".", @"\s", "ab", "ba", "a?", "b*", "(?:a|bx)", "(?>a+)", "x+?");
        string Seq(int n) => string.Concat(Enumerable.Range(0, n).Select(_ => Atom()));
        string Fuzz() => Pick("{e<=1}", "{i<=1}", "{s<=1}", "{d<=1}", "{i<=1,d<=1}", "{e<=2}");
        string Look()
        {
            string body = Seq(rng.Next(1, 3));
            string against = reversed ? Pick("(?=", "(?!") : Pick("(?<=", "(?<!");
            return condOnly || (!plainOnly && rng.Next(4) == 0)
                ? $"(?{against}{body})" + Seq(1) + "|" + Seq(1) + ")"
                : against + body + ")";
        }

        int memoActive = 0;
        List<string> changed = [];
        for (int i = 0; i < rows; i++)
        {
            reversed = rng.Next(5) == 0;
            string flags = reversed ? "(?br)" : "(?b)";
            string fuzzy = Seq(rng.Next(1, 4));
            string look = Look();
            string pattern = inside
                ? flags
                    + Pick(
                        $"(?:{look}{fuzzy}){Fuzz()}",
                        $"(?:{fuzzy}{look}|{Seq(1)}){Fuzz()}",
                        $"(?:{fuzzy}{look}{Seq(1)}){Fuzz()}"
                    )
                : flags
                    + Pick(
                        $"{look}(?:{fuzzy}){Fuzz()}",
                        $"(?:{fuzzy}){Fuzz()}{look}",
                        $"(?:{fuzzy}){Fuzz()}{look}{Seq(1)}",
                        $"(?:{fuzzy}){Fuzz()}|{look}{Seq(1)}"
                    );
            string text = new([.. Enumerable.Range(0, rng.Next(3, 14)).Select(_ => " abxab"[rng.Next(6)])]);

            (string asCompiled, long compiledSteps) = Run(pattern, text, withhold: false);
            (string withheld, long withheldSteps) = Run(pattern, text, withhold: true);
            if (compiledSteps != withheldSteps)
            {
                memoActive++;
            }

            if (!string.Equals(asCompiled, withheld, StringComparison.Ordinal))
            {
                changed.Add($"{pattern} '{text}': {asCompiled} / {withheld}");
            }
        }

#pragma warning restore CA5394, S2245

        changed.Should().BeEmpty("the memo only skips attempts that fail in both searches");
        EngineWork.SkipUnlessCounting();

        // The same generator fires the memo on 14-20% of 20,000-row grids at three seeds (the D116
        // row of docs/KNOWN-DEFECTS.md), so one row in 20 is a floor well below what it measures.
        memoActive.Should().BeGreaterThan(rows / 20, "the grid must exercise the memo to say anything about it");
    }

    private static (string Answer, long Steps) Run(string pattern, string text, bool withhold)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        if (withhold)
        {
            // Main before D116: every lookaround against the scan withheld the memo.
            regex.PatternObject.LooksAgainstTheScan = true;
            regex.PatternObject.LooksBackAtTheAnchor = true;
        }

        long before = WorkCounter.Steps;
        string answer = string.Join(
            '|',
            regex
                .Matches(text)
                .Select(static m =>
                    $"{m.Index},{m.Length},{m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions}"
                )
        );
        return (answer, WorkCounter.Steps - before);
    }
}
