// The boundary guards, on the members v2 added.
//
// These are not type tests. Everything below arrives as JSON at runtime - from a worker that may be
// a cached older deployment, from an examples.json somebody edited, from a help.json a generator
// wrote - so what is asserted here is what the page does with text that type-checks against nothing
// at all. The v1 members are covered by the tests that drive the page; these are the new ones.

import { expect, test } from 'vitest';

import { isExampleList, isHelp, parseReply } from '../src/lib/shapes';

/** One match in the engine's shape, so a test can vary a single member of it. */
const match = (extra: Record<string, unknown> = {}) => ({
    index: 0,
    length: 1,
    counts: { substitutions: 0, insertions: 0, deletions: 0 },
    groups: [],
    ...extra,
});

const reply = (value: unknown) => parseReply(JSON.stringify(value));

test('the rewritten subject is read, and anything that is not a string in its place is refused', () => {
    expect(reply({ matches: [match()], truncated: false, replaced: 'bb' }).replaced).toBe('bb');
    expect(reply({ matches: [match()], truncated: false, replaced: 12 }).error).toContain('not an answer');
});

test('a partial match says so with a boolean, and with nothing else', () => {
    expect(reply({ matches: [match({ partialMatch: true })] }).matches?.[0]?.partialMatch).toBe(true);
    // Absent on every match that is not partial, which is most of them.
    expect(reply({ matches: [match()] }).matches?.[0]?.partialMatch).toBeUndefined();
    expect(reply({ matches: [match({ partialMatch: 'yes' })] }).error).toContain('not an answer');
});

test('a parse error may carry the position it failed at, and it must be a number', () => {
    expect(reply({ error: 'missing )', errorOffset: 1 }).errorOffset).toBe(1);
    expect(reply({ error: 'missing )' }).errorOffset).toBeUndefined();
    expect(reply({ error: 'missing )', errorOffset: '1' }).error).toContain('not an answer');
});

test('an example may name a feature, a mode, a template and word lists', () => {
    const full = [
        {
            key: 'replace',
            title: 'Replace with a template',
            note: 'why it matters',
            pattern: '(a)',
            flags: '',
            subject: 'aa',
            mode: 'replace',
            replacement: '\\1!',
            namedLists: '',
        },
    ];
    expect(isExampleList(full)).toBe(true);

    // The four new members are optional - the syntax tour rows carry none of them - but a member
    // that is present and is not text is a file the page cannot drive the engine from.
    const bare = [{ title: 't', note: 'n', pattern: 'a', flags: '', subject: 'a' }];
    expect(isExampleList(bare)).toBe(true);
    expect(isExampleList([{ ...bare[0], mode: 3 }])).toBe(false);
    expect(isExampleList([{ ...bare[0], key: null }])).toBe(false);
});

test('help is read only in the shape the generator writes', () => {
    const generated = {
        source: 'docs/COMPARISON.md',
        note: 'GENERATED',
        entries: {
            posix: [
                {
                    heading: [{ code: false, text: 'Leftmost-longest' }],
                    blocks: [
                        {
                            kind: 'paragraph',
                            runs: [
                                { code: true, text: 'Posix' },
                                { code: false, text: ' does it' },
                            ],
                        },
                        { kind: 'code', language: 'csharp', text: 'var x = 1;' },
                    ],
                },
            ],
        },
    };
    expect(isHelp(generated)).toBe(true);

    // A key with no sections is legitimate JSON and a blank panel on screen, so it is refused here
    // rather than rendered as an empty disclosure nobody can tell from a missing one.
    expect(isHelp({ ...generated, entries: { posix: [] } })).toBe(false);
    expect(isHelp({ ...generated, entries: { posix: 'some prose' } })).toBe(false);
    expect(isHelp({ entries: {} })).toBe(false);
    expect(isHelp(null)).toBe(false);

    // An unknown block kind is the shape drifting away from the generator, which is the failure the
    // fail-on-rename guarantee exists to catch at build time and this catches at load time.
    const strange = structuredClone(generated) as typeof generated;
    (strange.entries.posix[0]!.blocks[0] as { kind: string }).kind = 'table';
    expect(isHelp(strange)).toBe(false);
});
