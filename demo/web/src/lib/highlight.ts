// Turning an answer into something renderable, under a cap.
//
// Rendering is the main thread's job and the Web Worker cannot protect it (DemoEngine.cs, MaxSpans):
// the engine can answer promptly with hundreds of matches and the page can still die building a DOM
// node for each one. So the cap is applied to the MATCHES, before any segment is built, rather than
// to the segments afterwards - slicing after the work is done is a cap that costs exactly as much as
// having no cap.

import type { Span } from '../types';

import { MAX_DISPLAYED_MATCHES } from './caps';

export { MAX_DISPLAYED_MATCHES };

/** One run of the subject as the page paints it. `match` is null for the text between matches. */
export interface Segment {
    readonly text: string;
    /** The match's number in the full answer, or null for plain text. */
    readonly match: number | null;
}

/** The subject split into paintable runs, and how much of the answer they cover. */
export interface View {
    readonly segments: readonly Segment[];
    readonly shown: number;
    readonly total: number;
}

/**
 * Splits the subject into the runs the page paints: plain text, and one run per highlighted match.
 *
 * @param subject The text that was searched.
 * @param matches The engine's matches, in order.
 * @param cap The most matches to draw. Defaults to {@link MAX_DISPLAYED_MATCHES}.
 */
export function segments(
    subject: string,
    matches: readonly Span[] | undefined,
    cap: number = MAX_DISPLAYED_MATCHES,
): View {
    const all = matches ?? [];
    const drawn = all.slice(0, Math.max(0, cap));
    const pieces: Segment[] = [];
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
