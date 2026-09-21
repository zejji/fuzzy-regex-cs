#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S57b: this port's half of the two `IndexError` rows, seed 20260920 rows 81232 and 87091 of the
// 6000-row gate.
//
//     dotnet run tools/probes/s57b-port-firstset-indexerror.cs
//
// The upstream half is tools/probes/s57b-upstream-firstset-indexerror.py, which shows upstream
// raising `IndexError: tuple index out of range` while COMPILING `(?r)^\u0130\ufb00` under
// IGNORECASE|FULLCASE, and traces it to an empty `String` node that `Sequence._fix_full_casefold`
// leaves behind when it slices the unfolded characters with offsets it found in the folded text.
//
// Read the two together. This half shows the same patterns compiling here, and shows the second,
// silent symptom of the same mis-sliced chunk: upstream's FORWARD `^\u0130\ufb00` compiles and then
// fails to match its own full fold, because the chunk that should have carried FULLIGNORECASE was
// given simple folding instead. This port matches it.
//
// Measured 2026-09-21.
using Fuzzy.Text.RegularExpressions;

const FuzzyRegexOptions IC = FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase;

static void Show(string label, string pattern, FuzzyRegexOptions options, string subject)
{
    string answer;
    try
    {
        Match m = new FuzzyRegex(pattern, options).Match(subject);
        answer = m.Success ? $"matches ({m.Index}, {m.Index + m.Length})" : "NO MATCH";
    }
    catch (Exception e)
    {
        answer = $"threw {e.GetType().Name}: {e}";
    }

    Console.WriteLine($"  {label, -30} over {Escape(subject), -18} {answer}");
}

static string Escape(string s) =>
    string.Concat(s.Select(static c => c is >= ' ' and < (char)0x7f ? c.ToString() : $"\\u{(int)c:x4}"));

Console.WriteLine("THE PATTERNS UPSTREAM CANNOT COMPILE");
Show("(?r)^\\u0130\\ufb00", "(?r)^\u0130\ufb00", IC, "i\u0307ff");
Show("(?r)\\A\\u0130\\ufb00", "(?r)\\A\u0130\ufb00", IC, "i\u0307ff");

// The two rows verbatim out of `tools/probes/s57b-gate-rows.jsonl`, each under its own operation:
// row 81232 was drawn as a split, row 87091 as a fullmatch.
Console.WriteLine();
Console.WriteLine("THE TWO GATE ROWS, as drawn");
FuzzyRegex row81232 = new("(?r)^\u00df(?<=\u0130\u0130\ufb00)(?:(?<=\\p{Nd})\u0130)?$", (FuzzyRegexOptions)0x4002);
Console.WriteLine(
    "  seed 20260920 row 81232        split    "
        + string.Join(
            " | ",
            row81232.Split("\u00df\rS\ufb01\u0130\ufb00\u0130").Select(static p => p is null ? "None" : Escape(p))
        )
);

FuzzyRegex row87091 = new(
    "(?r)^(?:(?(?<![\ufb01])\ufb01[^a]|(?(?<!(?:\ufb01|\\p{Ll})+)\\p{Nd}*?|))\u0130|\u0130)\ufb01$",
    (FuzzyRegexOptions)0x400A
);
Match full = row87091.FullMatch("\ufb01\ufb01\u0130");
Console.WriteLine(
    "  seed 20260920 row 87091        fullmatch "
        + (full.Success ? $"matches ({full.Index}, {full.Index + full.Length})" : "NO MATCH")
);

Console.WriteLine();
Console.WriteLine("THE SILENT HALF, forward, where upstream compiles and answers wrongly");
Show("^\\u0130\\ufb00", "^\u0130\ufb00", IC, "i\u0307ff");
Show("^\\ufb00", "^\ufb00", IC, "ff");
Show("^a\\ufb00", "^a\ufb00", IC, "aff");
Show("^\\ufb00\\ufb00", "^\ufb00\ufb00", IC, "ffff");
