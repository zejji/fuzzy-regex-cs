"""D11 chunk 11: the port's grid of design section 8.1. The model's two generators
(d11-direction-model.py's random grid and its shaped templates), extended to what the model does not
parse: (?w), \\X, (?a), (?p), (?b), (?e), the fuzzy forms {i<=1} and {e<=1}, a Hebrew letter, '"', ','
and the subjects 'a:\\u0301', 'e\\u0301', '\\u05D0"'; plus fuzzy sections inside lookaround bodies in
the shape of F1, group calls and recursion inside bodies, and the model's semantics rows. Patterns
the port refuses to compile are dropped by the grid itself (an ERR row).

Run d11-port-judge-grid.cs on the output in Debug (MAXW=2): the judge is the port's own non-partial
call over every continuation of up to two characters.

    python tools/probes/d11-port-grid.py out.jsonl SEED [N]
"""
import importlib.util
import json
import os
import random
import sys

spec = importlib.util.spec_from_file_location(
    "model", os.path.join(os.path.dirname(os.path.abspath(__file__)), "d11-direction-model.py")
)
model = importlib.util.module_from_spec(spec)
spec.loader.exec_module(model)

model.ATOMS += [r"\X", "\u05d0", '"', ",", ":"]
model.QUANTS += ["{e<=1}", "{i<=1}"]
FLAGS = ["", "", "", "(?r)", "(?m)", "(?w)", "(?wr)", "(?a)", "(?p)", "(?b)", "(?e)", "(?wa)"]
SUBJ_ALPHA = ["a", "b", " ", "\n", ":", "\u0301", "\u05d0", '"', ","]
FIXED_SUBJECTS = ["a:\u0301", "e\u0301", '\u05d0"']
OPS = ["match", "search", "fullmatch"]

# Fuzzy sections in lookaround bodies in the shape of F1 (design 8.1), over the shaped pieces.
FUZZY_SHAPES = ["(?:(?={0}{{e<=1}}){1}){{e<=1}}", "(?:(?!{0}{{e<=1}}){1}){{e<=1}}", "(?:(?={0}){1}){{i<=1}}"]
# Group calls and recursion inside bodies, including a body run-out inside a called group.
CALL_SHAPES = [
    "({0}(?=(?1)?{1}){2})", "(?:({0})(?={1}(?1)){2})", "({0}(?!(?1){1})|{2})", "(?=(?R)?{0}){1}",
    "({0}(?={1}(?1)?)b?)", "(?(DEFINE)(?<g>{0}{1}))(?=(?&g)){2}",
]
# The model's semantics rows named in design 8.0, judged on the port like any other row.
SEMANTICS_ROWS = [
    ("match", r"(?m)(?<=$(*SKIP)(?>a\b|)|[ab]*)\b", "ab"),
    ("match", r"(?m)(?<=$(*SKIP)(?>a\b|)|[ab]*)\b", "a "),
    ("search", r"(?rm)\n(?:((*SKIP).)|(*FAIL)((*PRUNE)(*FAIL))).", "a  "),
]


def subject(rng):
    if rng.random() < 0.15:
        return rng.choice(FIXED_SUBJECTS)
    return "".join(rng.choice(SUBJ_ALPHA) for _ in range(rng.randint(0, 3)))


def main():
    out, seed = sys.argv[1], int(sys.argv[2])
    n = int(sys.argv[3]) if len(sys.argv) > 3 else 20000
    rng = random.Random(seed)
    rows = set()
    # Random grid.
    while len(rows) < n:
        ctx = {"groups": 0}
        pattern = rng.choice(FLAGS) + model.gen_pattern(rng, 2, ctx)
        for _ in range(3):
            rows.add((rng.choice(OPS), pattern, subject(rng)))
    # Shaped grid.
    target = 2 * n
    while len(rows) < target:
        r = rng.random()
        fill = [rng.choice(model.PIECES) for _ in range(5)]
        if r < 0.7:
            shape = rng.choice(model.SHAPES)
            pattern = shape.format(
                *fill,
                v=rng.choice(["PRUNE", "SKIP"]),
                la=rng.choice(["=", "!", "<=", "<!"]),
                la2=rng.choice(["=", "!"]),
                q=rng.choice(["*", "+", "?", "*+", "{e<=1}"]),
            )
        elif r < 0.85:
            pattern = rng.choice(FUZZY_SHAPES).format(*fill)
        else:
            pattern = rng.choice(CALL_SHAPES).format(*fill)
        pattern = rng.choice(FLAGS) + pattern
        for _ in range(3):
            rows.add((rng.choice(OPS), pattern, rng.choice(model.SHAPE_SUBJECTS + FIXED_SUBJECTS)))
    rows.update(SEMANTICS_ROWS)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for op, p, s in sorted(rows):
            f.write(json.dumps({"pattern": p, "subject": s, "operation": op}) + "\n")
    print(len(rows))


if __name__ == "__main__":
    main()
