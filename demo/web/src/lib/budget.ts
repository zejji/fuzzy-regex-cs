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

/** A fuzzy budget in a pattern that allows any number of errors of one kind. */
export interface UnboundedBudget {
    /** The budget exactly as the pattern writes it, braces included. */
    readonly spec: string;
    /** The letter that has no bound. */
    readonly letter: BudgetLetter;
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
    // The bound goes into the budget the pattern already has, so a test on which characters an
    // edit may touch - `{e:[a-z]}` - keeps its test: dropping it would be different advice.
    const bounded = budget.spec.replace(budget.letter, `${budget.letter}<=2`);
    return (
        `${budget.spec} allows any number of ${KINDS[budget.letter]}, so a match can be any distance ` +
        `from the pattern. Write ${bounded} to allow at most two.`
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
 */
export function unboundedBudget(pattern: string): UnboundedBudget | null {
    let inClass = false;
    for (let at = 0; at < pattern.length; at += 1) {
        const character = pattern[at];

        if (character === '\\') {
            at += 1;
            continue;
        }

        if (inClass) {
            // `]` first in a class is that character and not the end of it: `[]{e}]` is a class of
            // four characters (measured, `tools/probes/s75-fuzzy-budget.py`).
            if (character === ']') inClass = false;
            continue;
        }

        if (character === '[') {
            inClass = true;
            if (pattern[at + 1] === '^') at += 1;
            if (pattern[at + 1] === ']') at += 1;
            continue;
        }

        // A budget applies to what precedes it, so a `{` with nothing to apply to is a literal
        // brace, as it is after `(` or `|`.
        if (character !== '{' || at === 0 || '(|'.includes(pattern[at - 1] ?? '')) continue;

        const parsed = parseFuzzy(pattern, at);
        if (parsed === null) continue;

        const letter = unboundedLetter(parsed.items, parsed.hasCostEquation);
        if (letter !== null) return { spec: pattern.slice(at, parsed.end), letter };

        at = parsed.end - 1;
    }

    return null;
}

/**
 * Which letter of a budget allows any number of errors, or null when none does.
 *
 * `Fuzzy.__init__`, upstream line 2796 onwards, in the order it applies its defaults.
 */
function unboundedLetter(items: readonly Item[], hasCostEquation: boolean): BudgetLetter | null {
    // A cost equation carries its own maximum, and every kind it names costs at least one by the
    // time the page sees it, so the count each kind can reach is bounded by what the equation
    // affords. `{0i+1d<3}` - a kind priced at nothing - is the exception, and is not worth the
    // machinery: the page then stays quiet, which is the safe way to be wrong.
    if (hasCostEquation) return null;

    const named = items.filter((item) => item.letter !== 'e');
    const total = items.find((item) => item.letter === 'e');
    if (total !== undefined && total.bounded) return null;

    // No kind named: each of the three defaults to unlimited, and `e` has no bound either.
    if (named.length === 0) return total === undefined ? null : 'e';

    // A kind named without a bound. Every kind nobody named is zero, so this is the only way what
    // is left can run away.
    return named.find((item) => !item.bounded)?.letter ?? null;
}

/**
 * Reads one fuzzy budget starting at the `{` at `from`, or null if what is there is not one.
 *
 * Null covers a repeat (`{2,3}`), a literal brace, and anything the grammar refuses - upstream
 * restores its position and reads the braces as text in exactly those cases, so a pattern this
 * returns null for is a pattern with no budget at that brace.
 */
function parseFuzzy(pattern: string, from: number): { items: Item[]; hasCostEquation: boolean; end: number } | null {
    let at = from + 1;
    const items: Item[] = [];
    let hasCostEquation = false;
    const seen = new Set<string>();

    for (;;) {
        const item = parseItem(pattern, at);
        if (item === null) return null;

        if (item.constraint !== null) {
            // "re-use of fuzzy constraint" upstream, which abandons the fuzzy reading altogether.
            if (seen.has(item.constraint.letter)) return null;
            seen.add(item.constraint.letter);
            items.push(item.constraint);
        } else {
            if (hasCostEquation) return null;
            hasCostEquation = true;
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
    return { items, hasCostEquation, end: at + 1 };
}

/** One item: a constraint on a kind, or a cost equation. */
function parseItem(
    pattern: string,
    from: number,
): { constraint: Item | null; end: number } | null {
    const constraint = parseConstraint(pattern, from);
    if (constraint !== null) return { constraint: constraint.item, end: constraint.end };

    const equation = parseCostEquation(pattern, from);
    return equation === null ? null : { constraint: null, end: equation.end };
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

/** `count? kind ("+" count? kind)* ("<=" | "<") count`, where a kind is one of `d`, `i`, `s`. */
function parseCostEquation(pattern: string, from: number): { end: number } | null {
    let at = from;
    for (;;) {
        while (DIGITS.includes(pattern[at] ?? '')) at += 1;
        if (!'dis'.includes(pattern[at] ?? '')) return null;
        at += 1;
        if (pattern[at] !== '+') break;
        at += 1;
    }

    const compare = parseCompare(pattern, at);
    if (compare === null) return null;
    const max = parseCount(pattern, compare);
    return max === null ? null : { end: max };
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
