# A second engine's answer to ledger entry 7's 4x4 dotted-I grid: does /iu (Perl's Unicode
# case-insensitive match, full casefolding) take the Turkic I/dotless-i pairing that upstream
# `regex` takes by default, and does it reach U+0130's full fold to "i" + U+0307?
#
# Perl has no locale-free way to opt into the Turkic mapping, so its /iu is the plain Unicode
# default - the same question CaseFolding.txt's own header answers under "Usage: A/B" (see
# upstream-turkic-definition.py for that text). This is the third engine (after CPython's
# str.casefold() and PCRE2) put on record for the same question.
#
# Run it:
#
#     perl tools/probes/upstream-turkic-grid.pl
#
# Real run, 2026-09-14, perl 5.42.2:
#
#     perl 5.042002 , unicode v5.42.2
#     pattern x subject, full match under /iu (Unicode rules, full casefolding)
#       U+0049 -> Y Y . .
#       U+0069 -> Y Y . .
#       U+0130 -> . . Y .
#       U+0131 -> . . . Y
#     U+0130 vs i+U+0307 : MATCH
#     i+U+0307 vs U+0130 : MATCH
#     fc(U+0130) = 0069 0307
#     fc(U+0049) = 0069
#     fc(U+0131) = 0131
#
# Reading the grid (rows/cols in pattern order U+0049, U+0069, U+0130, U+0131): U+0049 and
# U+0069 match each other (plain I/i case-insensitivity) but neither reaches U+0130 or U+0131 -
# no Turkic pairing. U+0130 matches only itself in the grid, but does reach "i" + U+0307
# separately (both directions, MATCH) - Perl takes the full fold entry 7 says upstream `regex`
# is missing. U+0131 matches only itself: Perl has no Turkic I/dotless-i pairing either.
use strict; use warnings; use utf8;
binmode(STDOUT, ':encoding(UTF-8)');
print "perl $] , unicode ", ($^V), "\n";
my @cps = (0x49, 0x69, 0x130, 0x131);
my @subj = (0x49, 0x69, 0x130, 0x131);
print "pattern x subject, full match under /iu (Unicode rules, full casefolding)\n";
for my $p (@cps) {
    my $pat = chr($p);
    my @row;
    for my $s (@subj) {
        my $str = chr($s);
        push @row, ($str =~ /\A\Q$pat\E\z/iu) ? 'Y' : '.';
    }
    printf("  U+%04X -> %s\n", $p, join(' ', @row));
}
# the expansion case: does U+0130 match "i" + U+0307 ?
my $dotted = chr(0x130);
my $idot = chr(0x69) . chr(0x307);
printf("U+0130 vs i+U+0307 : %s\n", ($idot =~ /\A\Q$dotted\E\z/iu) ? 'MATCH' : 'no');
printf("i+U+0307 vs U+0130 : %s\n", ($dotted =~ /\A\Q$idot\E\z/iu) ? 'MATCH' : 'no');
printf("fc(U+0130) = %s\n", join(' ', map { sprintf('%04X', ord) } split //, CORE::fc(chr(0x130))));
printf("fc(U+0049) = %s\n", join(' ', map { sprintf('%04X', ord) } split //, CORE::fc(chr(0x49))));
printf("fc(U+0131) = %s\n", join(' ', map { sprintf('%04X', ord) } split //, CORE::fc(chr(0x131))));
