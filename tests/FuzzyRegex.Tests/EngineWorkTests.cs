using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// The step limits of <see cref="WorkCounter"/>, which <see cref="EngineWork"/> relies on to stop a
/// runaway. Debug builds only: in Release the counters do not exist and these tests return at once.
/// </summary>
public sealed class EngineWorkTests
{
    [Test]
    public void A_step_limit_stops_the_engine_again_after_a_caller_swallows_it()
    {
        if (!WorkCounter.Enabled)
        {
            return;
        }

        // The demo's interop boundary catches every exception, so the first stop can be swallowed
        // and matching can go on. The limit must fire at every step past it, not only at the step
        // that reaches it: with that, the second call ran on until its two-second timeout.
        var regex = new FuzzyRegex(@"(a|a)*\1\b\B", FuzzyRegexOptions.None, TimeSpan.FromSeconds(2));
        string subject = new('a', 30);
        long before = WorkCounter.Steps;
        WorkCounter.StepLimit = before + 1_000;
        try
        {
            Action search = () => regex.IsMatch(subject);

            search.Should().Throw<StepLimitReachedException>();
            search.Should().Throw<StepLimitReachedException>("the limit is still passed");
            (WorkCounter.Steps - before).Should().Be(1_001, "each call stops at its first step past the limit");
        }
        finally
        {
            WorkCounter.StepLimit = 0;
        }
    }

    [Test]
    public void A_character_limit_stops_the_engine_again_after_a_caller_swallows_it()
    {
        if (!WorkCounter.Enabled)
        {
            return;
        }

        // The same rule for the walk limit. Without it, the second call walked the subject to the
        // end and answered.
        var regex = new FuzzyRegex(".*?cd", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = string.Concat(Enumerable.Repeat("x\U0001F600y", 1_000)) + "cd";
        long before = WorkCounter.CharactersWalked;
        WorkCounter.CharacterLimit = before + 100;
        try
        {
            Action match = () => regex.MatchAtStart(subject);

            match.Should().Throw<StepLimitReachedException>();
            match.Should().Throw<StepLimitReachedException>("the limit is still passed");
        }
        finally
        {
            WorkCounter.CharacterLimit = 0;
        }
    }
}
