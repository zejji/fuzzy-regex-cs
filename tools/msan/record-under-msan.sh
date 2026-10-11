#!/bin/sh
# Runs inside the image tools/msan/Dockerfile builds; started by tools/screen-undefined.py.
# /src is this checkout, read-only. /work holds rows.jsonl and receives worker.log. /cache is a
# named volume that keeps the MSan interpreter between runs.
set -e

# One build per CPython version, so a volume built by an older image is never reused.
BUILD=/cache/cpython-$(cat /opt/cpython-src/VERSION)
PY=$BUILD/python

# The interpreter, with MSan, built once per volume. MSan needs EVERY piece of code in the process
# instrumented or it reports false positives, so CPython itself is built with it.
# `--without-pymalloc` because CPython's small-object allocator hands out memory MSan cannot see
# being initialised; `-fsanitize-recover=memory` so one run reports every row instead of stopping at
# the first. `make` exits non-zero because a few optional stdlib modules do not build under MSan;
# what the screen needs is the interpreter and `unicodedata`, which upstream imports, so those two
# are checked instead of the exit code. flock, because two screens may start on one cold volume.
exec 9> /cache/.build.lock
flock 9
if [ ! -x "$PY" ] || ! ls $BUILD/build/lib.*/unicodedata* > /dev/null 2>&1; then
    rm -rf "$BUILD"
    cp -r /opt/cpython-src "$BUILD"
    cd "$BUILD"
    CFLAGS="-fsanitize-recover=memory" LDFLAGS="-fsanitize-recover=memory" CC=clang CXX=clang++ \
        ./configure -q --with-memory-sanitizer --without-pymalloc > /cache/configure.log 2>&1
    make -s -j"$(nproc)" > /cache/make.log 2>&1 || true
    "$PY" -c "print('msan cpython built')"
    ls build/lib.*/unicodedata* > /dev/null
fi
flock -u 9
STDLIB=$(echo $BUILD/build/lib.*)

# Upstream's extension, compiled from the checkout's own submodule every time, so the screen tests
# the pinned code and never a stale copy. Origin tracking is what names the local an uninitialised
# value came from; it is affordable because only candidate rows are recorded. A fixed suffix: the
# image pins CPython 3.14, and an uninstalled build cannot answer `sysconfig`.
mkdir -p /tmp/site/regex
cp /src/upstream/regex/*.py /tmp/site/regex/
clang -fsanitize=memory -fsanitize-recover=memory -fsanitize-memory-track-origins=2 \
    -fno-omit-frame-pointer -g -O2 -fPIC -shared \
    -I"$BUILD/Include" -I"$BUILD" -I/src/upstream/src \
    /src/upstream/src/_regex.c /src/upstream/src/_regex_unicode.c \
    -o /tmp/site/regex/_regex.cpython-314-x86_64-linux-gnu.so

PYTHONPATH="/tmp/site:$STDLIB" "$PY" /src/tools/record-oracle.py \
    --rows /work/rows.jsonl --output /work/recorded.jsonl > /work/recorder.out
