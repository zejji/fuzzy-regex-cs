# Perl's answer for tools/probes/d51-call-capture-survey.py: PATTERN SUBJECT OPERATION, each hex-encoded.
# Prints the match span and each group's (start,end), or "unset", or "None".
use strict;
use warnings;

my ($pat, $subj, $op) = map { pack("H*", $_) } @ARGV;
utf8::decode($pat);
utf8::decode($subj);
my $re = $op eq 'fullmatch' ? qr/\A(?:$pat)\z/ : ($op eq 'match' ? qr/\A(?:$pat)/ : qr/$pat/);
if ($subj =~ $re) {
    my @groups;
    for my $i (1 .. $#-) {
        push @groups, defined $-[$i] ? "($-[$i],$+[$i])" : "unset";
    }
    print "($-[0],$+[0]) ", join(" ", @groups), "\n";
}
else {
    print "None\n";
}
