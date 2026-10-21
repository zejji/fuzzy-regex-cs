"""Proves tools/memcap.py on the row that took regex 2026.9.10 to 19 GB, without risking the machine.

    python tools/tests/memcap_check.py        # exit 0: the capped row raised MemoryError and the
                                              # same process then answered the next row

The row runs in a child process capped at 1 GB by memcap; a psutil watchdog here kills the child
above 3 GB, so a cap that does not hold costs 3 GB, not 19. Windows only (memcap is a no-op
elsewhere, and the check says so).
"""

import subprocess
import sys
import time
from pathlib import Path

import psutil

TOOLS = Path(__file__).resolve().parent.parent
CHILD = r"""
import sys
sys.path.insert(0, sys.argv[1])
import memcap, regex
print("capped", memcap.cap_this_process(1), flush=True)
p = regex.compile("(?b)(?r)(?:(?:\U0001d518(?:\U0001d518){s<=1}){e<=2}[a]|[a-f])(?:\\p{Lu}.|\\S)(?>(?:\U0001d518a){s<=1,i<=1,d<=1})", 266)
try:
    print("answer", p.search("\U0001d518 \U0001d518aa\U00010428\r\n", timeout=60), flush=True)
except MemoryError:
    print("MemoryError", flush=True)
print("next", regex.search("a+", "xaa").span(), flush=True)
"""


def main() -> int:
    if sys.platform != "win32":
        print("memcap is Windows only; nothing to check here")
        return 0
    child = subprocess.Popen([sys.executable, "-c", CHILD, str(TOOLS)], stdout=subprocess.PIPE, text=True)
    proc, peak, start = psutil.Process(child.pid), 0, time.time()
    while child.poll() is None:
        try:
            peak = max(peak, proc.memory_info().rss)
        except psutil.Error:
            pass
        if peak > 3 * 1024**3 or time.time() - start > 120:
            proc.kill()
            print(f"FAIL: killed by the watchdog at {peak >> 20} MB after {time.time() - start:.0f} s")
            return 1
        time.sleep(0.05)
    out = child.stdout.read()
    print(out.strip())
    print(f"peak {peak >> 20} MB, {time.time() - start:.1f} s, exit {child.returncode}")
    ok = "capped True" in out and "MemoryError" in out and "next (1, 3)" in out
    print("OK" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
