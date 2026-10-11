namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// What a match state has learnt about where the subject's high surrogates are, in a form one
/// state can hand to the next: the code units <c>[CleanStart, CleanEnd)</c> hold none, and
/// <paramref name="Found"/> says whether one was met at an edge of that stretch.
/// </summary>
/// <remarks>
/// Not an upstream type; see <see cref="MatchState.NoHighSurrogateIn"/>. The default is "nothing
/// learnt yet", which is always correct and only costs a state the scanning it would have done
/// anyway. <see cref="Match.NextMatch"/> carries the last state's value to the next one so that a
/// walk examines each unit about once rather than once per match.
/// </remarks>
/// <param name="CleanStart">The first unit of the stretch known to hold no high surrogate.</param>
/// <param name="CleanEnd">One past its last unit.</param>
/// <param name="Found">Whether a high surrogate was found next to the stretch.</param>
internal readonly record struct SubjectScan(int CleanStart, int CleanEnd, bool Found);
