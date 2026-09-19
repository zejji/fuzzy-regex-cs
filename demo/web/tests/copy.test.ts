/**
 * The copy linter: every user-facing string in the demo, held to `COPY_RULES`.
 *
 * The strings are read out of the files that hold them (`copy-sources.ts`) rather than listed here,
 * so a string added tomorrow is linted without anybody remembering to add it. The risk in that is a
 * linter that passes because its extraction found nothing, so each source has a guard test naming a
 * string it must contain and a floor for how many it must find.
 */
import { describe, expect, it } from 'vitest';

import { COPY_RULES, violations } from './copy-rules';
import { literals, SOURCES, templateStrings } from './copy-sources';

describe('every user-facing string', () => {
    for (const [source, copy] of Object.entries(SOURCES)) {
        it(`in ${source} keeps to the copy rules`, () => {
            const broken = copy.flatMap(({ where, text }) =>
                violations(text).map((rule) => `${where}: [${rule.id}] ${rule.why}\n    "${text}"`),
            );
            expect(broken, `${broken.length} string(s) break a copy rule`).toEqual([]);
        });
    }
});

// --- the guards, so a silent extraction cannot pass -------------------------------------------

describe('the linter sees the strings it was written for', () => {
    it.each([
        ['examples.json', 30, 'Up to one error'],
        ['App.vue template', 15, 'Pattern'],
        ['App.vue script', 2, 'Find every match'],
        ['demo.ts', 2, 'Stopped'],
        ['index.html', 3, 'FuzzyRegex'],
        ['help generator', 1, 'GENERATED'],
    ])('finds at least %s strings in %s, including one saying %s', (source, floor, anchor) => {
        const copy = SOURCES[source as string] ?? [];
        expect(copy.length).toBeGreaterThanOrEqual(floor as number);
        expect(copy.some(({ text }) => text.includes(anchor as string))).toBe(true);
    });

    it('takes the comments out before linting a script', () => {
        const source = "// A robust, powerful, intuitive comment\nconst label = 'Find every match';";
        expect(literals(source, 'x').map(({ text }) => text)).toEqual(['Find every match']);
    });

    it('takes the interpolations out of a template literal', () => {
        const source = 'const label = `match ${i + 1}, ${empty ? "none" : text}`;';
        expect(literals(source, 'x').map(({ text }) => text)).toEqual(['match ,']);
    });

    it('takes the comments and interpolations out of a template', () => {
        const source = '<template><p><!-- leverage this --> Matches {{ count }} here</p></template>';
        expect(templateStrings(source, 'x').map(({ text }) => text)).toEqual(['Matches here']);
    });
});

describe('the rules themselves', () => {
    it('rejects the planted sentence and names three rules', () => {
        const planted = 'It seamlessly leverages our robust, powerful, intuitive engine.';
        expect(violations(planted).map((rule) => rule.id)).toEqual(
            expect.arrayContaining(['triad', 'puffery', 'corporate']),
        );
    });

    it.each([
        ['not a demonstration, but a starting point', 'negative-parallelism'],
        ["Upstream's language, not .NET's", 'says-what-it-is-not'],
        ['What this means is the budget was spent', 'rhetorical'],
        ['Why a budget? Because the subject is not exact.', 'rhetorical'],
        ['The engine delivers the answer', 'corporate'],
        ['This ensures the worker stays alive', 'llm-vocabulary'],
        ['The worker answers, allowing you to keep typing', 'trailing-ing'],
        ['The engine stops — the page does not', 'em-dash'],
    ])('rejects %s under %s', (text, rule) => {
        expect(violations(text as string).map((broken) => broken.id)).toContain(rule);
    });

    it.each([
        'Up to 5,000 characters. Currently 42.',
        'The engine stopped early at its own cap. This is part of the answer.',
        'Pattern, flags and subject go in the address bar.',
        'A repeated group keeps every capture.',
        // A nullable type carries a `?` that no reader hears as a question.
        'Run takes a TimeSpan? timeout and returns one answer.',
    ])('leaves %s alone', (text) => {
        expect(violations(text).map((rule) => rule.id)).toEqual([]);
    });

    it('every rule carries a reason', () => {
        for (const rule of COPY_RULES) expect(rule.why.length).toBeGreaterThan(20);
    });
});
