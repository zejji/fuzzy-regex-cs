using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// S65. Every public type and member in <c>src/FuzzyRegex</c> carries an XML doc comment, which the
/// NuGet package and <c>docs/COMPARISON.md</c> both assume. <c>CS1591</c> already enforces "some doc
/// comment" at compile time - <c>src/FuzzyRegex.csproj</c> is the one project where the repo-wide
/// <c>NoWarn</c> for it (<c>Directory.Build.props</c>) does not apply, because its own
/// <c>&lt;NoWarn&gt;RS0026&lt;/NoWarn&gt;</c> replaces rather than appends to the inherited value.
/// This test is the independent half: it reflects over the actual shipped assembly for the true
/// public surface (not the project's own <c>PublicAPI.*.txt</c>, which would just be checking the
/// tracked list against itself) and reads the generated <c>.xml</c> doc file to prove every member
/// it finds has a real entry with a <c>&lt;summary&gt;</c> or an <c>&lt;inheritdoc/&gt;</c> - the one
/// thing CS1591 does not check, since any doc comment at all silences it. It is a real scan, not a
/// vacuous one (S52b rule): the type list, the member groups and the loaded doc file each carry a
/// measured floor below which the test fails on its own setup rather than "passing" by finding
/// nothing.
/// </summary>
public sealed class PublicApiDocumentationTests
{
    [Test]
    public void Every_public_type_and_member_has_a_documented_XML_entry()
    {
        string xmlPath = Path.Combine(AppContext.BaseDirectory, "FuzzyRegex.xml");
        File.Exists(xmlPath)
            .Should()
            .BeTrue("the referenced project's generated doc file must be copied next to the test binary");

        List<(string Name, bool Documented)> entries = LoadDocEntries(xmlPath);
        entries
            .Should()
            .HaveCountGreaterThan(
                1000,
                "the doc file must actually have loaded - a broken path finds nothing and the rule below passes vacuously"
            );

        Type[] types = typeof(FuzzyRegex).Assembly.GetExportedTypes();
        types
            .Should()
            .HaveCountGreaterThan(10, "the exported-type scan must actually find this library's public surface");

        List<string> violations = [];
        foreach (Type type in types)
        {
            string typeName = type.FullName!.Replace('+', '.');
            CheckGroup(entries, "T:" + typeName, expectedCount: 1, violations, describe: typeName);

            if (type.IsSubclassOf(typeof(Delegate)))
            {
                // A delegate type's invoke method and its other runtime-synthesized members get no
                // XML doc member entry of their own; GenerateDocumentationFile treats the delegate
                // TYPE's own summary as the whole doc surface (measured 2026-09-18: MatchEvaluator
                // has no method-level entry at all, only its type-level one).
                continue;
            }

            foreach (
                IGrouping<string, MemberInfo> group in type.GetMembers(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                    )
                    .Where(m => !IsAccessor(m) && !IsCompilerSupplied(m))
                    .GroupBy(m => Prefix(m) + typeName + "." + MemberKeyName(m))
            )
            {
                CheckGroup(entries, group.Key, group.Count(), violations, describe: group.Key);
            }
        }

        violations.Should().BeEmpty();
    }

    /// <summary>Every <c>&lt;member&gt;</c> entry, and whether it carries a real summary.</summary>
    /// <param name="xmlPath">The generated doc file to load.</param>
    /// <returns>Each member's doc-comment-ID and whether it is documented.</returns>
    private static List<(string Name, bool Documented)> LoadDocEntries(string xmlPath) =>
        [
            .. XDocument
                .Load(xmlPath)
                .Root!.Element("members")!
                .Elements("member")
                .Select(m =>
                    (
                        Name: (string)m.Attribute("name")!,
                        Documented: (m.Element("summary") is { } summary && !string.IsNullOrWhiteSpace(summary.Value))
                            || m.Elements()
                                .Any(e => string.Equals(e.Name.LocalName, "inheritdoc", StringComparison.Ordinal))
                    )
                ),
        ];

    /// <summary>A property/event accessor method, already covered by its own property/event.</summary>
    /// <param name="member">The reflected member.</param>
    /// <returns>Whether it is an accessor rather than something needing its own doc entry.</returns>
    private static bool IsAccessor(MemberInfo member) =>
        member is MethodInfo method
        && method.IsSpecialName
        && (
            method.Name.StartsWith("get_", StringComparison.Ordinal)
            || method.Name.StartsWith("set_", StringComparison.Ordinal)
            || method.Name.StartsWith("add_", StringComparison.Ordinal)
            || method.Name.StartsWith("remove_", StringComparison.Ordinal)
        );

    /// <summary>
    /// A member the runtime supplies rather than a declaration a person wrote - the enum's hidden
    /// <c>value__</c> field, and any member carrying <see cref="CompilerGeneratedAttribute"/>.
    /// </summary>
    /// <param name="member">The reflected member.</param>
    /// <returns>Whether it needs no doc comment of its own.</returns>
    private static bool IsCompilerSupplied(MemberInfo member) =>
        (member is FieldInfo field && field.IsSpecialName)
        || member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;

    /// <summary>The doc-comment-ID prefix letter for a member's kind.</summary>
    /// <param name="member">The reflected member.</param>
    /// <returns><c>"P:"</c>, <c>"F:"</c> or <c>"M:"</c>.</returns>
    // SHORTCUT: a public event or a public nested type reached through this switch falls into
    // "M:" instead of the doc-comment-ID's own "E:"/"T:", and a generic method's key never gets
    // its "``n" arity suffix in CheckGroup below - either would spuriously report a documented
    // member as a violation. Neither shape exists in FuzzyRegex's public surface today (checked
    // 2026-09-18: no public event, nested type, or generic method), so this is a ceiling on
    // today's surface, not a live bug. Upgrade path: add EventInfo => "E:" and MemberInfo whose
    // MemberType is NestedType => "T:" here (nested types are otherwise double-counted, since
    // GetExportedTypes already lists them - exclude them from the member loop instead), and make
    // CheckGroup's match tolerate a `` `n`` arity suffix before the "(". Found by the first blind
    // review pass, 2026-09-18.
    private static string Prefix(MemberInfo member) =>
        member switch
        {
            PropertyInfo => "P:",
            FieldInfo => "F:",
            _ => "M:",
        };

    /// <summary>The member name as it appears in a doc-comment-ID, constructors spelled <c>#ctor</c>.</summary>
    /// <param name="member">The reflected member.</param>
    /// <returns>The name.</returns>
    private static string MemberKeyName(MemberInfo member) => member is ConstructorInfo ? "#ctor" : member.Name;

    /// <summary>
    /// Checks that at least <paramref name="expectedCount"/> loaded doc entries start with
    /// <paramref name="keyPrefix"/> (a parameter list, present on overloads, is everything after
    /// that point, so matching the prefix finds every overload without re-deriving the exact
    /// parameter-type encoding a doc-comment-ID uses) and that every one of them is documented.
    /// </summary>
    private static void CheckGroup(
        List<(string Name, bool Documented)> entries,
        string keyPrefix,
        int expectedCount,
        List<string> violations,
        string describe
    )
    {
        List<(string Name, bool Documented)> matches =
        [
            .. entries.Where(e =>
                string.Equals(e.Name, keyPrefix, StringComparison.Ordinal)
                || e.Name.StartsWith(keyPrefix + "(", StringComparison.Ordinal)
            ),
        ];

        if (matches.Count < expectedCount)
        {
            violations.Add(
                $"{describe}: expected at least {expectedCount} documented XML entr{(expectedCount == 1 ? "y" : "ies")} "
                    + $"starting with '{keyPrefix}', found {matches.Count}"
            );
        }
        else if (matches.Exists(m => !m.Documented))
        {
            violations.Add($"{describe}: has a doc entry with no <summary> and no <inheritdoc/> ('{keyPrefix}')");
        }
    }
}
