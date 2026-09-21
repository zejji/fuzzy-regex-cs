/**
 * Which fuzzy budgets have no bound, and so let the pattern match almost anywhere.
 *
 * The table comes from the Python `regex` module, not from reading the grammar:
 * `tools/probes/s75-fuzzy-budget.py` and `tools/probes/s75-fuzzy-defaults.py`, run against
 * regex 2026.9.10 on 2026-09-20. The second is the one that settles `{s<=1,e}`: a kind nobody names
 * is zero as soon as any kind IS named, so an unbounded `e` beside `s<=1` allows one error in total
 * and the page must say nothing.
 *
 *   (?:colour){e}        against "zzzzzzzzz"     match (0,6), 6 substitutions
 *   (?:colour){s}        against "zzzzzzzzz"     match (0,6), 6 substitutions
 *   (?:colour){i}        against "czozlzozuzzr"  match (0,12), 6 insertions
 *   (?:colour){d}        against "zzzzzzzzz"     match (0,0), 6 deletions
 *   (?:colour){s<=1,e}   against "zzzzzzzzz"     no match
 *   (?:colour){e<=2}     against "zzzzzzzzz"     no match
 *   (?:colour){1i+1d<3}  against "zzzzzzzzz"     no match
 */
import { describe, expect, it } from 'vitest';

import { budgetNote, unboundedBudget } from '../src/lib/budget';

describe('a budget with no bound is named', () => {
    it.each([
        ['(?:colour){e}', '{e}', ['e']],
        ['(?:colour){s}', '{s}', ['s']],
        ['(?:colour){i}', '{i}', ['i']],
        ['(?:colour){d}', '{d}', ['d']],
        // A test on the errors bounds which characters an edit may touch, not how many there are.
        ['(?:colour){e:[a-z]}', '{e:[a-z]}', ['e']],
        // A character class takes a budget like any other element: `[abc]{e}` matches "zzzzz" at
        // (0, 1) with one substitution (regex 2026.9.10, measured 2026-09-21).
        ['[abc]{e}', '{e}', ['e']],
        // The second spec is the unbounded one, and the page names the one it found.
        ['(?:a){e<=1}(?:b){d}', '{d}', ['d']],
        // Two kinds named and neither bounded, which is every letter that can still run away.
        // Bounding one of them is not enough: `(?:colour){i<=2,d}` matches 19 times in nine
        // characters, the same as `{i,d}` (regex 2026.9.10, measured 2026-09-21).
        ['(?:colour){i,d}', '{i,d}', ['i', 'd']],
        ['(?:colour){s,i,d}', '{s,i,d}', ['s', 'i', 'd']],
        // One of the two bounded, so only the other is named.
        ['(?:colour){s<=1,d}', '{s<=1,d}', ['d']],
        // A cost equation prices the kinds it names and leaves the rest at zero, so a kind named
        // beside it without a bound is the one that runs away: `{d,1i+1s<3}` deletes the whole
        // pattern (`fuzzy_counts=(0, 0, 6)` against the empty string), and `{d<=2,1i+1s<3}` finds
        // nothing in nine characters (regex 2026.9.10, measured 2026-09-21).
        ['(?:colour){d,1i+1s<3}', '{d,1i+1s<3}', ['d']],
        ['(?:colour){s,1i+1d<3}', '{s,1i+1d<3}', ['s']],
        // The same shape with the equation written as a repeated constraint. `{d,d<=1,i}` bounds
        // deletions at one and leaves insertions unlimited: it inserts the six characters of
        // "czozlzozuzzr", and the advice `{d,d<=1,i<=2}` stops at two (regex 2026.9.10,
        // measured 2026-09-21).
        ['(?:colour){d,d<=1,i}', '{d,d<=1,i}', ['i']],
        ['(?:colour){i,i<=1,d}', '{i,i<=1,d}', ['d']],
    ])('%s is unbounded', (pattern, spec, letters) => {
        expect(unboundedBudget(pattern)).toEqual({ spec, letters });
    });
});

describe('a budget that is bounded is left alone', () => {
    it.each([
        '(?:colour){e<=2}',
        '(?:colour){e<2}',
        '(?:colour){1<=e<=3}',
        '(?:colour){s<=1,i<=1,d<=1}',
        '(?:colour){e<=2:[a-z]}',
        // Unbounded in `e`, but naming `s` puts insertions and deletions at zero, so the total is
        // the one substitution `s<=1` allows. Measured, not reasoned about: see the header.
        '(?:colour){s<=1,e}',
        // A cost equation prices every kind it names at one or more, so what each can reach is
        // bounded by what the equation affords, and every kind it does not name is zero.
        '(?:colour){1i+1d<3}',
        '(?:colour){2i+2d+1s<=4}',
        '(?:colour){d<=1,1i+1s<3}',
        // A kind priced at nothing IS unbounded - `{0d+1i<3}` deletes freely - and the page still
        // says nothing, because the advice it would give is the advice it cannot write: `d<=2`
        // beside a price for `d` is a re-use of the constraint, and re-pricing somebody's equation
        // is a rewrite rather than a bound. Silence is the safe way to be wrong here.
        '(?:colour){0d+1i<3}',
        '(?:colour){0i+1d<3}',
        // A kind can carry a price AND a constraint, and the price binds it either way round:
        // `{i}` alone inserts six characters into "czozlzozuzzr" and neither of these matches it
        // at all (regex 2026.9.10, measured 2026-09-21).
        '(?:colour){i,1i+1d<3}',
        '(?:colour){1i+1d<3,i}',
        '(?:colour){d,1d+1i<3}',
        '(?:colour){s,1i+1s<3}',
        // A kind constrained twice is that same pair written the short way: upstream reads the
        // second item as an equation, so `{s,s<=1}` allows one substitution and nothing else - it
        // matches "colouu" with counts (1, 0, 0) and not "colzuu" (regex 2026.9.10, 2026-09-21).
        '(?:colour){s,s<=1}',
    ])('%s says nothing', (pattern) => {
        expect(unboundedBudget(pattern)).toBeNull();
    });
});

describe('the line under the pattern', () => {
    it.each([
        ['{e}', 'e', '{e} allows any number of errors', 'Write {e<=2} to allow at most two.'],
        ['{s}', 's', '{s} allows any number of substitutions', 'Write {s<=2} to allow at most two.'],
        ['{i}', 'i', '{i} allows any number of insertions', 'Write {i<=2} to allow at most two.'],
        ['{d}', 'd', '{d} allows any number of deletions', 'Write {d<=2} to allow at most two.'],
    ])('%s names what it allows and how to bound it', (spec, letter, opening, advice) => {
        const note = budgetNote({ spec, letters: [letter as 'e' | 's' | 'i' | 'd'] });
        expect(note).toContain(opening);
        expect(note).toContain(advice);
    });

    it('bounds every kind that has no bound, because bounding one leaves the rest running', () => {
        // `{i<=2,d}` matches as widely as `{i,d}` does, so advice that stopped at the first letter
        // would be advice that does not work: 19 matches in nine characters either way.
        const note = budgetNote({ spec: '{i,d}', letters: ['i', 'd'] });
        expect(note).toContain('{i,d} allows any number of insertions or deletions');
        expect(note).toContain('Write {i<=2,d<=2} to allow at most two of each.');
    });

    it('names three kinds as a list', () => {
        expect(budgetNote({ spec: '{s,i,d}', letters: ['s', 'i', 'd'] })).toContain(
            'any number of substitutions, insertions or deletions',
        );
    });

    it('keeps the test on which characters an edit may touch', () => {
        // `{e<=2}` would be different advice from `{e<=2:[a-z]}`: it drops the restriction the
        // pattern already has, which is not what the reader asked for.
        expect(budgetNote({ spec: '{e:[a-z]}', letters: ['e'] })).toContain(
            'Write {e<=2:[a-z]} to allow at most two.',
        );
    });
});

describe('what is not a fuzzy budget at all', () => {
    it.each([
        // A repeat, which is the other thing braces mean.
        'colou{2}r',
        'colou{2,}r',
        'colou{2,3}r',
        'colou?r',
        // Braces as characters: escaped, inside a class, and the whole pattern.
        String.raw`colour\{e\}`,
        '[{e}]',
        '{e}',
        // An empty pattern, and one that is only a group.
        '',
        '(?:colour)',
    ])('%s says nothing', (pattern) => {
        expect(unboundedBudget(pattern)).toBeNull();
    });

    it('reads a class that holds a closing bracket as the class it is', () => {
        // `[]}]` is a class holding `]` and `}`, because a `]` first in a class is that character.
        // Read as a class that ends at the first `]`, the rest of the pattern is scanned one
        // character out and the `{e}` inside it would be reported.
        expect(unboundedBudget('[]{e}]x')).toBeNull();
    });

    it('does not read a budget out of a pattern the engine would refuse', () => {
        // A kind constrained twice is not an error. Upstream re-reads the second item as a cost
        // equation, and `e` is not a kind an equation can price, so the budget fails altogether and
        // the braces are text: `(?:colour){e<=1,e}` matches the literal "colour{e<=1,e}" and
        // nothing else. Same for `{s<=1,s}`, where the second `s` has no `<=` to make an equation
        // of. (regex 2026.9.10, measured 2026-09-21.)
        expect(unboundedBudget('(?:colour){e<=1,e}')).toBeNull();
        expect(unboundedBudget('(?:colour){e,e<=1}')).toBeNull();
        expect(unboundedBudget('(?:colour){s<=1,s}')).toBeNull();
    });

    it('says nothing about a budget with nothing to apply to', () => {
        // `({e})` and `x|{e}` are the compile error "nothing for fuzzy constraint" upstream
        // (regex 2026.9.10, measured 2026-09-21), so there is no pattern to advise on.
        expect(unboundedBudget('({e})')).toBeNull();
        expect(unboundedBudget('(|{e})')).toBeNull();
        expect(unboundedBudget('x|{e}')).toBeNull();
    });

    it('reads a comment as the text it is', () => {
        // `(?#...)` is a comment, so `(?#{e})colour` has no fuzziness at all: upstream matches
        // "colour" exactly and finds nothing in "czozlzozuzzr" (regex 2026.9.10, 2026-09-21).
        expect(unboundedBudget('(?#{e})colour')).toBeNull();
        // A backslash takes the next character into the comment with it, so an escaped `)` does
        // not close it: `(?#\)x{e})y` matches "zzzzzyzzzzz" at (5, 6) with no errors, where
        // `(?#a)b)colour` ends at its `)` and is "unbalanced parenthesis at position 6".
        expect(unboundedBudget(String.raw`(?#\)x{e})y`)).toBeNull();
        // A comment nobody closed is a pattern upstream refuses: `(?#a{e}` is "missing ) at
        // position 7" and `(?#x` is the same error at position 4.
        expect(unboundedBudget('(?#a{e}')).toBeNull();
        expect(unboundedBudget('(?#x')).toBeNull();
    });

    it('sees through a comment to what the budget would apply to', () => {
        // A comment is not something a budget can apply to, so `(?#c){e}` is the compile error
        // "nothing for fuzzy constraint at position 5", exactly as `{e}` on its own is
        // (regex 2026.9.10, 2026-09-21).
        expect(unboundedBudget('(?#c){e}')).toBeNull();
        expect(unboundedBudget('((?#c){e})')).toBeNull();
        expect(unboundedBudget('(?#a)(?#b){e}')).toBeNull();
    });
});

describe('an escaped character takes a budget like any other', () => {
    // `\(` is the character `(`, not a group, so the budget after it has something to apply to:
    // `\({e}` matches "zzzzzzzzzzzz" with eleven insertions and one substitution, where `({e})` is
    // the compile error "nothing for fuzzy constraint" (regex 2026.9.10, measured 2026-09-21).
    it.each([
        [String.raw`\({e}`, '{e}'],
        [String.raw`\|{e}`, '{e}'],
    ])('%s is unbounded', (pattern, spec) => {
        expect(unboundedBudget(pattern)).toEqual({ spec, letters: ['e'] });
    });

    it('leaves a bounded one alone', () => {
        expect(unboundedBudget(String.raw`\({e<=2}`)).toBeNull();
    });
});
