# Perl's half of tools/matrix/survey.py (Perl 5.42.3, cygwin).
#
#     PERL_SIGNALS=unsafe perl tools/matrix/survey_worker.pl <rows.jsonl> <start-index>
#
# Reads translated rows from a FILE, prints READY and then one JSON line per row, flushed.
# survey.py enforces the per-row time limit and the memory cap from outside; the alarm here is a
# second guard, and it needs PERL_SIGNALS=unsafe, because Perl's default deferred signals are not
# delivered while the regex engine runs.
#
# Operations: survey.py has already written "match" as a search of \G(?:...) and "fullmatch" as
# \G(?:...)\z, both from pos() (rows with a whole-pattern call are out of dialect for fullmatch).
# A "match" of a pattern that calls itself whole stays a search whose answer must start at pos
# (the first start a backtracking engine tries is pos, so the first match found there IS the
# anchored match); survey.py refuses those with \K, which moves the reported start.
# Spans are codepoints: the subject is decoded text.
use strict;
use warnings;
use utf8;
use JSON::PP;

my ($path, $start) = @ARGV;
my $json = JSON::PP->new->utf8->canonical;
open(my $fh, '<:raw', $path) or die "cannot read $path";
my @rows = map { $json->decode($_) } grep { /\S/ } <$fh>;
close $fh;
$| = 1;
binmode STDOUT, ':raw';
print "READY\n";

for my $k ($start .. $#rows) {
    my $row = $rows[$k];
    my %res = (i => $row->{i}, unit => 'cp');
    my $flags = $row->{flags};
    my $re = eval { my $p = $row->{pattern}; $flags ? qr/(?$flags)$p/ : qr/$p/ };
    if (!defined $re) {
        my $e = $@; $e =~ s/\s+/ /g;
        %res = (i => $row->{i}, status => 'error', error => 'compile: ' . substr($e, 0, 200));
        print $json->encode(\%res), "\n";
        next;
    }
    # A slice (upstream's pos and endpos): the subject is cut at endpos and every search starts at
    # pos() through a scalar //g, so a lookbehind still sees the text before pos and \G is pos.
    my $subject = $row->{subject};
    $subject = substr($subject, 0, $row->{endpos}) if defined $row->{endpos};
    my $first = $row->{pos} // 0;
    my $op = $row->{op};
    my $ok = eval {
        local $SIG{ALRM} = sub { die "timeout\n" };
        alarm 3;
        pos($subject) = $first;
        if ($op eq 'finditer') {
            my @out;
            while ($subject =~ /$re/g) {
                push @out, { span => [$-[0], $+[0]], partial => JSON::PP::false };
                last if @out > 50;
            }
            $res{status} = 'matches';
            $res{matches} = \@out;
        } elsif ($subject =~ /$re/g) {
            my $n = $row->{ngroups};
            my @groups = map { defined $-[$_] ? [$-[$_], $+[$_]] : undef } 1 .. $n;
            my @span = ($-[0], $+[0]);
            # "match" reaches here only for a pattern that calls itself whole (survey.py wraps
            # every other one in \G(?:...)): the first match found from pos must start at pos.
            if ($op eq 'match' && $span[0] != $first) {
                $res{status} = 'none';
            } else {
                $res{status} = 'match';
                $res{span} = \@span;
                $res{groups} = \@groups;
            }
        } else {
            $res{status} = 'none';
        }
        alarm 0;
        1;
    };
    if (!$ok) {
        alarm 0;
        my $e = $@; $e =~ s/\s+/ /g;
        %res = (i => $row->{i}, status => ($e =~ /^timeout/ ? 'timeout' : 'error'),
                error => substr($e, 0, 200));
    }
    print $json->encode(\%res), "\n";
}
