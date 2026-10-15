using System.Diagnostics;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// Per-call timeouts and <see cref="CancellationToken"/> support (S51). Upstream takes a per-call
/// <c>timeout=</c> on every match method (<c>upstream/regex/_main.py</c> lines 253-298); the
/// built-in <c>Regex</c> takes a <c>matchTimeout</c> on its constructor and on eleven static
/// overloads and on no instance method at all (measured, see
/// <c>tools/probes/bcl-sync-cancellationtoken-apis.ps1</c>). This port takes both, on every method
/// whose running time depends on its input.
/// </summary>
/// <remarks>
/// <para>
/// These are gap tests: the per-call surface is this port's, so they do not count towards parity.
/// </para>
/// <para>
/// <b>Most of these tests do not need a slow pattern, and do not use one.</b> What they check is
/// plumbing: that a budget given to a call reaches the engine's clock check, and that the check's
/// answer comes back as the right exception. Until 2026-10-15 they needed a pattern too slow to
/// finish inside the budget, and every failure-memo extension threatened to make that pattern fast
/// (the seam section of <c>docs/plan/2026-10-13-linear-nested-repeats-design.md</c>). They now use
/// the test seam <see cref="FuzzyRegex.ExpiresOnCheck"/>, under which a quick pattern's first clock
/// check finds the budget spent. A budget of an hour on top means the real clock cannot fire
/// either, so a pass depends on neither the machine nor the engine's speed.
/// </para>
/// <para>
/// <b>The real clock is still exercised</b>: the per-method cancellation test and the two tests of
/// the static conveniences, which cannot reach the seam, run a genuinely slow pattern against a
/// genuine timer. That pattern is chosen so the subject cannot race the machine: at 26 characters
/// the work is about 2^26 steps and a 50 ms budget is exceeded by a margin no plausible machine
/// closes, and every assertion on it is "this threw", which a FASTER machine cannot falsify.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class TimeoutAndCancellationTests
{
    /// <summary>
    /// The pathological pattern, for the tests that need real work: exponential backtracking inside
    /// a lookahead, with no way to succeed because the subject does not end in <c>a</c>.
    /// </summary>
    /// <remarks>
    /// It used to be <c>(a|a)*\1\b\B</c>. Each piece of that was added because an optimisation
    /// defeated the one before: a literal tail fell to the required-string prefilter (S60), a
    /// tail with no literal fell to the failure memo (2026-09-26), and the backreference that kept
    /// the memo off will fall to the memo's planned backreference key. A repeat inside a lookahead
    /// is the one shape no planned memo option reaches (upstream: 1.2, 4.7, 18.7 and 77.7 ms at
    /// n = 12, 14, 16 and 18, 2^n; it is also the oracle's <c>alt-same-lookahead</c> row).
    /// <para>
    /// SHORTCUT: this holds only while nothing memoises failures inside a lookahead. Option C of the
    /// 2026-09-26 failure-memo design (depth-tagged failures) would make it fast, and these tests
    /// would then need an engine-side step budget exposed through an option, which is the upgrade
    /// path if C is ever built.
    /// </para>
    /// </remarks>
    private const string _slowPattern = "(?=(a|a)+$)a";

    /// <summary>A subject the pattern cannot match, long enough to make the search exponential.</summary>
    private static readonly string _slowSubject = new string('a', 26) + "!";

    /// <summary>A budget short enough to fire quickly and long enough not to fire on a fast call.</summary>
    private static readonly TimeSpan _shortTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The pattern the seam tests use. Any pattern that reaches the engine's first clock check would
    /// do; this one does on every method, including the splits and replacements.
    /// </summary>
    private const string _quickPattern = "a+";

    /// <summary>A subject the quick pattern matches at once.</summary>
    private const string _quickSubject = "baab";

    /// <summary>A budget the real clock cannot spend, so only the seam can fire.</summary>
    private static readonly TimeSpan _anHour = TimeSpan.FromHours(1);

    /// <summary>A quick pattern whose first clock check, in any call with a budget, finds it spent.</summary>
    /// <param name="budget">The pattern's own budget, or <see langword="null"/> for none.</param>
    /// <returns>The pattern.</returns>
    private static FuzzyRegex ExpiringAtOnce(TimeSpan? budget = null) =>
        new(_quickPattern, FuzzyRegexOptions.None, budget ?? FuzzyRegex.InfiniteMatchTimeout) { ExpiresOnCheck = 1 };

    /// <summary>
    /// Every instance method whose running time depends on its input, called with a per-call
    /// timeout. The list is the slice's scope written as code: if a method is added to the surface
    /// and not to this list, nothing here fails, so the companion reflection test below is what
    /// actually holds the line.
    /// </summary>
    /// <param name="method">Which method to call.</param>
    /// <param name="pattern">The compiled pattern to call it on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="timeout">The per-call budget to pass.</param>
    private static void CallWithTimeout(string method, FuzzyRegex pattern, string input, TimeSpan timeout)
    {
        switch (method)
        {
            case nameof(FuzzyRegex.IsMatch):
                _ = pattern.IsMatch(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.IsMatchAtStart):
                _ = pattern.IsMatchAtStart(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.IsFullMatch):
                _ = pattern.IsFullMatch(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.Match):
                _ = pattern.Match(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.MatchAtStart):
                _ = pattern.MatchAtStart(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.FullMatch):
                _ = pattern.FullMatch(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.Matches):
                // Read, because a lazy collection does its matching when it is read (2026-10-14).
                _ = pattern.Matches(input, timeout: timeout).Count;
                break;
            case nameof(FuzzyRegex.Count):
                _ = pattern.Count(input, timeout: timeout);
                break;
            case nameof(FuzzyRegex.Replace):
                _ = pattern.Replace(input, "x", timeout: timeout);
                break;
            case "ReplaceEvaluator":
                _ = pattern.Replace(input, static m => m.Value, timeout: timeout);
                break;
            case "ReplaceCounted":
                _ = pattern.Replace(input, "x", -1, out _, timeout: timeout);
                break;
            case "ReplaceEvaluatorCounted":
                _ = pattern.Replace(input, static m => m.Value, -1, out _, timeout: timeout);
                break;
            case nameof(FuzzyRegex.ReplaceFormat):
                _ = pattern.ReplaceFormat(input, "x", timeout: timeout);
                break;
            case "ReplaceFormatCounted":
                _ = pattern.ReplaceFormat(input, "x", -1, out _, timeout: timeout);
                break;
            case nameof(FuzzyRegex.Split):
                _ = pattern.Split(input, timeout: timeout);
                break;
            // Enumerated to the end, because a lazy walk does no work until it is pulled from and
            // the budget it carries is a per-step one (S53b).
            case nameof(FuzzyRegex.EnumerateMatches):
                _ = pattern.EnumerateMatches(input, timeout: timeout).ToList();
                break;
            case nameof(FuzzyRegex.EnumerateSplits):
                _ = pattern.EnumerateSplits(input, timeout: timeout).ToList();
                break;
            case "IsMatchSpan":
                _ = pattern.IsMatch(input.AsSpan(), timeout);
                break;
            case "CountSpan":
                _ = pattern.Count(input.AsSpan(), timeout);
                break;
            case "IsMatchMemory":
                _ = pattern.IsMatch(input.AsMemory(), timeout);
                break;
            case "CountMemory":
                _ = pattern.Count(input.AsMemory(), timeout);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, "no such method in this test");
        }
    }

    /// <summary>The same list again, with a token instead of a timeout.</summary>
    /// <param name="method">Which method to call.</param>
    /// <param name="pattern">The compiled pattern to call it on.</param>
    /// <param name="input">The subject.</param>
    /// <param name="token">The token to pass.</param>
    private static void CallWithToken(string method, FuzzyRegex pattern, string input, CancellationToken token)
    {
        switch (method)
        {
            case nameof(FuzzyRegex.IsMatch):
                _ = pattern.IsMatch(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.IsMatchAtStart):
                _ = pattern.IsMatchAtStart(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.IsFullMatch):
                _ = pattern.IsFullMatch(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.Match):
                _ = pattern.Match(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.MatchAtStart):
                _ = pattern.MatchAtStart(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.FullMatch):
                _ = pattern.FullMatch(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.Matches):
                _ = pattern.Matches(input, cancellationToken: token).Count;
                break;
            case nameof(FuzzyRegex.Count):
                _ = pattern.Count(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.Replace):
                _ = pattern.Replace(input, "x", cancellationToken: token);
                break;
            case "ReplaceEvaluator":
                _ = pattern.Replace(input, static m => m.Value, cancellationToken: token);
                break;
            case "ReplaceCounted":
                _ = pattern.Replace(input, "x", -1, out _, cancellationToken: token);
                break;
            case "ReplaceEvaluatorCounted":
                _ = pattern.Replace(input, static m => m.Value, -1, out _, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.ReplaceFormat):
                _ = pattern.ReplaceFormat(input, "x", cancellationToken: token);
                break;
            case "ReplaceFormatCounted":
                _ = pattern.ReplaceFormat(input, "x", -1, out _, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.Split):
                _ = pattern.Split(input, cancellationToken: token);
                break;
            case nameof(FuzzyRegex.EnumerateMatches):
                _ = pattern.EnumerateMatches(input, cancellationToken: token).ToList();
                break;
            case nameof(FuzzyRegex.EnumerateSplits):
                _ = pattern.EnumerateSplits(input, cancellationToken: token).ToList();
                break;
            case "IsMatchSpan":
                _ = pattern.IsMatch(input.AsSpan(), cancellationToken: token);
                break;
            case "CountSpan":
                _ = pattern.Count(input.AsSpan(), cancellationToken: token);
                break;
            case "IsMatchMemory":
                _ = pattern.IsMatch(input.AsMemory(), cancellationToken: token);
                break;
            case "CountMemory":
                _ = pattern.Count(input.AsMemory(), cancellationToken: token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, "no such method in this test");
        }
    }

    [Test]
    [Arguments(nameof(FuzzyRegex.IsMatch))]
    [Arguments(nameof(FuzzyRegex.IsMatchAtStart))]
    [Arguments(nameof(FuzzyRegex.IsFullMatch))]
    [Arguments(nameof(FuzzyRegex.Match))]
    [Arguments(nameof(FuzzyRegex.MatchAtStart))]
    [Arguments(nameof(FuzzyRegex.FullMatch))]
    [Arguments(nameof(FuzzyRegex.Matches))]
    [Arguments(nameof(FuzzyRegex.Count))]
    [Arguments(nameof(FuzzyRegex.Replace))]
    [Arguments("ReplaceEvaluator")]
    [Arguments("ReplaceCounted")]
    [Arguments("ReplaceEvaluatorCounted")]
    [Arguments(nameof(FuzzyRegex.ReplaceFormat))]
    [Arguments("ReplaceFormatCounted")]
    [Arguments(nameof(FuzzyRegex.Split))]
    [Arguments(nameof(FuzzyRegex.EnumerateMatches))]
    [Arguments(nameof(FuzzyRegex.EnumerateSplits))]
    [Arguments("IsMatchSpan")]
    [Arguments("CountSpan")]
    [Arguments("IsMatchMemory")]
    [Arguments("CountMemory")]
    public void A_per_call_timeout_fires_on_every_input_dependent_method(string method)
    {
        // The pattern itself has NO budget, so anything that fires here came from the call: the
        // seam fires only in a call that carries one. The second call is the control that shows
        // it: the same method with no budget answers.
        FuzzyRegex pattern = ExpiringAtOnce();

        Action call = () => CallWithTimeout(method, pattern, _quickSubject, _anHour);
        Action unbounded = () => CallWithTimeout(method, pattern, _quickSubject, FuzzyRegex.InfiniteMatchTimeout);

        call.Should().Throw<RegexMatchTimeoutException>();
        unbounded.Should().NotThrow();
    }

    [Test]
    [Arguments(nameof(FuzzyRegex.IsMatch))]
    [Arguments(nameof(FuzzyRegex.IsMatchAtStart))]
    [Arguments(nameof(FuzzyRegex.IsFullMatch))]
    [Arguments(nameof(FuzzyRegex.Match))]
    [Arguments(nameof(FuzzyRegex.MatchAtStart))]
    [Arguments(nameof(FuzzyRegex.FullMatch))]
    [Arguments(nameof(FuzzyRegex.Matches))]
    [Arguments(nameof(FuzzyRegex.Count))]
    [Arguments(nameof(FuzzyRegex.Replace))]
    [Arguments("ReplaceEvaluator")]
    [Arguments("ReplaceCounted")]
    [Arguments("ReplaceEvaluatorCounted")]
    [Arguments(nameof(FuzzyRegex.ReplaceFormat))]
    [Arguments("ReplaceFormatCounted")]
    [Arguments(nameof(FuzzyRegex.Split))]
    [Arguments(nameof(FuzzyRegex.EnumerateMatches))]
    [Arguments(nameof(FuzzyRegex.EnumerateSplits))]
    [Arguments("IsMatchSpan")]
    [Arguments("CountSpan")]
    [Arguments("IsMatchMemory")]
    [Arguments("CountMemory")]
    public void Cancellation_stops_every_input_dependent_method(string method)
    {
        // A real slow pattern and a real timer, on purpose: this is the test that keeps every
        // method's polling exercised by genuine work rather than by the seam.
        FuzzyRegex pattern = new(_slowPattern);
        using CancellationTokenSource source = new();
        source.CancelAfter(_shortTimeout);

        Action call = () => CallWithToken(method, pattern, _slowSubject, source.Token);

        call.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(source.Token);
    }

    [Test]
    [Arguments(nameof(FuzzyRegex.IsMatch))]
    [Arguments(nameof(FuzzyRegex.Match))]
    [Arguments(nameof(FuzzyRegex.Matches))]
    [Arguments(nameof(FuzzyRegex.Count))]
    [Arguments(nameof(FuzzyRegex.Replace))]
    [Arguments(nameof(FuzzyRegex.ReplaceFormat))]
    [Arguments(nameof(FuzzyRegex.Split))]
    public void An_already_cancelled_token_throws_before_any_work(string method)
    {
        // A pattern that cannot possibly match, on a subject too short for it: this is the path
        // where Substitution.Subx takes its min-width shortcut and never builds a state, so an
        // engine-only check would return normally here. The token has to be read at the door.
        FuzzyRegex pattern = new("abcdefghij");
        using CancellationTokenSource source = new();
        source.Cancel();

        Action call = () => CallWithToken(method, pattern, "z", source.Token);

        call.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(source.Token);
    }

    [Test]
    public void The_pattern_budget_applies_when_the_call_names_no_timeout()
    {
        FuzzyRegex pattern = ExpiringAtOnce(budget: _anHour);

        Action call = () => pattern.Match(_quickSubject);

        call.Should().Throw<RegexMatchTimeoutException>().Which.MatchTimeout.Should().Be(_anHour);
    }

    [Test]
    [Arguments(nameof(FuzzyRegex.EnumerateMatches))]
    [Arguments(nameof(FuzzyRegex.EnumerateSplits))]
    [Arguments(nameof(FuzzyRegex.Matches))]
    public void A_lazy_walk_does_not_charge_the_callers_time_between_steps_to_its_budget(string method)
    {
        // S61 moved the lazy walks onto one state for the whole walk, and a state carries one
        // start time, so without a clock restart per step the caller's own work between two
        // matches would time the walk out. The built-in Regex times each match of a lazy walk
        // (tools/probes/bcl-lazy-walk-timeout.cs), and DIVERGENCES keeps this port there.
        //
        // Half a second, not 50 ms, since 2026-09-28: beside a CPU burner the 50 ms version failed
        // one run in twenty, because a step that takes microseconds can still wait longer than
        // 50 ms for the processor (D13). Each spin is 1.2 budgets, so one spin alone already
        // exceeds the budget, and the four together are nearly five budgets of caller time that a
        // walk charging them to itself could not survive.
        FuzzyRegex pattern = new(@"\w+");
        TimeSpan budget = TimeSpan.FromMilliseconds(500);
        // Matches since 2026-10-14, when its collection became lazy: each match is found as the
        // foreach asks for it, so it has to restart the clock just as the other two do.
        IEnumerable<object?> walk = method switch
        {
            nameof(FuzzyRegex.EnumerateMatches) => pattern.EnumerateMatches("a b c d", timeout: budget),
            nameof(FuzzyRegex.Matches) => pattern.Matches("a b c d", timeout: budget),
            _ => pattern.EnumerateSplits("a b c d", timeout: budget),
        };

        int steps = 0;
        Action pull = () =>
        {
            foreach (object? _ in walk)
            {
                steps++;
                long started = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(started) < budget * 1.2)
                {
                    Thread.SpinWait(1_000);
                }
            }
        };

        pull.Should().NotThrow();
        steps
            .Should()
            .BeGreaterThan(3, "the walk has to spend several budgets' worth of caller time to prove anything");
    }

    [Test]
    public void A_per_call_timeout_replaces_the_pattern_budget_for_that_call()
    {
        // The pattern's own budget fires at its first check, so a call that kept it could not
        // answer. Until 2026-10-15 this used a one-tick budget and a slow subject and had to time
        // itself to show the run was long enough to prove anything; the seam makes it exact.
        FuzzyRegex pattern = ExpiringAtOnce(budget: _anHour);

        Action patternBudget = () => pattern.Match(_quickSubject);
        patternBudget.Should().Throw<RegexMatchTimeoutException>();

        Match result = pattern.Match(_quickSubject, timeout: FuzzyRegex.InfiniteMatchTimeout);

        result.Value.Should().Be("aa");
    }

    [Test]
    public void A_per_call_timeout_reaches_the_static_conveniences()
    {
        // The static conveniences build their pattern in the cache, out of the seam's reach, so this
        // test and its token twin below keep the real slow pattern and the real clock.
        Action match = static () => FuzzyRegex.Match(_slowSubject, _slowPattern, timeout: _shortTimeout);
        Action matchAtStart = static () => FuzzyRegex.MatchAtStart(_slowSubject, _slowPattern, timeout: _shortTimeout);
        Action fullMatch = static () => FuzzyRegex.FullMatch(_slowSubject, _slowPattern, timeout: _shortTimeout);
        Action isMatch = static () => FuzzyRegex.IsMatch(_slowSubject, _slowPattern, timeout: _shortTimeout);
        Action matches = static () => _ = FuzzyRegex.Matches(_slowSubject, _slowPattern, timeout: _shortTimeout).Count;
        Action count = static () => FuzzyRegex.Count(_slowSubject, _slowPattern, timeout: _shortTimeout);
        Action replace = static () => FuzzyRegex.Replace(_slowSubject, _slowPattern, "x", timeout: _shortTimeout);
        Action replaceEvaluator = static () =>
            FuzzyRegex.Replace(_slowSubject, _slowPattern, static m => m.Value, timeout: _shortTimeout);
        Action replaceFormat = static () =>
            FuzzyRegex.ReplaceFormat(_slowSubject, _slowPattern, "x", timeout: _shortTimeout);
        Action split = static () => FuzzyRegex.Split(_slowSubject, _slowPattern, timeout: _shortTimeout);

        match.Should().Throw<RegexMatchTimeoutException>();
        matchAtStart.Should().Throw<RegexMatchTimeoutException>();
        fullMatch.Should().Throw<RegexMatchTimeoutException>();
        isMatch.Should().Throw<RegexMatchTimeoutException>();
        matches.Should().Throw<RegexMatchTimeoutException>();
        count.Should().Throw<RegexMatchTimeoutException>();
        replace.Should().Throw<RegexMatchTimeoutException>();
        replaceEvaluator.Should().Throw<RegexMatchTimeoutException>();
        replaceFormat.Should().Throw<RegexMatchTimeoutException>();
        split.Should().Throw<RegexMatchTimeoutException>();
    }

    /// <summary>
    /// The public members whose running time does NOT depend on a subject, and which therefore
    /// take neither a timeout nor a token. Everything else on the surface must take both, which is
    /// what the reflection test below asserts. Adding a matching method and forgetting the pair is
    /// exactly the mistake this catches; adding a non-matching one means adding it here, on purpose.
    /// </summary>
    private static readonly HashSet<string> _membersWithNoSubjectToScan = new(StringComparer.Ordinal)
    {
        // Pattern-level: they read what the constructor already produced.
        nameof(FuzzyRegex.GroupNameFromNumber),
        nameof(FuzzyRegex.GroupNumberFromName),
        // Pure text transformation, linear in its input and with no engine behind it.
        nameof(FuzzyRegex.Escape),
        nameof(ToString),
        nameof(Equals),
        nameof(GetHashCode),
        nameof(GetType),
    };

    [Test]
    public void Every_input_dependent_public_method_takes_both_a_timeout_and_a_token()
    {
        List<string> missing = [];

        foreach (
            System.Reflection.MethodInfo method in typeof(FuzzyRegex).GetMethods(
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static
            )
        )
        {
            // Property getters have no subject and no arguments to carry a budget on.
            if (method.IsSpecialName || _membersWithNoSubjectToScan.Contains(method.Name))
            {
                continue;
            }

            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            bool hasTimeout = parameters.Any(static p =>
                string.Equals(p.Name, "timeout", StringComparison.Ordinal) && p.ParameterType == typeof(TimeSpan?)
            );
            bool tokenIsLast = parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken);

            if (!hasTimeout || !tokenIsLast)
            {
                missing.Add(
                    $"{method.Name}({string.Join(", ", parameters.Select(static p => p.ParameterType.Name))}) "
                        + $"- timeout: {hasTimeout}, token last: {tokenIsLast}"
                );
            }
        }

        missing.Should().BeEmpty();
    }

    [Test]
    public void The_token_is_the_last_parameter_everywhere_it_appears()
    {
        // CA1068's rule, held on this port's own surface rather than trusted: "It's considered a
        // good API design practice to have such parameters be the last parameter in the list."
        // Measured on the framework the same day (tools/probes/bcl-sync-cancellationtoken-apis.ps1):
        // 28 of 28 synchronous BCL methods that take a CancellationToken put it last.
        List<string> outOfPlace = [];

        foreach (
            System.Reflection.MethodInfo method in typeof(FuzzyRegex).GetMethods(
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static
            )
        )
        {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            int index = Array.FindIndex(parameters, static p => p.ParameterType == typeof(CancellationToken));

            if (index >= 0 && index != parameters.Length - 1)
            {
                outOfPlace.Add($"{method.Name}: token at {index + 1} of {parameters.Length}");
            }
        }

        outOfPlace.Should().BeEmpty();
    }

    [Test]
    public void A_cancellation_token_reaches_the_static_conveniences()
    {
        using CancellationTokenSource source = new();
        source.CancelAfter(_shortTimeout);

        Action match = () => FuzzyRegex.Match(_slowSubject, _slowPattern, cancellationToken: source.Token);
        Action isMatch = () => FuzzyRegex.IsMatch(_slowSubject, _slowPattern, cancellationToken: source.Token);
        Action matches = () => FuzzyRegex.Matches(_slowSubject, _slowPattern, cancellationToken: source.Token);
        Action split = () => FuzzyRegex.Split(_slowSubject, _slowPattern, cancellationToken: source.Token);

        match.Should().Throw<OperationCanceledException>();
        isMatch.Should().Throw<OperationCanceledException>();
        matches.Should().Throw<OperationCanceledException>();
        split.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void The_timeout_exception_carries_the_pattern_the_input_and_the_per_call_budget()
    {
        FuzzyRegex pattern = ExpiringAtOnce();

        Action call = () => pattern.Match(_quickSubject, timeout: _anHour);

        RegexMatchTimeoutException thrown = call.Should().Throw<RegexMatchTimeoutException>().Which;
        thrown.Pattern.Should().Be(_quickPattern);
        thrown.Input.Should().Be(_quickSubject);

        // The budget reported is the one the CALL asked for, not the pattern's - the pattern has
        // none, so reporting InfiniteMatchTimeout here would be actively misleading.
        thrown.MatchTimeout.Should().Be(_anHour);
    }

    [Test]
    [Arguments(0L)]
    [Arguments(-5L)]
    public void A_per_call_timeout_that_is_neither_positive_nor_infinite_is_rejected(long milliseconds)
    {
        FuzzyRegex pattern = new("a");

        Action call = () => pattern.Match("a", timeout: TimeSpan.FromMilliseconds(milliseconds));

        call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("timeout");
    }

    [Test]
    public void A_bad_timeout_is_rejected_even_when_the_token_is_already_cancelled()
    {
        // Argument validation is a trust boundary and comes first, as the constructor's own comment
        // says; a cancelled token is the operation's outcome, not a licence to stop checking the
        // caller's arguments. TimeSpan.Zero is a programming error the caller has to fix, and
        // hiding it behind an OperationCanceledException means they never see it. The built-in
        // Regex does the same: Regex.Match("a", "a", RegexOptions.None, TimeSpan.Zero) throws
        // ArgumentOutOfRangeException. Found by S51's blind review.
        FuzzyRegex pattern = new("a");
        using CancellationTokenSource source = new();
        source.Cancel();

        Action call = () => pattern.Match("a", timeout: TimeSpan.Zero, cancellationToken: source.Token);

        call.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("timeout");
    }

    [Test]
    public void A_token_that_is_never_cancelled_changes_nothing()
    {
        FuzzyRegex pattern = new("b(a)r");

        Match match = pattern.Match("foobarbaz", cancellationToken: CancellationToken.None);

        match.Success.Should().BeTrue();
        match.Value.Should().Be("bar");
        match.Groups[1].Value.Should().Be("a");
    }

    [Test]
    public void A_timeout_and_a_token_compose_and_the_token_wins_when_both_fire()
    {
        // Both are armed and the token is cancelled first, so the caller's own request is what is
        // reported: OperationCanceledException, not "the library ran out of time". The seam makes
        // the budget spent at the first check, so both really have fired.
        FuzzyRegex pattern = ExpiringAtOnce();
        using CancellationTokenSource source = new();
        source.Cancel();

        Action call = () => pattern.Match(_quickSubject, timeout: _anHour, cancellationToken: source.Token);

        call.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(source.Token);
    }
}
