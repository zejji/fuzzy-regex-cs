#!/bin/bash
# Launches one correctness battery run, detached, and returns at once.
#
#   tools/battery/run.sh RUN_ID [--commit REV] [--smoke]
#
# RUN_ID is the date and a run number, e.g. 20261016-1 (its digits are the master seed). Output and
# the progress log: .scratch/battery/RUN_ID/status in the main checkout. Launching the same RUN_ID
# again resumes it: finished parts and steps are skipped. Stop it with
#   taskkill //PID $(cat .scratch/battery/RUN_ID/driver.pid) //T //F
# (/T takes the parts and their dotnet and python children with it).
set -eu
if [ $# -lt 1 ]; then
    echo "usage: tools/battery/run.sh RUN_ID [--commit REV] [--smoke]" >&2
    exit 2
fi
here=$(cd "$(dirname "$0")" && pwd)
main=$(dirname "$(git -C "$here" rev-parse --path-format=absolute --git-common-dir)")
out="$main/.scratch/battery/$1"
mkdir -p "$out"
if [ -f "$out/driver.pid" ] && tasklist //FI "PID eq $(cat "$out/driver.pid")" 2>/dev/null | grep -q python; then
    echo "run $1 is already running as PID $(cat "$out/driver.pid")" >&2
    exit 1
fi
export PATH="/c/Program Files/dotnet:$PATH"
export PYTHONUNBUFFERED=1 PYTHONDONTWRITEBYTECODE=1
# Git Bash has no setsid; nohup plus a subshell detaches the driver from this shell and its console.
(nohup python "$here/battery.py" all "$@" >> "$out/driver.log" 2>&1 &)
sleep 5
echo "launched battery run $1; status: $out/status; driver log: $out/driver.log"
[ -f "$out/driver.pid" ] && echo "driver PID $(cat "$out/driver.pid")"
exit 0
