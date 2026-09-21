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
import { API_SOURCES, DOC_SOURCES, docParagraphs, literals, SOURCES, templateStrings } from './copy-sources';

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

/**
 * Prose outside the page that breaks a rule and is right as it stands, by the file holding it.
 *
 * Empty, and that is the finding rather than an oversight: the first run over the seven documents
 * reported 43 violations and the first over the public doc comments 18 (S75, 2026-09-20), and all
 * 61 read better rewritten, so every one was fixed in the prose. The list is here for the sentence
 * where that stops being true - a quotation from upstream's documentation, say, which cannot be
 * edited to suit our rules.
 *
 * An entry is the exact text the extraction produces, so a rewrite of the sentence takes its
 * exemption with it; `no allowed text has gone stale` holds each entry against the live files.
 */
const ALLOWED: Record<string, readonly { readonly text: string; readonly why: string }[]> = {};

/** Everything linted outside the page, so the staleness check covers both sets in one place. */
const OFF_PAGE: Record<string, readonly { readonly where: string; readonly text: string }[]> = {
    ...DOC_SOURCES,
    ...API_SOURCES,
};

const lint = (
    copy: readonly { readonly where: string; readonly text: string }[],
    exemptions: readonly { readonly text: string; readonly why: string }[],
): string[] => {
    const allowed = new Set(exemptions.map((entry) => entry.text));
    return copy.flatMap(({ where, text }) =>
        allowed.has(text)
            ? []
            : violations(text).map((rule) => `${where}: [${rule.id}] ${rule.why}\n    "${text}"`),
    );
};

describe('every document a reader of the library reads', () => {
    for (const [source, copy] of Object.entries(DOC_SOURCES)) {
        it(`in ${source} keeps to the same copy rules`, () => {
            const broken = lint(copy, ALLOWED[source] ?? []);
            expect(broken, `${broken.length} paragraph(s) break a copy rule`).toEqual([]);
        });
    }

    it('lets an exempt text through and nothing else', () => {
        // `ALLOWED` is empty, so the two guards below run over nothing and the exemption path is
        // uncovered. This is its coverage: with one entry, the text it names passes and a second
        // text breaking the same rule still fails.
        const copy = [
            { where: 'x:1', text: 'The engine delivers the answer' },
            { where: 'x:2', text: 'The worker delivers the answer' },
        ];
        const broken = lint(copy, [
            { text: 'The engine delivers the answer', why: 'a quotation that cannot be rewritten' },
        ]);

        expect(broken).toHaveLength(1);
        expect(broken[0]).toContain('x:2');
    });

    it('no allowed text has gone stale', () => {
        // An exemption for a sentence nobody can find is an exemption nobody can check. Without
        // this, a rewrite would leave its entry behind and the next violation of the same shape
        // would be allowed through by an entry that means nothing.
        const missing = Object.entries(ALLOWED).flatMap(([source, entries]) =>
            entries
                .filter((entry) => !(OFF_PAGE[source] ?? []).some(({ text }) => text === entry.text))
                .map((entry) => `${source}: "${entry.text}"`),
        );
        expect(missing).toEqual([]);
    });

    it('every allowed text says why', () => {
        for (const entries of Object.values(ALLOWED)) {
            for (const entry of entries) expect(entry.why.length, entry.text).toBeGreaterThan(20);
        }
    });
});

/**
 * The XML doc comments on the library's own types. A caller reads these in IntelliSense without
 * ever opening the documentation, so they are the copy most likely to be read and the least likely
 * to be proofread.
 */
describe('every doc comment in the API folder', () => {
    for (const [source, copy] of Object.entries(API_SOURCES)) {
        it(`in ${source} keeps to the same copy rules`, () => {
            const broken = lint(copy, ALLOWED[source] ?? []);
            expect(broken, `${broken.length} paragraph(s) break a copy rule`).toEqual([]);
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
        // 15 help sentences, two legends and the shut row's word; one-word strings included, which
        // is why this source is imported rather than scanned.
        ['flags.ts', 18, 'Case-insensitive matching'],
        // Three strings per heading note, six notes.
        ['help-notes.ts', 18, 'A named list is a set of words'],
        // One sentence per unbounded letter - e, s, i, d - and one for a budget with several.
        ['budget.ts', 5, 'allows any number of errors'],
        ['DemoEngine.cs', 12, 'The page stays responsive'],
        ['index.html', 3, 'FuzzyRegex'],
        ['help generator', 1, 'GENERATED'],
    ])('finds at least %s strings in %s, including one saying %s', (source, floor, anchor) => {
        const copy = SOURCES[source as string] ?? [];
        expect(copy.length).toBeGreaterThanOrEqual(floor as number);
        expect(copy.some(({ text }) => text.includes(anchor as string))).toBe(true);
    });

    it.each([
        ['README.md', 30, 'fuzzy (approximate) matching'],
        ['docs/COMPARISON.md', 100, 'take the best fuzzy match rather than the first'],
        // Almost all of this file is one wide table, and a table cell is dropped. Nine blocks of
        // prose around it is what there is to lint, so the floor is low on purpose.
        ['docs/DIVERGENCES.md', 8, 'The Status column is load-bearing'],
        ['docs/ORACLE-INVARIANTS.md', 150, 'The list is an instrument'],
        ['docs/PORTMAP.md', 50, 'A wave is generated on demand and never committed'],
        // Generated by New-StatusReport in tools/PortTools.psm1: the parity board is a table, and
        // the handful of sentences around it are written in that function.
        ['docs/STATUS.md', 6, 'Parity against upstream commit'],
        ['docs/VERIFICATION.md', 15, 'A finding is a hypothesis'],
    ])('finds at least %s blocks in %s, including one saying %s', (source, floor, anchor) => {
        const copy = DOC_SOURCES[source as string] ?? [];
        expect(copy.length).toBeGreaterThanOrEqual(floor as number);
        expect(copy.some(({ text }) => text.includes(anchor as string))).toBe(true);
    });

    it.each([
        ['FuzzyCounts.cs', 8, 'How many errors of each kind a fuzzy match used'],
        ['FuzzyRegex.cs', 400, 'The named lists this instance was compiled with'],
        ['FuzzyRegexOptions.cs', 25, 'Where a .NET name and an upstream name exist'],
        ['FuzzyRegexParseException.cs', 12, 'Indices are UTF-16 code units'],
        ['Match.cs', 80, 'Every capture the group made, oldest first'],
        ['MatchCollections.cs', 40, 'The captures of one group, oldest first'],
        ['PatternCache.cs', 25, 'The bound a new cache starts at'],
    ])('finds at least %s paragraphs in %s, including one saying %s', (source, floor, anchor) => {
        const copy = API_SOURCES[source as string] ?? [];
        expect(copy.length).toBeGreaterThanOrEqual(floor as number);
        expect(copy.some(({ text }) => text.includes(anchor as string))).toBe(true);
    });

    it('reads the API directory, so a file cannot drop out of the lint unnoticed', () => {
        // The set comes from the directory rather than a list, so a new type is linted the day it
        // is written. What that cannot catch is a file disappearing from the read - a moved folder,
        // a changed filter - which is what these seven names are here for.
        expect(Object.keys(API_SOURCES)).toEqual(
            expect.arrayContaining([
                'FuzzyCounts.cs',
                'FuzzyRegex.cs',
                'FuzzyRegexOptions.cs',
                'FuzzyRegexParseException.cs',
                'Match.cs',
                'MatchCollections.cs',
                'PatternCache.cs',
            ]),
        );
    });

    it.each([
        ['a fenced code block', '```csharp\nvar m = Match("not a sentence, but code");\n```'],
        ['a table', '| Name | Meaning |\n|---|---|\n| `x` | not a sentence, but a cell |'],
    ])('drops %s, whatever it holds', (_what, markdown) => {
        expect(docParagraphs(`Kept.\n\n${markdown}\n\nAlso kept.`, 'x').map(({ text }) => text)).toEqual([
            'Kept.',
            'Also kept.',
        ]);
    });

    it('keeps a heading, a bullet and a quote as blocks of their own', () => {
        const markdown = '## A heading\n\n- first bullet here\n- second bullet here\n\n> a quoted line here';
        expect(docParagraphs(markdown, 'x').map(({ where, text }) => `${where} ${text}`)).toEqual([
            'x:1 A heading',
            'x:3 first bullet here',
            'x:4 second bullet here',
            'x:6 a quoted line here',
        ]);
    });

    it('takes out the code spans and the link targets, and keeps the words', () => {
        const markdown = 'The `Match` type is described in [the comparison](https://example.invalid/x).';
        expect(docParagraphs(markdown, 'x').map(({ text }) => text)).toEqual([
            'The type is described in the comparison.',
        ]);
    });

    it("lints the flags panel's one-word strings, which a literal scan drops", () => {
        // `literals` needs two words to tell a sentence from an identifier, so scanning flags.ts
        // linted "Character set" and not "Version" - a legend a visitor reads either way.
        const texts = (SOURCES['flags.ts'] ?? []).map(({ text }) => text);
        expect(texts).toContain('Version');
        expect(texts).toContain('none');
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
