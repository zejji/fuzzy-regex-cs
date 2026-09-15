#!/bin/bash
# Heartbeat for a detached slice driver, for an orchestrator's Monitor tool.
#
# Prints a line every ~5 minutes WHETHER OR NOT anything changed - alive or exited, elapsed
# minutes, dirty count, HEAD, and the last driver log line. A monitor that only emits on change is
# silent for a whole run and tells you nothing; that pattern cost 42 wasted minutes once already.
#
# Fires a one-shot ALARM at 160 minutes, ~20 before the driver's own 180-minute force-kill, so the
# slice session can still be reached with a message and asked to commit. Breaks with a log tail
# when the driver exits.
#
# Usage: bash tools/heartbeat.sh <pid> <tag>
PID="$1"; TAG="$2"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="$REPO/.scratch/driver-$TAG.log"
START=$(date +%s)
ALARMED=0
DEADLINE_FILE="$REPO/.claude/driver/session-deadline.txt"   # not .scratch/: sessions clear that
# Remaining minutes of the CURRENT sitting, from the deadline the driver writes per session, so the
# alarm tracks the sitting and not the driver run (a checkpointed slice runs several sittings in
# one driver run, which misled the orchestrator twice on 2026-09-13).
remaining() {
  [ -f "$DEADLINE_FILE" ] || { echo "?"; return; }
  local dl; dl=$(python -c "import sys,datetime; d=datetime.datetime.fromisoformat(open(sys.argv[1]).read().strip()); print(int((d-datetime.datetime.now(d.tzinfo)).total_seconds()//60))" "$DEADLINE_FILE" 2>/dev/null)
  echo "${dl:-?}"
}
while true; do
  NOW=$(date +%s); EL=$(( (NOW-START)/60 ))
  if tasklist //FI "PID eq $PID" 2>/dev/null | grep -q "$PID"; then ALIVE=alive; else ALIVE=EXITED; fi
  HEAD=$(git -C "$REPO" log --oneline -1 2>/dev/null | cut -c1-60)
  DIRTY=$(git -C "$REPO" status --porcelain 2>/dev/null | wc -l)
  LAST=$(tail -n 1 "$LOG" 2>/dev/null | tr -d '\r' | cut -c1-120)
  LEFT=$(remaining)
  echo "[$TAG ${EL}m left=${LEFT}m] $ALIVE dirty=$DIRTY head=$HEAD | $LAST"
  if [ "$ALIVE" = "EXITED" ]; then
    echo "[$TAG] DRIVER EXITED - log tail:"; tail -n 25 "$LOG" 2>/dev/null | tr -d '\r'; break
  fi
  if [ "$LEFT" != "?" ] && [ "$LEFT" -le 20 ] && [ "$ALARMED" -eq 0 ]; then
    ALARMED=1
    echo "[$TAG] ALARM: $LEFT min left in this sitting before the driver kills it. Check for a commit; the session hook is already telling it to checkpoint. To speak to it: write .scratch/orchestrator-message.txt."
  fi
  if [ "$LEFT" != "?" ] && [ "$LEFT" -gt 20 ]; then ALARMED=0; fi
  sleep 300
done
