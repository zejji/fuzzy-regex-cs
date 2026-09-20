#!/bin/sh
# The author and committer of anything this repository records or publishes must be the public
# GitHub identity. On 2026-09-19 one commit was authored with the owner's employer address (a
# checkout outside this .git took the global config) and reached origin/main on 2026-09-20 before
# anyone looked; putting it right cost a sixty-commit rewrite and a force-push. Three layers now:
# this check at commit time, this check over every commit a push would publish, and the same
# check in CI over the whole history (.github/workflows/ci.yml, "Author identity").
#
#   sh .husky/check-identity.sh commit          # the identity the next commit would carry
#   sh .husky/check-identity.sh push  < stdin   # the pre-push protocol lines, one per ref
#   sh .husky/check-identity.sh history [rev]   # every commit reachable from rev (default HEAD)
#
# Exit 1 with the offending address named. The address is fixed here on purpose: reading it from
# `git config user.email` would pass exactly the misconfiguration this exists to catch.

allowed='zejji@users.noreply.github.com'

fail() {
    echo "check-identity: $1" >&2
    echo "check-identity: only <$allowed> may author or commit here; set it with 'git config user.email $allowed'." >&2
    exit 1
}

check_range() {
    # $1: a rev range or rev. Prints nothing when clean.
    bad=$(git log --format='%h %ae %ce' "$1" 2>/dev/null | awk -v ok="$allowed" '$2 != ok || $3 != ok')
    if [ -n "$bad" ]; then
        echo "$bad" | head -5 >&2
        fail "$(echo "$bad" | wc -l | tr -d ' ') commit(s) in $1 carry another address (first five above)."
    fi
}

case "$1" in
    commit)
        author=$(git var GIT_AUTHOR_IDENT | sed 's/.*<\(.*\)>.*/\1/')
        committer=$(git var GIT_COMMITTER_IDENT | sed 's/.*<\(.*\)>.*/\1/')
        [ "$author" = "$allowed" ] || fail "the author would be <$author>."
        [ "$committer" = "$allowed" ] || fail "the committer would be <$committer>."
        ;;
    push)
        zero='0000000000000000000000000000000000000000'
        while read -r local_ref local_sha remote_ref remote_sha; do
            [ "$local_sha" = "$zero" ] && continue   # a deletion publishes nothing
            if [ "$remote_sha" = "$zero" ]; then range="$local_sha"; else range="$remote_sha..$local_sha"; fi
            check_range "$range"
        done
        ;;
    history)
        check_range "${2:-HEAD}"
        ;;
    *)
        echo "usage: check-identity.sh commit | push | history [rev]" >&2
        exit 2
        ;;
esac
