namespace FuzzyRegex.UnicodeGenerator;

/// <summary>
/// Generates the Unicode table sources under <c>src/FuzzyRegex/Unicode/</c> from UCD data files,
/// as a port of <c>upstream/tools/build_regex_unicode.py</c> (design spec section 6).
/// </summary>
internal static class Program
{
    private static int Main()
    {
        Console.Error.WriteLine(
            "FuzzyRegex.UnicodeGenerator is not implemented yet; it is built in the Unicode slice.\n"
                + "See docs/plan/ROADMAP.md and upstream/tools/build_regex_unicode.py."
        );

        return 2;
    }
}
