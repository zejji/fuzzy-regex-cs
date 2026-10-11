#!/bin/bash
# tre-agrep CLI: deletions cost 1, insertions/substitutions priced out, at most 2 errors.
cd "$(dirname "$0")"
for c in 'ac+:a' 'b+:bb' 'b*:bba' 'b{3}:b' '(ab)+:ab' 'cats|cat:cat'; do
  p=${c%%:*}; t=${c#*:}
  printf '%s\n' "$t" | tre-agrep -s --show-position -D 1 -I 9 -S 9 -E 2 "$p" | sed "s/^/tre-agrep $(tre-agrep --version | head -1 | awk '{print $NF}') '$p' over '$t': /"
done
