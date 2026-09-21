/**
 * Finding a fuzzy budget that has no bound, so the page can say so under the pattern.
 *
 * `(?:colour){e}` allows any number of errors. Every position in the subject then matches something,
 * the page fills with markers, and a visitor who meant `{e<=1}` reads that as a broken engine (S75,
 * item 1). One line under the pattern says what the budget allows and how to bound it.
 *
 * The grammar is upstream's, `parse_fuzzy` in `regex/_regex_core.py` (lines 655 to 800), and the
 * defaults are `Fuzzy.__init__` (line 2786): a kind nobody names is unlimited while no kind is
 * named, and zero as soon as one is. That is why `{s<=1,e}` is bounded even though its `e` has no
 * bound - naming `s` puts insertions and deletions at zero, so one substitution is the whole budget.
 * Measured against regex 2026.9.10 on 2026-09-20 by `tools/probes/s75-fuzzy-defaults.py`, and the
 * table is quoted in `budget.test.ts`.
 *
 * Reading the pattern text, rather than asking the engine, because the engine does not expose a
 * compiled pattern's constraints and the answer is wanted while the pattern is being typed.
 */

/** The four letters a fuzzy budget is written with: total errors, substitutions, insertions, deletions. */
export type BudgetLetter = 'e' | 's' | 'i' | 'd';

/** A fuzzy budget in a pattern that allows any number of errors of at least one kind. */
export interface UnboundedBudget {
    /** The budget exactly as the pattern writes it, braces included. */
    readonly spec: string;
    /** Every letter that has no bound, in the order the budget writes them. */
    readonly letters: readonly BudgetLetter[];
}

/** What each letter counts, in the words the line uses. */
const KINDS: Record<BudgetLetter, string> = {
    e: 'errors',
    s: 'substitutions',
    i: 'insertions',
    d: 'deletions',
};

/**
 * The line the page puts under the pattern for a budget with no bound.
 *
 * Two is the bound it suggests because any number would do and a reader needs one to edit, not a
 * rule to derive. The copy linter reads these sentences through `tests/copy-sources.ts`.
 */
export function budgetNote(budget: UnboundedBudget): string {
    // Every unbounded kind, because bounding one of two leaves the other running: `{i<=2,d}`
    // matches as widely as `{i,d}` does (19 matches in nine characters, regex 2026.9.10,
    // 2026-09-21). The bound goes into the budget the pattern already has, so a test on which
    // characters an edit may touch - `{e:[a-z]}` - keeps its test: dropping it would be different
    // advice. Each letter is replaced at its first occurrence, which is that letter's own item: a
    // letter a cost equation prices is bounded and so is never one of these, and the items of a
    // budget all come before the `:` that introduces the test. Without the first of those,
    // `{1i+1d<3,i}` would be advised as `{1i<=2+1d<3,i}`, which upstream refuses to compile
    // ("expected } at position 16", regex 2026.9.10, 2026-09-21).
    let bounded = budget.spec;
    for (const letter of budget.letters) bounded = bounded.replace(letter, `${letter}<=2`);

    const kinds = budget.letters.map((letter) => KINDS[letter]);
    const allows = kinds.length === 1 ? kinds[0] : `${kinds.slice(0, -1).join(', ')} or ${kinds[kinds.length - 1]}`;
    const each = kinds.length === 1 ? '' : ' of each';

    return (
        `${budget.spec} allows any number of ${allows}, so a match can be any distance ` +
        `from the pattern. Write ${bounded} to allow at most two${each}.`
    );
}

/** One `letter` or `count <= letter <= count` item, and whether it carried a maximum. */
interface Item {
    readonly letter: BudgetLetter;
    readonly bounded: boolean;
}

const LETTERS = 'esid';
const DIGITS = '0123456789';

/**
 * The first fuzzy budget in `pattern` that allows any number of errors, or null if there is none.
 *
 * Null is also the answer for a budget this cannot read. A page that says nothing is a page that is
 * merely unhelpful; a page that names a bound the engine is not applying is a page that is wrong.
 *
 * `pattern` is a pattern the engine accepted: `App.vue` asks only when the answer on screen parsed,
 * and a `#budget-note` is never drawn beside a parse error (`page.test.ts`). The guards below turn
 * away the shapes a budget cannot attach to that are cheap to spot - the start of the pattern, an
 * unescaped `(` or `|`, a comment - but they do not cover every one of them. `(?:{e})`, `(?i){e}`
 * and `a*{e}` are all "nothing for fuzzy constraint" upstream and this still reads `{e}` out of
 * them (regex 2026.9.10, 2026-09-21). Telling those apart needs a parser for group headers and
 * quantifiers, which is the engine's job, and the caller has already asked it.
 */
export function unboundedBudget(pattern: string): UnboundedBudget | null {
    let inClass = false;
    // Whether a budget written here would have nothing to apply to. True at the start of the
    // pattern and after an unescaped `(` or `|`: `({e})` and `x|{e}` are the compile error
    // "nothing for fuzzy constraint", so there is no pattern to advise on. An escaped `\(` is a
    // character like any other and does take a budget - `\({e}` matches "zzzzzzzzzzzz" with eleven
    // insertions and a substitution (regex 2026.9.10, 2026-09-21).
    let nothingPrecedes = true;
    for (let at = 0; at < pattern.length; at += 1) {
        const character = pattern[at];

        if (character === '\\') {
            at += 1;
            nothingPrecedes = false;
            continue;
        }

        if (inClass) {
            // `]` first in a class is that character and not the end of it: `[]{e}]` is a class of
            // four characters (measured, `tools/probes/s75-fuzzy-budget.py`).
            if (character === ']') {
                inClass = false;
                nothingPrecedes = false;
            }
            continue;
        }

        if (character === '[') {
            inClass = true;
            if (pattern[at + 1] === '^') at += 1;
            if (pattern[at + 1] === ']') at += 1;
            continue;
        }

        // A `(?#...)` comment is not pattern, so a budget written inside one is text: upstream
        // matches `(?#{e})colour` exactly and finds nothing in "czozlzozuzzr". It is not something
        // a budget can apply to either, which is why `nothingPrecedes` is carried across it
        // unchanged: `(?#c){e}` is the compile error "nothing for fuzzy constraint at position 5"
        // (regex 2026.9.10, 2026-09-21).
        if (character === '(' && pattern.startsWith('?#', at + 1)) {
            const close = endOfComment(pattern, at + 2);
            if (close === null) return null;
            at = close;
            continue;
        }

        if (character === '(' || character === '|') {
            nothingPrecedes = true;
            continue;
        }

        if (character !== '{' || nothingPrecedes) {
            nothingPrecedes = false;
            continue;
        }

        nothingPrecedes = false;
        const parsed = parseFuzzy(pattern, at);
        if (parsed === null) continue;

        const letters = unboundedLetters(parsed.items, parsed.free);
        if (letters.length > 0) return { spec: pattern.slice(at, parsed.end), letters };

        at = parsed.end - 1;
    }

    return null;
}

/**
 * Which letters of a budget allow any number of errors, empty when none does.
 *
 * `Fuzzy.__init__`, upstream line 2796 onwards, in the order it applies its defaults.
 */
function unboundedLetters(items: readonly Item[], free: boolean): BudgetLetter[] {
    // A kind a cost equation prices at nothing is unbounded - `{0d+1i<3}` deletes freely, measured
    // 2026-09-21 - and the page says nothing about it anyway, because the advice would have to
    // re-price somebody's equation rather than add a bound to it. Every other kind the equation
    // names costs at least one, so what it can reach is bounded by what the equation affords, and
    // those kinds arrive here as bounded items.
    if (free) return [];

    const named = items.filter((item) => item.letter !== 'e');
    const total = items.find((item) => item.letter === 'e');
    if (total !== undefined && total.bounded) return [];

    // No kind named: each of the three defaults to unlimited, and `e` has no bound either.
    if (named.length === 0) return total === undefined ? [] : ['e'];

    // The kinds named without a bound. Every kind nobody named is zero, so these are the only ways
    // what is left can run away, and all of them have to be bounded for the pattern to be.
    //
    // A kind can be named twice: once as a constraint and once with a price. The price wins,
    // because it binds whatever the constraint says - `(?:colour){i,1i+1d<3}` and `{1i+1d<3,i}`
    // both fail to insert the six characters that `{i}` alone inserts (measured, regex 2026.9.10,
    // 2026-09-21). A kind constrained twice is read the same way, since upstream answers the
    // repeat by re-reading the item as an equation: see `parseItem`.
    const bounded = new Set(named.filter((item) => item.bounded).map((item) => item.letter));
    return named
        .filter((item) => !item.bounded && !bounded.has(item.letter))
        .map((item) => item.letter);
}

/**
 * Reads one fuzzy budget starting at the `{` at `from`, or null if what is there is not one.
 *
 * Null covers a repeat (`{2,3}`), a literal brace, and anything the grammar refuses - upstream
 * restores its position and reads the braces as text in exactly those cases, so a pattern this
 * returns null for is a pattern with no budget at that brace.
 */
function parseFuzzy(pattern: string, from: number): { items: Item[]; free: boolean; end: number } | null {
    let at = from + 1;
    const items: Item[] = [];
    let equations = 0;
    let free = false;
    const seen = new Set<string>();

    for (;;) {
        const item = parseItem(pattern, at, seen);
        if (item === null) return null;

        if (item.constraint !== null) {
            seen.add(item.constraint.letter);
            items.push(item.constraint);
        } else {
            if (equations > 0) return null;
            equations += 1;
            // A kind the equation prices is bounded by the equation, so it joins the items as
            // one; a kind priced at nothing is the case the page keeps quiet about.
            items.push(...item.priced);
            free = item.priced.some((kind) => !kind.bounded);
        }

        at = item.end;
        if (pattern[at] !== ',') break;
        at += 1;
    }

    // `:` introduces a test - which characters an edit may touch - and the test is a pattern of its
    // own. Skipped to the closing brace rather than parsed: what is wanted here is the bound.
    if (pattern[at] === ':') {
        const end = endOfTest(pattern, at + 1);
        if (end === null) return null;
        at = end;
    }

    if (pattern[at] !== '}') return null;
    return { items, free, end: at + 1 };
}

/**
 * One item: a constraint on a kind, or a cost equation and the kinds it prices.
 *
 * `seen` holds the kinds already constrained. Upstream's `parse_constraint` (line 762) raises
 * ParseError for a kind constrained twice, and `parse_fuzzy_item` (line 679) answers that by
 * re-reading the item as a cost equation, so `{s,s<=1}` is a constraint and then an equation that
 * prices substitutions at one - it allows one substitution and no other error (measured against
 * regex 2026.9.10, 2026-09-21). When the second reading fails too, `{e<=1,e}` and `{s<=1,s}` among
 * them, the whole budget is not a budget and upstream reads the braces as text: `{e<=1,e}` matches
 * the literal "colour{e<=1,e}".
 */
function parseItem(
    pattern: string,
    from: number,
    seen: ReadonlySet<string>,
): { constraint: Item | null; priced: Item[]; end: number } | null {
    const constraint = parseConstraint(pattern, from);
    if (constraint !== null && !seen.has(constraint.item.letter)) {
        return { constraint: constraint.item, priced: [], end: constraint.end };
    }

    const equation = parseCostEquation(pattern, from);
    return equation === null ? null : { constraint: null, priced: equation.priced, end: equation.end };
}

/** `letter [("<=" | "<") count]`, or `count ("<=" | "<") letter ("<=" | "<") count`. */
function parseConstraint(pattern: string, from: number): { item: Item; end: number } | null {
    const character = pattern[from] ?? '';

    if (LETTERS.includes(character)) {
        const compare = parseCompare(pattern, from + 1);
        if (compare === null) return { item: { letter: character as BudgetLetter, bounded: false }, end: from + 1 };

        const count = parseCount(pattern, compare);
        if (count === null) return null;
        return { item: { letter: character as BudgetLetter, bounded: true }, end: count };
    }

    if (!DIGITS.includes(character)) return null;

    const min = parseCount(pattern, from);
    if (min === null) return null;
    const afterMin = parseCompare(pattern, min);
    if (afterMin === null) return null;

    const letter = pattern[afterMin] ?? '';
    if (!LETTERS.includes(letter)) return null;

    const afterLetter = parseCompare(pattern, afterMin + 1);
    if (afterLetter === null) return null;
    const max = parseCount(pattern, afterLetter);
    if (max === null) return null;

    return { item: { letter: letter as BudgetLetter, bounded: true }, end: max };
}

/**
 * `count? kind ("+" count? kind)* ("<=" | "<") count`, where a kind is one of `d`, `i`, `s`.
 *
 * The kinds come back with the cost read: a kind that costs something is bounded by what the
 * equation affords, and a kind priced at zero - `{0d+1i<3}` - buys as many errors as it likes. An
 * absent count is one, as upstream's `parse_fuzzy_item` reads it.
 */
function parseCostEquation(pattern: string, from: number): { priced: Item[]; end: number } | null {
    let at = from;
    const priced: Item[] = [];
    for (;;) {
        const cost = at;
        while (DIGITS.includes(pattern[at] ?? '')) at += 1;
        const letter = pattern[at] ?? '';
        if (!'dis'.includes(letter)) return null;
        priced.push({
            letter: letter as BudgetLetter,
            bounded: at === cost || Number(pattern.slice(cost, at)) >= 1,
        });
        at += 1;
        if (pattern[at] !== '+') break;
        at += 1;
    }

    const compare = parseCompare(pattern, at);
    if (compare === null) return null;
    const max = parseCount(pattern, compare);
    return max === null ? null : { priced, end: max };
}

/** Where a `<=` or `<` ends, or null when there is neither. */
function parseCompare(pattern: string, from: number): number | null {
    if (pattern.startsWith('<=', from)) return from + 2;
    if (pattern[from] === '<') return from + 1;
    return null;
}

/** Where a run of digits ends, or null when there is not one. */
function parseCount(pattern: string, from: number): number | null {
    let at = from;
    while (DIGITS.includes(pattern[at] ?? '')) at += 1;
    return at === from ? null : at;
}

/**
 * Where a `(?#...)` comment's closing `)` sits, or null when it has none.
 *
 * `parse_comment`, upstream line 978, takes the character after a backslash with it, so an escaped
 * `)` stays inside the comment: `(?#\)zzz)a` matches "a" and `(?#\)x{e})y` has no fuzziness at all.
 * A comment nobody closed is a pattern upstream refuses - `(?#x` is "missing ) at position 4" - and
 * a pattern that does not compile has no budget to name (regex 2026.9.10, 2026-09-21).
 */
function endOfComment(pattern: string, from: number): number | null {
    for (let at = from; at < pattern.length; at += 1) {
        if (pattern[at] === '\\') {
            at += 1;
            continue;
        }
        if (pattern[at] === ')') return at;
    }
    return null;
}

/** Where the test after `:` ends: the closing brace, with escapes and classes stepped over. */
function endOfTest(pattern: string, from: number): number | null {
    let inClass = false;
    for (let at = from; at < pattern.length; at += 1) {
        const character = pattern[at];
        if (character === '\\') {
            at += 1;
            continue;
        }
        if (inClass) {
            if (character === ']') inClass = false;
            continue;
        }
        if (character === '[') {
            inClass = true;
            if (pattern[at + 1] === '^') at += 1;
            if (pattern[at + 1] === ']') at += 1;
            continue;
        }
        if (character === '}') return at;
    }
    return null;
}
