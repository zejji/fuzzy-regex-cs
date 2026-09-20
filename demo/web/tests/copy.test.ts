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
        // The two the chunk-1 fix added, and the two this table went without until the blind pass
        // of 2026-09-20 noticed that the sources most likely to drift were the unguarded ones.
        ['src/lib', 2, 'could not be read'],
        ['DemoEngine.cs', 12, 'The page stays responsive'],
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

    it('keeps a string holding a comment marker', () => {
        // The regex this replaced truncated the literal at the slashes and left the quote open, so
        // the whole string vanished from the linted set rather than being linted.
        const source = "const a = 'read the notes // and the rest';\nconst b = 'a second string';";
        expect(literals(source, 'x').map(({ text }) => text)).toEqual([
            'read the notes // and the rest',
            'a second string',
        ]);
    });

    it('takes the holes out of a C# interpolated string', () => {
        const source = 'error = $"The pattern is longer than the limit of {MaxPatternLength} characters.";';
        expect(literals(source, 'x', 'csharp').map(({ text }) => text)).toEqual([
            'The pattern is longer than the limit of characters.',
        ]);
    });

    it('leaves a script string\'s braces alone, because the page shows them', () => {
        // `{e<=2}` is fuzzy-regex syntax a visitor reads, not an interpolation.
        const source = "const hint = 'Write {e<=2} after the group';";
        expect(literals(source, 'x').map(({ text }) => text)).toEqual(['Write {e<=2} after the group']);
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
