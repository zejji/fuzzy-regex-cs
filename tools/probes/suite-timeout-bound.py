"""Why run_suite uses Popen + kill_tree rather than subprocess.run(timeout=...).

`dotnet test` spawns MSBuild nodes and a test host that inherit the parent's stdout handle. On a
timeout, subprocess.run kills only the direct child and then blocks draining a pipe the
grandchildren still hold open, so the bound is not a bound. While it blocks, the caller's `finally`
never runs and a control's mutation stays in the working tree.

This reproduces both halves with a stand-in: a child that spawns a longer-lived grandchild and
exits immediately, leaving the grandchild holding the handle.

Measured 2026-09-22 on Python 3.14, Windows 11. Run: python tools/probes/suite-timeout-bound.py
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import time
from pathlib import Path

BOUND = 3
GRANDCHILD_LIFETIME = 25

CHILD = (
    "import subprocess, sys; "
    f"subprocess.Popen([sys.executable, '-c', 'import time; time.sleep({GRANDCHILD_LIFETIME})']); "
    "sys.exit(0)"
)


def through_a_pipe() -> float:
    """subprocess.run with capture_output, the pattern run_suite originally used."""
    started = time.time()
    try:
        subprocess.run([sys.executable, "-c", CHILD], capture_output=True, text=True, timeout=BOUND)
    except subprocess.TimeoutExpired:
        pass
    return time.time() - started


def through_a_file() -> float:
    """Popen onto a file plus a whole-tree kill, the pattern `consume` uses and run_suite now does."""
    started = time.time()
    with tempfile.NamedTemporaryFile("w", suffix=".log", delete=False) as handle:
        log = Path(handle.name)
        proc = subprocess.Popen(
            [sys.executable, "-c", CHILD], stdout=handle, stderr=subprocess.STDOUT,
            **({} if sys.platform == "win32" else {"start_new_session": True}),
        )
        try:
            proc.wait(timeout=BOUND)
        except subprocess.TimeoutExpired:
            proc.kill()
    elapsed = time.time() - started
    # The grandchild still holds this handle, which is the whole point: deleting it raises
    # PermissionError [WinError 32] until that process exits. Left for the temp directory to reap.
    try:
        log.unlink(missing_ok=True)
    except PermissionError:
        pass
    return elapsed


if __name__ == "__main__":
    piped = through_a_pipe()
    filed = through_a_file()
    print(f"bound asked for: {BOUND}s, grandchild lives {GRANDCHILD_LIFETIME}s")
    print(f"  subprocess.run(capture_output=True, timeout={BOUND}): returned after {piped:.1f}s")
    print(f"  Popen onto a file + kill:                            returned after {filed:.1f}s")
    print("  the first is the defect: the bound is not a bound while a grandchild holds the pipe")
