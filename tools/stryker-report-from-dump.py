"""Rebuild a Stryker mutation-report.json (schema 2) from a memory dump of a running dotnet-stryker.

Stryker writes its report only when a run ends, so a run that must be stopped early loses every
verdict. A full dump (`dotnet-dump collect -p <pid> --type Full`, which does not stop the run) keeps
them in the heap; this script reads every Stryker.Core.Mutants.Mutant object and writes the report
Stryker would have written, with two honest differences:
  * mutants not yet tested keep status "Pending" (Stryker never emits it; the re-run tooling needs it);
  * "replacement" is omitted (the mutated syntax tree is not flattened in the dump) and
    "coveredBy"/"killedBy" are omitted (coverage analysis is off in this repo's runs).
Locations are the original node's span (leading and trailing trivia stripped), 1-based line and
column, as Stryker reports them. Verified 2026-09-18 against Stryker's own log counts and a blind
re-derivation (docs: .claude/driver/compact-handoff.md).

Usage: python tools/stryker-report-from-dump.py <dump.dmp> <out mutation-report.json> [<projectRoot>]
projectRoot defaults to the src/FuzzyRegex directory that the dump's file paths point into.
"""
import collections
import json
import os
import re
import subprocess
import sys

STATUS = {0: 'Pending', 1: 'Killed', 2: 'Survived', 3: 'Timeout', 4: 'CompileError',
          5: 'Ignored', 6: 'NoCoverage', 7: 'RuntimeError'}


def analyze(dump, cmds):
    """One dotnet-dump session (one load of the dump); commands go through stdin because
    thousands of --command arguments fail silently."""
    p = subprocess.run(['dotnet-dump', 'analyze', dump], input='\n'.join(cmds + ['exit']) + '\n',
                       capture_output=True, text=True, errors='replace')
    if p.returncode != 0:
        sys.exit(f'dotnet-dump failed: {p.stderr[-500:]}')
    return p.stdout.replace('\r', '')


def objects(dump, cmds):
    return [q for q in re.split(r'\n(?=Name:\s)', analyze(dump, cmds)) if q.startswith('Name:')]


def fld(obj, name):
    m = re.search(r'\s(\S+)\s+(?:<' + name + r'>k__BackingField|' + name + r')\s*$', obj, re.M)
    return m.group(1) if m else None


def string_value(obj):
    m = re.search(r'^String:\s*(.*)$', obj, re.M)
    return m.group(1) if m else ''


def span(text, pos, width):
    """Position is the node's FullSpan start (leading trivia included); Stryker reports the Span."""
    a, b = pos, min(len(text), pos + width)
    while a < b:
        if text[a].isspace():
            a += 1
        elif text.startswith('//', a):
            nl = text.find('\n', a)
            a = b if nl < 0 else nl
        elif text.startswith('/*', a):
            end = text.find('*/', a)
            a = b if end < 0 else end + 2
        else:
            break
    while b > a and text[b - 1].isspace():
        b -= 1
    return a, b


def linecol(text, offset):
    return text.count('\n', 0, offset) + 1, offset - (text.rfind('\n', 0, offset) + 1) + 1


def main(dump, out, project_root=None):
    addrs = re.findall(r'^([0-9a-f]{12,16})\s*$',
                       analyze(dump, ['dumpheap -type Stryker.Core.Mutants.Mutant -short']), re.M)
    print(f'mutant objects: {len(addrs)}', file=sys.stderr)
    rows = []
    for m in objects(dump, ['dumpobj ' + a for a in addrs]):
        if 'Stryker.Core.Mutants.Mutant' not in m.split('\n')[0]:
            continue
        if fld(m, 'Id') is None or fld(m, 'ResultStatus') is None or fld(m, 'Mutation') is None:
            continue  # a dumpobj error block or a split artifact, not a mutant
        rows.append(dict(id=int(fld(m, 'Id')), status=int(fld(m, 'ResultStatus')),
                         mutation=fld(m, 'Mutation'), static=fld(m, 'IsStaticValue') == '1',
                         reason=fld(m, 'ResultStatusReason')))
    for r, m in zip(rows, objects(dump, ['dumpobj ' + r['mutation'] for r in rows])):
        r['name'] = fld(m, 'DisplayName')
        r['node'] = fld(m, 'OriginalNode')
        r['desc'] = fld(m, 'Description')
    for r, n in zip(rows, objects(dump, ['dumpobj ' + r['node'] for r in rows])):
        r['pos'] = int(fld(n, 'Position'))
        r['tree'] = fld(n, '_syntaxTree')
        r['green'] = fld(n, 'Green')
    for r, g in zip(rows, objects(dump, ['dumpobj ' + r['green'] for r in rows])):
        r['width'] = int(fld(g, '_fullWidth') or 0)
    trees = sorted({r['tree'] for r in rows})
    path_addr = {t: fld(o, '_path') for t, o in zip(trees, objects(dump, ['dumpobj ' + t for t in trees]))}
    wanted = [a for a in list(path_addr.values()) + [r[k] for r in rows for k in ('name', 'reason', 'desc')]
              if a and int(a, 16) != 0]
    str_addrs = sorted(set(wanted))
    strings = {a: string_value(o) for a, o in zip(str_addrs, objects(dump, ['dumpobj ' + a for a in str_addrs]))}
    tree_path = {t: strings[a] for t, a in path_addr.items()}

    sources = {}

    def source(path):
        if path not in sources:
            sources[path] = open(path, encoding='utf-8-sig').read()
        return sources[path]

    if project_root is None:
        any_path = next(iter(tree_path.values()))
        marker = os.sep + 'FuzzyRegex' + os.sep
        project_root = (any_path[:any_path.find(marker) + len(marker) - 1] if marker in any_path
                        else os.path.dirname(any_path))
    files = collections.OrderedDict()
    for r in sorted(rows, key=lambda r: r['id']):
        path = tree_path[r['tree']]
        text = source(path)
        a, b = span(text, r['pos'], r['width'])
        sl, sc = linecol(text, a)
        el, ec = linecol(text, b)
        mutant = {'id': str(r['id']), 'mutatorName': strings.get(r['name'], ''),
                  'location': {'start': {'line': sl, 'column': sc}, 'end': {'line': el, 'column': ec}},
                  'status': STATUS.get(r['status'], str(r['status'])), 'static': r['static']}
        if strings.get(r['reason']):
            mutant['statusReason'] = strings[r['reason']]
        if strings.get(r['desc']):
            mutant['description'] = strings[r['desc']]
        files.setdefault(path, {'language': 'cs', 'source': text, 'mutants': []})['mutants'].append(mutant)
    report = {'schemaVersion': '2', 'thresholds': {'high': 80, 'low': 60}, 'projectRoot': project_root,
              'files': files, 'recoveredFrom': os.path.basename(dump)}
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, 'w', encoding='utf-8') as f:
        json.dump(report, f, indent=1)
    counts = collections.Counter(m['status'] for v in files.values() for m in v['mutants'])
    print(f'wrote {out}: {len(rows)} mutants in {len(files)} files; {dict(counts)}', file=sys.stderr)


if __name__ == '__main__':
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
