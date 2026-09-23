using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// S61. A state <see cref="MatchStateCache"/> hands out a second time must be the state
/// <see cref="MatchState.Create"/> would have built, field for field, whatever the first match left
/// in it.
/// </summary>
/// <remarks>
/// <para>
/// The comparison reads every instance field of <see cref="MatchState"/> by reflection, so a field
/// added later is compared without anyone remembering to add it here, and a field of a type the
/// comparison has no rule for fails the test rather than being skipped. Buffers that keep their
/// capacity between matches - a group's captures, a guard list's spans - are compared up to their
/// count, which is all the engine reads.
/// </para>
/// <para>
/// Each case dirties the state with one call and reuses it for a different one: another subject,
/// another slice, and the opposite settings for partial, visible captures and match-all. The two
/// states are compared once before matching and once after, so a field a reused state carries
/// into the match shows either as a difference at the start or as a different match.
/// </para>
/// </remarks>
public sealed class MatchStateCacheTests
{
    public static IEnumerable<Func<(string Pattern, string Dirty, string Subject)>> Wave()
    {
        // Captures, repeats and their guards.
        yield return static () => (@"(a|b)*c", "ababababababababx", "abc");
        yield return static () => (@"(?:(\w)(\d))+z", "a1b2c3d4e5f6g7", "x9z");
        // Fuzzy: counts, changes and the error totals.
        yield return static () => ("(?:colour){e<=2}", "the colr of the cloour", "colour");
        yield return static () => ("(?e)(?:amber lantern){e<=3}", "amebr lnatern", "amber lantern");
        // BESTMATCH and POSIX keep a best match of their own.
        yield return static () => ("(?b)(?:abcde){e<=2}", "xabxdexabcdxe", "abcde");
        yield return static () => ("(?p)(a|ab)(c|bcd)", "abcdabcd", "abcd");
        // Group calls, which leave entries in the open-call set and list.
        yield return static () => (@"(a(?1)?b)", "aaabbbaab", "ab");
        yield return static () => (@"(?<x>\((?&x)*\))", "((()())(", "()");
        // Lookaround, atomic groups, backreferences and the verbs.
        yield return static () => (@"(?=(\w+))\1(?>a+)b", "wordaab", "aab");
        yield return static () => (@"(\w)\1", "abcdeff", "gg");
        yield return static () => (@"a(*SKIP)b|ac", "aaacab", "ac");
        yield return static () => (@"a+(*PRUNE)b|a+c", "aaac", "ab");
        // Reverse, and a subject with a surrogate pair, which builds the character index.
        yield return static () => (@"(?r)ab+", "abbbbab", "ab");
        yield return static () => (@".{1,3}?x", "\U0001F600\U0001F600\U0001F600x\U0001F600", "yyx");
        // A partial match that reaches the end of the subject.
        yield return static () => (@"\w+\b(?!x)", "abcd", "ab");
    }

    [Test]
    [MethodDataSource(nameof(Wave))]
    public void A_reused_state_is_the_state_Create_builds(string pattern, string dirty, string subject)
    {
        FuzzyRegex regex = new(pattern);
        var cache = new MatchStateCache();

        MatchState first = cache.Rent(
            regex.PatternObject,
            dirty.AsMemory(),
            0,
            dirty.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(first, search: true);
        if (!first.OneUnitPerCharacter)
        {
            _ = first.GetCharacterIndex();
        }

        cache.Return(first);

        MatchState reused = cache.Rent(
            regex.PatternObject,
            subject.AsMemory(),
            1,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: true,
            regex.PatternLimits
        );
        using MatchState fresh = MatchState.Create(
            regex.PatternObject,
            subject.AsMemory(),
            1,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: true,
            regex.PatternLimits
        );

        reused.Should().BeSameAs(first, "the cache must hand the kept state back, or this compares two new ones");
        Describe(reused).Should().Equal(Describe(fresh), "a reused state must start where a new one does");

        int reusedStatus = Matcher.DoMatch(reused, search: false);
        int freshStatus = Matcher.DoMatch(fresh, search: false);

        reusedStatus.Should().Be(freshStatus);
        Describe(reused).Should().Equal(Describe(fresh), "and must finish where a new one does");

        cache.Return(reused);
    }

    [Test]
    [Arguments("(?:(a)b)+(?:bc){e<=1}", "ababx")]
    [Arguments(@"(?r)(?:(\w)x)+(?1)", "x\U0001F600axbx")]
    public void A_reused_state_forgets_every_field_its_last_call_set(string pattern, string subject)
    {
        // The wave above dirties what real matches dirty, which is not every field: a match that
        // ends normally closes its own group calls, for one. So this writes a value that no new
        // state holds into every field, by reflection, and asks the reuse to undo all of it.
        FuzzyRegex regex = new(pattern);
        var cache = new MatchStateCache();
        MatchState first = cache.Rent(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );

        Scribble(first);
        cache.Return(first);

        MatchState reused = cache.Rent(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: true,
            regex.PatternLimits
        );
        using MatchState fresh = MatchState.Create(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: true,
            regex.PatternLimits
        );

        reused.Should().BeSameAs(first);
        Describe(reused).Should().Equal(Describe(fresh));
        cache.Return(reused);
    }

    [Test]
    public void The_comparison_sees_a_field_that_Init_forgets()
    {
        // Non-vacuity: a difference in one scalar, one list and one nested buffer must each show.
        FuzzyRegex regex = new("(a)+(?:bc){e<=1}");
        using MatchState one = Create(regex, "aabx");
        using MatchState two = Create(regex, "aabx");

        Describe(one).Should().Equal(Describe(two));

        two.TotalErrors = 1;
        Describe(one).Should().NotEqual(Describe(two));

        two.TotalErrors = 0;
        two.FuzzyChanges.Add(new FuzzyChange(0, 0));
        Describe(one).Should().NotEqual(Describe(two));

        two.FuzzyChanges.Clear();
        two.Groups[0].Current = 0;
        Describe(one).Should().NotEqual(Describe(two));
    }

    private static MatchState Create(FuzzyRegex regex, string subject) =>
        MatchState.Create(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );

    /// <summary>
    /// Writes a value no new state holds into every field but <see cref="MatchState.Pattern"/>, and
    /// fails if any field's rendering is unchanged afterwards - so a field this has no rule for
    /// cannot slip past the test that uses it.
    /// </summary>
    private static void Scribble(MatchState state)
    {
        // The character index first, while the subject and its bounds are still the real ones.
        IReadOnlyList<FieldInfo> fields = [.. InstanceFields(typeof(MatchState))];
        foreach (
            FieldInfo field in fields
                .Where(static field => field.FieldType == typeof(CharacterIndex))
                .Concat(fields.Where(static field => field.FieldType != typeof(CharacterIndex)))
        )
        {
            if (string.Equals(field.Name, nameof(MatchState.Pattern), StringComparison.Ordinal))
            {
                continue;
            }

            var before = new List<string>();
            Render(field.Name, field.GetValue(state), before);

            ScribbleField(state, field);

            var after = new List<string>();
            Render(field.Name, field.GetValue(state), after);
            after.Should().NotEqual(before, $"{field.Name} must be scribbled, or the reuse test cannot see it");
        }
    }

    private static void ScribbleField(MatchState state, FieldInfo field)
    {
        object? value = field.GetValue(state);
        switch (value)
        {
            case bool flag:
                field.SetValue(state, !flag);
                break;
            case int number:
                field.SetValue(state, number + 7);
                break;
            case long number:
                field.SetValue(state, number + 7);
                break;
            case ushort number:
                field.SetValue(state, (ushort)(number + 7));
                break;
            case string:
                field.SetValue(state, "scribbled");
                break;
            case ReadOnlyMemory<char>:
                field.SetValue(state, "scribbled".AsMemory());
                break;
            case Enum:
                // GetValuesAsUnderlyingType rather than GetValues(Type), which Native AOT cannot
                // promise (IL3050, tools/run-aot-tests.ps1).
                Array values = Enum.GetValuesAsUnderlyingType(field.FieldType);
                object last = Enum.ToObject(field.FieldType, values.GetValue(values.Length - 1)!);
                object first = Enum.ToObject(field.FieldType, values.GetValue(0)!);
                field.SetValue(state, last.Equals(value) ? first : last);
                break;
            case CancellationToken:
                field.SetValue(state, new CancellationToken(canceled: true));
                break;
            case long[] numbers:
                Array.Fill(numbers, 7);
                break;
            case GroupData[] groups:
                foreach (GroupData group in groups)
                {
                    group.Captures = [new GroupSpan(1, 2)];
                    group.Count = 1;
                    group.Current = 0;
                }

                break;
            case RepeatData[] repeats:
                foreach (RepeatData repeat in repeats)
                {
                    repeat.Count = 7;
                    repeat.Start = 7;
                    repeat.CaptureChange = 7;
                    ScribbleGuards(repeat.BodyGuardList);
                    ScribbleGuards(repeat.TailGuardList);
                }

                break;
            case ByteStack stack:
                stack.Push(7);
                break;
            case HashSet<long> set:
                set.Add(7);
                break;
            case List<FuzzyChange> changes:
                changes.Add(new FuzzyChange(1, 7));
                break;
            case List<(long Key, int SstackDepth)> calls:
                calls.Add((7, 7));
                break;
            case null when field.FieldType == typeof(Node):
                field.SetValue(state, state.Pattern.NodeList[0]);
                break;
            case null when field.FieldType == typeof(GroupData[]):
                field.SetValue(state, new GroupData[1]);
                break;
            case null when field.FieldType == typeof(CharacterIndex):
                _ = state.GetCharacterIndex();
                break;
            default:
                throw new InvalidOperationException(
                    $"{field.Name} is a {field.FieldType}, which Scribble has no rule for; add one"
                );
        }
    }

    private static void ScribbleGuards(GuardList guards)
    {
        typeof(GuardList)
            .GetField("_spans", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(guards, (GuardSpan[])[new GuardSpan(1, 2, Protect: true)]);
        guards.Count = 1;
    }

    /// <summary>Every field of the state, rendered one per line as <c>path=value</c>.</summary>
    private static List<string> Describe(MatchState state)
    {
        var lines = new List<string>();
        foreach (FieldInfo field in InstanceFields(typeof(MatchState)))
        {
            Render(field.Name, field.GetValue(state), lines);
        }

        return lines;
    }

    // MatchState.Cache is left out: it says where the state came from, which is the one thing a
    // rented state and a new one are meant to differ in, and it is readonly, so no call can set it.
    private static IOrderedEnumerable<FieldInfo> InstanceFields(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static field => field.FieldType != typeof(MatchStateCache))
            .OrderBy(static field => field.Name, StringComparer.Ordinal);

    private static void Render(string path, object? value, List<string> lines)
    {
        switch (value)
        {
            case null:
                lines.Add($"{path}=null");
                break;
            case string or bool or int or long or ushort or byte or Enum:
                lines.Add($"{path}={Convert.ToString(value, CultureInfo.InvariantCulture)}");
                break;
            case CancellationToken token:
                lines.Add($"{path}={token.Equals(CancellationToken.None)}");
                break;
            case ReadOnlyMemory<char> text:
                lines.Add($"{path}={text}");
                break;
            case CharacterIndex:
                // Each state builds its own, so the question is only whether one exists.
                lines.Add($"{path}=built");
                break;
            case PatternObject or Node:
                // Shared with the pattern, so identity is the question.
                lines.Add($"{path}=#{RuntimeHelpers.GetHashCode(value)}");
                break;
            case GroupData group:
                lines.Add($"{path}.Count={group.Count}");
                lines.Add($"{path}.Current={group.Current}");
                RenderItems($"{path}.Captures", group.Captures.Take(group.Count), lines);
                break;
            case RepeatData repeat:
                lines.Add($"{path}=({repeat.Count},{repeat.Start},{repeat.CaptureChange})");
                Render($"{path}.Body", repeat.BodyGuardList, lines);
                Render($"{path}.Tail", repeat.TailGuardList, lines);
                break;
            case GuardList guards:
                RenderItems(path, ((GuardSpan[])Private(guards, "_spans")).Take(guards.Count), lines);
                break;
            case ByteStack stack:
                RenderItems(path, ((byte[])Private(stack, "_storage")).Take(stack.Count), lines);
                break;
            case HashSet<long> set:
                RenderItems(path, set.Order(), lines);
                break;
            case Array array and (long[] or GroupData[] or RepeatData[]):
                int index = 0;
                foreach (object? item in array)
                {
                    Render($"{path}[{index++}]", item, lines);
                }

                lines.Add($"{path}.Length={index}");
                break;
            case IList list:
                RenderItems(path, list.Cast<object>(), lines);
                break;
            default:
                throw new InvalidOperationException(
                    $"{path} is a {value.GetType()}, which this comparison has no rule for; add one"
                );
        }
    }

    private static void RenderItems<T>(string path, IEnumerable<T> items, List<string> lines) =>
        lines.Add($"{path}=[{string.Join(", ", items)}]");

    private static object Private(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
