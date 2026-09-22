using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// S80. <c>src/FuzzyRegex/PublicAPI.Unshipped.txt</c> is the tracked list of every public member
/// the library ships (nothing has reached 1.0 yet, so <c>PublicAPI.Shipped.txt</c> is a single
/// line and carries none of them); <c>RegexFlags.InlineFlags</c> is every letter <c>(?x)</c> syntax
/// accepts. Neither list is read by a person, so this is the gate that keeps the user
/// documentation - <c>README.md</c> and <c>docs/GUIDE.md</c> - describing the whole surface rather
/// than whatever happened to be described when it was last read by eye (found by the owner,
/// 2026-09-21: four flags and four public members named in neither file). It matches names, not
/// signatures, so an overload or an accessor pair costs the prose nothing extra.
/// </summary>
public sealed class UserDocumentationCompletenessTests
{
    /// <summary>
    /// Compiler and interface boilerplate that nothing in "Choosing an entry point" or "Reading a
    /// result" would ever name, because none of it is a decision a caller makes: a constructor is
    /// already reached through <c>new FuzzyRegex(...)</c> in prose under its type's own name, value
    /// equality and <c>GetHashCode</c>/<c>ToString</c> overrides exist so the type behaves like any
    /// other .NET type, <c>GetEnumerator</c> and an indexer exist only so <c>foreach</c> and
    /// <c>[]</c> work, <c>Deconstruct</c> is for pattern-matching a record, and a delegate's own
    /// <c>Invoke</c> is never called by that name. Excluded by member name, not by owning type, so
    /// a real property that happened to be named one of these would still be required.
    /// </summary>
    private static readonly HashSet<string> _excludedMemberNames = new(StringComparer.Ordinal)
    {
        "Equals",
        "GetHashCode",
        "ToString",
        "Deconstruct",
        "GetEnumerator",
        "Invoke",
        "operator ==",
        "operator !=",
        "this[]",
    };

    private static readonly string[] _documentationFiles = ["README.md", "docs/GUIDE.md"];

    [Test]
    public void Every_public_member_and_inline_flag_letter_is_named_in_the_user_documentation()
    {
        string repoRoot = TestTree.RepositoryRoot().FullName;
        string publicApiPath = Path.Combine(repoRoot, "src", "FuzzyRegex", "PublicAPI.Unshipped.txt");
        List<string> apiLines = [.. File.ReadAllLines(publicApiPath).Where(line => line.Length > 0 && line[0] != '#')];
        apiLines
            .Should()
            .HaveCountGreaterThan(
                140,
                "the PublicAPI.Unshipped.txt read must actually find its member lines (measured 146, "
                    + "2026-09-21) - a broken path, or reading PublicAPI.Shipped.txt by mistake, finds "
                    + "fewer, or nothing, and this gate would pass while documenting nothing"
            );

        List<string> memberNames =
        [
            .. apiLines
                .Select(MemberName)
                .Where(name => !_excludedMemberNames.Contains(name))
                .Distinct(StringComparer.Ordinal),
        ];
        memberNames
            .Should()
            .HaveCountGreaterThan(
                65,
                "the name extraction must actually turn lines into names (measured 75, 2026-09-21)"
            );

        string text = string.Join(
            "\n\n",
            _documentationFiles.Select(file => File.ReadAllText(Path.Combine(repoRoot, file)))
        );

        List<string> missingMembers = [.. memberNames.Where(name => !text.Contains(name, StringComparison.Ordinal))];
        missingMembers.Should().BeEmpty();

        List<string> inlineLetters = [.. RegexFlags.InlineFlags.Keys];
        inlineLetters.Should().HaveCount(15, "measured against RegexFlags.InlineFlags, 2026-09-21");

        // The guide's flag table writes each letter as the inline syntax itself, `(?a)`, not the
        // bare dictionary key - a bare single letter like "a" or "e" is a substring of almost any
        // paragraph, so checking for it would pass whether or not the flag was ever mentioned.
        List<string> missingFlags =
        [
            .. inlineLetters.Where(letter => !text.Contains($"(?{letter})", StringComparison.Ordinal)),
        ];
        missingFlags.Should().BeEmpty();
    }

    /// <summary>
    /// The simple member (or type) name a <c>PublicAPI.Unshipped.txt</c> line declares: the last
    /// dotted segment once modifiers (<c>~</c>, <c>override</c>, <c>static</c>, <c>const</c>,
    /// <c>readonly</c>, <c>virtual</c>), the return type after <c>-&gt;</c>, an enum's <c>= value</c>,
    /// a method's parameter list, and a property's <c>.get</c>/<c>.set</c>/<c>.init</c> accessor
    /// suffix are all stripped. An indexer collapses to the literal <c>this[]</c> and an operator
    /// overload to <c>operator ==</c> / <c>operator !=</c>, both handled by
    /// <see cref="_excludedMemberNames"/> rather than read from prose.
    /// </summary>
    /// <param name="line">One non-comment line of <c>PublicAPI.Unshipped.txt</c>.</param>
    /// <returns>The name to look for in the documentation set.</returns>
    private static string MemberName(string line)
    {
        string signature = line;
        while (true)
        {
            string next = StripLeadingModifier(signature);
            if (string.Equals(next, signature, StringComparison.Ordinal))
            {
                break;
            }

            signature = next;
        }

        int arrow = signature.IndexOf(" -> ", StringComparison.Ordinal);
        if (arrow >= 0)
        {
            signature = signature[..arrow];
        }

        int equals = signature.IndexOf(" = ", StringComparison.Ordinal);
        if (equals >= 0)
        {
            signature = signature[..equals];
        }

        int openParen = signature.IndexOf('(');
        string head = openParen >= 0 ? signature[..openParen] : signature;

        if (
            head.EndsWith(".get", StringComparison.Ordinal)
            || head.EndsWith(".set", StringComparison.Ordinal)
            || head.EndsWith(".init", StringComparison.Ordinal)
        )
        {
            head = head[..head.LastIndexOf('.')];
        }

        if (head.Contains('[', StringComparison.Ordinal))
        {
            return "this[]";
        }

        int operatorAt = head.IndexOf("operator ", StringComparison.Ordinal);
        if (operatorAt >= 0)
        {
            return head[operatorAt..];
        }

        int lastDot = head.LastIndexOf('.');
        return lastDot >= 0 ? head[(lastDot + 1)..] : head;
    }

    private static readonly string[] _leadingModifiers =
    [
        "~",
        "override ",
        "static ",
        "const ",
        "readonly ",
        "virtual ",
        "abstract ",
        "sealed ",
    ];

    /// <summary>One leading modifier token removed, or the input unchanged if none matches.</summary>
    /// <param name="signature">The remaining, not yet fully stripped, line.</param>
    /// <returns><paramref name="signature"/> with its first modifier removed, if any.</returns>
    private static string StripLeadingModifier(string signature)
    {
        string? modifier = _leadingModifiers.FirstOrDefault(m => signature.StartsWith(m, StringComparison.Ordinal));
        return modifier is null ? signature : signature[modifier.Length..];
    }
}
