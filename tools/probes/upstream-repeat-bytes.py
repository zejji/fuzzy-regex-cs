"""Where upstream's backtrack-stack bytes go in a repeated group (ledger entry 18, S86).

Builds an instrumented `/Od /Zi` copy of the pinned `upstream/` source and counts, for one
`fullmatch`, every push onto and pop off each byte stack, by the source line that made it and the
number of bytes. It also logs every time the backtrack stack's capacity grows.

    python tools/probes/upstream-repeat-bytes.py            # (ab)* and (?:ab)* at n = 1000
    python tools/probes/upstream-repeat-bytes.py 100000     # another n

The builds need MSVC (see upstream-bestmatch-lost-candidate.py, whose build helper this reuses).
Everything is built under `.scratch/`; `upstream/` is never written to.
"""

import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location(
    "bestmatch_probe", os.path.join(HERE, "upstream-bestmatch-lost-candidate.py"))
probe = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(probe)

# The counters. Each push and pop records (stack, line, bytes); a stack is 'b' for state->bstack,
# 's' for state->sstack, 'p' for state->pstack and '?' for any other.
COUNTERS = r"""
#include <stdio.h>
#include <stdlib.h>
#define PROBE_MAX 4096
typedef struct { char stack; char op; int line; size_t bytes; size_t calls; } ProbeRow;
static ProbeRow probe_rows[PROBE_MAX];
static int probe_used = 0;
static int probe_registered = 0;
static size_t probe_bstack_peak = 0;
static void probe_dump(void) {
    int i;
    fprintf(stderr, "PEAK bstack %zu\n", probe_bstack_peak);
    for (i = 0; i < probe_used; i++)
        fprintf(stderr, "ROW %c %c line %d bytes %zu calls %zu\n", probe_rows[i].stack,
          probe_rows[i].op, probe_rows[i].line, probe_rows[i].bytes, probe_rows[i].calls);
}
static void probe_note(char stack, char op, int line, size_t bytes) {
    int i;
    if (!probe_registered) {
        probe_registered = 1;
        atexit(probe_dump);
    }
    for (i = 0; i < probe_used; i++) {
        if (probe_rows[i].stack == stack && probe_rows[i].op == op &&
          probe_rows[i].line == line && probe_rows[i].bytes == bytes) {
            probe_rows[i].calls++;
            return;
        }
    }
    if (probe_used < PROBE_MAX) {
        probe_rows[probe_used].stack = stack;
        probe_rows[probe_used].op = op;
        probe_rows[probe_used].line = line;
        probe_rows[probe_used].bytes = bytes;
        probe_rows[probe_used].calls = 1;
        probe_used++;
    }
}
#define PROBE_STACK(state, stack) ((stack) == &(state)->bstack ? 'b' : \
  (stack) == &(state)->sstack ? 's' : (stack) == &(state)->pstack ? 'p' : '?')
"""

# The four primitives are renamed and given a line argument; a macro after each definition passes
# __LINE__, so every later caller - the typed wrappers push_int8 and friends included - is counted
# at its own line.
PATCHES = [
    ('#include "pythread.h"\n', '#include "pythread.h"\n' + COUNTERS),
    (
        "Py_LOCAL_INLINE(BOOL) ByteStack_push(RE_State* state, ByteStack* stack, BYTE\n  item) {\n",
        "Py_LOCAL_INLINE(BOOL) ByteStack_push_l(RE_State* state, ByteStack* stack, BYTE\n"
        "  item, int line) {\n"
        "    probe_note(PROBE_STACK(state, stack), '+', line, 1);\n",
    ),
    (
        "        new_capacity = stack->capacity * 2;\n\n"
        "        if (new_capacity == 0)\n            new_capacity = 64;\n",
        "        new_capacity = stack->capacity * 2;\n\n"
        "        if (new_capacity == 0)\n            new_capacity = 64;\n"
        "        if (stack == &state->bstack)\n"
        "            fprintf(stderr, \"GROW bstack %zu -> %zu at count %zu\\n\", stack->capacity,\n"
        "              new_capacity, stack->count + 1);\n",
    ),
    (
        "    stack->storage[stack->count++] = item;\n",
        "    stack->storage[stack->count++] = item;\n"
        "    if (stack == &state->bstack && stack->count > probe_bstack_peak)\n"
        "        probe_bstack_peak = stack->count;\n",
    ),
    (
        "Py_LOCAL_INLINE(BOOL) ByteStack_push_block(RE_State* state, ByteStack* stack,\n"
        "  void* block, size_t count) {\n    size_t new_count;\n",
        "#define ByteStack_push(s, st, i) ByteStack_push_l(s, st, i, __LINE__)\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_push_block_l(RE_State* state, ByteStack* stack,\n"
        "  void* block, size_t count, int line) {\n    size_t new_count;\n"
        "    probe_note(PROBE_STACK(state, stack), '+', line, count);\n",
    ),
    (
        "        stack->capacity = new_capacity;\n        stack->storage = new_storage;\n    }\n\n"
        "    Py_MEMCPY(stack->storage + stack->count, block, count);\n"
        "    stack->count = new_count;\n",
        "        if (stack == &state->bstack)\n"
        "            fprintf(stderr, \"GROW bstack %zu -> %zu at count %zu\\n\", stack->capacity,\n"
        "              new_capacity, new_count);\n"
        "        stack->capacity = new_capacity;\n        stack->storage = new_storage;\n    }\n\n"
        "    Py_MEMCPY(stack->storage + stack->count, block, count);\n"
        "    stack->count = new_count;\n"
        "    if (stack == &state->bstack && new_count > probe_bstack_peak)\n"
        "        probe_bstack_peak = new_count;\n",
    ),
    (
        "Py_LOCAL_INLINE(BOOL) ByteStack_pop(RE_State* state, ByteStack* stack, BYTE*\n  item) {\n",
        "#define ByteStack_push_block(s, st, b, c) ByteStack_push_block_l(s, st, b, c, __LINE__)\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_pop_l(RE_State* state, ByteStack* stack, BYTE*\n"
        "  item, int line) {\n"
        "    probe_note(PROBE_STACK(state, stack), '-', line, 1);\n",
    ),
    (
        "Py_LOCAL_INLINE(BOOL) ByteStack_pop_block(RE_State* state, ByteStack* stack,\n"
        "  void* block, size_t count) {\n",
        "#define ByteStack_pop(s, st, i) ByteStack_pop_l(s, st, i, __LINE__)\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_pop_block_l(RE_State* state, ByteStack* stack,\n"
        "  void* block, size_t count, int line) {\n"
        "    probe_note(PROBE_STACK(state, stack), '-', line, count);\n",
    ),
    (
        "/* Drops a byte off a stack of bytes. */\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_drop(RE_State* state, ByteStack* stack) {\n",
        "#define ByteStack_pop_block(s, st, b, c) ByteStack_pop_block_l(s, st, b, c, __LINE__)\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_drop_l(RE_State* state, ByteStack* stack, int line) {\n"
        "    probe_note(PROBE_STACK(state, stack), '-', line, 1);\n",
    ),
    (
        "Py_LOCAL_INLINE(BOOL) ByteStack_drop_block(RE_State* state, ByteStack* stack,\n"
        "  size_t count) {\n",
        "#define ByteStack_drop(s, st) ByteStack_drop_l(s, st, __LINE__)\n"
        "Py_LOCAL_INLINE(BOOL) ByteStack_drop_block_l(RE_State* state, ByteStack* stack,\n"
        "  size_t count, int line) {\n"
        "    probe_note(PROBE_STACK(state, stack), '-', line, count);\n",
    ),
    (
        "/* Gets the top block off a stack of bytes. */\n",
        "#define ByteStack_drop_block(s, st, c) ByteStack_drop_block_l(s, st, c, __LINE__)\n"
        "/* Gets the top block off a stack of bytes. */\n",
    ),
]

ASK = r"""
import regex, sys
n = int(sys.argv[2])
print('using', regex._regex.__file__)
m = regex.fullmatch(sys.argv[3], 'ab' * n)
print('span', m.span() if m else None)
"""


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    tree = probe.build("s86-repeat-bytes", PATCHES)
    for pattern in ("(ab)*", "(?:ab)*"):
        out, err = probe.run_in(tree, ASK, str(n), pattern)
        probe.say("== %s over 'ab' * %d" % (pattern, n))
        probe.say(out.strip())
        probe.say(err.strip())


if __name__ == "__main__":
    main()
