// Turning an answer into something renderable, under a cap.
//
// Rendering is the main thread's job and the Web Worker cannot protect it (DemoEngine.cs, MaxSpans):
// the engine can answer promptly with hundreds of matches and the page can still die building a DOM
// node for each one. So the cap is applied to the MATCHES, before any segment is built, rather than
// to the segments afterwards - slicing after the work is done is a cap that costs exactly as much as
// having no cap.

import { MAX_DISPLAYED_MATCHES } from './caps.js';

export { MAX_DISPLAYED_MATCHES };

/**
 * Splits the subject into the runs the page paints: plain text, and one run per highlighted match.
 *
 * @param {string} subject The text that was searched.
 * @param {Array<{index: number, length: number}>} matches The engine's matches, in order.
 * @param {number} [cap] The most matches to draw. Defaults to {@link MAX_DISPLAYED_MATCHES}.
 * @returns {{segments: Array<{text: string, match: number | null}>, shown: number, total: number}}
 *   `match` is the match's number in the full answer, or null for the text between matches.
 */
export function segments(subject, matches, cap = MAX_DISPLAYED_MATCHES) {
    const all = matches ?? [];
    const drawn = all.slice(0, Math.max(0, cap));
    const pieces = [];
    let at = 0;

    for (const [number, match] of drawn.entries()) {
        // An engine walk returns matches in order and never overlapping, so this cannot fire from
        // the engine. It can fire from a hand-written fragment or the console, and a negative slice
        // length renders as an empty string rather than as an error - which is the kind of silent
        // wrongness the demo exists to not have.
        if (match.index < at) continue;

        if (match.index > at) {
            pieces.push({ text: subject.slice(at, match.index), match: null });
        }

        // A zero-length match (`a*` against "bbb") still needs its own segment: it is the answer,
        // and a page that skipped it would show nothing where the group table shows a match.
        pieces.push({ text: subject.slice(match.index, match.index + match.length), match: number });
        at = match.index + match.length;
    }

    if (at < subject.length) {
        pieces.push({ text: subject.slice(at), match: null });
    }

    return { segments: pieces, shown: drawn.length, total: all.length };
}
