import { expect, test } from 'vitest';

import { MAX_DISPLAYED_MATCHES, segments } from '../src/lib/highlight';
import type { Span } from '../src/types';

const texts = (result: ReturnType<typeof segments>) =>
    result.segments.map((s) => (s.match === null ? s.text : `[${s.text}]`)).join('');

/**
 * The edits inside the matches, as `kind:text` per run, so a test can name the whole breakdown.
 * A deletion run that stands for more than one missing character is written `del*5`.
 */
const runs = (result: ReturnType<typeof segments>) =>
    result.segments
        .filter((s) => s.match !== null)
        .map((s) =>
            (s.runs ?? [])
                .map((run) => `${run.kind ?? '-'}${run.count > 1 ? `*${run.count}` : ''}:${run.text}`)
                .join('|'),
        )
        .join(' ');

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

test('a fuzzy match is broken into the characters each error was spent on', () => {
    // Upstream's answer for (?:foobar){i<=1,d<=1,s<=1} against "xfoobat"
    // (tools/probes/demo-json-contract-expectations.py, run 2026-09-20 against regex 2026.9.10):
    // span (0,6), fuzzy_changes ([0], [1], [6]), and the deletion at subject position 6.
    const result = segments('xfoobat', [
        { index: 0, length: 6, edits: { substitutions: [0], insertions: [1], deletions: [6] } },
    ]);

    expect(texts(result)).toBe('[xfooba]t');
    expect(runs(result)).toBe('sub:x|ins:f|-:ooba|del:');
});

test('deletions in the same place are one gap carrying their count', () => {
    // (?:abcdef){d<=2} against "abef", read off the subject: "abef" is "abcdef" with "c" and "d"
    // missing, both from the one place, after "ab" and before "ef", which is subject position 2.
    // Upstream's own answer for the same match is `fuzzy_changes = ([], [], [2, 3])`, shifted as if
    // the deletions were put back; `DemoEngine.Edits` un-shifts it, and the derivation above is how
    // that 2,2 is checked without going through either implementation.
    //
    // Two gaps drawn side by side were 3 px of dashed border apart and read as one wide mark, and
    // the letters under them overlapped. One gap that says how many characters are missing carries
    // the same fact and can be read (S75, owner's finding 2026-09-20).
    const result = segments('abef', [
        { index: 0, length: 4, edits: { substitutions: [], insertions: [], deletions: [2, 2] } },
    ]);

    expect(runs(result)).toBe('-:ab|del*2:|-:ef');
});

test('a single deletion is a gap with no count to draw', () => {
    const result = segments('abef', [
        { index: 0, length: 4, edits: { substitutions: [], insertions: [], deletions: [2] } },
    ]);

    const [match] = result.segments.filter((s) => s.match !== null);
    expect(runs(result)).toBe('-:ab|del:|-:ef');
    expect(match?.runs?.find((run) => run.kind === 'del')?.count).toBe(1);
});

test('neighbouring errors of one kind are one run, so one letter marks them all', () => {
    // The first of the owner's five matches: `(foobar){e}` answers (0,6) on "xirefoabralfobarxie"
    // with six substitutions, one per character (tools/probes/s75-stacked-deletions.py and .cs,
    // 2026-09-20). One run per character drew six `s` letters in a row under one word.
    const result = segments('xirefoabralfobarxie', [
        { index: 0, length: 6, edits: { substitutions: [0, 1, 2, 3, 4, 5], insertions: [], deletions: [] } },
    ]);

    expect(runs(result)).toBe('sub*6:xirefo');
});

test('errors of different kinds side by side stay separate runs', () => {
    // Grouping is by kind, so a substitution next to an insertion is still two marks: they are two
    // colours, two underlines and two letters, and merging them would say the wrong thing.
    const result = segments('xfoobat', [
        { index: 0, length: 6, edits: { substitutions: [0], insertions: [1], deletions: [6] } },
    ]);

    expect(runs(result)).toBe('sub:x|ins:f|-:ooba|del:');
});

test('a gap breaks a run of one kind in two', () => {
    // "ab" and "cd" are both substituted, with a character missing between them. The gap is drawn
    // between the two, so the run cannot span it and each half keeps its own letter.
    const result = segments('abcd', [
        { index: 0, length: 4, edits: { substitutions: [0, 1, 2, 3], insertions: [], deletions: [2] } },
    ]);

    expect(runs(result)).toBe('sub*2:ab|del:|sub*2:cd');
});

test("the owner's stacked deletions become one counted gap and an empty match keeps its own", () => {
    // `(foobar){e}` against "xirefoabralfobarxie", the last two of its five matches. Upstream
    // answers (18,19) with 1 substitution and 5 deletions, then an empty (19,19) with 6, and this
    // port answers the same spans and counts; the deletion positions below are what DemoEngine
    // hands the page, un-shifted back into the subject. Both halves measured 2026-09-20 by
    // tools/probes/s75-stacked-deletions.py (regex 2026.9.10) and tools/probes/s75-stacked-deletions.cs.
    //
    // Drawn one mark per deletion this was eleven `d` letters on top of each other at one
    // x-position, which is what the owner saw.
    const result = segments('xirefoabralfobarxie', [
        { index: 18, length: 1, edits: { substitutions: [18], insertions: [], deletions: [19, 19, 19, 19, 19] } },
        {
            index: 19,
            length: 0,
            edits: { substitutions: [], insertions: [], deletions: [19, 19, 19, 19, 19, 19] },
        },
    ]);

    expect(runs(result)).toBe('sub:e|del*5: del*6:');
});

test('an exact match has no breakdown at all', () => {
    // The engine omits `edits` from an exact match, and a match that spent no error inside a fuzzy
    // walk carries three empty lists. Neither draws anything, and neither may cost a run.
    const [plain, spent] = segments('abcd', [
        { index: 0, length: 2 },
        { index: 2, length: 2, edits: { substitutions: [], insertions: [], deletions: [] } },
    ]).segments.filter((s) => s.match !== null);

    expect(plain?.runs).toBeUndefined();
    expect(spent?.runs).toBeUndefined();
});

test('an edit outside the match it belongs to is not drawn', () => {
    // No engine answer puts one there - the positions come from the match's own walk - but the
    // answer arrives as JSON from a worker, and a slice beyond the run would paint text that
    // belongs to the subject around the match as though it were inside it.
    const result = segments('abcdef', [
        { index: 2, length: 2, edits: { substitutions: [0, 5], insertions: [], deletions: [9] } },
    ]);

    expect(texts(result)).toBe('ab[cd]ef');
    expect(result.segments.filter((s) => s.match !== null)[0]?.runs).toBeUndefined();
});

test('an error spent on half a surrogate pair marks the whole character', () => {
    // The engine counts UTF-16 code units, so a position can land on one half of a pair. Slicing
    // there would put a lone surrogate in a text node, which browsers paint as U+FFFD - a subject
    // the demo has corrupted. Hand-built, like the overlap case below: it is a guard against a
    // shape the page can be handed, not a claim about what the engine answers.
    const result = segments('a\u{10400}b', [
        { index: 0, length: 4, edits: { substitutions: [2], insertions: [], deletions: [] } },
    ]);

    expect(runs(result)).toBe('-:a|sub:\u{10400}|-:b');
});

test('a match that ends inside a surrogate pair paints its own text and no more', () => {
    // Half a character at each end of the span is the other way a code-unit position lands badly,
    // and the whole-character rule above made it worse rather than better: widening the run to the
    // end of the pair took it past the end of the match, and the low half was then painted a second
    // time by the segment after it. Whatever the indices, the runs are the match's own text - that
    // is what the page renders instead of `text` when they are present.
    const result = segments('a\u{10400}b', [
        { index: 0, length: 2, edits: { substitutions: [1], insertions: [], deletions: [] } },
    ]);

    const match = result.segments.filter((s) => s.match !== null)[0];
    expect((match?.runs ?? []).map((run) => run.text).join('')).toBe(match?.text);
});

test('a match that starts inside a surrogate pair still draws the error at its first character', () => {
    // The mirror of it. The whole-character rule moves a position on the low half back to the high
    // half, and there the high half is outside the match: the run was keyed to a position the walk
    // never visits, so the error vanished while the chip beside the match still counted it.
    const result = segments('a\u{10400}b', [
        { index: 2, length: 2, edits: { substitutions: [2], insertions: [], deletions: [] } },
    ]);

    const match = result.segments.filter((s) => s.match !== null)[0];
    expect((match?.runs ?? []).map((run) => run.kind)).toContain('sub');
    expect((match?.runs ?? []).map((run) => run.text).join('')).toBe(match?.text);
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
