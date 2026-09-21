using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using FuzzyRegexDemo.Wasm;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>
/// The demo sidebar's worked examples, answered by the engine and checked against upstream (S71,
/// extended to one example per feature in S72).
/// </summary>
/// <remarks>
/// <para>
/// The sidebar <b>is</b> the guided tour (ROADMAP, 2026-08-31), so every entry in
/// <c>demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json</c> is a public claim about what this library
/// does. An example that quietly stopped matching would be a broken promise on the front page of
/// the project, and nothing in the browser would fail: the page would render "0 matches" and look
/// like it was working. This file is what notices, on all three CI operating systems.
/// </para>
/// <para>
/// <b>Provenance of every expected span and count below</b>, per the port-slice rule (owner,
/// 2026-09-15): a real upstream run, not this port's own output.
/// <c>tools/probes/demo-examples-expectations.py</c> reads the same shipped JSON - so the examples
/// have one source, not a copy that drifts - and run on 2026-09-19 against <c>regex 2026.9.10</c>
/// it printed, for the feature examples S72 added:
/// </para>
/// <code>
/// Up to one error               (?:colour){e&lt;=1}                    "the color of the collar"
///     matches=1 spans=[[4, 5]]             [0] 'color'   counts(sub,ins,del)=(0, 0, 1)
/// A budget per kind of error    (?:foobar){i&lt;=1,d&lt;=1,s&lt;=1}          "xfoobat"
///     matches=1 spans=[[0, 6]]             [0] 'xfooba'  counts=(1, 1, 1)
/// Weighted cost                 (foobar){i&lt;=1,d&lt;=2,s&lt;=3,2d+1s&lt;4}    "3oifaowefbaoraofuiebofasebfaobfaorfeoaro"
///     matches=3 spans=[[6, 7], [25, 7], [33, 6]]   [0] 'wefbaor' counts=(3, 1, 0)
/// The first match the budget allows  (foobar){e}                     "xirefoabralfobarxie"
///     matches=5 spans=[[0, 6], [6, 6], [12, 6], [18, 1], [19, 0]]   [0] 'xirefo' counts=(6, 0, 0)
/// BestMatch: the closest fit    (foobar){e}  BestMatch               "xirefoabralfobarxie"
///     matches=3 spans=[[11, 5], [16, 3], [19, 0]]  [0] 'fobar'  counts=(0, 0, 1)
/// EnhanceMatch: tighten what was found  (foobar){e}  EnhanceMatch    "xirefoabralfobarxie"
///     matches=6 spans=[[0, 3], [4, 5], [11, 4], [15, 1], [16, 3], [19, 0]]  [0] 'xir' counts=(2, 0, 3)
/// Fuzzy matching against a word list  \b(?:\L&lt;fruit&gt;){e&lt;=1}\b        "aple bananna cherry"
///     namedLists={"fruit": ["apple", "banana", "cherry"]}
///     matches=3 spans=[[0, 4], [5, 7], [13, 6]]    [0] 'aple'   counts=(0, 0, 1)
/// Leftmost-first                a|ab|abc                             "abcd"
///     matches=1 spans=[[0, 1]]             [0] 'a'       counts=(0, 0, 0)
/// POSIX: leftmost-longest       a|ab|abc  Posix                      "abcd"
///     matches=1 spans=[[0, 3]]             [0] 'abc'     counts=(0, 0, 0)
/// Partial: so far, so good      \d{4}-\d{2}-\d{2}                    "2026-09"
///     mode=partial partial=True
///     matches=1 spans=[[0, 7]]             [0] '2026-09' counts=(0, 0, 0)
/// Search from the right         \w+  RightToLeft                     "one two three"
///     matches=3 spans=[[8, 5], [4, 3], [0, 3]]     [0] 'three'  counts=(0, 0, 0)
/// Replace with a template       (?&lt;year&gt;\d{4})-(?&lt;month&gt;\d{2})       "2026-09 and 1999-12"
///     mode=replace replacement='\g&lt;month&gt;/\g&lt;year&gt;'
///     replaced='09/2026 and 12/1999'       matches=2 spans=[[0, 7], [12, 7]]
/// Replace a misspelling         (?:colour){e&lt;=1}  -&gt; 'colour'        "the color of the collar"
///     replaced='the colour of the collar'  matches=1 spans=[[4, 5]]   [0] counts=(0, 0, 1)
/// </code>
/// <para>
/// and, for the four syntax-tour examples S71 shipped and this slice kept unchanged:
/// </para>
/// <code>
/// Named groups and every capture  (?:(?&lt;word&gt;\w+),?\s*)+   "alpha, beta, gamma"
///     matches=1 spans=[[0, 18]]           [0] counts=(0, 0, 0)
///     group 1 (word): participated=True captures=[[0, 5], [7, 4], [13, 5]]
/// Set operations                [\w--[\d]]+                 "abc123def456"
///     matches=2 spans=[[0, 3], [6, 3]]    [0] 'abc'     counts=(0, 0, 0)
/// Unicode properties            \p{Greek}+                  "alpha then αβγ then beta"
///     matches=1 spans=[[11, 3]]           [0] 'αβγ'     counts=(0, 0, 0)
/// Case-insensitive, with a flag \bfuzzy\b  IgnoreCase       "Fuzzy FUZZY fuzzy"
///     matches=3 spans=[[0, 5], [6, 5], [12, 5]]  [0] 'Fuzzy' counts=(0, 0, 0)
/// </code>
/// <para>
/// and, for the test-set example S75 added, run on 2026-09-20 against the same <c>regex 2026.9.10</c>:
/// </para>
/// <code>
/// Edits limited to letters      (?:colour){e&lt;=2:[a-z]}               "colour, color, col our and col0ur"
///     matches=2 spans=[[0, 6], [8, 5]]     [0] 'colour' counts=(0, 0, 0)
/// </code>
/// <para>
/// The example's claim is about the two candidates it does <b>not</b> match, and a subject that
/// holds all four cannot show that on its own: an absent match looks the same as one the search
/// stepped over. <c>tools/probes/s75-example-test-set.py</c> therefore searches each candidate
/// alone, and <c>tools/probes/s75-example-test-set.cs</c> asks this port the same five questions.
/// Both printed the same five rows on 2026-09-20 (<c>diff</c> agreed to the byte):
/// "colour" matches at (0, 6) with no errors, "color" at (0, 5) for one substitution and one
/// deletion, and "col our" and "col0ur" have no match at all, because the only edits that would
/// reach them fall on a space and on a digit, which <c>[a-z]</c> forbids.
/// <see cref="The_test_set_example_misses_the_two_candidates_it_names"/> is that claim as a test.
/// </para>
/// <para>
/// <b>The probe runs upstream under <c>VERSION1</c></b>, because this port defaults to
/// <see cref="FuzzyRegexOptions.Version1"/> where upstream defaults to <c>VERSION0</c> (a
/// deliberate divergence, <c>docs/DIVERGENCES.md</c>). Asking upstream its default-flag question
/// would be asking a different question from the one the page asks: measured 2026-09-18,
/// <c>[\w--[\d]]+</c> against <c>"abc123def456"</c> is 0 matches under <c>VERSION0</c> and 2 under
/// <c>VERSION1</c>, and the sidebar's set-operations example only exists because of that default.
/// </para>
/// <para>
/// <b>The timeout example has no upstream answer to compare spans with</b>, and the probe says so
/// where it would have printed one: the claim it makes is about the demo's own two-second
/// <see cref="DemoEngine.MatchTimeout"/>, not about parity.
/// <see cref="The_timeout_example_is_the_one_row_with_no_upstream_answer"/> pins that, and
/// <see cref="The_timeout_example_really_does_run_out_of_time"/> runs it - because the claim was
/// wrong for as long as nothing ran it. Until 2026-09-19 the example was <c>(a+a+)+b</c> against 36
/// a's, described here as "deliberately exponential"; measured on the published page that day it
/// answered in 75 ms with no match, and upstream answered the same input in 1 ms. Both engines
/// really do optimise that shape away - upstream's 1 ms is its repeat guards and not its
/// required-string prefilter, because it is just as fast on <c>"a" * 28 + "cb"</c>, where a "b" is
/// there to be found. <c>^(a|aa)+$</c> against 36 a's and a b is exponential in both instead: this
/// port spends its whole two-second budget, and upstream <c>regex 2026.9.10</c>, given the same
/// input with <c>timeout=6.0</c>, spent 6.298 s before raising <c>TimeoutError</c> (2.252 s for 34
/// a's, 0.985 s for 32 - the shape of the growth, and why the subject is 37 characters and not 60).
/// </para>
/// </remarks>
public sealed class DemoExamplesTests
{
    /// <summary>
    /// What upstream answered for each example, keyed by title. Spans are UTF-16 code unit offsets,
    /// because the page slices a JavaScript string with them.
    /// </summary>
    private static readonly Dictionary<string, Expected> _upstream = new(StringComparer.Ordinal)
    {
        ["Up to one error"] = new([(4, 5)], (0, 0, 1)),
        ["A budget per kind of error"] = new([(0, 6)], (1, 1, 1)),
        ["Weighted cost"] = new([(6, 7), (25, 7), (33, 6)], (3, 1, 0)),
        ["Edits limited to letters"] = new([(0, 6), (8, 5)], (0, 0, 0)),
        ["The first match the budget allows"] = new([(0, 6), (6, 6), (12, 6), (18, 1), (19, 0)], (6, 0, 0)),
        ["BestMatch: the closest fit"] = new([(11, 5), (16, 3), (19, 0)], (0, 0, 1)),
        ["EnhanceMatch: tighten what was found"] = new([(0, 3), (4, 5), (11, 4), (15, 1), (16, 3), (19, 0)], (2, 0, 3)),
        ["Fuzzy matching against a word list"] = new([(0, 4), (5, 7), (13, 6)], (0, 0, 1)),
        ["Leftmost-first"] = new([(0, 1)], (0, 0, 0)),
        ["POSIX: leftmost-longest"] = new([(0, 3)], (0, 0, 0)),
        ["Partial: so far, so good"] = new([(0, 7)], (0, 0, 0)) { Partial = true },
        ["Search from the right"] = new([(8, 5), (4, 3), (0, 3)], (0, 0, 0)),
        ["Replace with a template"] = new([(0, 7), (12, 7)], (0, 0, 0)) { Replaced = "09/2026 and 12/1999" },
        ["Replace a misspelling"] = new([(4, 5)], (0, 0, 1)) { Replaced = "the colour of the collar" },
        ["Named groups and every capture"] = new([(0, 18)], (0, 0, 0)),
        ["Set operations"] = new([(0, 3), (6, 3)], (0, 0, 0)),
        ["Unicode properties"] = new([(11, 3)], (0, 0, 0)),
        ["Case-insensitive, with a flag"] = new([(0, 5), (6, 5), (12, 5)], (0, 0, 0)),
    };

    [Test]
    [MethodDataSource(typeof(DemoExamples), nameof(DemoExamples.All))]
    public void Answers_what_upstream_answers(DemoExampleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        // The timeout example is the one row with nothing to compare against; the test below is what
        // holds it to its own claim.
        if (string.Equals(row.Key, "timeout", StringComparison.Ordinal))
        {
            return;
        }

        // A missing entry is a failure and not a skip: an example nobody checked against upstream is
        // an unverified claim on the demo's front page, which is the thing this file exists to stop.
        _upstream
            .ContainsKey(row.Title)
            .Should()
            .BeTrue(
                "'{0}' has no upstream answer recorded. Add it to examples.json AND re-run "
                    + "tools/probes/demo-examples-expectations.py, then record what it printed here",
                row.Title
            );

        Expected expected = _upstream[row.Title];
        using JsonDocument answer = JsonDocument.Parse(Run(row));

        answer
            .RootElement.TryGetProperty("error", out JsonElement error)
            .Should()
            .BeFalse("'{0}' must answer, and it said: {1}", row.Title, error.ToString());

        (int, int)[] spans =
        [
            .. answer
                .RootElement.GetProperty("matches")
                .EnumerateArray()
                .Select(static match =>
                    (match.GetProperty("index").GetInt32(), match.GetProperty("length").GetInt32())
                ),
        ];

        using (new AssertionScope())
        {
            spans.Should().Equal(expected.Spans, "the sidebar promises '{0}' finds these spans", row.Title);

            if (spans.Length > 0)
            {
                JsonElement first = answer.RootElement.GetProperty("matches")[0];
                JsonElement counts = first.GetProperty("counts");
                (
                    counts.GetProperty("substitutions").GetInt32(),
                    counts.GetProperty("insertions").GetInt32(),
                    counts.GetProperty("deletions").GetInt32()
                )
                    .Should()
                    .Be(
                        expected.FirstMatchCounts,
                        "the first match's error counts are what makes '{0}' worth showing",
                        row.Title
                    );

                // Absent rather than false on an ordinary match: the flag is only meaningful where
                // the question was asked, and the page reads its absence as "this is a real match".
                first
                    .TryGetProperty("partialMatch", out JsonElement partial)
                    .Should()
                    .Be(expected.Partial, "'{0}' is {1}a partial match", row.Title, expected.Partial ? "" : "not ");

                if (expected.Partial)
                {
                    partial.GetBoolean().Should().BeTrue();
                }
            }

            if (expected.Replaced is not null)
            {
                answer
                    .RootElement.GetProperty("replaced")
                    .GetString()
                    .Should()
                    .Be(expected.Replaced, "'{0}' promises this replacement", row.Title);
            }
        }
    }

    /// <summary>
    /// The capture list of the one example that exists to show it. The spans above would pass
    /// against an engine that kept only the last capture, which is exactly what
    /// <c>System.Text.RegularExpressions</c> does not do and what the example is there to
    /// demonstrate.
    /// </summary>
    [Test]
    public void The_named_group_example_shows_every_capture()
    {
        DemoExampleRow row = Single("Named groups and every capture");
        using JsonDocument answer = JsonDocument.Parse(Run(row));

        JsonElement word = answer
            .RootElement.GetProperty("matches")[0]
            .GetProperty("groups")
            .EnumerateArray()
            .Single(static group =>
                string.Equals(group.GetProperty("name").GetString(), "word", StringComparison.Ordinal)
            );

        (int, int)[] captures =
        [
            .. word.GetProperty("captures")
                .EnumerateArray()
                .Select(static capture =>
                    (capture.GetProperty("index").GetInt32(), capture.GetProperty("length").GetInt32())
                ),
        ];

        captures.Should().Equal([(0, 5), (7, 4), (13, 5)], "upstream reports all three captures of the repeated group");
    }

    /// <summary>
    /// The named-list example's lists reach the engine. Without the <c>namedLists</c> field the same
    /// pattern is a parse error, so this is the difference between the example working and the
    /// sidebar showing an error message.
    /// </summary>
    [Test]
    public void The_named_list_example_needs_its_lists()
    {
        DemoExampleRow row = Single("Fuzzy matching against a word list");

        using JsonDocument without = JsonDocument.Parse(
            DemoEngine.Run(row.Pattern, row.Flags, row.Subject, row.Mode, row.Replacement, namedLists: "")
        );

        using (new AssertionScope())
        {
            row.NamedLists.Should().NotBeNullOrWhiteSpace("the example's lists are what make its pattern legal");
            without
                .RootElement.TryGetProperty("error", out JsonElement error)
                .Should()
                .BeTrue("'{0}' without its lists is a pattern naming a list that does not exist", row.Title);
            error.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    /// <summary>
    /// The test-set example's note names two candidates it misses, "col our" and "col0ur". The
    /// example's own subject cannot show that: a candidate with no match looks exactly like one the
    /// search never reached, so the spans above would be just as green if the test set were ignored
    /// altogether. Each candidate is searched on its own here, which is the question the note asks.
    /// </summary>
    /// <remarks>
    /// Upstream's answers, <c>tools/probes/s75-example-test-set.py</c> against <c>regex 2026.9.10</c>
    /// on 2026-09-20: "colour" at (0, 6) with counts (0, 0, 0), "color" at (0, 5) with (1, 0, 1),
    /// and nothing for "col our" or for "col0ur".
    /// </remarks>
    [Test]
    [Arguments("colour", 0, 6)]
    [Arguments("color", 0, 5)]
    [Arguments("col our", -1, 0)]
    [Arguments("col0ur", -1, 0)]
    public void The_test_set_example_misses_the_two_candidates_it_names(string candidate, int index, int length)
    {
        DemoExampleRow row = Single("Edits limited to letters");
        using JsonDocument answer = JsonDocument.Parse(
            DemoEngine.Run(row.Pattern, row.Flags, candidate, row.Mode, row.Replacement, row.NamedLists)
        );

        (int, int)[] spans =
        [
            .. answer
                .RootElement.GetProperty("matches")
                .EnumerateArray()
                .Select(static match =>
                    (match.GetProperty("index").GetInt32(), match.GetProperty("length").GetInt32())
                ),
        ];

        spans
            .Should()
            .Equal(
                index < 0 ? [] : [(index, length)],
                "'{0}' is what the note claims the budget can and cannot reach",
                candidate
            );
    }

    /// <summary>
    /// The timeout example is the only row the probe cannot answer, and that is a property of the
    /// example rather than an omission. Pinning it here means a future row that quietly arrives
    /// without an upstream answer fails <see cref="Answers_what_upstream_answers"/> instead of
    /// slipping through the same door.
    /// </summary>
    [Test]
    public void The_timeout_example_is_the_one_row_with_no_upstream_answer()
    {
        DemoExampleRow[] unanswered = [.. DemoExamples.All().Where(static row => !_upstream.ContainsKey(row.Title))];

        using (new AssertionScope())
        {
            unanswered.Should().ContainSingle().Which.Key.Should().Be("timeout");
            // Under 100 characters, so a subject that is already exponential cannot also be long
            // (memory rule, owner 2026-09-17).
            unanswered[0].Subject.Length.Should().BeLessThan(100);
        }
    }

    /// <summary>
    /// The timeout example times out. It is the one example whose claim is about the demo rather than
    /// about matching, so it is also the one nothing else can check: its answer is an error message,
    /// and an example that quietly started answering in 75 ms would still look right on the page.
    /// </summary>
    /// <remarks>
    /// Two seconds of wall clock, and worth every one of them: that is exactly what happened, and
    /// this test is what noticed it (see the file's remarks). The assertion is on the engine's own
    /// timeout sentence, not on elapsed time, so a faster machine cannot turn it green by accident.
    /// </remarks>
    [Test]
    public void The_timeout_example_really_does_run_out_of_time()
    {
        DemoExampleRow row = DemoExamples
            .All()
            .Single(static example => string.Equals(example.Key, "timeout", StringComparison.Ordinal));

        using JsonDocument answer = JsonDocument.Parse(Run(row));

        using (new AssertionScope())
        {
            answer
                .RootElement.TryGetProperty("error", out JsonElement error)
                .Should()
                .BeTrue(
                    "'{0}' promises the engine gives up, so an answer means the example is not exponential",
                    row.Title
                );
            error
                .GetString()
                .Should()
                .Contain("timed out", "the sidebar's note is about the timeout and not about some other refusal");
        }
    }

    /// <summary>
    /// One example per feature, every field filled in, and every feature key that the help generator
    /// writes a panel for used exactly once. A number in a test is how the sidebar's shape stays a
    /// decision rather than a drift.
    /// </summary>
    [Test]
    public void The_sidebar_is_one_complete_example_per_feature()
    {
        DemoExampleRow[] examples = [.. DemoExamples.All()];

        using (new AssertionScope())
        {
            examples.Should().HaveCount(19);
            examples.Select(static e => e.Title).Should().OnlyHaveUniqueItems();

            // Every feature the slice promises a sample for, in the order the sidebar shows them and
            // nothing else. The keys are also the help panels tools/build-demo-help.ps1 generates,
            // so a key with no example is a panel nothing opens.
            string[] features =
            [
                "fuzzy",
                "fuzzy-test-set",
                "bestmatch",
                "enhancematch",
                "namedlists",
                "posix",
                "partial",
                "reverse",
                "replace",
                "timeout",
            ];
            examples.Select(static e => e.Key).Where(static key => key.Length > 0).Distinct().Should().Equal(features);

            foreach (DemoExampleRow example in examples)
            {
                example.Title.Should().NotBeNullOrWhiteSpace();
                // The note is the tour's only prose. An example without one is a pattern with no
                // explanation, which teaches nobody anything.
                example.Note.Should().NotBeNullOrWhiteSpace("'{0}' needs a note", example.Title);
                example.Pattern.Should().NotBeNullOrWhiteSpace("'{0}' needs a pattern", example.Title);
                example.Subject.Should().NotBeNullOrWhiteSpace("'{0}' needs a subject", example.Title);
                example.Mode.Should().BeOneOf("", "partial", "replace");
                // A template only means something in replace mode, and a row carrying one it never
                // uses is a row whose JSON says one thing and whose behaviour says another.
                (example.Replacement.Length > 0)
                    .Should()
                    .Be(
                        string.Equals(example.Mode, "replace", StringComparison.Ordinal),
                        "'{0}' has a replacement exactly when it replaces",
                        example.Title
                    );
            }
        }
    }

    /// <summary>
    /// Every example is inside the caps the page and the engine enforce. An example that breached
    /// one would put an error message in front of a visitor who clicked the tour.
    /// </summary>
    [Test]
    public void Every_example_is_within_the_demos_caps()
    {
        using (new AssertionScope())
        {
            foreach (DemoExampleRow example in DemoExamples.All())
            {
                example.Pattern.Length.Should().BeLessThanOrEqualTo(DemoEngine.MaxPatternLength);
                example.Subject.Length.Should().BeLessThanOrEqualTo(DemoEngine.MaxSubjectLength);
                example.Flags.Length.Should().BeLessThanOrEqualTo(DemoEngine.MaxFlagsLength);
                example.Replacement.Length.Should().BeLessThanOrEqualTo(DemoEngine.MaxReplacementLength);
                example.NamedLists.Length.Should().BeLessThanOrEqualTo(DemoEngine.MaxNamedListsLength);
            }
        }
    }

    private static DemoExampleRow Single(string title) =>
        DemoExamples.All().Single(row => string.Equals(row.Title, title, StringComparison.Ordinal));

    /// <summary>The example exactly as the page would post it.</summary>
    private static string Run(DemoExampleRow row) =>
        DemoEngine.Run(row.Pattern, row.Flags, row.Subject, row.Mode, row.Replacement, row.NamedLists);

    /// <summary>Upstream's answer for one example.</summary>
    private sealed record Expected(
        (int Index, int Length)[] Spans,
        (int Substitutions, int Insertions, int Deletions) FirstMatchCounts
    )
    {
        /// <summary>The whole subject after replacement, for the two rows that replace.</summary>
        public string? Replaced { get; init; }

        /// <summary>Whether the first match is a partial one.</summary>
        public bool Partial { get; init; }
    }
}
