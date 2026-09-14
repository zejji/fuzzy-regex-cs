"""What the definitive source and three second engines say about the default case fold of
U+0130, U+0131 and plain I/i - the question ledger entry 7 turns on.

Design spec amendment 16 asks for a real run of a second engine before calling a divergence
upstream's bug. This probe puts four sources on record for the same 5x5 grid: the Unicode
Character Database's own CaseFolding.txt (the definitive source - see its header below for
what each status letter means), CPython's str.casefold() (which implements UCD default full
folding), upstream `regex`'s own fold_case()/get_expand_on_folding() internals under both
(?i) and (?fi), and PCRE2 compiled with PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS. Perl and .NET
answer the same grid in the sibling probes upstream-turkic-grid.pl and upstream-turkic-grid.ps1.

CaseFolding.txt is fetched from unicode.org and cached under .scratch/ucd/ (gitignored,
created on demand) - a clean checkout has no cache yet, so the first run downloads it; later
runs read the cached copy instead of re-fetching.

Run it:

    python tools/probes/upstream-turkic-definition.py

Real run, 2026-09-14, regex 2026.9.10, PCRE2 10.47 2025-10-21 (loaded from
C:\\Program Files\\Git\\mingw64\\bin\\libpcre2-8-0.dll):

    === CaseFolding.txt 17.0.0 ===
    0049; C; 0069; # LATIN CAPITAL LETTER I
    0049; T; 0131; # LATIN CAPITAL LETTER I
    00DF; F; 0073 0073; # LATIN SMALL LETTER SHARP S
    0130; F; 0069 0307; # LATIN CAPITAL LETTER I WITH DOT ABOVE
    0130; T; 0069; # LATIN CAPITAL LETTER I WITH DOT ABOVE
    FB01; F; 0066 0069; # LATIN SMALL LIGATURE FI

    === CaseFolding.txt header, status letters ===
    # The data supports both implementations that require simple case foldings
    # (where string lengths don't change), and implementations that allow full case folding
    # full case foldings are superior: for example, they allow "FUSS" and "Fuß" to match.
    # C: common case folding, common mappings shared by both simple and full mappings.
    # F: full case folding, mappings that cause strings to grow in length. Multiple characters are separated by spaces.
    # S: simple case folding, mappings to single characters where different from F.
    # T: special case for uppercase I and dotted uppercase I
    #    - For non-Turkic languages, this mapping is normally not used.
    #    - For Turkic languages (tr, az), this mapping can be used instead of the normal mapping for these characters.
    #      Note that the Turkic mappings do not maintain canonical equivalence without additional processing.
    #  A. To do a simple case folding, use the mappings with status C + S.
    #  B. To do a full case folding, use the mappings with status C + F.
    #    behavior. (The default option is to exclude them.)

    (0069 and 0131 print no row of their own above - neither has an explicit mapping in the
    file, so each folds to itself. That is an absence in the output, not a line of it.)

    === Python str.casefold (CPython, full C+F folding) ===
      U+0130 I-dot       casefold -> ['0x69', '0x307']
      i + U+0307         casefold -> ['0x69', '0x307']
      i                  casefold -> ['0x69']
      I                  casefold -> ['0x69']
      U+0131 dotless i   casefold -> ['0x131']

    === upstream regex (pinned release) fullmatch under (?i) and (?fi) ===
      regex 2026.9.10
      pat U+0130 I-dot       subj U+0130 I-dot       (?i)=Y (?fi)=Y
      pat U+0130 I-dot       subj i + U+0307         (?i)=n (?fi)=n
      pat U+0130 I-dot       subj i                  (?i)=Y (?fi)=Y
      pat U+0130 I-dot       subj I                  (?i)=n (?fi)=n
      pat U+0130 I-dot       subj U+0131 dotless i   (?i)=n (?fi)=n
      pat i + U+0307         subj U+0130 I-dot       (?i)=n (?fi)=n
      pat i + U+0307         subj i + U+0307         (?i)=Y (?fi)=Y
      pat i + U+0307         subj i                  (?i)=n (?fi)=n
      pat i + U+0307         subj I                  (?i)=n (?fi)=n
      pat i + U+0307         subj U+0131 dotless i   (?i)=n (?fi)=n
      pat i                  subj U+0130 I-dot       (?i)=Y (?fi)=Y
      pat i                  subj i + U+0307         (?i)=n (?fi)=n
      pat i                  subj i                  (?i)=Y (?fi)=Y
      pat i                  subj I                  (?i)=Y (?fi)=Y
      pat i                  subj U+0131 dotless i   (?i)=n (?fi)=n
      pat I                  subj U+0130 I-dot       (?i)=n (?fi)=n
      pat I                  subj i + U+0307         (?i)=n (?fi)=n
      pat I                  subj i                  (?i)=Y (?fi)=Y
      pat I                  subj I                  (?i)=Y (?fi)=Y
      pat I                  subj U+0131 dotless i   (?i)=Y (?fi)=Y
      pat U+0131 dotless i   subj U+0130 I-dot       (?i)=n (?fi)=n
      pat U+0131 dotless i   subj i + U+0307         (?i)=n (?fi)=n
      pat U+0131 dotless i   subj i                  (?i)=n (?fi)=n
      pat U+0131 dotless i   subj I                  (?i)=Y (?fi)=Y
      pat U+0131 dotless i   subj U+0131 dotless i   (?i)=Y (?fi)=Y

    === upstream regex: fold_case and expand_on_folding ===
      fold_case(FULLCASE|IGNORECASE, U+0130) -> ['0x130']
      fold_case(...).lower()               -> ['0x69', '0x307']
      fold_case(FULLCASE|IGNORECASE, U+0131) -> ['0x131']
      fold_case(FULLCASE|IGNORECASE, 0xDF)   -> ['0x73', '0x73']
      0x130 in get_expand_on_folding(): True
      0x131 in get_expand_on_folding(): False
      expand_on_folding entries whose fold_case does NOT expand:
        U+0130 folds to ['0x130']

    === PCRE2 (PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS) ===
      PCRE2 10.47 2025-10-21 (loaded from C:\\Program Files\\Git\\mingw64\\bin\\libpcre2-8-0.dll)
      pat U+0130 I-dot       subj U+0130 I-dot       -> Y
      pat U+0130 I-dot       subj i + U+0307         -> n
      pat U+0130 I-dot       subj i                  -> n
      pat U+0130 I-dot       subj I                  -> n
      pat U+0130 I-dot       subj U+0131 dotless i   -> n
      pat i + U+0307         subj U+0130 I-dot       -> n
      pat i + U+0307         subj i + U+0307         -> Y
      pat i + U+0307         subj i                  -> n
      pat i + U+0307         subj I                  -> n
      pat i + U+0307         subj U+0131 dotless i   -> n
      pat i                  subj U+0130 I-dot       -> n
      pat i                  subj i + U+0307         -> n
      pat i                  subj i                  -> Y
      pat i                  subj I                  -> Y
      pat i                  subj U+0131 dotless i   -> n
      pat I                  subj U+0130 I-dot       -> n
      pat I                  subj i + U+0307         -> n
      pat I                  subj i                  -> Y
      pat I                  subj I                  -> Y
      pat I                  subj U+0131 dotless i   -> n
      pat U+0131 dotless i   subj U+0130 I-dot       -> n
      pat U+0131 dotless i   subj i + U+0307         -> n
      pat U+0131 dotless i   subj i                  -> n
      pat U+0131 dotless i   subj I                  -> n
      pat U+0131 dotless i   subj U+0131 dotless i   -> Y

    PCRE2 never reaches the full fold (`U+0130` vs `i` is `n`) and never takes the Turkic pairing
    (`I` vs `U+0131` is `n`) - it is the plain default-simple grid, UCD `C` rows only. Upstream
    `regex` under (?i) takes the Turkic pairing (`I`/`U+0131` both directions, `Y`) that the UCD
    header says is excluded by default, and under (?fi) still does not reach the full fold
    (`U+0130` vs `i + U+0307` stays `n`) - both are the divergence ledger entry 7 is about.
"""
import ctypes
import sys
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CACHE = REPO / '.scratch/ucd'
UNICODE_VERSION = '17.0.0'

# ponytail: DLL search list, not a general library resolver - three fixed Windows paths for the
# one machine this probe runs on. If PCRE2 moves again, add a fourth path rather than building a
# search algorithm.
PCRE2_DLL_CANDIDATES = [
    r'C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll',
    r'C:\Program Files\Git\mingw64\libexec\git-core\libpcre2-8-0.dll',
    'libpcre2-8-0.dll',  # let the OS loader search PATH as a last resort
]


def fetch(name):
    CACHE.mkdir(parents=True, exist_ok=True)
    path = CACHE / f'{UNICODE_VERSION}-{name}'
    if not path.exists():
        url = f'https://www.unicode.org/Public/{UNICODE_VERSION}/ucd/{name}'
        print(f'downloading {url}', file=sys.stderr, flush=True)
        with urllib.request.urlopen(url, timeout=120) as response:
            path.write_bytes(response.read())
    return path.read_text(encoding='utf-8')


def load_pcre2():
    errors = []
    for candidate in PCRE2_DLL_CANDIDATES:
        try:
            return ctypes.CDLL(candidate), candidate
        except OSError as exc:
            errors.append(f'{candidate}: {exc}')
    raise RuntimeError(
        'could not load libpcre2-8-0.dll from any candidate path:\n  ' + '\n  '.join(errors)
    )


print('=== CaseFolding.txt', UNICODE_VERSION, '===')
text = fetch('CaseFolding.txt')
for line in text.splitlines():
    body = line.split('#')[0].strip()
    if not body:
        continue
    code = body.split(';')[0].strip()
    if code in ('0049', '0069', '0130', '0131', '00DF', 'FB01'):
        print(line)

print()
print('=== CaseFolding.txt header, status letters ===')
# The slice has to reach past the `T:` line and its four indented continuations, or the probe
# offered as ledger entry 7's reproduction does not print the `T`-row text the entry quotes.
# It stopped at 40 until S47b's blind review noticed; `T:` is line 42 and the "default option is
# to exclude them" sentence is line 53.
for line in text.splitlines()[:60]:
    if line.startswith('#') and any(
        k in line
        for k in ('C:', 'F:', 'S:', 'T:', 'full case folding', 'simple case folding', 'Turkic', 'exclude them')
    ):
        print(line)

SUBJECTS = {
    'U+0130 I-dot': '\u0130',
    'i + U+0307': 'i\u0307',
    'i': 'i',
    'I': 'I',
    'U+0131 dotless i': '\u0131',
}

print()
print('=== Python str.casefold (CPython, full C+F folding) ===')
for name, s in SUBJECTS.items():
    print(f'  {name:18} casefold -> {[hex(ord(c)) for c in s.casefold()]}')

print()
print('=== upstream regex (pinned release) fullmatch under (?i) and (?fi) ===')
import regex
print('  regex', regex.__version__)
for pname, pat in SUBJECTS.items():
    for sname, subj in SUBJECTS.items():
        simple = regex.compile(regex.escape(pat), regex.I).fullmatch(subj)
        full = regex.compile(regex.escape(pat), regex.I | regex.F).fullmatch(subj)
        print(f'  pat {pname:18} subj {sname:18} (?i)={"Y" if simple else "n"} (?fi)={"Y" if full else "n"}')

print()
print('=== upstream regex: fold_case and expand_on_folding ===')
from regex import _regex
FULL = regex.I | regex.F | regex.U
print('  fold_case(FULLCASE|IGNORECASE, U+0130) ->', [hex(ord(c)) for c in _regex.fold_case(FULL, '\u0130')])
print('  fold_case(...).lower()               ->', [hex(ord(c)) for c in _regex.fold_case(FULL, '\u0130').lower()])
print('  fold_case(FULLCASE|IGNORECASE, U+0131) ->', [hex(ord(c)) for c in _regex.fold_case(FULL, '\u0131')])
print('  fold_case(FULLCASE|IGNORECASE, 0xDF)   ->', [hex(ord(c)) for c in _regex.fold_case(FULL, '\u00df')])
expand = _regex.get_expand_on_folding()
print('  0x130 in get_expand_on_folding():', '\u0130' in expand)
print('  0x131 in get_expand_on_folding():', '\u0131' in expand)
print('  expand_on_folding entries whose fold_case does NOT expand:')
for c in expand:
    if len(_regex.fold_case(FULL, c)) <= 1:
        print(f'    U+{ord(c):04X} folds to {[hex(ord(x)) for x in _regex.fold_case(FULL, c)]}')

print()
print('=== PCRE2 (PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS) ===')
lib, loaded_from = load_pcre2()
UTF, UCP, CASELESS, ANCHORED, ENDANCHORED = 0x00080000, 0x00020000, 0x00000008, 0x80000000, 0x20000000
NOMATCH = -1
lib.pcre2_compile_8.restype = ctypes.c_void_p
lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32, ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_size_t), ctypes.c_void_p]
lib.pcre2_match_data_create_from_pattern_8.restype = ctypes.c_void_p
lib.pcre2_match_data_create_from_pattern_8.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_match_8.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t, ctypes.c_size_t, ctypes.c_uint32, ctypes.c_void_p, ctypes.c_void_p]
buf = ctypes.create_string_buffer(256)
lib.pcre2_config_8(11, buf)
print(f'  PCRE2 {buf.value.decode()} (loaded from {loaded_from})')


def pcre2_fullmatch(pat, subj):
    err, off = ctypes.c_int(), ctypes.c_size_t()
    p = pat.encode('utf-8')
    code = lib.pcre2_compile_8(p, len(p), UTF | UCP | CASELESS, ctypes.byref(err), ctypes.byref(off), None)
    if not code:
        return f'compile error {err.value}'
    md = lib.pcre2_match_data_create_from_pattern_8(code, None)
    s = subj.encode('utf-8')
    rc = lib.pcre2_match_8(code, s, len(s), 0, ANCHORED | ENDANCHORED, md, None)
    if rc == NOMATCH:
        return 'n'
    return 'Y' if rc > 0 else f'error {rc}'


for pname, pat in SUBJECTS.items():
    for sname, subj in SUBJECTS.items():
        print(f'  pat {pname:18} subj {sname:18} -> {pcre2_fullmatch(pat, subj)}')
