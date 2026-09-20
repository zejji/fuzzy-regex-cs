/**
 * The selected match laid out one character at a time, so each error can be read on its own.
 *
 * The subject pane marks errors in place, which is the fast way to see WHERE a match went wrong.
 * It cannot say much about any one of them: the marks are as wide as the characters under them,
 * and a deletion is 6 px of dashed border. This is the other view - a cell per character, each one
 * named in full - and it is the path a keyboard and a screen reader have to the same facts, since
 * a highlight is a control and axe-core's `nested-interactive` rule refuses focusable elements
 * inside one (S75).
 *
 * The subject's side only. `fuzzy_changes` is three lists of subject positions and carries nothing
 * about which part of the pattern each error was spent against, and a pattern is not a sequence of
 * characters to begin with: `(?:colou?r|couleur){e<=2}` against "calor" answers
 * `fuzzy_changes=([1], [], [4])` and says nothing about which branch matched
 * (`tools/probes/s75-alignment-inputs.py`, regex 2026.9.10, 2026-09-20).
 */

import type { EditKind, Highlighted } from './highlight';

/** One character of the match, or one place where characters are missing from it. */
export interface AlignmentCell {
    /** The character, or the empty string for a deletion, which has no character of its own. */
    readonly text: string;
    /** What happened here, or null where the subject matched the pattern as it stands. */
    readonly kind: EditKind | null;
    /** Where this is in the subject, in UTF-16 code units, as the engine counts. */
    readonly index: number;
    /** How many characters are missing at this place. 1 everywhere else. */
    readonly count: number;
}

/**
 * The match as cells, or null when it spent no errors inside its own span.
 *
 * Null rather than a row of plain characters: an exact match has nothing to align, and a view that
 * appeared for every match would be a second copy of the subject under the first.
 */
export function alignment(subject: string, match: Highlighted): readonly AlignmentCell[] | null {
    const start = match.index;
    const end = match.index + match.length;
    const edits = match.edits;
    if (edits === undefined) return null;

    const kinds = new Map<number, EditKind>();
    const missing = new Map<number, number>();

    const mark = (at: number, kind: EditKind): void => {
        // First kind wins, as in `highlight.ts`: two errors on one character is not an answer the
        // engine gives, and picking the later one is no more true than picking the earlier.
        if (at >= start && at < end && !kinds.has(at)) kinds.set(at, kind);
    };

    for (const at of edits.substitutions) mark(at, 'sub');
    for (const at of edits.insertions) mark(at, 'ins');
    for (const at of edits.deletions) {
        // `<= end`: a deletion at the end of the match is the ordinary case of a pattern that
        // asked for one more character than the subject had.
        if (at < start || at > end) continue;
        missing.set(at, (missing.get(at) ?? 0) + 1);
    }

    if (kinds.size === 0 && missing.size === 0) return null;

    const cells: AlignmentCell[] = [];
    let at = start;
    while (at < end) {
        const gap = missing.get(at);
        if (gap !== undefined) cells.push({ text: '', kind: 'del', index: at, count: gap });

        // Iterated by code point, so a character outside the BMP is one cell. Sliced by code
        // units, because that is what the engine's indices count.
        const codepoint = subject.codePointAt(at);
        const width = codepoint !== undefined && codepoint > 0xffff ? 2 : 1;
        const text = subject.slice(at, Math.min(at + width, end));
        cells.push({ text, kind: kinds.get(at) ?? null, index: at, count: 1 });
        at += width;
    }

    const trailing = missing.get(end);
    if (trailing !== undefined) cells.push({ text: '', kind: 'del', index: end, count: trailing });

    return cells;
}
