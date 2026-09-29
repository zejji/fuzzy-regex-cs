// .NET's half of tools/probes/ignorecase-property-precheck.py: does a case-insensitive \p{Ll},
// bare and in a set, accept U+2102, U+1D518, 'A' and 'a'? And this port's answer to the oracle row
// 20260927:3732 and the ladder the Python probe prints for upstream, both under Version 0 as the
// oracle and Python's `regex` default ask them.
//
// Run: dotnet run tools/probes/ignorecase-property-precheck.cs
#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
using System.Text.RegularExpressions;
using Fuzzy.Text.RegularExpressions;

(string Name, string Text)[] letters = [("U+2102", "\u2102"), ("U+1D518", "\U0001D518"), ("A", "A"), ("a", "a")];

Console.WriteLine($".NET {Environment.Version}");
foreach ((string name, string text) in letters)
{
    // .NET's \p{Ll} reads one UTF-16 unit, so an astral letter is asked as a surrogate pair
    // and the class can never match it; the BMP U+2102 is the letter that answers the question.
    bool bare = Regex.IsMatch(text, @"^\p{Ll}$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    bool set = Regex.IsMatch(text, @"^[\p{Ll}x]$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    Console.WriteLine($"{name, -8} bare {(bare ? 1 : 0)} set {(set ? 1 : 0)}");
}

Console.WriteLine("== this port");
string subject = "\n\r\U0001D518\U0001F600";
string sub = new FuzzyRegex(@"(?im)(?r)(?P<g1>\p{ASCII}{2})(\p{Ll}+?)??", FuzzyRegexOptions.Version0).Replace(
    subject,
    "\U0001F600\\1]"
);
Console.WriteLine("row sub: " + Escape(sub));
foreach (
    (string pattern, string shape) in new[]
    {
        (@"(?i)\p{Ll}", "{0}"),
        (@"(?i)[\p{Ll}x]", "{0}"),
        (@"(?i)\p{Ll}?a{2}", "{0}aa"),
        (@"(?i)\p{Ll}?aa", "{0}aa"),
        (@"(?ri)a{2}\p{Ll}?", "aa{0}"),
        (@"(?ri)a{2}\p{Ll}", "aa{0}"),
        (@"(?i)\p{Lu}?a{2}", "{0}aa"),
    }
)
{
    var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.Version0);
    IEnumerable<string> cells = letters.Select(l =>
    {
        Fuzzy.Text.RegularExpressions.Match m = regex.Match(string.Format(shape, l.Text));
        return $"{l.Name}:" + (m.Success ? $"({m.Index},{m.Index + m.Length})" : "None");
    });
    Console.WriteLine($"{pattern, -22} {string.Join("  ", cells)}");
}

static string Escape(string s) =>
    string.Concat(s.Select(static c => c is >= ' ' and < (char)127 ? c.ToString() : $"\\u{(int)c:x4}"));
