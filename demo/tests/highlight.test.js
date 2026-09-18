import assert from 'node:assert/strict';
import test from 'node:test';

import { MAX_DISPLAYED_MATCHES, segments } from '../FuzzyRegex.Demo.Wasm/wwwroot/lib/highlight.js';

const texts = (result) => result.segments.map((s) => (s.match === null ? s.text : `[${s.text}]`)).join('');

test('the subject comes back whole, with the matches marked', () => {
    // Upstream's answer for \d+ against "a1 b22 c333" (tools/probes/demo-json-contract-expectations.py,
    // run 2026-09-18 against regex 2026.9.10): (1,1), (4,2), (8,3).
    const result = segments('a1 b22 c333', [{ index: 1, length: 1 }, { index: 4, length: 2 }, { index: 8, length: 3 }]);
    assert.equal(texts(result), 'a[1] b[22] c[333]');
    assert.equal(result.shown, 3);
    assert.equal(result.total, 3);
});

test('a match at the very start and one at the very end lose nothing', () => {
    const result = segments('abc', [{ index: 0, length: 1 }, { index: 2, length: 1 }]);
    assert.equal(texts(result), '[a]b[c]');
});

test('no matches is the whole subject, unmarked', () => {
    const result = segments('abc', []);
    assert.deepEqual(result.segments, [{ text: 'abc', match: null }]);
    assert.equal(result.total, 0);
});

test('an empty subject produces nothing to draw', () => {
    assert.deepEqual(segments('', []).segments, []);
});

test('a zero-length match still gets a segment of its own', () => {
    // `a*` against "bbb" matches empty at every position. The group table will show a match, so the
    // highlighted view must not silently show nothing where it points.
    const result = segments('bb', [{ index: 0, length: 0 }, { index: 1, length: 0 }, { index: 2, length: 0 }]);
    assert.equal(result.segments.filter((s) => s.match !== null).length, 3);
    assert.equal(texts(result), '[]b[]b[]');
});

test('the cap limits the matches drawn and reports the true total', () => {
    const matches = Array.from({ length: 50 }, (_, i) => ({ index: i * 2, length: 1 }));
    const result = segments('x'.repeat(100), matches, 10);
    assert.equal(result.shown, 10);
    assert.equal(result.total, 50);
    assert.equal(result.segments.filter((s) => s.match !== null).length, 10);
});

test('the tail after the last drawn match is plain text, not lost', () => {
    // The Hunt in the slice file: a cap that caps the array and then renders nothing after it would
    // show a subject that ends where the cap fell, which reads as a truncated subject rather than as
    // a capped view.
    const result = segments('abcdef', [{ index: 0, length: 1 }, { index: 4, length: 1 }], 1);
    assert.equal(texts(result), '[a]bcdef');
});

test('the cap is applied before any segment is built', () => {
    // The other half of the same Hunt: capping after the work is a cap that costs what having no cap
    // costs. A getter that counts reads proves the slice happens first - only the drawn matches are
    // ever looked at.
    const read = new Set();
    const matches = Array.from({ length: 1000 }, (_, i) => {
        const match = { length: 1 };
        Object.defineProperty(match, 'index', { get() { read.add(i); return i * 2; } });
        return match;
    });

    segments('x'.repeat(2000), matches, 5);
    // WHICH matches were touched, not how many times each was: the count per match is an internal
    // detail (the overlap guard, two slices and the cursor all read it), but a match beyond the cap
    // being looked at at all is the bug.
    assert.deepEqual([...read], [0, 1, 2, 3, 4]);
});

test('the display cap is below the engine\'s own match cap', () => {
    // DemoEngine.MaxMatches is 1,000. A display cap at or above it would never fire, and the "showing
    // N of M" line would be dead code that nobody noticed was dead.
    assert.ok(MAX_DISPLAYED_MATCHES < 1000);
});

test('an out-of-order match is skipped rather than rendered as an empty slice', () => {
    const result = segments('abcdef', [{ index: 2, length: 2 }, { index: 0, length: 1 }]);
    assert.equal(texts(result), 'ab[cd]ef');
});
