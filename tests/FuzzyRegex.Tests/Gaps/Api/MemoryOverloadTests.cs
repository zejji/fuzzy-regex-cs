using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S61. The <see cref="ReadOnlyMemory{T}"/> overloads of <c>IsMatch</c> and <c>Count</c> read the
/// caller's buffer in place. Each expectation is the <see cref="string"/> overload's answer for the
/// same characters, which the ported suite and the oracle already hold to upstream's; what is new
/// here is only where the characters live.
/// </summary>
public sealed class MemoryOverloadTests
{
    [Test]
    // A surrogate pair inside the slice: three codepoints, so three matches of '.'.
    [Arguments(".", "##x\U0001F600y##", 2, 4, FuzzyRegexOptions.None)]
    // The slice is the whole subject, so a word boundary sees its start and end, and a
    // lookbehind cannot see the buffer's 'n' before it.
    [Arguments(@"\bcat\b", "concatenate", 3, 3, FuzzyRegexOptions.None)]
    [Arguments("(?<=n)cat", "concatenate", 3, 3, FuzzyRegexOptions.None)]
    [Arguments(
        "(?:amber lantern works){e<=2}",
        "xx the Amber Lantern Wroks are shut xx",
        3,
        32,
        FuzzyRegexOptions.IgnoreCase
    )]
    public void A_slice_of_a_buffer_answers_what_the_same_characters_as_a_string_do(
        string pattern,
        string buffer,
        int start,
        int length,
        FuzzyRegexOptions options
    )
    {
        FuzzyRegex regex = new(pattern, options);
        ReadOnlyMemory<char> slice = buffer.ToCharArray().AsMemory(start, length);
        string same = buffer.Substring(start, length);

        regex.IsMatch(slice).Should().Be(regex.IsMatch(same));
        regex.Count(slice).Should().Be(regex.Count(same));
    }

    [Test]
    public void A_char_array_argument_still_compiles()
    {
        // An array converts to both ReadOnlySpan and ReadOnlyMemory. Adding the memory overloads
        // must not make a call that compiled before ambiguous; this file compiling is the test.
        FuzzyRegex regex = new("a+");
        char[] subject = ['b', 'a', 'a', 'b', 'a'];

        regex.IsMatch(subject).Should().BeTrue();
        regex.Count(subject).Should().Be(2);
    }

    [Test]
    public void A_null_argument_still_binds_to_the_string_overload()
    {
        // null converts to string and, through char[], to ReadOnlyMemory, so the memory overloads
        // made these two calls ambiguous (CS0121; S61 blind review). Before them, null picked the
        // string overload and threw, as System.Text.RegularExpressions.Regex.IsMatch(null) does.
        FuzzyRegex regex = new("a+");

        Action isMatch = () => regex.IsMatch(null!);
        Action count = () => regex.Count(null!);

        isMatch.Should().Throw<ArgumentNullException>().WithParameterName("input");
        count.Should().Throw<ArgumentNullException>().WithParameterName("input");
    }
}
