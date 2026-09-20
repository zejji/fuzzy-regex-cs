/**
 * The selected match, character by character.
 *
 * The engine's `fuzzy_changes` is three lists of SUBJECT positions and says nothing about which
 * part of the pattern an error was spent against, so the view is the subject's side of the
 * alignment. Measured 2026-09-20 with regex 2026.9.10, `tools/probes/s75-alignment-inputs.py`:
 *
 *   (?:colour){e<=2}            'calor'    span=(0, 5) fuzzy_changes=([1], [], [4])
 *   (?:foobar){i<=1,d<=1,s<=1}  'xfoobat'  span=(0, 6) fuzzy_changes=([0], [1], [6])
 *   (?:colou?r|couleur){e<=2}   'calor'    span=(0, 5) fuzzy_changes=([1], [], [4])
 *
 * The third row is why there is no pattern row: `colou?r|couleur` has no fixed sequence of
 * characters to put above the subject, and the answer carries no record of the branch taken.
 */
import { expect, test } from 'vitest';

import { alignment } from '../src/lib/alignment';

/** Each cell as `character:kind`, with a deletion's count when it stands for more than one. */
const cells = (subject: string, match: Parameters<typeof alignment>[1]): string =>
    (alignment(subject, match) ?? [])
        .map((cell) => `${cell.text}:${cell.kind ?? '-'}${cell.count > 1 ? `*${cell.count}` : ''}@${cell.index}`)
        .join(' ');

test('a substitution and a deletion each get their own cell', () => {
    // (?:colour){e<=2} against "calor": "a" stands where the pattern wanted "o", and the "u" the
    // pattern wanted before "r" is missing from the subject altogether.
    expect(cells('calor', { index: 0, length: 5, edits: { substitutions: [1], insertions: [], deletions: [4] } })).toBe(
        'c:-@0 a:sub@1 l:-@2 o:-@3 :del@4 r:-@4',
    );
});

test('an insertion is a character of the subject the pattern never asked for', () => {
    expect(
        cells('xfoobat', { index: 0, length: 6, edits: { substitutions: [0], insertions: [1], deletions: [6] } }),
    ).toBe('x:sub@0 f:ins@1 o:-@2 o:-@3 b:-@4 a:-@5 :del@6');
});

test('characters missing from one place are one cell that says how many', () => {
    expect(cells('abef', { index: 0, length: 4, edits: { substitutions: [], insertions: [], deletions: [2, 2] } })).toBe(
        'a:-@0 b:-@1 :del*2@2 e:-@2 f:-@3',
    );
});

test('a match with no errors has nothing to align', () => {
    expect(alignment('colour', { index: 0, length: 6 })).toBeNull();
    expect(
        alignment('colour', { index: 0, length: 6, edits: { substitutions: [], insertions: [], deletions: [] } }),
    ).toBeNull();
});

test('a character outside the BMP is one cell, not two', () => {
    // "😀" is two UTF-16 code units, and the engine counts code units. A cell per unit would be two
    // halves of a surrogate pair, each of which a browser paints as U+FFFD.
    expect(cells('a😀b', { index: 0, length: 4, edits: { substitutions: [1], insertions: [], deletions: [] } })).toBe(
        'a:-@0 😀:sub@1 b:-@3',
    );
});

test('an error the match does not cover is not drawn', () => {
    // The engine cannot produce one - the positions come from the match's own walk - but the answer
    // arrives as JSON from a worker, and a cell outside the match would describe someone else's.
    expect(cells('abcdef', { index: 2, length: 2, edits: { substitutions: [0], insertions: [5], deletions: [] } })).toBe(
        '',
    );
});

test('the cells cover the match and nothing but the match', () => {
    const drawn = alignment('xfoobat', {
        index: 1,
        length: 5,
        edits: { substitutions: [2], insertions: [], deletions: [] },
    });
    expect((drawn ?? []).map((cell) => cell.text).join('')).toBe('fooba');
});
