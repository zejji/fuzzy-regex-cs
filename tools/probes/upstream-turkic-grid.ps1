# A second engine's answer to ledger entry 7's 5x5 dotted-I grid: does .NET's
# RegexOptions.IgnoreCase | RegexOptions.CultureInvariant take the Turkic I/dotless-i pairing
# upstream `regex` takes by default, or reach U+0130's full fold to "i" + U+0307?
#
# .NET's CultureInvariant match is the same plain-default question CaseFolding.txt's header
# answers under "Usage: A/B" (see upstream-turkic-definition.py for that text, and for the
# PCRE2/CPython answers to the same grid). This is the fourth engine put on record.
#
# Run it:
#
#     pwsh -File tools/probes/upstream-turkic-grid.ps1
#
# Real run, 2026-09-14, .NET 10.0.10:
#
#     .NET 10.0.10
#       pat U+0130 I-dot       subj U+0130 I-dot       -> Y
#       pat U+0130 I-dot       subj i + U+0307         -> n
#       pat U+0130 I-dot       subj i                  -> n
#       pat U+0130 I-dot       subj I                  -> n
#       pat U+0130 I-dot       subj U+0131 dotless i   -> n
#       pat i + U+0307         subj U+0130 I-dot       -> n
#       pat i + U+0307         subj i + U+0307         -> Y
#       pat i + U+0307         subj i                  -> n
#       pat i + U+0307         subj I                  -> n
#       pat i + U+0307         subj U+0131 dotless i   -> n
#       pat i                  subj U+0130 I-dot       -> n
#       pat i                  subj i + U+0307         -> n
#       pat i                  subj i                  -> Y
#       pat i                  subj I                  -> Y
#       pat i                  subj U+0131 dotless i   -> n
#       pat I                  subj U+0130 I-dot       -> n
#       pat I                  subj i + U+0307         -> n
#       pat I                  subj i                  -> Y
#       pat I                  subj I                  -> Y
#       pat I                  subj U+0131 dotless i   -> n
#       pat U+0131 dotless i   subj U+0130 I-dot       -> n
#       pat U+0131 dotless i   subj i + U+0307         -> n
#       pat U+0131 dotless i   subj i                  -> n
#       pat U+0131 dotless i   subj I                  -> n
#       pat U+0131 dotless i   subj U+0131 dotless i   -> Y
#     string.ToLowerInvariant / ToUpperInvariant of U+0130: 0x130 0x130
#
# .NET's grid is exactly PCRE2's (see upstream-turkic-definition.py): the default-simple fold
# only - `U+0130` reaches neither the full fold (`i + U+0307`, n) nor `I` (n), and `I`/`U+0131`
# do not pair either way. No Turkic default, no full fold - CultureInvariant is genuinely
# locale-free here. ToLowerInvariant/ToUpperInvariant leave U+0130 fixed, so .NET's simple case
# mapping does not even give it the plain lower-case pairing PCRE2's CASELESS grid does.
$names = @('U+0130 I-dot', 'i + U+0307', 'i', 'I', 'U+0131 dotless i')
$vals = @([string][char]0x0130, ('i' + [char]0x0307), 'i', 'I', [string][char]0x0131)
".NET $([System.Environment]::Version)"
foreach ($p in 0..4) {
    foreach ($s in 0..4) {
        $pat = '^' + [regex]::Escape($vals[$p]) + '$'
        $m = [regex]::IsMatch($vals[$s], $pat, 'IgnoreCase, CultureInvariant')
        $flag = if ($m) { 'Y' } else { 'n' }
        '  pat {0,-18} subj {1,-18} -> {2}' -f $names[$p], $names[$s], $flag
    }
}
'string.ToLowerInvariant / ToUpperInvariant of U+0130: 0x{0:x} 0x{1:x}' -f [int][char]([string][char]0x0130).ToLowerInvariant()[0], [int][char]([string][char]0x0130).ToUpperInvariant()[0]
