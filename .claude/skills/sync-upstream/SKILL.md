---
name: sync-upstream
description: Use to bring the FuzzyRegex port forward to a newer mrab-regex release. Bumps the upstream submodule, reads the changelog delta, maps the diff onto our files via PORTMAP.md, and ports one changelog entry at a time, test-first. Post-1.0 maintenance, roughly one session a month.
---

# Syncing with upstream

Upstream ships several releases a month and most of them are engine or semantic bug fixes, not
Unicode data bumps. This is why the port keeps upstream's structure: a sync should be reading
diffs and applying them, not redesigning anything.

One session, one sync, one commit per changelog entry.

## 1. See what changed

```powershell
$old = git -C upstream rev-parse HEAD
git -C upstream fetch --tags
git -C upstream log --oneline "$old..origin/hg" | Select-Object -First 40
git -C upstream diff --stat "$old..origin/hg" -- src/ regex/
```

Read `upstream/changelog.txt` for the range. The changelog is the unit of work: each entry is one
behaviour change, and each becomes one commit here.

Do not bump the submodule yet. Decide the scope first: if the delta spans many releases, sync to
an intermediate release rather than swallowing everything at once.

## 2. Map the diff onto our files

`docs/PORTMAP.md` maps upstream symbols to our types and members. For each changed hunk:

| Upstream file | Ours |
|---|---|
| `regex/_regex_core.py` | `src/FuzzyRegex/Parsing/` |
| `src/_regex.c` | `src/FuzzyRegex/Engine/` |
| `src/_regex_unicode.c` | generated - do not hand-edit, see below |
| `regex/_main.py` | `src/FuzzyRegex/` root |
| `regex/tests/test_regex.py` | `tests/FuzzyRegex.Tests/Ported/` |

A hunk PORTMAP cannot place means either the map is stale (fix it) or the code was never ported
(check the "not ported" section before assuming it is a gap).

## 3. Port one entry at a time, test-first

For each changelog entry:

1. **Port the upstream test change first** if the diff touches `test_regex.py`. Translate it with
   the `port-tests` skill and watch it fail against our current engine. That failure is the proof
   the bug exists here too.
2. **If upstream changed behaviour with no test**, write the test yourself from the changelog
   description, and verify the expectation against the new upstream directly rather than
   reasoning about it:

   ```powershell
   python -m pip install --upgrade regex   # or build the submodule; see the note below
   python -c "import regex; print(regex.__version__); print(regex.search(r'...', '...'))"
   ```

3. **Apply the fix** to our port, mirroring upstream's change rather than inventing an equivalent.
4. `tools/check-ratchet.ps1` must be green, then `-UpdateBaseline`, then commit:
   `sync: <changelog entry>`.

If a fix genuinely does not apply - it addresses a CPython-specific concern, or a bug our port
never had - record that in `docs/PORTMAP.md` with the reason. Skipping silently is how a sync
becomes untrustworthy.

## 4. Unicode data bumps

Never hand-edit generated tables. A UCD version bump is:

1. drop the new UCD data files into the generator's input directory,
2. `dotnet run --project src/FuzzyRegex.UnicodeGenerator`,
3. run the oracle tests (`tests/FuzzyRegex.OracleTests`), which compare every property, script
   and block against Python across all planes,
4. commit the regenerated tables and the new UCD version together.

## 5. Finish the sync

```powershell
git -C upstream checkout <new sha>     # the exact commit we now track
git add upstream
```

Then:

- update the pinned SHA and release version in `NOTICE`,
- add a CHANGELOG entry naming the upstream version this release tracks,
- rewrite `docs/plan/STATE.md`,
- append a dated line to `docs/plan/DECISIONS.md` for anything a future sync would re-derive.

## A note on the Python oracle version

The oracle compares against the `regex` module installed from PyPI, which can lag the commit our
submodule pins (checked 2026-08-29: the pinned 2026.8.12 commit had no PyPI release, so the
oracle ran 2026.7.19). Before blaming the port for a divergence, check whether the gap is just a
version difference - and if it matters, build the submodule itself instead:

```powershell
python -m pip install ./upstream        # needs a C compiler
```
