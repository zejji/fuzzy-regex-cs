using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Conventions;

/// <summary>
/// S65. <c>docs/DIVERGENCES.md</c> is the authoritative, dated record of every deliberate
/// difference from upstream; <c>docs/COMPARISON.md</c> is the user-facing page Phase 8 writes from
/// it. This pins the join: every row whose Status column says <c>SHIPPED</c> must be named in
/// COMPARISON, by the row's own bolded heading text appearing in one of COMPARISON's own
/// <c>##</c>/<c>###</c> heading lines (not merely somewhere in the file - see
/// <see cref="ComparisonHeadingLines"/>), so the two documents cannot quietly drift apart - a row
/// added to one without the other, or a section deleted while a cross-reference to it survives, is
/// exactly the failure mode this guards against.
/// </summary>
public sealed class ComparisonCoversDivergencesTests
{
    [Test]
    public void Every_SHIPPED_divergence_is_named_in_the_comparison_page()
    {
        string divergences = File.ReadAllText(DocPath("DIVERGENCES.md"));
        string comparison = File.ReadAllText(DocPath("COMPARISON.md"));

        List<string> headings = ShippedRowHeadings(divergences);
        headings
            .Should()
            .HaveCountGreaterThan(
                33,
                "the row scan must actually find DIVERGENCES.md's SHIPPED rows (measured 34, 2026-09-18) - "
                    + "a broken parse finds fewer, or nothing, and passes vacuously"
            );

        List<string> headingLines = ComparisonHeadingLines(comparison);
        headingLines
            .Should()
            .HaveCountGreaterThan(
                45,
                "the heading scan must actually find COMPARISON.md's own ## / ### lines (measured 46, 2026-09-18)"
            );

        List<string> missing =
        [
            .. headings.Where(heading =>
                !headingLines.Exists(line => line.Contains(heading, StringComparison.Ordinal))
            ),
        ];

        missing.Should().BeEmpty();
    }

    /// <summary>
    /// Every <c>##</c>/<c>###</c> heading line in <paramref name="markdown"/>. Matching against
    /// heading lines only, rather than the whole document, matters because COMPARISON.md's own
    /// cross-reference tables quote a heading's text inside a body cell ("See
    /// <c>**There is no `findall`.**</c> below.") - checking the raw file text lets that
    /// cross-reference stand in for the section it is pointing at, so deleting the section itself
    /// (heading and body) while leaving the cross-reference behind still passes (found by a
    /// negative control that removed the "There is no `findall`" heading only, 2026-09-18).
    /// </summary>
    /// <param name="markdown">The raw contents of <c>docs/COMPARISON.md</c>.</param>
    /// <returns>Each heading line, trimmed, markdown decoration intact.</returns>
    // SHORTCUT: a line inside a fenced ``` code block that happens to start with "## " or "### "
    // would be read as a heading too; COMPARISON.md has no such line today (every fence's content
    // is C# or Python, not markdown), so this is a ceiling on today's file rather than a live bug.
    // Upgrade path: track fence state the same way SplitRowRespectingCodeSpans tracks backtick
    // state, and skip lines while inside one. Found by the second blind review pass, 2026-09-18.
    private static List<string> ComparisonHeadingLines(string markdown) =>
        [
            .. markdown
                .Split('\n')
                .Select(static line => line.TrimEnd('\r').Trim())
                .Where(static line =>
                    line.StartsWith("## ", StringComparison.Ordinal)
                    || line.StartsWith("### ", StringComparison.Ordinal)
                ),
        ];

    /// <summary>
    /// Every table row in <paramref name="markdown"/> whose Status cell (the fourth <c>|</c>-cell)
    /// starts with <c>SHIPPED</c> (allowing surrounding <c>**bold**</c>), reduced to the bolded
    /// heading text at the start of its first cell - the same text S65 quotes as each
    /// <c>docs/COMPARISON.md</c> section heading.
    /// </summary>
    /// <param name="markdown">The raw contents of <c>docs/DIVERGENCES.md</c>.</param>
    /// <returns>One heading per SHIPPED row.</returns>
    private static List<string> ShippedRowHeadings(string markdown)
    {
        List<string> headings = [];
        foreach (string line in markdown.Split('\n'))
        {
            if (!line.TrimStart().StartsWith("| **", StringComparison.Ordinal))
            {
                continue;
            }

            List<string> cells = SplitRowRespectingCodeSpans(line);
            if (cells.Count < 5)
            {
                continue;
            }

            string status = cells[4].Trim().Trim('*');
            if (!status.StartsWith("SHIPPED", StringComparison.Ordinal))
            {
                continue;
            }

            System.Text.RegularExpressions.Match bold = System.Text.RegularExpressions.Regex.Match(
                cells[1],
                @"\*\*(?<heading>.+?)\*\*",
                System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
                TimeSpan.FromSeconds(1)
            );
            if (!bold.Success)
            {
                continue;
            }

            headings.Add(bold.Groups["heading"].Value.TrimEnd('.', ',', ':', ' '));
        }

        return headings;
    }

    private static string DocPath(string fileName) =>
        Path.Combine(TestTree.RepositoryRoot().FullName, "docs", fileName);

    /// <summary>
    /// Splits a markdown table row on <c>|</c>, except inside a backtick code span - a raw
    /// <c>|</c> inside inline code (this table's rows quote fuzzy patterns, which use <c>|</c> for
    /// alternation, e.g. `` `I\|F` ``) is not a column delimiter. A plain <c>line.Split('|')</c>
    /// desynchronises every cell after it, which silently drops the row from
    /// <see cref="ShippedRowHeadings"/> instead of failing loudly (found by a negative control on
    /// the DIVERGENCES.md row for "Inherited upstream bugs are fixed here", 2026-09-18).
    /// </summary>
    /// <param name="line">One line of the markdown table.</param>
    /// <returns>The cells, in order, backtick spans intact.</returns>
    private static List<string> SplitRowRespectingCodeSpans(string line)
    {
        List<string> cells = [];
        System.Text.StringBuilder current = new();
        bool insideCode = false;
        foreach (char c in line)
        {
            if (c == '`')
            {
                insideCode = !insideCode;
                current.Append(c);
            }
            else if (c == '|' && !insideCode)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
