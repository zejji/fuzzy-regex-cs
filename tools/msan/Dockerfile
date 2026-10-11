# The toolchain and CPython source for tools/screen-undefined.py's MemorySanitizer build.
#
# The MSan INTERPRETER is not built here, and cannot be: an MSan binary calls personality() at
# start-up and Docker's default seccomp profile refuses it ("CHECK failed: msan_linux.cpp:193"),
# `docker build` cannot relax that, and CPython's build runs the interpreter it is building.
# tools/msan/record-under-msan.sh builds it on first use, in a container started with seccomp
# relaxed, into a named volume that later runs reuse.
# The oracle's interpreter (see oracle.yml): upstream calls the host's `unicodedata`.
FROM python:3.14

ARG CPYTHON=3.14.7

# Recommends included: clang only RECOMMENDS the sanitizer runtimes (libclang_rt.msan.a), and
# without them every MSan link fails - configure then misreports it as a missing getaddrinfo().
RUN apt-get update \
    && apt-get install -y clang lld \
    && rm -rf /var/lib/apt/lists/*

RUN curl -sSfL "https://www.python.org/ftp/python/${CPYTHON}/Python-${CPYTHON}.tar.xz" | tar xJ -C /opt \
    && mv "/opt/Python-${CPYTHON}" /opt/cpython-src \
    && echo "${CPYTHON}" > /opt/cpython-src/VERSION

RUN git config --global --add safe.directory '*'
