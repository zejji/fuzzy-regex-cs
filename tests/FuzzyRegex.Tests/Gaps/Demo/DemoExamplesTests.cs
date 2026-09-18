using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using FuzzyRegexDemo.Wasm;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>
/// The demo sidebar's eight worked examples, answered by the engine and checked against upstream
/// (S71).
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
/// have one source, not a copy that drifts - and run on 2026-09-18 against <c>regex 2026.9.10</c>
/// it printed:
/// </para>
/// <code>
/// Up to two errors              (?:colour){e&lt;=2}            "the color of the collar"
///     matches=2 spans=[[3, 6], [17, 6]]   [0] ' color'  counts(sub,ins,del)=(0, 1, 1)
/// Per-error-type limits         (?:foobar){i&lt;=1,d&lt;=1,s&lt;=1}  "xfoobat"
///     matches=1 spans=[[0, 6]]            [0] 'xfooba'  counts=(1, 1, 1)
/// Best match, not first match   (?:kitten){e&lt;=3}  BestMatch  "the sitting kitten"
///     matches=1 spans=[[12, 6]]           [0] 'kitten'  counts=(0, 0, 0)
/// Enhanced match                (?:organise){e&lt;=3}  EnhanceMatch  "the organisers organized it"
///     matches=2 spans=[[4, 8], [15, 8]]   [0] 'organise' counts=(0, 0, 0)
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
/// <b>The probe runs upstream under <c>VERSION1</c></b>, because this port defaults to
/// <see cref="FuzzyRegexOptions.Version1"/> where upstream defaults to <c>VERSION0</c> (a
/// deliberate divergence, <c>docs/DIVERGENCES.md</c>). Asking upstream its default-flag question
/// would be asking a different question from the one the page asks: measured 2026-09-18,
/// <c>[\w--[\d]]+</c> against <c>"abc123def456"</c> is 0 matches under <c>VERSION0</c> and 2 under
/// <c>VERSION1</c>, and the sidebar's set-operations example only exists because of that default.
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
        ["Up to two errors"] = new([(3, 6), (17, 6)], (0, 1, 1)),
        ["Per-error-type limits"] = new([(0, 6)], (1, 1, 1)),
        ["Best match, not first match"] = new([(12, 6)], (0, 0, 0)),
        ["Enhanced match"] = new([(4, 8), (15, 8)], (0, 0, 0)),
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
        using JsonDocument answer = JsonDocument.Parse(DemoEngine.Run(row.Pattern, row.Flags, row.Subject));

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
                JsonElement counts = answer.RootElement.GetProperty("matches")[0].GetProperty("counts");
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
        DemoExampleRow row = DemoExamples
            .All()
            .Single(static e => string.Equals(e.Title, "Named groups and every capture", StringComparison.Ordinal));
        using JsonDocument answer = JsonDocument.Parse(DemoEngine.Run(row.Pattern, row.Flags, row.Subject));

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
    /// Eight, and every field filled in. "Keep it to eight: the per-feature set is S72's, and a
    /// sidebar trying to be both is neither" (the slice file). A number in a test is how that stays
    /// a decision rather than a drift.
    /// </summary>
    [Test]
    public void The_sidebar_is_eight_complete_examples()
    {
        DemoExampleRow[] examples = [.. DemoExamples.All()];

        using (new AssertionScope())
        {
            examples.Should().HaveCount(8);
            examples.Select(static e => e.Title).Should().OnlyHaveUniqueItems();

            foreach (DemoExampleRow example in examples)
            {
                example.Title.Should().NotBeNullOrWhiteSpace();
                // The note is the tour's only prose. An example without one is a pattern with no
                // explanation, which teaches nobody anything.
                example.Note.Should().NotBeNullOrWhiteSpace("'{0}' needs a note", example.Title);
                example.Pattern.Should().NotBeNullOrWhiteSpace("'{0}' needs a pattern", example.Title);
                example.Subject.Should().NotBeNullOrWhiteSpace("'{0}' needs a subject", example.Title);
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
            }
        }
    }

    /// <summary>Upstream's answer for one example.</summary>
    private sealed record Expected(
        (int Index, int Length)[] Spans,
        (int Substitutions, int Insertions, int Deletions) FirstMatchCounts
    );
}
