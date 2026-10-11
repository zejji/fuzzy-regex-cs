using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A <c>beginning</c> that lands between the two halves of a surrogate pair. This surface takes
/// UTF-16 indices, so a caller can ask for it; upstream indexes by codepoint and cannot. The rule
/// is the one <c>MatchSpineTests.A_length_that_cuts_a_surrogate_pair_leaves_a_lone_surrogate_that_matches_as_one_character</c>
/// pinned for the other edge (DECISIONS 2026-09-01): the cut leaves two lone surrogates, each one
/// character, and nothing inside the slice pairs with anything outside it.
/// </summary>
/// <remarks>
/// That rule has an oracle after all. A Python <c>str</c> can hold a high and a low surrogate side
/// by side as two separate codepoints, which is exactly the text a cut pair denotes, and a codepoint
/// <c>pos</c> can then fall between them. Every expected value below is upstream's answer over that
/// string, regex 2026.9.10, measured 2026-09-26, with <c>H</c> and <c>L</c> the two halves built
/// by <c>chr</c>.
/// </remarks>
public sealed class SliceSplitsSurrogatePairTests
{
    /// <summary>
    /// Far more than any call here needs, and short enough that a hang is a red test rather than a
    /// stalled suite. The assembly-wide timeout does not stop a CPU-bound synchronous body.
    /// </summary>
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    /// <summary>Runs one call, failing with a <see cref="TimeoutException"/> if it hangs.</summary>
    private static Task<Match> Bounded(Func<Match> call) => Task.Run(call).WaitAsync(_bound);

    [Test]
    public async Task A_greedy_repeat_backtracks_to_a_beginning_on_a_low_surrogate_and_stops_there()
    {
        // The hang a grid found: backtracking one character at a time from the end of the repeat,
        // the engine stepped from 2 over the whole pair to 0, below the slice start at 1, and its
        // stop test - an equality with the slice start - never came true again.
        //   regex.fullmatch(r'(?:.)*x', H + L + 'xc', pos=1, endpos=4)  -> None
        //   regex.fullmatch(r'(?:.)*x', H + L + 'xc', pos=1, endpos=3)  -> (1, 3)
        const string subject = "\U00010428xc";
        FuzzyRegex pattern = new("(?:.)*x", FuzzyRegexOptions.None, TimeSpan.FromSeconds(2));

        Match whole = await Bounded(() => pattern.FullMatch(subject, 1, 3)).ConfigureAwait(false);
        whole.Success.Should().BeFalse();

        Match shorter = await Bounded(() => pattern.FullMatch(subject, 1, 2)).ConfigureAwait(false);
        (shorter.Success, shorter.Index, shorter.Length).Should().Be((true, 1, 2));
    }

    [Test]
    public async Task A_repeat_with_nothing_after_it_does_not_walk_below_the_slice_either()
    {
        // The same walk where the tail is the end-of-match test, which never reads the clock, so
        // the engine's own timeout could not stop it. U+1F600 is cut at 1; '.' stops at the '\n'.
        //   t = H + L + '\U0001F601x\U0001F602\na'
        //   regex.fullmatch(r'(?:.)*', t, pos=1, endpos=6)  -> None
        //   regex.fullmatch(r'(?:.)*', t, pos=1, endpos=5)  -> (1, 5), UTF-16 (1, 7)
        const string subject = "\U0001F600\U0001F601x\U0001F602\na";
        FuzzyRegex pattern = new("(?:.)*", FuzzyRegexOptions.None, TimeSpan.FromSeconds(2));

        Match whole = await Bounded(() => pattern.FullMatch(subject, 1, 7)).ConfigureAwait(false);
        whole.Success.Should().BeFalse();

        Match shorter = await Bounded(() => pattern.FullMatch(subject, 1, 6)).ConfigureAwait(false);
        (shorter.Success, shorter.Index, shorter.Length).Should().Be((true, 1, 6));
    }

    [Test]
    public async Task A_reversed_repeat_takes_the_lone_low_surrogate_and_stops_at_the_slice_start()
    {
        // Walking back from the slice end, the low half at the slice start is one character on
        // its own; it used to pair with the high half before the slice and report a match
        // starting outside it.
        //   regex.search(r'(?r)[\s\S]*', 'a' + H + L, pos=2)  -> (2, 3)
        Match m = await Bounded(static () => new FuzzyRegex(@"(?r)[\s\S]*").Match("a\U0001F600", 2))
            .ConfigureAwait(false);

        (m.Success, m.Index, m.Length).Should().Be((true, 2, 1));
    }

    [Test]
    public async Task The_text_before_the_slice_ends_in_a_lone_high_surrogate()
    {
        // The other half of the cut: a lookbehind or a word boundary at the slice start reads the
        // high half alone, not the whole character.
        //   regex.search(r'(?<=\ud83d).', 'a' + H + L, pos=2)        -> (2, 3)
        //   regex.search(r'(?<=\U0001F600).', 'a' + H + L, pos=2)    -> None
        //   regex.search(r'\B', 'a' + H + L, pos=2)                  -> (2, 2)
        const string subject = "a\U0001F600";

        Match high = await Bounded(static () => new FuzzyRegex(@"(?<=\ud83d).").Match(subject, 2))
            .ConfigureAwait(false);
        (high.Success, high.Index, high.Length).Should().Be((true, 2, 1));

        Match astral = await Bounded(static () => new FuzzyRegex(@"(?<=\U0001F600).").Match(subject, 2))
            .ConfigureAwait(false);
        astral.Success.Should().BeFalse();

        Match boundary = await Bounded(static () => new FuzzyRegex(@"\B").Match(subject, 2)).ConfigureAwait(false);
        (boundary.Success, boundary.Index, boundary.Length).Should().Be((true, 2, 0));

        // And a forward step from the high half stops at the cut: a lookahead inside a lookbehind
        // reads one lone character there, not the pair.
        //   regex.search(r'(?<=(?=(.)).).', 'a' + H + L + 'x', pos=2)  -> (2, 3), group 1 (1, 2)
        Match ahead = await Bounded(static () => new FuzzyRegex(@"(?<=(?=(.)).).").Match("a\U0001F600x", 2))
            .ConfigureAwait(false);
        (ahead.Index, ahead.Length, ahead.Groups[1].Index, ahead.Groups[1].Length).Should().Be((2, 1, 1, 1));
    }
}
