# Perl battery runner. Prints engine, case id, span and final capture of group 1.
use strict; use warnings;
use File::Basename qw(dirname); my $here = dirname(__FILE__) . "/";
open my $fh, '<', "${here}battery.tsv" or die $!;
while (my $line = <$fh>) {
  next if $line =~ /^#/ or $line !~ /\S/;
  chomp $line; $line =~ s/\r$//;
  my ($id, $pat, $subj) = split /\t/, $line, -1;
  my $r;
  if ($subj =~ /$pat/) { $r = sprintf "span=%d,%d g1=%s", $-[0], $+[0], defined $1 ? "'$1'" : 'unset'; }
  else { $r = 'nomatch'; }
  print "perl $^V\t$id\t$r\n";
}
