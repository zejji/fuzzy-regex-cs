"""Caps this process's memory, so a runaway row fails with MemoryError instead of taking the machine.

    import memcap; memcap.cap_this_process(2)      # GB; a no-op off Windows or with BATTERY_MEMCAP_GB=0

On Windows the process joins a job object with a process memory limit. An allocation past the
limit fails, regex reports it as MemoryError, and the caller records the row and goes on: measured
2026-10-17 on regex 2026.9.10 with the row that took upstream to 19 GB in battery run 20261016-2's
triage (`(?b)(?r)(?:(?:\U0001d518(?:\U0001d518){s<=1}){e<=2}[a]|[a-f])...` search, flags 266),
tools/tests/memcap_check.py. The limit is per process, so it bounds one row's growth without the
cost of a process per row (the oracle part asks over half a million rows).

BATTERY_MEMCAP_GB overrides the figure the caller passes; 0 turns the cap off.
"""

from __future__ import annotations

import os
import sys


def cap_this_process(gb: float) -> bool:
    """Puts this process under a memory limit of `gb` GB. True if the limit was set."""
    gb = float(os.environ.get("BATTERY_MEMCAP_GB", gb))
    if gb <= 0 or sys.platform != "win32":
        return False
    import ctypes
    from ctypes import wintypes

    class IoCounters(ctypes.Structure):
        _fields_ = [(n, ctypes.c_ulonglong) for n in ("r", "w", "o", "rb", "wb", "ob")]

    class BasicLimits(ctypes.Structure):
        _fields_ = [("PerProcessUserTimeLimit", ctypes.c_longlong), ("PerJobUserTimeLimit", ctypes.c_longlong),
                    ("LimitFlags", wintypes.DWORD), ("MinimumWorkingSetSize", ctypes.c_size_t),
                    ("MaximumWorkingSetSize", ctypes.c_size_t), ("ActiveProcessLimit", wintypes.DWORD),
                    ("Affinity", ctypes.c_size_t), ("PriorityClass", wintypes.DWORD),
                    ("SchedulingClass", wintypes.DWORD)]

    class ExtendedLimits(ctypes.Structure):
        _fields_ = [("BasicLimitInformation", BasicLimits), ("IoInfo", IoCounters),
                    ("ProcessMemoryLimit", ctypes.c_size_t), ("JobMemoryLimit", ctypes.c_size_t),
                    ("PeakProcessMemoryUsed", ctypes.c_size_t), ("PeakJobMemoryUsed", ctypes.c_size_t)]

    job_object_limit_process_memory = 0x100
    job_object_extended_limit_information = 9
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateJobObjectW.restype = wintypes.HANDLE
    kernel32.GetCurrentProcess.restype = wintypes.HANDLE
    job = kernel32.CreateJobObjectW(None, None)
    if not job:
        return False
    info = ExtendedLimits()
    info.BasicLimitInformation.LimitFlags = job_object_limit_process_memory
    info.ProcessMemoryLimit = int(gb * 1024**3)
    if not kernel32.SetInformationJobObject(wintypes.HANDLE(job), job_object_extended_limit_information,
                                            ctypes.byref(info), ctypes.sizeof(info)):
        return False
    return bool(kernel32.AssignProcessToJobObject(wintypes.HANDLE(job), wintypes.HANDLE(kernel32.GetCurrentProcess())))
