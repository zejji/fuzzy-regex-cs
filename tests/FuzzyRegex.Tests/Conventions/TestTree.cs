namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>Where this test project's own source lives, for the conventions that scan it.</summary>
internal static class TestTree
{
    /// <summary>
    /// The repository root, found from the test assembly's own location rather than from the
    /// working directory, which a test runner does not promise. It holds under the Native AOT
    /// publish too: the published executable sits under <c>tests/FuzzyRegex.Tests/bin/</c>.
    /// </summary>
    /// <returns>The root directory.</returns>
    internal static DirectoryInfo RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FuzzyRegex.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("no FuzzyRegex.slnx above " + AppContext.BaseDirectory);
    }

    /// <summary>Every C# source file in this test project.</summary>
    /// <returns>The files.</returns>
    internal static IEnumerable<FileInfo> Sources()
    {
        DirectoryInfo project = new(Path.Combine(RepositoryRoot().FullName, "tests", "FuzzyRegex.Tests"));

        return project
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            // bin/ and obj/ hold generated copies and the source generator's output, neither of
            // which is source anyone here writes.
            .Where(static file =>
                !file.FullName.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
                && !file.FullName.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            );
    }
}
