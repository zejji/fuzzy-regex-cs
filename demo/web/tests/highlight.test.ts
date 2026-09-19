import { expect, test } from 'vitest';

import { MAX_DISPLAYED_MATCHES, segments } from '../src/lib/highlight';
import type { Span } from '../src/types';

const texts = (result: ReturnType<typeof segments>) =>
    result.segments.map((s) => (s.match === null ? s.text : `[${s.text}]`)).join('');

test('the subject comes back whole, with the matches marked', () => {
    // Upstream's answer for \d+ against "a1 b22 c333" (tools/probes/demo-json-contract-expectations.py,
    // run 2026-09-18 against regex 2026.9.10): (1,1), (4,2), (8,3).
    const result = segments('a1 b22 c333', [
        { index: 1, length: 1 },
        { index: 4, length: 2 },
        { index: 8, length: 3 },
    ]);
    expect(texts(result)).toBe('a[1] b[22] c[333]');
    expect(result.shown).toBe(3);
    expect(result.total).toBe(3);
});

test('a match at the very start and one at the very end lose nothing', () => {
    const result = segments('abc', [
        { index: 0, length: 1 },
        { index: 2, length: 1 },
    ]);
    expect(texts(result)).toBe('[a]b[c]');
});

test('no matches is the whole subject, unmarked', () => {
    const result = segments('abc', []);
    expect(result.segments).toEqual([{ text: 'abc', match: null }]);
    expect(result.total).toBe(0);
});

test('an empty subject produces nothing to draw', () => {
    expect(segments('', []).segments).toEqual([]);
});

test('a zero-length match still gets a segment of its own', () => {
    // `a*` against "bbb" matches empty at every position. The group table will show a match, so the
    // highlighted view must not silently show nothing where it points.
    const result = segments('bb', [
        { index: 0, length: 0 },
        { index: 1, length: 0 },
        { index: 2, length: 0 },
    ]);
    expect(result.segments.filter((s) => s.match !== null)).toHaveLength(3);
    expect(texts(result)).toBe('[]b[]b[]');
});

test('the cap limits the matches drawn and reports the true total', () => {
    const matches: Span[] = Array.from({ length: 50 }, (_, i) => ({ index: i * 2, length: 1 }));
    const result = segments('x'.repeat(100), matches, 10);
    expect(result.shown).toBe(10);
    expect(result.total).toBe(50);
    expect(result.segments.filter((s) => s.match !== null)).toHaveLength(10);
});

test('the tail after the last drawn match is plain text, not lost', () => {
    // The Hunt in the slice file: a cap that caps the array and then renders nothing after it would
    // show a subject that ends where the cap fell, which reads as a truncated subject rather than as
    // a capped view.
    const result = segments(
        'abcdef',
        [
            { index: 0, length: 1 },
            { index: 4, length: 1 },
        ],
        1,
    );
    expect(texts(result)).toBe('[a]bcdef');
});

test('the cap is applied before any segment is built', () => {
    // The other half of the same Hunt: capping after the work is a cap that costs what having no cap
    // costs. A getter that counts reads proves the slice happens first - only the drawn matches are
    // ever looked at.
    const read = new Set<number>();
    const matches: Span[] = Array.from({ length: 1000 }, (_, i) => {
        const match = { length: 1 };
        Object.defineProperty(match, 'index', {
            get() {
                read.add(i);
                return i * 2;
            },
        });
        return match as Span;
    });

    segments('x'.repeat(2000), matches, 5);
    // WHICH matches were touched, not how many times each was nor in what order: both are internal
    // details (the overlap guard, two slices, the cursor and the sort into subject order all read
    // the index), but a match beyond the cap being looked at at all is the bug.
    expect([...read].sort((a, b) => a - b)).toEqual([0, 1, 2, 3, 4]);
});

test("the display cap is below the engine's own match cap", () => {
    // DemoEngine.MaxMatches is 1,000. A display cap at or above it would never fire, and the "showing
    // N of M" line would be dead code that nobody noticed was dead.
    expect(MAX_DISPLAYED_MATCHES).toBeLessThan(1000);
});

test('a right-to-left answer paints every match, in subject order', () => {
    // RightToLeft searches from the end, so the engine numbers the LAST match in the subject first.
    // Upstream's answer for \w+ with REVERSE against "one two three"
    // (tests/FuzzyRegex.Tests/Gaps/Demo/DemoExamplesTests.cs, from regex 2026.9.10): (8,5), (4,3),
    // (0,3), in that order. The subject is still painted left to right.
    const result = segments('one two three', [
        { index: 8, length: 5 },
        { index: 4, length: 3 },
        { index: 0, length: 3 },
    ]);
    expect(texts(result)).toBe('[one] [two] [three]');
    expect(result.segments.filter((s) => s.match !== null)).toHaveLength(3);
});

test('a match keeps its number in the answer, whatever order it is painted in', () => {
    // The table numbers matches as the engine found them, and the highlight's label, its selection
    // and its alternating tone all key off that number. Painting in subject order must not renumber.
    const result = segments('one two three', [
        { index: 8, length: 5 },
        { index: 4, length: 3 },
        { index: 0, length: 3 },
    ]);
    expect(result.segments.filter((s) => s.match !== null).map((s) => s.match)).toEqual([2, 1, 0]);
});

test('a zero-length match sharing a start with a longer one is still painted', () => {
    // The engine DOES answer with two matches at the same index: a reverse search finds the longer
    // one first and then the empty one at its start. Upstream, regex 2026.9.10, 2026-09-19:
    //   regex.finditer(r'a*', 'baa', flags=regex.REVERSE|regex.VERSION1)
    //   -> [(1, 3), (1, 1), (0, 0)]
    // This port answers the same, as spans: [1,2], [1,0], [0,0]. The empty match fits before the
    // longer one starts, so all three are paintable and none may be dropped.
    const result = segments('baa', [
        { index: 1, length: 2 },
        { index: 1, length: 0 },
        { index: 0, length: 0 },
    ]);
    expect(result.segments.filter((s) => s.match !== null).map((s) => s.match)).toEqual([2, 1, 0]);
    expect(texts(result)).toBe('[]b[][aa]');
});

test('an overlapping match is skipped rather than rendered as an empty slice', () => {
    // Two matches that share characters cannot both be painted in one flat run of text, and a
    // negative slice length renders as an empty string rather than as an error - the kind of silent
    // wrongness the demo exists to not have. No engine walk produces this; a hand-written fragment
    // or the console can.
    const result = segments('abcdef', [
        { index: 2, length: 2 },
        { index: 3, length: 2 },
    ]);
    expect(texts(result)).toBe('ab[cd]ef');
});
