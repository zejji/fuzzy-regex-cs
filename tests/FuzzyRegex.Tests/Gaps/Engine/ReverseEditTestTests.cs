using System.Text;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D155: a fuzzy edit test (the <c>:[...]</c> after a constraint) in a reversed
/// pattern tests exactly what it tests forward.
/// </summary>
/// <remarks>
/// <para>
/// A set means the same characters whichever way the pattern reads the text, and a fullmatch over
/// the whole text has no direction of its own, so <c>(?r)</c> and forward must accept the same
/// texts. Upstream's <c>fuzzy_ext_match</c> (<c>upstream/src/_regex.c:9938</c>) has no arm for the
/// reversed set opcodes, so a reversed test whose set holds two or more separate pieces (a
/// <c>SET_*_REV</c> node; one character or one range compiles to CHARACTER or RANGE, which have arms)
/// falls off the switch to <c>return true</c> and lets an edit bring in any character.
/// </para>
/// <para>
/// Measured on <c>regex</c> 2026.9.29 (2026-11-16): fullmatch <c>(?r)(?:abc){s&lt;=1:[xy]}</c> on
/// 'adc' is <c>(0, 3)</c> with one substitution, and forward <c>None</c>; the same with
/// <c>[x]</c>, <c>[x-z]</c>, <c>[0-9]</c> or <c>[b]</c> is <c>None</c> both ways. This port answered
/// as upstream does until D155.
/// </para>
/// </remarks>
public sealed class ReverseEditTestTests
{
    [Test]
    [Arguments("[xy]")]
    [Arguments("[xyz]")]
    [Arguments("[pq]")]
    [Arguments("[x]")]
    [Arguments("[x-z]")]
    [Arguments("[0-9]")]
    [Arguments("[b]")]
    [Arguments("[^dq]")]
    [Arguments("[0-9xy]")]
    public void A_reversed_substitution_test_refuses_a_character_outside_its_set(string test)
    {
        // upstream (?r): [xy], [xyz], [pq], [0-9xy] and [^dq] give (0, 3); the rest None.
        Accepts($"(?r)(?:abc){{s<=1:{test}}}", "adc").Should().BeFalse();
        Accepts($"(?:abc){{s<=1:{test}}}", "adc").Should().BeFalse();
    }

    [Test]
    [Arguments("[dy]")]
    [Arguments("[xd]")]
    [Arguments("[^xy]")]
    [Arguments("[a-ex]")]
    public void A_reversed_substitution_test_allows_a_character_inside_its_set(string test)
    {
        Accepts($"(?r)(?:abc){{s<=1:{test}}}", "adc").Should().BeTrue();
        Accepts($"(?:abc){{s<=1:{test}}}", "adc").Should().BeTrue();
    }

    [Test]
    public void A_reversed_insertion_test_refuses_a_character_outside_its_set()
    {
        Accepts("(?r)(?:ab){i<=1:[xy]}", "a1b").Should().BeFalse();
        Accepts("(?r)(?:ab){i<=1:[xy]}", "ayb").Should().BeTrue();
        Accepts("(?r)(?:ab){i<=1:[0-9x]}", "a1b").Should().BeTrue();
    }

    [Test]
    public void A_deletion_touches_no_text_character_so_the_test_never_refuses_it()
    {
        Accepts("(?r)(?:abc){d<=1:[xy]}", "ac").Should().BeTrue();
        Accepts("(?:abc){d<=1:[xy]}", "ac").Should().BeTrue();
    }

    [Test]
    public void The_witnesses_the_defect_was_found_with_refuse_both_ways()
    {
        // Upstream and this port before D155 matched both reversed, with two substitutions and with
        // an insertion and a deletion.
        Accepts(@"(?r)\A(?:tesco){e<=2:[xyz]}\Z", "tecso").Should().BeFalse();
        Accepts(@"\A(?:tesco){e<=2:[xyz]}\Z", "tecso").Should().BeFalse();
        Accepts(@"(?r)\A(?:ab){i<=1,d<=1:[xyz]}\Z", "ba").Should().BeFalse();
        Accepts(@"\A(?:ab){i<=1,d<=1:[xyz]}\Z", "ba").Should().BeFalse();

        // The same edits on characters the set holds are still allowed.
        Accepts(@"(?r)\A(?:tesco){e<=2:[cs]}\Z", "tecso").Should().BeTrue();
        Accepts(@"(?r)\A(?:ab){i<=1,d<=1:[ab]}\Z", "ba").Should().BeTrue();
    }

    [Test]
    public void A_reversed_case_insensitive_set_test_folds_and_tests()
    {
        // SET_UNION_IGN_REV, which had no arm either.
        Accepts("(?ir)(?:abc){s<=1:[xy]}", "aXc").Should().BeTrue();
        Accepts("(?ir)(?:abc){s<=1:[xy]}", "aDc").Should().BeFalse();
    }

    [Test]
    [Arguments("[[a-z]--[aeiou]]", "adc", true)]
    [Arguments("[[a-z]--[aeiou]]", "aec", false)]
    [Arguments("[[a-z]&&[d-f]]", "adc", true)]
    [Arguments("[[a-z]&&[e-f]]", "adc", false)]
    [Arguments("[[a-e]~~[d-z]]", "adc", false)]
    [Arguments("[[a-e]~~[e-z]]", "adc", true)]
    public void A_reversed_set_operation_test_tests(string test, string text, bool expected)
    {
        // SET_DIFF_REV, SET_INTER_REV and SET_SYM_DIFF_REV need the V1 set syntax.
        Accepts($"(?V1r)(?:abc){{s<=1:{test}}}", text).Should().Be(expected);
        Accepts($"(?V1)(?:abc){{s<=1:{test}}}", text).Should().Be(expected);
    }

#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
    [Test]
    public void Reversed_fullmatch_accepts_exactly_what_forward_accepts()
    {
        // A seeded grid. Each row is a word under a constraint and a test, and a text made from the
        // word by up to two random edits. Fullmatch over the whole text has no direction, so the
        // reversed pattern must accept exactly the texts the forward one does.
        string[] words = ["abc", "ab", "tesco", "a1c"];
        string[] constraints = ["s<=1", "i<=1", "d<=1", "e<=1", "e<=2", "i<=1,d<=1", "s<=1,i<=1"];
        string[] tests =
        [
            "[xy]",
            "[xyz]",
            "[pq]",
            "[x]",
            "[x-z]",
            "[0-9]",
            "[0-9xy]",
            "[^xy]",
            "[^ab]",
            "[bcx]",
            "[[a-z]--[aeiou]]",
            "[[a-z]&&[c-y]]",
            "[[a-e]~~[c-z]]",
            ".",
        ];
        string[] singlePiece = ["[x]", "[x-z]", "[0-9]", "."];
        string[] flags = ["", "i", "s"];
        const string Alphabet = "abcdexyzpqXY01\n";
        var random = new Random(20261116);
        var mismatches = new List<string>();
        int testBit = 0;
        int reversedSetRows = 0;

        foreach (string word in words)
        {
            foreach (string constraint in constraints)
            {
                foreach (string test in tests)
                {
                    foreach (string flag in flags)
                    {
                        for (int n = 0; n < 6; n++)
                        {
                            string text = Mutate(word, random, Alphabet);
                            string body = $"(?:{word}){{{constraint}:{test}}}";
                            bool forward = Accepts($"(?V1{flag}){body}", text);
                            bool reversed = Accepts($"(?V1{flag}r){body}", text);
                            bool untested = Accepts($"(?V1{flag})(?:{word}){{{constraint}}}", text);

                            if (forward != reversed)
                            {
                                mismatches.Add($"{flag} {body} '{text}': forward {forward}, reversed {reversed}");
                            }

                            // Rule 11: the rows where the test changed the forward answer are the
                            // ones that could have gone red; a set of two or more pieces is the
                            // shape D155 was about.
                            if (untested && !forward)
                            {
                                testBit++;

                                if (!singlePiece.Contains(test, StringComparer.Ordinal))
                                {
                                    reversedSetRows++;
                                }
                            }
                        }
                    }
                }
            }
        }

        mismatches.Should().BeEmpty();
        testBit.Should().BeGreaterThan(500);
        reversedSetRows.Should().BeGreaterThan(300);
    }

    private static bool Accepts(string pattern, string text) => new FuzzyRegex(pattern).FullMatch(text).Success;

    private static string Mutate(string word, Random random, string alphabet)
    {
        var text = new StringBuilder(word);
        int edits = random.Next(3);

        for (int i = 0; i < edits; i++)
        {
            char c = alphabet[random.Next(alphabet.Length)];
            int kind = random.Next(3);

            if (kind == 0 && text.Length > 0)
            {
                text[random.Next(text.Length)] = c;
            }
            else if (kind == 1)
            {
                text.Insert(random.Next(text.Length + 1), c);
            }
            else if (text.Length > 0)
            {
                text.Remove(random.Next(text.Length), 1);
            }
        }

        return text.ToString();
    }
#pragma warning restore CA5394, S2245
}
