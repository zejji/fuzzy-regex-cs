// The boundary guards, on the members v2 added.
//
// These are not type tests. Everything below arrives as JSON at runtime - from a worker that may be
// a cached older deployment, from an examples.json somebody edited, from a help.json a generator
// wrote - so what is asserted here is what the page does with text that type-checks against nothing
// at all. The v1 members are covered by the tests that drive the page; these are the new ones.

import { expect, test } from 'vitest';

import { MAX_PATTERN_LENGTH } from '../src/lib/caps';
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
    expect(reply({ matches: [match()], truncated: false, replaced: 12 }).error).toContain('wrong shape');
});

test('a partial match says so with a boolean, and with nothing else', () => {
    expect(reply({ matches: [match({ partialMatch: true })] }).matches?.[0]?.partialMatch).toBe(true);
    // Absent on every match that is not partial, which is most of them.
    expect(reply({ matches: [match()] }).matches?.[0]?.partialMatch).toBeUndefined();
    expect(reply({ matches: [match({ partialMatch: 'yes' })] }).error).toContain('wrong shape');
});

test('the breakdown of a fuzzy match is three lists of numbers, or it is not believed', () => {
    // The highlighter iterates all three (`for (const at of edits.substitutions)`), and it runs
    // inside a computed the page renders: a member that is present and is not a list throws out of
    // a render rather than being caught, which is a blank page and a console message. Absent is a
    // real answer - the engine omits `edits` from a match that spent nothing.
    const edits = { substitutions: [1], insertions: [], deletions: [2] };
    expect(reply({ matches: [match({ edits })] }).matches?.[0]?.edits).toEqual(edits);
    expect(reply({ matches: [match()] }).matches?.[0]?.edits).toBeUndefined();

    expect(reply({ matches: [match({ edits: { ...edits, substitutions: 1 } })] }).error).toContain('wrong shape');
    expect(reply({ matches: [match({ edits: { insertions: [], deletions: [] } })] }).error).toContain('wrong shape');
    expect(reply({ matches: [match({ edits: { ...edits, deletions: ['2'] } })] }).error).toContain('wrong shape');
    expect(reply({ matches: [match({ edits: 'two' })] }).error).toContain('wrong shape');
});

test('a parse error may carry the position it failed at, and it must be a number', () => {
    expect(reply({ error: 'missing )', errorOffset: 1 }).errorOffset).toBe(1);
    expect(reply({ error: 'missing )' }).errorOffset).toBeUndefined();
    expect(reply({ error: 'missing )', errorOffset: '1' }).error).toContain('wrong shape');
});

test('a parse error position must be an index a pattern the page could have sent really has', () => {
    // App.vue draws the caret with `' '.repeat(errorOffset)`, so this member is not read, it is
    // EXECUTED. A negative one throws RangeError out of a render - a blank page, not a bad caret -
    // and a large one builds a string of that many spaces on the main thread. Neither is a reply
    // this engine sends; both are what a cached older worker or a hand-driven one could send.
    expect(reply({ error: 'missing )', errorOffset: 0 }).errorOffset).toBe(0);
    // The end of the longest pattern the page will send is a real position to fail at: "missing )"
    // is reported at the character after the last one.
    expect(reply({ error: 'missing )', errorOffset: MAX_PATTERN_LENGTH }).errorOffset).toBe(MAX_PATTERN_LENGTH);

    expect(reply({ error: 'missing )', errorOffset: -1 }).error).toContain('wrong shape');
    expect(reply({ error: 'missing )', errorOffset: 1.5 }).error).toContain('wrong shape');
    expect(reply({ error: 'missing )', errorOffset: MAX_PATTERN_LENGTH + 1 }).error).toContain('wrong shape');
    expect(reply({ error: 'missing )', errorOffset: 1e9 }).error).toContain('wrong shape');
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

    // And a section with no BLOCKS is the same failure one level down: the generator maps a
    // heading whose prose has since moved under a sub-heading, writes the section with an empty
    // body, and the panel opens onto nothing. Refused here as well as at build time.
    const hollow = { heading: [{ code: false, text: 'Leftmost-longest' }], blocks: [] };
    expect(isHelp({ ...generated, entries: { posix: [hollow] } })).toBe(false);

    expect(isHelp({ ...generated, entries: { posix: 'some prose' } })).toBe(false);
    expect(isHelp({ entries: {} })).toBe(false);
    expect(isHelp(null)).toBe(false);

    // An unknown block kind is the shape drifting away from the generator, which is the failure the
    // fail-on-rename guarantee exists to catch at build time and this catches at load time.
    const strange = structuredClone(generated) as typeof generated;
    (strange.entries.posix[0]!.blocks[0] as { kind: string }).kind = 'table';
    expect(isHelp(strange)).toBe(false);
});
