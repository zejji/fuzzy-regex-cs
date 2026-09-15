"""Times UPSTREAM on chosen rows of a recorded wave at the same truncated lengths as the port half.

The port half is tools/probes/port-long-subject-cost.ps1; its header says how to reproduce the wave
and why the truncation cuts from the right. Run both or neither: a cost curve for one engine says
only that it is slow, and the question S52 sitting 5 asked was whether the two engines are in
different complexity classes. They are not - on row 307 both are quadratic.

    python tools/probes/upstream-long-subject-cost.py
"""
import argparse
import json
import time

import regex


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--wave", default="TestResults/oracle/wave.jsonl")
    parser.add_argument("--rows", default="305,307")
    args = parser.parse_args()

    wanted = {int(part) for part in args.rows.split(",")}
    selected = []
    with open(args.wave, encoding="utf-8") as handle:
        index = 0
        for line in handle:
            row = json.loads(line)
            if row.get("kind") == "header":
                continue
            index += 1
            if index in wanted:
                selected.append((index, row))

    for number, row in selected:
        subject = row["subject"]
        partial = bool(row.get("partial", False))
        print()
        print("row %d  %s  partial=%s  flags=0x%x  full length %d"
              % (number, row["pattern"], partial, row["flags"], len(subject)))
        compiled = regex.compile(row["pattern"], row["flags"])
        for n in (100, 200, 400, 800, 1600, 3200, 6400, 12800, len(subject)):
            if n > len(subject):
                continue
            started = time.perf_counter()
            try:
                match = compiled.search(subject[:n], partial=partial)
                elapsed = (time.perf_counter() - started) * 1000
                answer = "(%d,%d)" % match.span() if match else "None"
            except Exception as error:  # noqa: BLE001 - whatever upstream raises IS the answer
                elapsed = (time.perf_counter() - started) * 1000
                answer = "ERROR " + type(error).__name__
            print("  n=%-6d %9.0f ms   %s" % (n, elapsed, answer))
            if elapsed > 30000:
                print("  (stopping: over 30s)")
                break


if __name__ == "__main__":
    main()
