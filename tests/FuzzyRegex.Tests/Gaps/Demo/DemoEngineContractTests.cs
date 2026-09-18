using System.Text.Json;
using AwesomeAssertions;
using FuzzyRegexDemo.Wasm;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>
/// The JSON contract the browser demo's Web Worker speaks (S70).
/// </summary>
/// <remarks>
/// <para>
/// <c>demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs</c> is compiled into this assembly by a
/// <c>&lt;Compile Include&gt;</c> link, so the contract is pinned under the JIT on all three CI
/// operating systems and the <c>browser-wasm</c> project needs no test host. What is NOT pinned
/// here is the interop itself - that is <c>demo/wwwroot/harness.html</c>, driven in a real browser.
/// </para>
/// <para>
/// <b>Provenance of every expected span and count below</b>, per the port-slice rule (owner,
/// 2026-09-15): they come from a real upstream run, not from this port's own output.
/// <c>tools/probes/demo-json-contract-expectations.py</c> is the probe; run on 2026-09-18 against
/// <c>regex 2026.9.10</c> on Python 3.14.6 it printed:
/// </para>
/// <code>
/// groups-and-captures: compile(r"(?&lt;word&gt;\w+)\s+(\w+)").search("hello world")
///     match       utf16 index=0 length=11
///     group 0          utf16 index=0 length=11
///     group 1 (word)   utf16 index=0 length=5
///     group 2          utf16 index=6 length=5
/// repeated-captures: compile(r"(\w)+").search("abc")
///     match       utf16 index=0 length=3
///     group 1          utf16 index=2 length=1
///         capture utf16 index=0 length=1
///         capture utf16 index=1 length=1
///         capture utf16 index=2 length=1
/// optional-group-that-did-not-take-part: compile(r"(a)|(b)").search("b")
///     match       utf16 index=0 length=1
///     group 1          no match
///     group 2          utf16 index=0 length=1
/// fuzzy-with-counts: compile(r"(?:kitten){e&lt;=3}").search("sitting")
///     match       utf16 index=0 length=6
///     fuzzy_counts (sub, ins, del) = (2, 0, 0)
/// fuzzy-per-error-type: compile(r"(?:foobar){i&lt;=1,d&lt;=1,s&lt;=1}").search("xfoobat")
///     match       utf16 index=0 length=6
///     fuzzy_counts (sub, ins, del) = (1, 1, 1)
/// astral-subject: compile(r"\p{Deseret}+").search("ab\U00010400\U00010401cd")
///     match       utf16 index=2 length=4   (codepoints index=2 length=2)
/// every-match: compile(r"\d+").finditer("a1 b22 c333")
///     match       utf16 index=1 length=1
///     match       utf16 index=4 length=2
///     match       utf16 index=8 length=3
/// ignorecase-flag: compile(r"ab", regex.IGNORECASE).finditer("AB ab Ab")
///     match       utf16 index=0 length=2
///     match       utf16 index=3 length=2
///     match       utf16 index=6 length=2
/// unbalanced-parenthesis: compile('(')
///     error: missing ) at position 1
/// </code>
/// <para>
/// The timeout, the subject-length cap, the pattern-length cap, the match cap and the unknown-flag
/// rejection have no upstream counterpart - they are this demo's own trust-boundary contract, not a
/// parity claim - so those tests assert only the shape of the error JSON.
/// </para>
/// </remarks>
public sealed class DemoEngineContractTests
{
    /// <summary>
    /// The exact wire format, written out once. Every other test in this file navigates the JSON
    /// instead, so this is the only place a property rename would have to be noticed by hand - and
    /// it has to be noticed, because the page reads these names.
    /// </summary>
    [Test]
    public void The_wire_format_is_exactly_this()
    {
        DemoEngine
            .Run(@"(\w)+", "", "abc")
            .Should()
            .Be(
                """{"matches":[{"index":0,"length":3,"counts":{"substitutions":0,"insertions":0,"deletions":0},"groups":[{"number":0,"name":"0","success":true,"index":0,"length":3,"captures":[{"index":0,"length":3}]},{"number":1,"name":"1","success":true,"index":2,"length":1,"captures":[{"index":0,"length":1},{"index":1,"length":1},{"index":2,"length":1}]}]}],"truncated":false}"""
            );
    }

    /// <summary>
    /// The caps as literals. Every other cap test builds its input from the constant it is testing,
    /// so all of them still pass when the constant moves - the S70 review demonstrated it by
    /// setting <c>MaxMatches</c> to 1. This is the test that notices.
    /// </summary>
    [Test]
    public void The_caps_are_exactly_these_numbers()
    {
        (
            DemoEngine.MaxSubjectLength,
            DemoEngine.MaxPatternLength,
            DemoEngine.MaxFlagsLength,
            DemoEngine.MaxMatches,
            DemoEngine.MaxSpans,
            DemoEngine.MaxQuotedTokenLength,
            DemoEngine.MatchTimeout
        )
            .Should()
            .Be((100_000, 1_000, 200, 1_000, 50_000, 40, TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void A_named_group_carries_its_name_and_its_span()
    {
        JsonElement match = Matches(DemoEngine.Run(@"(?<word>\w+)\s+(\w+)", "", "hello world")).Single();

        Span(match).Should().Be((0, 11));
        Groups(match)
            .Select(static g =>
                (
                    g.GetProperty("number").GetInt32(),
                    g.GetProperty("name").GetString(),
                    g.GetProperty("index").GetInt32(),
                    g.GetProperty("length").GetInt32()
                )
            )
            .Should()
            .Equal((0, "0", 0, 11), (1, "word", 0, 5), (2, "2", 6, 5));
    }

    /// <summary>
    /// Upstream reports <c>span(1) == (-1, -1)</c> for a group that took no part. The demo reports
    /// <c>success: false</c> with index -1 and length 0, so that a page which slices the subject by
    /// index and length without checking <c>success</c> produces an empty string rather than a
    /// wrong one.
    /// </summary>
    [Test]
    public void A_group_that_took_no_part_reports_success_false_and_index_minus_one()
    {
        JsonElement match = Matches(DemoEngine.Run("(a)|(b)", "", "b")).Single();

        Groups(match)
            .Select(static g =>
                (
                    g.GetProperty("success").GetBoolean(),
                    g.GetProperty("index").GetInt32(),
                    g.GetProperty("length").GetInt32(),
                    g.GetProperty("captures").GetArrayLength()
                )
            )
            .Should()
            .Equal((true, 0, 1, 1), (false, -1, 0, 0), (true, 0, 1, 1));
    }

    [Test]
    [Arguments("(?:kitten){e<=3}", "sitting", 0, 6, 2, 0, 0)]
    [Arguments("(?:foobar){i<=1,d<=1,s<=1}", "xfoobat", 0, 6, 1, 1, 1)]
    public void A_fuzzy_match_carries_its_per_error_type_counts(
        string pattern,
        string subject,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        JsonElement match = Matches(DemoEngine.Run(pattern, "", subject)).First();

        Span(match).Should().Be((index, length));
        JsonElement counts = match.GetProperty("counts");
        (
            counts.GetProperty("substitutions").GetInt32(),
            counts.GetProperty("insertions").GetInt32(),
            counts.GetProperty("deletions").GetInt32()
        )
            .Should()
            .Be((substitutions, insertions, deletions));
    }

    /// <summary>
    /// Upstream counts codepoints and reports index 2 length 2 here; the demo reports UTF-16, which
    /// is index 2 length 4, because the page slices a JavaScript string with these numbers.
    /// </summary>
    [Test]
    public void Indices_are_UTF16_code_units_so_the_page_can_slice_with_them()
    {
        const string subject = "ab\U00010400\U00010401cd";

        JsonElement match = Matches(DemoEngine.Run(@"\p{Deseret}+", "", subject)).Single();

        Span(match).Should().Be((2, 4));
        subject.Substring(2, 4).Should().Be("\U00010400\U00010401");
    }

    [Test]
    public void Every_match_is_reported_not_just_the_first()
    {
        Matches(DemoEngine.Run(@"\d+", "", "a1 b22 c333")).Select(Span).Should().Equal((1, 1), (4, 2), (8, 3));
    }

    [Test]
    [Arguments("IgnoreCase")]
    [Arguments("ignorecase")]
    [Arguments(" IgnoreCase ")]
    [Arguments("IgnoreCase,Multiline")]
    [Arguments("IgnoreCase | Multiline")]
    public void The_flags_are_member_names_in_any_case_and_any_separator(string flags)
    {
        Matches(DemoEngine.Run("ab", flags, "AB ab Ab")).Select(Span).Should().Equal((0, 2), (3, 2), (6, 2));
    }

    [Test]
    public void No_flags_means_no_flags()
    {
        Matches(DemoEngine.Run("ab", "", "AB ab Ab")).Select(Span).Should().Equal((3, 2));
    }

    [Test]
    [Arguments("NoSuchFlag")]
    [Arguments("2")] // a number would be a silently different flag set, so it is refused
    [Arguments("IgnoreCase,NoSuchFlag")]
    public void An_unknown_flag_is_an_error_naming_it(string flags)
    {
        Error(DemoEngine.Run("ab", flags, "ab")).Should().Contain(flags.Split(',')[^1].Trim());
    }

    [Test]
    public void A_parse_error_comes_back_as_json_not_as_an_exception()
    {
        Error(DemoEngine.Run("(", "", "abc")).Should().Contain("missing )");
    }

    /// <summary>
    /// The pathological pattern <c>TimeoutAndCancellationTests</c> and
    /// <c>LazyEnumerationTests</c> already use: exponential backtracking with no way to succeed.
    /// This is the fast common-case exit; <c>worker.terminate()</c> is the real safety net, and the
    /// harness proves that half in a browser.
    /// </summary>
    [Test]
    public void A_runaway_pattern_comes_back_as_a_timeout_error_not_as_an_exception()
    {
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        Error(DemoEngine.Run("(a|a)*b", "", new string('a', 30))).Should().Contain("timed out");

        // The message alone is not the contract - returning is. Asserting only on the text is how
        // the suite stayed green while Run could take forever (S70 review). The bound is loose
        // because the walk polls its deadline between steps and each step carries its own budget,
        // so the true ceiling is about twice MatchTimeout, not once.
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// <c>EnumerateMatches</c>'s <c>timeout</c> is documented as "How long ONE step may run...
    /// this is not the whole walk's budget" (<c>src/FuzzyRegex/FuzzyRegex.cs:947</c>), so a lazy
    /// walk of a thousand matches gets a thousand budgets. Measured before the fix: 31.6 seconds
    /// for this subject, returning success rather than a timeout.
    /// </summary>
    /// <remarks>
    /// Each chunk is eighteen <c>a</c>s, a <c>c</c> the pattern cannot pass, and the <c>b</c> that
    /// finally matches - so every single step finishes well inside a per-step budget and two
    /// hundred of them do not come close to fitting in one.
    /// </remarks>
    [Test]
    public void The_whole_walk_shares_one_time_budget_rather_than_one_per_match()
    {
        string subject = string.Concat(Enumerable.Repeat(new string('a', 18) + "cb", 200));
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

        Error(DemoEngine.Run("(a|a)*b", "", subject)).Should().Contain("timed out");

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// The match cap bounds how many matches come back and says nothing about how many spans each
    /// one carries, and a capture list is per repetition. Measured before the fix: a 103-character
    /// pattern on a legal 100,000-character subject returned a 128 MB JSON string with
    /// <c>truncated: false</c> - which is precisely the page-side freeze the caps exist to prevent,
    /// and the one freeze a Web Worker does nothing about.
    /// </summary>
    [Test]
    public void One_match_cannot_carry_unbounded_capture_spans()
    {
        string pattern = new string('(', 20) + @"\w" + new string(')', 20) + "+";
        string subject = new('a', 5_000);

        string answer = DemoEngine.Run(pattern, "", subject);

        using JsonDocument json = JsonDocument.Parse(answer);
        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(1);
        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
        answer.Length.Should().BeLessThan(2_000_000);
    }

    [Test]
    public void A_subject_over_the_cap_is_refused_before_anything_is_compiled()
    {
        Error(DemoEngine.Run("a", "", new string('a', DemoEngine.MaxSubjectLength + 1)))
            .Should()
            .Contain(DemoEngine.MaxSubjectLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The third input a stranger controls. It had no cap until the S70 review: a flag list of two
    /// million quotation marks came back as a twelve million character JSON string, because the
    /// unknown-flag error quotes the token it could not read and JSON escaping multiplied it.
    /// </summary>
    [Test]
    public void A_flag_list_over_the_cap_is_refused()
    {
        Error(DemoEngine.Run("a", new string('z', DemoEngine.MaxFlagsLength + 1), "aaa"))
            .Should()
            .Contain(DemoEngine.MaxFlagsLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The error quotes back only enough of an unreadable token to recognise a typo by. The input
    /// here is legal - it is under the flag-list cap - so the only thing keeping the answer small
    /// is the quoting bound itself.
    /// </summary>
    [Test]
    public void An_unreadable_flag_token_is_quoted_back_briefly_not_in_full()
    {
        string answer = DemoEngine.Run("a", new string('"', DemoEngine.MaxFlagsLength), "aaa");

        answer.Length.Should().BeLessThan(500);
        Error(answer).Should().Contain("...");
    }

    /// <summary>
    /// The caps are checked before the pattern is compiled, so an over-long subject is refused
    /// even when the pattern would not have compiled either. The test that claimed this used a
    /// valid pattern and so never exercised the ordering (S70 review).
    /// </summary>
    [Test]
    public void The_subject_cap_is_checked_before_the_pattern_is_compiled()
    {
        Error(DemoEngine.Run("(", "", new string('a', DemoEngine.MaxSubjectLength + 1)))
            .Should()
            .Contain("limit")
            .And.NotContain("missing )");
    }

    [Test]
    public void A_pattern_over_the_cap_is_refused()
    {
        Error(DemoEngine.Run(new string('a', DemoEngine.MaxPatternLength + 1), "", "aaa"))
            .Should()
            .Contain(DemoEngine.MaxPatternLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The cap the page cannot enforce for itself: the engine answers quickly and it is the
    /// rendering of hundreds of thousands of spans that would freeze the tab, which is the one
    /// freeze a Web Worker does nothing about.
    /// </summary>
    [Test]
    public void The_match_list_stops_at_the_cap_and_says_so()
    {
        using JsonDocument json = JsonDocument.Parse(
            DemoEngine.Run("a", "", new string('a', DemoEngine.MaxMatches + 500))
        );

        json.RootElement.GetProperty("matches").GetArrayLength().Should().Be(DemoEngine.MaxMatches);
        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void An_answer_that_fits_under_the_cap_is_not_flagged_as_truncated()
    {
        using JsonDocument json = JsonDocument.Parse(DemoEngine.Run("a", "", "aaa"));

        json.RootElement.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// Nothing may cross the interop boundary as an exception, so the shapes most likely to throw
    /// somewhere other than the parser are swept here. The assertion is deliberately weak - valid
    /// JSON carrying either answer - because what is under test is that a call returns at all.
    /// </summary>
    [Test]
    [Arguments("", "", "")] // the empty pattern on the empty subject
    [Arguments("(?<n>a)(?<n>b)", "", "ab")] // a duplicate group name
    [Arguments(@"\N{NO SUCH CHARACTER}", "", "a")] // an unknown character name
    [Arguments("(?R)", "", "a")] // recursion with nothing to recurse into
    [Arguments("a{2,1}", "", "aa")] // a reversed quantifier range
    [Arguments(@"\L<missing>", "", "a")] // a named list the demo cannot supply
    [Arguments("(?i)[", "", "a")] // an unterminated set
    [Arguments("a", "Version0,Version1", "a")] // two flags that contradict each other
    public void Run_always_returns_json_and_never_throws(string pattern, string flags, string subject)
    {
        string answer = DemoEngine.Run(pattern, flags, subject);

        using JsonDocument json = JsonDocument.Parse(answer);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        (json.RootElement.TryGetProperty("matches", out _) || json.RootElement.TryGetProperty("error", out _))
            .Should()
            .BeTrue(answer);
    }

    private static IEnumerable<JsonElement> Matches(string json)
    {
        using JsonDocument parsed = JsonDocument.Parse(json);
        parsed
            .RootElement.TryGetProperty("error", out JsonElement error)
            .Should()
            .BeFalse(error.ValueKind == JsonValueKind.String ? error.GetString() : json);

        // Cloned because the JsonDocument that owns the buffer is disposed on the way out.
        return [.. parsed.RootElement.GetProperty("matches").EnumerateArray().Select(static m => m.Clone())];
    }

    private static string Error(string json)
    {
        using JsonDocument parsed = JsonDocument.Parse(json);
        parsed.RootElement.TryGetProperty("matches", out _).Should().BeFalse(json);
        return parsed.RootElement.GetProperty("error").GetString()!;
    }

    private static IEnumerable<JsonElement> Groups(JsonElement match) => match.GetProperty("groups").EnumerateArray();

    private static (int Index, int Length) Span(JsonElement spanned) =>
        (spanned.GetProperty("index").GetInt32(), spanned.GetProperty("length").GetInt32());
}
