"""Grid rows for D52 (a conditional's lookaround test is matched fuzzily) and D54 (captures of a
failed negative test are dropped). Each row also carries `posForm`: the same pattern with every
negative conditional written as the positive one with its branches swapped, which by the D54 ruling
must answer the same."""
import json, random, sys

random.seed(int(sys.argv[2]) if len(sys.argv) > 2 else 52)
N = int(sys.argv[1]) if len(sys.argv) > 1 else 6000

TESTS = ["a", "(a)", "ab", "(a)b", "(a|b)", "a|b", "(a*)", "[ab]", "(?:(a)|b)", "(a)(b)?", "(ab|a)"]
BRANCH = ["", "a", "b", "ab", "x", "(b)", "(?:a|ab)", "a*", r"\1b", r"\1", r"(?:\1|b)"]
KINDS = [("?=", "?!"), ("?<=", "?<!")]
WRAP = [
    "{C}", "{C}", "(?:{C}){{e<=1}}", "(?:{C}){{s<=1}}", "(?:{C}){{i<=1}}", "(?:{C}){{d<=1}}",
    "(?:{C}){{e<=2}}", "(?:{C})*", "x?(?:{C})+b?", "(?:{C}){{e<=1}}b", "(?:{C}|b){{e<=1}}",
    "((?:{C}){{e<=1}})", "(?:{C})*?", "b?(?:{C})",
]
FLAGS = ["", "", "", "(?b)", "(?e)", "(?r)"]
OPS = ["search", "search", "match", "fullmatch", "finditer"]


def cond(test, kind, yes, no):
    # kind: (positive, negative) pair, neg flag picks one
    return f"({test[0]}{test[1]})"


def build():
    t = random.choice(TESTS)
    look = random.choice(KINDS)
    neg = random.random() < 0.6
    y, n = random.choice(BRANCH), random.choice(BRANCH)
    has_group = "(" in t.replace("(?:", "")
    if not has_group:
        y, n = (b if "\\1" not in b else "b" for b in (y, n))
    op = look[1] if neg else look[0]
    c = f"(?({op}{t}){y}|{n})"
    # the positive twin of a negative conditional, branches swapped
    pc = f"(?({look[0]}{t}){n}|{y})" if neg else c
    w = random.choice(WRAP)
    f = random.choice(FLAGS)
    pre = random.choice(["", "", "x", "(b)?"]) if not has_group else ""
    pat = f + pre + w.format(C=c)
    pos = f + pre + w.format(C=pc)
    subj = "".join(random.choice("abx") for _ in range(random.randint(0, 4)))
    return dict(pattern=pat, posForm=pos, flags=0, subject=subj, operation=random.choice(OPS),
                partial=random.random() < 0.1, neg=neg)


WITNESS = [
    dict(pattern="(?:(?(?=a)ab|b)){s<=1}", posForm="(?:(?(?=a)ab|b)){s<=1}", subject="b"),
    dict(pattern="(?(?!(a))x|\\1b)", posForm="(?(?=(a))\\1b|x)", subject="ab"),
    dict(pattern="(?:(?(?<![ab])baab|ab)){1<=e<=2}", posForm="(?:(?(?<=[ab])ab|baab)){1<=e<=2}", subject="a baax"),
]
rows = [dict(w, flags=0, operation="search", partial=False, neg=True) for w in WITNESS]
seen = set()
while len(rows) < N:
    r = build()
    k = (r["pattern"], r["subject"], r["operation"], r["partial"])
    if k in seen:
        continue
    seen.add(k)
    rows.append(r)
for i, r in enumerate(rows):
    r["id"] = i
    print(json.dumps(r))
