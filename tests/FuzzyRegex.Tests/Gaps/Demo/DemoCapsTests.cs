using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using FuzzyRegexDemo.Wasm;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Demo;

/// <summary>
/// The page's caps and the engine's caps, checked against each other (S71).
/// </summary>
/// <remarks>
/// <para>
/// The demo enforces its bounds twice, in two languages, for two different reasons: the engine's
/// (<see cref="DemoEngine"/>) hold even when somebody drives the worker from the browser console,
/// and the page's (<c>wwwroot/lib/caps.js</c>) protect the main thread, which is the one freeze a
/// Web Worker does nothing about. Two copies of a number in two languages is exactly the arrangement
/// that drifts, and a drift here is silent: raise the engine's subject cap and the page goes on
/// refusing at the old number with a message quoting the new one.
/// </para>
/// <para>
/// So the JavaScript is read as text and its constants parsed. That is cruder than importing them,
/// and it is the only option that does not put a JavaScript runtime in the test suite - the suite
/// also runs published as Native AOT - or duplicate the numbers a third time in a shared JSON file
/// that nothing would check either.
/// </para>
/// </remarks>
public sealed class DemoCapsTests
{
    /// <summary>
    /// <c>caps.js</c>, found from the test assembly rather than from the working directory.
    /// </summary>
    /// <remarks>
    /// Source-relative: <see cref="CallerFilePathAttribute"/> is how a test finds a file that is
    /// deliberately not embedded. Embedding it would make this test read a copy of the file the
    /// browser loads, and the whole claim is about the file the browser loads.
    /// </remarks>
    private static string CapsSource([CallerFilePath] string thisFile = "")
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "../../../.."));
        string caps = Path.Combine(repoRoot, "demo/FuzzyRegex.Demo.Wasm/wwwroot/lib/caps.js");

        File.Exists(caps).Should().BeTrue("the page's caps live in {0}", caps);
        return File.ReadAllText(caps);
    }

    private static int Cap(string name)
    {
        string source = CapsSource();
        // Fully qualified: this namespace has a Match of its own, which is the type the port ships.
        System.Text.RegularExpressions.Match declaration = Regex.Match(
            source,
            $@"export const {Regex.Escape(name)} = (?<value>\d+);",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)
        );

        declaration.Success.Should().BeTrue("caps.js must declare {0} as a plain number", name);
        return int.Parse(declaration.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    [Test]
    public void The_pages_subject_cap_is_the_engines_subject_cap()
    {
        // Equal, not merely lower. The page's message names its own number and the engine's refusal
        // names the engine's; a visitor who hit two different limits with two different sentences
        // would be reading a demo that disagrees with itself.
        Cap("MAX_SUBJECT_LENGTH").Should().Be(DemoEngine.MaxSubjectLength);
    }

    [Test]
    public void The_pages_display_cap_is_below_the_engines_match_cap()
    {
        // Strictly below, or the "showing the first N of M" line can never appear and the page has a
        // freeze guard that has never once fired.
        Cap("MAX_DISPLAYED_MATCHES").Should().BeLessThan(DemoEngine.MaxMatches);
    }

    [Test]
    public void The_fragment_cap_could_not_hold_a_capped_subject()
    {
        // Not a parity check - nothing in the engine knows about URLs - but a check that the page's
        // "too long to share" branch is reachable. If the fragment cap were above the subject cap it
        // would be dead code, and dead code that claims to protect a link is worse than none.
        Cap("MAX_FRAGMENT_LENGTH").Should().BeLessThan(DemoEngine.MaxSubjectLength);
    }
}
