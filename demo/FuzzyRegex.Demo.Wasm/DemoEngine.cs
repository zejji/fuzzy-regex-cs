using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fuzzy.Text.RegularExpressions;

namespace FuzzyRegexDemo.Wasm;

/// <summary>
/// The demo's whole engine-side contract: a pattern, a flag list and a subject in as strings, one
/// JSON string out.
/// </summary>
/// <remarks>
/// <para>
/// This type has no <c>[JSExport]</c> on it and knows nothing about JavaScript, which is the point.
/// The attributed wrapper is one line in <c>Interop.cs</c>, and everything worth testing lives
/// here, so <c>tests/FuzzyRegex.Tests</c> pins the JSON contract under the JIT on three operating
/// systems and the browser project needs no test host of its own (S70).
/// </para>
/// <para>
/// <b>It never throws.</b> A managed exception crossing the interop boundary arrives in JavaScript
/// as a rejected promise with no useful text, so every failure - a parse error, the timeout, a cap
/// breach, an unknown flag - comes back as <c>{"error": "..."}</c> instead.
/// </para>
/// <para>
/// <b>The caps are enforced here, not in the page.</b> S71 will cap the subject box too, but a cap
/// that only exists in the UI is no cap once somebody drives the worker from the browser console.
/// </para>
/// </remarks>
internal static class DemoEngine
{
    /// <summary>
    /// The longest subject the demo will match against. Defence against a paste, not a security
    /// boundary: the Web Worker is what actually bounds a runaway (ROADMAP, 2026-08-31).
    /// </summary>
    internal const int MaxSubjectLength = 100_000;

    /// <summary>The longest pattern the demo will compile.</summary>
    internal const int MaxPatternLength = 1_000;

    /// <summary>
    /// The longest flag list the demo will read. A flag list is a handful of member names, so this
    /// is generous; it exists because the pattern and the subject were capped and this was not.
    /// </summary>
    /// <remarks>
    /// Every input a stranger can set needs a bound, not just the two obvious ones. Measured while
    /// this cap was missing: a flag list of two million quotation marks came back as a twelve
    /// million character JSON string, because the unknown-flag error quotes the token it could not
    /// read and JSON escaping then multiplied it six-fold.
    /// </remarks>
    internal const int MaxFlagsLength = 200;

    /// <summary>
    /// How much of an unreadable flag token the error message quotes back. Enough to recognise a
    /// typo, not enough to be an amplifier.
    /// </summary>
    internal const int MaxQuotedTokenLength = 40;

    /// <summary>
    /// The most matches reported. A subject of 100,000 characters and a pattern that matches the
    /// empty string yields 100,001 matches, and it is the page's rendering of them - not the engine
    /// - that would freeze; the worker cannot help with that, so the cap is here.
    /// </summary>
    internal const int MaxMatches = 1_000;

    /// <summary>
    /// The most spans the whole answer may carry, counting every group and every capture in every
    /// match.
    /// </summary>
    /// <remarks>
    /// <see cref="MaxMatches"/> bounds how many matches come back and says nothing about how big
    /// each one is: a capture list holds one span per repetition, so a single match of
    /// <c>(((...\w...)))+</c> against a legal subject carries hundreds of thousands of spans, and
    /// nesting multiplies that by the group count. Measured before this cap existed: a
    /// 103-character pattern on a 100,000-character subject produced a 128 MB JSON string with
    /// <c>truncated: false</c>. That is the page-side freeze the caps exist to prevent, and the one
    /// freeze a Web Worker does nothing about - the engine answers promptly and the main thread
    /// dies rendering the answer.
    /// </remarks>
    internal const int MaxSpans = 50_000;

    /// <summary>
    /// The per-call time budget. Defence in depth and the fast common-case exit, never the only
    /// safety net: <c>worker.terminate()</c> is (ROADMAP, 2026-08-31).
    /// </summary>
    /// <remarks>
    /// <b>It does not cover construction, and nothing here does.</b> This budget is a matching
    /// budget, so a pattern whose repeat counts multiply out to an enormous program - the shape
    /// <c>(((a{100}){100}){100}){100}</c>, well inside <see cref="MaxPatternLength"/> - spends its
    /// time in <see cref="FuzzyRegex"/>'s constructor, where no clock is running, and
    /// <see cref="Run"/> does not return. The blind review of S70 found it; the underlying
    /// blow-up is investigated and sliced separately as S56b
    /// (<c>docs/plan/2026-09-18-repeat-unrolling-investigation.md</c>), so the demo does not try to
    /// out-guess it with a repeat-count heuristic here.
    /// <para>
    /// This is exactly the case the Web Worker exists for, and it is why the ROADMAP calls the
    /// worker the whole safety design rather than a nicety: <c>worker.terminate()</c> is the only
    /// thing that recovers a wedged construction, and S71's page must therefore treat terminate as
    /// its primary control and not as an error path.
    /// </para>
    /// </remarks>
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The separators a flag list may use, so that a human typing one is not caught out.</summary>
    private static readonly char[] _flagSeparators = [',', ' ', '|', '\t'];

    /// <summary>
    /// Matches <paramref name="pattern"/> against <paramref name="subject"/> and returns the answer
    /// as JSON.
    /// </summary>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="flags">
    /// Zero or more <see cref="FuzzyRegexOptions"/> member names, separated by commas, spaces or
    /// <c>|</c>. Case-insensitive. Numbers are refused: the demo takes names so that a typo is an
    /// error rather than a silently different set of flags.
    /// </param>
    /// <param name="subject">The text to search.</param>
    /// <returns>
    /// <c>{"matches": [...], "truncated": false}</c>, or <c>{"error": "..."}</c>. Every index and
    /// length is a UTF-16 code unit offset, because the page slices a JavaScript string with it and
    /// JavaScript strings are UTF-16 too.
    /// </returns>
    internal static string Run(string pattern, string flags, string subject)
    {
        try
        {
            if (TooLong(pattern, flags, subject) is string breach)
            {
                return Failed(breach);
            }

            if (!TryParseFlags(flags, out FuzzyRegexOptions options, out string? flagError))
            {
                return Failed(flagError);
            }

            return Search(new FuzzyRegex(pattern, options, MatchTimeout), subject);
        }
        catch (FuzzyRegexParseException parseError)
        {
            return Failed(parseError.Message);
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return TimedOut();
        }
        // This IS the interop boundary, and the boundary's contract is that nothing crosses it as
        // an exception - a managed exception reaching JavaScript arrives as a rejected promise
        // carrying no useful text, so an unanticipated failure would reach the user as silence.
        // Narrowing this to a list of types would mean guessing that list correctly for every
        // pattern a stranger can type, which is the guess the demo cannot afford to get wrong. The
        // type name is reported beside the message because a bare "Value cannot be null." tells
        // whoever is reading the demo's output nothing at all.
        //
        // The whole exception goes to the console and only the sentence goes to the page. Under
        // the WebAssembly runtime `Console.Error` is the browser's developer console, so the stack
        // trace is one keypress away for whoever is debugging and is not in the face of whoever is
        // trying the library. Swallowing it entirely is what ErrorProne's EPC12 is for.
        catch (Exception unexpected)
        {
            Console.Error.WriteLine(unexpected);
            return Failed($"{unexpected.GetType().Name}: {unexpected.Message}");
        }
    }

    /// <summary>
    /// Says which of the three inputs a stranger controls is over its cap, or <see langword="null"/>
    /// when none is. Checked before the pattern is compiled, because compiling is the expensive
    /// step and an over-long subject should not pay for it.
    /// </summary>
    private static string? TooLong(string pattern, string flags, string subject) =>
        (pattern.Length, subject.Length, flags.Length) switch
        {
            var (patternLength, _, _) when patternLength > MaxPatternLength => string.Create(
                CultureInfo.InvariantCulture,
                $"The pattern is longer than the demo's limit of {MaxPatternLength} characters."
            ),
            var (_, subjectLength, _) when subjectLength > MaxSubjectLength => string.Create(
                CultureInfo.InvariantCulture,
                $"The subject is longer than the demo's limit of {MaxSubjectLength} characters."
            ),
            var (_, _, flagsLength) when flagsLength > MaxFlagsLength => string.Create(
                CultureInfo.InvariantCulture,
                $"The flag list is longer than the demo's limit of {MaxFlagsLength} characters."
            ),
            _ => null,
        };

    /// <summary>
    /// Walks the subject lazily and stops at <see cref="MaxMatches"/>, <see cref="MaxSpans"/> or
    /// <see cref="MatchTimeout"/>, whichever comes first. The eager
    /// <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
    /// would walk the whole subject before the caps could apply, which is the walk the caps exist
    /// to avoid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clock is kept here because no one else keeps it.</b> The <see cref="MatchTimeout"/>
    /// handed to the constructor bounds ONE step - "this is not the whole walk's budget"
    /// (<c>src/FuzzyRegex/FuzzyRegex.cs:947</c>) - so a walk of a thousand matches gets a thousand
    /// budgets. Measured before this loop had a stopwatch in it: 31.6 seconds for a subject of two
    /// hundred cheap chunks, returning success rather than a timeout.
    /// </para>
    /// <para>
    /// A <see cref="CancellationTokenSource"/> with a due time would read better and would not
    /// work: its timer callback needs the event loop, and in the browser this walk occupies the
    /// worker's only thread until it returns, so the callback cannot run until the deadline no
    /// longer matters. A stopwatch this loop polls itself needs nothing but the loop.
    /// </para>
    /// </remarks>
    private static string Search(FuzzyRegex regex, string subject)
    {
        long deadline = Stopwatch.GetTimestamp() + (long)(MatchTimeout.TotalSeconds * Stopwatch.Frequency);
        List<DemoMatch> matches = [];
        bool truncated = false;
        int spans = 0;

        foreach (Match match in regex.EnumerateMatches(subject))
        {
            if (Stopwatch.GetTimestamp() >= deadline)
            {
                return TimedOut();
            }

            if (matches.Count == MaxMatches || spans >= MaxSpans)
            {
                truncated = true;
                break;
            }

            DemoMatch described = Describe(match, MaxSpans - spans, out bool clipped);
            matches.Add(described);
            spans += described.Groups.Sum(static g => 1 + g.Captures.Count);

            // A clipped match is the last one: its own capture lists are already short of the
            // truth, and going round again would only add more spans to an answer that has just
            // been told it has no room for them.
            if (clipped)
            {
                truncated = true;
                break;
            }
        }

        return JsonSerializer.Serialize(new DemoAnswer(matches, truncated, null), DemoJson.Default.DemoAnswer);
    }

    /// <summary>Renders one match as the shape the page reads, within a budget of spans.</summary>
    /// <param name="match">The match to describe.</param>
    /// <param name="budget">How many spans this match may spend. See <see cref="MaxSpans"/>.</param>
    /// <param name="clipped">
    /// Set when the budget ran out, so that some capture list - or some whole group - is shorter
    /// than the match really is. The answer says <c>truncated: true</c> when it happens, which is
    /// the only signal the page gets that it is not looking at the whole truth.
    /// </param>
    private static DemoMatch Describe(Match match, int budget, out bool clipped)
    {
        List<DemoGroup> groups = [];
        int number = 0;
        clipped = false;

        foreach (Group group in match.Groups)
        {
            // A group costs one span of its own before any of its captures.
            if (budget <= 0)
            {
                clipped = true;
                break;
            }

            budget--;

            List<DemoSpan> captures = [];
            if (group.Success)
            {
                foreach (Capture capture in group.Captures)
                {
                    if (budget <= 0)
                    {
                        clipped = true;
                        break;
                    }

                    budget--;
                    captures.Add(new DemoSpan(capture.Index, capture.Length));
                }
            }

            // Upstream reports span (-1, -1) for a group that took no part. Index -1 with length 0
            // is the same answer in a form a page can slice with: a caller that forgets to check
            // `success` gets an empty string rather than a wrong one.
            groups.Add(
                new DemoGroup(
                    number++,
                    group.Name,
                    group.Success,
                    group.Success ? group.Index : -1,
                    group.Success ? group.Length : 0,
                    captures
                )
            );
        }

        FuzzyCounts counts = match.FuzzyCounts;
        return new DemoMatch(
            match.Index,
            match.Length,
            new DemoCounts(counts.Substitutions, counts.Insertions, counts.Deletions),
            groups
        );
    }

    /// <summary>
    /// The one timeout sentence, said the same way whichever clock ran out - the engine's own
    /// per-step budget or <see cref="Search"/>'s budget for the whole walk.
    /// </summary>
    private static string TimedOut() =>
        Failed(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The match timed out after {MatchTimeout.TotalSeconds} seconds. The page stays responsive because the engine runs in a Web Worker; this message is the engine's own early exit."
            )
        );

    /// <summary>
    /// Turns a flag list into options, or says which token it could not read.
    /// </summary>
    /// <remarks>
    /// Names only. <c>Enum.TryParse</c> would happily read "2" as <c>IgnoreCase</c>, and a flag
    /// list arriving from a URL fragment or the browser console is exactly where a stray number
    /// should be an error rather than a silently different set of flags.
    /// </remarks>
    private static bool TryParseFlags(string flags, out FuzzyRegexOptions options, out string? error)
    {
        options = FuzzyRegexOptions.None;
        error = null;

        foreach (
            string token in flags.Split(
                _flagSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            if (
                !char.IsLetter(token[0])
                || !Enum.TryParse(token, ignoreCase: true, out FuzzyRegexOptions one)
                || !Enum.IsDefined(one)
            )
            {
                // Quoted short, because the message is built from input a stranger chose.
                string quoted =
                    token.Length > MaxQuotedTokenLength
                        ? string.Concat(token.AsSpan(0, MaxQuotedTokenLength), "...")
                        : token;
                error = $"'{quoted}' is not a FuzzyRegexOptions member name.";
                return false;
            }

            options |= one;
        }

        return true;
    }

    private static string Failed(string? message) =>
        JsonSerializer.Serialize(new DemoAnswer(null, null, message ?? "Unknown error."), DemoJson.Default.DemoAnswer);
}

/// <summary>One span in the subject, in UTF-16 code units.</summary>
internal sealed record DemoSpan(int Index, int Length);

/// <summary>The per-error-type cost of a fuzzy match. Zero everywhere for an exact one.</summary>
internal sealed record DemoCounts(int Substitutions, int Insertions, int Deletions);

/// <summary>
/// One capturing group's result. <c>Captures</c> is the full capture list, which is an mrab-regex
/// feature the built-in engine does not have, and is what makes a repeated group worth showing.
/// </summary>
internal sealed record DemoGroup(
    int Number,
    string Name,
    bool Success,
    int Index,
    int Length,
    IReadOnlyList<DemoSpan> Captures
);

/// <summary>One match: its span, its cost, and every group.</summary>
internal sealed record DemoMatch(int Index, int Length, DemoCounts Counts, IReadOnlyList<DemoGroup> Groups);

/// <summary>
/// The whole answer. Exactly one of <c>Matches</c> and <c>Error</c> is ever set, and the null one
/// is omitted from the JSON, so a failure is literally <c>{"error": "..."}</c>.
/// </summary>
internal sealed record DemoAnswer(IReadOnlyList<DemoMatch>? Matches, bool? Truncated, string? Error);

/// <summary>
/// The source-generated serialiser. Reflection-based <c>JsonSerializer</c> calls are trimmed away
/// under <c>PublishTrimmed</c>, which is how the demo publishes, so the context is not a
/// nicety here - it is the only form that survives.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(DemoAnswer))]
internal sealed partial class DemoJson : JsonSerializerContext;
