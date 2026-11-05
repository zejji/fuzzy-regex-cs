using System.Collections.Concurrent;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The formulas <see cref="AsciiAnswers"/> computes a case-insensitive character's and range's
/// ASCII answers with (D141), against the predicates they stand in for: <see cref="Matcher.SameCharIgn"/>
/// and <see cref="Matcher.InRangeIgn"/>.
/// </summary>
public sealed class AsciiAnswerFormulaTests
{
    private static readonly CaseEncoding[] _encodings = [CaseEncoding.Ascii, CaseEncoding.Unicode];

    [Test]
    public void The_only_cases_of_ascii_codepoints_outside_ascii_are_long_s_and_kelvin_sign()
    {
        // The formulas' premise. Were the case tables to give an ASCII codepoint a third non-ASCII
        // partner, both formulas would need it, so this fails first and says which.
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        foreach (CaseEncoding encoding in _encodings)
        {
            var asciiPartners = new List<(uint, uint)>();
            var outside = new SortedSet<uint>();
            for (uint ch = 0; ch < 128; ch++)
            {
                int count = Encodings.AllCases(encoding, ch, cases);
                foreach (uint other in cases[..count])
                {
                    if (other >= 128)
                    {
                        outside.Add(other);
                    }
                    else if (other != ch)
                    {
                        asciiPartners.Add((ch, other));
                    }
                }
            }

            outside.Should().Equal(encoding == CaseEncoding.Unicode ? [0x017F, 0x212A] : [], encoding.ToString());
            asciiPartners
                .Should()
                .OnlyContain(static pair => IsAsciiLetter(pair.Item1) && pair.Item2 == (pair.Item1 ^ 0x20));
            asciiPartners.Should().HaveCount(52, encoding.ToString());
        }
    }

    [Test]
    public void The_character_formula_agrees_with_the_predicate_for_every_codepoint()
    {
        var wrong = new ConcurrentQueue<string>();

        foreach (CaseEncoding encoding in _encodings)
        {
            Parallel.For(
                0,
                0x110,
                block =>
                {
                    Span<ulong> answers = stackalloc ulong[2];
                    for (uint c = (uint)block << 12; c < (uint)(block + 1) << 12; c++)
                    {
                        answers.Clear();
                        AsciiAnswers.CharacterIgn(encoding, c, answers);
                        for (uint ch = 0; ch < 128; ch++)
                        {
                            if (Bit(answers, ch) != Matcher.SameCharIgn(encoding, c, ch))
                            {
                                wrong.Enqueue($"{encoding} U+{c:X4} U+{ch:X4}");
                            }
                        }
                    }
                }
            );
        }

        wrong.Take(20).Should().BeEmpty();
    }

    [Test]
    public void The_range_formula_agrees_with_the_predicate_for_every_distinct_range()
    {
        // An ASCII codepoint's answer depends only on which of its cases the range holds, and those
        // cases are ASCII, U+017F or U+212A (the test above). So two ranges that hold the same of
        // those codepoints answer alike, and bounds drawn from them and their neighbours reach every
        // such intersection, including reversed bounds, which hold nothing.
        uint[] bounds =
        [
            .. Enumerable.Range(0, 130).Select(static n => (uint)n),
            0x017E,
            0x017F,
            0x0180,
            0x2129,
            0x212A,
            0x212B,
            0x10FFFF,
        ];
        var wrong = new List<string>();
        Span<ulong> answers = stackalloc ulong[2];
        int ranges = 0;

        foreach (CaseEncoding encoding in _encodings)
        {
            foreach (uint lower in bounds)
            {
                foreach (uint upper in bounds)
                {
                    ++ranges;
                    answers.Clear();
                    AsciiAnswers.RangeIgn(encoding, lower, upper, answers);
                    for (uint ch = 0; ch < 128; ch++)
                    {
                        if (Bit(answers, ch) != Matcher.InRangeIgn(encoding, lower, upper, ch))
                        {
                            wrong.Add($"{encoding} [U+{lower:X4}-U+{upper:X4}] U+{ch:X4}");
                        }
                    }
                }
            }
        }

        wrong.Take(20).Should().BeEmpty();
        ranges.Should().Be(2 * 137 * 137);
    }

    [Test]
    public void The_range_formula_agrees_with_the_predicate_for_random_ranges()
    {
        // A second net, not resting on the argument above: random bounds over the whole codespace,
        // biased towards the low end where the answers change.
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(20261105);
        var wrong = new List<string>();
        Span<ulong> answers = stackalloc ulong[2];

        for (int i = 0; i < 20_000; i++)
        {
            CaseEncoding encoding = _encodings[i & 1];
            uint lower = Draw(random);
            uint upper = Draw(random);
            answers.Clear();
            AsciiAnswers.RangeIgn(encoding, lower, upper, answers);
            for (uint ch = 0; ch < 128; ch++)
            {
                if (Bit(answers, ch) != Matcher.InRangeIgn(encoding, lower, upper, ch))
                {
                    wrong.Add($"{encoding} [U+{lower:X4}-U+{upper:X4}] U+{ch:X4}");
                }
            }
        }

        wrong.Take(20).Should().BeEmpty();

        static uint Draw(Random random) =>
            random.Next(4) switch
            {
                0 => (uint)random.Next(0, 0x80),
                1 => (uint)random.Next(0, 0x200),
                2 => (uint)random.Next(0, 0x3000),
                _ => (uint)random.Next(0, 0x110000),
            };
#pragma warning restore CA5394, S2245
    }

    private static bool IsAsciiLetter(uint ch) => ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static bool Bit(ReadOnlySpan<ulong> answers, uint ch) => ((answers[(int)(ch >> 6)] >> (int)ch) & 1) != 0;
}
