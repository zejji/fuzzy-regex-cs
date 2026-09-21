#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S75 (2026-09-20): the port's half of the owner's `(foobar){e}` case. The upstream half, and what
// the case is about, is in tools/probes/s75-stacked-deletions.py.
//
//     pattern  (foobar){e}
//     subject  xirefoabralfobarxie
//
// Two columns, because the demo does not draw the library's own deletion positions. A deletion is
// reported where the missing character would sit in a string that had every deletion put back, so
// the i-th deletion of a match is i past the place in the subject it happened at; DemoEngine.Edits
// subtracts that i so the page has a position in the string it slices (DemoEngine.cs, the
// `changes.Deletions[i] - i`). "library" below is what upstream is compared against, "demo" is what
// reaches the highlight, and the stacking the slice fixes is only visible in the second.
//
//     dotnet run tools/probes/s75-stacked-deletions.cs

using System.Globalization;
using Fuzzy.Text.RegularExpressions;

const string pattern = "(foobar){e}";
const string subject = "xirefoabralfobarxie";

Console.WriteLine($"pattern {pattern}  subject {subject}");
Console.WriteLine();

// PadRight/PadLeft rather than interpolation alignment specifiers: CSharpier writes those with a
// space after the comma and IDE0055 rejects the space, so a line using one satisfies neither of
// this repo's formatting gates (the same note is on tools/probes/aot-smoke-slow-patterns.cs).
Console.WriteLine("  #       span  text         s  i  d  library deletions -> demo deletions");

int number = 0;
foreach (Match match in FuzzyRegex.EnumerateMatches(subject, pattern))
{
    number++;
    FuzzyCounts counts = match.FuzzyCounts;
    FuzzyChanges changes = match.FuzzyChanges;

    int[] library = [.. changes.Deletions];
    int[] demo = [.. library.Select(static (at, i) => at - i)];

    string span = $"({match.Index},{match.Index + match.Length})";
    string counted =
        counts.Substitutions.ToString(CultureInfo.InvariantCulture).PadLeft(2)
        + " "
        + counts.Insertions.ToString(CultureInfo.InvariantCulture).PadLeft(2)
        + " "
        + counts.Deletions.ToString(CultureInfo.InvariantCulture).PadLeft(2);
    Console.WriteLine(
        number.ToString(CultureInfo.InvariantCulture).PadLeft(3)
            + "  "
            + span.PadLeft(9)
            + "  "
            + $"'{match.Value}'".PadRight(10)
            + "  "
            + counted
            + $"  [{string.Join(", ", library)}] -> [{string.Join(", ", demo)}]"
    );
}
