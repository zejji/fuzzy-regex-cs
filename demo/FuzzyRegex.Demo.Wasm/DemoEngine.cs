using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// <see cref="Run(string, string, string, string, string, string)"/> does not return. The blind review of S70 found it; the underlying
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

    /// <summary>
    /// The longest replacement template the demo will apply. A template is a short piece of text
    /// with <c>\1</c> and <c>\g&lt;name&gt;</c> references in it, so this is generous.
    /// </summary>
    internal const int MaxReplacementLength = 1_000;

    /// <summary>
    /// The longest named-list block the demo will read - every list, together, as the user typed
    /// them. One bound on the whole block bounds the list count, the word count and the word
    /// lengths at once, which three separate caps would do less clearly.
    /// </summary>
    internal const int MaxNamedListsLength = 2_000;

    /// <summary>
    /// The longest replaced subject the demo will hand back.
    /// </summary>
    /// <remarks>
    /// Replacement is the one operation whose output can be far larger than its input: a subject at
    /// <see cref="MaxSubjectLength"/> with a match every character and a template at
    /// <see cref="MaxReplacementLength"/> is a hundred million characters, built in the worker and
    /// then posted to the page, where rendering it is the freeze the caps exist to prevent. Twice
    /// the subject cap leaves every honest replacement intact.
    /// </remarks>
    internal const int MaxReplacedLength = 2 * MaxSubjectLength;

    /// <summary>The separators a flag list may use, so that a human typing one is not caught out.</summary>
    private static readonly char[] _flagSeparators = [',', ' ', '|', '\t'];

    /// <summary>The separators a named list's words may use.</summary>
    private static readonly char[] _wordSeparators = [',', ';'];

    /// <summary>The line separators a named-list block may use, whichever way the browser sends them.</summary>
    private static readonly string[] _lineSeparators = ["\r\n", "\n", "\r"];

    /// <summary>
    /// Matches <paramref name="pattern"/> against <paramref name="subject"/> in one of the demo's
    /// three modes and returns the answer as JSON.
    /// </summary>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="flags">
    /// Zero or more <see cref="FuzzyRegexOptions"/> member names, separated by commas, spaces or
    /// <c>|</c>. Case-insensitive. Numbers are refused: the demo takes names so that a typo is an
    /// error rather than a silently different set of flags.
    /// </param>
    /// <param name="subject">The text to search.</param>
    /// <param name="mode">
    /// <c>""</c> or <c>"match"</c> to walk every match, <c>"partial"</c> to ask for one match that
    /// may be partial, or <c>"replace"</c> to apply <paramref name="replacement"/> as well.
    /// </param>
    /// <param name="replacement">
    /// The replacement template, in upstream's language (<c>\1</c>, <c>\g&lt;name&gt;</c>), used
    /// only in <c>"replace"</c> mode.
    /// </param>
    /// <param name="namedLists">
    /// The pattern's <c>\L&lt;name&gt;</c> lists, one per line, as <c>name: word, word</c>. Empty
    /// when the pattern references none.
    /// </param>
    /// <returns>
    /// <c>{"matches": [...], "truncated": false}</c> - with <c>"replaced"</c> beside it in replace
    /// mode and <c>"partialMatch": true</c> on a match that is partial - or <c>{"error": "..."}</c>,
    /// carrying <c>"errorOffset"</c> when the pattern failed to parse at a known position. Every
    /// index and length is a UTF-16 code unit offset, because the page slices a JavaScript string
    /// with it and JavaScript strings are UTF-16 too.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Only the pattern's own parse failure is reported with a position.</b> A replacement
    /// template's is caught in <see cref="Replace"/> instead, because upstream parses a template
    /// with the pattern parser and the position it raises indexes the template, not the pattern -
    /// see <see cref="DemoAnswer.ErrorOffset"/>.
    /// </para>
    /// <para>
    /// <b>Partial matching is its own mode rather than a flag on the walk</b> because upstream's
    /// scanning functions take no <c>partial</c> argument and neither do this port's: a partial
    /// match is only ever the LAST thing a search finds, so "every match, and the last one may be
    /// partial" is not a question the engine answers. The mode asks the single-match entry point
    /// instead, which is the question that has an answer.
    /// </para>
    /// </remarks>
    internal static string Run(
        string pattern,
        string flags,
        string subject,
        string mode,
        string replacement,
        string namedLists
    )
    {
        try
        {
            if (
                !TryPrepare(
                    pattern,
                    flags,
                    subject,
                    mode,
                    replacement,
                    namedLists,
                    out DemoMode parsedMode,
                    out FuzzyRegex? regex,
                    out string? refusal
                )
            )
            {
                return Failed(refusal);
            }

            return parsedMode switch
            {
                DemoMode.Partial => Partial(regex, subject),
                DemoMode.Replace => Replace(regex, subject, replacement),
                _ => Search(regex, subject),
            };
        }
        catch (FuzzyRegexParseException parseError)
        {
            return Failed(parseError.Message, parseError.Offset >= 0 ? parseError.Offset : null);
        }
        // A replacement template naming a group the pattern does not have arrives as an
        // ArgumentException whose message carries the .NET parameter name ("unknown group (Parameter
        // 'replacement')"). The page shows the sentence and not the plumbing: a stranger typing a
        // template has no idea what a parameter called 'replacement' is (blind review, 2026-09-19).
        catch (ArgumentException badArgument)
        {
            return Failed(badArgument.Message.Split(" (Parameter", StringSplitOptions.None)[0]);
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
    /// Reads and checks everything the page sent, and compiles the pattern, or says what it refused
    /// and why in the one sentence the page will show.
    /// </summary>
    /// <remarks>
    /// The order is the order of cost: the caps first, because they are string lengths; then the
    /// three things a human can mistype, so a typo is named rather than compiled; then the
    /// constructor, which is the expensive step and the one that can throw.
    /// </remarks>
    private static bool TryPrepare(
        string pattern,
        string flags,
        string subject,
        string mode,
        string replacement,
        string namedLists,
        out DemoMode parsedMode,
        [NotNullWhen(true)] out FuzzyRegex? regex,
        out string? refusal
    )
    {
        parsedMode = DemoMode.Match;
        regex = null;

        if (TooLong(pattern, flags, subject) is string breach)
        {
            refusal = breach;
            return false;
        }

        if (TooLongExtra(replacement, namedLists) is string extraBreach)
        {
            refusal = extraBreach;
            return false;
        }

        if (!TryParseMode(mode, out parsedMode, out refusal))
        {
            return false;
        }

        if (!TryParseFlags(flags, out FuzzyRegexOptions options, out refusal))
        {
            return false;
        }

        if (!TryParseNamedLists(namedLists, out Dictionary<string, IReadOnlyCollection<string>>? lists, out refusal))
        {
            return false;
        }

        regex = new FuzzyRegex(pattern, options, MatchTimeout, lists);
        refusal = null;
        return true;
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
    /// <b>The clock is polled here because no one else keeps it</b>, and it is the CALLER's clock -
    /// see <see cref="Deadline"/> - so that two passes share one budget. The <see cref="MatchTimeout"/>
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
    private static string Search(FuzzyRegex regex, string subject) =>
        TryWalk(regex, subject, Deadline(), out List<DemoMatch> matches, out bool truncated)
            ? JsonSerializer.Serialize(new DemoAnswer(matches, truncated, null, null), DemoJson.Default.DemoAnswer)
            : TimedOut();

    /// <summary>
    /// The timestamp one <see cref="MatchTimeout"/> from now. Taken once per request, so that an
    /// operation made of two passes - <see cref="Replace"/> is - spends one budget between them
    /// rather than one each.
    /// </summary>
    private static long Deadline() =>
        Stopwatch.GetTimestamp() + (long)(MatchTimeout.TotalSeconds * Stopwatch.Frequency);

    /// <summary>
    /// The walk itself, shared by <see cref="Search"/> and <see cref="Replace"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the clock ran out, in which case the caller answers with
    /// <see cref="TimedOut"/> and the matches found so far are discarded - a partial answer
    /// presented as a whole one would be a lie about what the engine found.
    /// </returns>
    private static bool TryWalk(
        FuzzyRegex regex,
        string subject,
        long deadline,
        out List<DemoMatch> found,
        out bool truncated
    )
    {
        List<DemoMatch> matches = [];
        truncated = false;
        found = matches;
        int spans = 0;

        foreach (Match match in regex.EnumerateMatches(subject))
        {
            if (Stopwatch.GetTimestamp() >= deadline)
            {
                return false;
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

        return true;
    }

    /// <summary>
    /// Asks for ONE match that may be partial: a match that ran out of subject before it ran out of
    /// pattern. Upstream's <c>search(..., partial=True)</c>, and this port's
    /// <see cref="FuzzyRegex.Match(string, int, int, bool, TimeSpan?, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// The answer carries at most one match because that is what the entry point returns. A page
    /// that walked on from a partial match would be asking the engine to continue past the end of
    /// the subject, which is where the partial match came from.
    /// </remarks>
    private static string Partial(FuzzyRegex regex, string subject)
    {
        Match match = regex.Match(subject, partial: true, timeout: MatchTimeout);
        bool clipped = false;
        List<DemoMatch> matches = match.Success ? [Describe(match, MaxSpans, out clipped)] : [];

        // One match can exhaust the span budget on its own - a repeated group over a long subject is
        // one capture per repetition - and an answer whose capture lists are short of the truth has
        // to say so here exactly as the walk does. Found by the blind review, 2026-09-19: a partial
        // match of `(\w)+` over 60,000 characters reported 49,997 captures and truncated: false.
        return JsonSerializer.Serialize(new DemoAnswer(matches, clipped, null, null), DemoJson.Default.DemoAnswer);
    }

    /// <summary>
    /// Applies a replacement template to every match and returns the rewritten subject beside the
    /// matches themselves, so the page can highlight what was replaced as well as show the result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The replacement and the walk are two passes over the subject and they share ONE clock: the
    /// deadline is taken before the first and handed to the second, so neither pass BEGINS a step
    /// after the budget is spent. Measured by the blind review before the deadline was shared,
    /// 2026-09-19: a subject of ten cheap chunks answered successfully after 3.4 seconds against a
    /// stated budget of 2.
    /// </para>
    /// <para>
    /// It does not make the wall clock a hard 2 seconds, and the earlier wording here said it did.
    /// A step already running when the deadline passes carries its own budget and is not
    /// interrupted - the same one-step overrun the walk has had since S70, recorded on
    /// <see cref="TryWalk"/> - so the ceiling is one budget plus one step. What the sharing buys is
    /// that the second pass cannot start a fresh one. Measured 2026-09-19 on the catastrophic
    /// subject of
    /// <c>DemoEngineContractTests.Replace_mode_answers_inside_one_budget_when_the_pattern_runs_away</c>:
    /// 2.08 s, with and without the sharing, because that subject spends the whole budget in the
    /// replacement pass and never reaches the walk.
    /// </para>
    /// <para>
    /// <b>At most <see cref="MaxMatches"/> occurrences are replaced</b>, which is the same cap the
    /// walk applies and the reason the output cannot be enormous: without it, an in-cap subject and
    /// an in-cap template multiply out to a hundred million characters built in the worker's heap
    /// before <see cref="MaxReplacedLength"/> could clip them (measured by the same review: a peak
    /// working set of 695 MB for a 100,000-character subject and a 1,000-character template). The
    /// answer says <c>truncated</c> when the cap left an occurrence unreplaced, and the walk is what
    /// knows: counting the replacements cannot separate "one too many" from "exactly enough", and
    /// reporting both as truncated cried truncation over complete answers until 2026-09-19.
    /// </para>
    /// <para>
    /// A template that references a group the pattern does not have is rejected by the template
    /// parser, which <see cref="Run(string, string, string, string, string, string)"/> reports like
    /// any other bad input.
    /// </para>
    /// </remarks>
    private static string Replace(FuzzyRegex regex, string subject, string replacement)
    {
        long deadline = Deadline();
        string replaced;
        try
        {
            replaced = regex.Replace(subject, replacement, count: MaxMatches, timeout: MatchTimeout);
        }
        // Caught HERE rather than beside the pattern's own parse failure, which is the only way to
        // tell the two apart: upstream parses a replacement template with the pattern parser, so a
        // bad template raises the same exception carrying a position that indexes the TEMPLATE. Left
        // to the outer catch it would reach the page as an offset into the pattern, and the caret
        // would sit under an unrelated character of a pattern that compiled perfectly well. The
        // sentence is still shown; only the position is dropped. See DemoAnswer.ErrorOffset.
        catch (FuzzyRegexParseException templateError)
        {
            return Failed(templateError.Message);
        }

        bool clipped = replaced.Length > MaxReplacedLength;
        if (clipped)
        {
            replaced = replaced[..MaxReplacedLength];
        }

        // The matches come back too, so the page can highlight what was replaced rather than only
        // showing the result. The walk is also what decides whether the REPLACEMENT was complete:
        // it goes over the same matches in the same order, so it reaches an occurrence past the cap
        // exactly when the replacement left one behind, and it can tell that from a subject holding
        // exactly as many occurrences as the cap allows - where nothing was lost. The replacement's
        // own count cannot: it is MaxMatches in both cases.
        if (!TryWalk(regex, subject, deadline, out List<DemoMatch> matches, out bool truncated))
        {
            return TimedOut();
        }

        return JsonSerializer.Serialize(
            new DemoAnswer(matches, truncated || clipped, null, replaced),
            DemoJson.Default.DemoAnswer
        );
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
            groups,
            // Null rather than false, so the member is omitted from every ordinary answer: partial
            // matching is one mode of three, and a "partialMatch": false on all thousand matches of
            // a walk would be a thousand copies of "this question was not asked".
            match.PartialMatch
                ? true
                : null
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
                error = $"Unknown flag '{quoted}'. Flags are FuzzyRegexOptions member names.";
                return false;
            }

            options |= one;
        }

        return true;
    }

    /// <summary>
    /// Says which of the two v2 inputs is over its cap, or <see langword="null"/> when neither is.
    /// Separate from <see cref="TooLong"/> only because that one is the S71 contract and its three
    /// messages are pinned by name.
    /// </summary>
    private static string? TooLongExtra(string replacement, string namedLists) =>
        (replacement.Length, namedLists.Length) switch
        {
            var (replacementLength, _) when replacementLength > MaxReplacementLength => string.Create(
                CultureInfo.InvariantCulture,
                $"The replacement is longer than the demo's limit of {MaxReplacementLength} characters."
            ),
            var (_, listsLength) when listsLength > MaxNamedListsLength => string.Create(
                CultureInfo.InvariantCulture,
                $"The named lists are longer than the demo's limit of {MaxNamedListsLength} characters."
            ),
            _ => null,
        };

    /// <summary>
    /// Reads the mode name, or says which one it could not read. Empty means the ordinary walk - the
    /// question the page asks unless it is asking for a partial match or a replacement - so a caller
    /// that has nothing to say about the mode says nothing.
    /// </summary>
    private static bool TryParseMode(string mode, out DemoMode parsed, out string? error)
    {
        error = null;
        switch (mode.Trim().ToLowerInvariant())
        {
            case "":
            case "match":
                parsed = DemoMode.Match;
                return true;
            case "partial":
                parsed = DemoMode.Partial;
                return true;
            case "replace":
                parsed = DemoMode.Replace;
                return true;
            default:
                parsed = DemoMode.Match;
                string quoted =
                    mode.Length > MaxQuotedTokenLength
                        ? string.Concat(mode.AsSpan(0, MaxQuotedTokenLength), "...")
                        : mode;
                error = $"Unknown mode '{quoted}'. The modes are 'match', 'partial' and 'replace'.";
                return false;
        }
    }

    /// <summary>
    /// Reads the named-list block - one list per line, <c>name: word, word</c> - or says which line
    /// it could not read.
    /// </summary>
    /// <remarks>
    /// A dictionary is only built when there is something to put in it: passing an empty one to
    /// <see cref="FuzzyRegex"/> and passing <see langword="null"/> mean the same thing to the
    /// engine, and null is what every S71 caller has always passed.
    /// </remarks>
    private static bool TryParseNamedLists(
        string namedLists,
        out Dictionary<string, IReadOnlyCollection<string>>? lists,
        out string? error
    )
    {
        lists = null;
        error = null;

        foreach (string line in namedLists.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                error = $"Line '{Quoted(line)}' needs a colon. Write one list per line, as: name: word, word, word";
                return false;
            }

            string name = line[..colon].Trim();
            string[] words =
            [
                .. line[(colon + 1)..]
                    .Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ];

            if (words.Length == 0)
            {
                error = $"The named list '{Quoted(name)}' has no words in it.";
                return false;
            }

            lists ??= new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            if (!lists.TryAdd(name, words))
            {
                error = $"The named list '{Quoted(name)}' is defined twice.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Shortens text a stranger chose before it goes into an error message, for the reason
    /// <see cref="MaxQuotedTokenLength"/> exists.
    /// </summary>
    private static string Quoted(string text) =>
        text.Length > MaxQuotedTokenLength ? string.Concat(text.AsSpan(0, MaxQuotedTokenLength), "...") : text;

    private static string Failed(string? message, int? offset = null) =>
        JsonSerializer.Serialize(
            new DemoAnswer(null, null, message ?? "Unknown error.", null, offset),
            DemoJson.Default.DemoAnswer
        );
}

/// <summary>Which question the page is asking. See <see cref="DemoEngine.Run(string, string, string, string, string, string)"/>.</summary>
internal enum DemoMode
{
    /// <summary>Walk the subject and report every match. The demo's default, and S71's only mode.</summary>
    Match,

    /// <summary>Ask for one match that may be partial.</summary>
    Partial,

    /// <summary>Walk the subject, and also apply a replacement template to it.</summary>
    Replace,
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

/// <summary>
/// One match: its span, its cost, every group, and - in partial mode only - whether the subject ran
/// out before the pattern did. <c>Partial</c> is null except when it is true, so it appears in the
/// JSON only where the question was asked.
/// </summary>
internal sealed record DemoMatch(
    int Index,
    int Length,
    DemoCounts Counts,
    IReadOnlyList<DemoGroup> Groups,
    bool? PartialMatch
);

/// <summary>
/// The whole answer. Exactly one of <c>Matches</c> and <c>Error</c> is ever set, and the null one
/// is omitted from the JSON, so a failure is literally <c>{"error": "..."}</c>. <c>Replaced</c> is
/// the rewritten subject, present in replace mode only.
/// </summary>
/// <param name="Matches">Every match found, or null when the answer is a failure.</param>
/// <param name="Truncated">Whether the engine stopped at one of its own caps.</param>
/// <param name="Error">The one sentence the page shows instead of an answer.</param>
/// <param name="Replaced">The rewritten subject. Replace mode only.</param>
/// <param name="ErrorOffset">
/// Where in the PATTERN the parse failed, so the page can draw a caret under the character upstream
/// blamed. Null - and so absent from the JSON - whenever the answer is not a pattern parse error
/// with a position: a cap refusal, a misspelt flag, a timeout, a pattern no single character of
/// which is at fault (<c>maxCompiledNodes</c>, and the three sites upstream raises with
/// <c>pos=None</c>), and a parse error raised against some other string. That last one is the case
/// worth naming: upstream parses a replacement template with the pattern parser, so a bad template's
/// position is an offset into the TEMPLATE, and drawing it under the pattern field would be a right
/// number in the wrong place. <see cref="DemoEngine"/>'s replace path catches that one itself.
/// </param>
internal sealed record DemoAnswer(
    IReadOnlyList<DemoMatch>? Matches,
    bool? Truncated,
    string? Error,
    string? Replaced,
    int? ErrorOffset = null
);

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
